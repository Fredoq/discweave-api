namespace Cratebase.Api.Http;

public static class Pagination
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;

    public static bool TryNormalize(
        int? limit,
        int? offset,
        out int normalizedLimit,
        out int normalizedOffset,
        out IResult error)
    {
        normalizedLimit = limit ?? DefaultLimit;
        normalizedOffset = offset ?? 0;
        error = Results.Empty;

        if (normalizedLimit < 1 || normalizedLimit > MaximumLimit || normalizedOffset < 0)
        {
            error = EndpointErrors.BadRequest("pagination.invalid", "Pagination values are invalid");
            return false;
        }

        return true;
    }
}
