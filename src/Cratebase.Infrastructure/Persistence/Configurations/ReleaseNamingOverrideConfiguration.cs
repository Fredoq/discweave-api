using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseNamingOverrideConfiguration : IEntityTypeConfiguration<ReleaseNamingOverride>
{
    public void Configure(EntityTypeBuilder<ReleaseNamingOverride> builder)
    {
        _ = builder.ToTable("release_naming_overrides");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(overrideEntry => overrideEntry.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(overrideEntry => overrideEntry.ReleaseId)
            .HasColumnName("release_id")
            .HasConversion(PersistenceValueConverters.ReleaseId)
            .ValueGeneratedNever();

        _ = builder.Ignore(overrideEntry => overrideEntry.Id);

        _ = builder.Property<NamingProfileId?>("_namingProfileId")
            .HasColumnName("naming_profile_id")
            .HasConversion(PersistenceValueConverters.NullableNamingProfileId);

        _ = builder.Property<string?>("_releaseFolderTemplate")
            .HasColumnName("release_folder_template")
            .HasMaxLength(1024);

        _ = builder.Property<string?>("_trackFileTemplate")
            .HasColumnName("track_file_template")
            .HasMaxLength(1024);

        _ = builder.Property<string?>("_trackFileWithArtistTemplate")
            .HasColumnName("track_file_with_artist_template")
            .HasMaxLength(1024);

        _ = builder.Property<string?>("_source")
            .HasColumnName("source")
            .HasMaxLength(128);

        _ = builder.HasAlternateKey(overrideEntry => new { overrideEntry.CollectionId, overrideEntry.ReleaseId })
            .HasName("ak_release_naming_overrides_collection_release_id");

        _ = builder.Ignore(overrideEntry => overrideEntry.NamingProfileId);
        _ = builder.Ignore(overrideEntry => overrideEntry.ReleaseFolderTemplate);
        _ = builder.Ignore(overrideEntry => overrideEntry.TrackFileTemplate);
        _ = builder.Ignore(overrideEntry => overrideEntry.TrackFileWithArtistTemplate);
        _ = builder.Ignore(overrideEntry => overrideEntry.Source);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(overrideEntry => overrideEntry.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasOne<Release>()
            .WithMany()
            .HasForeignKey(overrideEntry => new { overrideEntry.CollectionId, overrideEntry.ReleaseId })
            .HasPrincipalKey(release => new { release.CollectionId, release.Id })
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasOne<NamingProfile>()
            .WithMany()
            .HasForeignKey(
                nameof(ReleaseNamingOverride.CollectionId),
                "_namingProfileId")
            .HasPrincipalKey(nameof(NamingProfile.CollectionId), nameof(NamingProfile.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasIndex("_namingProfileId");
    }
}
