using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Catalog;

public abstract class Artist : IEntity<ArtistId>, INamedEntity
{
    private readonly List<ExternalSourceReference> _externalSources = [];

    protected Artist(CollectionId collectionId, ArtistId id, string name)
    {
        CollectionId = collectionId;
        Id = id;
        Name = ValidateName(name);
    }

    public CollectionId CollectionId { get; }

    public ArtistId Id { get; }

    public string Name { get; private set; }

    public IReadOnlyList<ExternalSourceReference> ExternalSources => _externalSources.AsReadOnly();

    public void Rename(string name)
    {
        Name = ValidateName(name);
    }

    public void ReplaceExternalSources(IReadOnlyList<ExternalSourceReference> externalSources)
    {
        ExternalSourceReferences.Replace(_externalSources, externalSources);
    }

    private static string ValidateName(string name)
    {
        return Guard.RequiredText(name, nameof(name), "artist.name_required");
    }
}
