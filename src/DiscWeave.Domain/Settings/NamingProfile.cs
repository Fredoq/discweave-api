using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Settings;

public sealed class NamingProfile : IEntity<NamingProfileId>
{
    private NamingProfile()
    {
    }

    private NamingProfile(
        CollectionId collectionId,
        NamingProfileId id,
        (
            string Name,
            string ReleaseFolderTemplate,
            string TrackFileTemplate,
            string TrackFileWithArtistTemplate,
            int SortOrder,
            bool IsDefault,
            bool IsBuiltin) definition)
    {
        CollectionId = collectionId;
        Id = id;
        Name = Guard.RequiredText(definition.Name, nameof(definition.Name), "naming_profile.name_required");
        ReleaseFolderTemplate = NamingTemplateValidator.Validate(definition.ReleaseFolderTemplate, NamingTemplateKind.ReleaseFolder);
        TrackFileTemplate = NamingTemplateValidator.Validate(definition.TrackFileTemplate, NamingTemplateKind.TrackFile);
        TrackFileWithArtistTemplate = NamingTemplateValidator.Validate(definition.TrackFileWithArtistTemplate, NamingTemplateKind.TrackFileWithArtist);
        SortOrder = RequiredSortOrder(definition.SortOrder);
        IsDefault = definition.IsDefault;
        IsActive = true;
        IsBuiltin = definition.IsBuiltin;
    }

    public NamingProfileId Id { get; private set; }

    public CollectionId CollectionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ReleaseFolderTemplate { get; private set; } = string.Empty;

    public string TrackFileTemplate { get; private set; } = string.Empty;

    public string TrackFileWithArtistTemplate { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsBuiltin { get; private set; }

    public static NamingProfile Create(
        CollectionId collectionId,
        NamingProfileId id,
        (
            string Name,
            string ReleaseFolderTemplate,
            string TrackFileTemplate,
            string TrackFileWithArtistTemplate,
            int SortOrder,
            bool IsDefault,
            bool IsBuiltin) definition)
    {
        return new NamingProfile(
            collectionId,
            id,
            definition);
    }

    public void Update(
        string name,
        string releaseFolderTemplate,
        string trackFileTemplate,
        string trackFileWithArtistTemplate,
        int sortOrder,
        bool isDefault,
        bool isActive)
    {
        if (IsBuiltin)
        {
            throw new DomainException("naming_profile.builtin_immutable", "Built-in naming profiles cannot be edited");
        }

        Name = Guard.RequiredText(name, nameof(name), "naming_profile.name_required");
        ReleaseFolderTemplate = NamingTemplateValidator.Validate(releaseFolderTemplate, NamingTemplateKind.ReleaseFolder);
        TrackFileTemplate = NamingTemplateValidator.Validate(trackFileTemplate, NamingTemplateKind.TrackFile);
        TrackFileWithArtistTemplate = NamingTemplateValidator.Validate(trackFileWithArtistTemplate, NamingTemplateKind.TrackFileWithArtist);
        SortOrder = RequiredSortOrder(sortOrder);
        IsDefault = isDefault;
        IsActive = isActive;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }

    public void SyncBuiltinDefaults(
        string name,
        string releaseFolderTemplate,
        string trackFileTemplate,
        string trackFileWithArtistTemplate,
        int sortOrder)
    {
        if (!IsBuiltin)
        {
            throw new DomainException("naming_profile.not_builtin", "Only built-in naming profiles can be synchronized");
        }

        Name = Guard.RequiredText(name, nameof(name), "naming_profile.name_required");
        ReleaseFolderTemplate = NamingTemplateValidator.Validate(releaseFolderTemplate, NamingTemplateKind.ReleaseFolder);
        TrackFileTemplate = NamingTemplateValidator.Validate(trackFileTemplate, NamingTemplateKind.TrackFile);
        TrackFileWithArtistTemplate = NamingTemplateValidator.Validate(trackFileWithArtistTemplate, NamingTemplateKind.TrackFileWithArtist);
        SortOrder = RequiredSortOrder(sortOrder);
        IsActive = true;
    }

    public void EnsureCanDelete()
    {
        if (IsBuiltin)
        {
            throw new DomainException("naming_profile.builtin_immutable", "Built-in naming profiles cannot be deleted");
        }
    }

    private static int RequiredSortOrder(int sortOrder)
    {
        return sortOrder < 0
            ? throw new DomainException("naming_profile.sort_order_invalid", "Naming profile sort order cannot be negative")
            : sortOrder;
    }
}
