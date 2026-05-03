using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;

namespace Cratebase.Infrastructure.Persistence.Queries;

internal static class SearchResultCodes
{
    public static string ArtistType(Artist artist)
    {
        return artist switch
        {
            Person => "person",
            Group => "group",
            _ => "artist"
        };
    }

    public static string ToCreditRoleCode(CreditRole role)
    {
        return role switch
        {
            CreditRole.MainArtist => "mainArtist",
            CreditRole.FeaturedArtist => "featuredArtist",
            CreditRole.Remixer => "remixer",
            CreditRole.Producer => "producer",
            CreditRole.Composer => "composer",
            CreditRole.Performer => "performer",
            CreditRole.Engineer => "engineer",
            _ => throw new InvalidOperationException("Credit role is not supported")
        };
    }

    public static string ToOwnershipStatusCode(OwnershipStatus status)
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

    public static string ToMediumCode(IMedium medium)
    {
        return medium switch
        {
            DigitalFile => "digital",
            VinylRecord => "vinyl",
            CompactDisc => "cd",
            CassetteTape => "cassette",
            OtherMedium => "other",
            _ => throw new InvalidOperationException("Medium type is not supported")
        };
    }

    public static int TypeRank(string type)
    {
        return type switch
        {
            "artist" => 1,
            "release" => 2,
            "track" => 3,
            "label" => 4,
            "ownedItem" => 5,
            _ => 6
        };
    }
}
