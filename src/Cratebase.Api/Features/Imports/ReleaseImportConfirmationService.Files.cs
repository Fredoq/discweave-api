using Cratebase.Application.Catalog.Releases;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Importing;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public sealed partial class ReleaseImportConfirmationService
{
    private async Task<ReleaseMetadata> ApplyCoverAsync(
        ReleaseMetadata metadata,
        ReleaseId releaseId,
        CollectionId collectionId,
        ReleaseImportDraft draft,
        CancellationToken cancellationToken)
    {
        if (draft.CoverContent is { Length: > 0 } coverContent &&
            !string.IsNullOrWhiteSpace(draft.CoverExtension) &&
            ReleaseImportFileRules.IsSupportedCover(draft.CoverExtension))
        {
            await using var artifactStream = new MemoryStream(coverContent);
            ReleaseCoverStoredFile storedArtifact = await _coverStorage.SaveAsync(
                collectionId,
                releaseId,
                draft.CoverExtension,
                artifactStream,
                cancellationToken);

            return metadata.WithCoverImage(CoverImage.FromLocalUpload(
                storedArtifact.StorageKey,
                draft.CoverContentType ?? ReleaseImportFileRules.CoverContentType(draft.CoverExtension),
                draft.CoverFileName ?? $"cover{draft.CoverExtension}",
                draft.CoverSizeBytes ?? coverContent.Length));
        }

        if (string.IsNullOrWhiteSpace(draft.CoverPath) || !File.Exists(draft.CoverPath) || !ReleaseImportFileRules.IsSupportedCover(draft.CoverPath))
        {
            return metadata;
        }

        await using FileStream stream = File.OpenRead(draft.CoverPath);
        ReleaseCoverStoredFile stored = await _coverStorage.SaveAsync(
            collectionId,
            releaseId,
            Path.GetExtension(draft.CoverPath),
            stream,
            cancellationToken);
        FileInfo file = new(draft.CoverPath);

        return metadata.WithCoverImage(CoverImage.FromLocalUpload(
            stored.StorageKey,
            ContentType(file.Extension),
            file.Name,
            file.Length));
    }

    private static async Task AddTrackOwnedItemAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Track track,
        ReleaseImportDraftTrack draftTrack,
        CancellationToken cancellationToken)
    {
        bool exists = await context.OwnedItems.AnyAsync(
            item => item.CollectionId == collectionId &&
                (EF.Property<string?>(item, "_digitalFilePath") == draftTrack.FilePath ||
                    (EF.Property<string?>(item, "_importIdentityPath") == draftTrack.FilePath &&
                        EF.Property<long?>(item, "_importIdentitySizeBytes") == draftTrack.SizeBytes &&
                        EF.Property<DateTimeOffset?>(item, "_importIdentityLastModifiedAt") == draftTrack.LastModifiedAt)),
            cancellationToken);
        if (exists)
        {
            return;
        }

        var path = FilePath.FromAbsolutePath(draftTrack.FilePath);
        var identity = FileImportIdentity.Create(path, draftTrack.SizeBytes, draftTrack.LastModifiedAt);
        var item = OwnedItem.Create(
            collectionId,
            OwnedItemId.New(),
            OwnedItemTarget.ForTrack(track.Id),
            OwnershipStatus.Owned,
            DigitalFile.Create(path, draftTrack.Format, identity));
        _ = context.OwnedItems.Add(item);
    }

    private static string ContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
