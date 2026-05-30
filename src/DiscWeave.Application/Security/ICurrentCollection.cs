using System.Diagnostics.CodeAnalysis;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Application.Security;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Current Collection is the request context term for the collection boundary.")]
public interface ICurrentCollection
{
    CollectionId CollectionId { get; }
}
