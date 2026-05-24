namespace Cratebase.Seeding;

public sealed class SeedCommand
{
    public SeedCommand(
        string connectionString,
        string email,
        string password,
        LargeCollectionSeedOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(options);

        ConnectionString = connectionString;
        Email = email;
        Password = password;
        Options = options;
    }

    public string ConnectionString { get; }

    public string Email { get; }

    public string Password { get; }

    public LargeCollectionSeedOptions Options { get; }
}
