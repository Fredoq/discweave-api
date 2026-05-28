namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static string[] ReleaseHeader()
    {
        return
        [
            "id",
            "title",
            "type",
            "label_id",
            "year",
            "release_date",
            "is_various_artists",
            "not_on_label",
            "genres",
            "tags",
            "cover_image_url",
            "cover_image_content_type",
            "cover_image_original_file_name",
            "cover_image_size_bytes",
            "cover_image_source_type"
        ];
    }

    private static string[] ReleaseLabelHeader()
    {
        return ["release_id", "label_id", "name", "catalog_number", "has_no_catalog_number"];
    }

    private static string[] ReleaseTracklistHeader()
    {
        return ["release_id", "track_id", "position", "title", "duration_seconds", "version_note"];
    }

    private static string[] TrackHeader()
    {
        return ["id", "title", "duration_seconds", "genres", "tags"];
    }

    private static string[] OwnedItemHeader()
    {
        return ["id", "target_type", "target_id", "status", "medium_type", "medium_description", "medium_path", "medium_format", "medium_disc_count", "condition", "storage_location"];
    }

    private static string[] CreditHeader()
    {
        return ["id", "contributor_artist_id", "contributor_name", "target_type", "target_id", "role"];
    }

    private static string[] ArtistRelationHeader()
    {
        return ["id", "source_artist_id", "target_artist_id", "type", "start_year", "end_year"];
    }

    private static string[] TrackRelationHeader()
    {
        return ["id", "source_track_id", "target_track_id", "type"];
    }

    private static string[] DictionaryHeader()
    {
        return ["id", "kind", "code", "name", "sort_order", "is_active", "is_builtin", "is_protected", "media_profile"];
    }

    private static string[] ImportPatternHeader()
    {
        return ["id", "kind", "template", "sort_order", "is_active", "is_builtin"];
    }

    private static string[] RatingCriterionHeader()
    {
        return ["id", "code", "name", "target_types", "sort_order", "is_active", "is_builtin", "is_protected"];
    }

    private static string[] RatingHeader()
    {
        return ["id", "criterion_id", "target_type", "target_id", "value"];
    }
}
