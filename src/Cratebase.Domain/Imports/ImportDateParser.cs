using System.Globalization;

namespace Cratebase.Domain.Imports;

internal static class ImportDateParser
{
    public static ImportDateResult ParseReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ImportDateResult(null, null, []);
        }

        string trimmed = value.Trim();
        int? leadingYear = trimmed.Length >= 4 && int.TryParse(trimmed[..4], CultureInfo.InvariantCulture, out int year)
            ? year
            : null;

        if (leadingYear is { } partialYear && trimmed.EndsWith("-00-00", StringComparison.Ordinal))
        {
            return new ImportDateResult(
                null,
                partialYear,
                [new ImportReviewIssue(ImportIssueCodes.PartialReleaseDate, "Release date has unknown month or day")]);
        }

        DateOnly? parsedReleaseDate = null;
        int? parsedYear = leadingYear;
        IReadOnlyList<ImportReviewIssue> issues = [];

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly releaseDate))
        {
            parsedReleaseDate = releaseDate;
            parsedYear = releaseDate.Year;
        }
        else
        {
            issues = [new ImportReviewIssue(ImportIssueCodes.InvalidReleaseDate, "Release date could not be parsed", ImportReviewSeverity.Error)];
        }

        return new ImportDateResult(parsedReleaseDate, parsedYear, issues);
    }
}

internal sealed record ImportDateResult(DateOnly? ReleaseDate, int? Year, IReadOnlyList<ImportReviewIssue> Issues);
