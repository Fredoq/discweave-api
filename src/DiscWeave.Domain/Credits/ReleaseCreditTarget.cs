using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Credits;

public sealed class ReleaseCreditTarget : CreditTarget
{
    private ReleaseCreditTarget(ReleaseId releaseId)
    {
        ReleaseId = releaseId;
    }

    public override bool IsRelease => true;

    public override bool IsTrack => false;

    public ReleaseId ReleaseId { get; }

    public static ReleaseCreditTarget Create(ReleaseId releaseId)
    {
        return new ReleaseCreditTarget(releaseId);
    }
}
