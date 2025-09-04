using System.Net;

namespace Fuzn.TestFuzn.Plugins.Http;

public class TestFusionHttpClientFactory : IHttpClientFactory
{
    private static readonly Dictionary<string, HttpClient> _clients = new Dictionary<string, HttpClient>();
    private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

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

    public HttpClient CreateClient(string baseUrl)
    {
        return GetClient(baseUrl);
    }
}