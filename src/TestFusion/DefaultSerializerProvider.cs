using System.Text.Json;
using TestFusion.Contracts.Providers;

namespace TestFusion;

public class DefaultSerializerProvider : ISerializerProvider
{
    private readonly JsonSerializerOptions _options;

    public DefaultSerializerProvider()
    {
        _options = DefaultSerializerProviderOptions.DefaultOptions;
    }

    public DefaultSerializerProvider(JsonSerializerOptions options)
    {
        _options = options;
    }

    public string Serialize<T>(T obj) where T : class
    {
        return JsonSerializer.Serialize(obj, _options);
    }

    public T Deserialize<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<T>(json, _options);
    }
}

internal static class DefaultSerializerProviderOptions
{
    internal static JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
}
