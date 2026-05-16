using Cratebase.Api.Auth;
using Cratebase.Api.Http;
using Cratebase.Application.Security;
using Cratebase.Domain.Imports;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Importing;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public static partial class ReleaseImportsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapReleaseImportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/imports")
            .WithTags("Imports")
            .RequireAuthorization(CratebaseAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListImportsAsync).WithName("ListReleaseImports");
        _ = group.MapGet("/{sessionId:guid}", GetImportAsync).WithName("GetReleaseImport");
        _ = group.MapGet("/local-agent-downloads/macos", DownloadMacOsLocalAgentAsync).WithName("DownloadMacOsLocalAgent");
        _ = group.MapPost("/local-agent-tokens", CreateLocalAgentTokenAsync).WithName("CreateLocalAgentImportToken");
        _ = group.MapPost("/local-agent-scans", AcceptLocalAgentScanAsync).AllowAnonymous().WithName("AcceptLocalAgentImportScan");
        _ = group.MapPut("/{sessionId:guid}/drafts/{draftId:guid}", UpdateDraftAsync).WithName("UpdateReleaseImportDraft");
        _ = group.MapPost("/{sessionId:guid}/drafts/{draftId:guid}/confirm", ConfirmDraftAsync).WithName("ConfirmReleaseImportDraft");
        _ = group.MapPost("/{sessionId:guid}/drafts/{draftId:guid}/skip", SkipDraftAsync).WithName("SkipReleaseImportDraft");

        return endpoints;
    }

    private static async Task<IResult> ListImportsAsync(CratebaseDbContext context, ICurrentCollection currentCollection, CancellationToken cancellationToken)
    {
        ReleaseImportSession[] sessions = await context.ReleaseImportSessions.AsNoTracking()
            .Where(session => session.CollectionId == currentCollection.CollectionId)
            .OrderByDescending(session => session.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new ListResponse<ReleaseImportSessionResponse>(
            [.. sessions.Select(ReleaseImportResponseMapper.ToSessionResponse)],
            sessions.Length,
            0,
            sessions.Length));
    }

    private static async Task<IResult> GetImportAsync(Guid sessionId, CratebaseDbContext context, ICurrentCollection currentCollection, CancellationToken cancellationToken)
    {
        ReleaseImportSession? session = await FindSessionAsync(context, currentCollection.CollectionId, sessionId, cancellationToken);

        return session is null
            ? EndpointErrors.NotFound("release_import.not_found", "Release import session was not found")
            : Results.Ok(await ReleaseImportResponseMapper.ToDetailResponseAsync(session, context, currentCollection.CollectionId, cancellationToken));
    }

    private static IResult DownloadMacOsLocalAgentAsync(IWebHostEnvironment environment)
    {
        string path = Path.Combine(environment.ContentRootPath, "local-agent", "Cratebase.LocalAgent.dmg");
        return File.Exists(path)
            ? Results.File(path, "application/x-apple-diskimage", "Cratebase.LocalAgent.dmg")
            : EndpointErrors.NotFound("local_agent.download_not_found", "Local agent macOS installer is not available in this build");
    }

    private static async Task<IResult> CreateLocalAgentTokenAsync(
        LocalAgentImportTokenService tokens,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        LocalAgentImportTokenIssue token = await tokens.CreateAsync(context, currentCollection.CollectionId, cancellationToken);

        return Results.Ok(new LocalAgentImportTokenResponse(
            token.Token,
            token.ExpiresAt,
            "http://127.0.0.1:43817",
            1,
            "/api/imports/local-agent-downloads/macos",
            token.ReleaseFolderPatterns,
            token.TrackFilePatterns));
    }

    private static async Task<IResult> AcceptLocalAgentScanAsync(
        LocalAgentScanUploadRequest request,
        LocalAgentImportScanService scans,
        CratebaseDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            LocalAgentImportScanResult result = await scans.AcceptAsync(request, context, cancellationToken);
            return Results.Created(
                $"/api/imports/{result.Session.Id.Value}",
                await ReleaseImportResponseMapper.ToDetailResponseAsync(result.Session, context, result.CollectionId, cancellationToken));
        }
        catch (DomainException exception) when (exception.Code.StartsWith("local_agent_import_token.", StringComparison.Ordinal))
        {
            return EndpointErrors.Unauthorized(exception.Code, exception.Message);
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> UpdateDraftAsync(
        Guid sessionId,
        Guid draftId,
        ReleaseImportDraftUpdateRequest request,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        ReleaseImportDraft? draft = await FindDraftAsync(context, currentCollection.CollectionId, sessionId, draftId, cancellationToken);
        if (draft is null)
        {
            return EndpointErrors.NotFound("release_import_draft.not_found", "Release import draft was not found");
        }

        try
        {
            DateOnly? releaseDate = ParseOptionalDate(request.ReleaseDate);
            draft.UpdateEditableFields(new ReleaseImportDraftEditableFields(
                request.Title,
                request.Type ?? "unknown",
                request.CatalogNumber,
                request.LabelName,
                releaseDate,
                request.Year,
                request.IsVariousArtists,
                request.NotOnLabel,
                request.CoverPath,
                request.ArtistNames ?? [],
                [.. request.ArtistCredits?.Select(ToImportArtistCredit) ?? []],
                [.. request.Labels?.Select(ToImportLabel) ?? []],
                request.SelectedArtistIds ?? [],
                request.Genres ?? [],
                request.Tags ?? [],
                draft.Issues));
            await UpdateTracksAsync(request, draft, context, cancellationToken);
            _ = await context.SaveChangesAsync(cancellationToken);
            ReleaseImportSession session = await FindSessionAsync(context, currentCollection.CollectionId, sessionId, cancellationToken)
                ?? throw new InvalidOperationException("Release import session is required");

            return Results.Ok(await ReleaseImportResponseMapper.ToDetailResponseAsync(session, context, currentCollection.CollectionId, cancellationToken));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static ReleaseImportArtistCredit ToImportArtistCredit(ReleaseImportArtistCreditRequest request)
    {
        return new ReleaseImportArtistCredit(request.ArtistId, request.Name ?? string.Empty, request.Role ?? string.Empty);
    }

    private static ReleaseImportLabel ToImportLabel(ReleaseImportLabelRequest request)
    {
        return new ReleaseImportLabel(request.LabelId, request.Name ?? string.Empty, request.CatalogNumber, request.HasNoCatalogNumber);
    }

    private static async Task<IResult> ConfirmDraftAsync(
        Guid sessionId,
        Guid draftId,
        ReleaseImportConfirmationService confirmation,
        CratebaseDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            ReleaseImportSession? session = await confirmation.ConfirmAsync(sessionId, draftId, context, currentCollection.CollectionId, cancellationToken);
            return session is null
                ? EndpointErrors.NotFound("release_import_draft.not_found", "Release import draft was not found")
                : Results.Ok(await ReleaseImportResponseMapper.ToDetailResponseAsync(session, context, currentCollection.CollectionId, cancellationToken));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> SkipDraftAsync(Guid sessionId, Guid draftId, CratebaseDbContext context, ICurrentCollection currentCollection, CancellationToken cancellationToken)
    {
        ReleaseImportDraft? draft = await FindDraftAsync(context, currentCollection.CollectionId, sessionId, draftId, cancellationToken);
        if (draft is null)
        {
            return EndpointErrors.NotFound("release_import_draft.not_found", "Release import draft was not found");
        }

        draft.Skip();
        _ = await context.SaveChangesAsync(cancellationToken);
        ReleaseImportSession session = await FindSessionAsync(context, currentCollection.CollectionId, sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Release import session is required");
        return Results.Ok(await ReleaseImportResponseMapper.ToDetailResponseAsync(session, context, currentCollection.CollectionId, cancellationToken));
    }

}
