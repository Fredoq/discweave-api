using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.ArtistRelations;

internal static class ArtistRelationMapper
{
    public static ArtistRelationType ParseType(string type)
    {
        return type.Trim() switch
        {
            "alias" => ArtistRelationType.Alias,
            "memberOf" => ArtistRelationType.MemberOf,
            "soloProject" => ArtistRelationType.SoloProject,
            "collaboration" => ArtistRelationType.Collaboration,
            _ => throw new DomainException("artist_relation.type_invalid", "Artist relation type is invalid")
        };
    }

    public static ArtistRelationPeriod? CreatePeriod(int? startYear, int? endYear)
    {
        return (startYear, endYear) switch
        {
            (null, null) => null,
            ({ } start, null) => ArtistRelationPeriod.StartingAt(start),
            (null, { } end) => ArtistRelationPeriod.EndingAt(end),
            ({ } start, { } end) => ArtistRelationPeriod.FromYears(start, end)
        };
    }

    public static ArtistRelationResponse ToResponse(ArtistRelation relation)
    {
        return new ArtistRelationResponse(
            relation.Id.Value,
            relation.SourceArtistId.Value,
            relation.TargetArtistId.Value,
            ToTypeCode(relation.Type),
            GetStartYear(relation),
            GetEndYear(relation));
    }

    private static string ToTypeCode(ArtistRelationType type)
    {
        return type switch
        {
            ArtistRelationType.Alias => "alias",
            ArtistRelationType.MemberOf => "memberOf",
            ArtistRelationType.SoloProject => "soloProject",
            ArtistRelationType.Collaboration => "collaboration",
            _ => throw new InvalidOperationException("Artist relation type is not supported")
        };
    }

    private static int? GetStartYear(ArtistRelation relation)
    {
        return relation.Period is PresentOptionalValue<ArtistRelationPeriod> period &&
            period.Value.StartYear is PresentOptionalValue<int> startYear
            ? startYear.Value
            : null;
    }

    private static int? GetEndYear(ArtistRelation relation)
    {
        return relation.Period is PresentOptionalValue<ArtistRelationPeriod> period &&
            period.Value.EndYear is PresentOptionalValue<int> endYear
            ? endYear.Value
            : null;
    }
}
