using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Features.CatalogQuality;

public static partial class CatalogQualityEndpointRouteBuilderExtensions
{
    private sealed record OwnedItemProjection(
        OwnedItemId Id,
        string TargetType,
        ReleaseId? TargetReleaseId,
        TrackId? TargetTrackId,
        OwnershipStatus Status,
        string MediumType,
        string? DigitalFilePath,
        AudioFileFormat? DigitalFileFormat,
        string? ImportIdentityContentHash,
        ItemCondition? Condition,
        string? StorageLocation);

    private sealed record TargetKey(string Type, Guid Id);

    private sealed record TargetItem(TargetKey Target, OwnershipStatus Status, AudioFileFormat? DigitalFileFormat);

    private sealed record TargetGroup(TargetKey Target, IReadOnlyList<TargetItem> Items);
}
