using DiscWeave.Domain.SharedKernel.Optional;

namespace DiscWeave.Domain.Playlists;

public sealed record SmartPlaylistRules
{
    public SmartPlaylistRules(
        IReadOnlyList<string> tags,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> media,
        IReadOnlyList<string> ownershipStatuses,
        IOptionalValue<int> yearFrom,
        IOptionalValue<int> yearTo)
    {
        ArgumentNullException.ThrowIfNull(yearFrom);
        ArgumentNullException.ThrowIfNull(yearTo);

        Tags = Normalize(tags);
        Genres = Normalize(genres);
        Media = Normalize(media);
        OwnershipStatuses = Normalize(ownershipStatuses);
        YearFrom = yearFrom;
        YearTo = yearTo;
    }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<string> Genres { get; }

    public IReadOnlyList<string> Media { get; }

    public IReadOnlyList<string> OwnershipStatuses { get; }

    public IOptionalValue<int> YearFrom { get; }

    public IOptionalValue<int> YearTo { get; }

    public static SmartPlaylistRules Empty { get; } = Create([], [], [], [], Optional.Missing<int>(), Optional.Missing<int>());

    public static SmartPlaylistRules Create(
        IReadOnlyList<string> tags,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> media,
        IReadOnlyList<string> ownershipStatuses,
        IOptionalValue<int> yearFrom,
        IOptionalValue<int> yearTo)
    {
        return new SmartPlaylistRules(
            tags,
            genres,
            media,
            ownershipStatuses,
            yearFrom,
            yearTo);
    }

    private static string[] Normalize(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return
        [
            .. values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }
}
