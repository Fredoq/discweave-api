namespace Cratebase.Application.Catalog.Artists;

public sealed record ArtistListQuery
{
    public ArtistListQuery(string search, string type, int limit, int offset)
    {
        Search = search;
        Type = type;
        Limit = limit;
        Offset = offset;
    }

    public string Search { get; }

    public string Type { get; }

    public int Limit { get; }

    public int Offset { get; }
}
