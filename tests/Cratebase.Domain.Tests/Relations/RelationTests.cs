using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Tests.Relations;

public sealed class RelationTests
{
    [Fact]
    public void Artist_relation_rejects_self_relations_and_invalid_periods()
    {
        var artistId = ArtistId.New();

        DomainException selfException = Assert.Throws<DomainException>(
            () => ArtistRelation.Create(ArtistRelationId.New(), CollectionId.New(), artistId, artistId, ArtistRelationType.Alias));
        DomainException periodException = Assert.Throws<DomainException>(
            () => ArtistRelationPeriod.FromYears(1990, 1989));
        DomainException startYearException = Assert.Throws<DomainException>(
            () => ArtistRelationPeriod.StartingAt(0));
        DomainException endYearException = Assert.Throws<DomainException>(
            () => ArtistRelationPeriod.EndingAt(-1));

        Assert.Equal("artist_relation.self_relation", selfException.Code);
        Assert.Equal("relation_period.invalid_range", periodException.Code);
        Assert.Equal("relation_period.start_year_required", startYearException.Code);
        Assert.Equal("relation_period.end_year_required", endYearException.Code);
    }

    [Fact]
    public void Artist_relation_supports_optional_periods()
    {
        var relation = ArtistRelation.Create(
            ArtistRelationId.New(),
            CollectionId.New(),
            ArtistId.New(),
            ArtistId.New(),
            ArtistRelationType.MemberOf,
            ArtistRelationPeriod.FromYears(1980, 1985));

        ArtistRelationPeriod period = Assert.IsType<PresentOptionalValue<ArtistRelationPeriod>>(relation.Period).Value;

        Assert.Equal(1980, Assert.IsType<PresentOptionalValue<int>>(period.StartYear).Value);
        Assert.Equal(1985, Assert.IsType<PresentOptionalValue<int>>(period.EndYear).Value);
    }

    [Fact]
    public void Artist_relation_can_omit_a_period()
    {
        var relation = ArtistRelation.Create(
            ArtistRelationId.New(),
            CollectionId.New(),
            ArtistId.New(),
            ArtistId.New(),
            ArtistRelationType.Collaboration);

        Assert.False(relation.Period.HasValue);
    }

    [Fact]
    public void Track_relation_rejects_self_relations_and_carries_a_relation_type()
    {
        var collectionId = CollectionId.New();
        var trackId = TrackId.New();

        DomainException exception = Assert.Throws<DomainException>(
            () => TrackRelation.Create(TrackRelationId.New(), collectionId, trackId, trackId, TrackRelationType.RemixOf));
        var relation = TrackRelation.Create(
            TrackRelationId.New(),
            collectionId,
            TrackId.New(),
            TrackId.New(),
            TrackRelationType.VersionOf);

        Assert.Equal("track_relation.self_relation", exception.Code);
        Assert.Equal(collectionId, relation.CollectionId);
        Assert.Equal("versionOf", relation.RelationType);
    }

    [Fact]
    public void Relation_types_are_closed_object_catalogs()
    {
        Assert.Contains(nameof(ArtistRelationType.MemberOf), Enum.GetNames<ArtistRelationType>());
        Assert.Contains(nameof(TrackRelationType.VersionOf), Enum.GetNames<TrackRelationType>());
        Assert.NotEqual(ArtistRelationType.Alias, ArtistRelationType.Collaboration);
        Assert.NotEqual(TrackRelationType.RemixOf, TrackRelationType.EditOf);
    }
}
