using Cratebase.Domain.Imports;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public sealed partial class ReleaseImportConfirmationService
{
    private static async Task UpdateSessionStatusAsync(
        CratebaseDbContext context,
        ReleaseImportSession session,
        CancellationToken cancellationToken)
    {
        ReleaseImportDraftStatus[] openStatuses =
        [
            ReleaseImportDraftStatus.NeedsReview,
            ReleaseImportDraftStatus.Ready
        ];
        bool hasOpenDrafts = await context.ReleaseImportDrafts.AnyAsync(
            draft => draft.SessionId == session.Id && openStatuses.Contains(draft.Status),
            cancellationToken);

        if (!hasOpenDrafts)
        {
            session.Complete(DateTimeOffset.UtcNow);
        }
    }
}
