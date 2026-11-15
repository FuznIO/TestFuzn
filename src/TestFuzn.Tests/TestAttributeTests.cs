namespace Fuzn.TestFuzn.Tests;

[TestClass]
[Tags("integration")]
public class TestAttributeTests : Test
{
    [Test(Name = "Custom test name")]
    public async Task Name_from_attribute_is_used_as_test_name()
    {
        await Scenario().Step("Step 1", (context) => { });

        Assert.AreEqual("Custom test name", TestInfo.Name);
    }

    [Test]
    public async Task Method_name_is_used_as_test_name_without_attribute_name()
    {
        await Scenario().Step("Step 1", (context) => { });

        Assert.AreEqual("Method name is used as test name without attribute name", TestInfo.Name);
    }
}
