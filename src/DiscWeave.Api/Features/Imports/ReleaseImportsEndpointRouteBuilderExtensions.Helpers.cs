using DiscWeave.Domain.Imports;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DiscWeave.Api.Features.Imports;

public static partial class ReleaseImportsEndpointRouteBuilderExtensions
{
    private static async Task<ReleaseImportSession?> FindSessionAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await context.ReleaseImportSessions.SingleOrDefaultAsync(
            session => session.CollectionId == collectionId && session.Id == new ReleaseImportSessionId(sessionId),
            cancellationToken);
    }

    private static async Task<ReleaseImportDraft?> FindDraftAsync(
        DiscWeaveDbContext context,
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
        return string.IsNullOrWhiteSpace(releaseDate) ? null : ParseRequiredDate(releaseDate.Trim());
    }

    private static DateOnly ParseRequiredDate(string releaseDate)
    {
        return DateOnly.TryParseExact(releaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
            ? parsed
            : throw new DomainException("release_import.release_date_invalid", "Release date must use yyyy-MM-dd format");
    }

    private static IOptionalValue<string> ToOptional(string? value)
    {
        return value is null ? Optional.Missing<string>() : Optional.From(value);
    }

    private static IOptionalValue<T> ToOptional<T>(T? value)
        where T : struct
    {
        return value is { } present ? Optional.From(present) : Optional.Missing<T>();
    }

    private static async Task UpdateTracksAsync(
        ReleaseImportDraftUpdateRequest request,
        ReleaseImportDraft draft,
        DiscWeaveDbContext context,
        CancellationToken cancellationToken)
    {
        if (request.Tracks is null)
        {
            return;
        }

        ReleaseImportDraftTrack[] tracks = await context.ReleaseImportDraftTracks
            .Where(track => track.CollectionId == draft.CollectionId && track.DraftId == draft.Id)
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, ReleaseImportDraftTrack> tracksById = tracks.ToDictionary(track => track.Id.Value);

        foreach (ReleaseImportDraftTrackUpdateRequest trackRequest in request.Tracks)
        {
            if (!tracksById.TryGetValue(trackRequest.Id, out ReleaseImportDraftTrack? track))
            {
                throw new DomainException("release_import.track_not_found", "Release import draft track was not found");
            }

            TrackId? selectedTrackId = trackRequest.SelectedTrackId is null ? null : new TrackId(trackRequest.SelectedTrackId.Value);
            if (selectedTrackId is { } trackId)
            {
                bool selectedTrackExists = await context.Tracks.AnyAsync(
                    candidate => candidate.CollectionId == draft.CollectionId && candidate.Id == trackId,
                    cancellationToken);
                if (!selectedTrackExists)
                {
                    throw new DomainException("release_import.selected_track_not_found", "Selected import track was not found");
                }
            }

            track.UpdateEditableFields(new DraftTrackEditableFields(
                trackRequest.Position,
                trackRequest.Disc,
                trackRequest.Side,
                trackRequest.Title,
                trackRequest.DurationSeconds is null ? null : TimeSpan.FromSeconds(trackRequest.DurationSeconds.Value),
                trackRequest.ArtistNames ?? [],
                [.. trackRequest.ArtistCredits?.Select(ToImportArtistCredit) ?? []],
                trackRequest.SelectedArtistIds ?? [],
                selectedTrackId,
                trackRequest.IsSkipped,
                track.Issues));
        }
    }
}
