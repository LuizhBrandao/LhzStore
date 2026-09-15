using MarketplaceApi.Domain.Entities;

namespace MarketplaceApi.Domain.Repositories;

public interface IProductPriceHistoryRepository
{
    Task AddAsync(ProductPriceHistory history, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductPriceHistory>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
}
