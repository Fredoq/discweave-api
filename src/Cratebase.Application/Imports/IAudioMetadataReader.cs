namespace Cratebase.Application.Imports;

public interface IAudioMetadataReader
{
    AudioMetadata Read(string filePath);
}
