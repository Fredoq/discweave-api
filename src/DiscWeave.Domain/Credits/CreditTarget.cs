using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Credits;

public abstract class CreditTarget
{
    private protected CreditTarget()
    {
    }

    public abstract bool IsRelease { get; }

    public abstract bool IsTrack { get; }

    public static CreditTarget ForRelease(ReleaseId releaseId)
    {
        return ReleaseCreditTarget.Create(releaseId);
    }

    public static CreditTarget ForTrack(TrackId trackId)
    {
        return TrackCreditTarget.Create(trackId);
    }
}
