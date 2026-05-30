namespace DiscWeave.Api.Features.Auth;

public sealed record AuthSessionResponse(
    bool IsAuthenticated,
    bool BootstrapRequired,
    string? Email,
    IReadOnlyList<string> Roles);
