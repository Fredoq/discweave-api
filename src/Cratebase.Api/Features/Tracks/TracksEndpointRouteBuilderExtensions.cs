using Cratebase.Api.Http;
using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
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

        RouteGroupBuilder group = endpoints.MapGroup("/api/tracks").WithTags("Tracks");
        _ = group.MapPost("/", CreateTrackAsync).WithName("CreateTrack");
        _ = group.MapGet("/{trackId:guid}", GetTrackAsync).WithName("GetTrack");
        _ = group.MapGet("/", ListTracksAsync).WithName("ListTracks");
        _ = group.MapPut("/{trackId:guid}", UpdateTrackAsync).WithName("UpdateTrack");
        _ = group.MapDelete("/{trackId:guid}", DeleteTrackAsync).WithName("DeleteTrack");

        return endpoints;
    }

    private static async Task<IResult> CreateTrackAsync(
        TrackRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            Track track = ApplyTrackRequest(Track.Create(TrackId.New(), request.Title), request);
            IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
            tracks.Add(track);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/tracks/{track.Id}", ToTrackResponse(track));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetTrackAsync(Guid trackId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        Track? track = await context.Tracks.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new TrackId(trackId), cancellationToken);

        return track is null
            ? EndpointErrors.NotFound("track.not_found", "Track was not found")
            : Results.Ok(ToTrackResponse(track));
    }

    private static async Task<IResult> ListTracksAsync(
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

        IQueryable<Track> tracks = context.Tracks.AsNoTracking();
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
        CancellationToken cancellationToken)
    {
        IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
        Track? track = await tracks.TryFindAsync(new TrackId(trackId), cancellationToken);
        if (track is null)
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
    }

    private static async Task<IResult> DeleteTrackAsync(
        Guid trackId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "track", trackId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
        Track? track = await tracks.TryFindAsync(new TrackId(trackId), cancellationToken);
        if (track is null)
        {
            return EndpointErrors.NotFound("track.not_found", "Track was not found");
        }

        try
        {
            tracks.Delete(track);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DbUpdateException exception) when (PersistenceErrors.IsForeignKeyViolation(exception))
        {
            return EndpointErrors.Conflict("track.delete_conflict", "Track has dependent data");
        }
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
