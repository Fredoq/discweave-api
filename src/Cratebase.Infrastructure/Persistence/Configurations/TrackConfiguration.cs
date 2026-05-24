using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    private const string CollectionIdProperty = nameof(Track.CollectionId);
    private const string TrackIdColumn = "track_id";

    public void Configure(EntityTypeBuilder<Track> builder)
    {
        _ = builder.ToTable("tracks");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(track => track.Id)
            .HasColumnName(TrackIdColumn)
            .HasConversion(PersistenceValueConverters.TrackId)
            .ValueGeneratedNever();

        _ = builder.Property(track => track.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(track => new { track.CollectionId, track.Id })
            .HasName("ak_tracks_collection_track_id");

        _ = builder.Property(track => track.Title)
            .HasColumnName("title")
            .HasMaxLength(1024)
            .IsRequired();

        _ = builder.Ignore(track => track.DisplayName);
        _ = builder.Ignore(track => track.Cataloging);

        ConfigureDetails(builder);
        ConfigureCataloging(builder);

        _ = builder.HasIndex(track => track.CollectionId);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(track => track.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
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

        });
    }

    private static void ConfigureCataloging(EntityTypeBuilder<Track> builder)
    {
        _ = builder.OwnsMany<Genre>("_genres", genre =>
        {
            _ = genre.ToTable("track_genres");

            _ = genre.Property<TrackId>(TrackIdColumn)
                .HasColumnName(TrackIdColumn)
                .HasConversion(PersistenceValueConverters.TrackId);

            _ = genre.Property<CollectionId>(CollectionIdProperty)
                .HasColumnName("collection_id")
                .HasConversion(PersistenceValueConverters.CollectionId);

            _ = genre.WithOwner()
                .HasForeignKey(CollectionIdProperty, TrackIdColumn)
                .HasPrincipalKey(track => new { track.CollectionId, track.Id });

            _ = genre.Property(value => value.Name)
                .HasColumnName("name")
                .HasMaxLength(256)
                .IsRequired();

            _ = genre.HasKey(CollectionIdProperty, TrackIdColumn, nameof(Genre.Name));
        });

        _ = builder.Navigation("_genres")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        _ = builder.OwnsMany<Tag>("_tags", tag =>
        {
            _ = tag.ToTable("track_tags");

            _ = tag.Property<TrackId>(TrackIdColumn)
                .HasColumnName(TrackIdColumn)
                .HasConversion(PersistenceValueConverters.TrackId);

            _ = tag.Property<CollectionId>(CollectionIdProperty)
                .HasColumnName("collection_id")
                .HasConversion(PersistenceValueConverters.CollectionId);

            _ = tag.WithOwner()
                .HasForeignKey(CollectionIdProperty, TrackIdColumn)
                .HasPrincipalKey(track => new { track.CollectionId, track.Id });

            _ = tag.Property(value => value.Name)
                .HasColumnName("name")
                .HasMaxLength(256)
                .IsRequired();

            _ = tag.HasKey(CollectionIdProperty, TrackIdColumn, nameof(Tag.Name));
        });

        _ = builder.Navigation("_tags")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
