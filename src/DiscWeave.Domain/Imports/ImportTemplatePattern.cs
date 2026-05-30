using System.Text;
using System.Text.RegularExpressions;

namespace DiscWeave.Domain.Imports;

internal sealed class ImportTemplatePattern
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private ImportTemplatePattern(string template, Regex regex, int score)
    {
        Template = template;
        Regex = regex;
        Score = score;
    }

    public string Template { get; }

    public int Score { get; }

    private Regex Regex { get; }

    public static ImportTemplatePattern Compile(string template)
    {
        var regex = new StringBuilder("^\\s*");
        int score = 0;

        int index = 0;
        while (index < template.Length)
        {
            char current = template[index];
            if (current == '{')
            {
                int end = template.IndexOf('}', index);
                if (end < 0)
                {
                    throw new FormatException("Import pattern token is not closed");
                }

                string token = template[(index + 1)..end];
                _ = regex.Append(TokenExpression(token));
                score += TokenScore(token);
                index = end + 1;
                continue;
            }

            AppendLiteral(regex, current);
            index++;
        }

        _ = regex.Append("\\s*$");
        return new ImportTemplatePattern(
            template,
            new Regex(regex.ToString(), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, MatchTimeout),
            score);
    }

    public ImportPatternMatch? Match(string value)
    {
        Match match = Regex.Match(value.Trim());
        return !match.Success
            ? null
            : new ImportPatternMatch(
                Template,
                Score,
                match.Groups.Values
                    .Where(group => group.Name.Any(char.IsLetter) && group.Success)
                    .ToDictionary(group => group.Name, group => group.Value.Trim(), StringComparer.Ordinal));
    }

    private static string TokenExpression(string token)
    {
        return token switch
        {
            "artist" => "(?<artist>.+?)",
            "catalogNumber" => "(?<catalogNumber>[^\\],]+?)",
            "position" => "(?<position>\\d{1,3})",
            "releaseDate" => "(?<releaseDate>\\d{4}-\\d{2}-\\d{2})",
            "title" => "(?<title>.+?)",
            _ => throw new FormatException($"Import pattern token '{token}' is not supported")
        };
    }

    private static int TokenScore(string token)
    {
        return token switch
        {
            "artist" => 10,
            "catalogNumber" => 3,
            "position" => 3,
            "releaseDate" => 3,
            "title" => 2,
            _ => 0
        };
    }

    private static void AppendLiteral(StringBuilder regex, char current)
    {
        if (char.IsWhiteSpace(current))
        {
            _ = regex.Append("\\s+");
            return;
        }

        if (current == '-')
        {
            _ = regex.Append("\\s*[-–—]\\s*");
            return;
        }

        _ = regex.Append(Regex.Escape(current.ToString()));
    }
}

internal sealed record ImportPatternMatch(string Template, int Score, IReadOnlyDictionary<string, string> Values);
