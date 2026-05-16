using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Imports;

public sealed class ReleaseImportSession : IEntity<ReleaseImportSessionId>
{
    private ReleaseImportSession()
    {
        SourceRoot = string.Empty;
    }

    private ReleaseImportSession(CollectionId collectionId, ReleaseImportSessionId id, string sourceRoot, DateTimeOffset createdAt)
    {
        CollectionId = collectionId;
        Id = id;
        SourceRoot = Guard.RequiredText(sourceRoot, nameof(sourceRoot), "release_import.source_root_required");
        Status = ReleaseImportSessionStatus.ReadyForReview;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public CollectionId CollectionId { get; private set; }

    public ReleaseImportSessionId Id { get; private set; }

    public string SourceRoot { get; private set; }

    public ReleaseImportSessionStatus Status { get; private set; }

    public int DraftCount { get; private set; }

    public int TrackCount { get; private set; }

    public int IgnoredFileCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ReleaseImportSession Create(CollectionId collectionId, ReleaseImportSessionId id, string sourceRoot, DateTimeOffset createdAt)
    {
        return new ReleaseImportSession(collectionId, id, sourceRoot, createdAt);
    }

    public void UpdateCounts(int draftCount, int trackCount, int ignoredFileCount, DateTimeOffset updatedAt)
    {
        if (draftCount < 0 || trackCount < 0 || ignoredFileCount < 0)
        {
            throw new DomainException("release_import.counts_invalid", "Release import session counts cannot be negative");
        }

        DraftCount = draftCount;
        TrackCount = trackCount;
        IgnoredFileCount = ignoredFileCount;
        UpdatedAt = updatedAt;
    }

    public void Complete(DateTimeOffset updatedAt)
    {
        Status = ReleaseImportSessionStatus.Completed;
        UpdatedAt = updatedAt;
    }
}
