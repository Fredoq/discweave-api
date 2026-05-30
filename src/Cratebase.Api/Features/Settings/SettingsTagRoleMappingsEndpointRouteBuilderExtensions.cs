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

public static class SettingsTagRoleMappingsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSettingsTagRoleMappingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/settings/tag-role-mappings")
            .WithTags("Settings")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListTagRoleMappingsAsync).WithName("ListTagRoleMappings");
        _ = group.MapPost("", CreateTagRoleMappingAsync).WithName("CreateTagRoleMapping");
        _ = group.MapPut("/{mappingId:guid}", UpdateTagRoleMappingAsync).WithName("UpdateTagRoleMapping");
        _ = group.MapDelete("/{mappingId:guid}", DeleteTagRoleMappingAsync).WithName("DeleteTagRoleMapping");

        return endpoints;
    }

    private static async Task<IResult> ListTagRoleMappingsAsync(
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await TagRoleMappingDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);
        TagRoleMapping[] mappings = await context.TagRoleMappings.AsNoTracking()
            .Where(mapping => mapping.CollectionId == currentCollection.CollectionId)
            .OrderBy(mapping => mapping.SortOrder)
            .ThenBy(mapping => mapping.CreditRoleCode)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<TagRoleMappingResponse>([.. mappings.Select(ToResponse)], mappings.Length, 0, mappings.Length));
    }

    private static async Task<IResult> CreateTagRoleMappingAsync(
        TagRoleMappingRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await TagRoleMappingDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);

        try
        {
            string roleCode = await DictionaryValidation.RequireActiveCodeAsync(
                context,
                currentCollection.CollectionId,
                DictionaryKind.CreditRole,
                request.CreditRoleCode,
                "tag_role_mapping.role_invalid",
                "Credit role is invalid",
                cancellationToken);
            var mapping = TagRoleMapping.Create(
                currentCollection.CollectionId,
                TagRoleMappingId.New(),
                roleCode,
                request.TagField,
                request.SortOrder ?? 100,
                request.IsActive ?? true,
                isBuiltin: false);

            _ = context.TagRoleMappings.Add(mapping);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/settings/tag-role-mappings/{mapping.Id.Value}", ToResponse(mapping));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ResourceConflictException)
        {
            return EndpointErrors.Conflict("tag_role_mapping.conflict", "Tag role mapping already exists");
        }
    }

    private static async Task<IResult> UpdateTagRoleMappingAsync(
        Guid mappingId,
        TagRoleMappingRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await TagRoleMappingDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);
        TagRoleMapping? mapping = await FindMappingAsync(context, currentCollection.CollectionId, mappingId, cancellationToken);
        if (mapping is null)
        {
            return EndpointErrors.NotFound("tag_role_mapping.not_found", "Tag role mapping was not found");
        }

        try
        {
            string roleCode = await DictionaryValidation.RequireActiveCodeAsync(
                context,
                currentCollection.CollectionId,
                DictionaryKind.CreditRole,
                request.CreditRoleCode,
                "tag_role_mapping.role_invalid",
                "Credit role is invalid",
                cancellationToken);
            mapping.Update(
                roleCode,
                request.TagField,
                request.SortOrder ?? mapping.SortOrder,
                request.IsActive ?? mapping.IsActive);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToResponse(mapping));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
        catch (ResourceConflictException)
        {
            return EndpointErrors.Conflict("tag_role_mapping.conflict", "Tag role mapping already exists");
        }
    }

    private static async Task<IResult> DeleteTagRoleMappingAsync(
        Guid mappingId,
        HttpRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "tag-role-mapping", mappingId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        await TagRoleMappingDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);
        TagRoleMapping? mapping = await FindMappingAsync(context, currentCollection.CollectionId, mappingId, cancellationToken);
        if (mapping is null)
        {
            return EndpointErrors.NotFound("tag_role_mapping.not_found", "Tag role mapping was not found");
        }

        try
        {
            mapping.EnsureCanDelete();
            _ = context.TagRoleMappings.Remove(mapping);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<TagRoleMapping?> FindMappingAsync(
        CratebaseDbContext context,
        CollectionId collectionId,
        Guid mappingId,
        CancellationToken cancellationToken)
    {
        return await context.TagRoleMappings.SingleOrDefaultAsync(
            mapping => mapping.CollectionId == collectionId && mapping.Id == new TagRoleMappingId(mappingId),
            cancellationToken);
    }

    private static TagRoleMappingResponse ToResponse(TagRoleMapping mapping)
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
