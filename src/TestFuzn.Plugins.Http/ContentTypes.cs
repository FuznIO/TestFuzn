namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Specifies the content type for HTTP request bodies.
/// </summary>
public enum ContentTypes
{
    /// <summary>
    /// URL-encoded form data (application/x-www-form-urlencoded).
    /// </summary>
    XFormUrlEncoded,

    /// <summary>
    /// JSON content (application/json).
    /// </summary>
    Json
}
