using MarketplaceApi.Domain.Enums;

namespace MarketplaceApi.Application.DTOs;

public record ProductSearchDocument(
    Guid Id,
    string Sku,
    string ExternalCardId,
    string Name,
    string Collection,
    string ImageUrl,
    string Condition,
    string Language,
    int StockQuantity,
    decimal FinalSalePrice
)
{
    public static ProductSearchDocument FromDto(ProductDto product) =>
        new(
            product.Id,
            product.Sku,
            product.ExternalCardId,
            product.Name,
            product.Collection,
            product.ImageUrl,
            product.Condition,
            product.Language,
            product.StockQuantity,
            product.FinalSalePrice
        );
}

public record ProductSearchQuery(
    string? Query = null,
    string? Collection = null,
    CardCondition? Condition = null,
    CardLanguage? Language = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int Limit = 20,
    int Offset = 0
);

public record ProductSearchResult(
    IReadOnlyList<ProductSearchDocument> Hits,
    int TotalHits,
    int ProcessingTimeMs,
    string Query,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? FacetDistribution = null
);
