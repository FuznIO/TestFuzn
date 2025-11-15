using Microsoft.EntityFrameworkCore;
using SampleApp.WebApp.Data;
using SampleApp.WebApp.Models;

public class ProductService
{
    private readonly ProductDbContext _db;

    public ProductService(ProductDbContext db)
    {
        _db = db;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await _db.Products.AsNoTracking().ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        return await _db.Products.FindAsync(id);
    }

    public async Task<Product> AddProductAsync(Product product)
    {
        product.Id = Guid.NewGuid();
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<bool> UpdateProductAsync(Product product)
    {
        var exists = await _db.Products.AnyAsync(p => p.Id == product.Id);
        if (!exists) return false;

        _db.Products.Update(product);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveProductAsync(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    }
}
