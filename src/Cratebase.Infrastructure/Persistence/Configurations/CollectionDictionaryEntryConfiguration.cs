using Cratebase.Domain.Collection;
using Cratebase.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class CollectionDictionaryEntryConfiguration : IEntityTypeConfiguration<CollectionDictionaryEntry>
{
    public void Configure(EntityTypeBuilder<CollectionDictionaryEntry> builder)
    {
        _ = builder.ToTable("collection_dictionary_entries");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(entry => entry.Id)
            .HasColumnName("dictionary_entry_id")
            .HasConversion(PersistenceValueConverters.CollectionDictionaryEntryId)
            .ValueGeneratedNever();

        _ = builder.Property(entry => entry.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(entry => entry.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Property(entry => entry.Code)
            .HasColumnName("code")
            .HasMaxLength(128)
            .IsRequired();

        _ = builder.Property(entry => entry.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        _ = builder.Property(entry => entry.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        _ = builder.Property(entry => entry.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        _ = builder.Property(entry => entry.IsBuiltin)
            .HasColumnName("is_builtin")
            .IsRequired();

        _ = builder.Property<string?>("_mediaProfile")
            .HasColumnName("media_profile")
            .HasMaxLength(32);

        _ = builder.Ignore(entry => entry.IsProtected);
        _ = builder.Ignore(entry => entry.MediaProfile);

        _ = builder.HasAlternateKey(entry => new { entry.CollectionId, entry.Id })
            .HasName("ak_collection_dictionary_entries_collection_entry_id");

        _ = builder.HasIndex(entry => new { entry.CollectionId, entry.Kind, entry.Code })
            .IsUnique()
            .HasDatabaseName("ix_collection_dictionary_entries_collection_kind_code");

        _ = builder.HasIndex(entry => entry.CollectionId);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(entry => entry.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
