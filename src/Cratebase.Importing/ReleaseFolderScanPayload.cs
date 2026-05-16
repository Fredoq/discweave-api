namespace Cratebase.Importing;

public sealed record ReleaseFolderScanPayload(
    string SourceRoot,
    IReadOnlyList<ReleaseFolderScanDraft> Drafts,
    int IgnoredFileCount);
