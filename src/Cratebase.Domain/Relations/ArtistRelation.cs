using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Relations;

public sealed class ArtistRelation : IEntity<ArtistRelationId>
{
    private const string SelfRelationCode = "artist_relation.self_relation";
    private const string SelfRelationMessage = "Artist relation cannot reference the same artist twice";

    private int? _periodStartYear;
    private int? _periodEndYear;

    private ArtistRelation()
    {
    }

    private ArtistRelation(
        CollectionId collectionId,
        ArtistRelationId id,
        ArtistId sourceArtistId,
        ArtistId targetArtistId,
        ArtistRelationType type,
        IOptionalValue<ArtistRelationPeriod> period)
    {
        CollectionId = collectionId;
        Id = id;
        SourceArtistId = sourceArtistId;
        TargetArtistId = targetArtistId;
        Type = type;
        SetPeriod(period);
    }

    public CollectionId CollectionId { get; private set; }

    public ArtistRelationId Id { get; private set; }

    public ArtistId SourceArtistId { get; private set; }

    public ArtistId TargetArtistId { get; private set; }

    public ArtistRelationType Type { get; private set; }

    public IOptionalValue<ArtistRelationPeriod> Period => CreatePeriod();

    public static ArtistRelation Create(
        ArtistRelationId id,
        CollectionId collectionId,
        ArtistId sourceArtistId,
        ArtistId targetArtistId,
        ArtistRelationType type)
    {
        return sourceArtistId == targetArtistId
            ? throw new DomainException(SelfRelationCode, SelfRelationMessage)
            : new ArtistRelation(collectionId, id, sourceArtistId, targetArtistId, type, Optional.Missing<ArtistRelationPeriod>());
    }

    public static ArtistRelation Create(
        ArtistRelationId id,
        CollectionId collectionId,
        ArtistId sourceArtistId,
        ArtistId targetArtistId,
        ArtistRelationType type,
        ArtistRelationPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        return sourceArtistId == targetArtistId
            ? throw new DomainException(SelfRelationCode, SelfRelationMessage)
            : new ArtistRelation(collectionId, id, sourceArtistId, targetArtistId, type, Optional.From(period));
    }

    public void Update(
        ArtistId sourceArtistId,
        ArtistId targetArtistId,
        ArtistRelationType type)
    {
        if (sourceArtistId == targetArtistId)
        {
            throw new DomainException(SelfRelationCode, SelfRelationMessage);
        }

        SourceArtistId = sourceArtistId;
        TargetArtistId = targetArtistId;
        Type = type;
        SetPeriod(Optional.Missing<ArtistRelationPeriod>());
    }

    public void Update(
        ArtistId sourceArtistId,
        ArtistId targetArtistId,
        ArtistRelationType type,
        ArtistRelationPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        if (sourceArtistId == targetArtistId)
        {
            throw new DomainException(SelfRelationCode, SelfRelationMessage);
        }

        SourceArtistId = sourceArtistId;
        TargetArtistId = targetArtistId;
        Type = type;
        SetPeriod(Optional.From(period));
    }

    private void SetPeriod(IOptionalValue<ArtistRelationPeriod> period)
    {
        if (period is not PresentOptionalValue<ArtistRelationPeriod> presentPeriod)
        {
            _periodStartYear = null;
            _periodEndYear = null;
            return;
        }

        ArtistRelationPeriod value = presentPeriod.Value;
        _periodStartYear = value.StartYear is PresentOptionalValue<int> presentStartYear
            ? presentStartYear.Value
            : null;
        _periodEndYear = value.EndYear is PresentOptionalValue<int> presentEndYear
            ? presentEndYear.Value
            : null;
    }

    private IOptionalValue<ArtistRelationPeriod> CreatePeriod()
    {
        return (_periodStartYear, _periodEndYear) switch
        {
            (null, null) => Optional.Missing<ArtistRelationPeriod>(),
            ({ } startYear, null) => Optional.From(ArtistRelationPeriod.StartingAt(startYear)),
            (null, { } endYear) => Optional.From(ArtistRelationPeriod.EndingAt(endYear)),
            ({ } startYear, { } endYear) => Optional.From(ArtistRelationPeriod.FromYears(startYear, endYear))
        };
    }
}
