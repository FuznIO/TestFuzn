using Fuzn.TestFuzn.Contracts.Results.Standard;

namespace Fuzn.TestFuzn.Internals.ConsoleOutput;

internal class SubStepHelper
{
    internal static List<SubStepStandardResult> GetSubStepResults(StepStandardResult stepResult, int[] parentStepNumber, int level = 0)
    {
        var list = new List<SubStepStandardResult>();
        if (stepResult?.StepResults == null || stepResult.StepResults.Count == 0)
            return list;

        foreach (var (child, index) in stepResult.StepResults.Select((sr, i) => (sr, i)))
        {
            var node = new SubStepStandardResult
            {
                Name = child.Name,
                StepNumber = $"{string.Join(".", parentStepNumber)}.{index + 1}",
                Level = level + 1,
                Status = child.Status,
                Duration = child.Duration,
                Exception = child.Exception,
                Comments = child.Comments,
                Attachments = child.Attachments,
                Id = child.Id
            };

            node.StepResults.AddRange(GetSubStepResults(child, [.. parentStepNumber, index + 1], node.Level));

            list.Add(node);

            if (node.StepResults.Count > 0)
                list.AddRange(node.StepResults);
        }

        return list;
    }

    internal class SubStepStandardResult : StepStandardResult
    {
        public int Level { get; set; }
        public string StepNumber { get; set; }
        public new List<SubStepStandardResult> StepResults { get; set; } = [];
    }
}
