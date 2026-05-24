using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Playlists;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    private const string CollectionIdProperty = nameof(Playlist.CollectionId);
    private const string PlaylistIdColumn = "playlist_id";

    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        _ = builder.ToTable("playlists");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(playlist => playlist.Id)
            .HasColumnName(PlaylistIdColumn)
            .HasConversion(PersistenceValueConverters.PlaylistId)
            .ValueGeneratedNever();

        _ = builder.Property(playlist => playlist.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(playlist => new { playlist.CollectionId, playlist.Id }).HasName("ak_playlists_collection_playlist_id");

        _ = builder.Property(playlist => playlist.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
        _ = builder.Property<string?>("_description").HasColumnName("description").HasMaxLength(4096);
        _ = builder.Property(playlist => playlist.Type).HasColumnName("playlist_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        _ = builder.Property<string>("_ruleTags").HasColumnName("rule_tags").HasMaxLength(4096).IsRequired();
        _ = builder.Property<string>("_ruleGenres").HasColumnName("rule_genres").HasMaxLength(4096).IsRequired();
        _ = builder.Property<string>("_ruleMedia").HasColumnName("rule_media").HasMaxLength(4096).IsRequired();
        _ = builder.Property<string>("_ruleOwnershipStatuses").HasColumnName("rule_ownership_statuses").HasMaxLength(4096).IsRequired();
        _ = builder.Property<int?>("_ruleYearFrom").HasColumnName("rule_year_from");
        _ = builder.Property<int?>("_ruleYearTo").HasColumnName("rule_year_to");

        _ = builder.Ignore(playlist => playlist.Description);
        _ = builder.Ignore(playlist => playlist.Rules);

        ConfigureEntries(builder);

        _ = builder.HasIndex(playlist => playlist.CollectionId);
        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(playlist => playlist.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureEntries(EntityTypeBuilder<Playlist> builder)
    {
        _ = builder.OwnsMany(playlist => playlist.Entries, entry =>
        {
            _ = entry.ToTable(
                "playlist_entries",
                table => table.HasCheckConstraint(
                    "ck_playlist_entries_target_consistency",
                    "(kind = 'release' AND release_id IS NOT NULL AND track_id IS NULL) OR " +
                    "(kind = 'track' AND track_id IS NOT NULL AND release_id IS NULL)"));

            _ = entry.Property<long>("id").HasColumnName("id").ValueGeneratedOnAdd();
            _ = entry.HasKey("id");
            _ = entry.Property<PlaylistId>(PlaylistIdColumn).HasColumnName(PlaylistIdColumn).HasConversion(PersistenceValueConverters.PlaylistId);
            _ = entry.Property<CollectionId>(CollectionIdProperty).HasColumnName("collection_id").HasConversion(PersistenceValueConverters.CollectionId);
            _ = entry.WithOwner().HasForeignKey(CollectionIdProperty, PlaylistIdColumn).HasPrincipalKey(playlist => new { playlist.CollectionId, playlist.Id });
            _ = entry.Property(value => value.Position).HasColumnName("position").IsRequired();
            _ = entry.Property(value => value.Kind).HasColumnName("kind").HasMaxLength(32).IsRequired();
            _ = entry.Property<ReleaseId?>("_releaseId").HasColumnName("release_id").HasConversion(PersistenceValueConverters.NullableReleaseId);
            _ = entry.Property<TrackId?>("_trackId").HasColumnName("track_id").HasConversion(PersistenceValueConverters.NullableTrackId);
            _ = entry.Ignore(value => value.ReleaseId);
            _ = entry.Ignore(value => value.TrackId);
            _ = entry.HasOne<Release>().WithMany().HasForeignKey(CollectionIdProperty, "_releaseId").HasPrincipalKey(release => new { release.CollectionId, release.Id }).OnDelete(DeleteBehavior.Restrict);
            _ = entry.HasOne<Track>().WithMany().HasForeignKey(CollectionIdProperty, "_trackId").HasPrincipalKey(track => new { track.CollectionId, track.Id }).OnDelete(DeleteBehavior.Restrict);
            _ = entry.HasIndex(CollectionIdProperty, PlaylistIdColumn, nameof(PlaylistEntry.Position)).IsUnique();
            _ = entry.HasIndex("_releaseId");
            _ = entry.HasIndex("_trackId");
        });

        _ = builder.Navigation(playlist => playlist.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
