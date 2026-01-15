using System.Net;
using System.Net.Http.Headers;
using Fuzn.TestFuzn.Contracts.Providers;
using Fuzn.TestFuzn.Plugins.Http.Internals;

namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Represents an HTTP response received from executing an HTTP request.
/// </summary>
public class HttpResponse
{
    private readonly List<Cookie> _cookies = new List<Cookie>();
    private readonly HttpRequestMessage _request;
    private readonly ISerializerProvider _serializerProvider;

    internal HttpResponse(HttpRequestMessage request,
        HttpResponseMessage response,
        CookieContainer? cookieContainer,
        string body,
        ISerializerProvider serializerProvider)
    {
        _request = request;
        _serializerProvider = serializerProvider;
        InnerResponse = response;
        RawResponse = response.ToString();
        Body = body;

        if (cookieContainer != null)
        {
            var cookies = cookieContainer.GetAllCookies();
            foreach (var cookie in cookies.Cast<Cookie>())
            {
                _cookies.Add(cookie);
            }
        }
    }

    /// <summary>
    /// Gets or sets the URL that was requested.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the underlying <see cref="HttpResponseMessage"/>.
    /// </summary>
    public HttpResponseMessage InnerResponse { get; set; }

    /// <summary>
    /// Gets or sets the raw response string representation.
    /// </summary>
    public string RawResponse { get; set; }

    /// <summary>
    /// Gets the response headers.
    /// </summary>
    public HttpResponseHeaders Headers
    {
        get
        {
            return InnerResponse.Headers;
        }
    }

    /// <summary>
    /// Gets the response body as a string.
    /// </summary>
    public string Body
    {
        get;
        private set;
    }

    /// <summary>
    /// Gets the cookies received in the response.
    /// </summary>
    public List<Cookie> Cookies
    {
        get { return _cookies; }
    }

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    public HttpStatusCode StatusCode
    {
        get { return InnerResponse.StatusCode; }
    }

    /// <summary>
    /// Gets a value indicating whether the response was successful (status code 2xx).
    /// </summary>
    public bool Ok => InnerResponse.IsSuccessStatusCode;

    /// <summary>
    /// Deserializes the response body into the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the body into.</typeparam>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="Exception">Thrown when the body is empty, null, or cannot be deserialized.</exception>
    public T? BodyAs<T>()
        where T : class
    {
        if (string.IsNullOrEmpty(Body))
            return null;

        try
        {
            var obj = _serializerProvider.Deserialize<T>(Body);
            if (obj == null)
                throw new Exception($"Deserialized object is null.");
            return obj;
        }
        catch (Exception ex)
        {
            throw new Exception($"Unable to deserialize into {typeof(T)}. \nURL: {_request?.RequestUri} \nResponse body: \n{Body}\nException message: \n{ex?.Message}");
        }
    }

    /// <summary>
    /// Parses the response body as a dynamic JSON object.
    /// </summary>
    /// <returns>A dynamic object representing the JSON response, or null if the body is null.</returns>
    /// <exception cref="Exception">Thrown when the body is not valid JSON.</exception>
    public dynamic? BodyAsJson()
    {
        if (Body == null)
            return null;

        try
        {
            dynamic? json = DynamicHelper.ParseJsonToDynamic(Body);
            return json;
        }
        catch (Exception)
        {
            throw new Exception($"The response body was not a valid JSON. \nURL: {_request.RequestUri} \nResponse body: \n{Body}");
        }
    }

    /// <summary>
    /// Generates a curl command that replicates the original HTTP request.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the curl command string.</returns>
    public async Task<string> GetCurlCommand()
    {
        var request = _request;
        var curl = new System.Text.StringBuilder();

        curl.Append("curl");

        // Method
        if (request.Method != HttpMethod.Get)
        {
            curl.Append($" -X {request.Method.Method}");
        }

        // Headers
        foreach (var header in request.Headers)
        {
            foreach (var value in header.Value)
            {
                curl.Append($" -H \"{header.Key}: {value}\"");
            }
        }

        // Content headers and body
        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                foreach (var value in header.Value)
                {
                    curl.Append($" -H \"{header.Key}: {value}\"");
                }
            }

            var content = await request.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(content))
            {
                // Escape double quotes in content
                var escapedContent = content.Replace("\"", "\\\"");
                curl.Append($" --data \"{escapedContent}\"");
            }
        }

        // URL
        curl.Append($" \"{request.RequestUri}\"");

        return curl.ToString();
    }
}
