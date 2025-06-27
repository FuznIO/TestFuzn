using System.Text.Json;

namespace TestFusion;
public static class TestFusionConfigurationExtensions
{
    public static void UseDefaultSerializer(this TestFusionConfiguration configuration, Action<JsonSerializerOptions>? configureOptions = null)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        configureOptions?.Invoke(options);
        configuration.AddSerializerProvider(new DefaultSerializerProvider(options));
    }
}
