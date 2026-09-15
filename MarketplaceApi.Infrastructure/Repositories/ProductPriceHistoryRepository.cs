using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.Repositories;
using MarketplaceApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketplaceApi.Infrastructure.Repositories;

public class ProductPriceHistoryRepository : IProductPriceHistoryRepository
{
    private readonly MarketplaceDbContext _context;

    public ProductPriceHistoryRepository(MarketplaceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ProductPriceHistory history, CancellationToken cancellationToken = default)
    {
        await _context.ProductPriceHistories.AddAsync(history, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ProductPriceHistory>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await _context.ProductPriceHistories
                             .AsNoTracking()
                             .Where(h => h.ProductId == productId)
                             .OrderByDescending(h => h.ChangeDate)
                             .ToListAsync(cancellationToken);
    }
}
