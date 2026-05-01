using Cratebase.Domain.Catalog;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        _ = builder.ToTable("tracks");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(track => track.Id)
            .HasColumnName("track_id")
            .HasConversion(PersistenceValueConverters.TrackId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(track => track.Id)
            .HasName("track_id");

        _ = builder.Property(track => track.Title)
            .HasColumnName("title")
            .HasMaxLength(1024)
            .IsRequired();

        _ = builder.Ignore(track => track.DisplayName);
        _ = builder.Ignore(track => track.Cataloging);

        ConfigureDetails(builder);
        ConfigureCataloging(builder);
    }

    private static void ConfigureDetails(EntityTypeBuilder<Track> builder)
    {
        _ = builder.ComplexProperty(track => track.Details, details =>
        {
            ComplexTypePropertyBuilder<IOptionalValue<TimeSpan>> durationProperty = details.Property(value => value.Duration)
                .HasColumnName("duration_ticks")
                .HasConversion(PersistenceValueConverters.OptionalTimeSpanTicks)
                .IsRequired(false);
            durationProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalTimeSpanComparer);

            ComplexTypePropertyBuilder<IOptionalValue<Rating>> ratingProperty = details.Property(value => value.Rating)
                .HasColumnName("rating")
                .HasConversion(PersistenceValueConverters.OptionalRating)
                .IsRequired(false);
            ratingProperty.Metadata.SetValueComparer(PersistenceValueConverters.OptionalRatingComparer);
        });
    }

    private static void ConfigureCataloging(EntityTypeBuilder<Track> builder)
    {
        _ = builder.OwnsMany<Genre>("_genres", genre =>
        {
            _ = genre.ToTable("track_genres");

            _ = genre.Property<TrackId>("track_id")
                .HasColumnName("track_id")
                .HasConversion(PersistenceValueConverters.TrackId);

            _ = genre.WithOwner()
                .HasForeignKey("track_id")
                .HasPrincipalKey(track => track.Id);

            _ = genre.Property(value => value.Name)
                .HasColumnName("name")
                .HasMaxLength(256)
                .IsRequired();

            _ = genre.HasKey("track_id", nameof(Genre.Name));
        });

        _ = builder.Navigation("_genres")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        _ = builder.OwnsMany<Tag>("_tags", tag =>
        {
            _ = tag.ToTable("track_tags");

            _ = tag.Property<TrackId>("track_id")
                .HasColumnName("track_id")
                .HasConversion(PersistenceValueConverters.TrackId);

            _ = tag.WithOwner()
                .HasForeignKey("track_id")
                .HasPrincipalKey(track => track.Id);

            _ = tag.Property(value => value.Name)
                .HasColumnName("name")
                .HasMaxLength(256)
                .IsRequired();

            _ = tag.HasKey("track_id", nameof(Tag.Name));
        });

        _ = builder.Navigation("_tags")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
