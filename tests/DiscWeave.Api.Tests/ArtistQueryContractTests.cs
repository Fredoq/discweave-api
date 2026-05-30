using DiscWeave.Application.Catalog.Artists;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Api.Tests;

public sealed class ArtistQueryContractTests
{
    [Fact]
    public void Artist_query_contracts_preserve_read_model_values()
    {
        var artistId = ArtistId.New();
        var query = new ArtistListQuery("sumner", "person", 25, 50);
        var artist = new ArtistReadModel(artistId, "person", "Bernard Sumner");

        var result = new ArtistListResult([artist], query.Limit, query.Offset, 1);

        Assert.Equal("sumner", query.Search);
        Assert.Equal("person", query.Type);
        Assert.Equal(25, result.Limit);
        Assert.Equal(50, result.Offset);
        Assert.Equal(1, result.Total);
        Assert.Same(artist, result.Items.Single());
        Assert.Equal(artistId, result.Items[0].Id);
        Assert.Equal("person", result.Items[0].Type);
        Assert.Equal("Bernard Sumner", result.Items[0].Name);
    }
}
