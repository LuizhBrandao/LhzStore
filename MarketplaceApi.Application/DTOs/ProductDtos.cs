using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.Enums;

namespace MarketplaceApi.Application.DTOs;

public record ProductDto(
    Guid Id,
    string Sku,
    string ExternalCardId,
    string Name,
    string Collection,
    string ImageUrl,
    string Condition,
    string Language,
    int StockQuantity,
    decimal AveragePurchasePrice,
    decimal LigaPokemonPrice,
    decimal BaseCost,
    decimal CompetitivePrice,
    decimal FinalSalePrice
)
{
    public static ProductDto FromDomain(Product product) =>
        new(
            product.Id,
            product.Sku.Value,
            product.ExternalCardId,
            product.Name,
            product.Collection,
            product.ImageUrl,
            product.Condition.ToString(),
            product.Language.ToString(),
            product.StockQuantity,
            product.AveragePurchasePrice,
            product.LigaPokemonPrice,
            product.BaseCost,
            product.CompetitivePrice,
            product.FinalSalePrice
        );
}

public record CreateProductRequest(
    string ExternalCardId,
    string Name,
    string Collection,
    string ImageUrl,
    CardCondition Condition,
    CardLanguage Language,
    int StockQuantity,
    decimal AveragePurchasePrice,
    decimal LigaPokemonPrice
);

public record UpdatePricingRequest(
    decimal AveragePurchasePrice,
    decimal LigaPokemonPrice
);

public record UpdateStockRequest(
    int Quantity
);

public record ProductPriceHistoryDto(
    Guid Id,
    Guid ProductId,
    decimal AveragePurchasePrice,
    decimal LigaPokemonPrice,
    DateTime ChangeDate
)
{
    public static ProductPriceHistoryDto FromDomain(ProductPriceHistory history) =>
        new(history.Id, history.ProductId, history.AveragePurchasePrice, history.LigaPokemonPrice, history.ChangeDate);
}
