using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Tests;

public sealed class ArtistsEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public ArtistsEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Creating a person returns the created artist")]
    public async Task Creating_a_person_returns_the_created_artist()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artists",
            new CreateArtistRequest("person", "  Bernard Sumner  "));

        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Guid artistId = document.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, artistId);
        Assert.Equal($"/api/artists/{artistId}", response.Headers.Location?.OriginalString);
        Assert.Equal("person", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Bernard Sumner", document.RootElement.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "Creating a group returns the created artist")]
    public async Task Creating_a_group_returns_the_created_artist()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artists",
            new CreateArtistRequest("group", "New Order"));

        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("group", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("New Order", document.RootElement.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "Creating an artist with a blank name returns a validation error")]
    public async Task Creating_an_artist_with_a_blank_name_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artists",
            new CreateArtistRequest("person", " "));

        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("artist.name_required", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Creating an artist with an invalid type returns a validation error")]
    public async Task Creating_an_artist_with_an_invalid_type_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artists",
            new CreateArtistRequest("alias", "Electronic"));

        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("artist.type_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Getting an existing artist returns the artist")]
    public async Task Getting_an_existing_artist_returns_the_artist()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        ArtistId artistId = await host.SeedArtistAsync(Person.Create(ArtistId.New(), "Gillian Gilbert"));
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/artists/{artistId}");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(artistId.Value, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("person", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Gillian Gilbert", document.RootElement.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "Getting a missing artist returns not found")]
    public async Task Getting_a_missing_artist_returns_not_found()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"/api/artists/{Guid.CreateVersion7()}");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("artist.not_found", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Listing artists returns deterministic filtered pages")]
    public async Task Listing_artists_returns_deterministic_filtered_pages()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        _ = await host.SeedArtistAsync(Group.Create(ArtistId.New(), "New Order"));
        _ = await host.SeedArtistAsync(Person.Create(ArtistId.New(), "Bernard Sumner"));
        _ = await host.SeedArtistAsync(Person.Create(ArtistId.New(), "Gillian Gilbert"));
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/artists?search=gi&type=person&limit=1&offset=0");
        using JsonDocument document = await ReadJsonAsync(response);

        JsonElement root = document.RootElement;
        JsonElement firstItem = root.GetProperty("items")[0];
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, root.GetProperty("limit").GetInt32());
        Assert.Equal(0, root.GetProperty("offset").GetInt32());
        Assert.Equal(1, root.GetProperty("total").GetInt32());
        Assert.Equal("Gillian Gilbert", firstItem.GetProperty("name").GetString());
        Assert.Equal("person", firstItem.GetProperty("type").GetString());
    }

    [Fact(DisplayName = "Listing artists with invalid pagination returns a validation error")]
    public async Task Listing_artists_with_invalid_pagination_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/artists?limit=0&offset=-1");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("pagination.invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Listing artists with an invalid type returns a validation error")]
    public async Task Listing_artists_with_an_invalid_type_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/artists?type=alias");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("artist.type_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Updating an artist renames the artist without changing identity or type")]
    public async Task Updating_an_artist_renames_the_artist_without_changing_identity_or_type()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        ArtistId artistId = await host.SeedArtistAsync(Group.Create(ArtistId.New(), "Joy Division"));
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/artists/{artistId}",
            new UpdateArtistRequest("  Warsaw  "));
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(artistId.Value, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("group", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Warsaw", document.RootElement.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "Deleting an artist without confirmation returns a validation error")]
    public async Task Deleting_an_artist_without_confirmation_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        ArtistId artistId = await host.SeedArtistAsync(Person.Create(ArtistId.New(), "Peter Hook"));
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.DeleteAsync($"/api/artists/{artistId}");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("delete.confirmation_required", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Deleting an artist with mismatched confirmation returns a validation error")]
    public async Task Deleting_an_artist_with_mismatched_confirmation_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        ArtistId artistId = await host.SeedArtistAsync(Person.Create(ArtistId.New(), "Peter Hook"));
        HttpClient client = host.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/artists/{artistId}");
        request.Headers.Add("X-Cratebase-Confirm-Delete", $"artist:{Guid.CreateVersion7()}");

        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("delete.confirmation_required", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Deleting an artist with matching confirmation removes the artist")]
    public async Task Deleting_an_artist_with_matching_confirmation_removes_the_artist()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        ArtistId artistId = await host.SeedArtistAsync(Person.Create(ArtistId.New(), "Stephen Morris"));
        HttpClient client = host.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/artists/{artistId}");
        request.Headers.Add("X-Cratebase-Confirm-Delete", $"artist:{artistId}");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await host.FindArtistAsync(artistId));
    }

    [Fact(DisplayName = "Deleting an artist with dependent data returns a conflict")]
    public async Task Deleting_an_artist_with_dependent_data_returns_a_conflict()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        Artist artist = Person.Create(ArtistId.New(), "Arthur Baker");
        ArtistId artistId = await host.SeedArtistAsync(artist);
        await host.SeedReleaseCreditAsync(artist);
        HttpClient client = host.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/artists/{artistId}");
        request.Headers.Add("X-Cratebase-Confirm-Delete", $"artist:{artistId}");

        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("artist.delete_conflict", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(content, exception);
        }
    }

}
