using TestFusion.HttpTesting.Internals;

namespace TestFusion.HttpTesting;

public static class TestFusionConfigurationExtensions
{
    public static void UseHttpTesting(this TestFusionConfiguration configuration, Action<HttpTestingConfiguration> configureAction = null)
    {
        var httpTestingConfiguration = new HttpTestingConfiguration();
        if (configureAction != null)
            configureAction(httpTestingConfiguration);

        GlobalState.Configuration = httpTestingConfiguration;
        GlobalState.HasBeenInitialized = true;

        configuration.AddContextPlugin(new HttpTestingPlugin());
    }
}
