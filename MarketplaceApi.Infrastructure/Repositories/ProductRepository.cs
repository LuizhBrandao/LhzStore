using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.Repositories;
using MarketplaceApi.Domain.ValueObjects;
using MarketplaceApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketplaceApi.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly MarketplaceDbContext _context;

    public ProductRepository(MarketplaceDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
                             .AsNoTracking()
                             .OrderBy(p => p.Name)
                             .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
                             .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default)
    {
        return await _context.Products
                             .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products.AnyAsync(p => p.Id == id, cancellationToken);
    }
}
