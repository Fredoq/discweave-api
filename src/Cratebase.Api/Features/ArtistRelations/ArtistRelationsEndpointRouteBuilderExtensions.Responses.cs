using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.ArtistRelations;

public static partial class ArtistRelationsEndpointRouteBuilderExtensions
{
    private static async Task<ArtistRelationResponse> ToResponseAsync(
        ArtistRelation relation,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ArtistRelationResponse> responses = await ToResponsesAsync([relation], context, cancellationToken);

        return responses[0];
    }

    private static async Task<IReadOnlyList<ArtistRelationResponse>> ToResponsesAsync(
        IReadOnlyList<ArtistRelation> relations,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        ArtistId[] artistIds =
        [
            .. relations
                .SelectMany(relation => new[] { relation.SourceArtistId, relation.TargetArtistId })
                .Distinct()
        ];
        CollectionId[] collectionIds = [.. relations.Select(relation => relation.CollectionId).Distinct()];
        Dictionary<ArtistId, string> artistNames = artistIds.Length == 0
            ? []
            : await context.Artists.AsNoTracking()
                .Where(artist => collectionIds.Contains(artist.CollectionId) && artistIds.Contains(artist.Id))
                .ToDictionaryAsync(artist => artist.Id, artist => artist.Name, cancellationToken);

        return
        [
            .. relations.Select(relation => ArtistRelationMapper.ToResponse(
                relation,
                artistNames.GetValueOrDefault(relation.SourceArtistId),
                artistNames.GetValueOrDefault(relation.TargetArtistId)))
        ];
    }
}
