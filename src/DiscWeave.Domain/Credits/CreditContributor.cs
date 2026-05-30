using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Credits;

public sealed record CreditContributor
{
    private CreditContributor(ArtistId artistId, string name)
    {
        ArtistId = artistId;
        Name = Guard.RequiredText(name, nameof(name), "credit_contributor.name_required");
    }

    public ArtistId ArtistId { get; }

    public string Name { get; }

    public static CreditContributor FromArtist(Artist artist)
    {
        ArgumentNullException.ThrowIfNull(artist);

        return new CreditContributor(artist.Id, artist.Name);
    }

    internal static CreditContributor Create(ArtistId artistId, string name)
    {
        return new CreditContributor(artistId, name);
    }
}
