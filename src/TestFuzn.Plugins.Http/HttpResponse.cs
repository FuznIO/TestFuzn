using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http.Headers;
using FuznLabs.TestFuzn.Contracts.Providers;
using FuznLabs.TestFuzn.Plugins.Http.Internals;

namespace FuznLabs.TestFuzn.Plugins.Http;

public class HttpResponse
{
    internal HttpResponse(HttpRequestMessage request,
        HttpResponseMessage response,
        CookieContainer? cookieContainer,
        string body,
        HashSet<ISerializerProvider> serializerProviders)
    {
        _request = request;
        _serializerProviders = serializerProviders;
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
    
    private readonly List<Cookie> _cookies = new List<Cookie>();
    private readonly HttpRequestMessage _request;
    private readonly HashSet<ISerializerProvider> _serializerProviders;

    public string Url { get; set; }
    public HttpResponseMessage InnerResponse { get; set; }
    public string RawResponse { get; set; }
    
    public string CurlRequest => GetCurlCommand();

    public HttpResponseHeaders Headers
    {
        get
        {
            return InnerResponse.Headers;
        }
    }

    public string Body
    {
        get;
        private set;
    }

    public List<Cookie> Cookies
    {
        get { return _cookies; }
    }

    public HttpStatusCode StatusCode
    {
        get { return InnerResponse.StatusCode; }
    }

    public bool Ok => InnerResponse.IsSuccessStatusCode;

    public T BodyAs<T>()
        where T : class
    {
        if (string.IsNullOrEmpty(Body))
            return null;

        foreach (var serializerProvider in _serializerProviders.OrderBy(sp => sp.Priority))
        {
            if (serializerProvider.IsSerializerSpecific<T>())
                return serializerProvider.Deserialize<T>(Body);
        }

        Exception? exception = null;

        foreach (var serializerProvider in _serializerProviders.OrderBy(sp => sp.Priority))
        {
            try
            {
                var obj = serializerProvider.Deserialize<T>(Body);
                if (obj == null)
                    throw new AssertFailedException($"Deserialized object is null.");
                return obj;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }

        throw new AssertFailedException($"Unable to deserialize into {typeof(T)}. \nURL: {_request?.RequestUri} \nResponse body: \n{Body}\nException message: \n{exception?.Message}");
    }

    public dynamic BodyAsJson()
    {
        if (Body == null)
            return null;

        dynamic json = null;

        try
        {
            json = DynamicHelper.ParseJsonToDynamic(Body);
        }
        catch (Exception ex)
        {
            Assert.Fail($"The response body was not a valid JSON. \nURL: {_request.RequestUri} \nResponse body: \n{Body}");
        }

        return json;
    }

    // Generates a curl command that reproduces the current HTTP request
    private string GetCurlCommand()
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

            // TODO, get rid of GetAwaiter?
            var content = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
