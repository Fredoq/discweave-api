namespace Cratebase.Domain.Imports;

public static class ImportArtistNames
{
    public static bool IsVariousArtistsName(string? value)
    {
        string normalized = Normalize(value);

        return normalized is "va" or "various artists";
    }

    public static IReadOnlyList<string> Split(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || IsVariousArtistsName(value)
            ? []
            :
            [
                .. value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(part => !string.IsNullOrWhiteSpace(part))
            ];
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }
}
