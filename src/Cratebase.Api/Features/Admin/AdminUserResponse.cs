namespace Cratebase.Api.Features.Admin;

public sealed record AdminUserResponse(Guid Id, string Email, IReadOnlyList<string> Roles, Guid DefaultCollectionId, bool IsDisabled);
