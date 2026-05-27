using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Credits;

public static partial class CreditsEndpointRouteBuilderExtensions
{
    private static async Task<CreditResponse> ToResponseAsync(
        Credit credit,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CreditResponse> responses = await ToResponsesAsync([credit], context, cancellationToken);

        return responses[0];
    }

    private static async Task<IReadOnlyList<CreditResponse>> ToResponsesAsync(
        IReadOnlyList<Credit> credits,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        Dictionary<ReleaseId, string> releaseTitles = await LoadReleaseTitlesAsync(credits, context, cancellationToken);
        Dictionary<TrackId, string> trackTitles = await LoadTrackTitlesAsync(credits, context, cancellationToken);

        return
        [
            .. credits.Select(credit => CreditMapper.ToResponse(
                credit,
                TargetTitle(credit, releaseTitles, trackTitles)))
        ];
    }

    private static async Task<Dictionary<ReleaseId, string>> LoadReleaseTitlesAsync(
        IReadOnlyList<Credit> credits,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        ReleaseId[] releaseIds =
        [
            .. credits
                .Select(credit => credit.Target)
                .OfType<ReleaseCreditTarget>()
                .Select(target => target.ReleaseId)
                .Distinct()
        ];
        CollectionId[] collectionIds = [.. credits.Select(credit => credit.CollectionId).Distinct()];

        return releaseIds.Length == 0
            ? []
            : await context.Releases.AsNoTracking()
                .Where(release => collectionIds.Contains(release.CollectionId) && releaseIds.Contains(release.Id))
                .ToDictionaryAsync(release => release.Id, release => release.Summary.Title, cancellationToken);
    }

    private static async Task<Dictionary<TrackId, string>> LoadTrackTitlesAsync(
        IReadOnlyList<Credit> credits,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        TrackId[] trackIds =
        [
            .. credits
                .Select(credit => credit.Target)
                .OfType<TrackCreditTarget>()
                .Select(target => target.TrackId)
                .Distinct()
        ];
        CollectionId[] collectionIds = [.. credits.Select(credit => credit.CollectionId).Distinct()];

        return trackIds.Length == 0
            ? []
            : await context.Tracks.AsNoTracking()
                .Where(track => collectionIds.Contains(track.CollectionId) && trackIds.Contains(track.Id))
                .ToDictionaryAsync(track => track.Id, track => track.Title, cancellationToken);
    }

    private static string? TargetTitle(
        Credit credit,
        IReadOnlyDictionary<ReleaseId, string> releaseTitles,
        IReadOnlyDictionary<TrackId, string> trackTitles)
    {
        return credit.Target switch
        {
            ReleaseCreditTarget target => releaseTitles.GetValueOrDefault(target.ReleaseId),
            TrackCreditTarget target => trackTitles.GetValueOrDefault(target.TrackId),
            _ => null
        };
    }
}
