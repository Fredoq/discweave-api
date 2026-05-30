namespace DiscWeave.Infrastructure.Persistence.Search;

internal static class SearchDocumentText
{
    public static string Facet(IEnumerable<string> values)
    {
        string[] normalized = [.. values.Select(NormalizeFacet).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return normalized.Length == 0 ? string.Empty : $"|{string.Join('|', normalized)}|";
    }

    public static string Pack(IEnumerable<string> values)
    {
        return string.Join('\u001f', values.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> Unpack(string value)
    {
        return value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static IReadOnlyList<string> UnpackFacet(string value)
    {
        return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string NormalizeFacet(string value)
    {
        return value.Trim().Replace("|", " ", StringComparison.Ordinal).ToLowerInvariant();
    }
}
