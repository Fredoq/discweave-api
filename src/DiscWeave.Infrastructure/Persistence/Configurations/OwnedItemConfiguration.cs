using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class OwnedItemConfiguration : IEntityTypeConfiguration<OwnedItem>
{
    private const string TargetTypeProperty = "_targetType";
    private const string TargetReleaseIdProperty = "_targetReleaseId";
    private const string TargetTrackIdProperty = "_targetTrackId";
    private const string StatusProperty = "_status";
    private const string MediumTypeProperty = "_mediumType";
    private const string ConditionProperty = "_condition";
    private const string StorageLocationProperty = "_storageLocation";

    public void Configure(EntityTypeBuilder<OwnedItem> builder)
    {
        _ = builder.ToTable(
            "owned_items",
            table => table.HasCheckConstraint(
                "ck_owned_items_target_consistency",
                "(target_type = 'release' AND target_release_id IS NOT NULL AND target_track_id IS NULL) OR " +
                "(target_type = 'track' AND target_track_id IS NOT NULL AND target_release_id IS NULL)"));

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(item => item.Id)
            .HasColumnName("owned_item_id")
            .HasConversion(PersistenceValueConverters.OwnedItemId)
            .ValueGeneratedNever();

        _ = builder.Property(item => item.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(item => new { item.CollectionId, item.Id })
            .HasName("ak_owned_items_collection_owned_item_id");

        _ = builder.Ignore(item => item.Target);
        _ = builder.Ignore(item => item.Holding);

        _ = builder.Property<string>(TargetTypeProperty)
            .HasColumnName("target_type")
            .HasMaxLength(32)
            .IsRequired();

        _ = builder.Property<ReleaseId?>(TargetReleaseIdProperty)
            .HasColumnName("target_release_id")
            .HasConversion(PersistenceValueConverters.NullableReleaseId);

        _ = builder.Property<TrackId?>(TargetTrackIdProperty)
            .HasColumnName("target_track_id")
            .HasConversion(PersistenceValueConverters.NullableTrackId);

        _ = builder.Property<OwnershipStatus>(StatusProperty)
            .HasColumnName("ownership_status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Property<string>(MediumTypeProperty)
            .HasColumnName("medium_type")
            .HasMaxLength(32)
            .IsRequired();

        _ = builder.Property<string>("_digitalFilePath")
            .HasColumnName("digital_file_path")
            .HasMaxLength(4096);

        _ = builder.Property<AudioFileFormat?>("_digitalFileFormat")
            .HasColumnName("digital_file_format")
            .HasConversion<string>()
            .HasMaxLength(64);

        _ = builder.Property<string>("_importIdentityPath")
            .HasColumnName("import_identity_path")
            .HasMaxLength(4096);

        _ = builder.Property<long?>("_importIdentitySizeBytes")
            .HasColumnName("import_identity_size_bytes");

        _ = builder.Property<DateTimeOffset?>("_importIdentityLastModifiedAt")
            .HasColumnName("import_identity_last_modified_at");

        _ = builder.Property<string>("_importIdentityContentHash")
            .HasColumnName("import_identity_content_hash")
            .HasMaxLength(256);

        _ = builder.Property<string>("_vinylFormatDescription")
            .HasColumnName("vinyl_format_description")
            .HasMaxLength(256);

        _ = builder.Property<int?>("_compactDiscCount")
            .HasColumnName("compact_disc_count");

        _ = builder.Property<string>("_cassetteTapeType")
            .HasColumnName("cassette_tape_type")
            .HasMaxLength(256);

        _ = builder.Property<string>("_otherMediumName")
            .HasColumnName("other_medium_name")
            .HasMaxLength(256);

        _ = builder.Property<ItemCondition?>(ConditionProperty)
            .HasColumnName("condition")
            .HasConversion<string>()
            .HasMaxLength(64);

        _ = builder.Property<string>(StorageLocationProperty)
            .HasColumnName("storage_location")
            .HasMaxLength(512);

        _ = builder.HasOne<Release>()
            .WithMany()
            .HasForeignKey(nameof(OwnedItem.CollectionId), TargetReleaseIdProperty)
            .HasPrincipalKey(nameof(Release.CollectionId), nameof(Release.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Track>()
            .WithMany()
            .HasForeignKey(nameof(OwnedItem.CollectionId), TargetTrackIdProperty)
            .HasPrincipalKey(nameof(Track.CollectionId), nameof(Track.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasIndex(TargetReleaseIdProperty);
        _ = builder.HasIndex(TargetTrackIdProperty);
        _ = builder.HasIndex(item => item.CollectionId);
        _ = builder.HasIndex(MediumTypeProperty);
        _ = builder.HasIndex(StatusProperty);
        _ = builder.HasIndex(nameof(OwnedItem.CollectionId), ConditionProperty)
            .HasDatabaseName("ix_owned_items_collection_condition");
        _ = builder.HasIndex(nameof(OwnedItem.CollectionId), StorageLocationProperty)
            .HasDatabaseName("ix_owned_items_collection_storage_location");
        _ = builder.HasIndex(nameof(OwnedItem.CollectionId), TargetTypeProperty, TargetReleaseIdProperty, MediumTypeProperty)
            .HasDatabaseName("ix_owned_items_inventory_release_medium");
        _ = builder.HasIndex(nameof(OwnedItem.CollectionId), TargetTypeProperty, TargetTrackIdProperty, MediumTypeProperty)
            .HasDatabaseName("ix_owned_items_inventory_track_medium");
        _ = builder.HasIndex(nameof(OwnedItem.CollectionId), TargetTypeProperty, TargetReleaseIdProperty, StatusProperty)
            .HasDatabaseName("ix_owned_items_inventory_release_status");
        _ = builder.HasIndex(nameof(OwnedItem.CollectionId), TargetTypeProperty, TargetTrackIdProperty, StatusProperty)
            .HasDatabaseName("ix_owned_items_inventory_track_status");

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(item => item.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(
                nameof(OwnedItem.CollectionId),
                "_importIdentityPath",
                "_importIdentitySizeBytes",
                "_importIdentityLastModifiedAt",
                "_importIdentityContentHash")
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("import_identity_path IS NOT NULL")
            .HasDatabaseName("ix_owned_items_import_identity");
    }
}
