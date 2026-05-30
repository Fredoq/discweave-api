using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class NamingProfileConfiguration : IEntityTypeConfiguration<NamingProfile>
{
    public void Configure(EntityTypeBuilder<NamingProfile> builder)
    {
        _ = builder.ToTable("naming_profiles");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(profile => profile.Id)
            .HasColumnName("naming_profile_id")
            .HasConversion(PersistenceValueConverters.NamingProfileId)
            .ValueGeneratedNever();

        _ = builder.Property(profile => profile.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(profile => profile.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        _ = builder.Property(profile => profile.ReleaseFolderTemplate)
            .HasColumnName("release_folder_template")
            .HasMaxLength(1024)
            .IsRequired();

        _ = builder.Property(profile => profile.TrackFileTemplate)
            .HasColumnName("track_file_template")
            .HasMaxLength(1024)
            .IsRequired();

        _ = builder.Property(profile => profile.TrackFileWithArtistTemplate)
            .HasColumnName("track_file_with_artist_template")
            .HasMaxLength(1024)
            .IsRequired();

        _ = builder.Property(profile => profile.SortOrder).HasColumnName("sort_order");
        _ = builder.Property(profile => profile.IsDefault).HasColumnName("is_default");
        _ = builder.Property(profile => profile.IsActive).HasColumnName("is_active");
        _ = builder.Property(profile => profile.IsBuiltin).HasColumnName("is_builtin");

        _ = builder.HasAlternateKey(profile => new { profile.CollectionId, profile.Id })
            .HasName("ak_naming_profiles_collection_profile_id");
        _ = builder.HasIndex(profile => new { profile.CollectionId, profile.SortOrder });
        _ = builder.HasIndex(profile => new { profile.CollectionId, profile.Name })
            .IsUnique()
            .HasDatabaseName("ux_naming_profiles_collection_name");
        _ = builder.HasIndex(profile => profile.CollectionId)
            .HasFilter("is_default = TRUE")
            .IsUnique()
            .HasDatabaseName("ux_naming_profiles_collection_default");

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(profile => profile.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
