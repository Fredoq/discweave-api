using DiscWeave.Api.Features.Credits;
using DiscWeave.Api.Features.Settings;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;

namespace DiscWeave.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private const string MainArtistRoleCode = "mainArtist";

    private static readonly CreditArtistResolverErrors ReleaseCreditArtistErrors = new(
        "release.artist_conflict",
        "Release artist does not exist",
        "release.artist_name_required",
        "Release artist name is required");

    private static async Task<IReadOnlyList<ResolvedCredit>> ResolveTrackCreditsAsync(
        IReadOnlyList<ReleaseArtistCreditRequest>? artistCredits,
        IReadOnlyList<ResolvedCredit> releaseCredits,
        bool isVariousArtists,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResolvedCredit> resolvedCredits = await ResolveCreditsAsync(artistCredits, context, collectionId, cancellationToken);
        return resolvedCredits.Count > 0
            ? resolvedCredits
            : ResolveDefaultTrackCredits(releaseCredits, isVariousArtists);
    }

    private static IReadOnlyList<ResolvedCredit> ResolveDefaultTrackCredits(
        IReadOnlyList<ResolvedCredit> releaseCredits,
        bool isVariousArtists)
    {
        return isVariousArtists
            ? throw new DomainException("track.artist_required", "Track artist is required for Various Artists releases")
            : [.. releaseCredits.Where(credit => credit.Roles.Contains(MainArtistRoleCode, StringComparer.Ordinal))];
    }

    private static async Task<IReadOnlyList<ResolvedCredit>> ResolveCreditsAsync(
        IReadOnlyList<ReleaseArtistCreditRequest>? artistCredits,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        if (artistCredits is null || artistCredits.Count == 0)
        {
            return [];
        }

        var resolved = new List<ResolvedCredit>();
        foreach (ReleaseArtistCreditRequest creditRequest in artistCredits)
        {
            Artist artist = await CreditArtistResolver.ResolveAsync(
                creditRequest.ArtistId,
                creditRequest.Name,
                context,
                collectionId,
                ReleaseCreditArtistErrors,
                cancellationToken);
            string[] roles = await ResolveRoleCodesAsync(creditRequest.Role, creditRequest.Roles, MainArtistRoleCode, context, collectionId, cancellationToken);
            resolved.Add(new ResolvedCredit(artist, roles));
        }

        return MergeCredits(resolved);
    }

    private static async Task<string[]> ResolveRoleCodesAsync(
        string? legacyRole,
        IReadOnlyList<string>? roles,
        string defaultRole,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        string[] requestedRoles = NormalizeRequestedRoles(legacyRole, roles, defaultRole);
        var resolved = new List<string>(requestedRoles.Length);
        foreach (string requestedRole in requestedRoles)
        {
            string role = await DictionaryValidation.ResolveOrCreateActiveCodeAsync(
                context,
                collectionId,
                DictionaryKind.CreditRole,
                CreditMapper.ParseRole(requestedRole),
                "credit.role_invalid",
                "Credit role is invalid",
                cancellationToken);
            if (!resolved.Contains(role, StringComparer.Ordinal))
            {
                resolved.Add(role);
            }
        }

        return [.. resolved];
    }

    private static string[] NormalizeRequestedRoles(string? legacyRole, IReadOnlyList<string>? roles, string defaultRole)
    {
        IEnumerable<string> requested = roles is { Count: > 0 }
            ? roles
            : [legacyRole ?? defaultRole];

        string[] normalized =
        [
            .. requested
                .SelectMany(SplitRoleLabel)
                .Select(role => role.Trim())
                .Where(role => role.Length > 0)
                .Distinct(StringComparer.Ordinal)
        ];

        return normalized.Length > 0 ? normalized : [defaultRole];
    }

    private static IEnumerable<string> SplitRoleLabel(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return [];
        }

        var values = new List<string>();
        int bracketDepth = 0;
        int segmentStart = 0;
        for (int index = 0; index < role.Length; index++)
        {
            char value = role[index];
            bracketDepth += value switch
            {
                '[' => 1,
                ']' when bracketDepth > 0 => -1,
                _ => 0
            };

            if (value != ',' || bracketDepth > 0)
            {
                continue;
            }

            AddSegment(role, segmentStart, index, values);
            segmentStart = index + 1;
        }

        AddSegment(role, segmentStart, role.Length, values);
        return values;
    }

    private static void AddSegment(string role, int start, int end, List<string> values)
    {
        string segment = role[start..end].Trim();
        if (segment.Length > 0)
        {
            values.Add(segment);
        }
    }

    private static IReadOnlyList<ResolvedCredit> MergeCredits(IReadOnlyList<ResolvedCredit> credits)
    {
        return
        [
            .. credits
                .GroupBy(credit => credit.Artist.Id)
                .Select(group => new ResolvedCredit(
                    group.First().Artist,
                    [.. group.SelectMany(credit => credit.Roles).Distinct(StringComparer.Ordinal)]))
        ];
    }

    private sealed record ResolvedCredit(Artist Artist, IReadOnlyList<string> Roles)
    {
        public string Role => Roles[0];
    }
}
