namespace DiscWeave.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static IEnumerable<string[]> PlaylistEntryRows(ExportSnapshotResponse snapshot)
    {
        return snapshot.Playlists.SelectMany(playlist => playlist.Entries.Select((entry, index) => new[]
        {
            playlist.Id.ToString(),
            Invariant(index),
            entry.Kind,
            entry.Id.ToString(),
            entry.Title
        }));
    }

    private static string[] PlaylistHeader()
    {
        return ["id", "name", "type", "description", "rule_tags", "rule_genres", "rule_media", "rule_ownership_statuses", "rule_year_from", "rule_year_to"];
    }

    private static string[] PlaylistEntryHeader()
    {
        return ["playlist_id", "position", "kind", "id", "title"];
    }
}
