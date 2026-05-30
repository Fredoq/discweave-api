using DiscWeave.Api.Features.Releases;
using DiscWeave.Api.Features.Settings;
using DiscWeave.Domain.Settings;

namespace DiscWeave.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static NamingProfileResponse ToNamingProfileResponse(NamingProfile profile)
    {
        return new NamingProfileResponse(
            profile.Id.Value,
            profile.Name,
            profile.ReleaseFolderTemplate,
            profile.TrackFileTemplate,
            profile.TrackFileWithArtistTemplate,
            profile.SortOrder,
            profile.IsDefault,
            profile.IsActive,
            profile.IsBuiltin);
    }

    private static ReleaseNamingOverrideResponse ToReleaseNamingOverrideResponse(ReleaseNamingOverride overrideEntry)
    {
        return new ReleaseNamingOverrideResponse(
            overrideEntry.ReleaseId.Value,
            OptionalGuid(overrideEntry.NamingProfileId),
            OptionalString(overrideEntry.ReleaseFolderTemplate),
            OptionalString(overrideEntry.TrackFileTemplate),
            OptionalString(overrideEntry.TrackFileWithArtistTemplate),
            OptionalString(overrideEntry.Source));
    }

    private static TagRoleMappingResponse ToTagRoleMappingResponse(TagRoleMapping mapping)
    {
        return new TagRoleMappingResponse(
            mapping.Id.Value,
            mapping.CreditRoleCode,
            mapping.TagField,
            mapping.SortOrder,
            mapping.IsActive,
            mapping.IsBuiltin);
    }
}
