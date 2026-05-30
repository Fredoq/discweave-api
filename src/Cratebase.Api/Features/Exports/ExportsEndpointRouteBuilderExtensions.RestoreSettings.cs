using Cratebase.Api.Features.Releases;
using Cratebase.Api.Features.Settings;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Cratebase.Infrastructure.Persistence;

namespace Cratebase.Api.Features.Exports;

public static partial class ExportsEndpointRouteBuilderExtensions
{
    private static void RestoreNamingProfiles(
        CratebaseDbContext context,
        CollectionId collectionId,
        IReadOnlyList<NamingProfileResponse> profiles)
    {
        foreach (NamingProfileResponse response in profiles)
        {
            var profile = NamingProfile.Create(
                collectionId,
                new NamingProfileId(response.Id),
                response.Name,
                response.ReleaseFolderTemplate,
                response.TrackFileTemplate,
                response.TrackFileWithArtistTemplate,
                response.SortOrder,
                response.IsDefault,
                response.IsBuiltin);
            _ = context.NamingProfiles.Add(profile);
            context.Entry(profile).Property(item => item.IsActive).CurrentValue = response.IsActive;
        }
    }

    private static void RestoreTagRoleMappings(
        CratebaseDbContext context,
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
        CratebaseDbContext context,
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
