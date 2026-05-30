using DiscWeave.Api.Auth;
using DiscWeave.Api.Features.Imports;
using DiscWeave.Api.Http;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Imports;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Settings;

public static partial class SettingsImportPatternsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSettingsImportPatternsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/settings/import-patterns")
            .WithTags("Settings")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListImportPatternsAsync).WithName("ListImportPatterns");
        _ = group.MapPost("", CreateImportPatternAsync).WithName("CreateImportPattern");
        _ = group.MapPut("/{patternId:guid}", UpdateImportPatternAsync).WithName("UpdateImportPattern");
        _ = group.MapDelete("/{patternId:guid}", DeleteImportPatternAsync).WithName("DeleteImportPattern");
        _ = group.MapPost("/test", TestImportPattern).WithName("TestImportPattern");

        return endpoints;
    }

    private static async Task<IResult> ListImportPatternsAsync(
        string? kind,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        await ImportPatternDefaults.EnsureAsync(context, currentCollection.CollectionId, cancellationToken);
        IQueryable<ImportPattern> query = context.ImportPatterns.AsNoTracking().Where(pattern => pattern.CollectionId == currentCollection.CollectionId);

        if (!string.IsNullOrWhiteSpace(kind))
        {
            try
            {
                ImportPatternKind parsedKind = ImportPatternKindMapper.Parse(kind);
                query = query.Where(pattern => pattern.Kind == parsedKind);
            }
            catch (DomainException exception)
            {
                return EndpointErrors.BadRequest(exception.Code, exception.Message);
            }
        }

        ImportPattern[] patterns = await query.OrderBy(pattern => pattern.Kind).ThenBy(pattern => pattern.SortOrder).ToArrayAsync(cancellationToken);
        return Results.Ok(new ListResponse<ImportPatternResponse>([.. patterns.Select(ToResponse)], patterns.Length, 0, patterns.Length));
    }

    private static async Task<IResult> CreateImportPatternAsync(
        ImportPatternRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            ImportPatternKind kind = ImportPatternKindMapper.Parse(request.Kind);
            var pattern = ImportPattern.Create(currentCollection.CollectionId, ImportPatternId.New(), kind, request.Template, request.SortOrder ?? 100, isBuiltin: false);
            if (request.IsActive == false)
            {
                pattern.Update(pattern.Template, pattern.SortOrder, isActive: false);
            }

            _ = context.ImportPatterns.Add(pattern);
            _ = await context.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/settings/import-patterns/{pattern.Id.Value}", ToResponse(pattern));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> UpdateImportPatternAsync(
        Guid patternId,
        ImportPatternRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ImportPattern? pattern = await FindPatternAsync(context, currentCollection.CollectionId, patternId, cancellationToken);
        if (pattern is null)
        {
            return EndpointErrors.NotFound("import_pattern.not_found", "Import pattern was not found");
        }

        try
        {
            ImportPatternKind requestedKind = ImportPatternKindMapper.Parse(request.Kind);
            if (requestedKind != pattern.Kind)
            {
                return EndpointErrors.BadRequest("import_pattern.kind_immutable", "Import pattern kind cannot be changed");
            }

            pattern.Update(request.Template, request.SortOrder ?? pattern.SortOrder, request.IsActive ?? pattern.IsActive);
            _ = await context.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToResponse(pattern));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteImportPatternAsync(
        Guid patternId,
        HttpRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "import-pattern", patternId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        ImportPattern? pattern = await FindPatternAsync(context, currentCollection.CollectionId, patternId, cancellationToken);
        if (pattern is null)
        {
            return EndpointErrors.NotFound("import_pattern.not_found", "Import pattern was not found");
        }

        if (pattern.IsBuiltin)
        {
            return EndpointErrors.BadRequest("import_pattern.builtin_immutable", "Built-in import patterns cannot be deleted");
        }

        _ = context.ImportPatterns.Remove(pattern);
        _ = await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
