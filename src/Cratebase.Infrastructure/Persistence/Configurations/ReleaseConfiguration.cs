using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    private const string CollectionIdProperty = nameof(Release.CollectionId);
    private const string ReleaseIdColumn = "release_id";

    public void Configure(EntityTypeBuilder<Release> builder)
    {
        _ = builder.ToTable("releases");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(release => release.Id)
            .HasColumnName(ReleaseIdColumn)
            .HasConversion(PersistenceValueConverters.ReleaseId)
            .ValueGeneratedNever();

        _ = builder.Property(release => release.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(release => release.Id)
            .HasName(ReleaseIdColumn);

        _ = builder.HasAlternateKey(release => new { release.CollectionId, release.Id })
            .HasName("ak_releases_collection_release_id");

        _ = builder.Ignore(release => release.DisplayName);
        _ = builder.Ignore(release => release.Cataloging);

        ConfigureSummary(builder);
        ConfigureTracklist(builder);
        ConfigureCataloging(builder);

        _ = builder.HasIndex(release => release.CollectionId);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(release => release.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureSummary(EntityTypeBuilder<Release> builder)
    {
        _ = builder.ComplexProperty(release => release.Summary, summary =>
        {
            _ = summary.Property(value => value.Title)
                .HasColumnName("title")
                .HasMaxLength(1024)
                .IsRequired();

            ComplexTypePropertyBuilder<IOptionalValue<Rating>> ratingProperty = summary.Property(value => value.Rating)
                .HasColumnName("rating")
                .HasConversion(PersistenceValueConverters.OptionalRating)
                .IsRequired(false);
            ratingProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalRatingComparer);

            _ = summary.ComplexProperty(value => value.Metadata, metadata =>
            {
                _ = metadata.Property(value => value.Type)
                    .HasColumnName("release_type")
                    .HasConversion<string>()
                    .HasMaxLength(64)
                    .IsRequired();

                ComplexTypePropertyBuilder<IOptionalValue<LabelId>> labelProperty = metadata.Property(value => value.LabelId)
                    .HasColumnName("label_id")
                    .HasConversion(PersistenceValueConverters.OptionalLabelId)
                    .IsRequired(false);
                labelProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalLabelIdComparer);

                ComplexTypePropertyBuilder<IOptionalValue<int>> yearProperty = metadata.Property(value => value.Year)
                    .HasColumnName("release_year")
                    .HasConversion(PersistenceValueConverters.OptionalInt)
                    .IsRequired(false);
                yearProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalIntComparer);

                ComplexTypePropertyBuilder<IOptionalValue<DateOnly>> releaseDateProperty = metadata.Property(value => value.ReleaseDate)
                    .HasColumnName("release_date")
                    .HasConversion(PersistenceValueConverters.OptionalDateOnly)
                    .IsRequired(false);
                releaseDateProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalDateOnlyComparer);

                ComplexTypePropertyBuilder<IOptionalValue<CoverImage>> coverImageProperty = metadata.Property(value => value.CoverImage)
                    .HasColumnName("cover_image_path")
                    .HasMaxLength(2048)
                    .HasConversion(PersistenceValueConverters.OptionalCoverImage)
                    .IsRequired(false);
                coverImageProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalCoverImageComparer);
            });
        });
    }

    private static void ConfigureTracklist(EntityTypeBuilder<Release> builder)
    {
        _ = builder.OwnsMany(release => release.Tracklist, track =>
        {
            _ = track.ToTable("release_tracks");

            _ = track.Property<long>("id")
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            _ = track.HasKey("id");

            _ = track.Property<ReleaseId>(ReleaseIdColumn)
                .HasColumnName(ReleaseIdColumn)
                .HasConversion(PersistenceValueConverters.ReleaseId);

            _ = track.Property<CollectionId>(CollectionIdProperty)
                .HasColumnName("collection_id")
                .HasConversion(PersistenceValueConverters.CollectionId);

            _ = track.WithOwner()
                .HasForeignKey(CollectionIdProperty, ReleaseIdColumn)
                .HasPrincipalKey(release => new { release.CollectionId, release.Id });

            _ = track.Property(releaseTrack => releaseTrack.TrackId)
                .HasColumnName("track_id")
                .HasConversion(PersistenceValueConverters.TrackId);

            _ = track.HasOne<Track>()
                .WithMany()
                .HasForeignKey(CollectionIdProperty, nameof(ReleaseTrack.TrackId))
                .HasPrincipalKey(track => new { track.CollectionId, track.Id })
                .OnDelete(DeleteBehavior.Restrict);

            _ = track.OwnsOne(releaseTrack => releaseTrack.Position, position =>
            {
                _ = position.Property(value => value.Number)
                    .HasColumnName("position_number");

                PropertyBuilder discProperty = position.Property(value => value.Disc)
                    .HasColumnName("position_disc")
                    .HasMaxLength(64)
                    .HasConversion(PersistenceValueConverters.OptionalString)
                    .IsRequired(false);
                discProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalStringComparer);

                PropertyBuilder sideProperty = position.Property(value => value.Side)
                    .HasColumnName("position_side")
                    .HasMaxLength(64)
                    .HasConversion(PersistenceValueConverters.OptionalString)
                    .IsRequired(false);
                sideProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalStringComparer);
            });

            PropertyBuilder titleOverrideProperty = track.Property(releaseTrack => releaseTrack.TitleOverride)
                .HasColumnName("title_override")
                .HasMaxLength(1024)
                .HasConversion(PersistenceValueConverters.OptionalString)
                .IsRequired(false);
            titleOverrideProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalStringComparer);

            _ = track.HasIndex(ReleaseIdColumn);
            _ = track.HasIndex(releaseTrack => releaseTrack.TrackId);
            _ = track.HasIndex(CollectionIdProperty);
        });

        _ = builder.Navigation(release => release.Tracklist)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureCataloging(EntityTypeBuilder<Release> builder)
    {
        _ = builder.OwnsMany<Genre>("_genres", genre =>
        {
            _ = genre.ToTable("release_genres");

            _ = genre.Property<ReleaseId>(ReleaseIdColumn)
                .HasColumnName(ReleaseIdColumn)
                .HasConversion(PersistenceValueConverters.ReleaseId);

            _ = genre.WithOwner()
                .HasForeignKey(ReleaseIdColumn)
                .HasPrincipalKey(release => release.Id);

            _ = genre.Property(value => value.Name)
                .HasColumnName("name")
                .HasMaxLength(256)
                .IsRequired();

            _ = genre.HasKey(ReleaseIdColumn, nameof(Genre.Name));
        });

        _ = builder.Navigation("_genres")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        _ = builder.OwnsMany<Tag>("_tags", tag =>
        {
            _ = tag.ToTable("release_tags");

            _ = tag.Property<ReleaseId>(ReleaseIdColumn)
                .HasColumnName(ReleaseIdColumn)
                .HasConversion(PersistenceValueConverters.ReleaseId);

            _ = tag.WithOwner()
                .HasForeignKey(ReleaseIdColumn)
                .HasPrincipalKey(release => release.Id);

            _ = tag.Property(value => value.Name)
                .HasColumnName("name")
                .HasMaxLength(256)
                .IsRequired();

            _ = tag.HasKey(ReleaseIdColumn, nameof(Tag.Name));
        });

        _ = builder.Navigation("_tags")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
