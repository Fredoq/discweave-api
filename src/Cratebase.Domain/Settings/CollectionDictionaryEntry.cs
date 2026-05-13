using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Settings;

public sealed class CollectionDictionaryEntry : IEntity<CollectionDictionaryEntryId>
{
    private const string ProtectedCode = "dictionary_entry.protected";
    private const string ProtectedMessage = "Protected dictionary entry cannot be disabled or deleted";

    private string? _mediaProfile;

    private CollectionDictionaryEntry()
    {
    }

    private CollectionDictionaryEntry(
        CollectionDictionaryEntryId id,
        CollectionId collectionId,
        DictionaryKind kind,
        string code,
        string name,
        int sortOrder,
        bool isActive,
        bool isBuiltin,
        IOptionalValue<string> mediaProfile)
    {
        Id = id;
        CollectionId = collectionId;
        Kind = Guard.DefinedEnum(kind, nameof(kind), "dictionary_entry.kind_invalid");
        Code = Guard.RequiredText(code, nameof(code), "dictionary_entry.code_required");
        Name = Guard.RequiredText(name, nameof(name), "dictionary_entry.name_required");
        SortOrder = sortOrder;
        IsActive = isActive;
        IsBuiltin = isBuiltin;
        SetMediaProfile(mediaProfile);
    }

    public CollectionDictionaryEntryId Id { get; private set; }

    public CollectionId CollectionId { get; private set; }

    public DictionaryKind Kind { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsBuiltin { get; private set; }

    public bool IsProtected => IsProtectedCode(Kind, Code);

    public IOptionalValue<string> MediaProfile => _mediaProfile is null
        ? Optional.Missing<string>()
        : Optional.From(_mediaProfile);

    public static CollectionDictionaryEntry Create(
        CollectionDictionaryEntryId id,
        CollectionId collectionId,
        DictionaryKind kind,
        string code,
        string name,
        int sortOrder,
        bool isBuiltin)
    {
        return new CollectionDictionaryEntry(
            id,
            collectionId,
            kind,
            code,
            name,
            sortOrder,
            isActive: true,
            isBuiltin,
            Optional.Missing<string>());
    }

    public static CollectionDictionaryEntry CreateMedia(
        CollectionDictionaryEntryId id,
        CollectionId collectionId,
        string code,
        string name,
        int sortOrder,
        bool isBuiltin,
        string mediaProfile)
    {
        return new CollectionDictionaryEntry(
            id,
            collectionId,
            DictionaryKind.MediaType,
            code,
            name,
            sortOrder,
            isActive: true,
            isBuiltin,
            Optional.From(Guard.RequiredText(mediaProfile, nameof(mediaProfile), "dictionary_entry.media_profile_required")));
    }

    public void Rename(string name)
    {
        Name = Guard.RequiredText(name, nameof(name), "dictionary_entry.name_required");
    }

    public void Reorder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        if (IsProtected)
        {
            throw new DomainException(ProtectedCode, ProtectedMessage);
        }

        IsActive = false;
    }

    public void EnsureCanDelete()
    {
        if (IsProtected)
        {
            throw new DomainException(ProtectedCode, ProtectedMessage);
        }
    }

    public void UpdateMediaProfile(string mediaProfile)
    {
        if (Kind != DictionaryKind.MediaType)
        {
            throw new DomainException("dictionary_entry.media_profile_invalid", "Only media type entries can have a media profile");
        }

        _mediaProfile = ValidateMediaProfile(mediaProfile);
    }

    private void SetMediaProfile(IOptionalValue<string> mediaProfile)
    {
        if (mediaProfile is PresentOptionalValue<string> presentMediaProfile)
        {
            _mediaProfile = ValidateMediaProfile(presentMediaProfile.Value);
            return;
        }

        _mediaProfile = null;
    }

    private static bool IsProtectedCode(DictionaryKind kind, string code)
    {
        return (kind, code) is
            (DictionaryKind.ReleaseType, "unknown") or
            (DictionaryKind.CreditRole, "mainArtist") or
            (DictionaryKind.MediaType, "digital") or
            (DictionaryKind.MediaType, "other");
    }

    private static string ValidateMediaProfile(string mediaProfile)
    {
        string normalized = Guard.RequiredText(mediaProfile, nameof(mediaProfile), "dictionary_entry.media_profile_required");
        return normalized is "digital" or "vinyl" or "cd" or "cassette" or "other"
            ? normalized
            : throw new DomainException("dictionary_entry.media_profile_invalid", "Media profile is invalid");
    }
}
