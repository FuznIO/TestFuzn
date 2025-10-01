using System.Text.Json;

namespace Fuzn.TestFuzn;
public static class TestFuznConfigurationExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public static void UseSystemTextJsonSerializer(this TestFuznConfiguration configuration, Action<SystemTextJsonSerializerProviderOptions>? providerOptions)
    {
        var options = new SystemTextJsonSerializerProviderOptions();
        providerOptions?.Invoke(options);
        configuration.AddSerializerProvider(new SystemTextJsonSerializerProvider(options));
    }

    public static void UseSystemTextJsonSerializer(this TestFuznConfiguration configuration, int priority)
    {
        configuration.AddSerializerProvider(new SystemTextJsonSerializerProvider(new SystemTextJsonSerializerProviderOptions
        {
            Priority = priority
        }));
    }
}

public class SystemTextJsonSerializerProviderOptions
{
    private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    public JsonSerializerOptions JsonSerializerSettings { get; set; } = DefaultOptions;
    public int Priority { get; set; }
}
