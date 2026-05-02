using Cratebase.Api.Http;
using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Npgsql;

namespace Cratebase.Api.Features.Core;

public static class CoreCatalogEndpointRouteBuilderExtensions
{
    private const int DefaultLimit = 50;
    private const string DeleteConfirmationRequiredCode = "delete.confirmation_required";
    private const string DeleteConfirmationRequiredMessage = "Delete confirmation is required";
    private const int MaximumLimit = 100;
    private const string OtherTypeCode = "other";
    private const string DeleteConfirmationHeaderName = "X-Cratebase-Confirm-Delete";

    public static IEndpointRouteBuilder MapCoreCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        MapLabels(endpoints.MapGroup("/api/labels").WithTags("Labels"));
        MapTracks(endpoints.MapGroup("/api/tracks").WithTags("Tracks"));
        MapReleases(endpoints.MapGroup("/api/releases").WithTags("Releases"));
        MapOwnedItems(endpoints.MapGroup("/api/owned-items").WithTags("Owned Items"));

        return endpoints;
    }

    private static void MapLabels(RouteGroupBuilder group)
    {
        _ = group.MapPost("/", CreateLabelAsync).WithName("CreateLabel");
        _ = group.MapGet("/{labelId:guid}", GetLabelAsync).WithName("GetLabel");
        _ = group.MapGet("/", ListLabelsAsync).WithName("ListLabels");
        _ = group.MapPut("/{labelId:guid}", UpdateLabelAsync).WithName("UpdateLabel");
        _ = group.MapDelete("/{labelId:guid}", DeleteLabelAsync).WithName("DeleteLabel");
    }

    private static void MapTracks(RouteGroupBuilder group)
    {
        _ = group.MapPost("/", CreateTrackAsync).WithName("CreateTrack");
        _ = group.MapGet("/{trackId:guid}", GetTrackAsync).WithName("GetTrack");
        _ = group.MapGet("/", ListTracksAsync).WithName("ListTracks");
        _ = group.MapPut("/{trackId:guid}", UpdateTrackAsync).WithName("UpdateTrack");
        _ = group.MapDelete("/{trackId:guid}", DeleteTrackAsync).WithName("DeleteTrack");
    }

    private static void MapReleases(RouteGroupBuilder group)
    {
        _ = group.MapPost("/", CreateReleaseAsync).WithName("CreateRelease");
        _ = group.MapGet("/{releaseId:guid}", GetReleaseAsync).WithName("GetRelease");
        _ = group.MapGet("/", ListReleasesAsync).WithName("ListReleases");
        _ = group.MapPut("/{releaseId:guid}", UpdateReleaseAsync).WithName("UpdateRelease");
        _ = group.MapDelete("/{releaseId:guid}", DeleteReleaseAsync).WithName("DeleteRelease");
    }

    private static void MapOwnedItems(RouteGroupBuilder group)
    {
        _ = group.MapPost("/", CreateOwnedItemAsync).WithName("CreateOwnedItem");
        _ = group.MapGet("/{ownedItemId:guid}", GetOwnedItemAsync).WithName("GetOwnedItem");
        _ = group.MapGet("/", ListOwnedItemsAsync).WithName("ListOwnedItems");
        _ = group.MapPut("/{ownedItemId:guid}", UpdateOwnedItemAsync).WithName("UpdateOwnedItem");
        _ = group.MapDelete("/{ownedItemId:guid}", DeleteOwnedItemAsync).WithName("DeleteOwnedItem");
    }

    private static async Task<IResult> CreateLabelAsync(
        NameRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            var label = Label.Create(LabelId.New(), request.Name);
            IRepository<Label, LabelId> labels = unitOfWork.GetRepository<Label, LabelId>();
            labels.Add(label);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/labels/{label.Id}", ToLabelResponse(label));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetLabelAsync(Guid labelId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        Label? label = await context.Labels.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new LabelId(labelId), cancellationToken);

        return label is null
            ? NotFound("label.not_found", "Label was not found")
            : Results.Ok(ToLabelResponse(label));
    }

    private static async Task<IResult> ListLabelsAsync(
        string? search,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizePagination(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult? error))
        {
            return error;
        }

        IQueryable<Label> labels = context.Labels.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            labels = labels.Where(label => EF.Functions.ILike(label.Name, pattern));
        }

        int total = await labels.CountAsync(cancellationToken);
        LabelResponse[] items = await labels
            .OrderBy(label => label.Name)
            .ThenBy(label => label.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .Select(label => new LabelResponse(label.Id.Value, label.Name))
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<LabelResponse>(items, normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateLabelAsync(
        Guid labelId,
        NameRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<Label, LabelId> labels = unitOfWork.GetRepository<Label, LabelId>();
        Label? label = await labels.TryFindAsync(new LabelId(labelId), cancellationToken);
        if (label is null)
        {
            return NotFound("label.not_found", "Label was not found");
        }

        try
        {
            label.Rename(request.Name);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToLabelResponse(label));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteLabelAsync(
        Guid labelId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!HasDeleteConfirmation(request, "label", labelId))
        {
            return BadRequest(DeleteConfirmationRequiredCode, DeleteConfirmationRequiredMessage);
        }

        IRepository<Label, LabelId> labels = unitOfWork.GetRepository<Label, LabelId>();
        Label? label = await labels.TryFindAsync(new LabelId(labelId), cancellationToken);
        if (label is null)
        {
            return NotFound("label.not_found", "Label was not found");
        }

        try
        {
            labels.Delete(label);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return Results.Conflict(new ErrorResponse("label.delete_conflict", "Label has dependent data"));
        }
    }

    private static async Task<IResult> CreateTrackAsync(
        TrackRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            Track track = ApplyTrackRequest(Track.Create(TrackId.New(), request.Title), request);
            IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
            tracks.Add(track);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/tracks/{track.Id}", ToTrackResponse(track));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetTrackAsync(Guid trackId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        Track? track = await context.Tracks.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new TrackId(trackId), cancellationToken);

        return track is null
            ? NotFound("track.not_found", "Track was not found")
            : Results.Ok(ToTrackResponse(track));
    }

    private static async Task<IResult> ListTracksAsync(
        string? search,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizePagination(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult? error))
        {
            return error;
        }

        IQueryable<Track> tracks = context.Tracks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            tracks = tracks.Where(track => EF.Functions.ILike(track.Title, pattern));
        }

        int total = await tracks.CountAsync(cancellationToken);
        Track[] items = await tracks
            .OrderBy(track => track.Title)
            .ThenBy(track => track.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<TrackResponse>([.. items.Select(ToTrackResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateTrackAsync(
        Guid trackId,
        TrackRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
        Track? track = await tracks.TryFindAsync(new TrackId(trackId), cancellationToken);
        if (track is null)
        {
            return NotFound("track.not_found", "Track was not found");
        }

        try
        {
            _ = ApplyTrackRequest(track, request);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToTrackResponse(track));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteTrackAsync(
        Guid trackId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!HasDeleteConfirmation(request, "track", trackId))
        {
            return BadRequest(DeleteConfirmationRequiredCode, DeleteConfirmationRequiredMessage);
        }

        IRepository<Track, TrackId> tracks = unitOfWork.GetRepository<Track, TrackId>();
        Track? track = await tracks.TryFindAsync(new TrackId(trackId), cancellationToken);
        if (track is null)
        {
            return NotFound("track.not_found", "Track was not found");
        }

        try
        {
            tracks.Delete(track);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return Results.Conflict(new ErrorResponse("track.delete_conflict", "Track has dependent data"));
        }
    }

    private static async Task<IResult> CreateReleaseAsync(
        ReleaseRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            Release release = ApplyReleaseRequest(Release.Create(ReleaseId.New(), request.Title), request);
            IRepository<Release, ReleaseId> releases = unitOfWork.GetRepository<Release, ReleaseId>();
            releases.Add(release);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/releases/{release.Id}", ToReleaseResponse(release));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetReleaseAsync(Guid releaseId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        Release? release = await context.Releases.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new ReleaseId(releaseId), cancellationToken);

        return release is null
            ? NotFound("release.not_found", "Release was not found")
            : Results.Ok(ToReleaseResponse(release));
    }

    private static async Task<IResult> ListReleasesAsync(
        string? search,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizePagination(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult? error))
        {
            return error;
        }

        IQueryable<Release> releases = context.Releases.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            releases = releases.Where(release => EF.Functions.ILike(release.Summary.Title, pattern));
        }

        int total = await releases.CountAsync(cancellationToken);
        Release[] items = await releases
            .OrderBy(release => release.Summary.Title)
            .ThenBy(release => release.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<ReleaseResponse>([.. items.Select(ToReleaseResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateReleaseAsync(
        Guid releaseId,
        ReleaseRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<Release, ReleaseId> releases = unitOfWork.GetRepository<Release, ReleaseId>();
        Release? release = await releases.TryFindAsync(new ReleaseId(releaseId), cancellationToken);
        if (release is null)
        {
            return NotFound("release.not_found", "Release was not found");
        }

        try
        {
            _ = ApplyReleaseRequest(release, request);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToReleaseResponse(release));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteReleaseAsync(
        Guid releaseId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!HasDeleteConfirmation(request, "release", releaseId))
        {
            return BadRequest(DeleteConfirmationRequiredCode, DeleteConfirmationRequiredMessage);
        }

        IRepository<Release, ReleaseId> releases = unitOfWork.GetRepository<Release, ReleaseId>();
        Release? release = await releases.TryFindAsync(new ReleaseId(releaseId), cancellationToken);
        if (release is null)
        {
            return NotFound("release.not_found", "Release was not found");
        }

        try
        {
            releases.Delete(release);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return Results.Conflict(new ErrorResponse("release.delete_conflict", "Release has dependent data"));
        }
    }

    private static async Task<IResult> CreateOwnedItemAsync(
        CreateOwnedItemRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request.Medium);
            var item = OwnedItem.Create(
                OwnedItemId.New(),
                CreateOwnedItemTarget(request.TargetType, request.TargetId),
                ParseOwnershipStatus(request.Status),
                CreateMedium(request.Medium));
            item.UpdateHolding(CreateHolding(item.Holding.Medium, request.Status, request.Condition, request.StorageLocation));
            IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
            items.Add(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/owned-items/{item.Id}", ToOwnedItemResponse(item));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return BadRequest("owned_item.request_invalid", "Owned item request is invalid");
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return Results.Conflict(new ErrorResponse("owned_item.target_conflict", "Owned item target does not exist"));
        }
    }

    private static async Task<IResult> GetOwnedItemAsync(Guid ownedItemId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        OwnedItem? item = await context.OwnedItems.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new OwnedItemId(ownedItemId), cancellationToken);

        return item is null
            ? NotFound("owned_item.not_found", "Owned item was not found")
            : Results.Ok(ToOwnedItemResponse(item));
    }

    private static async Task<IResult> ListOwnedItemsAsync(
        string? status,
        string? medium,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizePagination(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult? error))
        {
            return error;
        }

        IQueryable<OwnedItem> items = context.OwnedItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!TryParseOwnershipStatus(status, out OwnershipStatus normalizedStatus))
            {
                return BadRequest("owned_item.status_invalid", "Owned item status is invalid");
            }

            items = items.Where(item => EF.Property<OwnershipStatus>(item, "_status") == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(medium))
        {
            string normalizedMedium = medium.Trim();
            items = items.Where(item => EF.Property<string>(item, "_mediumType") == normalizedMedium);
        }

        int total = await items.CountAsync(cancellationToken);
        OwnedItem[] page = await items
            .OrderBy(item => item.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<OwnedItemResponse>([.. page.Select(ToOwnedItemResponse)], normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateOwnedItemAsync(
        Guid ownedItemId,
        UpdateOwnedItemRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
        OwnedItem? item = await items.TryFindAsync(new OwnedItemId(ownedItemId), cancellationToken);
        if (item is null)
        {
            return NotFound("owned_item.not_found", "Owned item was not found");
        }

        try
        {
            item.UpdateHolding(CreateHolding(item.Holding.Medium, request.Status, request.Condition, request.StorageLocation));
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToOwnedItemResponse(item));
        }
        catch (DomainException exception)
        {
            return BadRequest(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return BadRequest("owned_item.request_invalid", "Owned item request is invalid");
        }
    }

    private static async Task<IResult> DeleteOwnedItemAsync(
        Guid ownedItemId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!HasDeleteConfirmation(request, "owned-item", ownedItemId))
        {
            return BadRequest(DeleteConfirmationRequiredCode, DeleteConfirmationRequiredMessage);
        }

        IRepository<OwnedItem, OwnedItemId> items = unitOfWork.GetRepository<OwnedItem, OwnedItemId>();
        OwnedItem? item = await items.TryFindAsync(new OwnedItemId(ownedItemId), cancellationToken);
        if (item is null)
        {
            return NotFound("owned_item.not_found", "Owned item was not found");
        }

        items.Delete(item);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static Track ApplyTrackRequest(Track track, TrackRequest request)
    {
        track.Rename(request.Title);
        TrackDetails details = TrackDetails.Empty;
        if (request.DurationSeconds is { } durationSeconds)
        {
            details = details.WithDuration(TimeSpan.FromSeconds(durationSeconds));
        }

        track.UpdateDetails(details);
        track.UpdateCataloging(CreateCataloging(request.Genres, request.Tags));

        return track;
    }

    private static Release ApplyReleaseRequest(Release release, ReleaseRequest request)
    {
        ReleaseMetadata metadata = ReleaseMetadata.Empty.WithType(ParseReleaseType(request.Type ?? string.Empty));
        if (request.LabelId is { } labelId)
        {
            metadata = metadata.WithLabel(new LabelId(labelId));
        }

        if (request.Year is { } year)
        {
            metadata = metadata.WithReleaseYear(year);
        }

        release.UpdateSummary(ReleaseSummary.Create(request.Title).WithMetadata(metadata));
        release.UpdateCataloging(CreateCataloging(request.Genres, request.Tags));

        return release;
    }

    private static Cataloging CreateCataloging(IReadOnlyList<string>? genres, IReadOnlyList<string>? tags)
    {
        Cataloging cataloging = Cataloging.Empty;

        foreach (string genre in genres ?? [])
        {
            cataloging = cataloging.WithGenre(Genre.FromName(genre));
        }

        foreach (string tag in tags ?? [])
        {
            cataloging = cataloging.WithTag(Tag.FromName(tag));
        }

        return cataloging;
    }

    private static OwnedItemTarget CreateOwnedItemTarget(string? targetType, Guid targetId)
    {
        return Required(targetType, "owned_item.target_type_required").Trim() switch
        {
            "release" => OwnedItemTarget.ForRelease(new ReleaseId(targetId)),
            "track" => OwnedItemTarget.ForTrack(new TrackId(targetId)),
            _ => throw new DomainException("owned_item.target_type_invalid", "Owned item target type is invalid")
        };
    }

    private static OwnedItemHolding CreateHolding(IMedium medium, string status, string? condition, string? storageLocation)
    {
        var holding = OwnedItemHolding.Create(ParseOwnershipStatus(status), medium);
        OwnedItemDetails details = OwnedItemDetails.Empty;

        if (!string.IsNullOrWhiteSpace(condition))
        {
            details = details.WithCondition(ParseItemCondition(condition));
        }

        if (!string.IsNullOrWhiteSpace(storageLocation))
        {
            details = details.WithStorageLocation(StorageLocation.FromName(storageLocation));
        }

        return holding.WithDetails(details);
    }

    private static IMedium CreateMedium(MediumRequest request)
    {
        return Required(request.Type, "medium.type_required").Trim() switch
        {
            "digital" => DigitalFile.Create(FilePath.FromAbsolutePath(Required(request.Path, "medium.path_required")), ParseAudioFileFormat(Required(request.Format, "medium.format_required"))),
            "vinyl" => VinylRecord.Create(Required(request.Description, "medium.description_required")),
            "cd" => CompactDisc.Create(request.DiscCount ?? 1),
            "cassette" => CassetteTape.Create(Required(request.Description, "medium.description_required")),
            OtherTypeCode => OtherMedium.Create(Required(request.Description, "medium.description_required")),
            _ => throw new DomainException("medium.type_invalid", "Medium type is invalid")
        };
    }

    private static LabelResponse ToLabelResponse(Label label)
    {
        return new LabelResponse(label.Id.Value, label.Name);
    }

    private static TrackResponse ToTrackResponse(Track track)
    {
        int? durationSeconds = track.Details.Duration.HasValue
            ? track.Details.Duration.Match(value => (int)value.TotalSeconds, () => 0)
            : null;

        return new TrackResponse(
            track.Id.Value,
            track.Title,
            durationSeconds,
            [.. track.Cataloging.Genres.Select(genre => genre.Name)],
            [.. track.Cataloging.Tags.Select(tag => tag.Name)]);
    }

    private static ReleaseResponse ToReleaseResponse(Release release)
    {
        ReleaseMetadata metadata = release.Summary.Metadata;

        return new ReleaseResponse(
            release.Id.Value,
            release.Summary.Title,
            ToReleaseTypeCode(metadata.Type),
            metadata.LabelId.HasValue ? metadata.LabelId.Match(value => value.Value, () => Guid.Empty) : null,
            metadata.Year.HasValue ? metadata.Year.Match(value => value, () => 0) : null,
            [.. release.Cataloging.Genres.Select(genre => genre.Name)],
            [.. release.Cataloging.Tags.Select(tag => tag.Name)]);
    }

    private static OwnedItemResponse ToOwnedItemResponse(OwnedItem item)
    {
        OwnedItemHolding holding = item.Holding;
        OwnedItemTarget target = item.Target;

        return new OwnedItemResponse(
            item.Id.Value,
            target is ReleaseOwnedItemTarget ? "release" : "track",
            target is ReleaseOwnedItemTarget release ? release.ReleaseId.Value : ((TrackOwnedItemTarget)target).TrackId.Value,
            ToOwnershipStatusCode(holding.Status),
            ToMediumResponse(holding.Medium),
            holding.Details.Condition.HasValue ? holding.Details.Condition.Match(ToItemConditionCode, () => string.Empty) : null,
            holding.Details.StorageLocation.HasValue ? holding.Details.StorageLocation.Match(location => location.Name, () => string.Empty) : null);
    }

    private static MediumResponse ToMediumResponse(IMedium medium)
    {
        return medium switch
        {
            DigitalFile digitalFile => new MediumResponse("digital", digitalFile.Description, digitalFile.Path.Value, ToAudioFileFormatCode(digitalFile.Format), null),
            VinylRecord vinylRecord => new MediumResponse("vinyl", vinylRecord.FormatDescription, null, null, null),
            CompactDisc compactDisc => new MediumResponse("cd", compactDisc.Description, null, null, compactDisc.DiscCount),
            CassetteTape cassetteTape => new MediumResponse("cassette", cassetteTape.TapeType, null, null, null),
            OtherMedium otherMedium => new MediumResponse(OtherTypeCode, otherMedium.Name, null, null, null),
            _ => throw new InvalidOperationException("Medium type is not supported")
        };
    }

    private static ReleaseType ParseReleaseType(string type)
    {
        return (type ?? string.Empty).Trim() switch
        {
            "" => ReleaseType.Unknown,
            "unknown" => ReleaseType.Unknown,
            "album" => ReleaseType.Album,
            "ep" => ReleaseType.Ep,
            "standalone" => ReleaseType.Standalone,
            "compilation" => ReleaseType.Compilation,
            "bootleg" => ReleaseType.Bootleg,
            "mixtape" => ReleaseType.Mixtape,
            "promo" => ReleaseType.Promo,
            OtherTypeCode => ReleaseType.Other,
            _ => throw new DomainException("release.type_invalid", "Release type is invalid")
        };
    }

    private static OwnershipStatus ParseOwnershipStatus(string status)
    {
        return Required(status, "owned_item.status_required").Trim() switch
        {
            "owned" => OwnershipStatus.Owned,
            "wanted" => OwnershipStatus.Wanted,
            "sold" => OwnershipStatus.Sold,
            "needsDigitization" => OwnershipStatus.NeedsDigitization,
            _ => throw new DomainException("owned_item.status_invalid", "Owned item status is invalid")
        };
    }

    private static ItemCondition ParseItemCondition(string condition)
    {
        return Required(condition, "owned_item.condition_required").Trim() switch
        {
            "mint" => ItemCondition.Mint,
            "nearMint" => ItemCondition.NearMint,
            "veryGoodPlus" => ItemCondition.VeryGoodPlus,
            "veryGood" => ItemCondition.VeryGood,
            "good" => ItemCondition.Good,
            "fair" => ItemCondition.Fair,
            "poor" => ItemCondition.Poor,
            _ => throw new DomainException("owned_item.condition_invalid", "Owned item condition is invalid")
        };
    }

    private static AudioFileFormat ParseAudioFileFormat(string format)
    {
        return Required(format, "medium.format_required").Trim() switch
        {
            "flac" => AudioFileFormat.Flac,
            "mp3" => AudioFileFormat.Mp3,
            "ogg" => AudioFileFormat.Ogg,
            "wav" => AudioFileFormat.Wav,
            "aiff" => AudioFileFormat.Aiff,
            "alac" => AudioFileFormat.Alac,
            _ => throw new DomainException("digital_file.format_invalid", "Digital file format is invalid")
        };
    }

    private static string ToReleaseTypeCode(ReleaseType type)
    {
        return type switch
        {
            ReleaseType.Unknown => "unknown",
            ReleaseType.Album => "album",
            ReleaseType.Ep => "ep",
            ReleaseType.Standalone => "standalone",
            ReleaseType.Compilation => "compilation",
            ReleaseType.Bootleg => "bootleg",
            ReleaseType.Mixtape => "mixtape",
            ReleaseType.Promo => "promo",
            ReleaseType.Other => OtherTypeCode,
            _ => throw new InvalidOperationException("Release type is not supported")
        };
    }

    private static string ToOwnershipStatusCode(OwnershipStatus status)
    {
        return status switch
        {
            OwnershipStatus.Owned => "owned",
            OwnershipStatus.Wanted => "wanted",
            OwnershipStatus.Sold => "sold",
            OwnershipStatus.NeedsDigitization => "needsDigitization",
            _ => throw new InvalidOperationException("Ownership status is not supported")
        };
    }

    private static string ToItemConditionCode(ItemCondition condition)
    {
        return condition switch
        {
            ItemCondition.Mint => "mint",
            ItemCondition.NearMint => "nearMint",
            ItemCondition.VeryGoodPlus => "veryGoodPlus",
            ItemCondition.VeryGood => "veryGood",
            ItemCondition.Good => "good",
            ItemCondition.Fair => "fair",
            ItemCondition.Poor => "poor",
            _ => throw new InvalidOperationException("Item condition is not supported")
        };
    }

    private static string ToAudioFileFormatCode(AudioFileFormat format)
    {
        return format switch
        {
            AudioFileFormat.Flac => "flac",
            AudioFileFormat.Mp3 => "mp3",
            AudioFileFormat.Ogg => "ogg",
            AudioFileFormat.Wav => "wav",
            AudioFileFormat.Aiff => "aiff",
            AudioFileFormat.Alac => "alac",
            _ => throw new InvalidOperationException("Audio file format is not supported")
        };
    }

    private static bool TryParseOwnershipStatus(string status, out OwnershipStatus ownershipStatus)
    {
        switch (status.Trim())
        {
            case "owned":
                ownershipStatus = OwnershipStatus.Owned;
                return true;
            case "wanted":
                ownershipStatus = OwnershipStatus.Wanted;
                return true;
            case "sold":
                ownershipStatus = OwnershipStatus.Sold;
                return true;
            case "needsDigitization":
                ownershipStatus = OwnershipStatus.NeedsDigitization;
                return true;
            default:
                ownershipStatus = default;
                return false;
        }
    }

    private static string Required(string? value, string code)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new DomainException(code, "Required value is missing")
            : value;
    }

    private static bool IsForeignKeyViolation(DbUpdateException exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is PostgresException postgresException &&
                (postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation ||
                    postgresException.MessageText.Contains("foreign key", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException;
        }

        return exception.ToString().Contains("foreign key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDeleteConfirmation(HttpRequest request, string resource, Guid resourceId)
    {
        string expectedConfirmation = $"{resource}:{resourceId}";

        return request.Headers.TryGetValue(DeleteConfirmationHeaderName, out StringValues confirmationValues) &&
            confirmationValues.Count == 1 &&
            string.Equals(confirmationValues[0], expectedConfirmation, StringComparison.Ordinal);
    }

    private static bool TryNormalizePagination(
        int? limit,
        int? offset,
        out int normalizedLimit,
        out int normalizedOffset,
        out IResult error)
    {
        normalizedLimit = limit ?? DefaultLimit;
        normalizedOffset = offset ?? 0;
        error = Results.Empty;

        if (normalizedLimit < 1 || normalizedLimit > MaximumLimit || normalizedOffset < 0)
        {
            error = BadRequest("pagination.invalid", "Pagination values are invalid");
            return false;
        }

        return true;
    }

    private static IResult BadRequest(string code, string message)
    {
        return Results.BadRequest(new ErrorResponse(code, message));
    }

    private static IResult NotFound(string code, string message)
    {
        return Results.NotFound(new ErrorResponse(code, message));
    }

    public sealed record NameRequest(string Name);

    public sealed record LabelResponse(Guid Id, string Name);

    public sealed record TrackRequest(string Title, int? DurationSeconds, IReadOnlyList<string>? Genres, IReadOnlyList<string>? Tags);

    public sealed record TrackResponse(Guid Id, string Title, int? DurationSeconds, IReadOnlyList<string> Genres, IReadOnlyList<string> Tags);

    public sealed record ReleaseRequest(string Title, string? Type, Guid? LabelId, int? Year, IReadOnlyList<string>? Genres, IReadOnlyList<string>? Tags);

    public sealed record ReleaseResponse(Guid Id, string Title, string Type, Guid? LabelId, int? Year, IReadOnlyList<string> Genres, IReadOnlyList<string> Tags);

    public sealed record CreateOwnedItemRequest(string TargetType, Guid TargetId, string Status, MediumRequest Medium, string? Condition, string? StorageLocation);

    public sealed record UpdateOwnedItemRequest(string Status, string? Condition, string? StorageLocation);

    public sealed record OwnedItemResponse(
        Guid Id,
        string TargetType,
        Guid TargetId,
        string Status,
        MediumResponse Medium,
        string? Condition,
        string? StorageLocation);

    public sealed record MediumRequest(string Type, string? Description, string? Path, string? Format, int? DiscCount);

    public sealed record MediumResponse(string Type, string Description, string? Path, string? Format, int? DiscCount);

    public sealed record ListResponse<T>(IReadOnlyList<T> Items, int Limit, int Offset, int Total);
}
