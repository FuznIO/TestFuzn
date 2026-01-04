using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn.Plugins.Http.Internals;

internal class HttpRequest
{
    internal ContentTypes ContentType { get; set; }
    internal string Url { get; set; }
    internal LoggingVerbosity LoggingVerbosity { get; set; }
    internal HttpMethod Method { get; set; }
    internal Context Context { get; set; }
    internal Authentication Auth { get; set; }
    internal object? Body { get; set; }
    internal AcceptTypes AcceptTypes { get; set; }
    internal List<Cookie> Cookies { get; set; }
    internal Dictionary<string, string> Headers { get; set; }
    internal Action<HttpRequestMessage>? BeforeSend { get; set; }
    internal string UserAgent { get; set; }
    internal TimeSpan Timeout { get; set; }
    internal ISerializerProvider SerializerProvider { get; set; }

    internal async Task<HttpResponse> Send()
    {
        var uri = new Uri(Url);
        var baseUri = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port).Uri;
        var relativeUri = uri.PathAndQuery;

        var request = new HttpRequestMessage(Method, relativeUri);

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

        if (ContentType == ContentTypes.Json && Body != null)
        {
            if (Body is string rawJson)
            {
                request.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
            }
            else
            {
                var jsonContent = SerializerProvider.Serialize(Body);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            }
        }
        else if (ContentType == ContentTypes.XFormUrlEncoded && Body is Dictionary<string, string> dictBody)
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
            var httpClientFactory = HttpGlobalState.Configuration.CustomHttpClientFactory ?? Context.Internals.Plugins.GetState<IHttpClientFactory>(typeof(HttpPlugin));
            var client = httpClientFactory.CreateClient("TestFuzn");
            client.BaseAddress = baseUri;

            var cts = new CancellationTokenSource(Timeout);

            Context.Logger.LogInformation($"Step {Context.StepInfo.Name} - HTTP Request: {request.Method} {request.RequestUri} - CorrelationId: {Context.Info.CorrelationId}");

            if (request.Content != null)
            {
                var requestBody = await request.Content.ReadAsStringAsync(cts.Token);
                Context.Logger.LogInformation($"Step {Context.StepInfo.Name} - Request Body: {requestBody} - CorrelationId: {Context.Info.CorrelationId}");
            }

            if (BeforeSend != null)
                BeforeSend?.Invoke(request);

            response = await client.SendAsync(request, cts.Token);
            responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            Context.Logger.LogInformation($"Step {Context.StepInfo.Name} - HTTP Response: {(int)response.StatusCode} {response.ReasonPhrase} - CorrelationId: {Context.Info.CorrelationId}");
            if (responseBody != null)
                Context.Logger.LogInformation($"Step {Context.StepInfo.Name} - Response Body: {responseBody} - CorrelationId: {Context.Info.CorrelationId}");

            responseCookies = ExtractResponseCookies(response, uri);

            if (!response.IsSuccessStatusCode)
                outputRequestResponse = true;
        }
        catch (Exception ex)
        {
            if (LoggingVerbosity > TestFuzn.LoggingVerbosity.None)
                Context.Logger.LogError(ex, $"Step {Context.StepInfo.Name} - HTTP Request failed - CorrelationId: {Context.Info.CorrelationId}");

            outputRequestResponse = true;
            throw;
        }
        finally
        {
            if (outputRequestResponse && HttpGlobalState.Configuration.LogFailedRequestsToTestConsole)
            {
                if (LoggingVerbosity == TestFuzn.LoggingVerbosity.Full)
                {
                    Context.Logger.LogError($"Step {Context.StepInfo.Name} - Request returned an error:\n{request} - CorrelationId: {Context.Info.CorrelationId}");
                    Console.WriteLine("Request returned an error:\n" + request.ToString());
                    if (response != null)
                    {
                        Context.Logger.LogError($"Step {Context.StepInfo.Name} - Response:\n{response} - CorrelationId: {Context.Info.CorrelationId}");
                        Context.Logger.LogError($"Step {Context.StepInfo.Name} - Response.Body:\n{responseBody} - CorrelationId: {Context.Info.CorrelationId}");
                    }
                }
            }
        }

        return new HttpResponse(request, response, responseCookies, body: responseBody, SerializerProvider);
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
}
