namespace DiscWeave.Api.Features.Credits;

internal sealed record CreditArtistResolverErrors(
    string ConflictCode,
    string ConflictMessage,
    string NameRequiredCode,
    string NameRequiredMessage);
