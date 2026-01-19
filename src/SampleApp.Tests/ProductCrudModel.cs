using SampleApp.WebApp.Models;

namespace SampleApp.Tests;

public partial class ProductLoadTests
{
    public class ProductCrudModel
    {
        public Product? NewProduct { get; set; } = null!;
        public Product? UpdatedProduct { get; set; } = null!;
        public string AuthToken { get; set; } = null!;
    }
}
