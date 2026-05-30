using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Optional;

namespace DiscWeave.Domain.Settings;

public sealed class ReleaseNamingOverride : IEntity<ReleaseId>
{
    private NamingProfileId? _namingProfileId;
    private string? _releaseFolderTemplate;
    private string? _trackFileTemplate;
    private string? _trackFileWithArtistTemplate;
    private string? _source;

    private ReleaseNamingOverride()
    {
    }

    private ReleaseNamingOverride(CollectionId collectionId, ReleaseId releaseId)
    {
        CollectionId = collectionId;
        ReleaseId = releaseId;
    }

    public CollectionId CollectionId { get; private set; }

    public ReleaseId Id => ReleaseId;

    public ReleaseId ReleaseId { get; private set; }

    public IOptionalValue<NamingProfileId> NamingProfileId => _namingProfileId.HasValue
        ? Optional.From(_namingProfileId.Value)
        : Optional.Missing<NamingProfileId>();

    public IOptionalValue<string> ReleaseFolderTemplate => OptionalText(_releaseFolderTemplate);

    public IOptionalValue<string> TrackFileTemplate => OptionalText(_trackFileTemplate);

    public IOptionalValue<string> TrackFileWithArtistTemplate => OptionalText(_trackFileWithArtistTemplate);

    public IOptionalValue<string> Source => OptionalText(_source);

    public static ReleaseNamingOverride Create(CollectionId collectionId, ReleaseId releaseId)
    {
        return new ReleaseNamingOverride(collectionId, releaseId);
    }

    public void Update(
        IOptionalValue<NamingProfileId> namingProfileId,
        IOptionalValue<string> releaseFolderTemplate,
        IOptionalValue<string> trackFileTemplate,
        IOptionalValue<string> trackFileWithArtistTemplate,
        IOptionalValue<string> source)
    {
        ArgumentNullException.ThrowIfNull(namingProfileId);
        ArgumentNullException.ThrowIfNull(releaseFolderTemplate);
        ArgumentNullException.ThrowIfNull(trackFileTemplate);
        ArgumentNullException.ThrowIfNull(trackFileWithArtistTemplate);
        ArgumentNullException.ThrowIfNull(source);

        _namingProfileId = OptionalValueOrNull(namingProfileId);
        _releaseFolderTemplate = OptionalTemplate(releaseFolderTemplate, NamingTemplateKind.ReleaseFolder);
        _trackFileTemplate = OptionalTemplate(trackFileTemplate, NamingTemplateKind.TrackFile);
        _trackFileWithArtistTemplate = OptionalTemplate(trackFileWithArtistTemplate, NamingTemplateKind.TrackFileWithArtist);
        _source = OptionalTextOrNull(source);
    }

    private static string? OptionalTemplate(IOptionalValue<string> template, NamingTemplateKind kind)
    {
        string? value = OptionalTextOrNull(template);

        return value is null
            ? null
            : NamingTemplateValidator.Validate(value, kind);
    }

    private static IOptionalValue<string> OptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Optional.Missing<string>()
            : Optional.From(value.Trim());
    }

    private static string? OptionalTextOrNull(IOptionalValue<string> value)
    {
        return value is PresentOptionalValue<string> present && !string.IsNullOrWhiteSpace(present.Value)
            ? present.Value.Trim()
            : null;
    }

    private static NamingProfileId? OptionalValueOrNull(IOptionalValue<NamingProfileId> value)
    {
        return value is PresentOptionalValue<NamingProfileId> present ? present.Value : null;
    }
}
