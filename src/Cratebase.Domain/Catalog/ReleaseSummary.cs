using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Catalog;

public sealed record ReleaseSummary
{
    private ReleaseSummary()
    {
        Title = string.Empty;
        Metadata = ReleaseMetadata.Empty;
        Rating = Optional.Missing<Rating>();
    }

    private ReleaseSummary(string title, ReleaseMetadata metadata, IOptionalValue<Rating> rating)
    {
        Title = title;
        Metadata = metadata;
        Rating = rating;
    }

    public string Title { get; }

    public ReleaseMetadata Metadata { get; }

    public IOptionalValue<Rating> Rating { get; }

    public static ReleaseSummary Empty { get; } = new();

    public static ReleaseSummary Create(string title)
    {
        return new ReleaseSummary(
            Guard.RequiredText(title, nameof(title), "release.title_required"),
            ReleaseMetadata.Empty,
            Optional.Missing<Rating>());
    }

    public ReleaseSummary WithMetadata(ReleaseMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ReleaseSummary(Title, metadata, Rating);
    }

    public ReleaseSummary WithRating(Rating rating)
    {
        ArgumentNullException.ThrowIfNull(rating);

        return new ReleaseSummary(Title, Metadata, Optional.From(rating));
    }
}
