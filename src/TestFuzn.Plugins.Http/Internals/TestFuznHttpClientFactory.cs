using System.Net;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

/// <summary>
/// A thread-safe implementation of <see cref="IHttpClientFactory"/> that caches HTTP clients by base URL.
/// </summary>
internal class TestFuznHttpClientFactory : IHttpClientFactory
{
    private static readonly Dictionary<string, HttpClient> _clients = new Dictionary<string, HttpClient>();
    private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    /// <summary>
    /// Gets or creates a cached HTTP client for the specified base URL.
    /// </summary>
    /// <param name="baseUrl">The base URL for the HTTP client.</param>
    /// <returns>An <see cref="HttpClient"/> configured for the specified base URL.</returns>
    public static HttpClient GetClient(string baseUrl)
    {
        _lock.EnterReadLock();
        try
        {
            if (_clients.TryGetValue(baseUrl, out var client))
            {
                return client;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // Upgrade to write lock if the client does not exist
        _lock.EnterWriteLock();
        try
        {
            // Double-check to ensure another thread hasn't added the client
            if (!_clients.TryGetValue(baseUrl, out var client))
            {
                var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer(),
                    AllowAutoRedirect = false
                };

                client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl)
                };


                _clients[baseUrl] = client;
            }

            return client;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Creates an HTTP client for the specified base URL.
    /// </summary>
    /// <param name="baseUrl">The base URL (used as the client name/key).</param>
    /// <returns>An <see cref="HttpClient"/> configured for the specified base URL.</returns>
    public HttpClient CreateClient(string baseUrl)
    {
        return GetClient(baseUrl);
    }
}