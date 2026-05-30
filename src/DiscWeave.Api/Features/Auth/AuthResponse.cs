namespace DiscWeave.Api.Features.Auth;

public sealed record AuthResponse(bool IsAuthenticated, string Email, IReadOnlyList<string> Roles);
