using DiscWeave.Domain.Imports;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Settings;

public static partial class SettingsImportPatternsEndpointRouteBuilderExtensions
{
    private static async Task<ImportPattern?> FindPatternAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        Guid patternId,
        CancellationToken cancellationToken)
    {
        return await context.ImportPatterns.SingleOrDefaultAsync(
            pattern => pattern.CollectionId == collectionId && pattern.Id == new ImportPatternId(patternId),
            cancellationToken);
    }

    private static IResult TestImportPattern(ImportPatternTestRequest request)
    {
        try
        {
            ImportPatternKind kind = ImportPatternKindMapper.Parse(request.Kind);
            ImportPatternTestResponse response = kind == ImportPatternKind.ReleaseFolder
                ? TestReleasePattern(request.Template, request.Input)
                : TestTrackPattern(request.Template, request.Input);

            return Results.Ok(response);
        }
        catch (Exception exception) when (exception is FormatException or DomainException)
        {
            return Results.BadRequest(new { code = "import_pattern.invalid", message = exception.Message });
        }
    }

    private static ImportPatternTestResponse TestReleasePattern(string template, string input)
    {
        ParsedReleaseFolder parsed = ReleaseFolderNameParser.Parse(input, [template]);

        return new ImportPatternTestResponse(
            parsed.MatchedTemplate is not null,
            new Dictionary<string, string?>
            {
                ["catalogNumber"] = parsed.CatalogNumber,
                ["releaseDate"] = parsed.ReleaseDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ["year"] = parsed.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["artist"] = string.Join(", ", parsed.ArtistNames),
                ["title"] = parsed.Title
            },
            [.. parsed.Issues.Select(issue => issue.Message)]);
    }

    private static ImportPatternTestResponse TestTrackPattern(string template, string input)
    {
        ParsedTrackFile parsed = TrackFileNameParser.Parse(input, [template]);

        return new ImportPatternTestResponse(
            parsed.MatchedTemplate is not null,
            new Dictionary<string, string?>
            {
                ["position"] = parsed.Position?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["artist"] = string.Join(", ", parsed.ArtistNames),
                ["title"] = parsed.Title
            },
            [.. parsed.Issues.Select(issue => issue.Message)]);
    }

    private static ImportPatternResponse ToResponse(ImportPattern pattern)
    {
        return new ImportPatternResponse(
            pattern.Id.Value,
            ImportPatternKindMapper.ToCode(pattern.Kind),
            pattern.Template,
            pattern.SortOrder,
            pattern.IsActive,
            pattern.IsBuiltin);
    }
}
