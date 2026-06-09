using DiscWeave.Api.Features.Settings;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;

namespace DiscWeave.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static async Task<string> ResolveReleaseTypeCodeAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        string? type,
        CancellationToken cancellationToken)
    {
        string code = string.IsNullOrWhiteSpace(type) ? "unknown" : type.Trim();
        return await DictionaryValidation.ResolveOrCreateActiveCodeAsync(
            context,
            collectionId,
            DictionaryKind.ReleaseType,
            code,
            "release.type_invalid",
            "Release type is invalid",
            cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ResolveGenreCodesAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IReadOnlyList<string>? genres,
        CancellationToken cancellationToken)
    {
        if (genres is null || genres.Count == 0)
        {
            return [];
        }

        string[] requestedCodes =
        [
            .. genres
                .Select(genre => string.IsNullOrWhiteSpace(genre)
                    ? throw new DomainException("release.genre_invalid", "Release genre is invalid")
                    : genre.Trim())
                .Distinct(StringComparer.Ordinal)
        ];

        var resolved = new List<string>(requestedCodes.Length);

        foreach (string code in requestedCodes)
        {
            string resolvedCode = await DictionaryValidation.ResolveOrCreateActiveCodeAsync(
                context,
                collectionId,
                DictionaryKind.Genre,
                code,
                "release.genre_invalid",
                "Release genre is invalid",
                cancellationToken);
            resolved.Add(resolvedCode);
        }

        return resolved;
    }
}
