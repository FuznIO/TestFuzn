using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

[TestClass]
public class ProductHttpTests : Test
{
    private const string BaseUrl = "https://localhost:44316";

    private class ProductTestContext
    {
        public string AuthToken { get; set; }
        public Product NewProduct { get; set; }
        public Product UpdatedProduct { get; set; }
    }

    private async Task<string> GetAuthTokenAsync(IterationContext context)
    {
        var response = await context.CreateHttpRequest($"{BaseUrl}/api/Auth/token")
            .Body(new { Username = "admin", Password = "admin123" })
            .Post();

        Assert.IsTrue(response.Ok, $"Authentication failed: {response.StatusCode}");
        var tokenResponse = response.BodyAs<TokenResponse>();
        Assert.IsFalse(string.IsNullOrEmpty(tokenResponse.Token), "Token should not be empty");
        return tokenResponse.Token;
    }

    [Test]
    public async Task Verify_product_can_be_created()
    {
        await Scenario<ProductTestContext>()
            .Step("Authenticate and retrieve JWT token", async (context) =>
            {
                context.Model.AuthToken = await GetAuthTokenAsync(context);
            })
            .Step("Call POST /Products to create a new product", async (context) =>
            {
                context.Model.NewProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = 100
                };
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                    .AuthBearer(context.Model.AuthToken)
                    .Body(context.Model.NewProduct)
                    .Post();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was created", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.NewProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
                    .Get();
                Assert.IsTrue(response.Ok);
                var createdProduct = response.BodyAs<Product>();
                Assert.AreEqual(context.Model.NewProduct.Name, createdProduct.Name);
                Assert.AreEqual(context.Model.NewProduct.Price, createdProduct.Price);
            })
            .Run();
    }

    [Test]
    public async Task Verify_product_can_be_updated()
    {
        await Scenario<ProductTestContext>()
            .Step("Authenticate and retrieve JWT token", async (context) =>
            {
                context.Model.AuthToken = await GetAuthTokenAsync(context);
            })
            .Step("Call POST /Products to create a new product", async (context) =>
            {
                context.Model.NewProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = 100
                };
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                    .AuthBearer(context.Model.AuthToken)
                    .Body(context.Model.NewProduct)
                    .Post();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call PUT /Products to update the product", async (context) =>
            {
                context.Model.UpdatedProduct = new Product
                {
                    Id = context.Model.NewProduct.Id,
                    Name = "Updated Test Product",
                    Price = 150
                };
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/")
                    .AuthBearer(context.Model.AuthToken)
                    .Body(context.Model.UpdatedProduct)
                    .Put();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was updated", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.NewProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
                    .Get();
                Assert.IsTrue(response.Ok);
                var product = response.BodyAs<Product>();
                Assert.AreEqual(context.Model.UpdatedProduct.Name, product.Name);
                Assert.AreEqual(context.Model.UpdatedProduct.Price, product.Price);
            })
            .Run();
    }

    [Test]
    public async Task Verify_product_can_be_deleted()
    {
        await Scenario<ProductTestContext>()
            .Step("Authenticate and retrieve JWT token", async (context) =>
            {
                context.Model.AuthToken = await GetAuthTokenAsync(context);
            })
            .Step("Call POST /Products to create a new product", async (context) =>
            {
                context.Model.NewProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = 100
                };
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                    .AuthBearer(context.Model.AuthToken)
                    .Body(context.Model.NewProduct)
                    .Post();

                Assert.IsTrue(response.Ok);
            })
            .Step("Call DELETE /Products to delete the product", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.NewProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
                    .Delete();
                Assert.IsTrue(response.Ok);
            })
            .Step("Call GET /Products to verify the product was deleted", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.NewProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
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
