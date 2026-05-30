using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;

namespace DiscWeave.Api.Features.Settings;

internal static class DictionaryKindMapper
{
    public static DictionaryKind Parse(string kind)
    {
        return string.IsNullOrWhiteSpace(kind)
            ? throw new DomainException("dictionary_entry.kind_invalid", "Dictionary kind is invalid")
            : kind.Trim() switch
            {
                "releaseType" => DictionaryKind.ReleaseType,
                "creditRole" => DictionaryKind.CreditRole,
                "genre" => DictionaryKind.Genre,
                "mediaType" => DictionaryKind.MediaType,
                "artistRelationType" => DictionaryKind.ArtistRelationType,
                "trackRelationType" => DictionaryKind.TrackRelationType,
                _ => throw new DomainException("dictionary_entry.kind_invalid", "Dictionary kind is invalid")
            };
    }

    public static string ToCode(DictionaryKind kind)
    {
        return kind switch
        {
            DictionaryKind.ReleaseType => "releaseType",
            DictionaryKind.CreditRole => "creditRole",
            DictionaryKind.Genre => "genre",
            DictionaryKind.MediaType => "mediaType",
            DictionaryKind.ArtistRelationType => "artistRelationType",
            DictionaryKind.TrackRelationType => "trackRelationType",
            _ => throw new InvalidOperationException("Dictionary kind is not supported")
        };
    }
}
