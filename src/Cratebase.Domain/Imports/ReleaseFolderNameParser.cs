namespace Cratebase.Domain.Imports;

public static class ReleaseFolderNameParser
{
    public const string DefaultTemplate = "[{catalogNumber}, {releaseDate}] {artist} - {title}";

    public static ParsedReleaseFolder Parse(string folderName)
    {
        return Parse(folderName, [DefaultTemplate]);
    }

    public static ParsedReleaseFolder Parse(string folderName, IReadOnlyList<string> templates)
    {
        ArgumentNullException.ThrowIfNull(folderName);
        ArgumentNullException.ThrowIfNull(templates);

        foreach (string template in templates)
        {
            ImportPatternMatch? match = ImportTemplatePattern.Compile(template).Match(folderName);
            if (match is not null)
            {
                return ToParsedRelease(match);
            }
        }

        return new ParsedReleaseFolder(
            null,
            null,
            null,
            [],
            false,
            folderName.Trim(),
            [new ImportReviewIssue(ImportIssueCodes.PatternUnmatched, "Release folder name did not match active import patterns")],
            null);
    }

    private static ParsedReleaseFolder ToParsedRelease(ImportPatternMatch match)
    {
        string? artist = ValueOrNull(match, "artist");
        ImportDateResult date = ImportDateParser.ParseReleaseDate(ValueOrNull(match, "releaseDate"));
        bool isVariousArtists = ImportArtistNames.IsVariousArtistsName(artist ?? string.Empty);

        return new ParsedReleaseFolder(
            ValueOrNull(match, "catalogNumber"),
            date.ReleaseDate,
            date.Year,
            isVariousArtists ? [] : ImportArtistNames.Split(artist ?? string.Empty),
            isVariousArtists,
            ValueOrNull(match, "title"),
            date.Issues,
            match.Template);
    }

    private static string? ValueOrNull(ImportPatternMatch match, string key)
    {
        return match.Values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }
}
