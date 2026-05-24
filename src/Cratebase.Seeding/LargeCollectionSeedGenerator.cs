using System.Globalization;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Seeding;

public static class LargeCollectionSeedGenerator
{
    private static readonly string[] Genres = ["Ambient", "Electronic", "IDM", "Techno", "House", "Post-punk", "Remix"];
    private static readonly string[] Tags = ["crate-dig", "dj-tool", "rare", "local-file", "needs-review", "club", "radio", "archive"];
    private static readonly string[] ReleaseTypes = ["album", "ep", "standalone", "compilation", "mixtape", "promo"];

    public static LargeCollectionSeedData Generate(CollectionId collectionId, LargeCollectionSeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<Artist> artists = CreateArtists(collectionId, options.ArtistCount);
        List<Label> labels = CreateLabels(collectionId, options.LabelCount);
        var releases = new List<Release>(options.ReleaseCount);
        var tracks = new List<Track>(options.TrackCount);
        var ownedItems = new List<OwnedItem>(options.ReleaseCount + options.TrackCount);
        var credits = new List<Credit>(options.ReleaseCount * (options.TracksPerRelease + 3));
        List<ArtistRelation> artistRelations = CreateArtistRelations(collectionId, artists);
        var trackRelations = new List<TrackRelation>();
        var state = new SeedGenerationState(collectionId, artists, tracks, credits, ownedItems, trackRelations);

        for (int releaseIndex = 0; releaseIndex < options.ReleaseCount; releaseIndex++)
        {
            Release release = CreateRelease(collectionId, labels, releaseIndex);
            Artist mainArtist = artists[releaseIndex % artists.Count];
            AddReleaseCredits(collectionId, credits, release, mainArtist, artists, releaseIndex);

            List<ReleaseTrack> releaseTracks = CreateTracksForRelease(
                state,
                options,
                releaseIndex,
                mainArtist);

            release.ReplaceTracklist(releaseTracks);
            releases.Add(release);
            ownedItems.Add(CreateReleaseOwnedItem(collectionId, release.Id, releaseIndex));
        }

        List<Playlist> playlists = CreatePlaylists(collectionId, releases, tracks);

        return new LargeCollectionSeedData
        {
            Artists = artists,
            Labels = labels,
            Releases = releases,
            Tracks = tracks,
            OwnedItems = ownedItems,
            Credits = credits,
            ArtistRelations = artistRelations,
            TrackRelations = trackRelations,
            Playlists = playlists
        };
    }

    private static List<Artist> CreateArtists(CollectionId collectionId, int artistCount)
    {
        var artists = new List<Artist>(artistCount);
        for (int index = 0; index < artistCount; index++)
        {
            bool isGroup = index % 6 == 0;
            string name = isGroup
                ? $"Seed Collective {index + 1:0000}"
                : $"Seed Artist {index + 1:0000}";
            artists.Add(isGroup
                ? Group.Create(collectionId, ArtistId.New(), name)
                : Person.Create(collectionId, ArtistId.New(), name));
        }

        return artists;
    }

    private static List<Label> CreateLabels(CollectionId collectionId, int labelCount)
    {
        var labels = new List<Label>(labelCount);
        for (int index = 0; index < labelCount; index++)
        {
            labels.Add(Label.Create(collectionId, LabelId.New(), $"Seed Label {index + 1:000}"));
        }

        return labels;
    }

