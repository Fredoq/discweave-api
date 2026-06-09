namespace DiscWeave.Api.Features.Credits;

public sealed record CreditResponse(
    Guid Id,
    Guid ContributorArtistId,
    string ContributorName,
    string TargetType,
    Guid TargetId,
    string Role,
    IReadOnlyList<string> Roles,
    string? TargetTitle = null);
