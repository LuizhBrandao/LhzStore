using MarketplaceApi.Domain.Enums;
using MarketplaceApi.Domain.ValueObjects;

namespace MarketplaceApi.Domain.Entities;

/// <summary>
/// Raiz de Agregado (Aggregate Root) que representa o Produto/SKU no Marketplace de Pokémon TCG.
/// Encapsula regras de precificação dinâmica, estoque e integridade de invariantes.
/// </summary>
public class Product
{
    public Guid Id { get; private set; }

    // Objeto de Valor (Value Object)
    public Sku Sku { get; private set; } = null!;

    // Informações da Carta
    public string ExternalCardId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Collection { get; private set; } = string.Empty;
    public string ImageUrl { get; private set; } = string.Empty;

    // Especificações do SKU
    public CardCondition Condition { get; private set; }
    public CardLanguage Language { get; private set; }

    // Estoque e Custos
    public int StockQuantity { get; private set; }
    public decimal AveragePurchasePrice { get; private set; }
    public decimal LigaPokemonPrice { get; private set; }

    // Regras de Negócio e Precificação Dinâmica
    public decimal BaseCost => Math.Round(AveragePurchasePrice * 1.10m, 2);
    public decimal CompetitivePrice => Math.Round(LigaPokemonPrice * 0.95m, 2);

    public decimal FinalSalePrice
    {
        get
        {
            if (CompetitivePrice > BaseCost)
            {
                return CompetitivePrice;
            }

            return BaseCost;
        }
    }

    // Construtor protegido para uso exclusivo do EF Core
    protected Product() { }

    public Product(
        string externalCardId,
        string name,
        string collection,
        string imageUrl,
        CardCondition condition,
        CardLanguage language,
        int initialStock,
        decimal averagePurchasePrice,
        decimal ligaPokemonPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da carta é obrigatório.", nameof(name));

        if (string.IsNullOrWhiteSpace(collection))
            throw new ArgumentException("A coleção da carta é obrigatória.", nameof(collection));

        if (initialStock < 0)
            throw new ArgumentException("O estoque inicial não pode ser negativo.", nameof(initialStock));

        if (averagePurchasePrice < 0)
            throw new ArgumentException("O preço médio de compra não pode ser negativo.", nameof(averagePurchasePrice));

        if (ligaPokemonPrice < 0)
            throw new ArgumentException("O preço da Liga Pokémon não pode ser negativo.", nameof(ligaPokemonPrice));

        Id = Guid.NewGuid();
        ExternalCardId = externalCardId.Trim();
        Name = name.Trim();
        Collection = collection.Trim();
        ImageUrl = imageUrl.Trim();
        Condition = condition;
        Language = language;
        Sku = Sku.Create(ExternalCardId, Condition, Language);
        StockQuantity = initialStock;
        AveragePurchasePrice = averagePurchasePrice;
        LigaPokemonPrice = ligaPokemonPrice;
    }

    /// <summary>
    /// Atualiza a quantidade absoluta de estoque garantindo consistência.
    /// </summary>
    public void UpdateStock(int newQuantity)
    {
        if (newQuantity < 0)
            throw new InvalidOperationException("Estoque não pode se tornar negativo.");

        StockQuantity = newQuantity;
    }

    /// <summary>
    /// Aplica uma alteração relativa de estoque (venda ou reposição).
    /// </summary>
    public void AdjustStock(int delta)
    {
        var updated = StockQuantity + delta;
        if (updated < 0)
            throw new InvalidOperationException($"Estoque insuficiente. Atual: {StockQuantity}, Solicitado: {Math.Abs(delta)}.");

        StockQuantity = updated;
    }

    /// <summary>
    /// Atualiza os parâmetros de precificação de mercado. Retorna true se houve alteração nos preços.
    /// </summary>
    public bool UpdatePricing(decimal averagePurchasePrice, decimal ligaPokemonPrice)
    {
        if (averagePurchasePrice < 0)
            throw new ArgumentException("O preço médio de compra não pode ser negativo.", nameof(averagePurchasePrice));

        if (ligaPokemonPrice < 0)
            throw new ArgumentException("O preço da Liga Pokémon não pode ser negativo.", nameof(ligaPokemonPrice));

        var changed = AveragePurchasePrice != averagePurchasePrice || LigaPokemonPrice != ligaPokemonPrice;

        AveragePurchasePrice = averagePurchasePrice;
        LigaPokemonPrice = ligaPokemonPrice;

        return changed;
    }

    public void UpdateDetails(string name, string collection, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da carta é obrigatório.", nameof(name));

        if (string.IsNullOrWhiteSpace(collection))
            throw new ArgumentException("A coleção da carta é obrigatória.", nameof(collection));

        Name = name.Trim();
        Collection = collection.Trim();
        ImageUrl = imageUrl.Trim();
    }

    public void UpdateImageUrl(string imageUrl)
    {
        ImageUrl = imageUrl?.Trim() ?? string.Empty;
    }
}
