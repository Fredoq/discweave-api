using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Application.Security;

public interface ICurrentUser
{
    UserId UserId { get; }
}
