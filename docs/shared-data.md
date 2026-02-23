# Shared Data

## Sharing Data in Standard Tests

In **standard tests**, you can use method-scoped variables to share data between steps since iterations
run sequentially:

```csharp
[Test]
public async Task Standard_test_with_variables()
{
    string authToken = null; // Method variable works for standard tests

    await Scenario()
        .Step("Authenticate", context => { authToken = "abc123"; })
        .Step("Use Token", context => { Console.WriteLine(authToken); })
        .Run();
}
```

---

## Sharing Data in Load Tests

In **load tests**, iterations run in parallel across multiple threads. Method-scoped variables are **not thread-safe** and will cause race conditions. Instead, use one of these approaches:

### SetSharedData / GetSharedData

Share untyped data between steps within the same iteration. Data is isolated per iteration and is safe to use in parallel load tests and reusable steps.

```csharp
.Step("Set Data", context =>
{
    context.SetSharedData("productId", Guid.NewGuid());
    context.SetSharedData("authToken", "abc123");
})
.Step("Get Data", context =>
{
    var productId = context.GetSharedData<Guid>("productId");
    var authToken = context.GetSharedData<string>("authToken");
})
```

### Custom Context Models

Share typed data between steps using custom context models. The context is iteration-scoped and safe to use in parallel load tests and reusable steps.

```csharp
public class ProductCrudModel
{
    public Product? NewProduct { get; set; }
    public Product? UpdatedProduct { get; set; }
    public string AuthToken { get; set; }
}

[Test]
public async Task Custom_context_example()
{
    await Scenario<ProductCrudModel>()
        .Step("Authenticate and create product", context =>
        {
            context.Model.AuthToken = "abc123";
            context.Model.NewProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Test Product",
                Price = 100
            };
        })
        .Step("Verify product", context =>
        {
            Assert.IsNotNull(context.Model.AuthToken);
            Assert.AreEqual("Test Product", context.Model.NewProduct?.Name);
        })
        .Run();
}
```

---

[? Back to Table of Contents](README.md)
