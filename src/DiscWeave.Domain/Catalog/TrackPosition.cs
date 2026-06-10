using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Catalog;

public sealed record TrackPosition
{
    private const int MarkerMaxLength = 64;

    private TrackPosition()
    {
        Disc = Optional.Missing<string>();
        Side = Optional.Missing<string>();
    }

    private TrackPosition(int number, IOptionalValue<string> disc, IOptionalValue<string> side)
    {
        Number = number;
        Disc = disc;
        Side = side;
    }

    public int Number { get; private set; }

    public IOptionalValue<string> Disc { get; private set; }

    public IOptionalValue<string> Side { get; private set; }

    internal static TrackPosition Empty { get; } = new();

    public static TrackPosition FromNumber(int number)
    {
        return new TrackPosition(
            Guard.Positive(number, nameof(number), "track_position.number_required"),
            Optional.Missing<string>(),
            Optional.Missing<string>());
    }

    public static TrackPosition FromNumber(int number, string disc, string side)
    {
        ArgumentNullException.ThrowIfNull(disc);
        ArgumentNullException.ThrowIfNull(side);

        return new TrackPosition(
            Guard.Positive(number, nameof(number), "track_position.number_required"),
            ToMarker(disc, nameof(disc), "track_position.disc_too_long"),
            ToMarker(side, nameof(side), "track_position.side_too_long"));
    }

    private static IOptionalValue<string> ToMarker(string value, string fieldName, string code)
    {
        string trimmed = value.Trim();
        return trimmed.Length switch
        {
            0 => Optional.Missing<string>(),
            > MarkerMaxLength => throw new DomainException(code, $"{fieldName} must be at most {MarkerMaxLength} characters"),
            _ => Optional.From(trimmed)
        };
    }
}
