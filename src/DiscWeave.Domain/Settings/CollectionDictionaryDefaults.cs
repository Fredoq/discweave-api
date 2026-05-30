using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Settings;

public static class CollectionDictionaryDefaults
{
    public static IReadOnlyList<CollectionDictionaryEntry> CreateEntries(CollectionId collectionId)
    {
        return
        [
            Entry(collectionId, DictionaryKind.ReleaseType, "unknown", "Unknown", 0),
            Entry(collectionId, DictionaryKind.ReleaseType, "album", "Album", 10),
            Entry(collectionId, DictionaryKind.ReleaseType, "ep", "EP", 20),
            Entry(collectionId, DictionaryKind.ReleaseType, "standalone", "Single", 30),
            Entry(collectionId, DictionaryKind.ReleaseType, "compilation", "Compilation", 40),
            Entry(collectionId, DictionaryKind.ReleaseType, "bootleg", "Bootleg", 50),
            Entry(collectionId, DictionaryKind.ReleaseType, "mixtape", "Mixtape", 60),
            Entry(collectionId, DictionaryKind.ReleaseType, "promo", "Promo", 70),
            Entry(collectionId, DictionaryKind.ReleaseType, "other", "Other", 80),
            Entry(collectionId, DictionaryKind.CreditRole, "mainArtist", "Main artist", 10),
            Entry(collectionId, DictionaryKind.CreditRole, "featuredArtist", "Featured artist", 20),
            Entry(collectionId, DictionaryKind.CreditRole, "remixer", "Remixer", 30),
            Entry(collectionId, DictionaryKind.CreditRole, "producer", "Producer", 40),
            Entry(collectionId, DictionaryKind.CreditRole, "composer", "Composer", 50),
            Entry(collectionId, DictionaryKind.CreditRole, "performer", "Performer", 60),
            Entry(collectionId, DictionaryKind.CreditRole, "engineer", "Engineer", 70),
            Entry(collectionId, DictionaryKind.Genre, "Ambient", "Ambient", 10),
            Entry(collectionId, DictionaryKind.Genre, "Electronic", "Electronic", 20),
            Entry(collectionId, DictionaryKind.Genre, "IDM", "IDM", 30),
            Entry(collectionId, DictionaryKind.Genre, "Techno", "Techno", 40),
            Entry(collectionId, DictionaryKind.Genre, "House", "House", 50),
            Entry(collectionId, DictionaryKind.Genre, "Synth-pop", "Synth-pop", 60),
            Entry(collectionId, DictionaryKind.Genre, "Post-punk", "Post-punk", 70),
            Entry(collectionId, DictionaryKind.Genre, "Remix", "Remix", 80),
            Media(collectionId, "digital", "Digital", 10, "digital"),
            Media(collectionId, "vinyl", "Vinyl", 20, "vinyl"),
            Media(collectionId, "cd", "CD", 30, "cd"),
            Media(collectionId, "cassette", "Cassette", 40, "cassette"),
            Media(collectionId, "other", "Other", 50, "other"),
            Entry(collectionId, DictionaryKind.ArtistRelationType, "alias", "Alias", 10),
            Entry(collectionId, DictionaryKind.ArtistRelationType, "memberOf", "Member of", 20),
            Entry(collectionId, DictionaryKind.ArtistRelationType, "soloProject", "Solo project", 30),
            Entry(collectionId, DictionaryKind.ArtistRelationType, "collaboration", "Collaboration", 40),
            Entry(collectionId, DictionaryKind.TrackRelationType, "remixOf", "Remix of", 10),
            Entry(collectionId, DictionaryKind.TrackRelationType, "versionOf", "Version of", 20),
            Entry(collectionId, DictionaryKind.TrackRelationType, "editOf", "Edit of", 30)
        ];
    }

    private static CollectionDictionaryEntry Entry(
        CollectionId collectionId,
        DictionaryKind kind,
        string code,
        string name,
        int sortOrder)
    {
        return CollectionDictionaryEntry.Create(CollectionDictionaryEntryId.New(), collectionId, kind, code, name, sortOrder, isBuiltin: true);
    }

    private static CollectionDictionaryEntry Media(
        CollectionId collectionId,
        string code,
        string name,
        int sortOrder,
        string mediaProfile)
    {
        return CollectionDictionaryEntry.CreateMedia(CollectionDictionaryEntryId.New(), collectionId, code, name, sortOrder, isBuiltin: true, mediaProfile);
    }
}
