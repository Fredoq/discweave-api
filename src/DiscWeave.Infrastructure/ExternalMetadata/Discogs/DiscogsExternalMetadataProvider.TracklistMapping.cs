using System.Text.RegularExpressions;
using DiscWeave.Application.ExternalMetadata;

namespace DiscWeave.Infrastructure.ExternalMetadata.Discogs;

public sealed partial class DiscogsExternalMetadataProvider
{
    private static ExternalMetadataReleaseTrack[] MapReleaseTracklist(IReadOnlyList<DiscogsTrackResponse>? tracklist)
    {
        if (tracklist is null)
        {
            return [];
        }

        var tracks = new List<ExternalMetadataReleaseTrack>();
        string? currentDisc = null;
        string? currentSide = null;

        foreach (DiscogsTrackResponse row in tracklist)
        {
            if (IsHeadingRow(row))
            {
                string? heading = EmptyToNull(row.Title);
                if (heading is null)
                {
                    continue;
                }

                string? side = SideFromHeading(heading);
                if (side is null)
                {
                    currentDisc = heading;
                    currentSide = null;
                }
                else
                {
                    currentSide = side;
                }

                continue;
            }

            tracks.Add(MapReleaseTrack(row, currentDisc, currentSide ?? SideFromTrackPosition(row.Position)));
        }

        return [.. tracks];
    }

    private static ExternalMetadataReleaseTrack MapReleaseTrack(DiscogsTrackResponse track, string? disc, string? side)
    {
        return new ExternalMetadataReleaseTrack(
            track.Title ?? string.Empty,
            EmptyToNull(track.Position),
            ParseDuration(track.Duration),
            track.Artists?.Select(artist => artist.Name).WhereNotBlank() ?? [],
            disc,
            side);
    }

    private static bool IsTrackRow(DiscogsTrackResponse track)
    {
        return !string.Equals(track.Type, "heading", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHeadingRow(DiscogsTrackResponse track)
    {
        return string.Equals(track.Type, "heading", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SideFromHeading(string heading)
    {
        Match match = SideHeadingRegex().Match(heading);
        return match.Success ? match.Groups["side"].Value.Trim() : null;
    }

    private static string? SideFromTrackPosition(string? position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return null;
        }

        Match match = TrackPositionSideRegex().Match(position.Trim());
        return match.Success ? match.Groups["side"].Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex("^side\\s+(?<side>[A-Za-z0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SideHeadingRegex();

    [GeneratedRegex("^(?<side>[A-Za-z])\\d+", RegexOptions.CultureInvariant)]
    private static partial Regex TrackPositionSideRegex();
}
