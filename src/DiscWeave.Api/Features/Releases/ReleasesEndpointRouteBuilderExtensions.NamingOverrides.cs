using DiscWeave.Api.Http;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Releases;

public static partial class ReleasesEndpointRouteBuilderExtensions
{
    private static async Task<IResult> GetReleaseNamingOverrideAsync(
        Guid releaseId,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ReleaseNamingOverride? overrideEntry = await context.ReleaseNamingOverrides.AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.CollectionId == currentCollection.CollectionId && entry.ReleaseId == new ReleaseId(releaseId),
                cancellationToken);

        return overrideEntry is null
            ? EndpointErrors.NotFound("release_naming_override.not_found", "Release naming override was not found")
            : Results.Ok(ToResponse(overrideEntry));
    }

    private static async Task<IResult> PutReleaseNamingOverrideAsync(
        Guid releaseId,
        ReleaseNamingOverrideRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        bool releaseExists = await context.Releases.AsNoTracking()
            .AnyAsync(release => release.CollectionId == currentCollection.CollectionId && release.Id == new ReleaseId(releaseId), cancellationToken);
        if (!releaseExists)
        {
            return EndpointErrors.NotFound("release.not_found", "Release was not found");
        }

        if (request.NamingProfileId is { } profileId)
        {
            bool profileExists = await context.NamingProfiles.AsNoTracking()
                .AnyAsync(profile => profile.CollectionId == currentCollection.CollectionId && profile.Id == new NamingProfileId(profileId), cancellationToken);
            if (!profileExists)
            {
                return EndpointErrors.BadRequest("naming_profile.not_found", "Naming profile was not found");
            }
        }

        ReleaseNamingOverride? overrideEntry = await context.ReleaseNamingOverrides
            .SingleOrDefaultAsync(
                entry => entry.CollectionId == currentCollection.CollectionId && entry.ReleaseId == new ReleaseId(releaseId),
                cancellationToken);
        if (overrideEntry is null)
        {
            overrideEntry = ReleaseNamingOverride.Create(currentCollection.CollectionId, new ReleaseId(releaseId));
            _ = context.ReleaseNamingOverrides.Add(overrideEntry);
        }

        try
        {
            overrideEntry.Update(
                ToOptionalNamingProfileId(request.NamingProfileId),
                ToOptionalString(request.ReleaseFolderTemplate),
                ToOptionalString(request.TrackFileTemplate),
                ToOptionalString(request.TrackFileWithArtistTemplate),
                ToOptionalString(request.Source));
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToResponse(overrideEntry));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteReleaseNamingOverrideAsync(
        Guid releaseId,
        HttpRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "release-naming-override", releaseId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        ReleaseNamingOverride? overrideEntry = await context.ReleaseNamingOverrides
            .SingleOrDefaultAsync(
                entry => entry.CollectionId == currentCollection.CollectionId && entry.ReleaseId == new ReleaseId(releaseId),
                cancellationToken);
        if (overrideEntry is null)
        {
            return EndpointErrors.NotFound("release_naming_override.not_found", "Release naming override was not found");
        }

        _ = context.ReleaseNamingOverrides.Remove(overrideEntry);
        _ = await context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static ReleaseNamingOverrideResponse ToResponse(ReleaseNamingOverride overrideEntry)
    {
        return new ReleaseNamingOverrideResponse(
            overrideEntry.ReleaseId.Value,
            OptionalNamingProfileId(overrideEntry.NamingProfileId),
            OptionalString(overrideEntry.ReleaseFolderTemplate),
            OptionalString(overrideEntry.TrackFileTemplate),
            OptionalString(overrideEntry.TrackFileWithArtistTemplate),
            OptionalString(overrideEntry.Source));
    }

    private static Guid? OptionalNamingProfileId(IOptionalValue<NamingProfileId> value)
    {
        return value is PresentOptionalValue<NamingProfileId> present
            ? present.Value.Value
            : null;
    }

    private static string? OptionalString(IOptionalValue<string> value)
    {
        return value is PresentOptionalValue<string> present
            ? present.Value
            : null;
    }

    private static IOptionalValue<NamingProfileId> ToOptionalNamingProfileId(Guid? value)
    {
        return value.HasValue
            ? Optional.From(new NamingProfileId(value.Value))
            : Optional.Missing<NamingProfileId>();
    }
}
