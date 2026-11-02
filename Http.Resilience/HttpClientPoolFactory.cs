using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Anvil.Http.Resilience;

public interface IHttpClientPoolFactory
{
    HttpClient GetClient(string poolName = "default", int maxTimeoutSeconds = 120);
}

public sealed class HttpClientPoolFactory : IHttpClientPoolFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, SocketsHttpHandler> _handlers = new();
    private readonly ILogger<HttpClientPoolFactory> _logger;

    public HttpClientPoolFactory(ILogger<HttpClientPoolFactory> logger)
    {
        _logger = logger;
    }

    public HttpClient GetClient(string poolName = "default", int maxTimeoutSeconds = 120)
    {
        var handler = _handlers.GetOrAdd(poolName, name =>
        {
            _logger.LogDebug("Created SocketsHttpHandler pool '{PoolName}'", name);
            return new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                ConnectTimeout = TimeSpan.FromSeconds(30)
            };
        });

        return new HttpClient(handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(maxTimeoutSeconds)
        };
    }

    public void Dispose()
    {
        foreach (var handler in _handlers.Values)
        {
            handler.Dispose();
        }

        _handlers.Clear();
    }
}
