using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Relations;

public sealed record ArtistRelationPeriod
{
    private ArtistRelationPeriod(IOptionalValue<int> startYear, IOptionalValue<int> endYear)
    {
        StartYear = startYear;
        EndYear = endYear;
    }

    public IOptionalValue<int> StartYear { get; }

    public IOptionalValue<int> EndYear { get; }

    public static ArtistRelationPeriod FromYears(int startYear, int endYear)
    {
        startYear = Guard.Positive(startYear, nameof(startYear), "relation_period.start_year_required");
        endYear = Guard.Positive(endYear, nameof(endYear), "relation_period.end_year_required");

        return startYear > endYear
            ? throw new DomainException("relation_period.invalid_range", "Relation period start year cannot be after end year")
            : new ArtistRelationPeriod(Optional.From(startYear), Optional.From(endYear));
    }

    public static ArtistRelationPeriod StartingAt(int startYear)
    {
        return new ArtistRelationPeriod(
            Optional.From(Guard.Positive(startYear, nameof(startYear), "relation_period.start_year_required")),
            Optional.Missing<int>());
    }

    public static ArtistRelationPeriod EndingAt(int endYear)
    {
        return new ArtistRelationPeriod(
            Optional.Missing<int>(),
            Optional.From(Guard.Positive(endYear, nameof(endYear), "relation_period.end_year_required")));
    }
}
