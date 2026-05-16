namespace Cratebase.LocalAgent;

public sealed record LocalAgentHealthResponse(
    string Service,
    string Version,
    int ProtocolVersion,
    string Status);

public sealed record LocalAgentPickAndScanRequest(
    string BackendBaseUrl,
    string Token,
    IReadOnlyList<string>? ReleaseFolderPatterns,
    IReadOnlyList<string>? TrackFilePatterns);
