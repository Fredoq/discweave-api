namespace DiscWeave.Api.Features.Admin;

public sealed record AdminUserResponse(Guid Id, string Email, IReadOnlyList<string> Roles, bool IsDisabled);
