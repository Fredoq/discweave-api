using Cratebase.Domain.Catalog;
using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Importing;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Cratebase.Api.Features.Imports;

internal static class ReleaseImportResponseMapper
{
    public static ReleaseImportSessionResponse ToSessionResponse(ReleaseImportSession session)
    {
        return new ReleaseImportSessionResponse(
            session.Id.Value,
            session.SourceRoot,
            StatusCode(session.Status),
            session.DraftCount,
            session.TrackCount,
            session.IgnoredFileCount,
            session.CreatedAt,
            session.UpdatedAt,
            null);
    }

    public static async Task<ReleaseImportSessionResponse> ToDetailResponseAsync(
        ReleaseImportSession session,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        ReleaseImportDraft[] drafts = await context.ReleaseImportDrafts.AsNoTracking()
            .Where(draft => draft.CollectionId == collectionId && draft.SessionId == session.Id)
            .OrderBy(draft => draft.RelativePath)
            .ToArrayAsync(cancellationToken);
        ReleaseImportDraftId[] draftIds = [.. drafts.Select(draft => draft.Id)];
        ReleaseImportDraftTrack[] tracks = draftIds.Length == 0
            ? []
            : await context.ReleaseImportDraftTracks.AsNoTracking()
            .Where(track => track.CollectionId == collectionId && draftIds.Contains(track.DraftId))
            .OrderBy(track => track.Position ?? 9999)
            .ThenBy(track => track.RelativePath)
            .ToArrayAsync(cancellationToken);
        SuggestionLookup suggestions = await SuggestionLookup.LoadAsync(context, collectionId, cancellationToken);

        return ToSessionResponse(session) with
        {
            Drafts = [.. drafts.Select(draft => ToDraftResponse(draft, tracks, suggestions))]
        };
    }

    private static ReleaseImportDraftResponse ToDraftResponse(
        ReleaseImportDraft draft,
        ReleaseImportDraftTrack[] tracks,
        SuggestionLookup suggestions)
    {
        return new ReleaseImportDraftResponse(
            draft.Id.Value,
            draft.SourcePath,
            draft.RelativePath,
            DraftStatusCode(draft.Status),
            draft.Title,
            draft.Type,
            draft.CatalogNumber,
            draft.LabelName,
            draft.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            draft.Year,
            draft.IsVariousArtists,
            draft.NotOnLabel,
            draft.ArtistNames,
            [.. EffectiveArtistCredits(draft).Select(ToArtistCreditResponse)],
            draft.SelectedArtistIds,
            suggestions.ForArtists([.. EffectiveArtistCredits(draft).Select(credit => credit.Name)]),
            [.. EffectiveLabels(draft).Select(ToLabelResponse)],
            draft.Genres,
            draft.Tags,
            draft.CoverPath,
            [.. draft.Issues.Select(ToIssueResponse)],
            [.. tracks.Where(track => track.DraftId == draft.Id).Select(track => ToTrackResponse(track, suggestions))]);
    }

    private static IReadOnlyList<ReleaseImportArtistCredit> EffectiveArtistCredits(ReleaseImportDraft draft)
    {
        return draft.ArtistCredits.Count > 0
            ? draft.ArtistCredits
            : [.. draft.ArtistNames.Select((name, index) => new ReleaseImportArtistCredit(
                index < draft.SelectedArtistIds.Count ? draft.SelectedArtistIds[index] : null,
                name,
                "mainArtist"))];
    }

    private static IReadOnlyList<ReleaseImportLabel> EffectiveLabels(ReleaseImportDraft draft)
    {
        return draft.Labels.Count > 0
            ? draft.Labels
            : string.IsNullOrWhiteSpace(draft.LabelName)
                ? []
                :
                [
                    new ReleaseImportLabel(
                        null,
                        draft.LabelName,
                        draft.CatalogNumber,
                        string.IsNullOrWhiteSpace(draft.CatalogNumber))
                ];
    }

    private static ReleaseImportArtistCreditResponse ToArtistCreditResponse(ReleaseImportArtistCredit credit)
    {
        return new ReleaseImportArtistCreditResponse(credit.ArtistId, credit.Name, credit.Role);
    }

    private static ReleaseImportLabelResponse ToLabelResponse(ReleaseImportLabel label)
    {
        return new ReleaseImportLabelResponse(label.LabelId, label.Name, label.CatalogNumber, label.HasNoCatalogNumber);
    }

