using Fuzn.TestFuzn.Contracts.Results.Load;

namespace Fuzn.TestFuzn;

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
