using Cratebase.Domain.Collection;
using Cratebase.Infrastructure.Persistence.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class SearchDocumentConfiguration : IEntityTypeConfiguration<SearchDocument>
{
    public void Configure(EntityTypeBuilder<SearchDocument> builder)
    {
        _ = builder.ToTable("search_documents");

        _ = builder.Property(document => document.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey(document => document.Id);

        _ = builder.Property(document => document.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(document => document.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(32)
            .IsRequired();

        _ = builder.Property(document => document.EntityId)
            .HasColumnName("entity_id")
            .ValueGeneratedNever();

        _ = builder.Property(document => document.Title)
            .HasColumnName("title")
            .HasMaxLength(512)
            .IsRequired();

        _ = builder.Property(document => document.Subtitle)
            .HasColumnName("subtitle")
            .HasMaxLength(512);

        _ = builder.Property(document => document.Summary)
            .HasColumnName("summary")
            .HasMaxLength(2048);

        _ = builder.Property(document => document.SearchText)
            .HasColumnName("search_text")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.MatchedFields)
            .HasColumnName("matched_fields")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.Snippets)
            .HasColumnName("snippets")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.RoleFacet)
            .HasColumnName("role_facet")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.MediaFacet)
            .HasColumnName("media_facet")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.StatusFacet)
            .HasColumnName("status_facet")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.TagFacet)
            .HasColumnName("tag_facet")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.LabelId)
            .HasColumnName("label_id");

        _ = builder.Property(document => document.LabelIdFacet)
            .HasColumnName("label_id_facet")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.Property(document => document.CollectorSignalFacet)
            .HasColumnName("collector_signal_facet")
            .HasColumnType("text")
            .IsRequired();

        _ = builder.HasIndex(document => new { document.CollectionId, document.EntityType, document.EntityId })
            .IsUnique()
            .HasDatabaseName("ix_search_documents_collection_entity");
        _ = builder.HasIndex(document => new { document.CollectionId, document.EntityType })
            .HasDatabaseName("ix_search_documents_collection_entity_type");
        _ = builder.HasIndex(document => new { document.CollectionId, document.LabelId })
            .HasDatabaseName("ix_search_documents_collection_label_id");

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(document => document.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
