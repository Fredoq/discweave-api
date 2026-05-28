using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.CatalogQuality;

public static partial class CatalogQualityEndpointRouteBuilderExtensions
{
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;
    private const string TargetTypeProperty = "_targetType";
    private const string TargetReleaseIdProperty = "_targetReleaseId";
    private const string TargetTrackIdProperty = "_targetTrackId";
    private const string StatusProperty = "_status";
    private const string MediumTypeProperty = "_mediumType";
    private const string DigitalFilePathProperty = "_digitalFilePath";
    private const string DigitalFileFormatProperty = "_digitalFileFormat";
    private const string ImportIdentityContentHashProperty = "_importIdentityContentHash";
    private const string ConditionProperty = "_condition";
    private const string StorageLocationProperty = "_storageLocation";
    private const string ReleaseTargetType = "release";
    private const string TrackTargetType = "track";
    private static readonly AudioFileFormat?[] LossyFormats = [AudioFileFormat.Mp3, AudioFileFormat.Ogg, AudioFileFormat.M4a];
    private static readonly AudioFileFormat?[] LosslessFormats = [AudioFileFormat.Flac, AudioFileFormat.Wav, AudioFileFormat.Aiff, AudioFileFormat.Alac];

    public static IEndpointRouteBuilder MapCatalogQualityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/catalog-quality")
            .WithTags("Catalog Quality")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", GetCatalogQualityAsync).WithName("GetCatalogQuality");

