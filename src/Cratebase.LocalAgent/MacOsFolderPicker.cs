using System.Diagnostics;

namespace Cratebase.LocalAgent;

public sealed class MacOsFolderPicker : ILocalFolderPicker
{
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Cratebase Local Agent v1 supports local folder selection on macOS only");
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "osascript",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        process.StartInfo.ArgumentList.Add("-e");
        process.StartInfo.ArgumentList.Add("POSIX path of (choose folder with prompt \"Choose import folder\")");

        if (!process.Start())
        {
            throw new InvalidOperationException("Folder picker could not be opened");
        }

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        _ = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return null;
        }

        string path = output.Trim();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }
}
