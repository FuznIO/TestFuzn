using Microsoft.Extensions.Logging;
using System.Dynamic;
using System.Net;
using System.Text;
using System.Text.Json;
using TestFusion.Plugins.Http.Internals;

namespace TestFusion.Plugins.Http;

public class HttpRequest
{
    private readonly ContentTypes _contentType;
    private readonly string _url;
    private LoggingVerbosity _loggingVerbosity = Http.LoggingVerbosity.Full;
    private HttpMethod _method;
    private Context _context;

    public Authentication Auth { get; set; } = new();
    //public dynamic Body { get; set; } = new ExpandoObject();
    public object? Body { get; set; }
    public AcceptTypes AcceptTypes { get; set; } = AcceptTypes.Json;
    public List<Cookie> Cookies { get; set; } = new();
    public Dictionary<string, string> Headers { get; private set; } = new();
    public Hooks? Hooks { get; set; } = new();
    public string UserAgent { get; set; } = "TestFusionHttp/1.0";

    internal HttpRequest(Context context, HttpMethod method, string url, ContentTypes contentType = ContentTypes.Json)
    {
        _context = context;
        _method = method;
        _contentType = contentType;
        _url = url;
    }

    public HttpRequest LoggingVerbosity(LoggingVerbosity loggingVerbosity)
    {
        _loggingVerbosity = loggingVerbosity;
        return this;
    }

    public async Task<HttpResponse> Send()
    {
        var uri = new Uri(_url);
        var baseUri = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port).Uri;
        var relativeUri = uri.PathAndQuery;

        var request = new HttpRequestMessage(_method, relativeUri);

        if (AcceptTypes == AcceptTypes.Json)
            request.Headers.Add("Accept", "application/json");
        else if (AcceptTypes == AcceptTypes.Html)
            request.Headers.Add("Accept", $"text/html,application/xhtml+xml");
        
        foreach (var header in Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (Cookies is { Count: > 0 })
        {
            var cookieContainer = new CookieContainer();
            foreach (var cookie in Cookies)
            {
                if (string.IsNullOrEmpty(cookie.Domain))
                    cookie.Domain = baseUri.Host;
                cookieContainer.Add(baseUri, cookie);
            }

            var cookieHeader = cookieContainer.GetCookieHeader(uri);
            request.Headers.Remove("Cookie");
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.Add("Cookie", cookieHeader);
        }

        Hooks?.PreSend?.Invoke(this);

        if (_contentType == ContentTypes.Json && Body != null)
        {
            var jsonContent = JsonSerializer.Serialize(Body);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        }
        else if (_contentType == ContentTypes.XFormUrlEncoded && Body is Dictionary<string, string> dictBody)
        {
            request.Content = new FormUrlEncodedContent(dictBody);
        }

        if (!string.IsNullOrEmpty(Auth?.BearerToken))
        {
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", $"Bearer {Auth.BearerToken}");
        }
        else if (!string.IsNullOrEmpty(Auth?.Basic))
        {
            request.Headers.Remove("Authorization");
            request.Headers.Add("Authorization", $"Basic {Auth.Basic}");
        }

        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        var outputRequestResponse = false;
        HttpResponseMessage? response = null;
        string? responseBody = null;
        CookieContainer? responseCookies = null;

        try
        {
            var httpClientFactory = GlobalState.Configuration.CustomHttpClientFactory ?? _context.Internals.Plugins.GetState<IHttpClientFactory>(typeof(HttpPlugin));
            var client = httpClientFactory.CreateClient("TestFusion");
            client.BaseAddress = baseUri;
            client.Timeout = GlobalState.Configuration.HttpClientTimeout;

            response = await client.SendAsync(request);
            responseBody = await response.Content.ReadAsStringAsync();
            responseCookies = ExtractResponseCookies(response, uri);

            if (!response.IsSuccessStatusCode)
                outputRequestResponse = true;
        }
        catch (Exception ex)
        {
            if (_loggingVerbosity > TestFusion.Plugins.Http.LoggingVerbosity.None)
                _context.Logger.LogError(ex, null);

            outputRequestResponse = true;
            throw;
        }
        finally
        {
            if (outputRequestResponse && GlobalState.Configuration.LogFailedRequestsToTestConsole)
            {
                if (_loggingVerbosity == TestFusion.Plugins.Http.LoggingVerbosity.Full)
                {
                    _context.Logger.LogError("Request returned an error:\n" + request.ToString());
                    Console.WriteLine("Request returned an error:\n" + request.ToString());
                    if (response != null)
                    {
                        _context.Logger.LogError("\nResponse:\n " + response.ToString());
                        _context.Logger.LogError("\nResponse.Body:\n " + responseBody);
                    }
                }
            }
        }

        return new HttpResponse(request, response, responseCookies, body: responseBody);
    }

    private static CookieContainer? ExtractResponseCookies(HttpResponseMessage response, Uri uri)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            var responseCookies = new CookieContainer();
            foreach (var setCookieHeader in setCookieHeaders)
            {
                try
                {
                    responseCookies.SetCookies(uri, setCookieHeader);
                }
                catch
                {
                    // Ignore malformed cookies
                }
            }

            return responseCookies;
        }

        return null;
    }

    private List<KeyValuePair<string, string>> ConvertExpandoObjectToKeyPairList(ExpandoObject obj)
    {
        var list = new List<KeyValuePair<string, string>>();

        var bodyAsDictionary = (IDictionary<string, object>)obj;
        foreach (var item in bodyAsDictionary)
            list.Add(new KeyValuePair<string, string>(item.Key, item.Value.ToString()));

        return list;
    }
}
