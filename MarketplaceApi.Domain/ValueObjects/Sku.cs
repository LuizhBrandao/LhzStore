using MarketplaceApi.Domain.Enums;

namespace MarketplaceApi.Domain.ValueObjects;

/// <summary>
/// Objeto de Valor (Value Object) que representa o SKU único de uma carta no marketplace.
/// Imutável, sem identificador próprio e com igualdade baseada exclusivamente em seus valores.
/// </summary>
public sealed record Sku
{
    public string Value { get; }

    private Sku(string value)
    {
        Value = value;
    }

    public static Sku Create(string externalCardId, CardCondition condition, CardLanguage language)
    {
        if (string.IsNullOrWhiteSpace(externalCardId))
            throw new ArgumentException("O identificador externo da carta não pode ser vazio.", nameof(externalCardId));

        var formattedSku = $"{externalCardId.Trim().ToLowerInvariant()}-{condition}-{language}";
        return new Sku(formattedSku);
    }

    public static Sku FromString(string rawSku)
    {
        if (string.IsNullOrWhiteSpace(rawSku))
            throw new ArgumentException("SKU não pode ser vazio.", nameof(rawSku));

        return new Sku(rawSku.Trim());
    }

    public override string ToString() => Value;

    public static implicit operator string(Sku sku) => sku.Value;
}
