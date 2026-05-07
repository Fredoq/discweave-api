using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Tracks;

public static class TracksEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTracksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/tracks")
            .WithTags("Tracks")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreateTrackAsync).WithName("CreateTrack");
        _ = group.MapGet("/{trackId:guid}", GetTrackAsync).WithName("GetTrack");
        _ = group.MapGet("", ListTracksAsync).WithName("ListTracks");
        _ = group.MapPut("/{trackId:guid}", UpdateTrackAsync).WithName("UpdateTrack");
        _ = group.MapDelete("/{trackId:guid}", DeleteTrackAsync).WithName("DeleteTrack");

        return endpoints;
    }

    private static async Task<IResult> CreateTrackAsync(
        TrackRequest request,
        IUnitOfWork unitOfWork,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            Track track = ApplyTrackRequest(Track.Create(currentCollection.CollectionId, TrackId.New(), request.Title), request);
            IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
            tracks.Add(track);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/tracks/{track.Id}", ToTrackResponse(track));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return EndpointErrors.BadRequest("track.request_invalid", "Track request is invalid");
        }
    }

    private static async Task<IResult> GetTrackAsync(
        Guid trackId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Track? track = await context.Tracks.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new TrackId(trackId),
            cancellationToken);

        return track is null
            ? EndpointErrors.NotFound("track.not_found", "Track was not found")
            : Results.Ok(ToTrackResponse(track));
    }

    private static async Task<IResult> ListTracksAsync(
        string? search,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<Track> tracks = context.Tracks.AsNoTracking().Where(track => track.CollectionId == currentCollection.CollectionId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            tracks = tracks.Where(track => EF.Functions.ILike(track.Title, pattern));
        }

        int total = await tracks.CountAsync(cancellationToken);
        Track[] items = await tracks
            .OrderBy(track => track.Title)
            .ThenBy(track => track.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<TrackResponse>([.. items.Select(ToTrackResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateTrackAsync(
        Guid trackId,
        TrackRequest request,
        IUnitOfWork unitOfWork,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
        Track? track = await tracks.TryFindAsync(new TrackId(trackId), cancellationToken);
        if (track is null || track.CollectionId != currentCollection.CollectionId)
        {
            return EndpointErrors.NotFound("track.not_found", "Track was not found");
        }

        try
        {
            _ = ApplyTrackRequest(track, request);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToTrackResponse(track));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return EndpointErrors.BadRequest("track.request_invalid", "Track request is invalid");
        }
    }

    private static async Task<IResult> DeleteTrackAsync(
        Guid trackId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "track", trackId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        Track? track = await context.Tracks.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new TrackId(trackId),
            cancellationToken);
        if (track is null)
        {
            return EndpointErrors.NotFound("track.not_found", "Track was not found");
        }

        if (await TrackHasExternalDependentsAsync(track.Id, context, currentCollection.CollectionId, cancellationToken))
        {
            return EndpointErrors.Conflict("track.delete_conflict", "Track has dependent data");
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            Release[] releases = await context.Releases
                .Where(release => release.CollectionId == currentCollection.CollectionId)
                .ToArrayAsync(cancellationToken);
            foreach (Release release in releases.Where(release => release.Tracklist.Any(releaseTrack => releaseTrack.TrackId == track.Id)))
            {
                release.ReplaceTracklist([.. release.Tracklist.Where(releaseTrack => releaseTrack.TrackId != track.Id)]);
            }

            Credit[] trackCredits = await context.Credits
                .Where(credit => credit.CollectionId == currentCollection.CollectionId)
                .ToArrayAsync(cancellationToken);
            context.Credits.RemoveRange(trackCredits.Where(credit => credit.Target is TrackCreditTarget target && target.TrackId == track.Id));
            _ = context.Tracks.Remove(track);

            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("track.delete_conflict", "Track has dependent data");
        }
    }

    private static async Task<bool> TrackHasExternalDependentsAsync(
        TrackId trackId,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        bool hasRelations = await context.TrackRelations.AnyAsync(
            relation =>
                relation.CollectionId == collectionId &&
                (relation.SourceTrackId == trackId || relation.TargetTrackId == trackId),
            cancellationToken);
        return hasRelations || await context.OwnedItems.AnyAsync(
            item =>
                item.CollectionId == collectionId &&
                EF.Property<TrackId?>(item, "_targetTrackId") == trackId,
            cancellationToken);
    }

    private static Track ApplyTrackRequest(Track track, TrackRequest request)
    {
        track.Rename(request.Title);
        TrackDetails details = TrackDetails.Empty;
        if (request.DurationSeconds is { } durationSeconds)
        {
            details = details.WithDuration(TimeSpan.FromSeconds(durationSeconds));
        }

        track.UpdateDetails(details);
        track.UpdateCataloging(CatalogingMapper.Create(request.Genres, request.Tags));

        return track;
    }

    private static TrackResponse ToTrackResponse(Track track)
    {
        int? durationSeconds = track.Details.Duration.HasValue
            ? track.Details.Duration.Match(value => (int)value.TotalSeconds, () => 0)
            : null;

        return new TrackResponse(
            track.Id.Value,
            track.Title,
            durationSeconds,
            [.. track.Cataloging.Genres.Select(genre => genre.Name)],
            [.. track.Cataloging.Tags.Select(tag => tag.Name)]);
    }

}
