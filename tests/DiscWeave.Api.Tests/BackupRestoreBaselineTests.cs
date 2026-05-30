using DiscWeave.Api.Tests.Architecture;

namespace DiscWeave.Api.Tests;

public sealed class BackupRestoreBaselineTests
{
    [Fact(DisplayName = "Hosted backup and restore baseline documentation is present")]
    public void Hosted_backup_and_restore_baseline_documentation_is_present()
    {
        DirectoryInfo root = RepositoryRoot.Find();
        string documentPath = Path.Combine(root.FullName, "docs", "hosting", "hosted-backup-restore-baseline.md");

        string content = File.ReadAllText(documentPath);

        Assert.Contains("managed PostgreSQL", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("release covers", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("desktop artifacts", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("restore drill", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user exports", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Hosted restore drill script is present and repeatable")]
    public void Hosted_restore_drill_script_is_present_and_repeatable()
    {
        DirectoryInfo root = RepositoryRoot.Find();
        string scriptPath = Path.Combine(root.FullName, "deploy", "hosted-restore-drill.sh");

        string content = File.ReadAllText(scriptPath);

        Assert.Contains("set -euo pipefail", content, StringComparison.Ordinal);
        Assert.Contains("pg_dump", content, StringComparison.Ordinal);
        Assert.Contains("pg_restore", content, StringComparison.Ordinal);
        Assert.Contains("service-storage", content, StringComparison.Ordinal);
        Assert.Contains("restore drill", content, StringComparison.OrdinalIgnoreCase);
    }
}
