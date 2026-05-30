using DiscWeave.Api.Features.Releases;
using DiscWeave.Api.Features.Settings;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;

namespace DiscWeave.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static void RestoreNamingProfiles(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IReadOnlyList<NamingProfileResponse> profiles)
    {
        foreach (NamingProfileResponse response in profiles)
        {
            var profile = NamingProfile.Create(
                collectionId,
                new NamingProfileId(response.Id),
                (
                    response.Name,
                    response.ReleaseFolderTemplate,
                    response.TrackFileTemplate,
                    response.TrackFileWithArtistTemplate,
                    response.SortOrder,
                    response.IsDefault,
                    response.IsBuiltin));
            _ = context.NamingProfiles.Add(profile);
            context.Entry(profile).Property(item => item.IsActive).CurrentValue = response.IsActive;
        }
    }

    private static void RestoreTagRoleMappings(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IReadOnlyList<TagRoleMappingResponse> mappings)
    {
        foreach (TagRoleMappingResponse response in mappings)
        {
            var mapping = TagRoleMapping.Create(
                collectionId,
                new TagRoleMappingId(response.Id),
                response.CreditRoleCode,
                response.TagField,
                response.SortOrder,
                response.IsActive,
                response.IsBuiltin);
            _ = context.TagRoleMappings.Add(mapping);
        }
    }

    private static void RestoreReleaseNamingOverrides(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        IReadOnlyList<ReleaseNamingOverrideResponse> overrides)
    {
        foreach (ReleaseNamingOverrideResponse response in overrides)
        {
            var overrideEntry = ReleaseNamingOverride.Create(collectionId, new ReleaseId(response.ReleaseId));
            overrideEntry.Update(
                OptionalNamingProfile(response.NamingProfileId),
                OptionalText(response.ReleaseFolderTemplate),
                OptionalText(response.TrackFileTemplate),
                OptionalText(response.TrackFileWithArtistTemplate),
                OptionalText(response.Source));
            _ = context.ReleaseNamingOverrides.Add(overrideEntry);
        }
    }

    private static IOptionalValue<NamingProfileId> OptionalNamingProfile(Guid? value)
    {
        return value.HasValue
            ? Optional.From(new NamingProfileId(value.Value))
            : Optional.Missing<NamingProfileId>();
    }
}
