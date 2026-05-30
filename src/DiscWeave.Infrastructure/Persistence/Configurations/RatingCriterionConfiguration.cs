using DiscWeave.Domain.Collection;
using DiscWeave.Domain.Ratings;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscWeave.Infrastructure.Persistence.Configurations;

internal sealed class RatingCriterionConfiguration : IEntityTypeConfiguration<RatingCriterion>
{
    private const string CollectionIdProperty = nameof(RatingCriterion.CollectionId);
    private const string RatingCriterionIdColumn = "rating_criterion_id";

    public void Configure(EntityTypeBuilder<RatingCriterion> builder)
    {
        _ = builder.ToTable("rating_criteria");

        _ = builder.Property<long>("id")
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        _ = builder.HasKey("id");

        _ = builder.Property(criterion => criterion.Id)
            .HasColumnName(RatingCriterionIdColumn)
            .HasConversion(PersistenceValueConverters.RatingCriterionId)
            .ValueGeneratedNever();

        _ = builder.Property(criterion => criterion.CollectionId)
            .HasColumnName("collection_id")
            .HasConversion(PersistenceValueConverters.CollectionId)
            .ValueGeneratedNever();

        _ = builder.HasAlternateKey(criterion => new { criterion.CollectionId, criterion.Id })
            .HasName("ak_rating_criteria_collection_criterion_id");

        _ = builder.Property(criterion => criterion.Code)
            .HasColumnName("code")
            .HasMaxLength(64)
            .IsRequired();

        _ = builder.Property(criterion => criterion.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        _ = builder.Property(criterion => criterion.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        _ = builder.Property(criterion => criterion.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        _ = builder.Property(criterion => criterion.IsBuiltin)
            .HasColumnName("is_builtin")
            .IsRequired();

        _ = builder.Property(criterion => criterion.IsProtected)
            .HasColumnName("is_protected")
            .IsRequired();

        _ = builder.Ignore(criterion => criterion.TargetTypes);

        _ = builder.OwnsMany<RatingCriterionTarget>("_targets", target =>
        {
            _ = target.ToTable("rating_criterion_targets");

            _ = target.Property<RatingCriterionId>(RatingCriterionIdColumn)
                .HasColumnName(RatingCriterionIdColumn)
                .HasConversion(PersistenceValueConverters.RatingCriterionId);

            _ = target.Property<CollectionId>(CollectionIdProperty)
                .HasColumnName("collection_id")
                .HasConversion(PersistenceValueConverters.CollectionId);

            _ = target.WithOwner()
                .HasForeignKey(CollectionIdProperty, RatingCriterionIdColumn)
                .HasPrincipalKey(criterion => new { criterion.CollectionId, criterion.Id });

            _ = target.Property(value => value.TargetType)
                .HasColumnName("target_type")
                .HasConversion(
                    value => RatingTargetTypeCodes.ToCode(value),
                    value => RatingTargetTypeCodes.FromCode(value))
                .HasMaxLength(32)
                .IsRequired();

            _ = target.HasKey(CollectionIdProperty, RatingCriterionIdColumn, nameof(RatingCriterionTarget.TargetType));
        });

        _ = builder.Navigation("_targets")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        _ = builder.HasIndex(criterion => new { criterion.CollectionId, criterion.Code })
            .IsUnique();
        _ = builder.HasIndex(criterion => criterion.CollectionId);

        _ = builder.HasOne<MusicCollection>()
            .WithMany()
            .HasForeignKey(criterion => criterion.CollectionId)
            .HasPrincipalKey(collection => collection.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
