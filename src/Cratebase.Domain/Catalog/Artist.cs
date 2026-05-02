using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Catalog;

public abstract class Artist : IEntity<ArtistId>, INamedEntity
{
    protected Artist(ArtistId id, string name)
    {
        Id = id;
        Name = ValidateName(name);
    }

    public ArtistId Id { get; }

    public string Name { get; private set; }

    public void Rename(string name)
    {
        Name = ValidateName(name);
    }

    private static string ValidateName(string name)
    {
        return Guard.RequiredText(name, nameof(name), "artist.name_required");
    }
}
