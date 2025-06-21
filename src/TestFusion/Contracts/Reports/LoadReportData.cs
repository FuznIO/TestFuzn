using TestFusion.Contracts.Results.Load;

namespace TestFusion.Contracts.Reports;

public class LoadReportData
{
    public string TestRunId { get; internal set; }
    public string TestsOutputDirectory { get; internal set; }
    public DateTime TestStarted { get; set; }
    public DateTime TestFinished { get; set; }
    public ScenarioLoadResult ScenarioResults { get; internal set; }
}