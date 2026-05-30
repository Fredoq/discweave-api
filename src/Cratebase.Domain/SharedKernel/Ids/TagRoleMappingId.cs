namespace Cratebase.Domain.SharedKernel.Ids;

public readonly record struct TagRoleMappingId(Guid Value)
{
    public static TagRoleMappingId New()
    {
        return new TagRoleMappingId(Guid.CreateVersion7());
    }
}
