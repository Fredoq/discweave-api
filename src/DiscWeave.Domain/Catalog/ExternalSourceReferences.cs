using DiscWeave.Domain.SharedKernel.Errors;

namespace DiscWeave.Domain.Catalog;

internal static class ExternalSourceReferences
{
    public static void Replace(
        List<ExternalSourceReference> current,
        IReadOnlyList<ExternalSourceReference> replacement)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);

        ExternalSourceReference[] replacementSnapshot = [.. replacement];
        for (int index = 0; index < replacementSnapshot.Length; index++)
        {
            ExternalSourceReference source = replacementSnapshot[index] ?? throw new DomainException(
                "external_source.required",
                "External source reference is required");

            if (replacementSnapshot.Take(index).Any(source.HasSameIdentity))
            {
                throw new DomainException("external_source.duplicate", "External source reference already exists");
            }
        }

        current.Clear();
        current.AddRange(replacementSnapshot);
    }
}
