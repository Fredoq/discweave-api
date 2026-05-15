using Cratebase.Api.Http;
using Cratebase.Application.Catalog.Releases;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private const long MaxCoverImageSizeBytes = 10 * 1024 * 1024;

    private static async Task<IResult> PutReleaseCoverImageAsync(
        Guid releaseId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        IReleaseCoverStorage storage,
        CancellationToken cancellationToken)
    {
        Release? release = await context.Releases.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ReleaseId(releaseId),
            cancellationToken);
        if (release is null)
        {
            return EndpointErrors.NotFound("release.not_found", "Release was not found");
        }

        if (!request.HasFormContentType)
        {
            return EndpointErrors.BadRequest("release_cover.form_required", "Release cover upload must use multipart/form-data");
        }

        IFormFile? file;
        try
        {
            IFormCollection form = await request.ReadFormAsync(cancellationToken);
            file = form.Files.Count == 1 && form.Files.GetFiles("file").Count == 1
                ? form.Files.GetFile("file")
                : null;
        }
        catch (InvalidDataException)
        {
            return EndpointErrors.BadRequest("release_cover.form_invalid", "Release cover upload form is invalid");
        }

        if (file is null)
        {
            return EndpointErrors.BadRequest("release_cover.file_required", "Release cover upload requires exactly one file field named file");
        }

        if (!TryValidateCoverFile(file, out CoverImageUploadFormat uploadFormat, out string? validationErrorCode, out string? validationErrorMessage))
        {
            return EndpointErrors.BadRequest(validationErrorCode, validationErrorMessage);
        }

        await using Stream fileStream = file.OpenReadStream();
        using var content = new MemoryStream((int)file.Length);
        await fileStream.CopyToAsync(content, cancellationToken);
        if (!HasValidSignature(content.ToArray(), uploadFormat))
        {
            return EndpointErrors.BadRequest("release_cover.signature_invalid", "Release cover file signature does not match the requested image format");
        }

        CoverImage? previousCoverImage = TryGetCoverImage(release);
        content.Position = 0;
        ReleaseCoverStoredFile storedFile = await storage.SaveAsync(
            currentCollection.CollectionId,
            release.Id,
            uploadFormat.Extension,
            content,
            cancellationToken);

        try
        {
            var coverImage = CoverImage.FromLocalUpload(
                storedFile.StorageKey,
                uploadFormat.ContentType,
                file.FileName,
                content.Length);
            release.UpdateSummary(release.Summary.WithMetadata(release.Summary.Metadata.WithCoverImage(coverImage)));
            _ = await context.SaveChangesAsync(cancellationToken);
            if (previousCoverImage is not null)
            {
                await storage.DeleteAsync(previousCoverImage.StorageKey, cancellationToken);
            }

            return Results.Ok(ToCoverImageResponse(release));
        }
        catch (DomainException exception)
        {
            await storage.DeleteAsync(storedFile.StorageKey, cancellationToken);
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch
        {
            await storage.DeleteAsync(storedFile.StorageKey, cancellationToken);
            throw;
        }
    }

    private static async Task<IResult> GetReleaseCoverImageAsync(
        Guid releaseId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        IReleaseCoverStorage storage,
        CancellationToken cancellationToken)
    {
        Release? release = await context.Releases.AsNoTracking().SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ReleaseId(releaseId),
            cancellationToken);
        CoverImage? coverImage = release is null ? null : TryGetCoverImage(release);
        if (coverImage is null)
        {
            return EndpointErrors.NotFound("release_cover.not_found", "Release cover image was not found");
        }

        Stream? stream = await storage.TryOpenReadAsync(coverImage.StorageKey, cancellationToken);
        return stream is null
            ? EndpointErrors.NotFound("release_cover.not_found", "Release cover image was not found")
            : Results.File(stream, coverImage.ContentType, enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteReleaseCoverImageAsync(
        Guid releaseId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        IReleaseCoverStorage storage,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "release-cover", releaseId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        Release? release = await context.Releases.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ReleaseId(releaseId),
            cancellationToken);
        CoverImage? coverImage = release is null ? null : TryGetCoverImage(release);
        if (release is null || coverImage is null)
        {
            return EndpointErrors.NotFound("release_cover.not_found", "Release cover image was not found");
        }

        release.UpdateSummary(release.Summary.WithMetadata(release.Summary.Metadata.WithoutCoverImage()));
        _ = await context.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(coverImage.StorageKey, cancellationToken);

        return Results.NoContent();
    }

    private static bool TryValidateCoverFile(
        IFormFile file,
        out CoverImageUploadFormat uploadFormat,
        out string errorCode,
        out string errorMessage)
    {
        uploadFormat = default;
        errorCode = string.Empty;
        errorMessage = string.Empty;

        if (file.Length <= 0)
        {
            errorCode = "release_cover.file_empty";
            errorMessage = "Release cover file must not be empty";
            return false;
        }

        if (file.Length > MaxCoverImageSizeBytes)
        {
            errorCode = "release_cover.file_too_large";
            errorMessage = "Release cover file must be 10 MB or smaller";
            return false;
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!TryGetUploadFormat(extension, out uploadFormat))
        {
            errorCode = "release_cover.format_invalid";
            errorMessage = "Release cover file format is not supported";
            return false;
        }

        if (!string.Equals(file.ContentType, uploadFormat.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "release_cover.content_type_invalid";
            errorMessage = "Release cover content type does not match the file extension";
            return false;
        }

        return true;
    }

    private static bool TryGetUploadFormat(string extension, out CoverImageUploadFormat uploadFormat)
    {
        uploadFormat = extension switch
        {
            ".png" => new CoverImageUploadFormat(".png", "image/png"),
            ".jpg" or ".jpeg" => new CoverImageUploadFormat(extension, "image/jpeg"),
            ".webp" => new CoverImageUploadFormat(".webp", "image/webp"),
            _ => default
        };

        return uploadFormat.ContentType is not null;
    }

    private static bool HasValidSignature(byte[] content, CoverImageUploadFormat uploadFormat)
    {
        return uploadFormat.Extension switch
        {
            ".png" => content is [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..],
            ".jpg" or ".jpeg" => content is [0xFF, 0xD8, 0xFF, ..],
            ".webp" => content is [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..],
            _ => false
        };
    }

    private static CoverImage? TryGetCoverImage(Release release)
    {
        return release.Summary.Metadata.CoverImage is PresentOptionalValue<CoverImage> { Value: CoverImage coverImage }
            ? coverImage
            : null;
    }

    private readonly record struct CoverImageUploadFormat(string Extension, string ContentType);
}
