namespace Fuzn.TestFuzn.Tests.Failing;

[TestClass]
public class DatarowAttributeTests : TestBase
{
    [DataRow("Test")]
    [Test]
    public void Should_fail_DataRow_is_not_supported(string param)
    {
        // This method should fail because MSTest DataRow is not supported.
        // DataSource is not supported either, but currently no test for it.
    }
}
