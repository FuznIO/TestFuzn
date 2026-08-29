using System.Reflection;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.StandaloneRunner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.StandaloneRunner;

/// <summary>
/// The real standalone adapter over a test host: everything it renders lands in the host's
/// <see cref="FakeTerminalWriter"/>, its color mode and layout width come from the host's
/// hand-resolved capabilities, and no test method is ever invoked.
/// </summary>
internal sealed class FakeStandaloneRunnerAdapter : BaseStandaloneRunnerAdapter
{
    public FakeStandaloneRunnerAdapter(ILiveViewHost liveViewHost)
        : base(liveViewHost)
    {
    }

    public override Task ExecuteTestMethod(ITest test, MethodInfo methodInfo)
    {
        return Task.CompletedTask;
    }
}
