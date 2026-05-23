using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private static IOptionalValue<int> OptionalYear(int? year)
    {
        return year.HasValue ? Optional.From(year.Value) : Optional.Missing<int>();
    }

    private static string? OptionalStringOrNull(IOptionalValue<string> value)
    {
        return value is PresentOptionalValue<string> present ? present.Value : null;
    }

    private static int? OptionalIntOrNull(IOptionalValue<int> value)
    {
        return value is PresentOptionalValue<int> present ? present.Value : null;
    }

    private static string? ReleaseYear(Domain.Catalog.Release release)
    {
        return release.Summary.Metadata.Year.HasValue
            ? release.Summary.Metadata.Year.Match(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture), () => string.Empty)
            : null;
    }

    private static int? ReleaseYearValue(Domain.Catalog.Release release)
    {
        return release.Summary.Metadata.Year.HasValue
            ? release.Summary.Metadata.Year.Match(value => value, () => 0)
            : null;
    }

    private static string StatusCode(OwnershipStatus status)
    {
        return status switch
        {
            OwnershipStatus.Owned => "owned",
            OwnershipStatus.Wanted => "wanted",
            OwnershipStatus.Sold => "sold",
            OwnershipStatus.NeedsDigitization => "needsDigitization",
            _ => throw new InvalidOperationException("Ownership status is not supported")
        };
    }
}
