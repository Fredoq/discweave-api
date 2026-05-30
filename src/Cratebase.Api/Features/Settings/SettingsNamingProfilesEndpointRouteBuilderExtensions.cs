using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Errors;
using Cratebase.Application.Security;
using Cratebase.Domain.Settings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Settings;

public static class SettingsNamingProfilesEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSettingsNamingProfilesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/settings/naming-profiles")
            .WithTags("Settings")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListNamingProfilesAsync).WithName("ListNamingProfiles");
        _ = group.MapPost("", CreateNamingProfileAsync).WithName("CreateNamingProfile");
        _ = group.MapPut("/{profileId:guid}", UpdateNamingProfileAsync).WithName("UpdateNamingProfile");
        _ = group.MapDelete("/{profileId:guid}", DeleteNamingProfileAsync).WithName("DeleteNamingProfile");

        return endpoints;
    }

    private static async Task<IResult> ListNamingProfilesAsync(
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await NamingProfileDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);
        NamingProfile[] profiles = await context.NamingProfiles.AsNoTracking()
            .Where(profile => profile.CollectionId == currentCollection.CollectionId)
            .OrderBy(profile => profile.SortOrder)
            .ThenBy(profile => profile.Name)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<NamingProfileResponse>([.. profiles.Select(ToResponse)], profiles.Length, 0, profiles.Length));
    }

    private static async Task<IResult> CreateNamingProfileAsync(
        NamingProfileRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await NamingProfileDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);

        try
        {
            var profile = NamingProfile.Create(
                currentCollection.CollectionId,
                NamingProfileId.New(),
                request.Name,
                request.ReleaseFolderTemplate,
                request.TrackFileTemplate,
                request.TrackFileWithArtistTemplate,
                request.SortOrder ?? 100,
                request.IsDefault == true,
                isBuiltin: false);
            if (request.IsActive == false)
            {
                profile.Update(
                    profile.Name,
                    profile.ReleaseFolderTemplate,
                    profile.TrackFileTemplate,
                    profile.TrackFileWithArtistTemplate,
                    profile.SortOrder,
                    profile.IsDefault,
                    isActive: false);
            }

            _ = context.NamingProfiles.Add(profile);
            await ApplyDefaultSelectionAsync(context, currentCollection.CollectionId, profile, cancellationToken);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/settings/naming-profiles/{profile.Id.Value}", ToResponse(profile));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ResourceConflictException)
        {
            return EndpointErrors.Conflict("naming_profile.conflict", "Naming profile already exists");
        }
    }

    private static async Task<IResult> UpdateNamingProfileAsync(
        Guid profileId,
        NamingProfileRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await NamingProfileDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);
        NamingProfile? profile = await FindProfileAsync(context, currentCollection.CollectionId, profileId, cancellationToken);
        if (profile is null)
        {
            return EndpointErrors.NotFound("naming_profile.not_found", "Naming profile was not found");
        }

        try
        {
            profile.Update(
                request.Name,
                request.ReleaseFolderTemplate,
                request.TrackFileTemplate,
                request.TrackFileWithArtistTemplate,
                request.SortOrder ?? profile.SortOrder,
                request.IsDefault ?? profile.IsDefault,
                request.IsActive ?? profile.IsActive);
            await ApplyDefaultSelectionAsync(context, currentCollection.CollectionId, profile, cancellationToken);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToResponse(profile));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ResourceConflictException)
        {
            return EndpointErrors.Conflict("naming_profile.conflict", "Naming profile already exists");
        }
    }

    private static async Task<IResult> DeleteNamingProfileAsync(
        Guid profileId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "naming-profile", profileId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        await NamingProfileDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);
        NamingProfile? profile = await FindProfileAsync(context, currentCollection.CollectionId, profileId, cancellationToken);
        if (profile is null)
        {
            return EndpointErrors.NotFound("naming_profile.not_found", "Naming profile was not found");
        }

        try
        {
            profile.EnsureCanDelete();
            bool wasDefault = profile.IsDefault;
            _ = context.NamingProfiles.Remove(profile);
            if (wasDefault)
            {
                NamingProfile? fallback = await context.NamingProfiles
                    .Where(candidate => candidate.CollectionId == currentCollection.CollectionId && candidate.Id != profile.Id && candidate.IsActive)
                    .OrderBy(candidate => candidate.SortOrder)
                    .FirstOrDefaultAsync(cancellationToken);
                fallback?.SetDefault(true);
            }

            _ = await context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("naming_profile.delete_conflict", "Naming profile has dependent release overrides");
        }
    }

    private static async Task<NamingProfile?> FindProfileAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Guid profileId,
        CancellationToken cancellationToken)
    {
        return await context.NamingProfiles.SingleOrDefaultAsync(
            profile => profile.CollectionId == collectionId && profile.Id == new NamingProfileId(profileId),
            cancellationToken);
    }

    private static async Task ApplyDefaultSelectionAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        NamingProfile selectedProfile,
        CancellationToken cancellationToken)
    {
        if (!selectedProfile.IsDefault)
        {
            return;
        }

        NamingProfile[] defaults = await context.NamingProfiles
            .Where(profile => profile.CollectionId == collectionId && profile.Id != selectedProfile.Id && profile.IsDefault)
            .ToArrayAsync(cancellationToken);
        foreach (NamingProfile profile in defaults)
        {
            profile.SetDefault(false);
        }
    }

    private static NamingProfileResponse ToResponse(NamingProfile profile)
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
}
