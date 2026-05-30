using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Settings;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Settings;

public static partial class SettingsDictionariesEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSettingsDictionariesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/settings/dictionaries")
            .WithTags("Settings")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListDictionaryEntriesAsync).WithName("ListDictionaryEntries");
        _ = group.MapPost("", CreateDictionaryEntryAsync).WithName("CreateDictionaryEntry");
        _ = group.MapPut("/{entryId:guid}", UpdateDictionaryEntryAsync).WithName("UpdateDictionaryEntry");
        _ = group.MapDelete("/{entryId:guid}", DeleteDictionaryEntryAsync).WithName("DeleteDictionaryEntry");
        _ = group.MapPost("/{entryId:guid}/replace", ReplaceDictionaryEntryAsync).WithName("ReplaceDictionaryEntry");

        return endpoints;
    }

    private static async Task<IResult> ListDictionaryEntriesAsync(
        string? kind,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        IQueryable<CollectionDictionaryEntry> query = context.CollectionDictionaryEntries.AsNoTracking()
            .Where(entry => entry.CollectionId == currentCollection.CollectionId);

        if (!string.IsNullOrWhiteSpace(kind))
        {
            try
            {
                DictionaryKind parsedKind = DictionaryKindMapper.Parse(kind);
                query = query.Where(entry => entry.Kind == parsedKind);
            }
            catch (DomainException exception)
            {
                return EndpointErrors.BadRequest(exception.Code, exception.Message);
            }
        }

        CollectionDictionaryEntry[] entries = await query
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Name)
            .ToArrayAsync(cancellationToken);

        DictionaryEntryResponse[] responses = [.. entries.Select(ToResponse)];
        return Results.Ok(new ListResponse<DictionaryEntryResponse>(responses, responses.Length, 0, responses.Length));
    }

    private static async Task<IResult> CreateDictionaryEntryAsync(
        DictionaryEntryRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            DictionaryKind kind = DictionaryKindMapper.Parse(request.Kind);
            int sortOrder = request.SortOrder ?? 100;
            string mediaProfile = string.IsNullOrWhiteSpace(request.MediaProfile)
                ? "other"
                : request.MediaProfile;
            CollectionDictionaryEntry entry = kind == DictionaryKind.MediaType
                ? CollectionDictionaryEntry.CreateMedia(
                    CollectionDictionaryEntryId.New(),
                    currentCollection.CollectionId,
                    request.Code,
                    request.Name,
                    sortOrder,
                    isBuiltin: false,
                    mediaProfile)
                : CollectionDictionaryEntry.Create(
                    CollectionDictionaryEntryId.New(),
                    currentCollection.CollectionId,
                    kind,
                    request.Code,
                    request.Name,
                    sortOrder,
                    isBuiltin: false);
            if (request.IsActive == false)
            {
                entry.Deactivate();
            }

            bool codeExists = await context.CollectionDictionaryEntries.AnyAsync(
                existing => existing.CollectionId == currentCollection.CollectionId &&
                    existing.Kind == kind &&
                    existing.Code == entry.Code,
                cancellationToken);
            if (codeExists)
            {
                return EndpointErrors.Conflict("dictionary_entry.code_conflict", "Dictionary entry code already exists");
            }

            _ = context.CollectionDictionaryEntries.Add(entry);
            _ = await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/settings/dictionaries/{entry.Id.Value}", ToResponse(entry));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> UpdateDictionaryEntryAsync(
        Guid entryId,
        UpdateDictionaryEntryRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        CollectionDictionaryEntry? entry = await FindEntryAsync(context, currentCollection.CollectionId, entryId, cancellationToken);
        if (entry is null)
        {
            return EndpointErrors.NotFound("dictionary_entry.not_found", "Dictionary entry was not found");
        }

        try
        {
            entry.Rename(request.Name);
            if (request.SortOrder is { } sortOrder)
            {
                entry.Reorder(sortOrder);
            }

            if (entry.Kind == DictionaryKind.MediaType && !string.IsNullOrWhiteSpace(request.MediaProfile))
            {
                entry.UpdateMediaProfile(request.MediaProfile);
            }

            if (request.IsActive == true)
            {
                entry.Activate();
            }
            else if (request.IsActive == false)
            {
                entry.Deactivate();
            }

            _ = await context.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToResponse(entry));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> DeleteDictionaryEntryAsync(
        Guid entryId,
        HttpRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        if (!DeleteConfirmation.Matches(request, "dictionary-entry", entryId))
        {
            return EndpointErrors.DeleteConfirmationRequired();
        }

        CollectionDictionaryEntry? entry = await FindEntryAsync(context, currentCollection.CollectionId, entryId, cancellationToken);
        if (entry is null)
        {
            return EndpointErrors.NotFound("dictionary_entry.not_found", "Dictionary entry was not found");
        }

        try
        {
            entry.EnsureCanDelete();
            if (await IsEntryUsedAsync(context, entry, cancellationToken))
            {
                return EndpointErrors.Conflict("dictionary_entry.in_use", "Dictionary entry is used by catalog data");
            }

            _ = context.CollectionDictionaryEntries.Remove(entry);
            _ = await context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> ReplaceDictionaryEntryAsync(
        Guid entryId,
        ReplaceDictionaryEntryRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        CollectionDictionaryEntry? entry = await FindEntryAsync(context, currentCollection.CollectionId, entryId, cancellationToken);
        if (entry is null)
        {
            return EndpointErrors.NotFound("dictionary_entry.not_found", "Dictionary entry was not found");
        }

        CollectionDictionaryEntry? replacement = await context.CollectionDictionaryEntries.SingleOrDefaultAsync(
            candidate => candidate.CollectionId == currentCollection.CollectionId &&
                candidate.Kind == entry.Kind &&
                candidate.Code == request.ReplacementCode.Trim() &&
                candidate.IsActive,
            cancellationToken);
        if (replacement is null)
        {
            return EndpointErrors.BadRequest("dictionary_entry.replacement_invalid", "Replacement dictionary entry is invalid");
        }

        try
        {
            entry.EnsureCanDelete();
            await ReplaceUsagesAsync(context, entry, replacement.Code, cancellationToken);
            _ = context.CollectionDictionaryEntries.Remove(entry);
            _ = await context.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToResponse(replacement));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<CollectionDictionaryEntry?> FindEntryAsync(
        DiscWeaveDbContext context,
        CollectionId collectionId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        return await context.CollectionDictionaryEntries.SingleOrDefaultAsync(
            entry => entry.CollectionId == collectionId && entry.Id == new CollectionDictionaryEntryId(entryId),
            cancellationToken);
    }

    private static DictionaryEntryResponse ToResponse(CollectionDictionaryEntry entry)
    {
        string? mediaProfile = entry.MediaProfile is PresentOptionalValue<string> presentMediaProfile
            ? presentMediaProfile.Value
            : null;

        return new DictionaryEntryResponse(
            entry.Id.Value,
            DictionaryKindMapper.ToCode(entry.Kind),
            entry.Code,
            entry.Name,
            entry.SortOrder,
            entry.IsActive,
            entry.IsBuiltin,
            entry.IsProtected,
            mediaProfile);
    }
}
