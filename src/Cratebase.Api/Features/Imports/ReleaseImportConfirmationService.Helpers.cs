using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Settings;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Imports;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public sealed partial class ReleaseImportConfirmationService
{
    private static readonly CreditArtistResolverErrors ImportReleaseCreditArtistErrors = new(
        "release_import.artist_conflict",
        "Release import artist does not exist",
        "release_import.artist_name_required",
        "Release import artist name is required");

    private static async Task<IReadOnlyList<Artist>> ResolveArtistsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<string> names,
        IReadOnlyList<Guid> selectedIds,
        CancellationToken cancellationToken)
    {
        List<Artist> artists = [];
        for (int index = 0; index < names.Count; index++)
        {
            string name = names[index];
            Artist? artist = index < selectedIds.Count
                ? await context.Artists.SingleOrDefaultAsync(candidate => candidate.CollectionId == collectionId && candidate.Id == new ArtistId(selectedIds[index]), cancellationToken)
                : await FindArtistByNameAsync(context, collectionId, name, cancellationToken);

            if (artist is null)
            {
                artist = Person.Create(collectionId, ArtistId.New(), name);
                _ = context.Artists.Add(artist);
            }

            artists.Add(artist);
        }

        return artists;
    }

    private static async Task<Artist?> FindArtistByNameAsync(CratebaseDbContext context, CollectionId collectionId, string name, CancellationToken cancellationToken)
    {
        string normalized = Normalize(name);
        Artist[] artists = await context.Artists.Where(artist => artist.CollectionId == collectionId).ToArrayAsync(cancellationToken);

        return artists.FirstOrDefault(artist => Normalize(artist.Name) == normalized);
    }

    private static async Task<Artist> ResolveArtistCreditAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportArtistCredit credit,
        CancellationToken cancellationToken)
    {
        return await CreditArtistResolver.ResolveAsync(
            credit.ArtistId,
            credit.Name,
            context,
            collectionId,
            ImportReleaseCreditArtistErrors,
            cancellationToken);
    }

    private static IReadOnlyList<ReleaseImportArtistCredit> EffectiveArtistCredits(ReleaseImportDraft draft)
    {
        return draft.ArtistCredits.Count > 0
            ? draft.ArtistCredits
            : [.. draft.ArtistNames.Select((name, index) => new ReleaseImportArtistCredit(
                index < draft.SelectedArtistIds.Count ? draft.SelectedArtistIds[index] : null,
                name,
                MainArtistRole))];
    }

    private static IReadOnlyList<ReleaseImportArtistCredit> MainArtistCredits(ReleaseImportDraft draft)
    {
        ReleaseImportArtistCredit[] mainCredits =
        [
            .. EffectiveArtistCredits(draft).Where(credit =>
                string.Equals(credit.Role, MainArtistRole, StringComparison.Ordinal) ||
                string.Equals(credit.Role, "Main artist", StringComparison.OrdinalIgnoreCase))
        ];

        return mainCredits.Length > 0 ? mainCredits : EffectiveArtistCredits(draft);
    }

    private static async Task AddReleaseCreditsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Release release,
        ReleaseImportDraft draft,
        CancellationToken cancellationToken)
    {
        if (draft.IsVariousArtists)
        {
            return;
        }

        foreach (ReleaseImportArtistCredit credit in EffectiveArtistCredits(draft))
        {
            Artist artist = await ResolveArtistCreditAsync(context, collectionId, credit, cancellationToken);
            string role = await DictionaryValidation.RequireActiveCodeAsync(
                context,
                collectionId,
                DictionaryKind.CreditRole,
                CreditMapper.ParseRole(string.IsNullOrWhiteSpace(credit.Role) ? MainArtistRole : credit.Role),
                "credit.role_invalid",
                "Credit role is invalid",
                cancellationToken);

            _ = context.Credits.Add(Credit.Create(
                collectionId,
                CreditId.New(),
                CreditContributor.FromArtist(artist),
                CreditTarget.ForRelease(release.Id),
                role));
        }
    }

    private static async Task<Track> ResolveTrackAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportDraftTrack draftTrack,
        CancellationToken cancellationToken)
    {
        if (draftTrack.SelectedTrackId is { } selectedTrackId)
        {
            Track? existing = await context.Tracks.SingleOrDefaultAsync(
                track => track.CollectionId == collectionId && track.Id == selectedTrackId,
                cancellationToken);

            return existing ?? throw new InvalidOperationException("Selected track is missing");
        }

        var track = Track.Create(collectionId, TrackId.New(), draftTrack.Title);
        if (draftTrack.Duration is { } duration)
        {
            track.UpdateDetails(track.Details.WithDuration(duration));
        }

        _ = context.Tracks.Add(track);
        return track;
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
