using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Collection;

public sealed record OtherMedium : IMedium
{
    private OtherMedium(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; }

    public string Name { get; }

    public string Description => Name;

    public static OtherMedium Create(string name)
    {
        return Create("other", name);
    }

    public static OtherMedium Create(string code, string name)
    {
        return new OtherMedium(
            Guard.RequiredText(code, nameof(code), "medium.type_required"),
            Guard.RequiredText(name, nameof(name), "other_medium.name_required"));
    }
}
