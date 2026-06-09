using System.Globalization;
using System.Text;
using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed partial class DiscogsExternalMetadataProvider
{
    public async Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>> SearchTracksAsync(
        ExternalMetadataTrackSearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ExternalMetadataError? configurationError = TryValidateConfiguration();
        if (configurationError is not null)
        {
            return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>(configurationError);
        }

        Dictionary<string, string> parameters = SearchParameters(query.Limit, "release");
        Add(parameters, "track", query.Title);
        Add(parameters, "artist", query.Artist);
        Add(parameters, "release_title", query.ReleaseTitle);
        Add(parameters, "year", query.Year?.ToString(CultureInfo.InvariantCulture));
        Add(parameters, "barcode", query.Barcode);
        Add(parameters, "catno", query.CatalogNumber);

        ExternalMetadataResult<DiscogsSearchResponse> response = await SendAsync<DiscogsSearchResponse>(
            "/database/search",
            parameters,
            cancellationToken);
        if (!response.IsSuccess)
        {
            return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>(response.Error);
        }

        List<ExternalMetadataTrackCandidate> candidates = [];
        foreach (DiscogsSearchResult result in response.Value.Results.Where(result => string.Equals(result.Type, "release", StringComparison.OrdinalIgnoreCase)))
        {
            ExternalMetadataResult<ExternalMetadataReleaseDetail> detail = await GetReleaseAsync(
                new ExternalMetadataLookupQuery(result.Id.ToString(CultureInfo.InvariantCulture)),
                cancellationToken);
            if (!detail.IsSuccess)
            {
                return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>(detail.Error);
            }

            candidates.AddRange(TrackCandidates(detail.Value, query));
            if (candidates.Count >= query.Limit)
            {
                break;
            }
        }

        ExternalMetadataTrackCandidate[] limited = [.. candidates.Take(query.Limit)];
        return new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>(
            new ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>(limited, limited.Length));
    }

    public async Task<ExternalMetadataResult<ExternalMetadataTrackDetail>> GetTrackAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ExternalMetadataError? configurationError = TryValidateConfiguration();
        if (configurationError is not null)
        {
            return new ExternalMetadataResult<ExternalMetadataTrackDetail>(configurationError);
        }

        if (!TryParseTrackExternalId(query.ExternalId, out string releaseId, out string position, out string title))
        {
            return new ExternalMetadataResult<ExternalMetadataTrackDetail>(InvalidResponse());
        }

        ExternalMetadataResult<ExternalMetadataReleaseDetail> release = await GetReleaseAsync(
            new ExternalMetadataLookupQuery(releaseId),
            cancellationToken);
        if (!release.IsSuccess)
        {
            return new ExternalMetadataResult<ExternalMetadataTrackDetail>(release.Error);
        }

        ExternalMetadataReleaseTrack? track = release.Value.Tracklist.FirstOrDefault(track => TrackIdentityMatches(track, position, title));
        return track is null
            ? new ExternalMetadataResult<ExternalMetadataTrackDetail>(InvalidResponse())
            : new ExternalMetadataResult<ExternalMetadataTrackDetail>(
            new ExternalMetadataTrackDetail(
                TrackSource(release.Value.Source, track),
                track.Title,
                track.Position,
                track.Duration,
                track.Artists,
                [.. release.Value.Credits
                    .Where(credit => TrackCreditMatches(credit, track))
                    .Select(credit => new ExternalMetadataTrackCredit(credit.Name, credit.Role))],
                ReleaseContext(release.Value)));
    }

    private static IEnumerable<ExternalMetadataTrackCandidate> TrackCandidates(
        ExternalMetadataReleaseDetail release,
        ExternalMetadataTrackSearchQuery query)
    {
        return release.Tracklist
            .Where(track => TrackMatches(track, release, query))
            .Select(track => new ExternalMetadataTrackCandidate(
                TrackSource(release.Source, track),
                track.Title,
                track.Position,
                track.Duration,
                track.Artists,
                ReleaseContext(release)));
    }

    private static bool TrackMatches(
        ExternalMetadataReleaseTrack track,
        ExternalMetadataReleaseDetail release,
        ExternalMetadataTrackSearchQuery query)
    {
        return TextMatches(track.Title, query.Title) &&
            (string.IsNullOrWhiteSpace(query.Artist) ||
                track.Artists.Any(artist => TextMatches(artist, query.Artist)) ||
                release.Artists.Any(artist => TextMatches(artist, query.Artist)));
    }

    private static bool TrackIdentityMatches(ExternalMetadataReleaseTrack track, string position, string title)
    {
        return string.Equals(track.Position ?? string.Empty, position, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(track.Title, title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrackCreditMatches(ExternalMetadataReleaseCredit credit, ExternalMetadataReleaseTrack track)
    {
        bool hasPosition = !string.IsNullOrWhiteSpace(credit.TrackPosition);
        bool hasTitle = !string.IsNullOrWhiteSpace(credit.TrackTitle);
        if (!hasPosition && !hasTitle)
        {
            return false;
        }

        bool positionMatches = !hasPosition ||
            string.Equals(credit.TrackPosition, track.Position, StringComparison.OrdinalIgnoreCase);
        bool titleMatches = !hasTitle ||
            string.Equals(credit.TrackTitle, track.Title, StringComparison.OrdinalIgnoreCase);

        return positionMatches && titleMatches;
    }

    private static bool TextMatches(string value, string? query)
    {
        return string.IsNullOrWhiteSpace(query) ||
            value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static ExternalMetadataSource TrackSource(ExternalMetadataSource releaseSource, ExternalMetadataReleaseTrack track)
    {
        return new ExternalMetadataSource(
            releaseSource.ProviderName,
            "track",
            TrackExternalId(releaseSource.ExternalId, track.Position, track.Title),
            releaseSource.SourceUrl,
            releaseSource.Attribution);
    }

    private static ExternalMetadataReleaseContext ReleaseContext(ExternalMetadataReleaseDetail release)
    {
        return new ExternalMetadataReleaseContext(release.Source, release.Title, release.Year, release.Artists);
    }

    private static string TrackExternalId(string releaseId, string? position, string title)
    {
        return $"{releaseId}:{EncodeTrackToken(position ?? string.Empty)}:{EncodeTrackToken(title)}";
    }

    private static bool TryParseTrackExternalId(
        string externalId,
        out string releaseId,
        out string position,
        out string title)
    {
        string[] parts = externalId.Split(':', 3, StringSplitOptions.None);
        if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]))
        {
            releaseId = string.Empty;
            position = string.Empty;
            title = string.Empty;
            return false;
        }

        releaseId = parts[0];
        bool decodedPosition = TryDecodeTrackToken(parts[1], out position);
        bool decodedTitle = TryDecodeTrackToken(parts[2], out title);
        return decodedPosition && decodedTitle;
    }

    private static string EncodeTrackToken(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Trim()))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeTrackToken(string value, out string decoded)
    {
        try
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return true;
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}
