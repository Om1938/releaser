using System.Net;
using Microsoft.Extensions.Options;
using Releaser.Server.Infrastructure.Manifests;

namespace Releaser.Server.Tests;

public sealed class ManifestFetcherTests
{
    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private static Task<FetchedManifest> FetchAsync(HttpStatusCode status) =>
        new HttpManifestFetcher(new HttpClient(new StatusHandler(status)), Options.Create(new ManifestOptions()))
            .FetchAsync(new Uri("https://bucket.s3.amazonaws.com/app/latest-mac.yml"), CancellationToken.None);

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task missing_files_explain_that_object_stores_may_answer_403(HttpStatusCode status)
    {
        var error = await Should.ThrowAsync<ManifestFetchException>(() => FetchAsync(status));
        error.Message.ShouldContain($"HTTP {(int)status}");
        error.Message.ShouldContain("S3 answer 403 instead of 404");
    }

    [Fact]
    public async Task redirects_ask_for_the_final_url()
    {
        var error = await Should.ThrowAsync<ManifestFetchException>(() => FetchAsync(HttpStatusCode.Found));
        error.Message.ShouldContain("redirects are not followed");
    }

    [Fact]
    public async Task plain_http_is_refused_by_default()
    {
        var fetcher = new HttpManifestFetcher(new HttpClient(new StatusHandler(HttpStatusCode.OK)), Options.Create(new ManifestOptions()));
        await Should.ThrowAsync<ManifestFetchException>(() => fetcher.FetchAsync(new Uri("http://cdn.example.com/latest.yml"), CancellationToken.None));
    }
}
