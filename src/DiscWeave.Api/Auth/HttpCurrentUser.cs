using System.Security.Claims;
using DiscWeave.Application.Security;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Api.Auth;

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
            ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                throw new InvalidOperationException("Current user is not authenticated");
            }

            string? userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out Guid parsedUserId)
                ? new UserId(parsedUserId)
                : throw new InvalidOperationException("Current user is not authenticated");
        }
    }
}
