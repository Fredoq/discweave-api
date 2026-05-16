using Cratebase.Domain.Collection;
using Cratebase.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal sealed class LocalAgentImportTokenConfiguration : IEntityTypeConfiguration<LocalAgentImportToken>
{
    public void Configure(EntityTypeBuilder<LocalAgentImportToken> builder)
    {
        _ = builder.ToTable("local_agent_import_tokens");

        _ = builder.Property<long>("id").HasColumnName("id").ValueGeneratedOnAdd();
        _ = builder.HasKey("id");

        _ = builder.Property(token => token.Id)
            .HasColumnName("local_agent_import_token_id")
            .HasConversion(PersistenceValueConverters.LocalAgentImportTokenId)
            .ValueGeneratedNever();
        _ = builder.Property(token => token.CollectionId).HasColumnName("collection_id").HasConversion(PersistenceValueConverters.CollectionId).ValueGeneratedNever();
        _ = builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        _ = builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        _ = builder.Property(token => token.CreatedAt).HasColumnName("created_at");
        _ = builder.Property(token => token.UsedAt).HasColumnName("used_at");

        _ = builder.HasAlternateKey(token => token.Id).HasName("local_agent_import_token_id");
        _ = builder.HasIndex(token => token.TokenHash).IsUnique();
        _ = builder.HasIndex(token => new { token.CollectionId, token.CreatedAt });

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(token => token.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
