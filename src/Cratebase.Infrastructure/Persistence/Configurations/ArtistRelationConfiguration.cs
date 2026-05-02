using Cratebase.Domain.Catalog;
using Cratebase.Domain.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class ArtistRelationConfiguration : IEntityTypeConfiguration<ArtistRelation>
{
    public void Configure(EntityTypeBuilder<ArtistRelation> builder)
    {
        _ = builder.ToTable("artist_relations");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(relation => relation.Id)
            .HasColumnName("artist_relation_id")
            .HasConversion(PersistenceValueConverters.ArtistRelationId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(relation => relation.Id)
            .HasName("artist_relation_id");

        _ = builder.Property(relation => relation.SourceArtistId)
            .HasColumnName("source_artist_id")
            .HasConversion(PersistenceValueConverters.ArtistId);

        _ = builder.Property(relation => relation.TargetArtistId)
            .HasColumnName("target_artist_id")
            .HasConversion(PersistenceValueConverters.ArtistId);

        _ = builder.Property(relation => relation.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Ignore(relation => relation.Period);

        _ = builder.Property<int?>("_periodStartYear")
            .HasColumnName("period_start_year");

        _ = builder.Property<int?>("_periodEndYear")
            .HasColumnName("period_end_year");

        _ = builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(relation => relation.SourceArtistId)
            .HasPrincipalKey(artist => artist.Id)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(relation => relation.TargetArtistId)
            .HasPrincipalKey(artist => artist.Id)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasIndex(relation => relation.SourceArtistId);
        _ = builder.HasIndex(relation => relation.TargetArtistId);
    }
}
