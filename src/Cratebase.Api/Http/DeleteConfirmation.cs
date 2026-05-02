using Microsoft.Extensions.Primitives;

namespace Cratebase.Api.Http;

public static class DeleteConfirmation
{
    public const string HeaderName = "X-Cratebase-Confirm-Delete";

    public static bool Matches(HttpRequest request, string resource, Guid resourceId)
    {
        ArgumentNullException.ThrowIfNull(request);

        string expectedConfirmation = $"{resource}:{resourceId}";

        return request.Headers.TryGetValue(HeaderName, out StringValues confirmationValues) &&
            confirmationValues.Count == 1 &&
            string.Equals(confirmationValues[0], expectedConfirmation, StringComparison.Ordinal);
    }
}
