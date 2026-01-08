using System.Net;
using Fuzn.TestFuzn.Contracts.Providers;
using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Fluent builder for constructing and sending HTTP requests.
/// </summary>
public class HttpRequestBuilder
{
    private static readonly string DefaultUserAgent = 
        $"TestFuzn.Http/{typeof(HttpRequestBuilder).Assembly.GetName().Version.ToString(3)}";
    private string _userAgent = DefaultUserAgent;
    private readonly Context _context;
    private readonly string _url;
    private ContentTypes _contentType = ContentTypes.Json;
    private object? _body;
    private AcceptTypes _acceptTypes = AcceptTypes.Json;
    private readonly List<Cookie> _cookies = new();
    private readonly Dictionary<string, string> _headers = new();
    private Authentication _auth = new();
    private Action<HttpRequestMessage>? _beforeSend;
    private LoggingVerbosity _loggingVerbosity = GlobalState.LoggingVerbosity;
    private TimeSpan _timeout = HttpGlobalState.Configuration.DefaultRequestTimeout;
    private ISerializerProvider _serializerProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestBuilder"/> class.
    /// </summary>
    /// <param name="context">The step context for where the request is created from.</param>
    /// <param name="url">The target URL for the request.</param>
    internal HttpRequestBuilder(Context context, string url)
    {
        _context = context;
        _url = url;
        _serializerProvider = GlobalState.SerializerProvider;
    }

    /// <summary>
    /// Sets a custom serializer provider for request/response body serialization.
    /// </summary>
    /// <param name="serializerProvider">The serializer provider to use.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder SerializerProvider(ISerializerProvider serializerProvider)
    {
        _serializerProvider = serializerProvider;
        return this;
    }

    /// <summary>
    /// Sets the Content-Type header for the request.
    /// </summary>
    /// <param name="contentType">The content type to use.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder ContentType(ContentTypes contentType)
    {
        _contentType = contentType;
        return this;
    }

    /// <summary>
    /// Sets the request body. The body will be serialized based on the configured content type.
    /// </summary>
    /// <param name="body">The body content to send.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder Body(object body)
    {
        _body = body;
        return this;
    }

    /// <summary>
    /// Sets the Accept header for the request.
    /// </summary>
    /// <param name="acceptTypes">The accepted response content types.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder Accept(AcceptTypes acceptTypes)
    {
        _acceptTypes = acceptTypes;
        return this;
    }

    /// <summary>
    /// Adds a cookie to the request.
    /// </summary>
    /// <param name="cookie">The cookie to add.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder Cookie(Cookie cookie)
    {
        _cookies.Add(cookie);
        return this;
    }

    /// <summary>
    /// Adds a cookie to the request with the specified parameters.
    /// </summary>
    /// <param name="name">The name of the cookie.</param>
    /// <param name="value">The value of the cookie.</param>
    /// <param name="path">The path for which the cookie is valid. Defaults to null.</param>
    /// <param name="domain">The domain for which the cookie is valid. Defaults to null.</param>
    /// <param name="duration">The duration until the cookie expires. Defaults to 10 seconds if not specified.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder Cookie(string name, string value, string? path = null, string? domain = null, TimeSpan? duration = null)
    {
        var cookie = new Cookie(name, value, path, domain);
        cookie.Expires = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : DateTime.UtcNow.AddSeconds(10);
        _cookies.Add(cookie);
        return this;
    }

    /// <summary>
    /// Adds a single header to the request.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder Header(string key, string value)
    {
        _headers[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple headers to the request.
    /// </summary>
    /// <param name="headers">A dictionary of header names and values.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder Headers(IDictionary<string, string> headers)
    {
        foreach (var header in headers)
            _headers[header.Key] = header.Value;
        return this;
    }

    /// <summary>
    /// Sets Bearer token authentication for the request.
    /// </summary>
    /// <param name="token">The Bearer token.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Basic authentication is already configured.</exception>
    public HttpRequestBuilder AuthBearer(string token)
    {
        if (!string.IsNullOrEmpty(_auth.Basic))
            throw new InvalidOperationException("Cannot set both Bearer and Basic authentication.");
            
        _auth = new Authentication { BearerToken = token };

        return this;
    }

    /// <summary>
    /// Sets Basic authentication for the request.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Bearer authentication is already configured.</exception>
    public HttpRequestBuilder AuthBasic(string username, string password)
    {
        if (!string.IsNullOrEmpty(_auth.BearerToken))
            throw new InvalidOperationException("Cannot set both Bearer and Basic authentication.");
            
        _auth = new Authentication{ Basic = BasicAuthenticationHelper.ToBase64String(username, password)};
        return this;
    }

    /// <summary>
    /// Registers an action to be invoked just before the request is sent.
    /// </summary>
    /// <param name="action">The action to execute with the constructed <see cref="HttpRequestMessage"/>.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder BeforeSend(Action<HttpRequestMessage> action)
    {
        _beforeSend = action;
        return this;
    }

    /// <summary>
    /// Sets the logging verbosity level for the request.
    /// </summary>
    /// <param name="verbosity">The logging verbosity level.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder LoggingVerbosity(LoggingVerbosity verbosity)
    {
        _loggingVerbosity = verbosity;
        return this;
    }

    /// <summary>
    /// Sets a custom User-Agent header for the request.
    /// </summary>
    /// <param name="userAgent">The User-Agent string.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder UserAgent(string userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    /// <summary>
    /// Sets the timeout for the request.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>The current builder instance for method chaining.</returns>
    public HttpRequestBuilder Timeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    internal HttpRequest Build(HttpMethod method)
    {
        var request = new HttpRequest()
        {
            Context = _context,
            Method = method,
            Url = _url,
            ContentType = _contentType,
            Body = _body,
            AcceptTypes = _acceptTypes,
            Auth = _auth,
            BeforeSend = _beforeSend,
            Cookies = new List<Cookie>(_cookies),
            UserAgent = _userAgent,
            Timeout = _timeout,
            SerializerProvider = _serializerProvider,
            LoggingVerbosity = _loggingVerbosity,
            Headers = _headers
        };

        request.Headers.TryAdd(HttpGlobalState.Configuration.CorrelationIdHeaderName, _context.Info.CorrelationId);

        return request;
    }

    /// <summary>
    /// Sends the request using the specified HTTP method.
    /// </summary>
    /// <param name="httpMethod">The HTTP method to use.</param>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Send(HttpMethod httpMethod) => Build(httpMethod).Send();

    /// <summary>
    /// Sends the request using the HTTP GET method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Get() => Build(HttpMethod.Get).Send();

    /// <summary>
    /// Sends the request using the HTTP POST method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Post() => Build(HttpMethod.Post).Send();

    /// <summary>
    /// Sends the request using the HTTP PUT method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Put() => Build(HttpMethod.Put).Send();

    /// <summary>
    /// Sends the request using the HTTP DELETE method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Delete() => Build(HttpMethod.Delete).Send();

    /// <summary>
    /// Sends the request using the HTTP PATCH method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Patch() => Build(HttpMethod.Patch).Send();

    /// <summary>
    /// Sends the request using the HTTP HEAD method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Head() => Build(HttpMethod.Head).Send();

    /// <summary>
    /// Sends the request using the HTTP OPTIONS method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Options() => Build(HttpMethod.Options).Send();

    /// <summary>
    /// Sends the request using the HTTP TRACE method.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response.</returns>
    public Task<HttpResponse> Trace() => Build(HttpMethod.Trace).Send();
}
