using Cratebase.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseImportDraftConfiguration : IEntityTypeConfiguration<ReleaseImportDraft>
{
    public void Configure(EntityTypeBuilder<ReleaseImportDraft> builder)
    {
        _ = builder.ToTable("release_import_drafts");

        _ = builder.Property<long>("id").HasColumnName("id").ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(draft => draft.Id).HasColumnName("release_import_draft_id").HasConversion(PersistenceValueConverters.ReleaseImportDraftId).ValueGeneratedNever();
        _ = builder.Property(draft => draft.CollectionId).HasColumnName("collection_id").HasConversion(PersistenceValueConverters.CollectionId).ValueGeneratedNever();
        _ = builder.Property(draft => draft.SessionId).HasColumnName("release_import_session_id").HasConversion(PersistenceValueConverters.ReleaseImportSessionId).ValueGeneratedNever();
        _ = builder.Property(draft => draft.SourcePath).HasColumnName("source_path").HasMaxLength(4096).IsRequired();
        _ = builder.Property(draft => draft.RelativePath).HasColumnName("relative_path").HasMaxLength(4096).IsRequired();
        _ = builder.Property(draft => draft.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(64).IsRequired();
        _ = builder.Property(draft => draft.Title).HasColumnName("title").HasMaxLength(1024).IsRequired();
        _ = builder.Property(draft => draft.Type).HasColumnName("release_type").HasMaxLength(64).IsRequired();
        _ = builder.Property(draft => draft.CatalogNumber).HasColumnName("catalog_number").HasMaxLength(256);
        _ = builder.Property(draft => draft.LabelName).HasColumnName("label_name").HasMaxLength(512);
        _ = builder.Property(draft => draft.ReleaseDate).HasColumnName("release_date");
        _ = builder.Property(draft => draft.Year).HasColumnName("release_year");
        _ = builder.Property(draft => draft.IsVariousArtists).HasColumnName("is_various_artists");
        _ = builder.Property(draft => draft.NotOnLabel).HasColumnName("is_not_on_label");
        _ = builder.Property(draft => draft.CoverPath).HasColumnName("cover_path").HasMaxLength(4096);
        _ = builder.Property(draft => draft.CoverFileName).HasColumnName("cover_file_name").HasMaxLength(512);
        _ = builder.Property(draft => draft.CoverExtension).HasColumnName("cover_extension").HasMaxLength(32);
        _ = builder.Property(draft => draft.CoverContentType).HasColumnName("cover_content_type").HasMaxLength(128);
        _ = builder.Property(draft => draft.CoverSizeBytes).HasColumnName("cover_size_bytes");
        _ = builder.Property(draft => draft.CoverContent).HasColumnName("cover_content");
        _ = builder.Property(draft => draft.ConfirmedReleaseId).HasColumnName("confirmed_release_id").HasConversion(PersistenceValueConverters.NullableReleaseId);
        _ = builder.Property<string>("_artistNamesJson").HasColumnName("artist_names_json").HasMaxLength(8192);
        _ = builder.Property<string>("_artistCreditsJson").HasColumnName("artist_credits_json").HasMaxLength(16384);
        _ = builder.Property<string>("_labelsJson").HasColumnName("labels_json").HasMaxLength(8192);
        _ = builder.Property<string>("_selectedArtistIdsJson").HasColumnName("selected_artist_ids_json").HasMaxLength(8192);
        _ = builder.Property<string>("_genresJson").HasColumnName("genres_json").HasMaxLength(8192);
        _ = builder.Property<string>("_tagsJson").HasColumnName("tags_json").HasMaxLength(8192);
        _ = builder.Property<string>("_issuesJson").HasColumnName("issues_json").HasMaxLength(8192);

        _ = builder.Ignore(draft => draft.ArtistNames);
        _ = builder.Ignore(draft => draft.ArtistCredits);
        _ = builder.Ignore(draft => draft.Labels);
        _ = builder.Ignore(draft => draft.SelectedArtistIds);
        _ = builder.Ignore(draft => draft.Genres);
        _ = builder.Ignore(draft => draft.Tags);
        _ = builder.Ignore(draft => draft.Issues);

        _ = builder.HasAlternateKey(draft => draft.Id).HasName("release_import_draft_id");
        _ = builder.HasAlternateKey(draft => new { draft.CollectionId, draft.Id })
            .HasName("ak_release_import_drafts_collection_draft_id");
        _ = builder.HasIndex(draft => draft.CollectionId);
        _ = builder.HasIndex(draft => draft.SessionId);

        _ = builder.HasOne<ReleaseImportSession>()
            .WithMany()
            .HasForeignKey(draft => new { draft.CollectionId, draft.SessionId })
            .HasPrincipalKey(session => new { session.CollectionId, session.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
