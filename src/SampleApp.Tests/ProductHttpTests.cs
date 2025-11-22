using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

[FeatureTest]
public class ProductHttpTests : BaseFeatureTest
{
    public override string FeatureName => "Product catalog";

    [ScenarioTest]
    public async Task Verify_that_products_can_be_managed()
    {
        Product newProduct = null;
        Product updatedProduct = null;

        await Scenario()
            .Step("Call POST /Products to create a new product", async (context) =>
            {
                newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = 100
                };
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                    .Body(newProduct)
                    .Post();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was created", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")               
                                .Get();
                Assert.IsTrue(response.Ok);
                var createdProduct = response.BodyAs<Product>();
                Assert.AreEqual(newProduct.Name, createdProduct.Name);
                Assert.AreEqual(newProduct.Price, createdProduct.Price);
            })
            .Step("Call PUT /Products to update the product", async (context) =>
            {
                updatedProduct = new Product();
                updatedProduct.Id = newProduct.Id;
                updatedProduct.Name = "Updated Test Product";
                updatedProduct.Price = 150;
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/")
                    .Body(updatedProduct)
                    .Put();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was updated", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")               
                                .Get();
                Assert.IsTrue(response.Ok);
                var product = response.BodyAs<Product>();
                Assert.AreEqual(updatedProduct.Name, product.Name);
                Assert.AreEqual(updatedProduct.Price, product.Price);
            })
            .Step("Call DELETE /Products to delete the product", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")               
                                .Delete();
                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was deleted", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")               
                                .Get();
                Assert.IsFalse(response.Ok);
                Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            })
            .Run();
    }
}
