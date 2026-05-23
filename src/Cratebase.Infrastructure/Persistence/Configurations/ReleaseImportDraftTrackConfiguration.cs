using Cratebase.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseImportDraftTrackConfiguration : IEntityTypeConfiguration<ReleaseImportDraftTrack>
{
    public void Configure(EntityTypeBuilder<ReleaseImportDraftTrack> builder)
    {
        _ = builder.ToTable("release_import_draft_tracks");

        _ = builder.Property<long>("id").HasColumnName("id").ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(track => track.Id).HasColumnName("release_import_draft_track_id").HasConversion(PersistenceValueConverters.ReleaseImportDraftTrackId).ValueGeneratedNever();
        _ = builder.Property(track => track.CollectionId).HasColumnName("collection_id").HasConversion(PersistenceValueConverters.CollectionId).ValueGeneratedNever();
        _ = builder.Property(track => track.DraftId).HasColumnName("release_import_draft_id").HasConversion(PersistenceValueConverters.ReleaseImportDraftId).ValueGeneratedNever();
        _ = builder.Property(track => track.FilePath).HasColumnName("file_path").HasMaxLength(4096).IsRequired();
        _ = builder.Property(track => track.RelativePath).HasColumnName("relative_path").HasMaxLength(4096).IsRequired();
        _ = builder.Property(track => track.Format).HasColumnName("audio_file_format").HasConversion<string>().HasMaxLength(64).IsRequired();
        _ = builder.Property(track => track.SizeBytes).HasColumnName("size_bytes");
        _ = builder.Property(track => track.LastModifiedAt).HasColumnName("last_modified_at");
        _ = builder.Property(track => track.ContentHash).HasColumnName("content_hash").HasMaxLength(256);
        _ = builder.Property(track => track.Duration).HasColumnName("duration");
        _ = builder.Property(track => track.Position).HasColumnName("position_number");
        _ = builder.Property(track => track.Title).HasColumnName("title").HasMaxLength(1024).IsRequired();
        _ = builder.Property(track => track.IsSkipped).HasColumnName("is_skipped");
        _ = builder.Property(track => track.SelectedTrackId).HasColumnName("selected_track_id").HasConversion(PersistenceValueConverters.NullableTrackId);
        _ = builder.Property<string>("_artistCreditsJson").HasColumnName("artist_credits_json").HasMaxLength(8192);
        _ = builder.Property<string>("_artistNamesJson").HasColumnName("artist_names_json").HasMaxLength(8192);
        _ = builder.Property<string>("_selectedArtistIdsJson").HasColumnName("selected_artist_ids_json").HasMaxLength(8192);
        _ = builder.Property<string>("_issuesJson").HasColumnName("issues_json").HasMaxLength(8192);

        _ = builder.Ignore(track => track.ArtistCredits);
        _ = builder.Ignore(track => track.ArtistNames);
        _ = builder.Ignore(track => track.SelectedArtistIds);
        _ = builder.Ignore(track => track.Issues);

        _ = builder.HasAlternateKey(track => track.Id).HasName("release_import_draft_track_id");
        _ = builder.HasIndex(track => track.CollectionId);
        _ = builder.HasIndex(track => track.DraftId);

        _ = builder.HasOne<ReleaseImportDraft>()
            .WithMany()
            .HasForeignKey(track => new { track.CollectionId, track.DraftId })
            .HasPrincipalKey(draft => new { draft.CollectionId, draft.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
