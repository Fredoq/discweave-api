using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Optional;

namespace DiscWeave.Domain.Collection;

public sealed class OwnedItem : IEntity<OwnedItemId>
{
    private const string ReleaseTargetType = "release";
    private const string TrackTargetType = "track";

    private string _targetType = string.Empty;
    private ReleaseId? _targetReleaseId;
    private TrackId? _targetTrackId;
    private OwnershipStatus _status;
    private string _mediumType = string.Empty;
    private string? _digitalFilePath;
    private AudioFileFormat? _digitalFileFormat;
    private string? _importIdentityPath;
    private long? _importIdentitySizeBytes;
    private DateTimeOffset? _importIdentityLastModifiedAt;
    private string? _importIdentityContentHash;
    private string? _vinylFormatDescription;
    private int? _compactDiscCount;
    private string? _cassetteTapeType;
    private string? _otherMediumName;
    private ItemCondition? _condition;
    private string? _storageLocation;

    private OwnedItem()
    {
    }

    private OwnedItem(
        CollectionId collectionId,
        OwnedItemId id,
        OwnedItemTarget target,
        OwnedItemHolding holding)
    {
        CollectionId = collectionId;
        Id = id;
        SetTarget(target);
        SetHolding(holding);
    }

    public CollectionId CollectionId { get; private set; }

    public OwnedItemId Id { get; private set; }

    public OwnedItemTarget Target => CreateTarget();

    public OwnedItemHolding Holding => CreateHolding();

