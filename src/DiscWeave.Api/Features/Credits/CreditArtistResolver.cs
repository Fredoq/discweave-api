using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Credits;

internal static class CreditArtistResolver
{
    public static async Task<Artist> ResolveAsync(
        Guid? artistId,
        string? name,
        DiscWeaveDbContext context,
        CollectionId collectionId,
        CreditArtistResolverErrors errors,
        CancellationToken cancellationToken)
    {
        if (artistId is { } id)
        {
            Artist? existing = await context.Artists.SingleOrDefaultAsync(
                artist => artist.CollectionId == collectionId && artist.Id == new ArtistId(id),
                cancellationToken);

            return existing ?? throw new DomainException(errors.ConflictCode, errors.ConflictMessage);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(errors.NameRequiredCode, errors.NameRequiredMessage);
        }

        string normalizedName = name.Trim();
        Artist? existingByName = context.ChangeTracker
            .Entries<Artist>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .FirstOrDefault(artist => artist.CollectionId == collectionId && artist.Name == normalizedName)
            ?? await context.Artists.FirstOrDefaultAsync(
                artist => artist.CollectionId == collectionId && artist.Name == normalizedName,
                cancellationToken);
        if (existingByName is not null)
        {
            return existingByName;
        }

        Artist created = Person.Create(collectionId, ArtistId.New(), normalizedName);
        _ = context.Artists.Add(created);

        return created;
    }
}
