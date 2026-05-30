namespace DiscWeave.Importing;

public sealed record CoverArtifactPayload(
    string SourcePath,
    string FileName,
    string Extension,
    string ContentType,
    long SizeBytes,
    string ContentBase64);