    private static ReleaseImportDraftTrackResponse ToTrackResponse(ReleaseImportDraftTrack track, SuggestionLookup suggestions)
    {
        return new ReleaseImportDraftTrackResponse(
            track.Id.Value,
            track.FilePath,
            track.RelativePath,
            ReleaseImportFileRules.FormatCode(track.Format),
            track.SizeBytes,
            track.LastModifiedAt,
            track.Duration is null ? null : (int)track.Duration.Value.TotalSeconds,
            track.Position,
            track.Title,
            track.ArtistNames,
            [.. EffectiveTrackArtistCredits(track).Select(ToArtistCreditResponse)],
            suggestions.ForArtists([.. EffectiveTrackArtistCredits(track).Select(credit => credit.Name)]),
            suggestions.ForTracks(track.Title),
            track.IsSkipped,
            track.SelectedTrackId?.Value,
            track.SelectedArtistIds,
            [.. track.Issues.Select(ToIssueResponse)]);
    }

    private static IReadOnlyList<ReleaseImportArtistCredit> EffectiveTrackArtistCredits(ReleaseImportDraftTrack track)
    {
        return track.ArtistCredits.Count > 0
            ? track.ArtistCredits
            : [.. track.ArtistNames.Select((name, index) => new ReleaseImportArtistCredit(
                index < track.SelectedArtistIds.Count ? track.SelectedArtistIds[index] : null,
                name,
                "mainArtist"))];
    }

    private static ImportIssueResponse ToIssueResponse(ImportReviewIssue issue)
    {
        return new ImportIssueResponse(issue.Code, issue.Message, IssueSeverityCode(issue.Severity));
    }

    private static string IssueSeverityCode(ImportReviewSeverity severity)
    {
        return severity switch
        {
            ImportReviewSeverity.Info => "info",
            ImportReviewSeverity.Warning => "warning",
            ImportReviewSeverity.Error => "error",
            _ => throw new InvalidOperationException("Import review issue severity is not supported")
        };
    }

    private static string StatusCode(ReleaseImportSessionStatus status)
    {
        return status switch
        {
            ReleaseImportSessionStatus.ReadyForReview => "readyForReview",
            ReleaseImportSessionStatus.Completed => "completed",
            _ => throw new InvalidOperationException("Release import session status is not supported")
        };
    }

    private static string DraftStatusCode(ReleaseImportDraftStatus status)
    {
        return status switch
        {
            ReleaseImportDraftStatus.NeedsReview => "needsReview",
            ReleaseImportDraftStatus.Ready => "ready",
            ReleaseImportDraftStatus.Confirmed => "confirmed",
            ReleaseImportDraftStatus.Skipped => "skipped",
            _ => throw new InvalidOperationException("Release import draft status is not supported")
        };
    }

    private sealed class SuggestionLookup
    {
        private readonly Artist[] _artists;
        private readonly Track[] _tracks;

        private SuggestionLookup(Artist[] artists, Track[] tracks)
        {
            _artists = artists;
            _tracks = tracks;
        }

        public static async Task<SuggestionLookup> LoadAsync(CratebaseDbContext context, CollectionId collectionId, CancellationToken cancellationToken)
        {
            Artist[] artists = await context.Artists.AsNoTracking().Where(artist => artist.CollectionId == collectionId).ToArrayAsync(cancellationToken);
            Track[] tracks = await context.Tracks.AsNoTracking().Where(track => track.CollectionId == collectionId).ToArrayAsync(cancellationToken);

            return new SuggestionLookup(artists, tracks);
        }

        public IReadOnlyList<EntitySuggestionResponse> ForArtists(IReadOnlyList<string> names)
        {
            return [.. names.SelectMany(name => Match(_artists, name, artist => artist.Id.Value, artist => artist.Name)).DistinctBy(suggestion => suggestion.Id)];
        }

        public IReadOnlyList<EntitySuggestionResponse> ForTracks(string title)
        {
            return [.. Match(_tracks, title, track => track.Id.Value, track => track.Title).Take(5)];
        }

        private static IEnumerable<EntitySuggestionResponse> Match<T>(IEnumerable<T> entities, string value, Func<T, Guid> id, Func<T, string> name)
        {
            string normalized = Normalize(value);
            return entities
                .Select(entity => new { Entity = entity, Normalized = Normalize(name(entity)) })
                .Where(candidate => candidate.Normalized == normalized || candidate.Normalized.Contains(normalized, StringComparison.Ordinal))
                .Select(candidate => new EntitySuggestionResponse(id(candidate.Entity), name(candidate.Entity), candidate.Normalized == normalized ? "exact" : "close"));
        }

        private static string Normalize(string value)
        {
            return string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
