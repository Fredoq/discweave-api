namespace Cratebase.Api.Features.OwnedItems;

public sealed record MediumRequest(string Type, string? Description, string? Path, string? Format, int? DiscCount);
