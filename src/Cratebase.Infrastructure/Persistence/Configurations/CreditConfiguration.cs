using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class CreditConfiguration : IEntityTypeConfiguration<Credit>
{
    public void Configure(EntityTypeBuilder<Credit> builder)
    {
        _ = builder.ToTable("credits");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(credit => credit.Id)
            .HasColumnName("credit_id")
            .HasConversion(PersistenceValueConverters.CreditId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(credit => credit.Id)
            .HasName("credit_id");

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
            .HasForeignKey("_contributorArtistId")
            .HasPrincipalKey(artist => artist.Id)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Release>()
            .WithMany()
            .HasForeignKey("_targetReleaseId")
            .HasPrincipalKey(release => release.Id)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasOne<Track>()
            .WithMany()
            .HasForeignKey("_targetTrackId")
            .HasPrincipalKey(track => track.Id)
            .OnDelete(DeleteBehavior.Restrict);

        _ = builder.HasIndex("_contributorArtistId");
        _ = builder.HasIndex("_targetReleaseId");
        _ = builder.HasIndex("_targetTrackId");
        _ = builder.HasIndex(credit => credit.Role);
    }
}
