namespace DiscWeave.Api.Features.CatalogGraph;

internal static class CatalogGraphEnumerableExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> values)
        where T : class
    {
        return values.Where(value => value is not null)!;
    }
}
