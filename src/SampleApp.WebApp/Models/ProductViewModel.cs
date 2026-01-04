using System.ComponentModel.DataAnnotations;

namespace SampleApp.WebApp.Models;

public class ProductViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Product name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(0.01, 999999.99, ErrorMessage = "Price must be between 0.01 and 999,999.99")]
    [DataType(DataType.Currency)]
    [Display(Name = "Price")]
    public decimal Price { get; set; }
}

public class ProductListViewModel
{
    public List<Product> Products { get; set; } = new();
    public string? SearchTerm { get; set; }
}
