using TestFusion.Contracts.Results.Load;

namespace TestFusion;

public class AssertStepStats
{
    public string StepName { get; }
    public AssertStats Ok { get; }
    public AssertStats Failed { get; }

    internal AssertStepStats(StepLoadResult stepResult)
    {
        StepName = stepResult.Name;
        Ok = new AssertStats(stepResult.Ok);
        Failed = new AssertStats(stepResult.Failed);
    }
}
