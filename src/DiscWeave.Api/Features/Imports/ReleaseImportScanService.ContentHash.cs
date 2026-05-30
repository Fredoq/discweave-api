using DiscWeave.Domain.SharedKernel.Optional;

namespace DiscWeave.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private static string? NormalizeContentHash(IOptionalValue<string> value)
    {
        return value is PresentOptionalValue<string> present ? NormalizeContentHash(present.Value) : null;
    }
}
