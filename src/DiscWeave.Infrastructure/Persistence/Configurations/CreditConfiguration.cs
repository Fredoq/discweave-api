using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class CreditConfiguration : IEntityTypeConfiguration<Credit>
{
    public void Configure(EntityTypeBuilder<Credit> builder)
    {
        _ = builder.ToTable(
            "credits",
            table => table.HasCheckConstraint(
                "ck_credits_target_consistency",
                "(target_type = 'release' AND target_release_id IS NOT NULL AND target_track_id IS NULL) OR " +
                "(target_type = 'track' AND target_track_id IS NOT NULL AND target_release_id IS NULL)"));

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(credit => credit.Id)
            .HasColumnName("credit_id")
            .HasConversion(PersistenceValueConverters.CreditId)
            .ValueGeneratedNever();

        _ = builder.Property(credit => credit.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(credit => new { credit.CollectionId, credit.Id })
            .HasName("ak_credits_collection_credit_id");

        _ = builder.Ignore(credit => credit.Target);

        _ = builder.Ignore(credit => credit.Contributor);

        _ = builder.Property<ArtistId>("_contributorArtistId")
            .HasColumnName("contributor_artist_id")
            .HasConversion(PersistenceValueConverters.ArtistId)
            .IsRequired();

        _ = builder.Property<string>("_contributorName")
            .HasColumnName("contributor_name")
            .HasMaxLength(1024)
            .IsRequired();

        _ = builder.Property<string>("_targetType")
            .HasColumnName("target_type")
            .HasMaxLength(32)
            .IsRequired();

        _ = builder.Property<ReleaseId?>("_targetReleaseId")
            .HasColumnName("target_release_id")
            .HasConversion(PersistenceValueConverters.NullableReleaseId);

        _ = builder.Property<TrackId?>("_targetTrackId")
            .HasColumnName("target_track_id")
            .HasConversion(PersistenceValueConverters.NullableTrackId);

        _ = builder.Property(credit => credit.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(nameof(Credit.CollectionId), "_contributorArtistId")
            .HasPrincipalKey(nameof(Artist.CollectionId), nameof(Artist.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Release>()
            .WithMany()
            .HasForeignKey(nameof(Credit.CollectionId), "_targetReleaseId")
            .HasPrincipalKey(nameof(Release.CollectionId), nameof(Release.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Track>()
            .WithMany()
            .HasForeignKey(nameof(Credit.CollectionId), "_targetTrackId")
            .HasPrincipalKey(nameof(Track.CollectionId), nameof(Track.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasIndex("_contributorArtistId");
        _ = builder.HasIndex("_targetReleaseId");
        _ = builder.HasIndex("_targetTrackId");
        _ = builder.HasIndex(credit => credit.CollectionId);
        _ = builder.HasIndex(credit => credit.Role);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(credit => credit.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
