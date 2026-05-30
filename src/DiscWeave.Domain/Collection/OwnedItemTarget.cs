using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Collection;

public abstract class OwnedItemTarget
{
    private protected OwnedItemTarget()
    {
    }

    public abstract bool IsRelease { get; }

    public abstract bool IsTrack { get; }

    public static OwnedItemTarget ForRelease(ReleaseId releaseId)
    {
        return ReleaseOwnedItemTarget.Create(releaseId);
    }

    public static OwnedItemTarget ForTrack(TrackId trackId)
    {
        return TrackOwnedItemTarget.Create(trackId);
    }
}
