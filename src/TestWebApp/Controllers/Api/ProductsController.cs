using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestWebApp.Models;
using TestWebApp.Services;

namespace TestWebApp.Controllers.Api;

[ApiController]
[Route("api/Products")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> Get()
    {
        var products = await _productService.GetAllProducts();
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> Get(Guid id)
    {
        var product = _productService.GetById(id);
        if (product == null)
            return NotFound();
        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult> Add(Product product)
    {
        _productService.AddProduct(product);
        return Ok();
    }

    [HttpPut]
    public async Task<IActionResult> Update(Product product)
    {
        var success = _productService.UpdateProduct(product);
        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _productService.RemoveProduct(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}
