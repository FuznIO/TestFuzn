using System.Text.Json;
using Fuzn.TestFuzn.Contracts.Providers;

namespace Fuzn.TestFuzn;

public class SystemTextJsonSerializerProvider : ISerializerProvider
{
    private readonly JsonSerializerOptions _options;

    public SystemTextJsonSerializerProvider()
    {
        _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public SystemTextJsonSerializerProvider(JsonSerializerOptions jsonSerializerOptions)
    {
        _options = jsonSerializerOptions;
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
