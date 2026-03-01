using Fuzn.TestFuzn.Internals.ConsoleOutput;
using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Logger;

internal class ConsoleManager
{
    private TestExecutionState _testExecutionState = null!;
    private ConsoleWriter _consoleWriter;
    private Task _realtimeLogging = null!;
    private CancellationTokenSource _ctSource = new CancellationTokenSource();

    public ConsoleManager(
        TestExecutionState testExecutionState,
        ConsoleWriter consoleWriter)
    {
        _testExecutionState = testExecutionState;
        _consoleWriter = consoleWriter;
    }

    public void StartRealtimeConsoleOutputIfEnabled()
    {
        var task = Task.CompletedTask;
        if (_testExecutionState.TestFramework.SupportsRealTimeConsoleOutput)
        {
            task = Task.Run(async () => await StartRealtimeConsoleOutput(_ctSource.Token));
        }

        _realtimeLogging = task;
    }

    private async Task StartRealtimeConsoleOutput(CancellationToken cancellationToken)
    {
        var loadTestMetrics = new Dictionary<string, LiveMetrics>();

        foreach (var scenario in _testExecutionState.Scenarios)
        {
            loadTestMetrics.TryAdd(scenario.Name, new LiveMetrics
            {
                ScenarioLoadResultSnapshot = _testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult(),
                Status = "Running"
            });
        }

        var live = new LiveLoadTestDisplay(loadTestMetrics, cancellationToken);
        var task = Task.Run(live.Show, cancellationToken);
        
        while (!_ctSource.Token.IsCancellationRequested)
        {
            UpdateMetrics(loadTestMetrics);
            if (_testExecutionState.IsConsumingCompleted)
                break;

            await DelayHelper.Delay(TimeSpan.FromMilliseconds(1000), _ctSource.Token);
        }
        
        UpdateMetrics(loadTestMetrics);
        live.KeepRunning = false;
        await task;
    }

    private void UpdateMetrics(Dictionary<string, LiveMetrics> loadTestMetrics)
    {
        foreach (var scenario in _testExecutionState.Scenarios)
        {
            if (_testExecutionState.IsScenarioExecutionComplete(scenario.Name) && loadTestMetrics[scenario.Name].ConsoleCompleted)
                continue;

            var updatedSnapshot = _testExecutionState.LoadCollectors[scenario.Name].GetCurrentResult();
            if (_testExecutionState.IsScenarioExecutionComplete(scenario.Name) || 
                _testExecutionState.ExecutionStatus == ExecutionStatus.Completed
                || _testExecutionState.ExecutionStatus == ExecutionStatus.Stopped)
            {
                loadTestMetrics[scenario.Name].Status = updatedSnapshot.Status.ToString();
                loadTestMetrics[scenario.Name].ConsoleCompleted = true;
            }
            else
            {
                loadTestMetrics[scenario.Name].Status = "Running";
                loadTestMetrics[scenario.Name].Duration = DateTime.UtcNow - updatedSnapshot.StartTime();
            }

            loadTestMetrics[scenario.Name].ScenarioLoadResultSnapshot = updatedSnapshot;
        }
    }

    public async Task Complete()
    {
        await _ctSource.CancelAsync();
        await _realtimeLogging;

        _consoleWriter.WriteSummary(_testExecutionState);
    }
}
