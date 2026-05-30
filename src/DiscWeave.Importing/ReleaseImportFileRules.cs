using DiscWeave.Domain.Collection;

namespace DiscWeave.Importing;

public static class ReleaseImportFileRules
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".flac",
        ".mp3",
        ".wav",
        ".ogg",
        ".m4a"
    };

    private static readonly HashSet<string> CoverExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public static IReadOnlyCollection<string> SupportedAudioExtensions => AudioExtensions;

    public static IReadOnlyCollection<string> SupportedCoverExtensions => CoverExtensions;

    public static bool IsSupportedAudio(string path)
    {
        return AudioExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsSupportedCover(string path)
    {
        return CoverExtensions.Contains(Path.GetExtension(path));
    }

    public static AudioFileFormat FormatFromPath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".flac" => AudioFileFormat.Flac,
            ".mp3" => AudioFileFormat.Mp3,
            ".wav" => AudioFileFormat.Wav,
            ".ogg" => AudioFileFormat.Ogg,
            ".m4a" => AudioFileFormat.M4a,
            _ => throw new InvalidOperationException("Audio file format is not supported")
        };
    }

    public static string FormatCode(AudioFileFormat format)
    {
        return format switch
        {
            AudioFileFormat.Flac => "flac",
            AudioFileFormat.Mp3 => "mp3",
            AudioFileFormat.Wav => "wav",
            AudioFileFormat.Ogg => "ogg",
            AudioFileFormat.M4a => "m4a",
            AudioFileFormat.Aiff => "aiff",
            AudioFileFormat.Alac => "alac",
            _ => throw new InvalidOperationException("Audio file format is not supported")
        };
    }

    public static string CoverContentType(string? extension)
    {
        return string.IsNullOrWhiteSpace(extension)
            ? "application/octet-stream"
            : extension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
    }
}
