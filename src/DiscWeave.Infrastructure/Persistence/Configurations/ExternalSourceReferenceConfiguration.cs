using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal static class ExternalSourceReferenceConfiguration
{
    private const string CollectionIdProperty = "CollectionId";
    private const string CollectionIdColumn = "collection_id";
    private const string ExternalSourcesNavigation = "_externalSources";

    public static void ConfigureArtist(EntityTypeBuilder<Artist> builder)
    {
        const string artistIdColumn = "artist_id";
        _ = builder.OwnsMany<ExternalSourceReference>(ExternalSourcesNavigation, source =>
        {
            _ = source.ToTable("artist_external_sources");
            _ = source.Property<ArtistId>(artistIdColumn)
                .HasColumnName(artistIdColumn)
                .HasConversion(PersistenceValueConverters.ArtistId);
            ConfigureOwner(source);
            _ = source.WithOwner()
                .HasForeignKey(CollectionIdProperty, artistIdColumn)
                .HasPrincipalKey(artist => new { artist.CollectionId, artist.Id });
            ConfigureColumns(source);
            ConfigureKey(source, artistIdColumn);
        });

        UseFieldAccess(builder);
    }

    public static void ConfigureRelease(EntityTypeBuilder<Release> builder)
    {
        const string releaseIdColumn = "release_id";
        _ = builder.OwnsMany<ExternalSourceReference>(ExternalSourcesNavigation, source =>
        {
            _ = source.ToTable("release_external_sources");
            _ = source.Property<ReleaseId>(releaseIdColumn)
                .HasColumnName(releaseIdColumn)
                .HasConversion(PersistenceValueConverters.ReleaseId);
            ConfigureOwner(source);
            _ = source.WithOwner()
                .HasForeignKey(CollectionIdProperty, releaseIdColumn)
                .HasPrincipalKey(release => new { release.CollectionId, release.Id });
            ConfigureColumns(source);
            ConfigureKey(source, releaseIdColumn);
        });

        UseFieldAccess(builder);
    }

    public static void ConfigureTrack(EntityTypeBuilder<Track> builder)
    {
        const string trackIdColumn = "track_id";
        _ = builder.OwnsMany<ExternalSourceReference>(ExternalSourcesNavigation, source =>
        {
            _ = source.ToTable("track_external_sources");
            _ = source.Property<TrackId>(trackIdColumn)
                .HasColumnName(trackIdColumn)
                .HasConversion(PersistenceValueConverters.TrackId);
            ConfigureOwner(source);
            _ = source.WithOwner()
                .HasForeignKey(CollectionIdProperty, trackIdColumn)
                .HasPrincipalKey(track => new { track.CollectionId, track.Id });
            ConfigureColumns(source);
            ConfigureKey(source, trackIdColumn);
        });

        UseFieldAccess(builder);
    }

    private static void ConfigureOwner<TOwner>(
        OwnedNavigationBuilder<TOwner, ExternalSourceReference> source)
        where TOwner : class
    {
        _ = source.Property<CollectionId>(CollectionIdProperty)
            .HasColumnName(CollectionIdColumn)
            .HasConversion(PersistenceValueConverters.CollectionId);
        _ = source.HasIndex(CollectionIdProperty);
    }

    private static void ConfigureColumns<TOwner>(OwnedNavigationBuilder<TOwner, ExternalSourceReference> source)
        where TOwner : class
    {
        _ = source.Property(value => value.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(128)
            .IsRequired();
        _ = source.Property(value => value.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(64)
            .IsRequired();
        _ = source.Property(value => value.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(256)
            .IsRequired();
        _ = source.Property(value => value.SourceUrl)
            .HasColumnName("source_url")
            .HasMaxLength(2048)
            .IsRequired();
        _ = source.Property(value => value.AppliedAt)
            .HasColumnName("applied_at")
            .IsRequired();
    }

    private static void ConfigureKey<TOwner>(
        OwnedNavigationBuilder<TOwner, ExternalSourceReference> source,
        string ownerIdColumn)
        where TOwner : class
    {
        _ = source.HasKey(
            CollectionIdProperty,
            ownerIdColumn,
            nameof(ExternalSourceReference.ProviderName),
            nameof(ExternalSourceReference.ResourceType),
            nameof(ExternalSourceReference.ExternalId));
    }

    private static void UseFieldAccess<TOwner>(EntityTypeBuilder<TOwner> builder)
        where TOwner : class
    {
        _ = builder.Navigation(ExternalSourcesNavigation).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