    public static OwnedItem Create(CollectionId collectionId, OwnedItemId id, OwnedItemTarget target, OwnershipStatus status, IMedium medium)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new OwnedItem(collectionId, id, target, OwnedItemHolding.Create(status, medium));
    }

    public OwnedItem WithCondition(ItemCondition condition)
    {
        return new OwnedItem(CollectionId, Id, Target, Holding.WithDetails(Holding.Details.WithCondition(condition)));
    }

    public OwnedItem WithStorageLocation(StorageLocation storageLocation)
    {
        return new OwnedItem(CollectionId, Id, Target, Holding.WithDetails(Holding.Details.WithStorageLocation(storageLocation)));
    }

    public void UpdateHolding(OwnedItemHolding holding)
    {
        SetHolding(holding);
    }

    public void UpdateTarget(OwnedItemTarget target)
    {
        SetTarget(target);
    }

    private void SetTarget(OwnedItemTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        switch (target)
        {
            case ReleaseOwnedItemTarget releaseTarget:
                _targetType = ReleaseTargetType;
                _targetReleaseId = releaseTarget.ReleaseId;
                _targetTrackId = null;
                break;
            case TrackOwnedItemTarget trackTarget:
                _targetType = TrackTargetType;
                _targetReleaseId = null;
                _targetTrackId = trackTarget.TrackId;
                break;
            default:
                throw new InvalidOperationException("Owned item target type is not supported");
        }
    }

    private OwnedItemTarget CreateTarget()
    {
        return _targetType switch
        {
            ReleaseTargetType when _targetReleaseId is { } releaseId => OwnedItemTarget.ForRelease(releaseId),
            TrackTargetType when _targetTrackId is { } trackId => OwnedItemTarget.ForTrack(trackId),
            _ => throw new InvalidOperationException("Owned item target payload is not valid")
        };
    }

    private void SetHolding(OwnedItemHolding holding)
    {
        ArgumentNullException.ThrowIfNull(holding);

        _status = holding.Status;
        SetMedium(holding.Medium);
        _condition = holding.Details.Condition is PresentOptionalValue<ItemCondition> presentCondition
            ? presentCondition.Value
            : null;
        _storageLocation = holding.Details.StorageLocation is PresentOptionalValue<StorageLocation> presentStorageLocation
            ? presentStorageLocation.Value.Name
            : null;
    }

    private OwnedItemHolding CreateHolding()
    {
        var holding = OwnedItemHolding.Create(_status, CreateMedium());
        OwnedItemDetails details = OwnedItemDetails.Empty;

        if (_condition is { } itemCondition)
        {
            details = details.WithCondition(itemCondition);
        }

        if (_storageLocation is { } location)
        {
            details = details.WithStorageLocation(StorageLocation.FromName(location));
        }

        return holding.WithDetails(details);
    }

    private void SetMedium(IMedium medium)
    {
        switch (medium)
        {
            case DigitalFile digitalFile:
                SetDigitalFile(digitalFile);
                break;
            case VinylRecord vinylRecord:
                ClearMediumDetails();
                _mediumType = vinylRecord.Code;
                _vinylFormatDescription = vinylRecord.FormatDescription;
                break;
            case CompactDisc compactDisc:
                ClearMediumDetails();
                _mediumType = compactDisc.Code;
                _compactDiscCount = compactDisc.DiscCount;
                break;
            case CassetteTape cassetteTape:
                ClearMediumDetails();
                _mediumType = cassetteTape.Code;
                _cassetteTapeType = cassetteTape.TapeType;
                break;
            case OtherMedium otherMedium:
                ClearMediumDetails();
                _mediumType = otherMedium.Code;
                _otherMediumName = otherMedium.Name;
                break;
            default:
                throw new InvalidOperationException("Medium type is not supported");
        }
    }

    private void SetDigitalFile(DigitalFile digitalFile)
    {
        ClearMediumDetails();
        _mediumType = digitalFile.Code;
        _digitalFilePath = digitalFile.Path.Value;
        _digitalFileFormat = digitalFile.Format;

        if (digitalFile.ImportIdentity is PresentOptionalValue<FileImportIdentity> presentImportIdentity)
        {
            FileImportIdentity importIdentity = presentImportIdentity.Value;
            _importIdentityPath = importIdentity.Path.Value;
            _importIdentitySizeBytes = importIdentity.SizeBytes;
            _importIdentityLastModifiedAt = importIdentity.LastModifiedAt;
            _importIdentityContentHash = importIdentity.ContentHash is PresentOptionalValue<string> presentContentHash
                ? presentContentHash.Value
                : null;
        }
    }

    private IMedium CreateMedium()
    {
        return _mediumType switch
        {
            _ when _digitalFilePath is not null && _digitalFileFormat is { } format => CreateDigitalFile(format),
            _ when _vinylFormatDescription is not null => VinylRecord.Create(_mediumType, _vinylFormatDescription),
            _ when _compactDiscCount is { } discCount => CompactDisc.Create(_mediumType, discCount),
            _ when _cassetteTapeType is not null => CassetteTape.Create(_mediumType, _cassetteTapeType),
            _ when _otherMediumName is not null => OtherMedium.Create(_mediumType, _otherMediumName),
            _ => throw new InvalidOperationException("Medium payload is not valid")
        };
    }

    private DigitalFile CreateDigitalFile(AudioFileFormat format)
    {
        var path = FilePath.FromAbsolutePath(_digitalFilePath ?? throw new InvalidOperationException("Digital file path is required"));

        bool hasAnyImportIdentityField =
            _importIdentityPath is not null ||
            _importIdentitySizeBytes is not null ||
            _importIdentityLastModifiedAt is not null ||
            _importIdentityContentHash is not null;

        if (!hasAnyImportIdentityField)
        {
            return DigitalFile.Create(_mediumType, path, format);
        }

        if (_importIdentityPath is null || _importIdentitySizeBytes is null || _importIdentityLastModifiedAt is null)
        {
            throw new InvalidOperationException("Digital file import identity payload is not valid");
        }

        var identityPath = FilePath.FromAbsolutePath(_importIdentityPath);
        FileImportIdentity identity = _importIdentityContentHash is null
            ? FileImportIdentity.Create(identityPath, _importIdentitySizeBytes.Value, _importIdentityLastModifiedAt.Value)
            : FileImportIdentity.Create(identityPath, _importIdentitySizeBytes.Value, _importIdentityLastModifiedAt.Value, _importIdentityContentHash);

        return DigitalFile.Create(_mediumType, path, format, identity);
    }

    private void ClearMediumDetails()
    {
        _digitalFilePath = null;
        _digitalFileFormat = null;
        _importIdentityPath = null;
        _importIdentitySizeBytes = null;
        _importIdentityLastModifiedAt = null;
        _importIdentityContentHash = null;
        _vinylFormatDescription = null;
        _compactDiscCount = null;
        _cassetteTapeType = null;
        _otherMediumName = null;
    }
}
