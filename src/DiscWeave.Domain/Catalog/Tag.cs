using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Catalog;

public sealed record Tag
{
    private Tag(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static Tag FromName(string name)
    {
        return new Tag(Guard.RequiredText(name, nameof(name), "tag.name_required"));
    }
}
