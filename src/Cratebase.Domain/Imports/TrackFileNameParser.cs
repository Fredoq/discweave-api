namespace Cratebase.Domain.Imports;

public static class TrackFileNameParser
{
    public static readonly string[] DefaultTemplates =
    [
        "{position} {title}",
        "{position} - {title}",
        "{position} {artist} - {title}",
        "{position} - {artist} - {title}"
    ];

    private static readonly string[] AudioExtensions = [".flac", ".mp3", ".wav", ".ogg", ".m4a"];

    public static ParsedTrackFile Parse(string fileName)
    {
        return Parse(fileName, DefaultTemplates);
    }

    public static ParsedTrackFile Parse(string fileName, IReadOnlyList<string> templates)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(templates);

        string baseName = BaseName(fileName);
        Dictionary<string, int> templateOrder = new(StringComparer.Ordinal);
        for (int index = 0; index < templates.Count; index++)
        {
            string template = templates[index];
            if (!templateOrder.ContainsKey(template))
            {
                templateOrder[template] = index;
            }
        }

        ParsedTrackFile? best = templates
            .Select(template => ImportTemplatePattern.Compile(template).Match(baseName))
            .Where(match => match is not null)
            .Select(match => ToParsedTrack(match!))
            .OrderByDescending(Score)
            .ThenBy(parsed => parsed.MatchedTemplate is not null && templateOrder.TryGetValue(parsed.MatchedTemplate, out int index)
                ? index
                : int.MaxValue)
            .FirstOrDefault();

        return best ?? new ParsedTrackFile(
            null,
            baseName,
            [],
            [new ImportReviewIssue(ImportIssueCodes.PatternUnmatched, "Track file name did not match active import patterns")],
            null);
    }

    private static ParsedTrackFile ToParsedTrack(ImportPatternMatch match)
    {
        int? position = match.Values.TryGetValue("position", out string? value) && int.TryParse(value, out int parsed)
            ? parsed
            : null;

        return new ParsedTrackFile(
            position,
            ValueOrNull(match, "title"),
            ImportArtistNames.Split(ValueOrNull(match, "artist") ?? string.Empty),
            [],
            match.Template);
    }

    private static int Score(ParsedTrackFile parsed)
    {
        return (parsed.Position.HasValue ? 3 : 0) +
            (string.IsNullOrWhiteSpace(parsed.Title) ? 0 : 2) +
            (parsed.ArtistNames.Count > 0 ? 10 : 0);
    }

    private static string? ValueOrNull(ImportPatternMatch match, string key)
    {
        return match.Values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static string BaseName(string fileName)
    {
        string name = Path.GetFileName(fileName.Trim());
        string extension = Path.GetExtension(name);

        return AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            ? name[..^extension.Length]
            : name;
    }
}
