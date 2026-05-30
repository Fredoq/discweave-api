namespace DiscWeave.Api.Features.Imports;

public sealed record DesktopFolderScanRequest(
    string SourceRoot,
    IReadOnlyList<DesktopFolderScanFileRequest>? Files,
    int IgnoredFileCount);
