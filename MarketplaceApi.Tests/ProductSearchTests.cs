using MarketplaceApi.Application.DTOs;
using MarketplaceApi.Application.Services;
using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.Enums;
using MarketplaceApi.Domain.Repositories;
using MarketplaceApi.Domain.ValueObjects;
using MarketplaceApi.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MarketplaceApi.Tests;

public class ProductSearchTests
{
    private class InMemoryProductRepository : IProductRepository
    {
        public readonly List<Product> Products = new();

        public Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<Product>>(Products);

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Products.FirstOrDefault(p => p.Id == id));

        public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default) =>
            Task.FromResult(Products.FirstOrDefault(p => p.Sku == sku));

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            Products.Add(product);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
        {
            Products.Remove(product);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Products.Any(p => p.Id == id));
    }

    private class InMemoryPriceHistoryRepository : IProductPriceHistoryRepository
    {
        public Task AddAsync(ProductPriceHistory history, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IEnumerable<ProductPriceHistory>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ProductPriceHistory>>(new List<ProductPriceHistory>());
    }

    [Fact]
    public void ProductSearchDocument_FromDto_ShouldMapPropertiesAccurately()
    {
        // Arrange
        var product = new Product(
            "base1-4",
            "Charizard",
            "Base Set",
            "https://images.pokemontcg.io/base1/4_hires.png",
            CardCondition.NearMint,
            CardLanguage.PT,
            5,
            150m,
            250m
        );
        var dto = ProductDto.FromDomain(product);

        // Act
        var doc = ProductSearchDocument.FromDto(dto);

        // Assert
        Assert.Equal(product.Id, doc.Id);
        Assert.Equal("base1-4-NearMint-PT", doc.Sku);
        Assert.Equal("Charizard", doc.Name);
        Assert.Equal("NearMint", doc.Condition);
        Assert.Equal("PT", doc.Language);
        Assert.Equal(product.FinalSalePrice, doc.FinalSalePrice);
    }

    [Fact]
    public async Task SearchService_IndexAndSearch_ShouldReturnMatchingHits()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build(); // Sem endpoint Meili para testar fallback local resiliente
        var searchService = new MeilisearchProductSearchService(config, NullLogger<MeilisearchProductSearchService>.Instance);

        var charizard = new ProductDto(
            Guid.NewGuid(), "base1-4-NearMint-PT", "base1-4", "Charizard", "Base Set", "url",
            "NearMint", "PT", 2, 100m, 200m, 110m, 190m, 190m
        );
        var pikachu = new ProductDto(
            Guid.NewGuid(), "base1-58-Mint-EN", "base1-58", "Pikachu", "Base Set", "url",
            "Mint", "EN", 10, 20m, 50m, 22m, 47.5m, 47.5m
        );

        await searchService.IndexProductAsync(charizard);
        await searchService.IndexProductAsync(pikachu);

        // Act 1: Busca por nome
        var resultName = await searchService.SearchAsync(new ProductSearchQuery(Query: "Charizard"));

        // Assert 1
        Assert.Single(resultName.Hits);
        Assert.Equal("Charizard", resultName.Hits[0].Name);

        // Act 2: Filtro por idioma e condição
        var resultFilter = await searchService.SearchAsync(new ProductSearchQuery(Condition: CardCondition.Mint, Language: CardLanguage.EN));

        // Assert 2
        Assert.Single(resultFilter.Hits);
        Assert.Equal("Pikachu", resultFilter.Hits[0].Name);

        // Act 3: Filtro por faixa de preço
        var resultPrice = await searchService.SearchAsync(new ProductSearchQuery(MinPrice: 100m));

        // Assert 3
        Assert.Single(resultPrice.Hits);
        Assert.Equal("Charizard", resultPrice.Hits[0].Name);
    }

    [Fact]
    public async Task SearchService_RemoveIndex_ShouldExcludeFromFutureSearches()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var searchService = new MeilisearchProductSearchService(config, NullLogger<MeilisearchProductSearchService>.Instance);
        var id = Guid.NewGuid();

        var card = new ProductDto(
            id, "base1-2-NearMint-PT", "base1-2", "Blastoise", "Base Set", "url",
            "NearMint", "PT", 1, 80m, 150m, 88m, 142.5m, 142.5m
        );

        await searchService.IndexProductAsync(card);
        var beforeDelete = await searchService.SearchAsync(new ProductSearchQuery(Query: "Blastoise"));
        Assert.Single(beforeDelete.Hits);

        // Act
        await searchService.RemoveProductIndexAsync(id);

        // Assert
        var afterDelete = await searchService.SearchAsync(new ProductSearchQuery(Query: "Blastoise"));
        Assert.Empty(afterDelete.Hits);
    }

    [Fact]
    public async Task ProductService_WithSearchService_ShouldAutomaticallySynchronizeIndex()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var searchService = new MeilisearchProductSearchService(config, NullLogger<MeilisearchProductSearchService>.Instance);
        var productRepo = new InMemoryProductRepository();
        var historyRepo = new InMemoryPriceHistoryRepository();

        var productService = new ProductService(productRepo, historyRepo, searchService);

        // Act: Criação sincroniza com o índice
        var created = await productService.CreateAsync(new CreateProductRequest(
            "base1-15",
            "Venusaur",
            "Base Set",
            "url",
            CardCondition.NearMint,
            CardLanguage.PT,
            3,
            70m,
            120m
        ));

        // Assert: Pode ser buscado imediatamente
        var searchCreated = await searchService.SearchAsync(new ProductSearchQuery(Query: "Venusaur"));
        Assert.Single(searchCreated.Hits);

        // Act: Exclusão sincroniza remoção
        await productService.DeleteAsync(created.Id);
        var searchDeleted = await searchService.SearchAsync(new ProductSearchQuery(Query: "Venusaur"));
        Assert.Empty(searchDeleted.Hits);
    }
}
