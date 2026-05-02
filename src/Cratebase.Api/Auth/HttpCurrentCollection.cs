using System.Diagnostics.CodeAnalysis;
using Cratebase.Application.Security;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Auth;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Current Collection is the request context term for the collection boundary.")]
public sealed class HttpCurrentCollection : ICurrentCollection
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentCollection(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CollectionId CollectionId
    {
        get
        {
            string? collectionId = _httpContextAccessor.HttpContext?.User.FindFirst(CratebaseClaimTypes.DefaultCollectionId)?.Value;

            return Guid.TryParse(collectionId, out Guid parsedCollectionId)
                ? new CollectionId(parsedCollectionId)
                : throw new InvalidOperationException("Current collection is not available");
        }
    }
}
