using Cratebase.Api.Http;
using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Releases;

public static class ReleasesEndpointRouteBuilderExtensions
{
    private const string LabelForeignKeyConstraintName = "FK_releases_labels_label_id";
    private const string OtherTypeCode = "other";

    public static IEndpointRouteBuilder MapReleasesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/releases").WithTags("Releases");
        _ = group.MapPost("/", CreateReleaseAsync).WithName("CreateRelease");
        _ = group.MapGet("/{releaseId:guid}", GetReleaseAsync).WithName("GetRelease");
        _ = group.MapGet("/", ListReleasesAsync).WithName("ListReleases");
        _ = group.MapPut("/{releaseId:guid}", UpdateReleaseAsync).WithName("UpdateRelease");
        _ = group.MapDelete("/{releaseId:guid}", DeleteReleaseAsync).WithName("DeleteRelease");

        return endpoints;
    }

    private static async Task<IResult> CreateReleaseAsync(
        ReleaseRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            Release release = ApplyReleaseRequest(Release.Create(ReleaseId.New(), request.Title), request);
            IRepository<Release, ReleaseId> releases = unitOfWork.GetRepository<Release, ReleaseId>();
            releases.Add(release);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/releases/{release.Id}", ToReleaseResponse(release));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (PersistenceConflictException exception) when (IsLabelConflict(exception))
        {
            return EndpointErrors.Conflict("release.label_conflict", "Release label does not exist");
        }
    }

    private static async Task<IResult> GetReleaseAsync(Guid releaseId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        Release? release = await context.Releases.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new ReleaseId(releaseId), cancellationToken);

        return release is null
            ? EndpointErrors.NotFound("release.not_found", "Release was not found")
            : Results.Ok(ToReleaseResponse(release));
    }

    private static async Task<IResult> ListReleasesAsync(
        string? search,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<Release> releases = context.Releases.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            releases = releases.Where(release => EF.Functions.ILike(release.Summary.Title, pattern));
        }

        int total = await releases.CountAsync(cancellationToken);
        Release[] items = await releases
            .OrderBy(release => release.Summary.Title)
            .ThenBy(release => release.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<ReleaseResponse>([.. items.Select(ToReleaseResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateReleaseAsync(
        Guid releaseId,
        ReleaseRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<Release, ReleaseId> releases = unitOfWork.GetRepository<Release, ReleaseId>();
        Release? release = await releases.TryFindAsync(new ReleaseId(releaseId), cancellationToken);
        if (release is null)
        {
            return EndpointErrors.NotFound("release.not_found", "Release was not found");
        }

        try
        {
            _ = ApplyReleaseRequest(release, request);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToReleaseResponse(release));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (PersistenceConflictException exception) when (IsLabelConflict(exception))
        {
            return EndpointErrors.Conflict("release.label_conflict", "Release label does not exist");
        }
    }

    private static async Task<IResult> DeleteReleaseAsync(
        Guid releaseId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "release", releaseId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<Release, ReleaseId> releases = unitOfWork.GetRepository<Release, ReleaseId>();
        Release? release = await releases.TryFindAsync(new ReleaseId(releaseId), cancellationToken);
        if (release is null)
        {
            return EndpointErrors.NotFound("release.not_found", "Release was not found");
        }

        try
        {
            releases.Delete(release);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (PersistenceConflictException exception) when (IsReferentialIntegrityConflict(exception))
        {
            return EndpointErrors.Conflict("release.delete_conflict", "Release has dependent data");
        }
    }

    private static Release ApplyReleaseRequest(Release release, ReleaseRequest request)
    {
        ReleaseMetadata metadata = ReleaseMetadata.Empty.WithType(ParseReleaseType(request.Type ?? string.Empty));
        if (request.LabelId is { } labelId)
        {
            metadata = metadata.WithLabel(new LabelId(labelId));
        }

        if (request.Year is { } year)
        {
            metadata = metadata.WithReleaseYear(year);
        }

        release.UpdateSummary(ReleaseSummary.Create(request.Title).WithMetadata(metadata));
        release.UpdateCataloging(CatalogingMapper.Create(request.Genres, request.Tags));

        return release;
    }

    private static ReleaseResponse ToReleaseResponse(Release release)
    {
        ReleaseMetadata metadata = release.Summary.Metadata;

        return new ReleaseResponse(
            release.Id.Value,
            release.Summary.Title,
            ToReleaseTypeCode(metadata.Type),
            metadata.LabelId.HasValue ? metadata.LabelId.Match(value => value.Value, () => Guid.Empty) : null,
            metadata.Year.HasValue ? metadata.Year.Match(value => value, () => 0) : null,
            [.. release.Cataloging.Genres.Select(genre => genre.Name)],
            [.. release.Cataloging.Tags.Select(tag => tag.Name)]);
    }

    private static ReleaseType ParseReleaseType(string type)
    {
        return type.Trim() switch
        {
            "" => ReleaseType.Unknown,
            "unknown" => ReleaseType.Unknown,
            "album" => ReleaseType.Album,
            "ep" => ReleaseType.Ep,
            "standalone" => ReleaseType.Standalone,
            "compilation" => ReleaseType.Compilation,
            "bootleg" => ReleaseType.Bootleg,
            "mixtape" => ReleaseType.Mixtape,
            "promo" => ReleaseType.Promo,
            OtherTypeCode => ReleaseType.Other,
            _ => throw new DomainException("release.type_invalid", "Release type is invalid")
        };
    }

    private static string ToReleaseTypeCode(ReleaseType type)
    {
        return type switch
        {
            ReleaseType.Unknown => "unknown",
            ReleaseType.Album => "album",
            ReleaseType.Ep => "ep",
            ReleaseType.Standalone => "standalone",
            ReleaseType.Compilation => "compilation",
            ReleaseType.Bootleg => "bootleg",
            ReleaseType.Mixtape => "mixtape",
            ReleaseType.Promo => "promo",
            ReleaseType.Other => OtherTypeCode,
            _ => throw new InvalidOperationException("Release type is not supported")
        };
    }

    private static bool IsLabelConflict(PersistenceConflictException exception)
    {
        return IsReferentialIntegrityConflict(exception) &&
            string.Equals(exception.ConstraintName, LabelForeignKeyConstraintName, StringComparison.Ordinal);
    }

    private static bool IsReferentialIntegrityConflict(PersistenceConflictException exception)
    {
        return exception.Kind is PersistenceConflictKind.ForeignKeyViolation or PersistenceConflictKind.ReferentialIntegrityViolation;
    }
}
