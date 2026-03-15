namespace Fuzn.TestFuzn.Tests.Configuration;

[TestClass]
public class AppConfigurationManagerTests : Test
{
    [Test]
    public async Task Verify_that_AppConfigurationManager_works()
    {
        await Scenario()
            .Step("Verify GetRequiredSection", context =>
            {
                Assert.AreEqual("CustomValueThatExists", context.AppConfiguration.GetRequiredSection<CustomSectionThatExists>("CustomSectionThatExists").CustomKeyThatExists);
                Assert.Throws<InvalidOperationException>(() => context.AppConfiguration.GetRequiredSection<CustomSectionThatDoesNotExist>("CustomSectionThatDoesNotExist"));
            })
            .Step("Verify GetRequiredValue", context =>
            {
                Assert.AreEqual("ValueThatExists", context.AppConfiguration.GetRequiredValue<string>("KeyThatExists"));
                Assert.Throws<KeyNotFoundException>(() => context.AppConfiguration.GetRequiredValue<string>("KeyThatDoesNotExist"));
            })
            .Step("Verify HasSection", context =>
            {
                Assert.IsTrue(context.AppConfiguration.HasSection("CustomSectionThatExists"));
                Assert.IsFalse(context.AppConfiguration.HasSection("SectionThatDoesNotExist"));
            })
            .Step("Verify HasValue", context =>
            {
                Assert.IsTrue(context.AppConfiguration.HasValue("KeyThatExists"));
                Assert.IsFalse(context.AppConfiguration.HasValue("KeyThatDoesNotExist"));
            })
            .Run();
    }
}
