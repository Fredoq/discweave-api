namespace Cratebase.Api.Features.OwnedItems;

public sealed record MediumResponse(string Type, string Description, string? Path, string? Format, int? DiscCount);
