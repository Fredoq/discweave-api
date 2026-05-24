namespace Cratebase.Api.Features.Playlists;

public sealed record SmartPlaylistRulesResponse
{
    public SmartPlaylistRulesResponse(
        IReadOnlyList<string> tags,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> media,
        IReadOnlyList<string> ownershipStatuses,
        int? yearFrom,
        int? yearTo)
    {
        Tags = tags;
        Genres = genres;
        Media = media;
        OwnershipStatuses = ownershipStatuses;
        YearFrom = yearFrom;
        YearTo = yearTo;
    }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<string> Genres { get; }

    public IReadOnlyList<string> Media { get; }

    public IReadOnlyList<string> OwnershipStatuses { get; }

    public int? YearFrom { get; }

    public int? YearTo { get; }
}
