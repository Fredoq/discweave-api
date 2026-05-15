using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Catalog;

public sealed record CoverImage
{
    public const string LocalUploadSourceType = "localUpload";

    private CoverImage(
        string storageKey,
        string contentType,
        string originalFileName,
        long sizeBytes,
        string sourceType)
    {
        StorageKey = Guard.RequiredText(storageKey, nameof(storageKey), "cover_image.storage_key_required");
        ContentType = Guard.RequiredText(contentType, nameof(contentType), "cover_image.content_type_required");
        OriginalFileName = Guard.RequiredText(Path.GetFileName(originalFileName), nameof(originalFileName), "cover_image.original_file_name_required");
        SizeBytes = Guard.Positive(sizeBytes, nameof(sizeBytes), "cover_image.size_bytes_required");
        SourceType = Guard.RequiredText(sourceType, nameof(sourceType), "cover_image.source_type_required");
    }

    public string StorageKey { get; }

    public string ContentType { get; }

    public string OriginalFileName { get; }

    public long SizeBytes { get; }

    public string SourceType { get; }

    public static CoverImage FromLocalUpload(
        string storageKey,
        string contentType,
        string originalFileName,
        long sizeBytes)
    {
        return FromStoredMetadata(
            storageKey,
            contentType,
            originalFileName,
            sizeBytes,
            LocalUploadSourceType);
    }

    public static CoverImage FromStoredMetadata(
        string storageKey,
        string contentType,
        string originalFileName,
        long sizeBytes,
        string sourceType)
    {
        return new CoverImage(storageKey, contentType, originalFileName, sizeBytes, sourceType);
    }
}
