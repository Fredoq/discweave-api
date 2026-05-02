using Cratebase.Api.Http;
using Cratebase.Application.Errors;
using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Labels;

public static class LabelsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapLabelsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/labels").WithTags("Labels");
        _ = group.MapPost("/", CreateLabelAsync).WithName("CreateLabel");
        _ = group.MapGet("/{labelId:guid}", GetLabelAsync).WithName("GetLabel");
        _ = group.MapGet("/", ListLabelsAsync).WithName("ListLabels");
        _ = group.MapPut("/{labelId:guid}", UpdateLabelAsync).WithName("UpdateLabel");
        _ = group.MapDelete("/{labelId:guid}", DeleteLabelAsync).WithName("DeleteLabel");

        return endpoints;
    }

    private static async Task<IResult> CreateLabelAsync(
        NameRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            var label = Label.Create(LabelId.New(), request.Name);
            IRepository<Label, LabelId> labels = unitOfWork.GetRepository<Label, LabelId>();
            labels.Add(label);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/labels/{label.Id}", ToLabelResponse(label));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> GetLabelAsync(Guid labelId, CratebaseDbContext context, CancellationToken cancellationToken)
    {
        Label? label = await context.Labels.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == new LabelId(labelId), cancellationToken);

        return label is null
            ? EndpointErrors.NotFound("label.not_found", "Label was not found")
            : Results.Ok(ToLabelResponse(label));
    }

    private static async Task<IResult> ListLabelsAsync(
        string? search,
        int? limit,
        int? offset,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryNormalize(limit, offset, out int normalizedLimit, out int normalizedOffset, out IResult error))
        {
            return error;
        }

        IQueryable<Label> labels = context.Labels.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search.Trim()}%";
            labels = labels.Where(label => EF.Functions.ILike(label.Name, pattern));
        }

        int total = await labels.CountAsync(cancellationToken);
        LabelResponse[] items = await labels
            .OrderBy(label => label.Name)
            .ThenBy(label => label.Id)
            .Skip(normalizedOffset)
            .Take(normalizedLimit)
            .Select(label => new LabelResponse(label.Id.Value, label.Name))
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<LabelResponse>(items, normalizedLimit, normalizedOffset, total));
    }

    private static async Task<IResult> UpdateLabelAsync(
        Guid labelId,
        NameRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        IRepository<Label, LabelId> labels = unitOfWork.GetRepository<Label, LabelId>();
        Label? label = await labels.TryFindAsync(new LabelId(labelId), cancellationToken);
        if (label is null)
        {
            return EndpointErrors.NotFound("label.not_found", "Label was not found");
        }

        try
        {
            label.Rename(request.Name);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToLabelResponse(label));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteLabelAsync(
        Guid labelId,
        HttpRequest request,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "label", labelId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        IRepository<Label, LabelId> labels = unitOfWork.GetRepository<Label, LabelId>();
        Label? label = await labels.TryFindAsync(new LabelId(labelId), cancellationToken);
        if (label is null)
        {
            return EndpointErrors.NotFound("label.not_found", "Label was not found");
        }

        try
        {
            labels.Delete(label);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (ResourceHasDependentsException)
        {
            return EndpointErrors.Conflict("label.delete_conflict", "Label has dependent data");
        }
    }

    private static LabelResponse ToLabelResponse(Label label)
    {
        return new LabelResponse(label.Id.Value, label.Name);
    }

}
