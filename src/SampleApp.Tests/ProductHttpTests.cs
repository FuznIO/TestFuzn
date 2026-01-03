using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

[TestClass]
public class ProductHttpTests : Test
{
    private const string BaseUrl = "https://localhost:44316";

    [Test]
    public async Task Verify_that_products_can_be_managed()
    {
        Product newProduct = null;
        Product updatedProduct = null;
        string authToken = null;

        await Scenario()
            .Step("Authenticate and retrieve JWT token", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Auth/token")
                    .Body(new { Username = "admin", Password = "admin123" })
                    .Post();

                Assert.IsTrue(response.Ok, $"Authentication failed: {response.StatusCode}");
                var tokenResponse = response.BodyAs<TokenResponse>();
                authToken = tokenResponse.Token;
                Assert.IsFalse(string.IsNullOrEmpty(authToken), "Token should not be empty");
            })
            .Step("Call POST /Products to create a new product", async (context) =>
            {
                newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = 100
                };
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                    .AuthBearer(authToken)
                    .Body(newProduct)
                    .Post();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was created", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{newProduct.Id}")
                    .AuthBearer(authToken)
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
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/")
                    .AuthBearer(authToken)
                    .Body(updatedProduct)
                    .Put();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was updated", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{newProduct.Id}")
                    .AuthBearer(authToken)
                    .Get();
                Assert.IsTrue(response.Ok);
                var product = response.BodyAs<Product>();
                Assert.AreEqual(updatedProduct.Name, product.Name);
                Assert.AreEqual(updatedProduct.Price, product.Price);
            })
            .Step("Call DELETE /Products to delete the product", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{newProduct.Id}")
                    .AuthBearer(authToken)
                    .Delete();
                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was deleted", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{newProduct.Id}")
                    .AuthBearer(authToken)
                    .Get();
                Assert.IsFalse(response.Ok);
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
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                    .Get();

                Assert.IsFalse(response.Ok);
                Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
            })
            .Run();
    }
}

record TokenResponse(string Token, DateTime ExpiresAt);
