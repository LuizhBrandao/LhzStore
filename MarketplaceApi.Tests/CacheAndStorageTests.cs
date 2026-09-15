using System.Text;
using MarketplaceApi.Application.DTOs;
using MarketplaceApi.Application.Services;
using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.Enums;
using MarketplaceApi.Domain.Repositories;
using MarketplaceApi.Domain.ValueObjects;
using MarketplaceApi.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MarketplaceApi.Tests;

public class CacheAndStorageTests
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
        public readonly List<ProductPriceHistory> Histories = new();

        public Task AddAsync(ProductPriceHistory history, CancellationToken cancellationToken = default)
        {
            Histories.Add(history);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ProductPriceHistory>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ProductPriceHistory>>(Histories.Where(h => h.ProductId == productId));
    }

    [Fact]
    public async Task StorageService_LocalFallback_ShouldSaveAndRetrieveImage()
    {
        // Arrange
        var inMemoryConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BucketName"] = "test-cards",
                ["Storage:S3Endpoint"] = "" // Vazio para forçar fallback local seguro
            })
            .Build();

        var storage = new MinioImageStorageService(inMemoryConfig, NullLogger<MinioImageStorageService>.Instance);
        var fileContent = "Fake image content for Charizard card";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));

        // Act
        var relativeUrl = await storage.UploadImageAsync(stream, "charizard_front.png", "image/png", TestContext.Current.CancellationToken);
        var fileName = Path.GetFileName(relativeUrl);

        using (var downloadedStream = await storage.GetImageAsync(fileName, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(downloadedStream);
            using var reader = new StreamReader(downloadedStream);
            var downloadedContent = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.Equal(fileContent, downloadedContent);
        }

        // Cleanup
        await storage.DeleteImageAsync(fileName, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StorageService_SanitizesFileName_AgainstPathTraversal()
    {
        // Arrange
        var inMemoryConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:S3Endpoint"] = ""
            })
            .Build();

        var storage = new MinioImageStorageService(inMemoryConfig, NullLogger<MinioImageStorageService>.Instance);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        // Act: Tenta injetar path traversal no nome do arquivo
        var relativeUrl = await storage.UploadImageAsync(stream, "../../../etc/passwd.png", "image/png");
        var fileName = Path.GetFileName(relativeUrl);

        // Assert: Garante que apenas o nome do arquivo sanitizado é preservado
        Assert.DoesNotContain("..", relativeUrl);
        Assert.EndsWith("passwd.png", relativeUrl);

        // Cleanup
        await storage.DeleteImageAsync(fileName);
    }

    [Fact]
    public async Task ProductService_UpdateImageUrl_ShouldPersistNewImageAndReturnUpdatedDto()
    {
        // Arrange
        var productRepo = new InMemoryProductRepository();
        var historyRepo = new InMemoryPriceHistoryRepository();
        var service = new ProductService(productRepo, historyRepo);

        var created = await service.CreateAsync(new CreateProductRequest(
            "base1-4",
            "Charizard",
            "Base Set",
            "https://old-url.com/charizard.png",
            CardCondition.NearMint,
            CardLanguage.PT,
            1,
            100m,
            200m
        ));

        // Act
        var updated = await service.UpdateImageUrlAsync(created.Id, "/api/images/new_charizard_real.png");

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("/api/images/new_charizard_real.png", updated.ImageUrl);
    }
}
