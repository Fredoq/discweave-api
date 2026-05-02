using System.Security.Claims;
using Cratebase.Application.Security;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Auth;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public UserId UserId
    {
        get
        {
            string? userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out Guid parsedUserId)
                ? new UserId(parsedUserId)
                : throw new InvalidOperationException("Current user is not authenticated");
        }
    }
}
