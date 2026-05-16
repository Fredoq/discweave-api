namespace Cratebase.Api.Features.Imports;

public sealed record DesktopCoverArtifactRequest(
    string FileName,
    string Extension,
    string ContentType,
    long SizeBytes,
    string ContentBase64);
