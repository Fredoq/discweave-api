using ATL;
using Cratebase.Application.Imports;
using Cratebase.Domain.Imports;
using System.Globalization;
using System.Reflection;

namespace Cratebase.Importing;

public sealed class AtlAudioMetadataReader : IAudioMetadataReader
{
    public AudioMetadata Read(string filePath)
    {
        try
        {
            var track = new Track(filePath);
            DateOnly? releaseDate = ReadDate(track);
            string? title = StringProperty(track, "Title");

            return new AudioMetadata(
                IsDerivedFileTitle(filePath, title) ? null : title,
                ImportArtistNames.Split(StringProperty(track, "Artist")),
                StringProperty(track, "Album"),
                ImportArtistNames.Split(StringProperty(track, "AlbumArtist")),
                null,
                releaseDate,
                releaseDate?.Year ?? IntProperty(track, "Year"),
                DurationProperty(track, "Duration"),
                IntProperty(track, "TrackNumber"));
        }
        catch (Exception)
        {
            return EmptyMetadata();
        }
    }

    private static AudioMetadata EmptyMetadata()
    {
        return new AudioMetadata(null, [], null, [], null, null, null, null, null);
    }

    private static bool IsDerivedFileTitle(string filePath, string? title)
    {
        return !string.IsNullOrWhiteSpace(title) &&
            string.Equals(title, Path.GetFileNameWithoutExtension(filePath), StringComparison.OrdinalIgnoreCase);
    }

    private static string? StringProperty(object instance, string name)
    {
        object? value = PropertyValue(instance, name);

        return value is null || string.IsNullOrWhiteSpace(value.ToString()) ? null : value.ToString()!.Trim();
    }

    private static int? IntProperty(object instance, string name)
    {
        object? value = PropertyValue(instance, name);
        return value is null ? null : value switch
        {
            int integer => integer > 0 ? integer : null,
            uint integer => integer > 0 ? (int)integer : null,
            short integer => integer > 0 ? integer : null,
            _ => int.TryParse(value.ToString(), CultureInfo.InvariantCulture, out int parsed) && parsed > 0 ? parsed : null
        };
    }

    private static TimeSpan? DurationProperty(object instance, string name)
    {
        object? value = PropertyValue(instance, name);

        return value switch
        {
            TimeSpan duration when duration > TimeSpan.Zero => duration,
            double seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            float seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            int seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            long seconds when seconds > 0 => TimeSpan.FromSeconds(seconds),
            _ => null
        };
    }

    private static DateOnly? ReadDate(object track)
    {
        object? value = PropertyValue(track, "Date");
        return value switch
        {
            DateOnly date when date.Year > 1 => date,
            DateTime date when date.Year > 1 => DateOnly.FromDateTime(date),
            string text => ParseDateString(text),
            _ => null
        };
    }

    private static DateOnly? ParseDateString(string value)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
    }

    private static object? PropertyValue(object instance, string name)
    {
        PropertyInfo? property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

        return property?.GetValue(instance);
    }
}
