using Fuzn.FluentHttp;
using Fuzn.TestFuzn;
using Fuzn.TestFuzn.Plugins.Http;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

[TestClass]
public partial class ProductLoadTests : Test
{
    [Test]
    public async Task Verify_product_crud_operations()
    {
        await Scenario<ProductCrudModel>()
            .Step("Authenticate and retrieve JWT token", async (context) =>
            {
                context.Model.AuthToken = await TokenHelper.GetAuthToken(context);
            })
            .Step("Call POST /Products to create a new product", async (context) =>
            {
                context.Model.NewProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = 100
                };

                var response = await context.CreateRequest("https://localhost:44316/api/Products")
                    .WithAuthBearer(context.Model.AuthToken)
                    .WithContent(context.Model.NewProduct)
                    .Post();

                Assert.IsTrue(response.IsSuccessful);
            })
            .Step("Call GET /Products to verify the product was created", async (context) =>
            {
                var response = await context.CreateRequest($"https://localhost:44316/api/Products/{context.Model.NewProduct!.Id}")
                    .WithAuthBearer(context.Model.AuthToken)
                    .Get<Product>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.AreEqual(context.Model.NewProduct.Name, response.Data.Name);
                Assert.AreEqual(context.Model.NewProduct.Price, response.Data.Price);
            })
            .Step("Call PUT /Products to update the product", async (context) =>
            {
                context.Model.UpdatedProduct = new Product
                {
                    Id = context.Model.NewProduct!.Id,
                    Name = "Updated Test Product",
                    Price = 150
                };
                var response = await context.CreateRequest("https://localhost:44316/api/Products/")
                    .WithAuthBearer(context.Model.AuthToken)
                    .WithContent(context.Model.UpdatedProduct)
                    .Put();

                Assert.IsTrue(response.IsSuccessful);
            })
            .Step("Call GET /Products to verify the product was updated", async (context) =>
            {
                var response = await context.CreateRequest($"https://localhost:44316/api/Products/{context.Model.NewProduct!.Id}")
                    .WithAuthBearer(context.Model.AuthToken)
                    .Get<Product>();

                Assert.IsTrue(response.IsSuccessful);
                Assert.IsNotNull(response.Data);
                Assert.AreEqual(context.Model.UpdatedProduct!.Name, response.Data.Name);
                Assert.AreEqual(context.Model.UpdatedProduct.Price, response.Data.Price);
            })
            .Step("Call DELETE /Products to delete the product", async (context) =>
            {
                var response = await context.CreateRequest($"https://localhost:44316/api/Products/{context.Model.NewProduct!.Id}")
                    .WithAuthBearer(context.Model.AuthToken)
                    .Delete();

                Assert.IsTrue(response.IsSuccessful);
            })
            .Step("Call GET /Products to verify the product was deleted", async (context) =>
            {
                var response = await context.CreateRequest($"https://localhost:44316/api/Products/{context.Model.NewProduct!.Id}")
                    .WithAuthBearer(context.Model.AuthToken)
                    .Get();

                Assert.IsFalse(response.IsSuccessful);
                Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            })
            .Load().Simulations((context, simulations) => simulations.FixedLoad(100, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15)))
            .Load().AssertWhenDone((context, result) =>
            {
                Assert.AreEqual(0, result.Failed.RequestCount, "There should be no failing steps");
            })
            .Run();
    }
}
