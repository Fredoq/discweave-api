using Cratebase.Api.Features.Credits;
using Cratebase.Api.Features.Settings;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Imports;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public sealed partial class ReleaseImportConfirmationService
{
    private static async Task<IReadOnlyList<ReleaseLabel>> ResolveLabelsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportDraft draft,
        CancellationToken cancellationToken)
    {
        if (draft.NotOnLabel)
        {
            return [];
        }

        if (draft.Labels.Count > 0)
        {
            return await ResolveDraftLabelsAsync(context, collectionId, draft, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(draft.LabelName))
        {
            return [];
        }

        Label? label = await FindLabelByNameAsync(context, collectionId, draft.LabelName, cancellationToken);
        if (label is null)
        {
            label = Label.Create(collectionId, LabelId.New(), draft.LabelName);
            _ = context.Labels.Add(label);
        }

        return
        [
            ReleaseLabel.Create(
                label.Id,
                string.IsNullOrWhiteSpace(draft.CatalogNumber) ? Optional.Missing<string>() : Optional.From(draft.CatalogNumber),
                hasNoCatalogNumber: string.IsNullOrWhiteSpace(draft.CatalogNumber))
        ];
    }

    private static async Task<IReadOnlyList<ReleaseLabel>> ResolveDraftLabelsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportDraft draft,
        CancellationToken cancellationToken)
    {
        if (draft.NotOnLabel || draft.Labels.Count == 0)
        {
            return [];
        }

        List<ReleaseLabel> labels = [];
        foreach (ReleaseImportLabel labelRequest in draft.Labels)
        {
            Label label = await ResolveImportLabelAsync(context, collectionId, labelRequest, cancellationToken);
            labels.Add(ReleaseLabel.Create(
                label.Id,
                string.IsNullOrWhiteSpace(labelRequest.CatalogNumber) ? Optional.Missing<string>() : Optional.From(labelRequest.CatalogNumber),
                labelRequest.HasNoCatalogNumber));
        }

        return labels;
    }

    private static async Task AddTracksAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Release release,
        ReleaseImportDraft draft,
        IReadOnlyList<ReleaseImportDraftTrack> draftTracks,
        CancellationToken cancellationToken)
    {
        List<ReleaseTrack> releaseTracks = [];
        foreach (ReleaseImportDraftTrack draftTrack in draftTracks)
        {
            Track track = await ResolveTrackAsync(context, collectionId, draftTrack, cancellationToken);
            await AddTrackCreditsAsync(context, collectionId, track, draft, draftTrack, cancellationToken);
            await AddTrackOwnedItemAsync(context, collectionId, track, draftTrack, cancellationToken);
            releaseTracks.Add(ReleaseTrack.Create(track.Id, TrackPosition.FromNumber(draftTrack.Position ?? (releaseTracks.Count + 1))));
        }

        release.ReplaceTracklist(releaseTracks);
    }

    private static async Task AddTrackCreditsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Track track,
        ReleaseImportDraft draft,
        ReleaseImportDraftTrack draftTrack,
        CancellationToken cancellationToken)
    {
        if (draftTrack.SelectedTrackId is not null)
        {
            return;
        }

        if (draftTrack.ArtistCredits.Count > 0)
        {
            foreach (ReleaseImportArtistCredit credit in draftTrack.ArtistCredits)
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
                    CreditTarget.ForTrack(track.Id),
                    role));
            }

            return;
        }

        if (draftTrack.ArtistNames.Count > 0)
        {
            IReadOnlyList<Artist> artists = await ResolveArtistsAsync(context, collectionId, draftTrack.ArtistNames, draftTrack.SelectedArtistIds, cancellationToken);
            foreach (Artist artist in artists)
            {
                _ = context.Credits.Add(Credit.Create(
                    collectionId,
                    CreditId.New(),
                    CreditContributor.FromArtist(artist),
                    CreditTarget.ForTrack(track.Id),
                    MainArtistRole));
            }

            return;
        }

        foreach (ReleaseImportArtistCredit credit in MainArtistCredits(draft))
        {
            Artist artist = await ResolveArtistCreditAsync(context, collectionId, credit, cancellationToken);
            _ = context.Credits.Add(Credit.Create(
                collectionId,
                CreditId.New(),
                CreditContributor.FromArtist(artist),
                CreditTarget.ForTrack(track.Id),
                MainArtistRole));
        }
    }

    private static async Task<Label?> FindLabelByNameAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        string name,
        CancellationToken cancellationToken)
    {
        string normalized = Normalize(name);
        Label[] labels = await context.Labels.Where(label => label.CollectionId == collectionId).ToArrayAsync(cancellationToken);

        return labels.FirstOrDefault(label => Normalize(label.Name) == normalized);
    }

    private static async Task<Label> ResolveImportLabelAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        ReleaseImportLabel labelRequest,
        CancellationToken cancellationToken)
    {
        if (labelRequest.LabelId is { } labelId)
        {
            Label? existing = await context.Labels.SingleOrDefaultAsync(
                label => label.CollectionId == collectionId && label.Id == new LabelId(labelId),
                cancellationToken);

            return existing ?? throw new DomainException("release_import.label_conflict", "Release import label does not exist");
        }

        if (string.IsNullOrWhiteSpace(labelRequest.Name))
        {
            throw new DomainException("release_import.label_name_required", "Release import label name is required");
        }

        Label? label = await FindLabelByNameAsync(context, collectionId, labelRequest.Name, cancellationToken);
        if (label is not null)
        {
            return label;
        }

        label = Label.Create(collectionId, LabelId.New(), labelRequest.Name);
        _ = context.Labels.Add(label);
        return label;
    }
}
