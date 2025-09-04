namespace Fuzn.TestFuzn.Tests.SerializerProvider;

public class ProductSystemTextJson
{
    public Guid Id { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductNewtonsoft
{
    public Guid Id { get; set; }
    [Newtonsoft.Json.JsonProperty("name")]
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class Product
{
    internal Product()
    {
        
    }
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
