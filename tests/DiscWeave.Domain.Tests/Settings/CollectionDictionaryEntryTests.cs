using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Tests.Settings;

public sealed class CollectionDictionaryEntryTests
{
    [Fact(DisplayName = "Dictionary entries require stable codes and mutable names")]
    public void Dictionary_entries_require_stable_codes_and_mutable_names()
    {
        var collectionId = CollectionId.New();
        var entry = CollectionDictionaryEntry.Create(
            CollectionDictionaryEntryId.New(),
            collectionId,
            DictionaryKind.ReleaseType,
            "album",
            "Album",
            10,
            isBuiltin: true);

        entry.Rename("Long player");
        entry.Reorder(20);
        entry.Deactivate();

        Assert.Equal(collectionId, entry.CollectionId);
        Assert.Equal(DictionaryKind.ReleaseType, entry.Kind);
        Assert.Equal("album", entry.Code);
        Assert.Equal("Long player", entry.Name);
        Assert.Equal(20, entry.SortOrder);
        Assert.False(entry.IsActive);
        Assert.True(entry.IsBuiltin);
    }

    [Fact(DisplayName = "Protected dictionary entries cannot be deactivated or deleted")]
    public void Protected_dictionary_entries_cannot_be_deactivated_or_deleted()
    {
        var entry = CollectionDictionaryEntry.Create(
            CollectionDictionaryEntryId.New(),
            CollectionId.New(),
            DictionaryKind.CreditRole,
            "mainArtist",
            "Main artist",
            10,
            isBuiltin: true);

        Assert.True(entry.IsProtected);
        Assert.Equal("dictionary_entry.protected", Assert.Throws<DomainException>(entry.Deactivate).Code);
        Assert.Equal("dictionary_entry.protected", Assert.Throws<DomainException>(entry.EnsureCanDelete).Code);
    }
}
