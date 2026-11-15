using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Fuzn.TestFuzn.Internals.Cluster.Protocol;

// One machine's connection to Cockpit, the hub of a cluster. Every call goes from this machine to Cockpit
// over plain HTTP(S), so workers and children open no ports. A connection carries one key: a project API
// key (remote client), the worker key (worker app) or a run token (controller).
internal sealed class CockpitConnection : IDisposable
{
    public const string UrlVariable = "TESTFUZN_COCKPIT_URL";
    public const string ApiKeyVariable = "TESTFUZN_COCKPIT_API_KEY";
    public const string WorkerKeyVariable = "TESTFUZN_WORKER_KEY";

    public const string ApiKeyHeader = "X-Api-Key";
    public const string WorkerKeyHeader = "X-Worker-Key";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly string _settingsHint;

    // settingsHint says where the URL and the key come from, for the error a person reads.
    public CockpitConnection(string url, string keyHeader, string key, string settingsHint)
    {
        Url = url.TrimEnd('/');
        _settingsHint = settingsHint;
        _http = new HttpClient { BaseAddress = new Uri(Url + "/"), Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.Add(keyHeader, key);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TestFuzn", typeof(CockpitConnection).Assembly.GetName().Version?.ToString() ?? "0"));
    }

    public string Url { get; }

    // Null when Cockpit has no such thing (404).
    public async Task<T?> Get<T>(string path, CancellationToken cancellationToken) where T : class
    {
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Get, path), RequestTimeout, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            return null;

        await EnsureSuccess(response, cancellationToken);
        return ProtocolJson.Deserialize<T>(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<TReply> Post<TReply>(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await Send(() => JsonRequest(HttpMethod.Post, path, body), RequestTimeout, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
        return ProtocolJson.Deserialize<TReply>(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task Post(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await Send(() => JsonRequest(HttpMethod.Post, path, body), RequestTimeout, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    public async Task<bool> Exists(string path, CancellationToken cancellationToken)
    {
        using var response = await Send(() => new HttpRequestMessage(HttpMethod.Head, path), RequestTimeout, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccess(response, cancellationToken);
        return true;
    }

    // Builds and reports can be hundreds of MB, so they stream and have no time limit of their own.
    public async Task Put(string path, Stream content, CancellationToken cancellationToken)
    {
        using var response = await Send(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = new StreamContent(content) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            return request;
        }, null, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    // False when Cockpit has no such file.
    public async Task<bool> Download(string path, Stream target, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendOnce(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        await EnsureSuccess(response, cancellationToken);
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        await body.CopyToAsync(target, cancellationToken);
        return true;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, object body)
    {
        return new HttpRequestMessage(method, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, body.GetType(), ProtocolJson.Options), Encoding.UTF8, "application/json")
        };
    }

    private async Task<HttpResponseMessage> Send(Func<HttpRequestMessage> createRequest, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != null)
            limit.CancelAfter(timeout.Value);

        using var request = createRequest();
        try
        {
            return await SendOnce(request, HttpCompletionOption.ResponseContentRead, limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CockpitException($"Cockpit at {Url} did not answer within {timeout?.TotalSeconds:0} s.", null);
        }
    }

    private async Task<HttpResponseMessage> SendOnce(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, completion, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new CockpitException($"Cannot reach Cockpit at {Url}: {ex.Message}. {_settingsHint}", null, ex);
        }
    }

    private async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var error = await ReadError(response, cancellationToken);
        var status = (int) response.StatusCode;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized when error == null => $"Cockpit at {Url} rejected the key. {_settingsHint}",
            HttpStatusCode.Unauthorized => $"Cockpit at {Url} rejected the key: {error} {_settingsHint}",
            HttpStatusCode.NotFound when error == null => $"Cockpit at {Url} has no {response.RequestMessage?.RequestUri?.AbsolutePath}. Is it a Cockpit with the TestFuzn integration turned on?",
            _ => error ?? $"Cockpit at {Url} answered {status} {response.ReasonPhrase}."
        };

        throw new CockpitException(message, status);
    }

    // Cockpit's errors are always { "error": "..." }.
    private static async Task<string?> ReadError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
                return null;

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

internal sealed class CockpitException : Exception
{
    public CockpitException(string message, int? statusCode, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    // Null when Cockpit was not reached at all.
    public int? StatusCode { get; }
}
