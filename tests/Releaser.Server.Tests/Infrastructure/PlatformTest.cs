namespace Releaser.Server.Tests.Infrastructure;

/// <summary>Base for tests that need one running node with its own database.</summary>
public abstract class PlatformTest(PostgresContainer postgres) : IAsyncLifetime
{
    protected StubManifestHost Manifests { get; } = new();
    protected ReleaserFactory Node { get; private set; } = null!;
    protected string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync();
        Node = new ReleaserFactory(new FactoryOptions(ConnectionString, Manifests));
        _ = Node.Server; // start: applies migrations and bootstraps the admin
    }

    public async ValueTask DisposeAsync() => await Node.DisposeAsync();

    protected Task<AdminClient> AdminAsync() => Node.CreateAdminClient().LoginAsync();

    protected static IEnumerable<(Domain.Targeting.PlatformTarget, string)> AllPlatforms(StubManifestHost host, string version) =>
        [.. new[] { Domain.Targeting.PlatformTarget.Windows, Domain.Targeting.PlatformTarget.MacOS, Domain.Targeting.PlatformTarget.LinuxX64 }
            .Select(platform => (platform, host.Publish(version, platform)))];
}
