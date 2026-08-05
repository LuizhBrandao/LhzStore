namespace MarketplaceApi.ApiService.Models;

// Enums para padronizar as características do SKU e evitar erros de digitação no banco
public enum CardCondition
{
    Mint,
    NearMint,
    SlightlyPlayed,
    ModeratelyPlayed,
    HeavilyPlayed,
    Damaged
}

public enum CardLanguage
{
    PT, // Português
    EN, // Inglês
    JP  // Japonês
}

public class Product
{
    public Guid Id { get; set; }

    // 1. Dados Base da Carta (Integrados com a API do Pokémon TCG)
    public string ExternalCardId { get; set; } = string.Empty; // Ex: "base1-4"
    public string Name { get; set; } = string.Empty;           // Ex: "Charizard"
    public string Collection { get; set; } = string.Empty;     // Ex: "Base Set"
    public string ImageUrl { get; set; } = string.Empty;       // URL da imagem

    // 2. Especificações do SKU
    public CardCondition Condition { get; set; }
    public CardLanguage Language { get; set; }

    // Propriedade calculada para gerar o SKU único da sua loja (Ex: base1-4-NearMint-PT)
    public string Sku => $"{ExternalCardId}-{Condition}-{Language}";

    // 3. Controle de Estoque e Custos (Mapeados via Dapper)
    public int StockQuantity { get; set; }
    public decimal AveragePurchasePrice { get; set; }
    public decimal LigaPokemonPrice { get; set; }

    // 4. Regras de Negócio e Precificação (Encapsuladas no Domínio)

    // Piso de Segurança: Custo + 10%
    public decimal BaseCost => AveragePurchasePrice * 1.10m;

    // Teto Competitivo: Preço da Liga - 5%
    public decimal CompetitivePrice => LigaPokemonPrice * 0.95m;

    // Preço Final de Venda calculado dinamicamente
    public decimal FinalSalePrice
    {
        get
        {
            // Se o preço competitivo for maior que o nosso piso de segurança,
            // maximizamos o lucro sendo a opção mais barata da Liga.
            if (CompetitivePrice > BaseCost)
            {
                return CompetitivePrice;
            }

            // Caso contrário, a proteção automática entra em ação e trava no Piso de Segurança
            return BaseCost;
        }
    }

    // Construtor vazio necessário para o Dapper realizar o mapeamento
    public Product() { }
}