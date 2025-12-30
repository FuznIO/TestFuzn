using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleApp.WebApp.Models;

namespace SampleApp.WebApp.Controllers.Mvc;

[Authorize]
[Route("Products")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ProductsController : Controller
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search)
    {
        var products = await _productService.GetAllProducts();

        if (!string.IsNullOrWhiteSpace(search))
        {
            products = products
                .Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Show latest products first
        products = products.OrderByDescending(p => p.Name).ToList();

        var viewModel = new ProductListViewModel
        {
            Products = products,
            SearchTerm = search
        };

        return View(viewModel);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View(new ProductViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ProductViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = model.Name,
            Price = model.Price
        };

        _productService.AddProduct(product);

        TempData["SuccessMessage"] = $"Product '{product.Name}' created successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFound();
        }

        var viewModel = new ProductViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price
        };

        return View(viewModel);
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Guid id, ProductViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var product = new Product
        {
            Id = model.Id,
            Name = model.Name,
            Price = model.Price
        };

        var success = _productService.UpdateProduct(product);
        if (!success)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"Product '{product.Name}' updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Delete/{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var product = _productService.GetById(id);
        if (product == null)
        {
            return NotFound();
        }

        var viewModel = new ProductViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price
        };

        return View(viewModel);
    }

    [HttpPost("Delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var product = _productService.GetById(id);
        var productName = product?.Name ?? "Product";

        var success = await _productService.RemoveProduct(id);
        if (!success)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"Product '{productName}' deleted successfully!";
        return RedirectToAction(nameof(Index));
    }
}
