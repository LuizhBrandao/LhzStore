using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.Enums;
using MarketplaceApi.Domain.ValueObjects;
using Xunit;

namespace MarketplaceApi.Tests;

public class ProductDomainTests
{
    [Fact]
    public void Sku_ShouldBeFormattedCorrectly_AndBeEqualByValue()
    {
        // Arrange & Act
        var sku1 = Sku.Create("base1-4", CardCondition.NearMint, CardLanguage.PT);
        var sku2 = Sku.Create("BASE1-4", CardCondition.NearMint, CardLanguage.PT);

        // Assert
        Assert.Equal("base1-4-NearMint-PT", sku1.Value);
        Assert.Equal(sku1, sku2); // Igualdade por valor
        Assert.True(sku1 == sku2);
    }

    [Fact]
    public void Sku_WithEmptyExternalId_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Sku.Create("", CardCondition.Mint, CardLanguage.EN));
    }

    [Fact]
    public void Product_Creation_ShouldCalculateSkuAndSetProperties()
    {
        // Arrange & Act
        var product = new Product(
            externalCardId: "base1-4",
            name: "Charizard",
            collection: "Base Set",
            imageUrl: "https://example.com/charizard.png",
            condition: CardCondition.NearMint,
            language: CardLanguage.PT,
            initialStock: 2,
            averagePurchasePrice: 200.00m,
            ligaPokemonPrice: 350.00m
        );

        // Assert
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("base1-4-NearMint-PT", product.Sku.Value);
        Assert.Equal(2, product.StockQuantity);
        Assert.Equal(200.00m, product.AveragePurchasePrice);
        Assert.Equal(350.00m, product.LigaPokemonPrice);
    }

    [Fact]
    public void Product_Pricing_WhenCompetitivePriceIsAboveBaseCost_ShouldUseCompetitivePrice()
    {
        // Arrange
        // Custo médio: R$ 100.00 -> Piso (+10%): R$ 110.00
        // Preço Liga: R$ 200.00 -> Teto (-5%): R$ 190.00
        var product = new Product("base1-4", "Charizard", "Base Set", "url", CardCondition.NearMint, CardLanguage.PT, 1, 100.00m, 200.00m);

        // Act & Assert
        Assert.Equal(110.00m, product.BaseCost);
        Assert.Equal(190.00m, product.CompetitivePrice);
        Assert.Equal(190.00m, product.FinalSalePrice); // Deve escolher o preço competitivo (maior lucro garantindo competitividade)
    }

    [Fact]
    public void Product_Pricing_WhenCompetitivePriceIsBelowBaseCost_ShouldLockAtBaseCost()
    {
        // Arrange
        // Custo médio: R$ 100.00 -> Piso (+10%): R$ 110.00
        // Preço Liga: R$ 100.00 -> Teto (-5%): R$ 95.00
        var product = new Product("base1-4", "Charizard", "Base Set", "url", CardCondition.NearMint, CardLanguage.PT, 1, 100.00m, 100.00m);

        // Act & Assert
        Assert.Equal(110.00m, product.BaseCost);
        Assert.Equal(95.00m, product.CompetitivePrice);
        Assert.Equal(110.00m, product.FinalSalePrice); // Trava de segurança no Piso de Custo + 10%
    }

    [Fact]
    public void Product_StockUpdate_NegativeStock_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var product = new Product("base1-4", "Charizard", "Base Set", "url", CardCondition.NearMint, CardLanguage.PT, 3, 100m, 200m);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => product.UpdateStock(-1));
    }

    [Fact]
    public void Product_AdjustStock_InsufficientStock_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var product = new Product("base1-4", "Charizard", "Base Set", "url", CardCondition.NearMint, CardLanguage.PT, 1, 100m, 200m);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => product.AdjustStock(-2));
    }

    [Fact]
    public void Product_AdjustStock_ValidDelta_ShouldUpdateQuantity()
    {
        // Arrange
        var product = new Product("base1-4", "Charizard", "Base Set", "url", CardCondition.NearMint, CardLanguage.PT, 2, 100m, 200m);

        // Act
        product.AdjustStock(-1);

        // Assert
        Assert.Equal(1, product.StockQuantity);

        product.AdjustStock(5);
        Assert.Equal(6, product.StockQuantity);
    }
}
