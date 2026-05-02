namespace Cratebase.Api.Tests.Architecture;

internal static class RepositoryRoot
{
    public static DirectoryInfo Find()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Cratebase.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found");
    }
}
