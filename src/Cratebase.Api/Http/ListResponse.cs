namespace Cratebase.Api.Http;

public sealed record ListResponse<T>(IReadOnlyList<T> Items, int Limit, int Offset, int Total);
