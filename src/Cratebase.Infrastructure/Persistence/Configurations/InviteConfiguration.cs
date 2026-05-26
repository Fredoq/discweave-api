using Cratebase.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        _ = builder.ToTable("invites");

        _ = builder.HasKey(invite => invite.Id);

        _ = builder.Property(invite => invite.Id)
            .HasColumnName("invite_id")
            .ValueGeneratedNever();

        _ = builder.Property(invite => invite.CodeHash)
            .HasColumnName("code_hash")
            .HasMaxLength(128)
            .IsRequired();

        _ = builder.Property(invite => invite.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        _ = builder.Property(invite => invite.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        _ = builder.Property(invite => invite.Note)
            .HasColumnName("note")
            .HasMaxLength(512);

        _ = builder.Property(invite => invite.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        _ = builder.Property(invite => invite.RevokedAt)
            .HasColumnName("revoked_at");

        _ = builder.Property(invite => invite.RevokedByUserId)
            .HasColumnName("revoked_by_user_id");

        _ = builder.Property(invite => invite.RedeemedAt)
            .HasColumnName("redeemed_at");

        _ = builder.Property(invite => invite.RedeemedUserId)
            .HasColumnName("redeemed_user_id");

        _ = builder.Property(invite => invite.RedeemedEmail)
            .HasColumnName("redeemed_email")
            .HasMaxLength(256);

        _ = builder.HasIndex(invite => invite.CodeHash)
            .IsUnique()
            .HasDatabaseName("ix_invites_code_hash");
    }
}
