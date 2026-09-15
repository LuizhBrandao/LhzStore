namespace MarketplaceApi.Domain.Entities;

/// <summary>
/// Entidade de auditoria que registra a série temporal de precificação da carta.
/// </summary>
public class ProductPriceHistory
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal AveragePurchasePrice { get; private set; }
    public decimal LigaPokemonPrice { get; private set; }
    public DateTime ChangeDate { get; private set; }

    // Construtor privado para o EF Core
    private ProductPriceHistory() { }

    public ProductPriceHistory(Guid productId, decimal averagePurchasePrice, decimal ligaPokemonPrice)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId inválido.", nameof(productId));

        if (averagePurchasePrice < 0)
            throw new ArgumentException("Preço médio de compra não pode ser negativo.", nameof(averagePurchasePrice));

        if (ligaPokemonPrice < 0)
            throw new ArgumentException("Preço da Liga Pokémon não pode ser negativo.", nameof(ligaPokemonPrice));

        Id = Guid.NewGuid();
        ProductId = productId;
        AveragePurchasePrice = averagePurchasePrice;
        LigaPokemonPrice = ligaPokemonPrice;
        ChangeDate = DateTime.UtcNow;
    }
}
