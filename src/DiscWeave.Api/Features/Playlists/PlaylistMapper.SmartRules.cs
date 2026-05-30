using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Playlists;
using DiscWeave.Infrastructure.Persistence;

namespace DiscWeave.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private static async Task<PlaylistItemResponse[]> ResolveSmartResultsAsync(
        Playlist playlist,
        DiscWeaveDbContext context,
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
