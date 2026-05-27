using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.ArtistRelations;

internal static class ArtistRelationMapper
{
    public static string ParseType(string type)
    {
        return string.IsNullOrWhiteSpace(type)
            ? throw new DomainException("artist_relation.type_invalid", "Artist relation type is invalid")
            : type.Trim();
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

    public static ArtistRelationResponse ToResponse(ArtistRelation relation, string? sourceArtistName = null, string? targetArtistName = null)
    {
        return new ArtistRelationResponse(
            relation.Id.Value,
            relation.SourceArtistId.Value,
            relation.TargetArtistId.Value,
            relation.Type,
            GetStartYear(relation),
            GetEndYear(relation),
            sourceArtistName,
            targetArtistName);
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
