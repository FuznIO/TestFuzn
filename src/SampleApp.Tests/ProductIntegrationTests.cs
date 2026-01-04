using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using Microsoft.Extensions.Logging;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

/// <summary>
/// Integration tests for the Product API.
/// These tests demonstrate TestFuzn lifecycle hooks and custom context models.
/// </summary>
[TestClass]
[Group("Product Integration Tests")]
public class ProductIntegrationTests : Test, IBeforeTest, IAfterTest
{
    private const string BaseUrl = "https://localhost:44316";

    /// <summary>
    /// Custom context model for sharing data between steps.
    /// </summary>
    private class ProductTestContext
    {
        public string AuthToken { get; set; } = string.Empty;
        public Product CreatedProduct { get; set; } = new();
        public List<Guid> ProductsToCleanup { get; set; } = new();
    }

    /// <summary>
    /// Runs before each test method.
    /// </summary>
    public Task BeforeTest(Context context)
    {
        context.Logger.LogInformation("Starting test run: {TestRunId}", context.Info.TestRunId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs after each test method.
    /// </summary>
    public Task AfterTest(Context context)
    {
        context.Logger.LogInformation("Completed test run: {TestRunId}", context.Info.TestRunId);
        return Task.CompletedTask;
    }

    [Test(Name = "Verify complete product lifecycle with custom context",
        Description = "Tests create, read, update, and delete operations using custom context model",
        Id = "IT-001")]
    [Tags("Integration", "CRUD")]
    [Metadata("Category", "API")]
    [Metadata("Priority", "High")]
    public async Task Verify_complete_product_lifecycle()
    {
        await Scenario<ProductTestContext>()
            .BeforeScenario((context) =>
            {
                context.Logger.LogInformation("Starting product lifecycle test scenario");
            })
            .BeforeIteration((context) =>
            {
                context.Model.ProductsToCleanup = new List<Guid>();
            })
            .Step("Authenticate and get JWT token", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Auth/token")
                    .Body(new { Username = "admin", Password = "admin123" })
                    .Post();

                Assert.IsTrue(response.Ok, $"Authentication failed: {response.StatusCode}");
                var tokenResponse = response.BodyAs<TokenResponse>();
                context.Model.AuthToken = tokenResponse.Token;
                
                context.Comment("Successfully authenticated");
            })
            .Step("Create a new product", async (context) =>
            {
                context.Model.CreatedProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = $"Integration Test Product {DateTime.UtcNow:yyyyMMddHHmmss}",
                    Price = 149.99m
                };

                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                    .AuthBearer(context.Model.AuthToken)
                    .Body(context.Model.CreatedProduct)
                    .Post();

                Assert.IsTrue(response.Ok, $"Create product failed: {response.StatusCode}");
                context.Model.ProductsToCleanup.Add(context.Model.CreatedProduct.Id);
                
                context.Comment($"Created product: {context.Model.CreatedProduct.Name}");
            })
            .Step("Verify product exists", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.CreatedProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
                    .Get();

                Assert.IsTrue(response.Ok, "Product should exist after creation");
                var product = response.BodyAs<Product>();
                Assert.AreEqual(context.Model.CreatedProduct.Name, product.Name);
                Assert.AreEqual(context.Model.CreatedProduct.Price, product.Price);
            })
            .Step("Update product price", async (context) =>
            {
                var updatedProduct = new Product
                {
                    Id = context.Model.CreatedProduct.Id,
                    Name = context.Model.CreatedProduct.Name,
                    Price = 199.99m
                };

                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products")
                    .AuthBearer(context.Model.AuthToken)
                    .Body(updatedProduct)
                    .Put();

                Assert.IsTrue(response.Ok, "Update should succeed");
                context.Model.CreatedProduct.Price = updatedProduct.Price;
            })
            .Step("Verify price was updated", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.CreatedProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
                    .Get();

                Assert.IsTrue(response.Ok);
                var product = response.BodyAs<Product>();
                Assert.AreEqual(199.99m, product.Price);
            })
            .Step("Delete the product", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.CreatedProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
                    .Delete();

                Assert.IsTrue(response.Ok, "Delete should succeed");
                context.Model.ProductsToCleanup.Remove(context.Model.CreatedProduct.Id);
            })
            .Step("Verify product was deleted", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Products/{context.Model.CreatedProduct.Id}")
                    .AuthBearer(context.Model.AuthToken)
                    .Get();

                Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            })
            .AfterIteration(async (context) =>
            {
                // Cleanup any products that weren't deleted during the test
                foreach (var productId in context.Model.ProductsToCleanup)
                {
                    await context.CreateHttpRequest($"{BaseUrl}/api/Products/{productId}")
                        .AuthBearer(context.Model.AuthToken)
                        .Delete();
                }
            })
            .AfterScenario((context) =>
            {
                context.Logger.LogInformation("Product lifecycle test completed successfully");
            })
            .Run();
    }

    [Test(Name = "Verify sub-steps work correctly")]
    [Tags("Integration", "SubSteps")]
    public async Task Verify_sub_steps_work_correctly()
    {
        await Scenario()
            .Step("Parent step with sub-steps", async (context) =>
            {
                var response = await context.CreateHttpRequest($"{BaseUrl}/api/Auth/token")
                    .Body(new { Username = "admin", Password = "admin123" })
                    .Post();

                Assert.IsTrue(response.Ok);
                var token = response.BodyAs<TokenResponse>().Token;
                context.SetSharedData("token", token);

                context.Step("Sub-step 1: Create product", async (subContext) =>
                {
                    var authToken = subContext.GetSharedData<string>("token");
                    var product = new Product
                    {
                        Id = Guid.NewGuid(),
                        Name = "Sub-step Test Product",
                        Price = 50
                    };

                    var createResponse = await subContext.CreateHttpRequest($"{BaseUrl}/api/Products")
                        .AuthBearer(authToken)
                        .Body(product)
                        .Post();

                    Assert.IsTrue(createResponse.Ok);
                    subContext.SetSharedData("productId", product.Id);
                });

                context.Step("Sub-step 2: Delete product", async (subContext) =>
                {
                    var authToken = subContext.GetSharedData<string>("token");
                    var productId = subContext.GetSharedData<Guid>("productId");

                    var deleteResponse = await subContext.CreateHttpRequest($"{BaseUrl}/api/Products/{productId}")
                        .AuthBearer(authToken)
                        .Delete();

                    Assert.IsTrue(deleteResponse.Ok);
                });
            })
            .Run();
    }
}
