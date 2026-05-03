using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Tests.Collection;

public sealed class OwnedItemTests
{
    [Fact]
    public void Owned_item_can_target_a_release()
    {
        var releaseId = ReleaseId.New();
        var releaseItem = OwnedItem.Create(CollectionId.New(),
            OwnedItemId.New(),
            OwnedItemTarget.ForRelease(releaseId),
            OwnershipStatus.Owned,
            VinylRecord.Create("LP"));

        Assert.True(releaseItem.Target.IsRelease);
        Assert.False(releaseItem.Target.IsTrack);
        Assert.Equal(releaseId, Assert.IsType<ReleaseOwnedItemTarget>(releaseItem.Target).ReleaseId);
    }

    [Fact]
    public void Owned_item_can_target_a_track()
    {
        var trackId = TrackId.New();
        var trackItem = OwnedItem.Create(CollectionId.New(),
            OwnedItemId.New(),
            OwnedItemTarget.ForTrack(trackId),
            OwnershipStatus.Owned,
            DigitalFile.Create(FilePath.FromAbsolutePath("/music/New Order/Blue Monday.flac"), AudioFileFormat.Flac));

        Assert.True(trackItem.Target.IsTrack);
        Assert.False(trackItem.Target.IsRelease);
        Assert.Equal(trackId, Assert.IsType<TrackOwnedItemTarget>(trackItem.Target).TrackId);
    }

    [Fact]
    public void Owned_item_requires_a_concrete_medium_model()
    {
        var vinylItem = OwnedItem.Create(CollectionId.New(),
            OwnedItemId.New(),
            OwnedItemTarget.ForRelease(ReleaseId.New()),
            OwnershipStatus.NeedsDigitization,
            VinylRecord.Create("12-inch"));
        var cdItem = OwnedItem.Create(CollectionId.New(),
            OwnedItemId.New(),
            OwnedItemTarget.ForRelease(ReleaseId.New()),
            OwnershipStatus.Owned,
            CompactDisc.Create(1));
        var cassetteItem = OwnedItem.Create(CollectionId.New(),
            OwnedItemId.New(),
            OwnedItemTarget.ForRelease(ReleaseId.New()),
            OwnershipStatus.Wanted,
            CassetteTape.Create("Chrome"));

        _ = Assert.IsType<VinylRecord>(vinylItem.Holding.Medium);
        _ = Assert.IsType<CompactDisc>(cdItem.Holding.Medium);
        _ = Assert.IsType<CassetteTape>(cassetteItem.Holding.Medium);
    }

    [Fact]
    public void Concrete_media_models_validate_required_details()
    {
        Assert.Equal("vinyl_record.format_required", Assert.Throws<DomainException>(() => VinylRecord.Create(" ")).Code);
        Assert.Equal("compact_disc.disc_count_required", Assert.Throws<DomainException>(() => CompactDisc.Create(0)).Code);
        Assert.Equal("cassette_tape.type_required", Assert.Throws<DomainException>(() => CassetteTape.Create(" ")).Code);
        Assert.Equal("other_medium.name_required", Assert.Throws<DomainException>(() => OtherMedium.Create(" ")).Code);
    }

    [Fact]
    public void Audio_file_formats_are_a_closed_object_catalog()
    {
        Assert.Equal(AudioFileFormat.Ogg, AudioFileFormat.Ogg);
        Assert.NotEqual(AudioFileFormat.Flac, AudioFileFormat.Mp3);
    }

    [Fact]
    public void File_path_accepts_unix_and_windows_absolute_paths()
    {
        var unixPath = FilePath.FromAbsolutePath("/music/New Order/Blue Monday.flac");
        var windowsPath = FilePath.FromAbsolutePath(@"C:\music\New Order\Blue Monday.flac");

        Assert.Equal("/music/New Order/Blue Monday.flac", unixPath.Value);
        Assert.Equal(@"C:\music\New Order\Blue Monday.flac", windowsPath.Value);
    }

    [Fact]
    public void File_path_requires_absolute_paths()
    {
        DomainException exception = Assert.Throws<DomainException>(() => FilePath.FromAbsolutePath("relative/file.flac"));

        Assert.Equal("file_path.not_absolute", exception.Code);
    }

    [Fact]
    public void Digital_file_requires_path_and_format()
    {
        var path = FilePath.FromAbsolutePath("/music/New Order/Blue Monday.flac");

        var file = DigitalFile.Create(path, AudioFileFormat.Flac);

        Assert.False(file.ImportIdentity.HasValue);
    }

