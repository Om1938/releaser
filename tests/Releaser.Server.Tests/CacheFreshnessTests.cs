using System.Net;
using Releaser.Domain.Targeting;
using Releaser.Server.Tests.Infrastructure;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Releaser.Server.Tests;

/// <summary>Bounded freshness and fail-closed behaviour of cached resolution inputs across nodes (ADR 0008).</summary>
public sealed class CacheFreshnessTests(PostgresContainer postgres)
{
    private const int FreshnessSeconds = 2;

    private static async Task<(Guid AppId, Guid DeploymentId)> ArrangeAsync(AdminClient admin, StubManifestHost manifests)
    {
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0", [(PlatformTarget.Windows, manifests.Publish("1.1.0", PlatformTarget.Windows))]);
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        var deployment = await admin.DeployAsync(app.Id, "Everyone", release.Id, everyone.Id);
        return (app.Id, deployment.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_pause_on_one_node_stops_offers_on_another_node_within_the_freshness_bound(bool withRedis)
    {
        await using var redis = withRedis ? new RedisBuilder("redis:8.8.3-alpine").Build() : null;
        if (redis is not null)
        {
            await redis.StartAsync();
        }
        var connectionString = await postgres.CreateDatabaseAsync();
        var manifests = new StubManifestHost();
        await using var nodeA = new ReleaserFactory(new FactoryOptions(connectionString, manifests, FreshnessSeconds, redis?.GetConnectionString()));
        await using var nodeB = new ReleaserFactory(new FactoryOptions(connectionString, manifests, FreshnessSeconds, redis?.GetConnectionString()));
        var admin = await nodeA.CreateAdminClient().LoginAsync();
        var (appId, deploymentId) = await ArrangeAsync(admin, manifests);
        var feedB = nodeB.CreateFeedClient();
        (await feedB.CheckAsync("installation-1", "1.0.0")).IsOffer("1.1.0").ShouldBeTrue("node B caches the active deployment");

        await admin.TransitionDeploymentAsync(appId, deploymentId, "pause");
        var pausedAt = DateTime.UtcNow;

        (await nodeA.CreateFeedClient().CheckAsync("installation-1", "1.0.0")).FileUrls.ShouldBeEmpty("the writing node evicts its own stamp immediately");
        while ((await feedB.CheckAsync("installation-1", "1.0.0")).FileUrls.Count > 0)
        {
            (DateTime.UtcNow - pausedAt).ShouldBeLessThan(TimeSpan.FromSeconds(FreshnessSeconds + 1), "node B must stop offering within the bound");
            await Task.Delay(100);
        }
        (await feedB.CheckAsync("installation-1", "1.0.0")).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task when_the_database_is_unreachable_after_the_bound_the_feed_fails_closed()
    {
        await using var database = new PostgreSqlBuilder("postgres:18.6-alpine").Build();
        await database.StartAsync();
        var manifests = new StubManifestHost();
        await using var node = new ReleaserFactory(new FactoryOptions(database.GetConnectionString(), manifests, FreshnessSeconds));
        var admin = await node.CreateAdminClient().LoginAsync();
        await ArrangeAsync(admin, manifests);
        var feed = node.CreateFeedClient();
        (await feed.CheckAsync("installation-1", "1.0.0")).IsOffer("1.1.0").ShouldBeTrue();

        await database.StopAsync();
        await Task.Delay(TimeSpan.FromSeconds(FreshnessSeconds + 0.5));

        var answer = await feed.CheckAsync("installation-1", "1.0.0");
        answer.Status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        answer.FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task an_unreachable_redis_does_not_change_decisions()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var manifests = new StubManifestHost();
        await using var node = new ReleaserFactory(new FactoryOptions(connectionString, manifests, FreshnessSeconds, "127.0.0.1:1,connectTimeout=200,abortConnect=false"));
        var admin = await node.CreateAdminClient().LoginAsync();
        var (appId, deploymentId) = await ArrangeAsync(admin, manifests);
        var feed = node.CreateFeedClient();
        (await feed.CheckAsync("installation-1", "1.0.0")).IsOffer("1.1.0").ShouldBeTrue();

        await admin.TransitionDeploymentAsync(appId, deploymentId, "pause");
        (await feed.CheckAsync("installation-1", "1.0.0")).FileUrls.ShouldBeEmpty();
    }
}
