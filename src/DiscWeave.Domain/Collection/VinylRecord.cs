using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Collection;

public sealed record VinylRecord : IMedium
{
    private VinylRecord(string code, string formatDescription)
    {
        Code = code;
        FormatDescription = formatDescription;
    }

    public string Code { get; }

    public string FormatDescription { get; }

    public string Description => FormatDescription;

    public static VinylRecord Create(string formatDescription)
    {
        return Create("vinyl", formatDescription);
    }

    public static VinylRecord Create(string code, string formatDescription)
    {
        return new VinylRecord(
            Guard.RequiredText(code, nameof(code), "medium.type_required"),
            Guard.RequiredText(formatDescription, nameof(formatDescription), "vinyl_record.format_required"));
    }
}
