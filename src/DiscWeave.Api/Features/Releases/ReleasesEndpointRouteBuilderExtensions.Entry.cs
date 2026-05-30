using DiscWeave.Api.Features.OwnedItems;
using DiscWeave.Api.Features.Settings;
using DiscWeave.Application.Errors;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static async Task<Release> CreateReleaseEntryAsync(
        ReleaseRequest request,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Release release = await ApplyReleaseRequestAsync(
            Release.Create(collectionId, ReleaseId.New(), request.Title),
            request,
            context,
            collectionId,
            cancellationToken);
        IReadOnlyList<ResolvedCredit> releaseCredits = await ResolveCreditsAsync(
            request.ArtistCredits,
            context,
            collectionId,
            cancellationToken);

        if (!request.IsVariousArtists && releaseCredits.All(credit => credit.Role != "mainArtist"))
        {
            throw new DomainException("release.artist_required", "Release artist is required unless the release is marked as Various Artists");
        }

        IReadOnlyList<ReleaseLabel> labels = await ResolveLabelsAsync(request, context, collectionId, cancellationToken);
        release.UpdateLabels(request.NotOnLabel, labels);
        _ = context.Releases.Add(release);

        foreach (ResolvedCredit credit in releaseCredits)
        {
            _ = context.Credits.Add(Credit.Create(collectionId, CreditId.New(), CreditContributor.FromArtist(credit.Artist), CreditTarget.ForRelease(release.Id), credit.Role));
        }

        await ReplaceReleaseTracklistAsync(request, release, releaseCredits, context, collectionId, cancellationToken);
        await CreateOwnedCopyAsync(request, release, context, collectionId, cancellationToken);

        return release;
    }

    private static async Task ReplaceReleaseTracklistAsync(
        ReleaseRequest request,
        Release release,
        IReadOnlyList<ResolvedCredit> releaseCredits,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        EnsureTracklistHasNoDuplicateTrackIds(request.Tracklist ?? []);

        var releaseTracks = new List<ReleaseTrack>();
        foreach (ReleaseTrackRequest trackRequest in request.Tracklist ?? [])
        {
            Track track;
            if (trackRequest.TrackId is { } trackId)
            {
                EnsureExistingTrackRequestHasNoCanonicalMetadata(trackRequest);

                track = await context.Tracks.SingleOrDefaultAsync(
                    entity => entity.CollectionId == collectionId && entity.Id == new TrackId(trackId),
                    cancellationToken)
                    ?? throw new DomainException("release_track.track_conflict", "Release track does not exist");
            }
            else
            {
                string title = trackRequest.Title?.Trim() ?? string.Empty;
                if (title.Length == 0)
                {
                    throw new DomainException("release_track.title_required", "Release track title is required when trackId is not provided");
                }

                track = Track.Create(collectionId, TrackId.New(), title);
                TrackDetails details = TrackDetails.Empty;
                if (trackRequest.DurationSeconds is { } durationSeconds)
                {
                    details = details.WithDuration(TimeSpan.FromSeconds(durationSeconds));
                }

                track.UpdateDetails(details);
                _ = context.Tracks.Add(track);

                IReadOnlyList<ResolvedCredit> trackCredits = await ResolveTrackCreditsAsync(
                    trackRequest.ArtistCredits,
                    releaseCredits,
                    request.IsVariousArtists,
                    context,
                    collectionId,
                    cancellationToken);
                foreach (ResolvedCredit credit in trackCredits)
                {
                    _ = context.Credits.Add(Credit.Create(collectionId, CreditId.New(), CreditContributor.FromArtist(credit.Artist), CreditTarget.ForTrack(track.Id), credit.Role));
                }
            }

            releaseTracks.Add(
                ReleaseTrack.Create(
                    track.Id,
                    TrackPosition.FromNumber(trackRequest.Position),
                    Optional.Missing<string>(),
                    ToOptionalString(trackRequest.VersionNote)));
        }

        release.ReplaceTracklist(releaseTracks);
    }

    private static void EnsureExistingTrackRequestHasNoCanonicalMetadata(ReleaseTrackRequest trackRequest)
    {
        bool hasArtistCredits = trackRequest.ArtistCredits?.Count > 0;

        if (!string.IsNullOrWhiteSpace(trackRequest.Title)
            || trackRequest.DurationSeconds is not null
            || hasArtistCredits)
        {
            throw new DomainException(
                "release_track.shape_invalid",
                "Release track with trackId must not include title, durationSeconds, or artistCredits");
        }
    }

    private static void EnsureTracklistHasNoDuplicateTrackIds(IReadOnlyList<ReleaseTrackRequest> trackRequests)
    {
        var requestedTrackIds = new HashSet<Guid>();
        foreach (ReleaseTrackRequest trackRequest in trackRequests)
        {
            if (trackRequest.TrackId is { } trackId && !requestedTrackIds.Add(trackId))
            {
                throw new DomainException(
                    "release_track.track_duplicate",
                    "Release tracklist contains duplicate track entries");
            }
        }
    }

    private static async Task CreateOwnedCopyAsync(
        ReleaseRequest request,
        Release release,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (request.OwnedCopy is not { } ownedCopy)
        {
            return;
        }

        CollectionDictionaryEntry mediaEntry = await DictionaryValidation.RequireActiveEntryAsync(
            context,
            collectionId,
            DictionaryKind.MediaType,
            ownedCopy.Medium.Type ?? string.Empty,
            "medium.type_invalid",
            "Medium type is invalid",
            cancellationToken);
        IMedium medium = OwnedItemMapper.CreateMedium(ownedCopy.Medium, mediaEntry);
        var item = OwnedItem.Create(
            collectionId,
            OwnedItemId.New(),
            OwnedItemTarget.ForRelease(release.Id),
            OwnedItemMapper.ParseOwnershipStatus(ownedCopy.Status),
            medium);
        item.UpdateHolding(OwnedItemMapper.CreateHolding(item.Holding.Medium, ownedCopy.Status, ownedCopy.Condition, ownedCopy.StorageLocation));
        _ = context.OwnedItems.Add(item);
    }

    private static async Task<IReadOnlyList<ReleaseLabel>> ResolveLabelsAsync(
        ReleaseRequest request,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (request.NotOnLabel)
        {
            return [];
        }

        var labels = new List<ReleaseLabel>();
        if (request.Labels is { Count: > 0 })
        {
            foreach (ReleaseLabelRequest labelRequest in request.Labels)
            {
                Label label = await ResolveLabelAsync(labelRequest, context, collectionId, cancellationToken);
                labels.Add(ReleaseLabel.Create(label.Id, ToOptionalString(labelRequest.CatalogNumber), labelRequest.HasNoCatalogNumber));
            }
        }
        else if (request.LabelId is { } labelId)
        {
            Label? label = await context.Labels.SingleOrDefaultAsync(
                record => record.CollectionId == collectionId && record.Id == new LabelId(labelId),
                cancellationToken);
            _ = label ?? throw new ReferencedResourceMissingException(new InvalidOperationException(LabelMissingMessage));

            labels.Add(ReleaseLabel.Create(label.Id, Optional.Missing<string>(), false));
        }

        return labels;
    }

    private static async Task<Label> ResolveLabelAsync(
        ReleaseLabelRequest labelRequest,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (labelRequest.LabelId is { } labelId)
        {
            Label? existing = await context.Labels.SingleOrDefaultAsync(
                label => label.CollectionId == collectionId && label.Id == new LabelId(labelId),
                cancellationToken);

            return existing ?? throw new ReferencedResourceMissingException(new InvalidOperationException(LabelMissingMessage));
        }

        if (string.IsNullOrWhiteSpace(labelRequest.Name))
        {
            throw new DomainException("release_label.name_required", "Release label name is required");
        }

        string name = labelRequest.Name.Trim();
        Label? existingByName = context.Labels.Local.FirstOrDefault(label => label.CollectionId == collectionId && label.Name == name)
            ?? await context.Labels.FirstOrDefaultAsync(
                label => label.CollectionId == collectionId && label.Name == name,
                cancellationToken);
        if (existingByName is not null)
        {
            return existingByName;
        }

        var created = Label.Create(collectionId, LabelId.New(), name);
        _ = context.Labels.Add(created);

        return created;
    }

    private static IOptionalValue<string> ToOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Optional.Missing<string>()
            : Optional.From(value.Trim());
    }

    private static async Task ReplaceReleaseCreditsAsync(
        Release release,
        IReadOnlyList<ResolvedCredit> releaseCredits,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        Credit[] releaseCreditsToRemove = await context.Credits
            .Where(credit =>
                credit.CollectionId == collectionId &&
                EF.Property<ReleaseId?>(credit, "_targetReleaseId") == release.Id)
            .ToArrayAsync(cancellationToken);
        context.Credits.RemoveRange(releaseCreditsToRemove);

        foreach (ResolvedCredit credit in releaseCredits)
        {
            _ = context.Credits.Add(Credit.Create(collectionId, CreditId.New(), CreditContributor.FromArtist(credit.Artist), CreditTarget.ForRelease(release.Id), credit.Role));
        }
    }
}