        return endpoints;
    }

    private static async Task<IResult> GetCatalogQualityAsync(
        int? limit,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeLimit(limit, out int normalizedLimit))
        {
            return EndpointErrors.BadRequest("catalog_quality.limit_invalid", "Catalog quality limit must be between 1 and 100");
        }

        Release[] releases = await context.Releases.AsNoTracking()
            .Where(release => release.CollectionId == currentCollection.CollectionId)
            .ToArrayAsync(cancellationToken);
        Track[] tracks = await context.Tracks.AsNoTracking()
            .Where(track => track.CollectionId == currentCollection.CollectionId)
            .ToArrayAsync(cancellationToken);
        OwnedItemProjection[] ownedItems = await context.OwnedItems.AsNoTracking()
            .Where(item => item.CollectionId == currentCollection.CollectionId)
            .Select(item => new OwnedItemProjection(
                item.Id,
                EF.Property<string>(item, TargetTypeProperty),
                EF.Property<ReleaseId?>(item, TargetReleaseIdProperty),
                EF.Property<TrackId?>(item, TargetTrackIdProperty),
                EF.Property<OwnershipStatus>(item, StatusProperty),
                EF.Property<string>(item, MediumTypeProperty),
                EF.Property<string?>(item, DigitalFilePathProperty),
                EF.Property<AudioFileFormat?>(item, DigitalFileFormatProperty),
                EF.Property<string?>(item, ImportIdentityContentHashProperty),
                EF.Property<ItemCondition?>(item, ConditionProperty),
                EF.Property<string?>(item, StorageLocationProperty)))
            .ToArrayAsync(cancellationToken);

        Dictionary<Guid, string> releaseTitles = releases.ToDictionary(release => release.Id.Value, release => release.Summary.Title);
        Dictionary<Guid, string> trackTitles = tracks.ToDictionary(track => track.Id.Value, track => track.Title);

        var response = new CatalogQualityResponse(
            normalizedLimit,
            new CatalogQualityResponse.DuplicateCandidateReport(
                DuplicateGroupSection(releases, release => release.Summary.Title, release => release.Id.Value, normalizedLimit),
                DuplicateGroupSection(tracks, track => track.Title, track => track.Id.Value, normalizedLimit),
                DigitalIdentitySection(ownedItems, normalizedLimit)),
            new CatalogQualityResponse.MissingMetadataReport(
                SampleSection(
                    releases
                        .Where(release => !release.Summary.Metadata.Year.HasValue && !release.Summary.Metadata.ReleaseDate.HasValue)
                        .Select(release => new CatalogQualityResponse.Sample(release.Id.Value, release.Summary.Title)),
                    normalizedLimit),
                SampleSection(
                    releases
                        .Where(release => !release.Summary.Metadata.LabelId.HasValue && !release.IsNotOnLabel && release.Labels.Count == 0)
                        .Select(release => new CatalogQualityResponse.Sample(release.Id.Value, release.Summary.Title)),
                    normalizedLimit),
                SampleSection(
                    tracks
                        .Where(track => !track.Details.Duration.HasValue)
                        .Select(track => new CatalogQualityResponse.Sample(track.Id.Value, track.Title)),
                    normalizedLimit),
                OwnedItemSampleSection(
                    ownedItems.Where(item => item.Condition is null),
                    releaseTitles,
                    trackTitles,
                    normalizedLimit),
                OwnedItemSampleSection(
                    ownedItems.Where(item => string.IsNullOrWhiteSpace(item.StorageLocation)),
                    releaseTitles,
                    trackTitles,
                    normalizedLimit),
                OwnedItemSampleSection(
                    ownedItems.Where(item => item.MediumType == "digital" && item.DigitalFileFormat is null),
                    releaseTitles,
                    trackTitles,
                    normalizedLimit)),
            BuildFormatGapReport(ownedItems, releaseTitles, trackTitles, normalizedLimit));

        return Results.Ok(response);
    }

    private static bool TryNormalizeLimit(int? requestedLimit, out int limit)
    {
        limit = requestedLimit ?? DefaultLimit;
        return limit is >= 1 and <= MaxLimit;
    }

    private static CatalogQualityResponse.Section<CatalogQualityResponse.DuplicateGroup> DuplicateGroupSection<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, string> keySelector,
        Func<TItem, Guid> idSelector,
        int limit)
    {
        CatalogQualityResponse.DuplicateGroup[] groups = [.. items
            .Select(item => (Key: keySelector(item).Trim(), Id: idSelector(item)))
            .Where(item => item.Key.Length > 0)
            .GroupBy(item => NormalizeGroupKey(item.Key), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => new CatalogQualityResponse.DuplicateGroup(
                group.Select(item => item.Key).Order(StringComparer.OrdinalIgnoreCase).First(),
                group.Count(),
                [.. group.Select(item => item.Id).Order()]))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)];

        return new CatalogQualityResponse.Section<CatalogQualityResponse.DuplicateGroup>(
            groups.Length,
            [.. groups.Take(limit)]);
    }

    private static CatalogQualityResponse.Section<CatalogQualityResponse.DuplicateGroup> DigitalIdentitySection(
        IEnumerable<OwnedItemProjection> ownedItems,
        int limit)
    {
        CatalogQualityResponse.DuplicateGroup[] groups = [.. ownedItems
            .Select(item => (Key: DigitalIdentityKey(item), item.Id))
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .Select(item => (Key: item.Key ?? string.Empty, Id: item.Id.Value))
            .GroupBy(item => NormalizeGroupKey(item.Key), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => new CatalogQualityResponse.DuplicateGroup(
                group.Select(item => item.Key).Order(StringComparer.OrdinalIgnoreCase).First(),
                group.Count(),
                [.. group.Select(item => item.Id).Order()]))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)];

        return new CatalogQualityResponse.Section<CatalogQualityResponse.DuplicateGroup>(
            groups.Length,
            [.. groups.Take(limit)]);
    }

    private static CatalogQualityResponse.Section<CatalogQualityResponse.Sample> OwnedItemSampleSection(
        IEnumerable<OwnedItemProjection> ownedItems,
        IReadOnlyDictionary<Guid, string> releaseTitles,
        IReadOnlyDictionary<Guid, string> trackTitles,
        int limit)
    {
        return SampleSection(
            ownedItems.Select(item => new CatalogQualityResponse.Sample(
                item.Id.Value,
                ResolveTargetTitle(item.TargetType, TargetId(item), releaseTitles, trackTitles),
                item.TargetType)),
            limit);
    }

    private static CatalogQualityResponse.FormatGapReport BuildFormatGapReport(
        IEnumerable<OwnedItemProjection> ownedItems,
        IReadOnlyDictionary<Guid, string> releaseTitles,
        IReadOnlyDictionary<Guid, string> trackTitles,
        int limit)
    {
        TargetGroup[] targetGroups = [.. ownedItems
            .Select(item => new TargetItem(
                new TargetKey(item.TargetType, TargetId(item)),
                item.Status,
                item.DigitalFileFormat))
            .Where(item => item.Target.Id != Guid.Empty)
            .GroupBy(item => item.Target)
            .Select(group => new TargetGroup(group.Key, [.. group]))];

        return new CatalogQualityResponse.FormatGapReport(
            TargetSampleSection(
                targetGroups.Where(group =>
                    group.Items.Any(item => item.DigitalFileFormat is null) &&
                    !group.Items.Any(item => item.DigitalFileFormat is not null)),
                releaseTitles,
                trackTitles,
                limit),
            TargetSampleSection(
                targetGroups.Where(group =>
                    group.Items.Any(item => LossyFormats.Contains(item.DigitalFileFormat)) &&
                    !group.Items.Any(item => LosslessFormats.Contains(item.DigitalFileFormat))),
                releaseTitles,
                trackTitles,
                limit),
            TargetSampleSection(
                targetGroups.Where(group =>
                    group.Items.Any(item => item.Status == OwnershipStatus.Wanted) &&
                    !group.Items.Any(item => item.Status == OwnershipStatus.Owned)),
                releaseTitles,
                trackTitles,
                limit),
            TargetSampleSection(
                targetGroups.Where(group => group.Items.Any(item => item.Status == OwnershipStatus.NeedsDigitization)),
                releaseTitles,
                trackTitles,
                limit));
    }

    private static CatalogQualityResponse.Section<CatalogQualityResponse.Sample> TargetSampleSection(
        IEnumerable<TargetGroup> groups,
        IReadOnlyDictionary<Guid, string> releaseTitles,
        IReadOnlyDictionary<Guid, string> trackTitles,
        int limit)
    {
        return SampleSection(
            groups.Select(group => new CatalogQualityResponse.Sample(
                group.Target.Id,
                ResolveTargetTitle(group.Target.Type, group.Target.Id, releaseTitles, trackTitles),
                group.Target.Type)),
            limit);
    }

    private static CatalogQualityResponse.Section<CatalogQualityResponse.Sample> SampleSection(
        IEnumerable<CatalogQualityResponse.Sample> samples,
        int limit)
    {
        CatalogQualityResponse.Sample[] orderedSamples = [.. samples
            .OrderBy(sample => sample.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sample => sample.Id)];

        return new CatalogQualityResponse.Section<CatalogQualityResponse.Sample>(
            orderedSamples.Length,
            [.. orderedSamples.Take(limit)]);
    }

    private static string NormalizeGroupKey(string key)
    {
        return key.Trim().ToUpperInvariant();
    }

    private static string? DigitalIdentityKey(OwnedItemProjection item)
    {
        return !string.IsNullOrWhiteSpace(item.ImportIdentityContentHash)
            ? item.ImportIdentityContentHash.Trim()
            : item.DigitalFilePath?.Trim();
    }

    private static Guid TargetId(OwnedItemProjection item)
    {
        return item.TargetType switch
        {
            ReleaseTargetType when item.TargetReleaseId is { } releaseId => releaseId.Value,
            TrackTargetType when item.TargetTrackId is { } trackId => trackId.Value,
            _ => Guid.Empty
        };
    }

    private static string ResolveTargetTitle(
        string targetType,
        Guid targetId,
        IReadOnlyDictionary<Guid, string> releaseTitles,
        IReadOnlyDictionary<Guid, string> trackTitles)
    {
        return targetType switch
        {
            ReleaseTargetType when releaseTitles.TryGetValue(targetId, out string? title) => title,
            TrackTargetType when trackTitles.TryGetValue(targetId, out string? title) => title,
            _ => targetId.ToString("D")
        };
    }

}
