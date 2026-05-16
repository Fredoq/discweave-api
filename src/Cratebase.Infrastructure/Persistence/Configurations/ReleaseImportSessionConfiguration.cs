using Cratebase.Domain.Collection;
using Cratebase.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseImportSessionConfiguration : IEntityTypeConfiguration<ReleaseImportSession>
{
    public void Configure(EntityTypeBuilder<ReleaseImportSession> builder)
    {
        _ = builder.ToTable("release_import_sessions");

        _ = builder.Property<long>("id").HasColumnName("id").ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(session => session.Id)
            .HasColumnName("release_import_session_id")
            .HasConversion(PersistenceValueConverters.ReleaseImportSessionId)
            .ValueGeneratedNever();

        _ = builder.Property(session => session.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(session => session.SourceRoot).HasColumnName("source_root").HasMaxLength(4096).IsRequired();
        _ = builder.Property(session => session.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(64).IsRequired();
        _ = builder.Property(session => session.DraftCount).HasColumnName("draft_count");
        _ = builder.Property(session => session.TrackCount).HasColumnName("track_count");
        _ = builder.Property(session => session.IgnoredFileCount).HasColumnName("ignored_file_count");
        _ = builder.Property(session => session.CreatedAt).HasColumnName("created_at");
        _ = builder.Property(session => session.UpdatedAt).HasColumnName("updated_at");

        _ = builder.HasAlternateKey(session => session.Id).HasName("release_import_session_id");
        _ = builder.HasIndex(session => session.CollectionId);
        _ = builder.HasIndex(session => new { session.CollectionId, session.CreatedAt });

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(session => session.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
