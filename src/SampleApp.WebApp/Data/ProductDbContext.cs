using Microsoft.EntityFrameworkCore;
using SampleApp.WebApp.Models;

namespace SampleApp.WebApp.Data;

public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();
}
