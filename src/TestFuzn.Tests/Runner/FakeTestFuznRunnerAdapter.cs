using System.Reflection;
using Fuzn.TestFuzn.Internals.Terminal;
using Fuzn.TestFuzn.Runner;
using Fuzn.TestFuzn.Tests.Terminal;

namespace Fuzn.TestFuzn.Tests.Runner;

internal sealed class FakeTestFuznRunnerAdapter : BaseTestFuznRunnerAdapter
{
    public FakeTestFuznRunnerAdapter(ILiveViewHost liveViewHost)
        : base(liveViewHost)
    {
    }

    public override Task ExecuteTestMethod(ITest test, MethodInfo methodInfo)
    {
        return Task.CompletedTask;
    }
}
