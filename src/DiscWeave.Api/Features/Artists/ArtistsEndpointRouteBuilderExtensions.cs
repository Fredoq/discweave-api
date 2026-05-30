using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.Catalog.Artists;
using DiscWeave.Application.Errors;
using DiscWeave.Application.Persistence;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.Relations;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Artists;

public static class ArtistsEndpointRouteBuilderExtensions
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;

    public static IEndpointRouteBuilder MapArtistsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/artists")
            .WithTags("Artists")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);

        _ = group.MapPost("/", CreateArtistAsync)
            .WithName("CreateArtist");
        _ = group.MapGet("/{artistId:guid}", GetArtistAsync)
            .WithName("GetArtist");
        _ = group.MapGet("", ListArtistsAsync)
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
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            string normalizedType = request.Type?.Trim() ?? string.Empty;
            Artist artist = CreateArtist(currentCollection.CollectionId, normalizedType, request.Name);

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
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "artist", artistId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        Artist? artist = await context.Artists.SingleOrDefaultAsync(
            entity => entity.CollectionId == currentCollection.CollectionId && entity.Id == new ArtistId(artistId),
            cancellationToken);
        if (artist is null)
        {
            return EndpointErrors.NotFound("artist.not_found", "Artist was not found");
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            Credit[] credits = await context.Credits
                .Where(credit =>
                    credit.CollectionId == currentCollection.CollectionId &&
                    EF.Property<ArtistId>(credit, "_contributorArtistId") == artist.Id)
                .ToArrayAsync(cancellationToken);
            ArtistRelation[] relations = await context.ArtistRelations
                .Where(relation =>
                    relation.CollectionId == currentCollection.CollectionId &&
                    (relation.SourceArtistId == artist.Id || relation.TargetArtistId == artist.Id))
                .ToArrayAsync(cancellationToken);

            context.Credits.RemoveRange(credits);
            context.ArtistRelations.RemoveRange(relations);
            _ = context.Artists.Remove(artist);

            _ = await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("artist.delete_conflict", "Artist has dependent data");
        }
    }

    private static Artist CreateArtist(CollectionId collectionId, string type, string name)
    {
        var artistId = ArtistId.New();

        return type switch
        {
            "person" => Person.Create(collectionId, artistId, name),
            "group" => Group.Create(collectionId, artistId, name),
            _ => throw new DomainException("artist.type_invalid", "Artist type is invalid")
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
