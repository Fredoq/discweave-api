using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Catalog;

public sealed record TrackDetails
{
    private TrackDetails(IOptionalValue<TimeSpan>? duration)
    {
        Duration = duration ?? Optional.Missing<TimeSpan>();
    }

    public IOptionalValue<TimeSpan> Duration { get; }

    public static TrackDetails Empty { get; } = new(Optional.Missing<TimeSpan>());

    public TrackDetails WithDuration(TimeSpan duration)
    {
        _ = Duration;

        return new TrackDetails(Optional.From(Guard.Positive(duration, nameof(duration), "track.duration_required")));
    }
}
