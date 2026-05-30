using DiscWeave.Domain.Imports;
using DiscWeave.Domain.Collection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class ImportPatternConfiguration : IEntityTypeConfiguration<ImportPattern>
{
    public void Configure(EntityTypeBuilder<ImportPattern> builder)
    {
        _ = builder.ToTable("import_patterns");

        _ = builder.Property<long>("id").HasColumnName("id").ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(pattern => pattern.Id)
            .HasColumnName("import_pattern_id")
            .HasConversion(PersistenceValueConverters.ImportPatternId)
            .ValueGeneratedNever();

        _ = builder.Property(pattern => pattern.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(pattern => pattern.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Property(pattern => pattern.Template)
            .HasColumnName("template")
            .HasMaxLength(1024)
            .IsRequired();

        _ = builder.Property(pattern => pattern.SortOrder).HasColumnName("sort_order");
        _ = builder.Property(pattern => pattern.IsActive).HasColumnName("is_active");
        _ = builder.Property(pattern => pattern.IsBuiltin).HasColumnName("is_builtin");

        _ = builder.HasAlternateKey(pattern => new { pattern.CollectionId, pattern.Id }).HasName("ak_import_patterns_collection_pattern_id");
        _ = builder.HasIndex(pattern => new { pattern.CollectionId, pattern.Kind, pattern.SortOrder });
        _ = builder.HasIndex(pattern => new { pattern.CollectionId, pattern.Kind, pattern.Template, pattern.IsBuiltin })
            .IsUnique()
            .HasDatabaseName("ux_import_patterns_collection_kind_template_builtin");

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(pattern => pattern.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
