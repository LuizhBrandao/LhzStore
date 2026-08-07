namespace MarketplaceApi.ApiService.Models;

public class ProductPriceHistory
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }

    // Guardamos a "foto" exata dos preços no momento da alteração
    public decimal AveragePurchasePrice { get; set; }
    public decimal LigaPokemonPrice { get; set; }

    // Data exata em que o preço mudou (usamos UTC para evitar problemas de fuso horário em servidores em nuvem)
    public DateTime ChangeDate { get; set; }

    // Construtor vazio para o Dapper
    public ProductPriceHistory() { }

    // Construtor prático para criarmos o log rapidamente
    public ProductPriceHistory(Guid productId, decimal averagePurchasePrice, decimal ligaPokemonPrice)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        AveragePurchasePrice = averagePurchasePrice;
        LigaPokemonPrice = ligaPokemonPrice;
        ChangeDate = DateTime.UtcNow;
    }
}