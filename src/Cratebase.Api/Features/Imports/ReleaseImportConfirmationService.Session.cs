using Cratebase.Domain.Imports;
using Cratebase.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Api.Features.Imports;

public sealed partial class ReleaseImportConfirmationService
{
    internal static async Task UpdateSessionStatusAsync(
        CratebaseDbContext context,
        ReleaseImportSession session,
        ReleaseImportDraft changedDraft,
        CancellationToken cancellationToken)
    {
        ReleaseImportDraftStatus[] openStatuses =
        [
            ReleaseImportDraftStatus.NeedsReview,
            ReleaseImportDraftStatus.Ready
        ];
        bool changedDraftIsOpen = openStatuses.Contains(changedDraft.Status);
        bool hasOtherOpenDrafts = await context.ReleaseImportDrafts.AnyAsync(
            draft =>
                draft.CollectionId == session.CollectionId &&
                draft.SessionId == session.Id &&
                draft.Id != changedDraft.Id &&
                openStatuses.Contains(draft.Status),
            cancellationToken);

        if (!changedDraftIsOpen && !hasOtherOpenDrafts)
        {
            session.Complete(DateTimeOffset.UtcNow);
        }
    }
}
