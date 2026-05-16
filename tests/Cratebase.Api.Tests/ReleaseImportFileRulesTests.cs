using Cratebase.Domain.Collection;
using Cratebase.Importing;

namespace Cratebase.Api.Tests;

public sealed class ReleaseImportFileRulesTests
{
    [Fact(DisplayName = "Release import file rules expose supported extensions")]
    public void Release_import_file_rules_expose_supported_extensions()
    {
        Assert.Equal([".flac", ".mp3", ".wav", ".ogg", ".m4a"], ReleaseImportFileRules.SupportedAudioExtensions);
        Assert.Equal([".jpg", ".jpeg", ".png", ".webp"], ReleaseImportFileRules.SupportedCoverExtensions);
    }

    [Theory(DisplayName = "Release import file rules detect supported audio files")]
    [InlineData("track.flac", true)]
    [InlineData("track.mp3", true)]
    [InlineData("track.wav", true)]
    [InlineData("track.ogg", true)]
    [InlineData("track.m4a", true)]
    [InlineData("track.aiff", false)]
    public void Release_import_file_rules_detect_supported_audio_files(string path, bool expected)
    {
        Assert.Equal(expected, ReleaseImportFileRules.IsSupportedAudio(path));
    }

    [Theory(DisplayName = "Release import file rules detect supported cover files")]
    [InlineData("cover.jpg", true)]
    [InlineData("cover.jpeg", true)]
    [InlineData("cover.png", true)]
    [InlineData("cover.webp", true)]
    [InlineData("cover.gif", false)]
    public void Release_import_file_rules_detect_supported_cover_files(string path, bool expected)
    {
        Assert.Equal(expected, ReleaseImportFileRules.IsSupportedCover(path));
    }

    [Theory(DisplayName = "Release import file rules map audio extensions to formats")]
    [InlineData("track.flac", AudioFileFormat.Flac)]
    [InlineData("track.mp3", AudioFileFormat.Mp3)]
    [InlineData("track.wav", AudioFileFormat.Wav)]
    [InlineData("track.ogg", AudioFileFormat.Ogg)]
    [InlineData("track.m4a", AudioFileFormat.M4a)]
    public void Release_import_file_rules_map_audio_extensions_to_formats(string path, AudioFileFormat expected)
    {
        Assert.Equal(expected, ReleaseImportFileRules.FormatFromPath(path));
    }

    [Theory(DisplayName = "Release import file rules map audio formats to codes")]
    [InlineData(AudioFileFormat.Flac, "flac")]
    [InlineData(AudioFileFormat.Mp3, "mp3")]
    [InlineData(AudioFileFormat.Wav, "wav")]
    [InlineData(AudioFileFormat.Ogg, "ogg")]
    [InlineData(AudioFileFormat.M4a, "m4a")]
    [InlineData(AudioFileFormat.Aiff, "aiff")]
    [InlineData(AudioFileFormat.Alac, "alac")]
    public void Release_import_file_rules_map_audio_formats_to_codes(AudioFileFormat format, string expected)
    {
        Assert.Equal(expected, ReleaseImportFileRules.FormatCode(format));
    }

    [Theory(DisplayName = "Release import file rules map cover content types")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".gif", "application/octet-stream")]
    [InlineData(null, "application/octet-stream")]
    public void Release_import_file_rules_map_cover_content_types(string? extension, string expected)
    {
        Assert.Equal(expected, ReleaseImportFileRules.CoverContentType(extension));
    }

    [Fact(DisplayName = "Release import file rules reject unsupported format mappings")]
    public void Release_import_file_rules_reject_unsupported_format_mappings()
    {
        _ = Assert.Throws<InvalidOperationException>(() => ReleaseImportFileRules.FormatFromPath("track.aiff"));
        _ = Assert.Throws<InvalidOperationException>(() => ReleaseImportFileRules.FormatCode((AudioFileFormat)999));
    }
}
