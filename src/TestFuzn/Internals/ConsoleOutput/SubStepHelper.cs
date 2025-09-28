using Fuzn.TestFuzn.Contracts.Results.Feature;

namespace Fuzn.TestFuzn.Internals.ConsoleOutput;

internal class SubStepHelper
{
    internal static List<SubStepFeatureResult> GetSubStepResults(StepFeatureResult stepResult, int level = 0)
    {
        var list = new List<SubStepFeatureResult>();

        if (stepResult?.StepResults == null || stepResult.StepResults.Count == 0)
            return list;

        foreach (var child in stepResult.StepResults)
        {
            var node = new SubStepFeatureResult
            {
                Name = child.Name,
                Level = level + 1,
                Status = child.Status,
                Duration = child.Duration,
                Exception = child.Exception
            };

            node.StepResults.AddRange(GetSubStepResults(child, node.Level));

            list.Add(node);

            if (node.StepResults.Count > 0)
                list.AddRange(node.StepResults);
        }

        return list;
    }

    internal class SubStepFeatureResult : StepFeatureResult
    {
        public int Level { get; set; }
        public new List<SubStepFeatureResult> StepResults { get; set; } = [];
    }
}
