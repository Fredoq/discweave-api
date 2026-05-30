namespace DiscWeave.Api.Features.Credits;

public sealed record CreditRequest(Guid ContributorArtistId, string TargetType, Guid TargetId, string Role);
