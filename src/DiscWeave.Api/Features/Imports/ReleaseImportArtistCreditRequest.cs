namespace DiscWeave.Api.Features.Imports;

public sealed record ReleaseImportArtistCreditRequest(Guid? ArtistId, string? Name, string? Role);
