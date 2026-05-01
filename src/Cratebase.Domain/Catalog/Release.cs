using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.Ratings;

namespace Cratebase.Domain.Catalog;

public sealed class Release : IEntity<ReleaseId>, ICreditTarget
{
    private readonly List<Genre> _genres = [];
    private readonly List<Tag> _tags = [];
    private readonly List<ReleaseTrack> _tracklist = [];

    private Release()
    {
        Summary = ReleaseSummary.Empty;
    }

    private Release(
        ReleaseId id,
        ReleaseSummary summary,
        IReadOnlyList<ReleaseTrack> tracklist,
        Cataloging cataloging)
    {
        Id = id;
        Summary = summary;
        _tracklist = [.. tracklist];
        _genres = [.. cataloging.Genres];
        _tags = [.. cataloging.Tags];
    }

    public ReleaseId Id { get; private set; }

    public ReleaseSummary Summary { get; private set; }

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

    public static Release Create(ReleaseId id, string title)
    {
        return new Release(id, ReleaseSummary.Create(title), [], Cataloging.Empty);
    }

    public Release WithSummary(ReleaseSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new Release(Id, summary, _tracklist, Cataloging);
    }

    public Release WithRating(Rating rating)
    {
        return WithSummary(Summary.WithRating(rating));
    }

    public Release WithTrack(ReleaseTrack releaseTrack)
    {
        ArgumentNullException.ThrowIfNull(releaseTrack);

        EnsureTrackPositionIsUnique(releaseTrack.Position);

        return new Release(Id, Summary, [.. _tracklist, releaseTrack], Cataloging);
    }

    public Release WithCataloging(Cataloging cataloging)
    {
        ArgumentNullException.ThrowIfNull(cataloging);

        return new Release(Id, Summary, _tracklist, cataloging);
    }

    private void EnsureTrackPositionIsUnique(TrackPosition position)
    {
        if (Tracklist.Any(existing => existing.Position == position))
        {
            throw new DomainException("release_track.position_duplicate", "Release track position already exists");
        }
    }
}
