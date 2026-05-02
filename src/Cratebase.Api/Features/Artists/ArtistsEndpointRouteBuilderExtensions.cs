using Cratebase.Api.Http;
using Cratebase.Application.Catalog.Artists;
using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Features.Artists;

public static class ArtistsEndpointRouteBuilderExtensions
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;

    public static IEndpointRouteBuilder MapArtistsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/artists")
            .WithTags("Artists");

        _ = group.MapPost("/", CreateArtistAsync)
            .WithName("CreateArtist");
        _ = group.MapGet("/{artistId:guid}", GetArtistAsync)
            .WithName("GetArtist");
        _ = group.MapGet("/", ListArtistsAsync)
            .WithName("ListArtists");
        _ = group.MapPut("/{artistId:guid}", UpdateArtistAsync)
            .WithName("UpdateArtist");
        _ = group.MapDelete("/{artistId:guid}", DeleteArtistAsync)
            .WithName("DeleteArtist");

        return endpoints;
    }

    private static async Task<IResult> CreateArtistAsync(
        CreateArtistRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            string normalizedType = request.Type?.Trim() ?? string.Empty;
            Artist? artist = CreateArtist(normalizedType, request.Name);
            if (artist is null)
            {
                return EndpointErrors.BadRequest("artist.type_invalid", "Artist type is invalid");
            }

            IRepository<Artist, ArtistId> artists = unitOfWork.GetRepository<Artist, ArtistId>();
            artists.Add(artist);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            ArtistResponse response = ToResponse(artist);
            return Results.Created($"/api/artists/{response.Id}", response);
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetArtistAsync(
        Guid artistId,
        IArtistQueries artistQueries,
        CancellationToken cancellationToken)
    {
        ArtistReadModel? artist = await artistQueries.TryGetAsync(new ArtistId(artistId), cancellationToken);

        return artist is null
            ? EndpointErrors.NotFound("artist.not_found", "Artist was not found")
            : Results.Ok(ToResponse(artist));
    }

    private static async Task<IResult> ListArtistsAsync(
        string? search,
        string? type,
        int? limit,
        int? offset,
        IArtistQueries artistQueries,
        CancellationToken cancellationToken)
    {
        string normalizedType = string.IsNullOrWhiteSpace(type) ? string.Empty : type.Trim();
        if (!string.IsNullOrEmpty(normalizedType) && !IsKnownArtistType(normalizedType))
        {
            return EndpointErrors.BadRequest("artist.type_invalid", "Artist type is invalid");
        }

        int normalizedLimit = limit ?? DefaultLimit;
        int normalizedOffset = offset ?? 0;
        if (normalizedLimit < 1 || normalizedLimit > MaximumLimit || normalizedOffset < 0)
        {
            return EndpointErrors.BadRequest("pagination.invalid", "Pagination values are invalid");
        }

        ArtistListResult result = await artistQueries.ListAsync(
            new ArtistListQuery(search?.Trim() ?? string.Empty, normalizedType, normalizedLimit, normalizedOffset),
            cancellationToken);

        ArtistListResponse response = new(
            [.. result.Items.Select(ToResponse)],
            result.Limit,
            result.Offset,
            result.Total);

        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateArtistAsync(
        Guid artistId,
        UpdateArtistRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<Artist, ArtistId> artists = unitOfWork.GetRepository<Artist, ArtistId>();
        Artist? artist = await artists.TryFindAsync(new ArtistId(artistId), cancellationToken);
        if (artist is null)
        {
            return EndpointErrors.NotFound("artist.not_found", "Artist was not found");
        }

        try
        {
            artist.Rename(request.Name);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToResponse(artist));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteArtistAsync(
        Guid artistId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "artist", artistId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<Artist, ArtistId> artists = unitOfWork.GetRepository<Artist, ArtistId>();
        Artist? artist = await artists.TryFindAsync(new ArtistId(artistId), cancellationToken);
        if (artist is null)
        {
            return EndpointErrors.NotFound("artist.not_found", "Artist was not found");
        }

        try
        {
            artists.Delete(artist);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }
        catch (PersistenceConflictException exception) when (exception.Kind == PersistenceConflictKind.ForeignKeyViolation)
        {
            return EndpointErrors.Conflict("artist.delete_conflict", "Artist has dependent data");
        }
    }

    private static Artist? CreateArtist(string type, string name)
    {
        var artistId = ArtistId.New();

        return type switch
        {
            "person" => Person.Create(artistId, name),
            "group" => Group.Create(artistId, name),
            _ => null
        };
    }

    private static ArtistResponse ToResponse(Artist artist)
    {
        return artist switch
        {
            Person => new ArtistResponse(artist.Id.Value, "person", artist.Name),
            Group => new ArtistResponse(artist.Id.Value, "group", artist.Name),
            _ => throw new InvalidOperationException("Artist type is not supported")
        };
    }

    private static ArtistResponse ToResponse(ArtistReadModel artist)
    {
        return new ArtistResponse(artist.Id.Value, artist.Type, artist.Name);
    }

    private static bool IsKnownArtistType(string type)
    {
        return type is "person" or "group";
    }

}
