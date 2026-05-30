using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Credits;

public sealed class TrackCreditTarget : CreditTarget
{
    private TrackCreditTarget(TrackId trackId)
    {
        TrackId = trackId;
    }

    public override bool IsRelease => false;

    public override bool IsTrack => true;

    public TrackId TrackId { get; }

    public static TrackCreditTarget Create(TrackId trackId)
    {
        return new TrackCreditTarget(trackId);
    }
}
