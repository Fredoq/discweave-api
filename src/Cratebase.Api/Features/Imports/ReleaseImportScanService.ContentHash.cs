using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Api.Features.Imports;

public static partial class ReleaseImportScanService
{
    private static string? NormalizeContentHash(IOptionalValue<string> value)
    {
        return value is PresentOptionalValue<string> present ? NormalizeContentHash(present.Value) : null;
    }
}
