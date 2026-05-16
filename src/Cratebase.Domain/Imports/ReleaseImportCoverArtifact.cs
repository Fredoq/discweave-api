namespace Cratebase.Domain.Imports;

public sealed record ReleaseImportCoverArtifact(
    string FileName,
    string Extension,
    string ContentType,
    long SizeBytes,
    byte[] Content);
