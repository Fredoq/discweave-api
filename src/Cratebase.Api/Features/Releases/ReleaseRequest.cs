using Cratebase.Api.Features.OwnedItems;

namespace Cratebase.Api.Features.Releases;

public sealed record ReleaseRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Type { get; init; }

    public Guid? LabelId { get; init; }

    public int? Year { get; init; }

    public bool IsVariousArtists { get; init; }

    public bool NotOnLabel { get; init; }

    public IReadOnlyList<ReleaseArtistCreditRequest>? ArtistCredits { get; init; }

    public IReadOnlyList<ReleaseLabelRequest>? Labels { get; init; }

    public IReadOnlyList<string>? Genres { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public IReadOnlyList<ReleaseTrackRequest>? Tracklist { get; init; }

    public ReleaseOwnedCopyRequest? OwnedCopy { get; init; }
}

public sealed record ReleaseArtistCreditRequest(Guid? ArtistId, string? Name, string? Role);

public sealed record ReleaseLabelRequest(Guid? LabelId, string? Name, string? CatalogNumber, bool HasNoCatalogNumber);

public sealed record ReleaseTrackRequest(
    string Title,
    int Position,
    int? DurationSeconds,
    IReadOnlyList<ReleaseArtistCreditRequest>? ArtistCredits,
    string? VersionNote);

public sealed record ReleaseOwnedCopyRequest(
    string Status,
    MediumRequest Medium,
    string? Condition,
    string? StorageLocation);
