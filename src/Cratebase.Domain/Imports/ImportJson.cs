using System.Text.Json;

namespace Cratebase.Domain.Imports;

internal static class ImportJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(IReadOnlyList<T>? values)
    {
        return JsonSerializer.Serialize(values ?? [], Options);
    }

    public static IReadOnlyList<T> Deserialize<T>(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyList<T>>(json, Options) ?? [];
    }
}
