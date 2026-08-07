using MarketplaceApi.ApiService.Models;

namespace MarketplaceApi.ApiService.Interfaces;

public interface IProductPriceHistoryRepository
{
    Task<bool> AddAsync(ProductPriceHistory priceHistory);
    Task<IEnumerable<ProductPriceHistory>> GetByProductIdAsync(Guid productId);
}