namespace Cratebase.Api.Features.Playlists;

public sealed record PlaylistRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "manual";

    public IReadOnlyList<PlaylistEntryRequest> Entries { get; init; } = [];

    public SmartPlaylistRulesRequest? Rules { get; init; }
}

public sealed record PlaylistEntryRequest(string Kind, Guid Id);

public sealed record SmartPlaylistRulesRequest(
    IReadOnlyList<string>? Tags,
    IReadOnlyList<string>? Genres,
    IReadOnlyList<string>? Media,
    IReadOnlyList<string>? OwnershipStatuses,
    int? YearFrom,
    int? YearTo);

public sealed record PlaylistResponse(
    Guid Id,
    string Name,
    string? Description,
    string Type,
    SmartPlaylistRulesResponse Rules,
    IReadOnlyList<PlaylistItemResponse> Entries,
    IReadOnlyList<PlaylistItemResponse> Results);

public sealed record SmartPlaylistRulesResponse(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Media,
    IReadOnlyList<string> OwnershipStatuses,
    int? YearFrom,
    int? YearTo);

public sealed record PlaylistItemResponse(string Kind, Guid Id, string Title, string? Subtitle);
