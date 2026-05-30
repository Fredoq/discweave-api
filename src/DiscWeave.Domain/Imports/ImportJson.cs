using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscWeave.Domain.Imports;

internal static class ImportJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

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

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<ImportReviewSeverity>(JsonNamingPolicy.CamelCase));
        return options;
    }
}
