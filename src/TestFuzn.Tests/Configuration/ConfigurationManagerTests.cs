namespace Fuzn.TestFuzn.Tests.Configuration;

[TestClass]
public class ConfigurationManagerTests : TestBase
{
    public override GroupInfo Group => new() { Name = "ConfigurationManager" };

    [Test]
    public async Task Verify_that_ConfigurationManager_works()
    {
        await Scenario()
            .Step("Verify GetRequiredSection", context =>
            {
                Assert.AreEqual("CustomValueThatExists", ConfigurationManager.GetRequiredSection<CustomSectionThatExists>("CustomSectionThatExists").CustomKeyThatExists);
                Assert.Throws<InvalidOperationException>(() => ConfigurationManager.GetRequiredSection<CustomSectionThatDoesNotExist>("CustomSectionThatDoesNotExist"));
            })
            .Step("Verify GetRequiredValue", context =>
            {
                Assert.AreEqual("ValueThatExists", ConfigurationManager.GetRequiredValue<string>("KeyThatExists"));
                Assert.Throws<KeyNotFoundException>(() => ConfigurationManager.GetRequiredValue<string>("KeyThatDoesNotExist"));
            })
            .Step("Verify HasSection", context =>
            {
                Assert.IsTrue(ConfigurationManager.HasSection("CustomSectionThatExists"));
                Assert.IsFalse(ConfigurationManager.HasSection("SectionThatDoesNotExist"));
            })
            .Step("Verify HasValue", context =>
            {
                Assert.IsTrue(ConfigurationManager.HasValue("KeyThatExists"));
                Assert.IsFalse(ConfigurationManager.HasValue("KeyThatDoesNotExist"));
            })
            .Run();
    }
}

public class CustomSectionThatExists
{
    public string CustomKeyThatExists { get; set; }
}

public class CustomSectionThatDoesNotExist
{
    public string CustomKeyThatDoesNotExist { get; set; }
}
