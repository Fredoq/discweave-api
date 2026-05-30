namespace DiscWeave.Seeding;

public sealed class LargeCollectionSeedOptions
{
    public const int DefaultArtistCount = 1200;
    public const int DefaultLabelCount = 120;
    public const int DefaultReleaseCount = 1500;
    public const int DefaultTracksPerRelease = 8;

    public LargeCollectionSeedOptions(
        int artistCount,
        int labelCount,
        int releaseCount,
        int tracksPerRelease)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(artistCount, 6);
        ArgumentOutOfRangeException.ThrowIfLessThan(labelCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(releaseCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(tracksPerRelease, 2);

        ArtistCount = artistCount;
        LabelCount = labelCount;
        ReleaseCount = releaseCount;
        TracksPerRelease = tracksPerRelease;
    }

    public int ArtistCount { get; }

    public int LabelCount { get; }

    public int ReleaseCount { get; }

    public int TracksPerRelease { get; }

    public int TrackCount => checked(ReleaseCount * TracksPerRelease);

    public static LargeCollectionSeedOptions Default()
    {
        return new LargeCollectionSeedOptions(
            DefaultArtistCount,
            DefaultLabelCount,
            DefaultReleaseCount,
            DefaultTracksPerRelease);
    }
}
