namespace Cratebase.Api.Features.Auth;

public sealed record AuthResponse(Guid Id, string Email, IReadOnlyList<string> Roles, Guid DefaultCollectionId);
