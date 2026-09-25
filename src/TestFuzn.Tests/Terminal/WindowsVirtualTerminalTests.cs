using Fuzn.TestFuzn.Internals.Terminal;

namespace Fuzn.TestFuzn.Tests.Terminal;

[TestClass]
public class WindowsVirtualTerminalTests : Test
{
    [Test]
    public async Task Verify_TryEnable_is_a_no_op_success_on_non_windows()
    {
        await Scenario()
            .Step("TryEnable never throws and reports success where no enablement is needed", context =>
            {
                var enabled = WindowsVirtualTerminal.TryEnable();

                if (!OperatingSystem.IsWindows())
                    Assert.IsTrue(enabled);
            })
            .Run();
    }
}
