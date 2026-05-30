using DiscWeave.Domain.Catalog;

namespace DiscWeave.Api.Features;

internal static class CatalogingMapper
{
    public static Cataloging Create(IReadOnlyList<string>? genres, IReadOnlyList<string>? tags)
    {
        Cataloging cataloging = Cataloging.Empty;

        foreach (string genre in genres ?? [])
        {
            cataloging = cataloging.WithGenre(Genre.FromName(genre));
        }

        foreach (string tag in tags ?? [])
        {
            cataloging = cataloging.WithTag(Tag.FromName(tag));
        }

        return cataloging;
    }
}
