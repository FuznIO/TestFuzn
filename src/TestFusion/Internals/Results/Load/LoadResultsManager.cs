using System.Diagnostics;
using TestFusion.Internals.State;
using TestFusion.Contracts.Reports;

namespace TestFusion.Internals.Results.Load;

internal class LoadResultsManager
{
    private readonly Dictionary<string, ScenarioLoadCollector> _scenarios = new();
    private readonly SharedExecutionState _sharedExecutionState;
    private Stopwatch _totalRunDuration = new();

    public TimeSpan TotalRunDuration => _totalRunDuration.Elapsed;

    public LoadResultsManager(SharedExecutionState sharedExecutionState)
    {
        _sharedExecutionState = sharedExecutionState;
    }

    public void Init(Scenario[] scenarios)
    {
        _totalRunDuration.Start();
       
        foreach (var scenario in scenarios)
        {
            var scenarioCollector = new ScenarioLoadCollector();
            scenarioCollector.Init(scenario, _sharedExecutionState.FeatureTestClassInstance.FeatureName);
            
            _scenarios[scenario.Name] = scenarioCollector;
        }
    }

    public ScenarioLoadCollector GetScenarioCollector(string scenarioName)
    {
        if (_scenarios.TryGetValue(scenarioName, out var scenarioCollector))
        {
            return scenarioCollector;
        }
        throw new KeyNotFoundException($"Scenario '{scenarioName}' not found.");
    }

    public void Complete()
    {
        _totalRunDuration.Stop();

        foreach (var scenario in _scenarios)
        {
            scenario.Value.MarkAsCompleted();
        }
    }

    public void WriteReports()
    {
        if (_sharedExecutionState.TestType != TestType.Load)
            return;

        var loadReports = GlobalState.Configuration.LoadReports;

        if (loadReports == null || loadReports.Count == 0)
            return;

        foreach (var scenario in _scenarios)
        {
            var loadReportData = new LoadReportData();
            loadReportData.TestRunId = GlobalState.TestRunId;
            loadReportData.TestsOutputDirectory = GlobalState.TestsOutputDirectory;
            loadReportData.ScenarioResults = scenario.Value.GetCurrentResult();

            foreach (var loadReport in loadReports)
                loadReport.WriteReport(loadReportData);
        }
    }
}
