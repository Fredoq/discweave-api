using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Collection;

public sealed record StorageLocation
{
    private StorageLocation(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static StorageLocation FromName(string name)
    {
        return new StorageLocation(Guard.RequiredText(name, nameof(name), "storage_location.name_required"));
    }
}
