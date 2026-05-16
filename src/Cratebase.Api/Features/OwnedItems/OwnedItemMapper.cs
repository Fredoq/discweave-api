using Cratebase.Domain.Collection;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.OwnedItems;

internal static class OwnedItemMapper
{
    private const string OtherTypeCode = "other";

    public static OwnedItemTarget CreateTarget(string? targetType, Guid targetId)
    {
        return Required(targetType, "owned_item.target_type_required").Trim() switch
        {
            "release" => OwnedItemTarget.ForRelease(new ReleaseId(targetId)),
            "track" => OwnedItemTarget.ForTrack(new TrackId(targetId)),
            _ => throw new DomainException("owned_item.target_type_invalid", "Owned item target type is invalid")
        };
    }

    public static OwnedItemHolding CreateHolding(IMedium medium, string status, string? condition, string? storageLocation)
    {
        var holding = OwnedItemHolding.Create(ParseOwnershipStatus(status), medium);
        OwnedItemDetails details = OwnedItemDetails.Empty;

        if (!string.IsNullOrWhiteSpace(condition))
        {
            details = details.WithCondition(ParseItemCondition(condition));
        }

        if (!string.IsNullOrWhiteSpace(storageLocation))
        {
            details = details.WithStorageLocation(StorageLocation.FromName(storageLocation));
        }

        return holding.WithDetails(details);
    }

    public static IMedium CreateMedium(MediumRequest request, CollectionDictionaryEntry mediaEntry)
    {
        string code = mediaEntry.Code;
        string profile = mediaEntry.MediaProfile is PresentOptionalValue<string> presentProfile
            ? presentProfile.Value
            : throw new DomainException("medium.profile_invalid", "Medium profile is invalid");

        return profile switch
        {
            "digital" => DigitalFile.Create(
                code,
                FilePath.FromAbsolutePath(Required(request.Path, "medium.path_required")),
                ParseAudioFileFormat(Required(request.Format, "medium.format_required"))),
            "vinyl" => VinylRecord.Create(code, Required(request.Description, "medium.description_required")),
            "cd" => CompactDisc.Create(code, request.DiscCount ?? 1),
            "cassette" => CassetteTape.Create(code, Required(request.Description, "medium.description_required")),
            OtherTypeCode => OtherMedium.Create(code, Required(request.Description, "medium.description_required")),
            _ => throw new DomainException("medium.profile_invalid", "Medium profile is invalid")
        };
    }

    public static OwnedItemResponse ToResponse(OwnedItem item)
    {
        OwnedItemHolding holding = item.Holding;
        OwnedItemTarget target = item.Target;

        return new OwnedItemResponse(
            item.Id.Value,
            target is ReleaseOwnedItemTarget ? "release" : "track",
            target is ReleaseOwnedItemTarget release ? release.ReleaseId.Value : ((TrackOwnedItemTarget)target).TrackId.Value,
            ToOwnershipStatusCode(holding.Status),
            ToMediumResponse(holding.Medium),
            holding.Details.Condition.HasValue ? holding.Details.Condition.Match(ToItemConditionCode, () => string.Empty) : null,
            holding.Details.StorageLocation.HasValue ? holding.Details.StorageLocation.Match(location => location.Name, () => string.Empty) : null);
    }

    public static bool TryParseOwnershipStatus(string status, out OwnershipStatus ownershipStatus)
    {
        switch (status.Trim())
        {
            case "owned":
                ownershipStatus = OwnershipStatus.Owned;
                return true;
            case "wanted":
                ownershipStatus = OwnershipStatus.Wanted;
                return true;
            case "sold":
                ownershipStatus = OwnershipStatus.Sold;
                return true;
            case "needsDigitization":
                ownershipStatus = OwnershipStatus.NeedsDigitization;
                return true;
            default:
                ownershipStatus = default;
                return false;
        }
    }

