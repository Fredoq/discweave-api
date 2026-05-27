using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.TrackRelations;

public static partial class TrackRelationsEndpointRouteBuilderExtensions
{
    private static async Task<TrackRelationResponse> ToResponseAsync(
        TrackRelation relation,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TrackRelationResponse> responses = await ToResponsesAsync([relation], context, cancellationToken);

        return responses[0];
    }

    private static async Task<IReadOnlyList<TrackRelationResponse>> ToResponsesAsync(
        IReadOnlyList<TrackRelation> relations,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        TrackId[] trackIds =
        [
            .. relations
                .SelectMany(relation => new[] { relation.SourceTrackId, relation.TargetTrackId })
                .Distinct()
        ];
        CollectionId[] collectionIds = [.. relations.Select(relation => relation.CollectionId).Distinct()];
        Dictionary<TrackId, string> trackTitles = trackIds.Length == 0
            ? []
            : await context.Tracks.AsNoTracking()
                .Where(track => collectionIds.Contains(track.CollectionId) && trackIds.Contains(track.Id))
                .ToDictionaryAsync(track => track.Id, track => track.Title, cancellationToken);

        return
        [
            .. relations.Select(relation => TrackRelationMapper.ToResponse(
                relation,
                trackTitles.GetValueOrDefault(relation.SourceTrackId),
                trackTitles.GetValueOrDefault(relation.TargetTrackId)))
        ];
    }
}
