using Fuzn.TestFuzn.Internals.State;

namespace Fuzn.TestFuzn.Internals.Cluster.Remote;

internal class ClusterRuntime
{
    private ControllerRun? _controllerRun;

    public Task<bool> RunRemote(TestExecutionState state, IServiceProvider serviceProvider)
    {
        return new RemoteClient(state, serviceProvider).Run();
    }

    public async Task RunController(TestExecutionState state, IServiceProvider serviceProvider)
    {
        _controllerRun = new ControllerRun(state, serviceProvider);
        await _controllerRun.Run();
    }

    public async Task AfterControllerSession(TestSession session)
    {
        // A test method that failed before its scenario ran leaves no controller run; the worker app then
        // tells Cockpit the controller ended, with its output.
        if (_controllerRun != null)
            await _controllerRun.StoreReportAndEnd(session);
    }
}