    private static Release CreateRelease(CollectionId collectionId, List<Label> labels, int releaseIndex)
    {
        Label label = labels[releaseIndex % labels.Count];
        var release = Release.Create(collectionId, ReleaseId.New(), $"Seed Release {releaseIndex + 1:00000}");
        ReleaseMetadata metadata = ReleaseMetadata.Empty
            .WithType(ReleaseTypes[releaseIndex % ReleaseTypes.Length])
            .WithLabel(label.Id)
            .WithReleaseYear(1980 + (releaseIndex % 45))
            .WithReleaseDate(new DateOnly(1980 + (releaseIndex % 45), (releaseIndex % 12) + 1, (releaseIndex % 27) + 1));

        release.UpdateSummary(ReleaseSummary.Create(release.Summary.Title).WithMetadata(metadata));
        release.UpdateCataloging(Cataloging.Empty
            .WithGenre(Genre.FromName(Genres[releaseIndex % Genres.Length]))
            .WithTag(Tag.FromName(Tags[releaseIndex % Tags.Length]))
            .WithTag(Tag.FromName($"crate-{releaseIndex % 20:00}")));
        release.UpdateArtistDisplay(releaseIndex % 10 == 0);
        release.UpdateLabels(false, [ReleaseLabel.Create(label.Id, Optional.From($"CB-{releaseIndex + 1:00000}"), false)]);

        return release;
    }

    private static void AddReleaseCredits(
        CollectionId collectionId,
        List<Credit> credits,
        Release release,
        Artist mainArtist,
        List<Artist> artists,
        int releaseIndex)
    {
        credits.Add(Credit.Create(collectionId, CreditId.New(), CreditContributor.FromArtist(mainArtist), CreditTarget.ForRelease(release.Id), CreditRole.MainArtist));
        credits.Add(Credit.Create(collectionId, CreditId.New(), CreditContributor.FromArtist(artists[(releaseIndex + 17) % artists.Count]), CreditTarget.ForRelease(release.Id), CreditRole.Producer));
    }

    private static List<ReleaseTrack> CreateTracksForRelease(
        SeedGenerationState state,
        LargeCollectionSeedOptions options,
        int releaseIndex,
        Artist mainArtist)
    {
        var releaseTracks = new List<ReleaseTrack>(options.TracksPerRelease);
        TrackId firstTrackId = default;

        for (int trackNumber = 1; trackNumber <= options.TracksPerRelease; trackNumber++)
        {
            int globalTrackIndex = (releaseIndex * options.TracksPerRelease) + trackNumber - 1;
            Track track = CreateTrack(state.CollectionId, releaseIndex, trackNumber, globalTrackIndex);
            state.Tracks.Add(track);
            releaseTracks.Add(ReleaseTrack.Create(track.Id, TrackPosition.FromNumber(trackNumber)));
            state.Credits.Add(Credit.Create(state.CollectionId, CreditId.New(), CreditContributor.FromArtist(mainArtist), CreditTarget.ForTrack(track.Id), CreditRole.MainArtist));

            if (globalTrackIndex % 7 == 0)
            {
                state.Credits.Add(Credit.Create(state.CollectionId, CreditId.New(), CreditContributor.FromArtist(state.Artists[(globalTrackIndex + 31) % state.Artists.Count]), CreditTarget.ForTrack(track.Id), CreditRole.Remixer));
            }

            if (releaseIndex % 7 != 0)
            {
                state.OwnedItems.Add(CreateDigitalTrackOwnedItem(state.CollectionId, track.Id, globalTrackIndex));
            }

            if (trackNumber == 1)
            {
                firstTrackId = track.Id;
            }
            else if (trackNumber == 2 && releaseIndex % 4 == 0)
            {
                state.TrackRelations.Add(TrackRelation.Create(TrackRelationId.New(), state.CollectionId, track.Id, firstTrackId, TrackRelationType.RemixOf));
            }
        }

        return releaseTracks;
    }

    private static Track CreateTrack(CollectionId collectionId, int releaseIndex, int trackNumber, int globalTrackIndex)
    {
        var track = Track.Create(collectionId, TrackId.New(), $"Seed Track {releaseIndex + 1:00000}-{trackNumber:00}");
        track.UpdateDetails(TrackDetails.Empty.WithDuration(TimeSpan.FromSeconds(150 + (globalTrackIndex % 360))));
        track.UpdateCataloging(Cataloging.Empty
            .WithGenre(Genre.FromName(Genres[globalTrackIndex % Genres.Length]))
            .WithTag(Tag.FromName(Tags[globalTrackIndex % Tags.Length])));

        return track;
    }

