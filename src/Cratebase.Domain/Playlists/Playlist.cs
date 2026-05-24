using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Playlists;

public sealed class Playlist : IEntity<PlaylistId>
{
    private readonly List<PlaylistEntry> _entries = [];
    private string? _description;
    private string _ruleTags = string.Empty;
    private string _ruleGenres = string.Empty;
    private string _ruleMedia = string.Empty;
    private string _ruleOwnershipStatuses = string.Empty;
    private int? _ruleYearFrom;
    private int? _ruleYearTo;

    private Playlist()
    {
    }

    private Playlist(CollectionId collectionId, PlaylistId id, string name, PlaylistType type)
    {
        CollectionId = collectionId;
        Id = id;
        Name = Guard.RequiredText(name, nameof(name), "playlist.name_required");
        Type = type;
    }

    public CollectionId CollectionId { get; private set; }

    public PlaylistId Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IOptionalValue<string> Description => _description is null
        ? Optional.Missing<string>()
        : Optional.From(_description);

    public PlaylistType Type { get; private set; }

    public IReadOnlyList<PlaylistEntry> Entries => _entries.AsReadOnly();

    public SmartPlaylistRules Rules => SmartPlaylistRules.Create(
        Unpack(_ruleTags),
        Unpack(_ruleGenres),
        Unpack(_ruleMedia),
        Unpack(_ruleOwnershipStatuses),
        OptionalYear(_ruleYearFrom),
        OptionalYear(_ruleYearTo));

    public static Playlist Create(CollectionId collectionId, PlaylistId id, string name, PlaylistType type)
    {
        return new Playlist(collectionId, id, name, type);
    }

    public void Rename(string name)
    {
        Name = Guard.RequiredText(name, nameof(name), "playlist.name_required");
    }

    public void UpdateDescription(IOptionalValue<string> description)
    {
        ArgumentNullException.ThrowIfNull(description);

        _description = description is PresentOptionalValue<string> present && !string.IsNullOrWhiteSpace(present.Value)
            ? present.Value.Trim()
            : null;
    }

    public void ReplaceManualEntries(IReadOnlyList<PlaylistEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Type = PlaylistType.Manual;
        ClearRules();
        _entries.Clear();
        _entries.AddRange(entries.OrderBy(entry => entry.Position));
    }

    public void ReplaceSmartRules(SmartPlaylistRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (TryGetYear(rules.YearFrom, out int yearFrom) &&
            TryGetYear(rules.YearTo, out int yearTo) &&
            yearFrom > yearTo)
        {
            throw new DomainException("playlist.rules_year_range_invalid", "Playlist year range is invalid");
        }

        Type = PlaylistType.Smart;
        _entries.Clear();
        _ruleTags = Pack(rules.Tags);
        _ruleGenres = Pack(rules.Genres);
        _ruleMedia = Pack(rules.Media);
        _ruleOwnershipStatuses = Pack(rules.OwnershipStatuses);
        _ruleYearFrom = TryGetYear(rules.YearFrom, out yearFrom) ? yearFrom : null;
        _ruleYearTo = TryGetYear(rules.YearTo, out yearTo) ? yearTo : null;
    }

    private void ClearRules()
    {
        _ruleTags = string.Empty;
        _ruleGenres = string.Empty;
        _ruleMedia = string.Empty;
        _ruleOwnershipStatuses = string.Empty;
        _ruleYearFrom = null;
        _ruleYearTo = null;
    }

    private static string Pack(IEnumerable<string> values)
    {
        return string.Join('\n', values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
    }

    private static string[] Unpack(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static IOptionalValue<int> OptionalYear(int? year)
    {
        return year.HasValue ? Optional.From(year.Value) : Optional.Missing<int>();
    }

    private static bool TryGetYear(IOptionalValue<int> year, out int value)
    {
        if (year is PresentOptionalValue<int> present)
        {
            value = present.Value;
            return true;
        }

        value = default;
        return false;
    }
}
