using System.Collections.ObjectModel;

namespace DiscWeave.Domain.Catalog;

public sealed class Cataloging
{
    private Cataloging(IReadOnlyList<Genre> genres, IReadOnlyList<Tag> tags)
    {
        Genres = genres;
        Tags = tags;
    }

    public IReadOnlyList<Genre> Genres { get; }

    public IReadOnlyList<Tag> Tags { get; }

    public static Cataloging Empty { get; } = new(ReadOnly(Array.Empty<Genre>()), ReadOnly(Array.Empty<Tag>()));

    public Cataloging WithGenre(Genre genre)
    {
        ArgumentNullException.ThrowIfNull(genre);

        return Genres.Contains(genre) ? this : new Cataloging(ReadOnly(Genres.Append(genre)), Tags);
    }

    public Cataloging WithTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        return Tags.Contains(tag) ? this : new Cataloging(Genres, ReadOnly(Tags.Append(tag)));
    }

    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values)
    {
        return Array.AsReadOnly(values.ToArray());
    }
}