    public static OwnershipStatus ParseOwnershipStatus(string status)
    {
        return Required(status, "owned_item.status_required").Trim() switch
        {
            "owned" => OwnershipStatus.Owned,
            "wanted" => OwnershipStatus.Wanted,
            "sold" => OwnershipStatus.Sold,
            "needsDigitization" => OwnershipStatus.NeedsDigitization,
            _ => throw new DomainException("owned_item.status_invalid", "Owned item status is invalid")
        };
    }

    private static MediumResponse ToMediumResponse(IMedium medium)
    {
        return medium switch
        {
            DigitalFile digitalFile => new MediumResponse(digitalFile.Code, digitalFile.Description, digitalFile.Path.Value, ToAudioFileFormatCode(digitalFile.Format), null),
            VinylRecord vinylRecord => new MediumResponse(vinylRecord.Code, vinylRecord.FormatDescription, null, null, null),
            CompactDisc compactDisc => new MediumResponse(compactDisc.Code, compactDisc.Description, null, null, compactDisc.DiscCount),
            CassetteTape cassetteTape => new MediumResponse(cassetteTape.Code, cassetteTape.TapeType, null, null, null),
            OtherMedium otherMedium => new MediumResponse(otherMedium.Code, otherMedium.Name, null, null, null),
            _ => throw new InvalidOperationException("Medium type is not supported")
        };
    }

    private static ItemCondition ParseItemCondition(string condition)
    {
        return Required(condition, "owned_item.condition_required").Trim() switch
        {
            "mint" => ItemCondition.Mint,
            "nearMint" => ItemCondition.NearMint,
            "veryGoodPlus" => ItemCondition.VeryGoodPlus,
            "veryGood" => ItemCondition.VeryGood,
            "good" => ItemCondition.Good,
            "fair" => ItemCondition.Fair,
            "poor" => ItemCondition.Poor,
            _ => throw new DomainException("owned_item.condition_invalid", "Owned item condition is invalid")
        };
    }

    private static AudioFileFormat ParseAudioFileFormat(string format)
    {
        return Required(format, "medium.format_required").Trim() switch
        {
            "flac" => AudioFileFormat.Flac,
            "mp3" => AudioFileFormat.Mp3,
            "ogg" => AudioFileFormat.Ogg,
            "wav" => AudioFileFormat.Wav,
            "aiff" => AudioFileFormat.Aiff,
            "alac" => AudioFileFormat.Alac,
            "m4a" => AudioFileFormat.M4a,
            _ => throw new DomainException("digital_file.format_invalid", "Digital file format is invalid")
        };
    }

    private static string ToOwnershipStatusCode(OwnershipStatus status)
    {
        return status switch
        {
            OwnershipStatus.Owned => "owned",
            OwnershipStatus.Wanted => "wanted",
            OwnershipStatus.Sold => "sold",
            OwnershipStatus.NeedsDigitization => "needsDigitization",
            _ => throw new InvalidOperationException("Ownership status is not supported")
        };
    }

    private static string ToItemConditionCode(ItemCondition condition)
    {
        return condition switch
        {
            ItemCondition.Mint => "mint",
            ItemCondition.NearMint => "nearMint",
            ItemCondition.VeryGoodPlus => "veryGoodPlus",
            ItemCondition.VeryGood => "veryGood",
            ItemCondition.Good => "good",
            ItemCondition.Fair => "fair",
            ItemCondition.Poor => "poor",
            _ => throw new InvalidOperationException("Item condition is not supported")
        };
    }

    private static string ToAudioFileFormatCode(AudioFileFormat format)
    {
        return format switch
        {
            AudioFileFormat.Flac => "flac",
            AudioFileFormat.Mp3 => "mp3",
            AudioFileFormat.Ogg => "ogg",
            AudioFileFormat.Wav => "wav",
            AudioFileFormat.Aiff => "aiff",
            AudioFileFormat.Alac => "alac",
            AudioFileFormat.M4a => "m4a",
            _ => throw new InvalidOperationException("Audio file format is not supported")
        };
    }

    private static string Required(string? value, string code)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new DomainException(code, "Required value is missing")
            : value;
    }
}
