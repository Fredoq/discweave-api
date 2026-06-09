using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Catalog;

public sealed class Track : IEntity<TrackId>, ICreditTarget
{
    private readonly List<ExternalSourceReference> _externalSources = [];
    private readonly List<Genre> _genres = [];
    private readonly List<Tag> _tags = [];

    private Track()
    {
        Title = string.Empty;
        Details = TrackDetails.Empty;
    }

    private Track(
        CollectionId collectionId,
        TrackId id,
        string title,
        TrackDetails details,
        Cataloging cataloging,
        IReadOnlyList<ExternalSourceReference>? externalSources = null)
    {
        CollectionId = collectionId;
        Id = id;
        Title = title;
        Details = details;
        _externalSources = [.. externalSources ?? []];
        _genres = [.. cataloging.Genres];
        _tags = [.. cataloging.Tags];
    }

    public CollectionId CollectionId { get; private set; }

    public TrackId Id { get; private set; }

    public string Title { get; private set; }

    public string DisplayName => Title;

    public TrackDetails Details { get; private set; }

    public IReadOnlyList<ExternalSourceReference> ExternalSources => _externalSources.AsReadOnly();

    public Cataloging Cataloging
    {
        get
        {
            Cataloging cataloging = _genres.Aggregate(Cataloging.Empty, (current, genre) => current.WithGenre(genre));

            return _tags.Aggregate(cataloging, (current, tag) => current.WithTag(tag));
        }
    }

    public static Track Create(CollectionId collectionId, TrackId id, string title)
    {
        return new Track(
            collectionId,
            id,
            Guard.RequiredText(title, nameof(title), "track.title_required"),
            TrackDetails.Empty,
            Cataloging.Empty);
    }

    public void Rename(string title)
    {
        Title = Guard.RequiredText(title, nameof(title), "track.title_required");
    }

    public void UpdateDetails(TrackDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        Details = details;
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

    public Track WithDetails(TrackDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        return new Track(CollectionId, Id, Title, details, Cataloging, _externalSources);
    }

    public Track WithDuration(TimeSpan duration)
    {
        return WithDetails(Details.WithDuration(duration));
    }

    public Track WithCataloging(Cataloging cataloging)
    {
        ArgumentNullException.ThrowIfNull(cataloging);

        return new Track(CollectionId, Id, Title, Details, cataloging, _externalSources);
    }
}
