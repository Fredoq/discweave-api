using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Application.Security;
using Cratebase.Api.Http;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private const string RestoreConfirmationHeader = "X-Cratebase-Confirm-Restore";
    private const string RestoreConfirmationValue = "restore-empty-collection";

    private static async Task<IResult> RestoreJsonAsync(
        ExportSnapshotResponse snapshot,
        HttpRequest request,
        CratebaseDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue(RestoreConfirmationHeader, out Microsoft.Extensions.Primitives.StringValues confirmation) ||
            !confirmation.Contains(RestoreConfirmationValue, StringComparer.Ordinal))
        {
            return EndpointErrors.BadRequest("export_restore.confirmation_required", "Restore confirmation is required");
        }

        if (snapshot.FormatVersion != FormatVersion)
        {
            return EndpointErrors.BadRequest("export_restore.format_version_unsupported", "Export format version is not supported");
        }

        CollectionId collectionId = currentCollection.CollectionId;
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        if (!await IsCollectionEmptyForRestoreAsync(context, collectionId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return EndpointErrors.Conflict("export_restore.collection_not_empty", "JSON restore requires an empty collection");
        }

        try
        {
            await ReplaceCollectionDefaultsAsync(context, collectionId, cancellationToken);
            RestoreDictionaries(context, collectionId, snapshot.Dictionaries);
            RestoreRatingCriteria(context, collectionId, snapshot.RatingCriteria);
            RestoreImportPatterns(context, collectionId, snapshot.ImportPatterns);
            RestoreNamingProfiles(context, collectionId, snapshot.NamingProfiles);
            RestoreTagRoleMappings(context, collectionId, snapshot.TagRoleMappings);
            ArtistLookup artists = RestoreArtists(context, collectionId, snapshot.Artists);
            RestoreLabels(context, collectionId, snapshot.Labels);
            RestoreTracks(context, collectionId, snapshot.Tracks);
            RestoreReleases(context, collectionId, snapshot.Releases);
            RestoreReleaseNamingOverrides(context, collectionId, snapshot.ReleaseNamingOverrides);
            RestoreOwnedItems(context, collectionId, snapshot.OwnedItems);
            RestoreCredits(context, collectionId, snapshot.Credits, artists);
            RestoreArtistRelations(context, collectionId, snapshot.ArtistRelations);
            RestoreTrackRelations(context, collectionId, snapshot.TrackRelations);
            RestorePlaylists(context, collectionId, snapshot.Playlists);
            RestoreRatings(context, collectionId, snapshot.Ratings);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(ToRestoreResponse(snapshot));
        }
        catch (Exception exception) when (IsSnapshotInvalid(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return EndpointErrors.BadRequest("export_restore.snapshot_invalid", "Export snapshot is invalid");
        }
    }

    private static async Task<bool> IsCollectionEmptyForRestoreAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        return !await context.Artists.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.Labels.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.Releases.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.Tracks.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.OwnedItems.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.Playlists.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.Credits.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.ArtistRelations.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.TrackRelations.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.ReleaseImportSessions.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.ReleaseImportDrafts.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.ReleaseImportDraftTracks.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken) &&
            !await context.RatingValues.AnyAsync(entity => entity.CollectionId == collectionId, cancellationToken);
    }

    private static async Task ReplaceCollectionDefaultsAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        _ = await context.CollectionDictionaryEntries
            .Where(entity => entity.CollectionId == collectionId)
            .ExecuteDeleteAsync(cancellationToken);
        _ = await context.ImportPatterns
            .Where(entity => entity.CollectionId == collectionId)
            .ExecuteDeleteAsync(cancellationToken);
        _ = await context.RatingCriteria
            .Where(entity => entity.CollectionId == collectionId)
            .ExecuteDeleteAsync(cancellationToken);
        _ = await context.NamingProfiles
            .Where(entity => entity.CollectionId == collectionId)
            .ExecuteDeleteAsync(cancellationToken);
        _ = await context.TagRoleMappings
            .Where(entity => entity.CollectionId == collectionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static ExportRestoreResponse ToRestoreResponse(ExportSnapshotResponse snapshot)
    {
        return new ExportRestoreResponse(
            true,
            snapshot.FormatVersion,
            snapshot.Artists.Count,
            snapshot.Labels.Count,
            snapshot.Releases.Count,
            snapshot.Tracks.Count,
            snapshot.OwnedItems.Count,
            snapshot.Playlists.Count,
            snapshot.Credits.Count,
            snapshot.ArtistRelations.Count,
            snapshot.TrackRelations.Count,
            snapshot.Dictionaries.Count,
            snapshot.ImportPatterns.Count,
            snapshot.NamingProfiles.Count,
            snapshot.TagRoleMappings.Count,
            snapshot.ReleaseNamingOverrides.Count,
            snapshot.RatingCriteria.Count,
            snapshot.Ratings.Count);
    }

    private static bool IsSnapshotInvalid(Exception exception)
    {
        return exception is DomainException or ReferencedResourceMissingException or ResourceConflictException;
    }
}
