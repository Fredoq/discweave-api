using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.Collection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        _ = builder.ToTable("artists");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(artist => artist.Id)
            .HasColumnName("artist_id")
            .HasConversion(PersistenceValueConverters.ArtistId)
            .ValueGeneratedNever();

        _ = builder.Property(artist => artist.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(artist => new { artist.CollectionId, artist.Id })
            .HasName("ak_artists_collection_artist_id");

        _ = builder.Property(artist => artist.Name)
            .HasColumnName("name")
            .HasMaxLength(512)
            .IsRequired();

        _ = builder.HasDiscriminator<string>("artist_type")
            .HasValue<Person>("person")
            .HasValue<Group>("group");

        _ = builder.HasIndex(artist => artist.CollectionId);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(artist => artist.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
