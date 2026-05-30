using DiscWeave.Api.Auth;
using DiscWeave.Api.Features.Invites;
using DiscWeave.Api.Http;
using DiscWeave.Application.Security;
using DiscWeave.Infrastructure.Identity;
using DiscWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Api.Features.Admin;

public static class AdminInvitesEndpointRouteBuilderExtensions
{
    private const int MaxNoteLength = 512;

    public static IEndpointRouteBuilder MapAdminInvitesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/invites")
            .WithTags("Admin Invites")
            .RequireAuthorization(DiscWeaveAuthorizationPolicies.Admin);

        _ = group.MapPost("/", CreateInviteAsync).WithName("CreateInvite");
        _ = group.MapGet("/", ListInvitesAsync).WithName("ListInvites");
        _ = group.MapPost("/{inviteId:guid}/revoke", RevokeInviteAsync).WithName("RevokeInvite");

        return endpoints;
    }

    private static async Task<IResult> CreateInviteAsync(
        CreateInviteRequest request,
        DiscWeaveDbContext context,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (request.Note?.Length > MaxNoteLength)
        {
            return EndpointErrors.BadRequest("invite.note_too_long", "Invite note must be 512 characters or fewer");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (request.ExpiresAt is { } explicitExpiresAt && explicitExpiresAt <= now)
        {
            return EndpointErrors.BadRequest("invite.expires_at_invalid", "Invite expiration must be in the future");
        }

        DateTimeOffset expiresAt = request.ExpiresAt ?? now.AddDays(30);
        string code = InviteCodes.Generate();
        var invite = Invite.Create(
            Guid.CreateVersion7(),
            InviteCodes.Hash(code),
            currentUser.UserId.Value,
            request.Note,
            expiresAt,
            now);

        _ = context.Invites.Add(invite);
        _ = await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/invites/{invite.Id}", ToCreateResponse(invite, code, now));
    }

    private static async Task<IResult> ListInvitesAsync(DiscWeaveDbContext context, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Invite[] invites = await context.Invites.AsNoTracking()
            .OrderByDescending(invite => invite.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(invites.Select(invite => ToResponse(invite, now)));
    }

    private static async Task<IResult> RevokeInviteAsync(
        Guid inviteId,
        DiscWeaveDbContext context,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        Invite? invite = await context.Invites.SingleOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);
        if (invite is null)
        {
            return EndpointErrors.NotFound("invite.not_found", "Invite was not found");
        }

        if (!invite.TryRevoke(currentUser.UserId.Value, DateTimeOffset.UtcNow))
        {
            return EndpointErrors.BadRequest("invite.already_redeemed", "Redeemed invites cannot be revoked");
        }

        _ = await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(invite, DateTimeOffset.UtcNow));
    }

    private static CreateInviteResponse ToCreateResponse(Invite invite, string code, DateTimeOffset now)
    {
        return new CreateInviteResponse(invite.Id, code, invite.Status(now), invite.CreatedAt, invite.ExpiresAt, invite.Note);
    }

    private static InviteResponse ToResponse(Invite invite, DateTimeOffset now)
    {
        return new InviteResponse(
            invite.Id,
            invite.Status(now),
            invite.CreatedAt,
            invite.CreatedByUserId,
            invite.Note,
            invite.ExpiresAt,
            invite.RevokedAt,
            invite.RevokedByUserId,
            invite.RedeemedAt,
            invite.RedeemedUserId,
            invite.RedeemedEmail);
    }
}
