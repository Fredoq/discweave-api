using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Catalog;

public sealed record ReleaseSummary
{
    private ReleaseSummary()
    {
        Title = string.Empty;
        Metadata = ReleaseMetadata.Empty;
    }

    private ReleaseSummary(string title, ReleaseMetadata metadata)
    {
        Title = title;
        Metadata = metadata;
    }

    public string Title { get; }

    public ReleaseMetadata Metadata { get; }

    internal static ReleaseSummary Empty { get; } = new();

    public static ReleaseSummary Create(string title)
    {
        return new ReleaseSummary(
            Guard.RequiredText(title, nameof(title), "release.title_required"),
            ReleaseMetadata.Empty);
    }

    public ReleaseSummary WithMetadata(ReleaseMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ReleaseSummary(Title, metadata);
    }
}
