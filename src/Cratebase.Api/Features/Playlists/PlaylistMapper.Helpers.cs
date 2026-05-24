using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.Playlists;

internal static partial class PlaylistMapper
{
    private static string NormalizeEntryKind(string? kind)
    {
        return string.IsNullOrWhiteSpace(kind)
            ? throw new DomainException("playlist.entry_kind_invalid", "Playlist entry kind is invalid")
            : kind.Trim();
    }

    private static ReleaseId[] ReleaseIds(IReadOnlyList<PlaylistEntry> entries)
    {
        return
        [
            .. entries
                .Select(ReleaseIdOrNull)
                .Where(id => id.HasValue)
                .Select(id => id.GetValueOrDefault())
                .Distinct()
        ];
    }

    private static TrackId[] TrackIds(IReadOnlyList<PlaylistEntry> entries)
    {
        return
        [
            .. entries
                .Select(TrackIdOrNull)
                .Where(id => id.HasValue)
                .Select(id => id.GetValueOrDefault())
                .Distinct()
        ];
    }

    private static ReleaseId? ReleaseIdOrNull(PlaylistEntry entry)
    {
        return entry.ReleaseId is PresentOptionalValue<ReleaseId> releaseId ? releaseId.Value : null;
    }

    private static TrackId? TrackIdOrNull(PlaylistEntry entry)
    {
        return entry.TrackId is PresentOptionalValue<TrackId> trackId ? trackId.Value : null;
    }

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
}