    [Fact]
    public void Digital_file_rejects_undefined_formats()
    {
        var path = FilePath.FromAbsolutePath("/music/New Order/Blue Monday.flac");

        Assert.Equal(
            "digital_file.format_invalid",
            Assert.Throws<DomainException>(() => DigitalFile.Create(path, (AudioFileFormat)999)).Code);
    }

    [Fact]
    public void Owned_item_can_store_condition_and_storage_location()
    {
        var collectionId = CollectionId.New();
        OwnedItem item = OwnedItem.Create(collectionId,
                OwnedItemId.New(),
                OwnedItemTarget.ForRelease(ReleaseId.New()),
                OwnershipStatus.Wanted,
                VinylRecord.Create("LP"))
            .WithStatus(OwnershipStatus.Owned)
            .WithCondition(ItemCondition.VeryGoodPlus)
            .WithStorageLocation(StorageLocation.FromName("Shelf A"));

        Assert.Equal(OwnershipStatus.Owned, item.Holding.Status);
        Assert.Equal(collectionId, item.CollectionId);
        Assert.Equal(
            ItemCondition.VeryGoodPlus,
            Assert.IsType<PresentOptionalValue<ItemCondition>>(item.Holding.Details.Condition).Value);
        Assert.Equal(
            "Shelf A",
            Assert.IsType<PresentOptionalValue<StorageLocation>>(item.Holding.Details.StorageLocation).Value.Name);
    }

    [Fact]
    public void Owned_item_conditions_are_a_closed_object_catalog()
    {
        Assert.Equal(ItemCondition.Mint, ItemCondition.Mint);
        Assert.NotEqual(ItemCondition.Mint, ItemCondition.Poor);
    }

    [Fact]
    public void Owned_item_rejects_undefined_statuses()
    {
        DomainException createException = Assert.Throws<DomainException>(() =>
            OwnedItem.Create(CollectionId.New(),
                OwnedItemId.New(),
                OwnedItemTarget.ForRelease(ReleaseId.New()),
                default,
                VinylRecord.Create("LP")));
        var item = OwnedItem.Create(CollectionId.New(),
            OwnedItemId.New(),
            OwnedItemTarget.ForRelease(ReleaseId.New()),
            OwnershipStatus.Owned,
            VinylRecord.Create("LP"));

        DomainException updateException = Assert.Throws<DomainException>(() => item.WithStatus((OwnershipStatus)999));

        Assert.Equal("owned_item.status_invalid", createException.Code);
        Assert.Equal("owned_item.status_invalid", updateException.Code);
    }

    [Fact]
    public void Owned_item_details_validate_required_values()
    {
        Assert.Equal("storage_location.name_required", Assert.Throws<DomainException>(() => StorageLocation.FromName(" ")).Code);
    }

    [Fact]
    public void Digital_file_can_carry_import_identity_for_deduplication()
    {
        var path = FilePath.FromAbsolutePath("/music/New Order/Blue Monday.flac");
        var identity = FileImportIdentity.Create(
            path,
            123_456,
            new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero),
            " ABCDEF ");
        var file = DigitalFile.Create(path, AudioFileFormat.Flac, identity);

        Assert.Equal(path, file.Path);
        FileImportIdentity actualIdentity = Assert.IsType<PresentOptionalValue<FileImportIdentity>>(file.ImportIdentity).Value;

        Assert.Equal(123_456, actualIdentity.SizeBytes);
        Assert.Equal("abcdef", Assert.IsType<PresentOptionalValue<string>>(actualIdentity.ContentHash).Value);
    }

    [Fact]
    public void File_import_identity_rejects_null_hash_values()
    {
        var path = FilePath.FromAbsolutePath("/music/New Order/Blue Monday.flac");

        _ = Assert.Throws<ArgumentNullException>(() =>
            FileImportIdentity.Create(
                path,
                123_456,
                DateTimeOffset.UnixEpoch,
                null!));
    }

    [Fact]
    public void File_import_identity_requires_matching_file_path_and_positive_size()
    {
        var path = FilePath.FromAbsolutePath("/music/New Order/Blue Monday.flac");
        var otherPath = FilePath.FromAbsolutePath("/music/New Order/Confusion.flac");
        var identity = FileImportIdentity.Create(
            otherPath,
            1,
            DateTimeOffset.UnixEpoch);

        Assert.Equal(
            "file_import_identity.size_required",
            Assert.Throws<DomainException>(() => FileImportIdentity.Create(path, 0, DateTimeOffset.UnixEpoch)).Code);
        Assert.Equal(
            "digital_file.import_identity_path_mismatch",
            Assert.Throws<DomainException>(() => DigitalFile.Create(path, AudioFileFormat.Flac, identity)).Code);
    }
}
