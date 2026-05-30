using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Catalog;

public sealed record Genre
{
    private Genre(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static Genre FromName(string name)
    {
        return new Genre(Guard.RequiredText(name, nameof(name), "genre.name_required"));
    }
}
