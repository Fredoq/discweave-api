using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Settings;

public static class NamingTemplateValidator
{
    private static readonly HashSet<string> ReleaseFolderTokens =
    [
        "releaseArtists",
        "title",
        "releaseDate",
        "year",
        "label",
        "catalogNumber",
        "source",
        "format",
        "bitDepth",
        "sampleRate"
    ];

    private static readonly HashSet<string> TrackFileTokens =
    [
        "position",
        "position2",
        "trackArtists",
        "title",
        "releaseArtists",
        "format",
        "bitDepth"
    ];

    public static string Validate(string template, NamingTemplateKind kind)
    {
        string normalized = Guard.RequiredText(template, nameof(template), "naming_profile.template_required");
        HashSet<string> allowedTokens = kind == NamingTemplateKind.ReleaseFolder ? ReleaseFolderTokens : TrackFileTokens;

        int index = 0;
        while (index < normalized.Length)
        {
            int start = normalized.IndexOf('{', index);
            if (start < 0)
            {
                return normalized.Trim();
            }

            int end = normalized.IndexOf('}', start);
            if (end < 0)
            {
                throw new DomainException("naming_profile.template_token_unclosed", "Naming template token is not closed");
            }

            string token = normalized[(start + 1)..end];
            if (!allowedTokens.Contains(token))
            {
                throw new DomainException("naming_profile.template_token_invalid", "Naming template token is invalid");
            }

            index = end + 1;
        }

        return normalized.Trim();
    }
}
