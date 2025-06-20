using TestFusion.Internals.ConsoleOutput;
using TestFusion.Internals.Results.Load;
using TestFusion.Internals.State;
using TestFusion.Contracts.Adapters;

namespace TestFusion.Internals.Logger;

internal class ConsoleManager(
    ITestFrameworkAdapter _testFramework,
    SharedExecutionState _sharedExecutionState,
    ConsoleWriter _consoleWriter,
    LoadResultsManager loadResultsManager)
{
    private Task _realtimeLogging;
    private CancellationTokenSource _ctSource = new CancellationTokenSource();

    public bool IsEnabled => _testFramework.SupportsRealTimeConsoleOutput;

    public void StartRealtimeConsoleOutputIfEnabled()
    {
        var task = Task.CompletedTask;
        if (_testFramework.SupportsRealTimeConsoleOutput)
        {
            task = Task.Run(async () => await StartRealtimeConsoleOutput(_ctSource.Token));
        }

        _realtimeLogging = task;
    }

    private async Task StartRealtimeConsoleOutput(CancellationToken cancellationToken)
    {
        var loadTestMetrics = new Dictionary<string, LiveMetrics>();

        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            loadTestMetrics.TryAdd(scenario.Name, new LiveMetrics
            {
                ScenarioLoadResultSnapshot = loadResultsManager.GetScenarioCollector(scenario.Name).GetCurrentResult(),
                Status = "Running"
            });
        }

        var live = new LiveLoadTestDisplay(loadTestMetrics, cancellationToken);
        var task = Task.Run(live.Show, cancellationToken);
        
        while (!_ctSource.Token.IsCancellationRequested)
        {
            UpdateMetrics(loadTestMetrics);
            if (_sharedExecutionState.IsConsumingCompleted)
                break;

            await DelayHelper.Delay(TimeSpan.FromMilliseconds(1000), _ctSource.Token);
        }
        
        UpdateMetrics(loadTestMetrics);
        live.KeepRunning = false;
        await task;
    }

    private void UpdateMetrics(Dictionary<string, LiveMetrics> loadTestMetrics)
    {
        foreach (var scenario in _sharedExecutionState.Scenarios)
        {
            if (_sharedExecutionState.IsScenarioExecutionComplete(scenario.Name) && loadTestMetrics[scenario.Name].ConsoleCompleted)
                continue;

            var updatedSnapshot = loadResultsManager.GetScenarioCollector(scenario.Name).GetCurrentResult();
            if (_sharedExecutionState.IsScenarioExecutionComplete(scenario.Name) || _sharedExecutionState.ExecutionStatus == ExecutionStatus.Completed)
            {
                loadTestMetrics[scenario.Name].Status = updatedSnapshot.Status.ToString();
                loadTestMetrics[scenario.Name].ConsoleCompleted = true;
            }
            else
            {
                loadTestMetrics[scenario.Name].Status = "Running";
                loadTestMetrics[scenario.Name].Duration = DateTime.UtcNow - updatedSnapshot.StartTime;
            }

            loadTestMetrics[scenario.Name].ScenarioLoadResultSnapshot = updatedSnapshot;
        }
    }

    public async Task Complete()
    {
        await _ctSource.CancelAsync();
        await _realtimeLogging;

        _consoleWriter.WriteSummary();
    }
}
