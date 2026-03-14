namespace Fuzn.TestFuzn.Tests.Configuration;

[TestClass]
public class ConfigurationManagerTests : Test
{
    [Test]
    public async Task Verify_that_ConfigurationManager_works()
    {
        await Scenario()
            .Step("Verify GetRequiredSection", context =>
            {
                Assert.AreEqual("CustomValueThatExists", context.Configuration.GetRequiredSection<CustomSectionThatExists>("CustomSectionThatExists").CustomKeyThatExists);
                Assert.Throws<InvalidOperationException>(() => context.Configuration.GetRequiredSection<CustomSectionThatDoesNotExist>("CustomSectionThatDoesNotExist"));
            })
            .Step("Verify GetRequiredValue", context =>
            {
                Assert.AreEqual("ValueThatExists", context.Configuration.GetRequiredValue<string>("KeyThatExists"));
                Assert.Throws<KeyNotFoundException>(() => context.Configuration.GetRequiredValue<string>("KeyThatDoesNotExist"));
            })
            .Step("Verify HasSection", context =>
            {
                Assert.IsTrue(context.Configuration.HasSection("CustomSectionThatExists"));
                Assert.IsFalse(context.Configuration.HasSection("SectionThatDoesNotExist"));
            })
            .Step("Verify HasValue", context =>
            {
                Assert.IsTrue(context.Configuration.HasValue("KeyThatExists"));
                Assert.IsFalse(context.Configuration.HasValue("KeyThatDoesNotExist"));
            })
            .Run();
    }
}
