using MarketplaceApi.Application.DTOs;

namespace MarketplaceApi.Application.Services;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto?> UpdatePricingAsync(Guid id, UpdatePricingRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto?> UpdateStockAsync(Guid id, UpdateStockRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductPriceHistoryDto>> GetPriceHistoryAsync(Guid productId, CancellationToken cancellationToken = default);
}
