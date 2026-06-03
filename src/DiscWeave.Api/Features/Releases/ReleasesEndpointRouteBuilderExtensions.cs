using DiscWeave.Api.Auth;
using DiscWeave.Api.Features.ExternalSources;
using DiscWeave.Api.Http;
using DiscWeave.Application.Errors;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DiscWeave.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private const string LabelConflictCode = "release.label_conflict";
    private const string LabelMissingMessage = "Release label does not exist";

    public static IEndpointRouteBuilder MapReleasesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/releases")
            .WithTags("Releases")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreateReleaseAsync).WithName("CreateRelease");
        _ = group.MapGet("/{releaseId:guid}", GetReleaseAsync).WithName("GetRelease");
        _ = group.MapGet("", ListReleasesAsync).WithName("ListReleases");
        _ = group.MapPut("/{releaseId:guid}", UpdateReleaseAsync).WithName("UpdateRelease");
        _ = group.MapDelete("/{releaseId:guid}", DeleteReleaseAsync).WithName("DeleteRelease");
        _ = group.MapGet("/{releaseId:guid}/naming-override", GetReleaseNamingOverrideAsync).WithName("GetReleaseNamingOverride");
        _ = group.MapPut("/{releaseId:guid}/naming-override", PutReleaseNamingOverrideAsync).WithName("PutReleaseNamingOverride");
        _ = group.MapDelete("/{releaseId:guid}/naming-override", DeleteReleaseNamingOverrideAsync).WithName("DeleteReleaseNamingOverride");
        _ = group.MapPut("/{releaseId:guid}/cover-image", PutReleaseCoverImageAsync).WithName("PutReleaseCoverImage");
        _ = group.MapGet("/{releaseId:guid}/cover-image", GetReleaseCoverImageAsync).WithName("GetReleaseCoverImage");
        _ = group.MapDelete("/{releaseId:guid}/cover-image", DeleteReleaseCoverImageAsync).WithName("DeleteReleaseCoverImage");

        return endpoints;
    }

    private static async Task<IResult> CreateReleaseAsync(
        ReleaseRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            Release release = await CreateReleaseEntryAsync(request, context, currentCollection.CollectionId, cancellationToken);

            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            ReleaseResponse response = await ToReleaseResponseAsync(release, context, currentCollection.CollectionId, cancellationToken);

            return Results.Created($"/api/releases/{release.Id}", response);
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ReferencedResourceMissingException)
        {
            return EndpointErrors.Conflict(LabelConflictCode, LabelMissingMessage);
        }
    }

    private static async Task<IResult> GetReleaseAsync(
        Guid releaseId,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Release? release = await context.Releases.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ReleaseId(releaseId),
            cancellationToken);

        return release is null
            ? EndpointErrors.NotFound("release.not_found", "Release was not found")
            : Results.Ok(await ToReleaseResponseAsync(release, context, currentCollection.CollectionId, cancellationToken));
    }

    private static async Task<IResult> ListReleasesAsync(
        string? search,
        int? limit,
        int? offset,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<Release> releases = context.Releases.AsNoTracking().Where(release => release.CollectionId == currentCollection.CollectionId);
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

        IReadOnlyList<ReleaseResponse> responses = await ToReleaseResponsesAsync(items, context, currentCollection.CollectionId, cancellationToken);

        return Results.Ok(new ListResponse<ReleaseResponse>(responses, normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateReleaseAsync(
        Guid releaseId,
        ReleaseRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Release? release = await context.Releases.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ReleaseId(releaseId),
            cancellationToken);
        if (release is null)
        {
            return EndpointErrors.NotFound("release.not_found", "Release was not found");
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _ = await ApplyReleaseRequestAsync(
                release,
                request,
                context,
                currentCollection.CollectionId,
                cancellationToken);
            IReadOnlyList<ResolvedCredit> releaseCredits = await ResolveCreditsAsync(
                request.ArtistCredits,
                context,
                currentCollection.CollectionId,
                cancellationToken);
            if (!request.IsVariousArtists && !releaseCredits.Any(credit => credit.Role == "mainArtist"))
            {
                throw new DomainException("release.artist_required", "Release artist is required unless the release is marked as Various Artists");
            }

            IReadOnlyList<ReleaseLabel> labels = await ResolveLabelsAsync(request, context, currentCollection.CollectionId, cancellationToken);
            release.UpdateLabels(request.NotOnLabel, labels);
            if (request.ExternalSources is not null)
            {
                release.ReplaceExternalSources(ExternalSourceReferenceMapper.FromRequests(request.ExternalSources, DateTimeOffset.UtcNow));
            }

            await ReplaceReleaseCreditsAsync(release, releaseCredits, context, currentCollection.CollectionId, cancellationToken);
            if (request.Tracklist is not null)
            {
                await ReplaceReleaseTracklistAsync(
                    request,
                    release,
                    releaseCredits,
                    context,
                    currentCollection.CollectionId,
                    cancellationToken);
            }

            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await ToReleaseResponseAsync(release, context, currentCollection.CollectionId, cancellationToken));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ReferencedResourceMissingException)
        {
            return EndpointErrors.Conflict(LabelConflictCode, LabelMissingMessage);
        }
    }

    private static async Task<Release> ApplyReleaseRequestAsync(
        Release release,
        ReleaseRequest request,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        string releaseType = await ResolveReleaseTypeCodeAsync(context, collectionId, request.Type, cancellationToken);
        IReadOnlyList<string> genres = await ResolveGenreCodesAsync(
            context,
            collectionId,
            request.Genres,
            cancellationToken);

        ReleaseMetadata metadata = ReleaseMetadata.Empty.WithType(releaseType);
        if (release.Summary.Metadata.CoverImage is PresentOptionalValue<CoverImage> { Value: CoverImage coverImage })
        {
            metadata = metadata.WithCoverImage(coverImage);
        }

        if (request.LabelId is not null && request.Labels is { Count: > 0 })
        {
            throw new DomainException("release.label_shape_invalid", "Release request must use either labelId or labels, not both");
        }

        if (!request.NotOnLabel)
        {
            Guid? firstLabelId = request.Labels is { Count: > 0 }
                ? request.Labels.FirstOrDefault(label => label.LabelId is not null)?.LabelId
                : request.LabelId;
            if (firstLabelId is { } labelId)
            {
                metadata = metadata.WithLabel(new LabelId(labelId));
            }
        }

        if (request.Year is { } year)
        {
            metadata = metadata.WithReleaseYear(year);
        }

        if (!string.IsNullOrWhiteSpace(request.ReleaseDate))
        {
            if (!DateOnly.TryParseExact(request.ReleaseDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly releaseDate))
            {
                throw new DomainException("release.release_date_invalid", "Release date must use yyyy-MM-dd format");
            }

            metadata = metadata.WithReleaseDate(releaseDate);
            if (request.Year is null)
            {
                metadata = metadata.WithReleaseYear(releaseDate.Year);
            }
        }

        release.UpdateSummary(ReleaseSummary.Create(request.Title).WithMetadata(metadata));
        release.UpdateCataloging(CatalogingMapper.Create(genres, request.Tags));
        release.UpdateArtistDisplay(request.IsVariousArtists);

        return release;
    }

}
