namespace Cratebase.Api.Features.Credits;

public sealed record CreditResponse(
    Guid Id,
    Guid ContributorArtistId,
    string ContributorName,
    string TargetType,
    Guid TargetId,
    string Role);
