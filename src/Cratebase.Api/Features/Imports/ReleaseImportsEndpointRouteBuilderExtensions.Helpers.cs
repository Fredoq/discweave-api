using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Cratebase.Api.Features.Imports;

public static partial class ReleaseImportsEndpointRouteBuilderExtensions
{
    private static async Task<ReleaseImportSession?> FindSessionAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await context.ReleaseImportSessions.SingleOrDefaultAsync(
            session => session.CollectionId == collectionId && session.Id == new ReleaseImportSessionId(sessionId),
            cancellationToken);
    }

    private static async Task<ReleaseImportDraft?> FindDraftAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Guid sessionId,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        return await context.ReleaseImportDrafts.SingleOrDefaultAsync(
            draft => draft.CollectionId == collectionId &&
                draft.SessionId == new ReleaseImportSessionId(sessionId) &&
                draft.Id == new ReleaseImportDraftId(draftId),
            cancellationToken);
    }

    private static DateOnly? ParseOptionalDate(string? releaseDate)
    {
        return string.IsNullOrWhiteSpace(releaseDate)
            ? null
            : DateOnly.TryParseExact(releaseDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
            ? parsed
            : throw new DomainException("release_import.release_date_invalid", "Release date must use yyyy-MM-dd format");
    }

    private static async Task UpdateTracksAsync(
        ReleaseImportDraftUpdateRequest request,
        ReleaseImportDraft draft,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (request.Tracks is null)
        {
            return;
        }

        ReleaseImportDraftTrack[] tracks = await context.ReleaseImportDraftTracks
            .Where(track => track.DraftId == draft.Id)
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, ReleaseImportDraftTrack> tracksById = tracks.ToDictionary(track => track.Id.Value);

        foreach (ReleaseImportDraftTrackUpdateRequest trackRequest in request.Tracks)
        {
            if (!tracksById.TryGetValue(trackRequest.Id, out ReleaseImportDraftTrack? track))
            {
                throw new DomainException("release_import.track_not_found", "Release import draft track was not found");
            }

            track.UpdateEditableFields(new DraftTrackEditableFields(
                trackRequest.Position,
                trackRequest.Title,
                trackRequest.DurationSeconds is null ? null : TimeSpan.FromSeconds(trackRequest.DurationSeconds.Value),
                trackRequest.ArtistNames ?? [],
                [.. trackRequest.ArtistCredits?.Select(ToImportArtistCredit) ?? []],
                trackRequest.SelectedArtistIds ?? [],
                trackRequest.SelectedTrackId is null ? null : new TrackId(trackRequest.SelectedTrackId.Value),
                trackRequest.IsSkipped,
                track.Issues));
        }
    }
}
