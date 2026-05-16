namespace Cratebase.LocalAgent;

public interface ILocalFolderPicker
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);
}
