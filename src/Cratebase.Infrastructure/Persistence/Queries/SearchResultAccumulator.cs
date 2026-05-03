using Cratebase.Application.Search;

namespace Cratebase.Infrastructure.Persistence.Queries;

internal sealed class SearchResultAccumulator
{
    private readonly Dictionary<(string Type, Guid Id), MutableSearchResult> _results = [];

    public IEnumerable<MutableSearchResult> Results => _results.Values;

    public void Add(Guid id, string type, string title, string? subtitle, string matchedField, int score)
    {
        if (!_results.TryGetValue((type, id), out MutableSearchResult? result))
        {
            result = new MutableSearchResult(id, type, title, subtitle);
            _results[(type, id)] = result;
        }

        result.AddMatch(matchedField, score, subtitle);
    }
}

internal sealed class MutableSearchResult
{
    private readonly SortedSet<string> _matchedFields = new(StringComparer.Ordinal);

    public MutableSearchResult(Guid id, string type, string title, string? subtitle)
    {
        Id = id;
        Type = type;
        Title = title;
        Subtitle = subtitle;
    }

    public Guid Id { get; }

    public string Type { get; }

    public string Title { get; }

    public string? Subtitle { get; private set; }

    public int Score { get; private set; }

    public void AddMatch(string matchedField, int score, string? subtitle)
    {
        _ = _matchedFields.Add(matchedField);
        Score = Math.Max(Score, score);
        Subtitle ??= subtitle;
    }

    public SearchResultReadModel ToReadModel()
    {
        return new SearchResultReadModel(Id, Type, Title, Subtitle, [.. _matchedFields]);
    }
}
