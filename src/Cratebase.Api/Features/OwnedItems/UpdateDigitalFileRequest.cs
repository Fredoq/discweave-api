namespace Cratebase.Api.Features.OwnedItems;

public sealed record UpdateDigitalFileRequest(
    string Path,
    string Format,
    long SizeBytes,
    DateTimeOffset LastModifiedAt,
    string? ContentHash);
