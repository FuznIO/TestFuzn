using Fuzn.FluentHttp;
using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

[TestClass]
public class ProductHttpTests : Test
{
    [Test]
    public async Task Verify_product_crud_operations()
    {
        Product? newProduct = null!;
        Product? updatedProduct = null!;
        string authToken = null!;

        await Scenario()
            .Step("Authenticate and retrieve JWT token", async (context) =>
            {
                authToken = await TokenHelper.GetAuthToken(context);
            })
            .Step("Call POST /Products to create a new product", async (context) =>
            {
                newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = 100
                };

                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                    .WithAuthBearer(authToken)
                    .WithContent(newProduct)
                    .Post();

                Assert.IsTrue(response.IsSuccessful);
            })
            .Step("Call GET /Products to verify the product was created", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")
                    .WithAuthBearer(authToken)
                    .Get<Product>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.AreEqual(newProduct.Name, response.Data.Name);
                Assert.AreEqual(newProduct.Price, response.Data.Price);
            })
            .Step("Call PUT /Products to update the product", async (context) =>
            {
                updatedProduct = new Product
                {
                    Id = newProduct.Id,
                    Name = "Updated Test Product",
                    Price = 150
                };
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products/")
                    .WithAuthBearer(authToken)
                    .WithContent(updatedProduct)
                    .Put();

                Assert.IsTrue(response.IsSuccessful);
            })
            .Step("Call GET /Products to verify the product was updated", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")
                    .WithAuthBearer(authToken)
                    .Get<Product>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.AreEqual(updatedProduct.Name, response.Data.Name);
                Assert.AreEqual(updatedProduct.Price, response.Data.Price);
            })
            .Step("Call DELETE /Products to delete the product", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")
                    .WithAuthBearer(authToken)
                    .Delete();

                Assert.IsTrue(response.IsSuccessful);
            })
            .Step("Call GET /Products to verify the product was deleted", async (context) =>
            {
                var response = await context.CreateHttpRequest($"https://localhost:44316/api/Products/{newProduct.Id}")
                    .WithAuthBearer(authToken)
                    .Get();

                Assert.IsFalse(response.IsSuccessful);
                Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            })
            .Run();
    }

    [Test]
    public async Task Verify_unauthorized_request_returns_401()
    {
        await Scenario()
            .Step("Call GET /Products without authentication", async (context) =>
            {
                var response = await context.CreateHttpRequest("https://localhost:44316/api/Products")
                    .Get();

                Assert.IsFalse(response.IsSuccessful);
                Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
            })
            .Run();
    }
}
