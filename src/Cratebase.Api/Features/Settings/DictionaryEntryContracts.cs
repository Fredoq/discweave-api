namespace Cratebase.Api.Features.Settings;

public sealed record DictionaryEntryRequest(
    string Kind,
    string Code,
    string Name,
    int? SortOrder,
    bool? IsActive,
    string? MediaProfile);

public sealed record UpdateDictionaryEntryRequest(
    string Name,
    int? SortOrder,
    bool? IsActive,
    string? MediaProfile);

public sealed record ReplaceDictionaryEntryRequest(string ReplacementCode);

public sealed record DictionaryEntryResponse(
    Guid Id,
    string Kind,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    bool IsBuiltin,
    bool IsProtected,
    string? MediaProfile);
