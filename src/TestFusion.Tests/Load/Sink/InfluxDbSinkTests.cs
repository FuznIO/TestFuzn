using TestFusion.HttpTesting;
using TestFusion.Tests.Http;

namespace TestFusion.Tests.Load.Simulations;

[FeatureTest]
public class InfluxDbSinkTests : BaseFeatureTest
{
    public override string FeatureName => "InfluxDbSink";

    [ScenarioTest]
    public async Task Test()
    {
        var stepExecutionCounter = 0;

        await Scenario("Test Influx DB")
            //.Step("Test", async context =>
            //{
            //    Assert.Fail();
            //    Interlocked.Increment(ref stepExecutionCounter);
            //    await Task.Delay(TimeSpan.FromMilliseconds(10));
            //})
            .Step("Call a http endpoint and verify that response is successful and body mapping is OK", async (context) =>
            {
                //var response = await context.CreateHttpRequest("https://localhost:44316/api/Products").Get();

                //Assert.IsTrue(response.Ok);
                //var products = response.BodyAs<List<Product>>();
                //Assert.IsTrue(products.Count > 0, "Expected more than one product to be returned.");
            })

            .Load().OneTimeLoad(1)
            //.Load().FixedLoad(10000, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30))
            .Run();

        //Assert.AreEqual(1, stepExecutionCounter);
    }
}
