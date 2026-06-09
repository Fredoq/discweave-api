using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;

namespace DiscWeave.Domain.Catalog;

public sealed class Release : IEntity<ReleaseId>, ICreditTarget
{
    private readonly List<Genre> _genres = [];
    private readonly List<ExternalSourceReference> _externalSources = [];
    private readonly List<ReleaseLabel> _labels = [];
    private readonly List<Tag> _tags = [];
    private readonly List<ReleaseTrack> _tracklist = [];

    private Release()
    {
        Summary = ReleaseSummary.Empty;
    }

    private Release(
        CollectionId collectionId,
        ReleaseId id,
        ReleaseState state,
        Cataloging cataloging,
        IReadOnlyList<ExternalSourceReference>? externalSources = null)
    {
        CollectionId = collectionId;
        Id = id;
        Summary = state.Summary;
        IsVariousArtists = state.IsVariousArtists;
        IsNotOnLabel = state.IsNotOnLabel;
        _externalSources = [.. externalSources ?? []];
        _labels = [.. state.Labels];
        _tracklist = [.. state.Tracklist];
        _genres = [.. cataloging.Genres];
        _tags = [.. cataloging.Tags];
    }

    public CollectionId CollectionId { get; private set; }

    public ReleaseId Id { get; private set; }

    public ReleaseSummary Summary { get; private set; }

    public bool IsVariousArtists { get; private set; }

    public bool IsNotOnLabel { get; private set; }

    public IReadOnlyList<ReleaseLabel> Labels => _labels.AsReadOnly();

    public IReadOnlyList<ExternalSourceReference> ExternalSources => _externalSources.AsReadOnly();

    public IReadOnlyList<ReleaseTrack> Tracklist => _tracklist.AsReadOnly();

    public Cataloging Cataloging
    {
        get
        {
            Cataloging cataloging = Cataloging.Empty;

            foreach (Genre genre in _genres)
            {
                cataloging = cataloging.WithGenre(genre);
            }

            foreach (Tag tag in _tags)
            {
                cataloging = cataloging.WithTag(tag);
            }

            return cataloging;
        }
    }

    public string DisplayName => Summary.Title;

    public static Release Create(CollectionId collectionId, ReleaseId id, string title)
    {
        return new Release(collectionId, id, ReleaseState.FromSummary(ReleaseSummary.Create(title)), Cataloging.Empty);
    }

    public void UpdateSummary(ReleaseSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        Summary = summary;
    }

    public void UpdateCataloging(Cataloging cataloging)
    {
        ArgumentNullException.ThrowIfNull(cataloging);

        _genres.Clear();
        _genres.AddRange(cataloging.Genres);
        _tags.Clear();
        _tags.AddRange(cataloging.Tags);
    }

    public void ReplaceExternalSources(IReadOnlyList<ExternalSourceReference> externalSources)
    {
        ExternalSourceReferences.Replace(_externalSources, externalSources);
    }

    public void UpdateArtistDisplay(bool isVariousArtists)
    {
        IsVariousArtists = isVariousArtists;
    }

    public void UpdateLabels(bool isNotOnLabel, IReadOnlyList<ReleaseLabel> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        IsNotOnLabel = isNotOnLabel;
        _labels.Clear();

        if (!isNotOnLabel)
        {
            _labels.AddRange(labels);
        }
    }

    public void ReplaceTracklist(IReadOnlyList<ReleaseTrack> tracklist)
    {
        ArgumentNullException.ThrowIfNull(tracklist);

        _tracklist.Clear();

        foreach (ReleaseTrack releaseTrack in tracklist)
        {
            EnsureTrackPositionIsUnique(releaseTrack.Position);
            _tracklist.Add(releaseTrack);
        }
    }

    public Release WithSummary(ReleaseSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new Release(CollectionId, Id, CurrentState() with { Summary = summary }, Cataloging, _externalSources);
    }

    public Release WithTrack(ReleaseTrack releaseTrack)
    {
        ArgumentNullException.ThrowIfNull(releaseTrack);

        EnsureTrackPositionIsUnique(releaseTrack.Position);

        return new Release(CollectionId, Id, CurrentState() with { Tracklist = [.. _tracklist, releaseTrack] }, Cataloging, _externalSources);
    }

    public Release WithCataloging(Cataloging cataloging)
    {
        ArgumentNullException.ThrowIfNull(cataloging);

        return new Release(CollectionId, Id, CurrentState(), cataloging, _externalSources);
    }

    private ReleaseState CurrentState()
    {
        return new ReleaseState(Summary, IsVariousArtists, IsNotOnLabel, _labels, _tracklist);
    }

    private void EnsureTrackPositionIsUnique(TrackPosition position)
    {
        if (Tracklist.Any(existing => existing.Position == position))
        {
            throw new DomainException("release_track.position_duplicate", "Release track position already exists");
        }
    }

    private sealed record ReleaseState(
        ReleaseSummary Summary,
        bool IsVariousArtists,
        bool IsNotOnLabel,
        IReadOnlyList<ReleaseLabel> Labels,
        IReadOnlyList<ReleaseTrack> Tracklist)
    {
        public static ReleaseState FromSummary(ReleaseSummary summary)
        {
            return new ReleaseState(summary, false, false, [], []);
        }
    }
}
