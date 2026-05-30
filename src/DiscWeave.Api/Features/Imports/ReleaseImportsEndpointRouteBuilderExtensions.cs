using DiscWeave.Api.Auth;
using DiscWeave.Api.Http;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Imports;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Imports;

public static partial class ReleaseImportsEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapReleaseImportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/imports")
            .WithTags("Imports")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.CollectionMember);

        _ = group.MapGet("", ListImportsAsync).WithName("ListReleaseImports");
        _ = group.MapGet("/{sessionId:guid}", GetImportAsync).WithName("GetReleaseImport");
        _ = group.MapGet("/desktop-downloads/macos", DownloadMacOsDesktopAsync).WithName("DownloadMacOsDesktop");
        _ = group.MapPost("/desktop-folder-scans", AcceptDesktopFolderScanAsync).WithName("AcceptDesktopFolderScan");
        _ = group.MapPut("/{sessionId:guid}/drafts/{draftId:guid}", UpdateDraftAsync).WithName("UpdateReleaseImportDraft");
        _ = group.MapPost("/{sessionId:guid}/drafts/{draftId:guid}/confirm", ConfirmDraftAsync).WithName("ConfirmReleaseImportDraft");
        _ = group.MapPost("/{sessionId:guid}/drafts/{draftId:guid}/skip", SkipDraftAsync).WithName("SkipReleaseImportDraft");

        return endpoints;
    }

    private static async Task<IResult> ListImportsAsync(DiscWeaveDbContext context, ICurrentCollection currentCollection, CancellationToken cancellationToken)
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

    private static async Task<IResult> GetImportAsync(Guid sessionId, DiscWeaveDbContext context, ICurrentCollection currentCollection, CancellationToken cancellationToken)
    {
        ReleaseImportSession? session = await FindSessionAsync(context, currentCollection.CollectionId, sessionId, cancellationToken);

        return session is null
            ? EndpointErrors.NotFound("release_import.not_found", "Release import session was not found")
            : Results.Ok(await ReleaseImportResponseMapper.ToDetailResponseAsync(session, context, currentCollection.CollectionId, cancellationToken));
    }

    private const string MacOsInstallerContentType = "application/x-apple-diskimage";
    private const string MacOsInstallerDefaultPattern = "DiscWeave*.dmg";

    private static IResult DownloadMacOsDesktopAsync(IWebHostEnvironment environment, IConfiguration configuration)
    {
        string? path = FindMacOsInstaller(environment, configuration);
        return path is not null
            ? Results.File(path, MacOsInstallerContentType, Path.GetFileName(path))
            : EndpointErrors.NotFound("desktop.download_not_found", "DiscWeave macOS desktop installer is not available in this build");
    }

    private static string? FindMacOsInstaller(IWebHostEnvironment environment, IConfiguration configuration)
    {
        string? configuredPath = ResolveConfiguredPath(configuration["DesktopDownloads:MacOsInstallerPath"], environment.ContentRootPath);
        if (configuredPath is not null && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        string pattern = configuration["DesktopDownloads:MacOsInstallerPattern"] ?? MacOsInstallerDefaultPattern;
        foreach (string directory in CandidateMacOsInstallerDirectories(environment, configuration))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            FileInfo? installer = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (installer is not null)
            {
                return installer.FullName;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateMacOsInstallerDirectories(IWebHostEnvironment environment, IConfiguration configuration)
    {
        string? configuredDirectory = ResolveConfiguredPath(configuration["DesktopDownloads:MacOsInstallerDirectory"], environment.ContentRootPath);
        if (configuredDirectory is not null)
        {
            yield return configuredDirectory;
        }

        yield return Path.Combine(environment.ContentRootPath, "desktop");
    }

    private static string? ResolveConfiguredPath(string? path, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string configuredPath = Path.IsPathFullyQualified(path)
            ? path
            : Path.Combine(contentRootPath, path);

        return Path.GetFullPath(configuredPath);
    }

    private static async Task<IResult> AcceptDesktopFolderScanAsync(
        DesktopFolderScanRequest request,
        DiscWeaveDbContext context,
        ICurrentCollection currentCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            ReleaseImportScanResult result = await ReleaseImportScanService.AcceptDesktopAsync(
                request,
                context,
                currentCollection.CollectionId,
                cancellationToken);
            return Results.Created(
                $"/api/imports/{result.Session.Id.Value}",
                await ReleaseImportResponseMapper.ToDetailResponseAsync(result.Session, context, result.CollectionId, cancellationToken));
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
        DiscWeaveDbContext context,
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
                ToOptional(request.CatalogNumber),
                ToOptional(request.LabelName),
                ToOptional(releaseDate),
                ToOptional(request.Year),
                request.IsVariousArtists,
                request.NotOnLabel,
                ToOptional(request.CoverPath),
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
        DiscWeaveDbContext context,
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

    private static async Task<IResult> SkipDraftAsync(Guid sessionId, Guid draftId, DiscWeaveDbContext context, ICurrentCollection currentCollection, CancellationToken cancellationToken)
    {
        ReleaseImportDraft? draft = await FindDraftAsync(context, currentCollection.CollectionId, sessionId, draftId, cancellationToken);
        if (draft is null)
        {
            return EndpointErrors.NotFound("release_import_draft.not_found", "Release import draft was not found");
        }

        try
        {
            draft.Skip();
            ReleaseImportSession session = await FindSessionAsync(context, currentCollection.CollectionId, sessionId, cancellationToken)
                ?? throw new InvalidOperationException("Release import session is required");
            await ReleaseImportConfirmationService.UpdateSessionStatusAsync(context, session, draft, cancellationToken);
            _ = await context.SaveChangesAsync(cancellationToken);
            return Results.Ok(await ReleaseImportResponseMapper.ToDetailResponseAsync(session, context, currentCollection.CollectionId, cancellationToken));
        }
        catch (DomainException exception)
        {
            return EndpointErrors.BadRequest(exception.Code, exception.Message);
        }
    }

}
