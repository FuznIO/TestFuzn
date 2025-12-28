using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

[TestClass]
public class ProductLoadTests : BaseFeatureTest
{
    public override string FeatureName => "Product catalog";

    [Test]
    public async Task Verify_that_get_products_scales()
    {
        await Scenario()
            .InputDataFromList(async (context) =>
            {
                // Create 100 products
                var products = new List<object>();

                for (int i = 0; i < 100; i++)
                {
                    var product = new Product();
                    product.Id = Guid.NewGuid();
                    product.Name = "Test Product" + product.Id.ToString();
                    product.Price = 100;

                    var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                        .Body(product)
                        .Post();
                    Assert.IsTrue(response.Ok);
                    products.Add(product);
                }

                return products;
            })
            .InputDataBehavior(InputDataBehavior.Loop)
            .Step("Call GET /Products{productId} to verify its performance", async (context) =>
            {
                var product = context.InputData<Product>();

                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{product.Id}")
                    .Get();
                Assert.IsTrue(response.Ok);
                var retrievedProduct = response.BodyAs<Product>();
                Assert.IsNotNull(retrievedProduct);
                Assert.AreEqual(product.Id, retrievedProduct.Id);
                Assert.AreEqual(product.Name, retrievedProduct.Name);
                Assert.AreEqual(product.Price, retrievedProduct.Price);
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(500, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)))
            .Load().AssertWhileRunning((context, stats) =>
            {
                Assert.AreEqual(0, stats.Failed.RequestCount);
            })
            .Load().AssertWhenDone((context, stats) =>
            {
                Assert.AreEqual(0, stats.Failed.RequestCount);
            })
            .Run();
    }
}
