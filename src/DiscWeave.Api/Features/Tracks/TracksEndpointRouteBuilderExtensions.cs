using DiscWeave.Api.Auth;
using DiscWeave.Api.Features.ExternalSources;
using DiscWeave.Api.Features.Settings;
using DiscWeave.Api.Http;
using DiscWeave.Application.Errors;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Tracks;

public static partial class TracksEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTracksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/tracks")
            .WithTags("Tracks")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreateTrackAsync).WithName("CreateTrack");
        _ = group.MapGet("/{trackId:guid}", GetTrackAsync).WithName("GetTrack");
        _ = group.MapGet("", ListTracksAsync).WithName("ListTracks");
        _ = group.MapPut("/{trackId:guid}", UpdateTrackAsync).WithName("UpdateTrack");
        _ = group.MapDelete("/{trackId:guid}", DeleteTrackAsync).WithName("DeleteTrack");

        return endpoints;
    }

    private static async Task<IResult> CreateTrackAsync(
        TrackRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            Track track = await ApplyTrackRequestAsync(
                Track.Create(currentCollection.CollectionId, TrackId.New(), request.Title),
                request,
                context,
                currentCollection.CollectionId,
                cancellationToken);
            track.ReplaceExternalSources(ExternalSourceReferenceMapper.FromRequests(request.ExternalSources, DateTimeOffset.UtcNow));
            _ = context.Tracks.Add(track);
            await ReplaceTrackCreditsAsync(track, request.Credits, context, currentCollection.CollectionId, cancellationToken);
            await ReplaceTrackAppearancesAsync(track, request.ReleaseAppearances, context, currentCollection.CollectionId, cancellationToken);

            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Created($"/api/tracks/{track.Id}", await ToTrackResponseAsync(track, context, currentCollection.CollectionId, cancellationToken));
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
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Track? track = await context.Tracks.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new TrackId(trackId),
            cancellationToken);

        return track is null
            ? EndpointErrors.NotFound("track.not_found", "Track was not found")
            : Results.Ok(await ToTrackResponseAsync(track, context, currentCollection.CollectionId, cancellationToken));
    }

    private static async Task<IResult> ListTracksAsync(
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

        IReadOnlyList<TrackResponse> responses = await ToTrackResponsesAsync(items, context, currentCollection.CollectionId, cancellationToken);

        return Results.Ok(new ListResponse<TrackResponse>(responses, normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateTrackAsync(
        Guid trackId,
        TrackRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Track? track = await context.Tracks.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new TrackId(trackId),
            cancellationToken);
        if (track is null || track.CollectionId != currentCollection.CollectionId)
        {
            return EndpointErrors.NotFound("track.not_found", "Track was not found");
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _ = await ApplyTrackRequestAsync(
                track,
                request,
                context,
                currentCollection.CollectionId,
                cancellationToken);
            if (request.ExternalSources is not null)
            {
                track.ReplaceExternalSources(ExternalSourceReferenceMapper.FromRequests(request.ExternalSources, DateTimeOffset.UtcNow));
            }

            await ReplaceTrackCreditsAsync(track, request.Credits, context, currentCollection.CollectionId, cancellationToken);
            await ReplaceTrackAppearancesAsync(track, request.ReleaseAppearances, context, currentCollection.CollectionId, cancellationToken);

            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(await ToTrackResponseAsync(track, context, currentCollection.CollectionId, cancellationToken));
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
        DiscWeaveDbContext context,
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

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (await TrackHasExternalDependentsAsync(track.Id, context, currentCollection.CollectionId, cancellationToken))
            {
                return EndpointErrors.Conflict("track.delete_conflict", "Track has dependent data");
            }

            Release[] releases = await context.Releases
                .Where(release => release.CollectionId == currentCollection.CollectionId)
                .ToArrayAsync(cancellationToken);
            foreach (Release release in releases.Where(release => release.Tracklist.Any(releaseTrack => releaseTrack.TrackId == track.Id)))
            {
                release.ReplaceTracklist([.. release.Tracklist.Where(releaseTrack => releaseTrack.TrackId != track.Id)]);
            }

            Credit[] trackCredits = await context.Credits
                .Where(credit =>
                    credit.CollectionId == currentCollection.CollectionId &&
                    EF.Property<TrackId?>(credit, "_targetTrackId") == track.Id)
                .ToArrayAsync(cancellationToken);
            context.Credits.RemoveRange(trackCredits);
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
        DiscWeaveDbContext context,
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

    private static async Task<Track> ApplyTrackRequestAsync(
        Track track,
        TrackRequest request,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> genres = await DictionaryValidation.RequireActiveCodesAsync(
            context,
            collectionId,
            DictionaryKind.Genre,
            request.Genres,
            "track.genre_invalid",
            "Track genre is invalid",
            cancellationToken);

        track.Rename(request.Title);
        TrackDetails details = TrackDetails.Empty;
        if (request.DurationSeconds is { } durationSeconds)
        {
            details = details.WithDuration(TimeSpan.FromSeconds(durationSeconds));
        }

        track.UpdateDetails(details);
        track.UpdateCataloging(CatalogingMapper.Create(genres, request.Tags));

        return track;
    }
}
