using System.Globalization;
using Cratebase.Api.Features.OwnedItems;
using Cratebase.Api.Features.Playlists;
using Cratebase.Api.Features.Releases;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static ReleaseMetadata ToReleaseMetadata(ReleaseResponse release)
    {
        ReleaseMetadata metadata = ReleaseMetadata.Empty.WithType(release.Type);
        if (release.LabelId is { } labelId)
        {
            metadata = metadata.WithLabel(new LabelId(labelId));
        }

        if (release.Year is { } year)
        {
            metadata = metadata.WithReleaseYear(year);
        }

        if (!string.IsNullOrWhiteSpace(release.ReleaseDate) &&
            DateOnly.TryParse(release.ReleaseDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly releaseDate))
        {
            metadata = metadata.WithReleaseDate(releaseDate);
        }

        return release.CoverImage is { } coverImage
            ? metadata.WithCoverImage(CoverImage.FromStoredMetadata(
                coverImage.Url,
                coverImage.ContentType,
                coverImage.OriginalFileName,
                coverImage.SizeBytes,
                coverImage.SourceType))
            : metadata;
    }

    private static Cataloging ToCataloging(IReadOnlyList<string> genres, IReadOnlyList<string> tags)
    {
        Cataloging cataloging = Cataloging.Empty;
        foreach (string genre in genres)
        {
            cataloging = cataloging.WithGenre(Genre.FromName(genre));
        }

        foreach (string tag in tags)
        {
            cataloging = cataloging.WithTag(Tag.FromName(tag));
        }

        return cataloging;
    }

    private static ReleaseLabel ToReleaseLabel(ReleaseLabelResponse label)
    {
        return ReleaseLabel.Create(
            new LabelId(label.LabelId.GetValueOrDefault()),
            OptionalText(label.CatalogNumber),
            label.HasNoCatalogNumber);
    }

    private static ReleaseTrack ToReleaseTrack(ReleaseTracklistItemResponse track)
    {
        return ReleaseTrack.Create(
            new TrackId(track.TrackId),
            TrackPosition.FromNumber(track.Position),
            Optional.Missing<string>(),
            OptionalText(track.VersionNote));
    }

    private static IMedium ToMedium(MediumResponse medium)
    {
        return medium.Type switch
        {
            "digital" => DigitalFile.Create(
                medium.Type,
                FilePath.FromAbsolutePath(medium.Path ?? "/cratebase/restored-digital-file"),
                ToAudioFileFormat(medium.Format ?? "mp3")),
            "vinyl" => VinylRecord.Create(medium.Type, medium.Description),
            "cd" => CompactDisc.Create(medium.Type, medium.DiscCount ?? 1),
            "cassette" => CassetteTape.Create(medium.Type, medium.Description),
            _ => OtherMedium.Create(medium.Type, medium.Description)
        };
    }

    private static AudioFileFormat ToAudioFileFormat(string format)
    {
        return format switch
        {
            "flac" => AudioFileFormat.Flac,
            "mp3" => AudioFileFormat.Mp3,
            "ogg" => AudioFileFormat.Ogg,
            "wav" => AudioFileFormat.Wav,
            "aiff" => AudioFileFormat.Aiff,
            "alac" => AudioFileFormat.Alac,
            "m4a" => AudioFileFormat.M4a,
            _ => throw new DomainException("digital_file.format_invalid", "Digital file format is invalid")
        };
    }

    private static CreditTarget ToCreditTarget(string targetType, Guid targetId)
    {
        return targetType switch
        {
            "release" => CreditTarget.ForRelease(new ReleaseId(targetId)),
            "track" => CreditTarget.ForTrack(new TrackId(targetId)),
            _ => throw new DomainException("credit.target_type_invalid", "Credit target type is invalid")
        };
    }

    private static PlaylistEntry ToPlaylistEntry(PlaylistItemResponse entry, int index)
    {
        return entry.Kind switch
        {
            PlaylistEntry.ReleaseKind => PlaylistEntry.ForRelease(index, new ReleaseId(entry.Id)),
            PlaylistEntry.TrackKind => PlaylistEntry.ForTrack(index, new TrackId(entry.Id)),
            _ => throw new DomainException("playlist.entry_kind_invalid", "Playlist entry kind is invalid")
        };
    }

    private static PlaylistEntry[] ToPlaylistEntries(IReadOnlyList<PlaylistItemResponse> entries)
    {
        var restored = new List<PlaylistEntry>(entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            restored.Add(ToPlaylistEntry(entries[index], index));
        }

        return [.. restored];
    }

    private static ArtistRelationPeriod ToRelationPeriod(int? startYear, int? endYear)
    {
        return (startYear, endYear) switch
        {
            ({ } start, { } end) => ArtistRelationPeriod.FromYears(start, end),
            ({ } start, null) => ArtistRelationPeriod.StartingAt(start),
            (null, { } end) => ArtistRelationPeriod.EndingAt(end),
            _ => throw new DomainException("relation_period.required", "Relation period is required")
        };
    }

    private static IOptionalValue<string> OptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Optional.Missing<string>() : Optional.From(value.Trim());
    }

    private static IOptionalValue<int> OptionalYear(int? value)
    {
        return value.HasValue ? Optional.From(value.Value) : Optional.Missing<int>();
    }

    private sealed class ArtistLookup
    {
        private readonly IReadOnlyDictionary<ArtistId, Artist> _artists;

        public ArtistLookup(IReadOnlyDictionary<ArtistId, Artist> artists)
        {
            _artists = artists;
        }

        public Artist Get(ArtistId artistId)
        {
            return _artists.TryGetValue(artistId, out Artist? artist)
                ? artist
                : throw new DomainException("artist.not_found", "Artist was not found");
        }
    }
}
