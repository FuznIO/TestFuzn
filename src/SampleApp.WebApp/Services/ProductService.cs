using SampleApp.WebApp.Models;
using System.Collections.Concurrent;

public class ProductService
{
    private static ConcurrentDictionary<Guid, Product> _products = new();

    public async Task<List<Product>> GetAllProducts()
    {
        return _products.Values.ToList();
    }

    public Product? GetById(Guid id)
    {
        _products.TryGetValue(id, out var product);

        return product;
    }

    public void AddProduct(Product product)
    {
        _products.AddOrUpdate(product.Id, product, (key, oldValue) => product);
    }

    public bool UpdateProduct(Product product)
    {
        if (_products.ContainsKey(product.Id))
        {
            _products[product.Id] = product;
            return true;
        }

        return false;
    }

    public async Task<bool> RemoveProduct(Guid id)
    {
        _products.TryRemove(id, out var removedProduct);
        if (removedProduct == null) 
            return false;

        return true;
    }
}
