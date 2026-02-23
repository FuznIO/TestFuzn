# Input Data

Input data provides per-iteration values for a scenario. 
Standard tests use input data to determine how many times the scenario runs 
(once if no input data is defined, otherwise once per input item), 
while load tests run the number of iterations defined by their simulations. 
For load tests, input data is reused and fed multiple times when the number of iterations 
exceeds the number of input data items.

Both simple types (e.g. `string`, `int`) and complex types (custom classes, records, DTOs) are supported.

---

## Static Input Data

```csharp
.InputData("Laptop", "Keyboard", "Monitor")
```

## Static Input Data (Complex Type)

```csharp
.InputData(
    new Product { Id = Guid.NewGuid(), Name = "Laptop", Price = 999.99m },
    new Product { Id = Guid.NewGuid(), Name = "Keyboard", Price = 49.99m }
)
```

## Input Data from Function (Sync)

```csharp
.InputDataFromList((context) =>
{
    return new List<object> { "Laptop", "Keyboard", "Monitor" };
})
```

## Input Data from Function (Async)

```csharp
.InputDataFromList(async (context) =>
{
    var products = await productService.GetAllProducts();
    return products.Cast<object>().ToList();
})
```

---

## Input Data Behaviors

Control how input data is consumed:

```csharp
.InputDataBehavior(InputDataBehavior.Loop)              // Sequential (default)
.InputDataBehavior(InputDataBehavior.Random)            // Random selection
.InputDataBehavior(InputDataBehavior.LoopThenRandom)    // Loop first, then random (load tests)
.InputDataBehavior(InputDataBehavior.LoopThenRepeatLast) // Loop, then repeat last item (load tests)
```

---

## Accessing Input Data

```csharp
.Step("Use Input Data", context =>
{
    var productName = context.InputData<string>();
    context.Logger.LogInformation($"Processing product: {productName}");
})
```

---

## Customizing Input Data Display

For **standard tests**, input data is displayed in console output and HTML reports. Override `ToString()` to customize how it appears:

```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    
    public override string ToString()
    {
        return $"Product: {Name} (${Price})";
    }
}
```

Without overriding `ToString()`, the output shows the fully qualified type name (e.g., `SampleApp.WebApp.Models.Product`).

> **Note**: Load tests do not display individual input data values in reports.

---

```markdown
[← Back to Table of Contents](README.md)
