using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Playlists;

public sealed class PlaylistEntry
{
    public const string ReleaseKind = "release";
    public const string TrackKind = "track";

    private ReleaseId? _releaseId;
    private TrackId? _trackId;

    private PlaylistEntry()
    {
        Kind = string.Empty;
    }

    private PlaylistEntry(int position, string kind, ReleaseId? releaseId, TrackId? trackId)
    {
        Position = position;
        Kind = kind;
        SetReleaseId(releaseId);
        SetTrackId(trackId);
    }

    public int Position { get; private set; }

    public string Kind { get; private set; }

    public IOptionalValue<ReleaseId> ReleaseId => _releaseId.HasValue
        ? Optional.From(_releaseId.Value)
        : Optional.Missing<ReleaseId>();

    public IOptionalValue<TrackId> TrackId => _trackId.HasValue
        ? Optional.From(_trackId.Value)
        : Optional.Missing<TrackId>();

    public static PlaylistEntry ForRelease(int position, ReleaseId releaseId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        return releaseId == default
            ? throw new ArgumentException("Release id is required", nameof(releaseId))
            : new PlaylistEntry(position, ReleaseKind, releaseId, null);
    }

    public static PlaylistEntry ForTrack(int position, TrackId trackId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        return trackId == default
            ? throw new ArgumentException("Track id is required", nameof(trackId))
            : new PlaylistEntry(position, TrackKind, null, trackId);
    }

    private void SetReleaseId(ReleaseId? releaseId)
    {
        _releaseId = releaseId;
    }

    private void SetTrackId(TrackId? trackId)
    {
        _trackId = trackId;
    }
}
