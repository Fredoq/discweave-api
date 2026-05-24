using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        _ = builder.ToTable("labels");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(label => label.Id)
            .HasColumnName("label_id")
            .HasConversion(PersistenceValueConverters.LabelId)
            .ValueGeneratedNever();

        _ = builder.Property(label => label.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(label => new { label.CollectionId, label.Id })
            .HasName("ak_labels_collection_label_id");

        _ = builder.Property(label => label.Name)
            .HasColumnName("name")
            .HasMaxLength(512)
            .IsRequired();

        _ = builder.HasIndex(label => label.CollectionId);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(label => label.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
