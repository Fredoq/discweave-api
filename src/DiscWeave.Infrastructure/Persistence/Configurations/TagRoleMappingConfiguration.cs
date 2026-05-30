using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class TagRoleMappingConfiguration : IEntityTypeConfiguration<TagRoleMapping>
{
    public void Configure(EntityTypeBuilder<TagRoleMapping> builder)
    {
        _ = builder.ToTable("tag_role_mappings");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(mapping => mapping.Id)
            .HasColumnName("tag_role_mapping_id")
            .HasConversion(PersistenceValueConverters.TagRoleMappingId)
            .ValueGeneratedNever();

        _ = builder.Property(mapping => mapping.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(mapping => mapping.CreditRoleCode)
            .HasColumnName("credit_role_code")
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Property(mapping => mapping.TagField)
            .HasColumnName("tag_field")
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Property(mapping => mapping.SortOrder).HasColumnName("sort_order");
        _ = builder.Property(mapping => mapping.IsActive).HasColumnName("is_active");
        _ = builder.Property(mapping => mapping.IsBuiltin).HasColumnName("is_builtin");

        _ = builder.HasAlternateKey(mapping => new { mapping.CollectionId, mapping.Id })
            .HasName("ak_tag_role_mappings_collection_mapping_id");
        _ = builder.HasIndex(mapping => new { mapping.CollectionId, mapping.SortOrder });
        _ = builder.HasIndex(mapping => new { mapping.CollectionId, mapping.CreditRoleCode })
            .IsUnique()
            .HasDatabaseName("ux_tag_role_mappings_collection_role");

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(mapping => mapping.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
