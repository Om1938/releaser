using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Releaser.Server.Infrastructure.Manifests;

/// <summary>Fetches manifests over HTTPS with SSRF guarding, no redirects, a size cap and a timeout (ADR 0003).</summary>
internal sealed class HttpManifestFetcher(HttpClient http, IOptions<ManifestOptions> options) : IManifestFetcher
{
    public const int MaxBytes = 1024 * 1024;

    public async Task<FetchedManifest> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        EnsureAllowedScheme(url);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new ManifestFetchException($"Fetching {url} returned HTTP {(int)response.StatusCode}; redirects are not followed.");
            }
            var bytes = await ReadCappedAsync(response.Content, timeout.Token);
            return new FetchedManifest(url, Encoding.UTF8.GetString(bytes), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ManifestFetchException($"Could not fetch {url}: {exception.Message}", exception);
        }
    }

    private void EnsureAllowedScheme(Uri url)
    {
        var allowed = url.Scheme == Uri.UriSchemeHttps || (options.Value.AllowInsecureHttp && url.Scheme == Uri.UriSchemeHttp);
        if (!url.IsAbsoluteUri || !allowed)
        {
            throw new ManifestFetchException("Manifest URLs must be absolute https:// URLs.");
        }
    }

    private static async Task<byte[]> ReadCappedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxBytes)
        {
            throw new ManifestFetchException("The manifest exceeds 1 MiB.");
        }
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                throw new ManifestFetchException("The manifest exceeds 1 MiB.");
            }
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>Handler that refuses to connect to non-public addresses after DNS resolution (defeats DNS rebinding).</summary>
    public static SocketsHttpHandler CreateHandler(ManifestOptions options) => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        UseCookies = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            var allowed = addresses.Where(address => options.AllowPrivateNetworks || NetworkGuard.IsPublic(address)).ToArray();
            if (allowed.Length == 0)
            {
                throw new HttpRequestException($"Host '{context.DnsEndPoint.Host}' does not resolve to an allowed public address.");
            }
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };
}
