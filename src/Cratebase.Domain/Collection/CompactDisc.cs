using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Collection;

public sealed record CompactDisc : IMedium
{
    private CompactDisc(string code, int discCount)
    {
        Code = code;
        DiscCount = discCount;
    }

    public string Code { get; }

    public int DiscCount { get; }

    public string Description => DiscCount == 1 ? "CD" : $"{DiscCount} CDs";

    public static CompactDisc Create(int discCount)
    {
        return Create("cd", discCount);
    }

    public static CompactDisc Create(string code, int discCount)
    {
        return new CompactDisc(
            Guard.RequiredText(code, nameof(code), "medium.type_required"),
            Guard.Positive(discCount, nameof(discCount), "compact_disc.disc_count_required"));
    }
}
