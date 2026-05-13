using Cratebase.Domain.Settings;

namespace Cratebase.Infrastructure.Persistence.Queries;

internal sealed class DictionarySearchLookup
{
    private readonly Dictionary<(DictionaryKind Kind, string Code), string> _names;

    private DictionarySearchLookup(Dictionary<(DictionaryKind Kind, string Code), string> names)
    {
        _names = names;
    }

    public static DictionarySearchLookup From(IEnumerable<CollectionDictionaryEntry> entries)
    {
        return new DictionarySearchLookup(entries.ToDictionary(entry => (entry.Kind, entry.Code), entry => entry.Name));
    }

    public bool Contains(DictionaryKind kind, string code, string term)
    {
        return code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (_names.TryGetValue((kind, code), out string? name) && name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public string LabelOrCode(DictionaryKind kind, string code)
    {
        return _names.TryGetValue((kind, code), out string? name) ? name : code;
    }
}
