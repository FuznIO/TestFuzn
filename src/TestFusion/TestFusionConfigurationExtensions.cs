using System.Text.Json;

namespace TestFusion;
public static class TestFusionConfigurationExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public static void UseSystemTextJsonSerializer(this TestFusionConfiguration configuration, Action<SystemTextJsonSerializerProviderOptions>? providerOptions)
    {
        var options = new SystemTextJsonSerializerProviderOptions();
        providerOptions?.Invoke(options);
        configuration.AddSerializerProvider(new SystemTextJsonSerializerProvider(options));
    }

    public static void UseSystemTextJsonSerializer(this TestFusionConfiguration configuration, int priority)
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
