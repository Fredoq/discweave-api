using Cratebase.Api.Auth;
using Cratebase.Api.Features.Settings;
using Cratebase.Api.Http;
using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Credits;

public static class CreditsEndpointRouteBuilderExtensions
{
    private const string CreditNotFoundCode = "credit.not_found";
    private const string CreditNotFoundMessage = "Credit was not found";

    public static IEndpointRouteBuilder MapCreditsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/credits")
            .WithTags("Credits")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreateCreditAsync).WithName("CreateCredit");
        _ = group.MapGet("/{creditId:guid}", GetCreditAsync).WithName("GetCredit");
        _ = group.MapGet("", ListCreditsAsync).WithName("ListCredits");
        _ = group.MapPut("/{creditId:guid}", UpdateCreditAsync).WithName("UpdateCredit");
        _ = group.MapDelete("/{creditId:guid}", DeleteCreditAsync).WithName("DeleteCredit");

        return endpoints;
    }

    private static async Task<IResult> CreateCreditAsync(
        CreditRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            Artist? contributor = await context.Artists.SingleOrDefaultAsync(
                artist => artist.CollectionId == currentCollection.CollectionId && artist.Id == new ArtistId(request.ContributorArtistId),
                cancellationToken);
            if (contributor is null)
            {
                return EndpointErrors.Conflict("credit.contributor_conflict", "Credit contributor does not exist");
            }

            CreditTarget target = CreditMapper.CreateTarget(request.TargetType, request.TargetId);
            if (!await TargetExistsAsync(target, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("credit.target_conflict", "Credit target does not exist");
            }

            string role = await DictionaryValidation.RequireActiveCodeAsync(
                context,
                currentCollection.CollectionId,
                DictionaryKind.CreditRole,
                CreditMapper.ParseRole(request.Role),
                "credit.role_invalid",
                "Credit role is invalid",
                cancellationToken);
            var credit = Credit.Create(currentCollection.CollectionId, CreditId.New(), CreditContributor.FromArtist(contributor), target, role);
            unitOfWork.GetRepository<Credit, CreditId>().Add(credit);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/credits/{credit.Id.Value}", CreditMapper.ToResponse(credit));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetCreditAsync(
        Guid creditId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Credit? credit = await context.Credits.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new CreditId(creditId),
            cancellationToken);

        return credit is null
            ? EndpointErrors.NotFound(CreditNotFoundCode, CreditNotFoundMessage)
            : Results.Ok(CreditMapper.ToResponse(credit));
    }

    private static async Task<IResult> ListCreditsAsync(
        [AsParameters] CreditListRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(request.Limit, request.Offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        try
        {
            string? role = string.IsNullOrWhiteSpace(request.Role)
                ? null
                : await DictionaryValidation.RequireCodeAsync(
                    context,
                    currentCollection.CollectionId,
                    DictionaryKind.CreditRole,
                    CreditMapper.ParseRole(request.Role),
                    "credit.role_invalid",
                    "Credit role is invalid",
                    cancellationToken);
            IQueryable<Credit> credits = ApplyFilters(
                context.Credits.AsNoTracking().Where(credit => credit.CollectionId == currentCollection.CollectionId),
                request.ContributorArtistId,
                request.TargetType,
                request.TargetId,
                role);
            int total = await credits.CountAsync(cancellationToken);
            Credit[] page = await credits.OrderBy(credit => credit.Id).Skip(normalizedOffset).Take(normalizedLimit).ToArrayAsync(cancellationToken);

            return Results.Ok(new ListResponse<CreditResponse>([.. page.Select(CreditMapper.ToResponse)], normalizedLimit, normalizedOffset, total));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> UpdateCreditAsync(
        Guid creditId,
        CreditRequest request,
        IUnitOfWork unitOfWork,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        IRepository<Credit, CreditId> credits = unitOfWork.GetRepository<Credit, CreditId>();
        Credit? credit = await credits.TryFindAsync(new CreditId(creditId), cancellationToken);
        if (credit is null)
        {
            return EndpointErrors.NotFound(CreditNotFoundCode, CreditNotFoundMessage);
        }

        try
        {
            if (credit.CollectionId != currentCollection.CollectionId)
            {
                return EndpointErrors.NotFound(CreditNotFoundCode, CreditNotFoundMessage);
            }

            Artist? contributor = await context.Artists.SingleOrDefaultAsync(
                artist => artist.CollectionId == currentCollection.CollectionId && artist.Id == new ArtistId(request.ContributorArtistId),
                cancellationToken);
            if (contributor is null)
            {
                return EndpointErrors.Conflict("credit.contributor_conflict", "Credit contributor does not exist");
            }

            CreditTarget target = CreditMapper.CreateTarget(request.TargetType, request.TargetId);
            if (!await TargetExistsAsync(target, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("credit.target_conflict", "Credit target does not exist");
            }

            string role = await DictionaryValidation.RequireActiveCodeAsync(
                context,
                currentCollection.CollectionId,
                DictionaryKind.CreditRole,
                CreditMapper.ParseRole(request.Role),
                "credit.role_invalid",
                "Credit role is invalid",
                cancellationToken);
            credit.Update(CreditContributor.FromArtist(contributor), target, role);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(CreditMapper.ToResponse(credit));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteCreditAsync(
        Guid creditId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "credit", creditId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<Credit, CreditId> credits = unitOfWork.GetRepository<Credit, CreditId>();
        Credit? credit = await credits.TryFindAsync(new CreditId(creditId), cancellationToken);
        if (credit is null || credit.CollectionId != currentCollection.CollectionId)
        {
            return EndpointErrors.NotFound(CreditNotFoundCode, CreditNotFoundMessage);
        }

        try
        {
            credits.Delete(credit);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("credit.delete_conflict", "Credit has dependent data");
        }
    }

    private static IQueryable<Credit> ApplyFilters(IQueryable<Credit> credits, Guid? contributorArtistId, string? targetType, Guid? targetId, string? role)
    {
        if (contributorArtistId is { } artistId)
        {
            credits = credits.Where(credit => EF.Property<ArtistId>(credit, "_contributorArtistId") == new ArtistId(artistId));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            credits = credits.Where(credit => credit.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            credits = credits.Where(credit => EF.Property<string>(credit, "_targetType") == targetType.Trim());
        }

        if (targetId is { } id)
        {
            credits = credits.Where(credit => EF.Property<ReleaseId?>(credit, "_targetReleaseId") == new ReleaseId(id) || EF.Property<TrackId?>(credit, "_targetTrackId") == new TrackId(id));
        }

        return credits;
    }

    private static async Task<bool> TargetExistsAsync(
        CreditTarget target,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return target switch
        {
            ReleaseCreditTarget releaseTarget => await context.Releases.AnyAsync(release => release.CollectionId == collectionId && release.Id == releaseTarget.ReleaseId, cancellationToken),
            TrackCreditTarget trackTarget => await context.Tracks.AnyAsync(track => track.CollectionId == collectionId && track.Id == trackTarget.TrackId, cancellationToken),
            _ => false
        };
    }
}
