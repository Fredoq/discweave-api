using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Playlists;

public sealed record SmartPlaylistRules(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Media,
    IReadOnlyList<string> OwnershipStatuses,
    IOptionalValue<int> YearFrom,
    IOptionalValue<int> YearTo)
{
    public static SmartPlaylistRules Empty { get; } = Create([], [], [], [], Optional.Missing<int>(), Optional.Missing<int>());

    public static SmartPlaylistRules Create(
        IReadOnlyList<string> tags,
        IReadOnlyList<string> genres,
        IReadOnlyList<string> media,
        IReadOnlyList<string> ownershipStatuses,
        IOptionalValue<int> yearFrom,
        IOptionalValue<int> yearTo)
    {
        ArgumentNullException.ThrowIfNull(yearFrom);
        ArgumentNullException.ThrowIfNull(yearTo);

        return new SmartPlaylistRules(
            Normalize(tags),
            Normalize(genres),
            Normalize(media),
            Normalize(ownershipStatuses),
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
