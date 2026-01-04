using Fuzn.TestFuzn;
using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

/// <summary>
/// Unit tests for Product validation logic.
/// These tests demonstrate basic TestFuzn syntax for unit testing.
/// </summary>
[TestClass]
[Group("Product Unit Tests")]
public class ProductUnitTests : Test
{
    [Test(Name = "Verify product name validation rejects empty names",
        Description = "Ensures that product validation correctly rejects products with empty names",
        Id = "UT-001")]
    [Tags("Unit", "Validation")]
    public async Task Verify_product_name_validation_rejects_empty_names()
    {
        await Scenario()
            .Step("Create product with empty name", (context) =>
            {
                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "",
                    Price = 100
                };
                context.SetSharedData("product", product);
            })
            .Step("Validate product name is invalid", (context) =>
            {
                var product = context.GetSharedData<Product>("product");
                var isValid = !string.IsNullOrWhiteSpace(product.Name);
                Assert.IsFalse(isValid, "Empty product name should be invalid");
            })
            .Run();
    }

    [Test(Name = "Verify product price validation rejects negative prices",
        Description = "Ensures that product validation correctly rejects products with negative prices",
        Id = "UT-002")]
    [Tags("Unit", "Validation")]
    public async Task Verify_product_price_validation_rejects_negative_prices()
    {
        await Scenario()
            .Step("Create product with negative price", (context) =>
            {
                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Product",
                    Price = -50
                };
                context.SetSharedData("product", product);
            })
            .Step("Validate product price is invalid", (context) =>
            {
                var product = context.GetSharedData<Product>("product");
                var isValid = product.Price >= 0;
                Assert.IsFalse(isValid, "Negative price should be invalid");
            })
            .Run();
    }

    [Test(Name = "Verify valid product passes all validations")]
    [Tags("Unit", "Validation")]
    public async Task Verify_valid_product_passes_all_validations()
    {
        await Scenario()
            .Step("Create valid product", (context) =>
            {
                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Valid Product",
                    Price = 99.99m
                };
                context.SetSharedData("product", product);
            })
            .Step("Validate product is valid", (context) =>
            {
                var product = context.GetSharedData<Product>("product");
                
                Assert.IsNotNull(product.Id, "Product ID should not be null");
                Assert.IsFalse(string.IsNullOrWhiteSpace(product.Name), "Product name should not be empty");
                Assert.IsTrue(product.Price >= 0, "Product price should be non-negative");
            })
            .Run();
    }

    [Test(Name = "Verify product validation with multiple products using input data")]
    [Tags("Unit", "DataDriven")]
    public async Task Verify_product_validation_with_multiple_products()
    {
        await Scenario()
            .InputData(
                new Product { Id = Guid.NewGuid(), Name = "Product A", Price = 10 },
                new Product { Id = Guid.NewGuid(), Name = "Product B", Price = 20 },
                new Product { Id = Guid.NewGuid(), Name = "Product C", Price = 30 }
            )
            .InputDataBehavior(InputDataBehavior.Loop)
            .Step("Validate each product", (context) =>
            {
                var product = context.InputData<Product>();
                
                context.Comment($"Validating product: {product.Name}");
                
                Assert.IsNotNull(product.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(product.Name));
                Assert.IsTrue(product.Price > 0);
            })
            .Run();
    }

    [Skip("Example of a skipped test - remove this attribute to enable")]
    [Test(Name = "Skipped test example")]
    [Tags("Unit")]
    public async Task Skipped_test_example()
    {
        await Scenario()
            .Step("This step will not run", (context) =>
            {
                Assert.Fail("This should never execute");
            })
            .Run();
    }
}
