using Cratebase.Api.Features.Playlists;
using Cratebase.Api.Features.Ratings;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static void RestorePlaylists(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<PlaylistResponse> playlists)
    {
        foreach (PlaylistResponse response in playlists)
        {
            var playlist = Playlist.Create(collectionId, new PlaylistId(response.Id), response.Name, PlaylistMapper.ParseType(response.Type));
            playlist.UpdateDescription(OptionalText(response.Description));
            if (response.Type == "smart")
            {
                SmartPlaylistRulesResponse rules = response.Rules;
                playlist.ReplaceSmartRules(SmartPlaylistRules.Create(
                    rules.Tags,
                    rules.Genres,
                    rules.Media,
                    rules.OwnershipStatuses,
                    OptionalYear(rules.YearFrom),
                    OptionalYear(rules.YearTo)));
            }
            else
            {
                playlist.ReplaceManualEntries(ToPlaylistEntries(response.Entries));
            }

            _ = context.Playlists.Add(playlist);
        }
    }

    private static void RestoreRatings(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<RatingValueResponse> ratings)
    {
        foreach (RatingValueResponse response in ratings)
        {
            _ = context.RatingValues.Add(RatingValue.Create(
                collectionId,
                new RatingValueId(response.Id),
                new RatingCriterionId(response.CriterionId),
                RatingEndpointHelpers.CreateTarget(RatingTargetTypeCodes.FromCode(response.TargetType), response.TargetId),
                Rating.FromValue(response.Value)));
        }
    }
}
