using System.Reflection;

namespace Fuzn.TestFuzn.Tests.Terminal;

internal sealed class FakeTest : ITest
{
    public object TestFramework { get; set; } = null!;

    public MethodInfo TestMethodInfo { get; set; } = null!;

    public TestInfo TestInfo { get; set; } = new TestInfo
    {
        Name = "Checkout_load",
        FullName = "Fuzn.TestFuzn.Tests.Terminal.ConsoleManagerTests.Checkout_load",
        Id = "checkout-load"
    };
}
