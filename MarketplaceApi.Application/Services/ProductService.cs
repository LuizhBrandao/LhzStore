using MarketplaceApi.Application.DTOs;
using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.Repositories;

namespace MarketplaceApi.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IProductPriceHistoryRepository _historyRepository;
    private readonly IProductSearchService? _searchService;

    public ProductService(
        IProductRepository productRepository,
        IProductPriceHistoryRepository historyRepository,
        IProductSearchService? searchService = null)
    {
        _productRepository = productRepository;
        _historyRepository = historyRepository;
        _searchService = searchService;
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        return products.Select(ProductDto.FromDomain);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product is null ? null : ProductDto.FromDomain(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product(
            request.ExternalCardId,
            request.Name,
            request.Collection,
            request.ImageUrl,
            request.Condition,
            request.Language,
            request.StockQuantity,
            request.AveragePurchasePrice,
            request.LigaPokemonPrice
        );

        await _productRepository.AddAsync(product, cancellationToken);

        // Se o produto começa com preços definidos, inicializa o histórico
        if (product.AveragePurchasePrice > 0 || product.LigaPokemonPrice > 0)
        {
            var history = new ProductPriceHistory(product.Id, product.AveragePurchasePrice, product.LigaPokemonPrice);
            await _historyRepository.AddAsync(history, cancellationToken);
        }

        var dto = ProductDto.FromDomain(product);
        if (_searchService != null)
        {
            await _searchService.IndexProductAsync(dto, cancellationToken);
        }

        return dto;
    }

    public async Task<ProductDto?> UpdatePricingAsync(Guid id, UpdatePricingRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        var changed = product.UpdatePricing(request.AveragePurchasePrice, request.LigaPokemonPrice);
        if (changed)
        {
            await _productRepository.UpdateAsync(product, cancellationToken);

            var history = new ProductPriceHistory(product.Id, product.AveragePurchasePrice, product.LigaPokemonPrice);
            await _historyRepository.AddAsync(history, cancellationToken);
        }

        var dto = ProductDto.FromDomain(product);
        if (_searchService != null)
        {
            await _searchService.IndexProductAsync(dto, cancellationToken);
        }

        return dto;
    }

    public async Task<ProductDto?> UpdateStockAsync(Guid id, UpdateStockRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        product.UpdateStock(request.Quantity);
        await _productRepository.UpdateAsync(product, cancellationToken);

        var dto = ProductDto.FromDomain(product);
        if (_searchService != null)
        {
            await _searchService.IndexProductAsync(dto, cancellationToken);
        }

        return dto;
    }

    public async Task<ProductDto?> UpdateImageUrlAsync(Guid id, string imageUrl, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        product.UpdateImageUrl(imageUrl);
        await _productRepository.UpdateAsync(product, cancellationToken);

        var dto = ProductDto.FromDomain(product);
        if (_searchService != null)
        {
            await _searchService.IndexProductAsync(dto, cancellationToken);
        }

        return dto;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return false;

        await _productRepository.DeleteAsync(product, cancellationToken);

        if (_searchService != null)
        {
            await _searchService.RemoveProductIndexAsync(id, cancellationToken);
        }

        return true;
    }

    public async Task<IEnumerable<ProductPriceHistoryDto>> GetPriceHistoryAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var history = await _historyRepository.GetByProductIdAsync(productId, cancellationToken);
        return history.Select(ProductPriceHistoryDto.FromDomain);
    }
}
