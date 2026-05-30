using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Application.Security;

public interface ICurrentUser
{
    UserId UserId { get; }
}
