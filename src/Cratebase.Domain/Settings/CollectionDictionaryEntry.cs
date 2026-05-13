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
        return FromCreation(
            new EntryCreation
            {
                Id = id,
                CollectionId = collectionId,
                Kind = kind,
                Code = code,
                Name = name,
                SortOrder = sortOrder,
                IsBuiltin = isBuiltin,
                MediaProfile = Optional.Missing<string>()
            });
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
        return FromCreation(
            new EntryCreation
            {
                Id = id,
                CollectionId = collectionId,
                Kind = DictionaryKind.MediaType,
                Code = code,
                Name = name,
                SortOrder = sortOrder,
                IsBuiltin = isBuiltin,
                MediaProfile = Optional.From(Guard.RequiredText(mediaProfile, nameof(mediaProfile), "dictionary_entry.media_profile_required"))
            });
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

    private static CollectionDictionaryEntry FromCreation(EntryCreation creation)
    {
        DictionaryKind kind = Guard.DefinedEnum(creation.Kind, nameof(creation.Kind), "dictionary_entry.kind_invalid");
        EnsureMediaProfileMatchesKind(kind, creation.MediaProfile);

        var entry = new CollectionDictionaryEntry
        {
            Id = creation.Id,
            CollectionId = creation.CollectionId,
            Kind = kind,
            Code = Guard.RequiredText(creation.Code, nameof(creation.Code), "dictionary_entry.code_required"),
            Name = Guard.RequiredText(creation.Name, nameof(creation.Name), "dictionary_entry.name_required"),
            SortOrder = creation.SortOrder,
            IsActive = true,
            IsBuiltin = creation.IsBuiltin
        };
        entry.SetMediaProfile(creation.MediaProfile);

        return entry;
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

    private static void EnsureMediaProfileMatchesKind(DictionaryKind kind, IOptionalValue<string> mediaProfile)
    {
        if (kind == DictionaryKind.MediaType)
        {
            if (mediaProfile is not PresentOptionalValue<string> presentMediaProfile || string.IsNullOrWhiteSpace(presentMediaProfile.Value))
            {
                throw new DomainException("dictionary_entry.media_profile_required", "Media type entries require a media profile");
            }

            return;
        }

        if (mediaProfile is PresentOptionalValue<string>)
        {
            throw new DomainException("dictionary_entry.media_profile_invalid", "Only media type entries can have a media profile");
        }
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

    private sealed class EntryCreation
    {
        public CollectionDictionaryEntryId Id { get; init; }

        public CollectionId CollectionId { get; init; }

        public DictionaryKind Kind { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public int SortOrder { get; init; }

        public bool IsBuiltin { get; init; }

        public IOptionalValue<string> MediaProfile { get; init; } = Optional.Missing<string>();
    }
}
