using Cratebase.Domain.Collection;
using Cratebase.Domain.Playlists;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private static async Task<PlaylistItemResponse[]> ResolveSmartResultsAsync(
        Playlist playlist,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        PlaylistItemResponse[] releaseResults = await QuerySmartReleaseResultsAsync(
            playlist,
            context,
            SmartPlaylistResultLimit,
            cancellationToken);
        int remainingTrackLimit = Math.Max(0, SmartPlaylistResultLimit - releaseResults.Length);
        if (remainingTrackLimit == 0)
        {
            return releaseResults;
        }

        PlaylistItemResponse[] trackResults = await QuerySmartTrackResultsAsync(
            playlist,
            context,
            remainingTrackLimit,
            cancellationToken);

        return [.. releaseResults, .. trackResults];
    }

    private static OwnershipStatus? StatusFromCode(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "owned" => OwnershipStatus.Owned,
            "wanted" => OwnershipStatus.Wanted,
            "sold" => OwnershipStatus.Sold,
            "needsdigitization" => OwnershipStatus.NeedsDigitization,
            _ => null
        };
    }
}
