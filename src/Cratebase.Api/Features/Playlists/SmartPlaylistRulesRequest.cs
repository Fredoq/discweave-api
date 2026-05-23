namespace Cratebase.Api.Features.Playlists;

public sealed record SmartPlaylistRulesRequest
{
    public IReadOnlyList<string>? Tags { get; init; }

    public IReadOnlyList<string>? Genres { get; init; }

    public IReadOnlyList<string>? Media { get; init; }

    public IReadOnlyList<string>? OwnershipStatuses { get; init; }

    public int? YearFrom { get; init; }

    public int? YearTo { get; init; }
}
