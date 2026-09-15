using MarketplaceApi.Application.DTOs;

namespace MarketplaceApi.Application.Services;

public interface IProductSearchService
{
    /// <summary>
    /// Realiza busca textual tolerante a erros de digitação (Fuzzy Search) com filtros facetados.
    /// </summary>
    Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Indexa ou atualiza um produto individual no motor de busca.
    /// </summary>
    Task IndexProductAsync(ProductDto product, CancellationToken ct = default);

    /// <summary>
    /// Remove um produto do índice de busca.
    /// </summary>
    Task RemoveProductIndexAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Reindexação em lote de todos os produtos no Meilisearch.
    /// </summary>
    Task ReindexAllAsync(IEnumerable<ProductDto> products, CancellationToken ct = default);
}
