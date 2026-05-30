using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Catalog;

public sealed class Label : IEntity<LabelId>, INamedEntity
{
    private Label(CollectionId collectionId, LabelId id, string name)
    {
        CollectionId = collectionId;
        Id = id;
        Name = name;
    }

    public CollectionId CollectionId { get; }

    public LabelId Id { get; }

    public string Name { get; private set; }

    public static Label Create(CollectionId collectionId, LabelId id, string name)
    {
        return new Label(collectionId, id, Guard.RequiredText(name, nameof(name), "label.name_required"));
    }

    public void Rename(string name)
    {
        Name = Guard.RequiredText(name, nameof(name), "label.name_required");
    }
}
