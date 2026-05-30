namespace DiscWeave.Api.Features.CatalogQuality;

public sealed record CatalogQualityResponse(
    int Limit,
    CatalogQualityResponse.DuplicateCandidateReport DuplicateCandidates,
    CatalogQualityResponse.MissingMetadataReport MissingMetadata,
    CatalogQualityResponse.FormatGapReport FormatGaps)
{
    public sealed record DuplicateCandidateReport(
        Section<DuplicateGroup> Releases,
        Section<DuplicateGroup> Tracks,
        Section<DuplicateGroup> DigitalFileIdentities);

    public sealed record MissingMetadataReport(
        Section<Sample> ReleasesMissingYearOrDate,
        Section<Sample> ReleasesMissingLabel,
        Section<Sample> TracksMissingDuration,
        Section<Sample> OwnedItemsMissingCondition,
        Section<Sample> OwnedItemsMissingStorageLocation,
        Section<Sample> OwnedItemsMissingDigitalFormat);

    public sealed record FormatGapReport(
        Section<Sample> PhysicalWithoutDigital,
        Section<Sample> LossyWithoutLossless,
        Section<Sample> WantedNotOwned,
        Section<Sample> NeedsDigitization);

    public sealed record Section<TItem>(int Total, IReadOnlyList<TItem> Items);

    public sealed record DuplicateGroup(string Key, int Count, IReadOnlyList<Guid> Ids);

    public sealed record Sample(Guid Id, string Title, string? TargetType = null);
}
