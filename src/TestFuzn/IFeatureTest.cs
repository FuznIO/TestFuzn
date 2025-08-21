namespace FuznLabs.TestFuzn;

public interface IFeatureTest
{
    public string FeatureName { get; }
    Task InitScenarioTest(Context context);
    Task CleanupScenarioTest(Context context);
}
