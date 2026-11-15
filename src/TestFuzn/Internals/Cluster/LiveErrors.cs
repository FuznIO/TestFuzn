using Fuzn.TestFuzn.Contracts.Results.Load;
using Fuzn.TestFuzn.Internals.Cluster.Protocol;
using Fuzn.TestFuzn.Internals.Results.Load;

namespace Fuzn.TestFuzn.Internals.Cluster;

internal static class LiveErrors
{
    public static List<LiveError> Of(ScenarioLoadResult result)
    {
        var errors = new List<LiveError>();
        if (result.Hooks != null)
        {
            foreach (var hook in result.Hooks)
                Add(errors, result.ScenarioName + " > " + hook.Name, hook);
        }
        if (result.Steps != null)
        {
            foreach (var step in result.Steps.Values)
                Add(errors, result.ScenarioName + " > " + step.Name, step);
        }
        return errors;
    }

    public static string? StoppedReason(ScenarioLoadResult result)
    {
        if (result.AssertWhileRunningException == null)
            return null;
        return result.AssertWhileRunningException.Message;
    }

    private static void Add(List<LiveError> errors, string path, StepLoadResult step)
    {
        if (step == null)
            return;

        if (step.Errors != null)
        {
            foreach (var error in step.Errors)
            {
                if (error.Value == null)
                    continue;
                errors.Add(new LiveError
                {
                    Path = path,
                    Message = error.Value.Message,
                    Count = error.Value.Count,
                    IsOther = error.Key == ErrorShape.OtherErrorsKey
                });
            }
        }

        if (step.Steps != null)
        {
            foreach (var child in step.Steps)
                Add(errors, path + " > " + child.Name, child);
        }
    }
}
