namespace DiscWeave.Application.ExternalMetadata;

public interface IExternalMetadataProvider
{
    string ProviderName { get; }

    Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>> SearchReleasesAsync(
        ExternalMetadataReleaseSearchQuery query,
        CancellationToken cancellationToken);

    Task<ExternalMetadataResult<ExternalMetadataReleaseDetail>> GetReleaseAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken);

    Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>> SearchArtistsAsync(
        ExternalMetadataArtistSearchQuery query,
        CancellationToken cancellationToken);

    Task<ExternalMetadataResult<ExternalMetadataArtistDetail>> GetArtistAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken);

    Task<ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>> SearchTracksAsync(
        ExternalMetadataTrackSearchQuery query,
        CancellationToken cancellationToken);

    Task<ExternalMetadataResult<ExternalMetadataTrackDetail>> GetTrackAsync(
        ExternalMetadataLookupQuery query,
        CancellationToken cancellationToken);
}

public sealed class ExternalMetadataResult<T>
{
    public ExternalMetadataResult(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        IsSuccess = true;
        Value = value;
        Error = null!;
    }

    public ExternalMetadataResult(ExternalMetadataError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        IsSuccess = false;
        Value = default!;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T Value { get; }

    public ExternalMetadataError Error { get; }
}

public sealed record ExternalMetadataError(
    ExternalMetadataErrorKind Kind,
    string Code,
    string Message,
    TimeSpan? RetryAfter = null);

public enum ExternalMetadataErrorKind
{
    Disabled,
    NotConfigured,
    Unauthorized,
    RateLimited,
    Timeout,
    Unavailable,
    InvalidResponse
}

public sealed record ExternalMetadataLookupQuery(string ExternalId);

public sealed record ExternalMetadataReleaseSearchQuery(
    string? Query = null,
    string? Artist = null,
    string? Title = null,
    int? Year = null,
    string? Barcode = null,
    string? CatalogNumber = null,
    int Limit = 25);

public sealed record ExternalMetadataArtistSearchQuery(
    string? Query = null,
    int Limit = 25);

public sealed record ExternalMetadataTrackSearchQuery(
    string? Title = null,
    string? Artist = null,
    string? ReleaseTitle = null,
    int? Year = null,
    string? Barcode = null,
    string? CatalogNumber = null,
    int Limit = 25);

public sealed record ExternalMetadataSearchResult<T>(
    IReadOnlyList<T> Items,
    int? Total);

public sealed record ExternalMetadataSource(
    string ProviderName,
    string ResourceType,
    string ExternalId,
    string SourceUrl,
    string Attribution);

public sealed record ExternalMetadataReleaseCandidate(
    ExternalMetadataSource Source,
    string Title,
    IReadOnlyList<string> Artists,
    int? Year,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Formats,
    string? CatalogNumber,
    IReadOnlyList<string> Barcodes);

public sealed record ExternalMetadataReleaseDetail(
    ExternalMetadataSource Source,
    string Title,
    IReadOnlyList<string> Artists,
    int? Year,
    DateOnly? ReleaseDate,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Formats,
    string? Type,
    IReadOnlyList<string> Genres,
    IReadOnlyList<ExternalMetadataReleaseTrack> Tracklist,
    IReadOnlyList<ExternalMetadataIdentifier> Identifiers,
    string? CatalogNumber,
    IReadOnlyList<ExternalMetadataReleaseLabel> LabelDetails,
    IReadOnlyList<ExternalMetadataReleaseCredit> Credits);

public sealed record ExternalMetadataReleaseLabel(
    string Name,
    string? CatalogNumber);

public sealed record ExternalMetadataReleaseCredit(
    string Name,
    string Role,
    string? TrackTitle,
    string? TrackPosition);

public sealed record ExternalMetadataReleaseTrack(
    string Title,
    string? Position,
    TimeSpan? Duration,
    IReadOnlyList<string> Artists,
    string? Disc,
    string? Side);

public sealed record ExternalMetadataIdentifier(
    string Type,
    string Value);

public sealed record ExternalMetadataArtistCandidate(
    ExternalMetadataSource Source,
    string Name,
    string? Profile,
    IReadOnlyList<string> NameVariations);

public sealed record ExternalMetadataArtistDetail(
    ExternalMetadataSource Source,
    string Name,
    string? Profile,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Members,
    IReadOnlyList<string> NameVariations);

public sealed record ExternalMetadataReleaseContext(
    ExternalMetadataSource Source,
    string Title,
    int? Year,
    IReadOnlyList<string> Artists);

public sealed record ExternalMetadataTrackCandidate(
    ExternalMetadataSource Source,
    string Title,
    string? Position,
    TimeSpan? Duration,
    IReadOnlyList<string> Artists,
    ExternalMetadataReleaseContext Release);

public sealed record ExternalMetadataTrackDetail(
    ExternalMetadataSource Source,
    string Title,
    string? Position,
    TimeSpan? Duration,
    IReadOnlyList<string> Artists,
    IReadOnlyList<ExternalMetadataTrackCredit> Credits,
    ExternalMetadataReleaseContext Release);

public sealed record ExternalMetadataTrackCredit(
    string Name,
    string Role);
