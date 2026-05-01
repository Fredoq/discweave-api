using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class OwnedItemConfiguration : IEntityTypeConfiguration<OwnedItem>
{
    public void Configure(EntityTypeBuilder<OwnedItem> builder)
    {
        _ = builder.ToTable("owned_items");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(item => item.Id)
            .HasColumnName("owned_item_id")
            .HasConversion(PersistenceValueConverters.OwnedItemId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(item => item.Id)
            .HasName("owned_item_id");

        _ = builder.Ignore(item => item.Target);
        _ = builder.Ignore(item => item.Holding);

        _ = builder.Property<string>("_targetType")
            .HasColumnName("target_type")
            .HasMaxLength(32)
            .IsRequired();

        _ = builder.Property<ReleaseId?>("_targetReleaseId")
            .HasColumnName("target_release_id")
            .HasConversion(PersistenceValueConverters.NullableReleaseId);

        _ = builder.Property<TrackId?>("_targetTrackId")
            .HasColumnName("target_track_id")
            .HasConversion(PersistenceValueConverters.NullableTrackId);

        _ = builder.Property<OwnershipStatus>("_status")
            .HasColumnName("ownership_status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Property<string>("_mediumType")
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

        _ = builder.Property<ItemCondition?>("_condition")
            .HasColumnName("condition")
            .HasConversion<string>()
            .HasMaxLength(64);

        _ = builder.Property<string>("_storageLocation")
            .HasColumnName("storage_location")
            .HasMaxLength(512);

        _ = builder.HasOne<Release>()
            .WithMany()
            .HasForeignKey("_targetReleaseId")
            .HasPrincipalKey(release => release.Id)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Track>()
            .WithMany()
            .HasForeignKey("_targetTrackId")
            .HasPrincipalKey(track => track.Id)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasIndex("_targetReleaseId");
        _ = builder.HasIndex("_targetTrackId");
        _ = builder.HasIndex("_mediumType");
        _ = builder.HasIndex("_status");
    }
}
