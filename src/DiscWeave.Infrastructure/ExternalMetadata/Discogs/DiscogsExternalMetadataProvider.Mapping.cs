using System.Globalization;
using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed partial class DiscogsExternalMetadataProvider
{
    private static ExternalMetadataReleaseCandidate MapReleaseCandidate(DiscogsSearchResult result)
    {
        return new ExternalMetadataReleaseCandidate(
            Source(result, "release"),
            result.Title ?? string.Empty,
            ArtistNamesFromTitle(result.Title),
            result.Year,
            result.Label ?? [],
            result.Format ?? [],
            EmptyToNull(result.Catno),
            result.Barcode ?? []);
    }

    private static ExternalMetadataArtistCandidate MapArtistCandidate(DiscogsSearchResult result)
    {
        return new ExternalMetadataArtistCandidate(
            Source(result, "artist"),
            result.Title ?? string.Empty,
            null,
            []);
    }

    private static ExternalMetadataTrackCandidate MapTrackCandidate(DiscogsSearchResult result, string? requestedTitle)
    {
        ExternalMetadataSource source = Source(result, "release");
        return new ExternalMetadataTrackCandidate(
            new ExternalMetadataSource(source.ProviderName, "track", source.ExternalId, source.SourceUrl, source.Attribution),
            string.IsNullOrWhiteSpace(requestedTitle) ? result.Title ?? string.Empty : requestedTitle.Trim(),
            null,
            null,
            ArtistNamesFromTitle(result.Title),
            new ExternalMetadataReleaseContext(
                source,
                result.Title ?? string.Empty,
                result.Year,
                ArtistNamesFromTitle(result.Title)));
    }

    private static ExternalMetadataReleaseDetail MapReleaseDetail(DiscogsReleaseDetailResponse response)
    {
        return new ExternalMetadataReleaseDetail(
            Source(response.Id, "release", response.Uri),
            response.Title ?? string.Empty,
            response.Artists?.Select(artist => artist.Name).WhereNotBlank() ?? [],
            response.Year,
            response.Labels?.Select(label => label.Name).WhereNotBlank() ?? [],
            response.Formats?.Select(format => format.Name).WhereNotBlank() ?? [],
            response.Tracklist?.Select(MapReleaseTrack).ToArray() ?? [],
            response.Identifiers?.Select(ToIdentifier).ToArray() ?? [],
            response.Labels?.Select(label => label.CatalogNumber).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            response.Labels?.Select(ToReleaseLabel).Where(label => !string.IsNullOrWhiteSpace(label.Name)).ToArray() ?? [],
            ReleaseCredits(response));
    }

    private static ExternalMetadataReleaseTrack MapReleaseTrack(DiscogsTrackResponse track)
    {
        return new ExternalMetadataReleaseTrack(
            track.Title ?? string.Empty,
            EmptyToNull(track.Position),
            ParseDuration(track.Duration),
            track.Artists?.Select(artist => artist.Name).WhereNotBlank() ?? []);
    }

    private static ExternalMetadataArtistDetail MapArtistDetail(DiscogsArtistDetailResponse response)
    {
        return new ExternalMetadataArtistDetail(
            Source(response.Id, "artist", response.Uri),
            response.Name ?? string.Empty,
            EmptyToNull(response.Profile),
            response.Aliases?.Select(alias => alias.Name).WhereNotBlank() ?? [],
            response.Members?.Select(member => member.Name).WhereNotBlank() ?? [],
            response.NameVariations ?? []);
    }

    private static ExternalMetadataIdentifier ToIdentifier(DiscogsIdentifierResponse identifier)
    {
        return new ExternalMetadataIdentifier(identifier.Type ?? string.Empty, identifier.Value ?? string.Empty);
    }

    private static ExternalMetadataReleaseLabel ToReleaseLabel(DiscogsLabelResource label)
    {
        return new ExternalMetadataReleaseLabel(label.Name?.Trim() ?? string.Empty, EmptyToNull(label.CatalogNumber));
    }

    private static ExternalMetadataReleaseCredit[] ReleaseCredits(DiscogsReleaseDetailResponse response)
    {
        IEnumerable<ExternalMetadataReleaseCredit> releaseCredits = response.ExtraArtists
            ?.Select(artist => ToReleaseCredit(artist, null, null)) ?? [];
        IEnumerable<ExternalMetadataReleaseCredit> trackCredits = response.Tracklist
            ?.SelectMany(track => track.ExtraArtists?.Select(artist => ToReleaseCredit(artist, track.Title, track.Position)) ?? []) ?? [];

        return [.. releaseCredits.Concat(trackCredits).Where(credit => !string.IsNullOrWhiteSpace(credit.Name))];
    }

    private static ExternalMetadataReleaseCredit ToReleaseCredit(DiscogsNamedResource artist, string? trackTitle, string? trackPosition)
    {
        return new ExternalMetadataReleaseCredit(
            artist.Name?.Trim() ?? string.Empty,
            artist.Role?.Trim() ?? string.Empty,
            EmptyToNull(trackTitle),
            EmptyToNull(trackPosition));
    }

    private static ExternalMetadataSource Source(DiscogsSearchResult result, string resourceType)
    {
        return Source(result.Id, resourceType, result.Uri);
    }

    private static ExternalMetadataSource Source(long id, string resourceType, string? uri)
    {
        string sourceUrl = string.IsNullOrWhiteSpace(uri)
            ? $"https://www.discogs.com/{resourceType}/{id.ToString(CultureInfo.InvariantCulture)}"
            : ToDiscogsWebsiteUrl(uri);

        return new ExternalMetadataSource(
            ProviderNameValue,
            resourceType,
            id.ToString(CultureInfo.InvariantCulture),
            sourceUrl,
            Attribution);
    }

    private static string ToDiscogsWebsiteUrl(string uri)
    {
        string trimmed = uri.Trim();
        return trimmed.StartsWith('/')
            ? $"https://www.discogs.com{trimmed}"
            : Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute)
                ? absolute.ToString()
                : $"https://www.discogs.com/{trimmed}";
    }

    private static IReadOnlyList<string> ArtistNamesFromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return [];
        }

        string[] parts = title.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0])
            ? [parts[0]]
            : [];
    }

    private static TimeSpan? ParseDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        string[] parts = duration.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds)
                ? TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds)
                : null;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

file static class DiscogsEnumerableExtensions
{
    public static string[] WhereNotBlank(this IEnumerable<string?> values)
    {
        return [.. values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim())];
    }
}
