using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class RatingValueConfiguration : IEntityTypeConfiguration<RatingValue>
{
    public void Configure(EntityTypeBuilder<RatingValue> builder)
    {
        _ = builder.ToTable(
            "rating_values",
            table => table.HasCheckConstraint(
                "ck_rating_values_target_consistency",
                "(target_type = 'artist' AND target_artist_id IS NOT NULL AND target_release_id IS NULL AND target_track_id IS NULL AND target_label_id IS NULL) OR " +
                "(target_type = 'release' AND target_release_id IS NOT NULL AND target_artist_id IS NULL AND target_track_id IS NULL AND target_label_id IS NULL) OR " +
                "(target_type = 'track' AND target_track_id IS NOT NULL AND target_artist_id IS NULL AND target_release_id IS NULL AND target_label_id IS NULL) OR " +
                "(target_type = 'label' AND target_label_id IS NOT NULL AND target_artist_id IS NULL AND target_release_id IS NULL AND target_track_id IS NULL)"));

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(value => value.Id)
            .HasColumnName("rating_value_id")
            .HasConversion(PersistenceValueConverters.RatingValueId)
            .ValueGeneratedNever();

        _ = builder.Property(value => value.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.Property(value => value.CriterionId)
            .HasColumnName("criterion_id")
            .HasConversion(PersistenceValueConverters.RatingCriterionId)
            .ValueGeneratedNever();

        _ = builder.Property(value => value.Rating)
            .HasColumnName("rating")
            .HasConversion(PersistenceValueConverters.RatingValue)
            .IsRequired();

        _ = builder.HasAlternateKey(value => value.Id)
            .HasName("rating_value_id");

        _ = builder.Ignore(value => value.Target);

        _ = builder.Property<string>("_targetType")
            .HasColumnName("target_type")
            .HasMaxLength(32)
            .IsRequired();

        _ = builder.Property<ArtistId?>("_targetArtistId")
            .HasColumnName("target_artist_id")
            .HasConversion(PersistenceValueConverters.NullableArtistId);

        _ = builder.Property<ReleaseId?>("_targetReleaseId")
            .HasColumnName("target_release_id")
            .HasConversion(PersistenceValueConverters.NullableReleaseId);

        _ = builder.Property<TrackId?>("_targetTrackId")
            .HasColumnName("target_track_id")
            .HasConversion(PersistenceValueConverters.NullableTrackId);

        _ = builder.Property<LabelId?>("_targetLabelId")
            .HasColumnName("target_label_id")
            .HasConversion(PersistenceValueConverters.NullableLabelId);

        _ = builder.HasOne<RatingCriterion>()
            .WithMany()
            .HasForeignKey(nameof(RatingValue.CollectionId), nameof(RatingValue.CriterionId))
            .HasPrincipalKey(nameof(RatingCriterion.CollectionId), nameof(RatingCriterion.Id))
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(nameof(RatingValue.CollectionId), "_targetArtistId")
            .HasPrincipalKey(nameof(Artist.CollectionId), nameof(Artist.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Release>()
            .WithMany()
            .HasForeignKey(nameof(RatingValue.CollectionId), "_targetReleaseId")
            .HasPrincipalKey(nameof(Release.CollectionId), nameof(Release.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Track>()
            .WithMany()
            .HasForeignKey(nameof(RatingValue.CollectionId), "_targetTrackId")
            .HasPrincipalKey(nameof(Track.CollectionId), nameof(Track.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Label>()
            .WithMany()
            .HasForeignKey(nameof(RatingValue.CollectionId), "_targetLabelId")
            .HasPrincipalKey(nameof(Label.CollectionId), nameof(Label.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(value => value.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasIndex(value => value.CollectionId);
        _ = builder.HasIndex(value => value.CriterionId);
        _ = builder.HasIndex("_targetArtistId");
        _ = builder.HasIndex("_targetReleaseId");
        _ = builder.HasIndex("_targetTrackId");
        _ = builder.HasIndex("_targetLabelId");
        _ = builder.HasIndex(nameof(RatingValue.CollectionId), nameof(RatingValue.CriterionId), "_targetType", "_targetArtistId")
            .IsUnique()
            .HasFilter("target_type = 'artist'");
        _ = builder.HasIndex(nameof(RatingValue.CollectionId), nameof(RatingValue.CriterionId), "_targetType", "_targetReleaseId")
            .IsUnique()
            .HasFilter("target_type = 'release'");
        _ = builder.HasIndex(nameof(RatingValue.CollectionId), nameof(RatingValue.CriterionId), "_targetType", "_targetTrackId")
            .IsUnique()
            .HasFilter("target_type = 'track'");
        _ = builder.HasIndex(nameof(RatingValue.CollectionId), nameof(RatingValue.CriterionId), "_targetType", "_targetLabelId")
            .IsUnique()
            .HasFilter("target_type = 'label'");
    }
}
