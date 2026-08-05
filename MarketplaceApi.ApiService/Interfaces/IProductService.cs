using MarketplaceApi.ApiService.Models;

namespace MarketplaceApi.ApiService.Interfaces;

public interface IProductService
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product> AddAsync(Product product);
    Task<bool> DeleteAsync(Guid id);
}