    private static OwnedItem CreateReleaseOwnedItem(CollectionId collectionId, ReleaseId releaseId, int releaseIndex)
    {
        IMedium medium = (releaseIndex % 4) switch
        {
            0 => VinylRecord.Create("12 inch LP"),
            1 => CompactDisc.Create(1 + (releaseIndex % 3)),
            2 => CassetteTape.Create("Chrome cassette"),
            _ => OtherMedium.Create("Promo CDR")
        };
        OwnershipStatus status = ReleaseOwnershipStatus(releaseIndex);

        return OwnedItem.Create(collectionId, OwnedItemId.New(), OwnedItemTarget.ForRelease(releaseId), status, medium)
            .WithCondition((ItemCondition)((releaseIndex % 7) + 1))
            .WithStorageLocation(StorageLocation.FromName($"Shelf {(releaseIndex % 24) + 1:00}"));
    }

    private static OwnedItem CreateDigitalTrackOwnedItem(CollectionId collectionId, TrackId trackId, int globalTrackIndex)
    {
        AudioFileFormat format = globalTrackIndex % 5 == 0 ? AudioFileFormat.Mp3 : AudioFileFormat.Flac;
        var path = FilePath.FromAbsolutePath($"/cratebase/seed/audio/{globalTrackIndex / 1000:000}/{globalTrackIndex:000000}.{format.ToString().ToLowerInvariant()}");
        var identity = FileImportIdentity.Create(
            path,
            3_000_000 + (globalTrackIndex * 17L),
            DateTimeOffset.UnixEpoch.AddMinutes(globalTrackIndex),
            globalTrackIndex.ToString("x64", CultureInfo.InvariantCulture));

        return OwnedItem.Create(collectionId, OwnedItemId.New(), OwnedItemTarget.ForTrack(trackId), OwnershipStatus.Owned, DigitalFile.Create(path, format, identity));
    }

    private static OwnershipStatus ReleaseOwnershipStatus(int releaseIndex)
    {
        return releaseIndex switch
        {
            int value when value % 11 == 0 => OwnershipStatus.Wanted,
            int value when value % 9 == 0 => OwnershipStatus.NeedsDigitization,
            _ => OwnershipStatus.Owned
        };
    }

    private static List<ArtistRelation> CreateArtistRelations(CollectionId collectionId, List<Artist> artists)
    {
        int relationCount = Math.Max(1, artists.Count / 12);
        var relations = new List<ArtistRelation>(relationCount);
        for (int index = 0; index < relationCount; index++)
        {
            Artist source = artists[((index * 6) + 1) % artists.Count];
            Artist target = artists[index * 6 % artists.Count];
            relations.Add(ArtistRelation.Create(ArtistRelationId.New(), collectionId, source.Id, target.Id, ArtistRelationType.MemberOf));
        }

        return relations;
    }

    private static List<Playlist> CreatePlaylists(CollectionId collectionId, IReadOnlyList<Release> releases, IReadOnlyList<Track> tracks)
    {
        var manual = Playlist.Create(collectionId, PlaylistId.New(), "Seed listening queue", PlaylistType.Manual);
        manual.ReplaceManualEntries(
        [
            .. releases.Take(8).Select((release, index) => PlaylistEntry.ForRelease(index, release.Id)),
            .. tracks.Take(12).Select((track, index) => PlaylistEntry.ForTrack(index + 8, track.Id))
        ]);

        var smart = Playlist.Create(collectionId, PlaylistId.New(), "Seed lossy and digitization audit", PlaylistType.Smart);
        smart.ReplaceSmartRules(SmartPlaylistRules.Create(
            ["crate-01", "needs-review"],
            ["Techno", "Electronic"],
            ["digital", "vinyl"],
            ["owned", "needsDigitization"],
            Optional.From(1990),
            Optional.From(2025)));

        return [manual, smart];
    }

    private sealed record SeedGenerationState(
        CollectionId CollectionId,
        List<Artist> Artists,
        List<Track> Tracks,
        List<Credit> Credits,
        List<OwnedItem> OwnedItems,
        List<TrackRelation> TrackRelations);
}
