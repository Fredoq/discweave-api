using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Catalog;

public sealed class ReleaseTrack
{
    private ReleaseTrack()
    {
        Position = TrackPosition.Empty;
        TitleOverride = Optional.Missing<string>();
        VersionNote = Optional.Missing<string>();
    }

    private ReleaseTrack(
        TrackId trackId,
        TrackPosition position,
        IOptionalValue<string> titleOverride,
        IOptionalValue<string> versionNote)
    {
        TrackId = trackId;
        Position = position;
        TitleOverride = titleOverride;
        VersionNote = versionNote;
    }

    public TrackId TrackId { get; private set; }

    public TrackPosition Position { get; private set; }

    public IOptionalValue<string> TitleOverride { get; private set; }

    public IOptionalValue<string> VersionNote { get; private set; }

    public static ReleaseTrack Create(TrackId trackId, TrackPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        return new ReleaseTrack(trackId, position, Optional.Missing<string>(), Optional.Missing<string>());
    }

    public static ReleaseTrack Create(TrackId trackId, TrackPosition position, string titleOverride)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(titleOverride);

        return Create(
            trackId,
            position,
            string.IsNullOrWhiteSpace(titleOverride)
                ? Optional.Missing<string>()
                : Optional.From(titleOverride.Trim()),
            Optional.Missing<string>());
    }

    public static ReleaseTrack Create(
        TrackId trackId,
        TrackPosition position,
        IOptionalValue<string> titleOverride,
        IOptionalValue<string> versionNote)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(titleOverride);
        ArgumentNullException.ThrowIfNull(versionNote);

        return new ReleaseTrack(trackId, position, titleOverride, versionNote);
    }
}
