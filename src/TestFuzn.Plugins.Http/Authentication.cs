namespace Fuzn.TestFuzn.Plugins.Http;

/// <summary>
/// Represents authentication credentials for HTTP requests.
/// </summary>
public class Authentication
{
    /// <summary>
    /// Gets or sets the Bearer token for Bearer authentication.
    /// </summary>
    public string BearerToken { get; set; }

    /// <summary>
    /// Gets or sets the Base64-encoded credentials for Basic authentication.
    /// </summary>
    public string Basic { get; set; }
}