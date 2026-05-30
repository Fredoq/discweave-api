using DiscWeave.Api.Http;

namespace DiscWeave.Api.Features.Search;

internal static class SearchRequestValidation
{
    private static readonly HashSet<string> EntityTypes = new(StringComparer.Ordinal)
    {
        "artist",
        "release",
        "track",
        "ownedItem",
        "label",
        "playlist"
    };

    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "owned",
        "wanted",
        "sold",
        "needsDigitization"
    };

    private static readonly HashSet<string> SavedViews = new(StringComparer.OrdinalIgnoreCase)
    {
        "all",
        "credits",
        "remixes",
        "productions",
        "labels",
        "needsDigitization",
        "physicalWithoutDigital",
        "lossyWithoutLossless",
        "mp3notlossless",
        "wantedNotOwned"
    };

    public static bool TryNormalize(
        string? entityType,
        string? status,
        string? savedView,
        out string? normalizedEntityType,
        out string? normalizedStatus,
        out string? normalizedSavedView,
        out IResult? error)
    {
        normalizedEntityType = NormalizeOptional(entityType);
        normalizedStatus = NormalizeOptional(status);
        normalizedSavedView = NormalizeOptional(savedView);
        error = null;

        if (normalizedEntityType is not null && !EntityTypes.Contains(normalizedEntityType))
        {
            error = EndpointErrors.BadRequest("search.entity_type_invalid", "Search entity type is invalid");
            return false;
        }

        if (normalizedStatus is not null && !Statuses.Contains(normalizedStatus))
        {
            error = EndpointErrors.BadRequest("search.status_invalid", "Search status is invalid");
            return false;
        }

        if (normalizedSavedView is not null && !SavedViews.Contains(normalizedSavedView))
        {
            error = EndpointErrors.BadRequest("search.saved_view_invalid", "Search saved view is invalid");
            return false;
        }

        return true;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
