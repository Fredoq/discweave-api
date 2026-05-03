using Cratebase.Domain.Collection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class MusicCollectionConfiguration : IEntityTypeConfiguration<MusicCollection>
{
    public void Configure(EntityTypeBuilder<MusicCollection> builder)
    {
        _ = builder.ToTable("collections");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(collection => collection.Id)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(collection => collection.Id)
            .HasName("collection_id");

        _ = builder.Property(collection => collection.OwnerUserId)
            .HasColumnName("owner_user_id")
            .HasConversion(PersistenceValueConverters.UserId)
            .IsRequired();

        _ = builder.Property(collection => collection.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        _ = builder.Property(collection => collection.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        _ = builder.HasIndex(collection => collection.OwnerUserId)
            .IsUnique();
    }
}
