//using SampleApp.WebApp.Models;
//using TestFusion.Feature;
//using TestFusion.Http;

//namespace SampleApp.Tests.FeatureTests;

//[TestClass]
//public class ProductE2ETests : TestFusion.Feature.FeatureTests
//{
//    public override string FeatureName => "Create/Update/Delete Product";

//    [ScenarioTest]
//    public async Task Verify()
//    {
//        var scenario = new Scenario("Verify that create, update and delete product works")
//            .Step("Create a product", async (context) =>
//            {
//                var request = new HttpRequest(HttpMethod.Post, "https://localhost:44316/api/Products");
//                request.Body = new
//                {
//                    name = "productfoo",
//                    price = 35
//                };

//                var response = await request.Send();
//                Assert.IsTrue(response.Ok);

//                var body = response.BodyAsJson();
//                var productId = body.id.ToString();

//                context.Data["ProductId"] = productId;

//            }).Step("Verify that the product was created", async (context) =>
//            {
//                var productId = context.Data["ProductId"];
//                var request = new HttpRequest(HttpMethod.Get, $"https://localhost:44316/api/Products/{productId.ToString()}");

//                var response = await request.Send();
//                Assert.IsTrue(response.Ok);

//                var body = response.BodyAs<Product>();


//                Assert.AreEqual(body.Name, "productfoo");
//                Assert.AreEqual(body.Price, 35);
//            });

//        await Runner.Run(scenario);
//    }
//}
