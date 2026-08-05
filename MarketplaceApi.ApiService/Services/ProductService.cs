using MarketplaceApi.ApiService.Interfaces;
using MarketplaceApi.ApiService.Models;

namespace MarketplaceApi.ApiService.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    // Injeção de Dependência: O serviço pede um repositório para funcionar
    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Product> AddAsync(Product product)
    {
        // Correção do erro: Como Product agora é uma "class", 
        // apenas atribuímos o novo ID diretamente à propriedade.
        product.Id = Guid.NewGuid();

        await _repository.AddAsync(product);

        return product;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        // Aqui poderíamos validar se o produto existe antes de tentar deletar
        return await _repository.DeleteAsync(id);
    }
}