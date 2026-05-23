using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Playlists;

public static class PlaylistsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPlaylistsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/playlists")
            .WithTags("Playlists")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);
        _ = group.MapPost("/", CreatePlaylistAsync).WithName("CreatePlaylist");
        _ = group.MapGet("/{playlistId:guid}", GetPlaylistAsync).WithName("GetPlaylist");
        _ = group.MapGet("", ListPlaylistsAsync).WithName("ListPlaylists");
        _ = group.MapPut("/{playlistId:guid}", UpdatePlaylistAsync).WithName("UpdatePlaylist");
        _ = group.MapDelete("/{playlistId:guid}", DeletePlaylistAsync).WithName("DeletePlaylist");

        return endpoints;
    }

    private static async Task<IResult> CreatePlaylistAsync(
        PlaylistRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            PlaylistType type = PlaylistMapper.ParseType(request.Type);
            var playlist = Playlist.Create(currentCollection.CollectionId, PlaylistId.New(), request.Name, type);
            playlist.UpdateDescription(OptionalDescription(request.Description));
            await ApplyPayloadAsync(playlist, request, context, currentCollection.CollectionId, cancellationToken);
            _ = context.Playlists.Add(playlist);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/playlists/{playlist.Id}",
                await PlaylistMapper.ToResponseAsync(playlist, context, cancellationToken));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetPlaylistAsync(
        Guid playlistId,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Playlist? playlist = await FindPlaylistAsync(context, currentCollection.CollectionId, new PlaylistId(playlistId), cancellationToken);

        return playlist is null
            ? EndpointErrors.NotFound("playlist.not_found", "Playlist was not found")
            : Results.Ok(await PlaylistMapper.ToResponseAsync(playlist, context, cancellationToken));
    }

    private static async Task<IResult> ListPlaylistsAsync(
        string? search,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<Playlist> query = context.Playlists.Where(playlist => playlist.CollectionId == currentCollection.CollectionId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            query = query.Where(playlist => EF.Functions.ILike(playlist.Name, pattern));
        }

        int total = await query.CountAsync(cancellationToken);
        Playlist[] page = await query
            .OrderBy(playlist => playlist.Name)
            .ThenBy(playlist => playlist.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .ToArrayAsync(cancellationToken);
        var responses = new List<PlaylistResponse>(page.Length);
        foreach (Playlist playlist in page)
        {
            responses.Add(await PlaylistMapper.ToResponseAsync(playlist, context, cancellationToken));
        }

        return Results.Ok(new ListResponse<PlaylistResponse>(responses, normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdatePlaylistAsync(
        Guid playlistId,
        PlaylistRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        Playlist? playlist = await FindPlaylistAsync(context, currentCollection.CollectionId, new PlaylistId(playlistId), cancellationToken);
        if (playlist is null)
        {
            return EndpointErrors.NotFound("playlist.not_found", "Playlist was not found");
        }

        try
        {
            playlist.Rename(request.Name);
            playlist.UpdateDescription(OptionalDescription(request.Description));
            await ApplyPayloadAsync(playlist, request, context, currentCollection.CollectionId, cancellationToken);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(await PlaylistMapper.ToResponseAsync(playlist, context, cancellationToken));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeletePlaylistAsync(
        Guid playlistId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "playlist", playlistId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        Playlist? playlist = await FindPlaylistAsync(context, currentCollection.CollectionId, new PlaylistId(playlistId), cancellationToken);
        if (playlist is null)
        {
            return EndpointErrors.NotFound("playlist.not_found", "Playlist was not found");
        }

        _ = context.Playlists.Remove(playlist);
        _ = await context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task ApplyPayloadAsync(
        Playlist playlist,
        PlaylistRequest request,
        CratebaseDbContext context,
        CollectionId collectionId,
        CancellationToken cancellationToken)
    {
        PlaylistType type = PlaylistMapper.ParseType(request.Type);
        if (type == PlaylistType.Manual)
        {
            PlaylistEntry[] entries = await PlaylistMapper.ToEntriesAsync(request.Entries, context, collectionId, cancellationToken);
            playlist.ReplaceManualEntries(entries);
            return;
        }

        playlist.ReplaceSmartRules(PlaylistMapper.ToRules(request.Rules));
    }

    private static async Task<Playlist?> FindPlaylistAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        PlaylistId playlistId,
        CancellationToken cancellationToken)
    {
        return await context.Playlists.SingleOrDefaultAsync(
            playlist => playlist.CollectionId == collectionId && playlist.Id == playlistId,
            cancellationToken);
    }

    private static IOptionalValue<string> OptionalDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? Optional.Missing<string>()
            : Optional.From(description.Trim());
    }
}
