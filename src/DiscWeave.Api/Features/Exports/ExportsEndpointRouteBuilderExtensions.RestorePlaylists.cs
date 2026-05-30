using DiscWeave.Api.Features.Playlists;
using DiscWeave.Api.Features.Ratings;
using DiscWeave.Domain.Playlists;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;

namespace DiscWeave.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static void RestorePlaylists(
        DiscWeaveDbContext context,
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
        DiscWeaveDbContext context,
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
