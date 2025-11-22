using System.Net;
using Fuzn.TestFuzn.Contracts.Providers;
using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

public class HttpRequestBuilder
{
    private readonly Context _context;
    private readonly string _url;
    private ContentTypes _contentType = ContentTypes.Json;
    private object? _body;
    private AcceptTypes _acceptTypes = AcceptTypes.Json;
    private readonly List<Cookie> _cookies = new();
    private readonly Dictionary<string, string> _headers = new();
    private Authentication _auth = new();
    private Hooks _hooks = new();
    private LoggingVerbosity _loggingVerbosity = Http.LoggingVerbosity.Full;
    private string _userAgent = "TestFuznHttpTesting/1.0";
    private TimeSpan _timeout = HttpGlobalState.Configuration.DefaultRequestTimeout;
    private ISerializerProvider _serializerProvider;

    public HttpRequestBuilder(Context context, string url)
    {
        _context = context;
        _url = url;
        _serializerProvider = GlobalState.SerializerProvider;
    }

    public HttpRequestBuilder SerializerProvider(ISerializerProvider serializerProvider)
    {
        _serializerProvider = serializerProvider;
        return this;
    }

    public HttpRequestBuilder ContentType(ContentTypes contentType)
    {
        _contentType = contentType;
        return this;
    }

    public HttpRequestBuilder Body(object body)
    {
        _body = body;
        return this;
    }

    public HttpRequestBuilder Accept(AcceptTypes acceptTypes)
    {
        _acceptTypes = acceptTypes;
        return this;
    }

    public HttpRequestBuilder Cookie(Cookie cookie)
    {
        if (cookie.Expires < DateTime.UtcNow)
            cookie.Expires = DateTime.UtcNow.AddSeconds(10);
        _cookies.Add(cookie);
        return this;
    }

    public HttpRequestBuilder Cookie(string name, string value, string? path = null, string? domain = null, TimeSpan? duration = null)
    {
        var cookie = new Cookie(name, value, path, domain);
        cookie.Expires = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : DateTime.UtcNow.AddSeconds(10);
        _cookies.Add(cookie);
        return this;
    }

    public HttpRequestBuilder Header(string key, string value)
    {
        _headers[key] = value;
        return this;
    }

    public HttpRequestBuilder Headers(IDictionary<string,string> headers)
    {
        foreach (var header in headers)
            _headers[header.Key] = header.Value;
        return this;
    }

    public HttpRequestBuilder AuthBearer(string token)
    {
        if (!string.IsNullOrEmpty(_auth.Basic))
            throw new InvalidOperationException("Cannot set both Bearer and Basic authentication.");
            
        _auth = new Authentication { BearerToken = token };

        return this;
    }

    public HttpRequestBuilder AuthBasic(string username, string password)
    {
        if (!string.IsNullOrEmpty(_auth.BearerToken))
            throw new InvalidOperationException("Cannot set both Bearer and Basic authentication.");
            
        _auth = new Authentication{ Basic = BasicAuthenticationHelper.ToBase64String(username, password)};
        return this;
    }

    public HttpRequestBuilder Hooks(Hooks hooks)
    {
        _hooks = hooks;
        return this;
    }

    public HttpRequestBuilder LoggingVerbosity(LoggingVerbosity verbosity)
    {
        _loggingVerbosity = verbosity;
        return this;
    }

    public HttpRequestBuilder UserAgent(string userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    public HttpRequestBuilder Timeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public HttpRequest Build(HttpMethod method)
    {
        var request = new HttpRequest(_context, method, _url, _contentType)
        {
            Body = _body,
            AcceptTypes = _acceptTypes,
            Auth = _auth,
            Hooks = _hooks,
            Cookies = new List<Cookie>(_cookies),
            UserAgent = _userAgent,
            Timeout = _timeout,
            SerializerProvider = _serializerProvider
        };

        request.Headers.Add(HttpGlobalState.Configuration.CorrelationIdHeaderName, _context.Info.CorrelationId);
        foreach (var header in _headers)
            request.Headers[header.Key] = header.Value;

        request.LoggingVerbosity(_loggingVerbosity);
        return request;
    }

    public Task<HttpResponse> Send(HttpMethod httpMethod) => Build(httpMethod).Send();
    public Task<HttpResponse> Get() => Build(HttpMethod.Get).Send();
    public Task<HttpResponse> Post() => Build(HttpMethod.Post).Send();
    public Task<HttpResponse> Put() => Build(HttpMethod.Put).Send();
    public Task<HttpResponse> Delete() => Build(HttpMethod.Delete).Send();
    public Task<HttpResponse> Patch() => Build(HttpMethod.Patch).Send();
    public Task<HttpResponse> Head() => Build(HttpMethod.Head).Send();
    public Task<HttpResponse> Options() => Build(HttpMethod.Options).Send();
    public Task<HttpResponse> Trace() => Build(HttpMethod.Trace).Send();
}
