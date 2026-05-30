using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class TrackRelationConfiguration : IEntityTypeConfiguration<TrackRelation>
{
    public void Configure(EntityTypeBuilder<TrackRelation> builder)
    {
        _ = builder.ToTable("track_relations");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(relation => relation.Id)
            .HasColumnName("track_relation_id")
            .HasConversion(PersistenceValueConverters.TrackRelationId)
            .ValueGeneratedNever();

        _ = builder.Property(relation => relation.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(relation => new { relation.CollectionId, relation.Id })
            .HasName("ak_track_relations_collection_relation_id");

        _ = builder.Property(relation => relation.SourceTrackId)
            .HasColumnName("source_track_id")
            .HasConversion(PersistenceValueConverters.TrackId);

        _ = builder.Property(relation => relation.TargetTrackId)
            .HasColumnName("target_track_id")
            .HasConversion(PersistenceValueConverters.TrackId);

        _ = builder.Property(relation => relation.RelationType)
            .HasColumnName("relation_type")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.HasOne<Track>()
            .WithMany()
            .HasForeignKey(nameof(TrackRelation.CollectionId), nameof(TrackRelation.SourceTrackId))
            .HasPrincipalKey(nameof(Track.CollectionId), nameof(Track.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Track>()
            .WithMany()
            .HasForeignKey(nameof(TrackRelation.CollectionId), nameof(TrackRelation.TargetTrackId))
            .HasPrincipalKey(nameof(Track.CollectionId), nameof(Track.Id))
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasIndex(relation => relation.SourceTrackId);
        _ = builder.HasIndex(relation => relation.TargetTrackId);
        _ = builder.HasIndex(relation => relation.CollectionId);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(relation => relation.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
