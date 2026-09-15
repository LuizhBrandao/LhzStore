using System.Collections.Concurrent;
using System.Globalization;
using MarketplaceApi.Application.DTOs;
using MarketplaceApi.Application.Services;
using Meilisearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketplaceApi.Infrastructure.Search;

public class MeilisearchProductSearchService : IProductSearchService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MeilisearchProductSearchService> _logger;
    private readonly string _indexName = "products";
    private readonly ConcurrentDictionary<Guid, ProductSearchDocument> _localFallbackIndex = new();
    private MeilisearchClient? _client;
    private bool _indexConfigured;

    public MeilisearchProductSearchService(
        IConfiguration configuration,
        ILogger<MeilisearchProductSearchService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        InitializeClient();
    }

    private void InitializeClient()
    {
        var endpoint = _configuration["Meilisearch:Endpoint"];
        var apiKey = _configuration["Meilisearch:ApiKey"] ?? "masterKey123";

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogWarning("Endpoint do Meilisearch não configurado. Utilizando fallback local em memória para busca.");
            return;
        }

        try
        {
            _client = new MeilisearchClient(endpoint, apiKey);
            _logger.LogInformation("Cliente Meilisearch inicializado no endpoint: {Endpoint}", endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao inicializar o cliente Meilisearch. Fallback em memória ativado.");
            _client = null;
        }
    }

    private async Task EnsureIndexConfiguredAsync(CancellationToken ct)
    {
        if (_client == null || _indexConfigured)
            return;

        try
        {
            var index = _client.Index(_indexName);
            await _client.CreateIndexAsync(_indexName, "id", ct);

            await index.UpdateSearchableAttributesAsync(new[]
            {
                "name",
                "externalCardId",
                "collection",
                "sku"
            }, ct);

            await index.UpdateFilterableAttributesAsync(new[]
            {
                "condition",
                "language",
                "collection",
                "finalSalePrice",
                "stockQuantity"
            }, ct);

            await index.UpdateSortableAttributesAsync(new[]
            {
                "finalSalePrice",
                "stockQuantity",
                "name"
            }, ct);

            _indexConfigured = true;
            _logger.LogInformation("Índice '{Index}' configurado com sucesso no Meilisearch.", _indexName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível conectar ao Meilisearch para configurar o índice. A busca continuará funcionando em modo fallback.");
        }
    }

    public async Task IndexProductAsync(ProductDto product, CancellationToken ct = default)
    {
        var doc = ProductSearchDocument.FromDto(product);
        _localFallbackIndex[doc.Id] = doc;

        if (_client != null)
        {
            try
            {
                await EnsureIndexConfiguredAsync(ct);
                var index = _client.Index(_indexName);
                await index.AddDocumentsAsync(new[] { doc }, primaryKey: "id", cancellationToken: ct);
                _logger.LogInformation("Produto '{Sku}' indexado no Meilisearch.", doc.Sku);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao indexar produto '{Sku}' no Meilisearch.", doc.Sku);
            }
        }
    }

    public async Task RemoveProductIndexAsync(Guid productId, CancellationToken ct = default)
    {
        _localFallbackIndex.TryRemove(productId, out _);

        if (_client != null)
        {
            try
            {
                var index = _client.Index(_indexName);
                await index.DeleteOneDocumentAsync(productId.ToString(), ct);
                _logger.LogInformation("Produto '{Id}' removido do Meilisearch.", productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover produto '{Id}' do Meilisearch.", productId);
            }
        }
    }

    public async Task ReindexAllAsync(IEnumerable<ProductDto> products, CancellationToken ct = default)
    {
        var documents = products.Select(ProductSearchDocument.FromDto).ToList();

        _localFallbackIndex.Clear();
        foreach (var doc in documents)
        {
            _localFallbackIndex[doc.Id] = doc;
        }

        if (_client != null)
        {
            try
            {
                await EnsureIndexConfiguredAsync(ct);
                var index = _client.Index(_indexName);
                await index.AddDocumentsAsync(documents, primaryKey: "id", cancellationToken: ct);
                _logger.LogInformation("{Count} produtos reindexados no Meilisearch com sucesso.", documents.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao reindexar produtos em lote no Meilisearch.");
            }
        }
    }

    public async Task<ProductSearchResult> SearchAsync(ProductSearchQuery query, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (_client != null)
        {
            try
            {
                await EnsureIndexConfiguredAsync(ct);
                var index = _client.Index(_indexName);

                var filterClauses = new List<string>();

                if (query.Condition.HasValue)
                    filterClauses.Add($"condition = '{query.Condition.Value}'");

                if (query.Language.HasValue)
                    filterClauses.Add($"language = '{query.Language.Value}'");

                if (!string.IsNullOrWhiteSpace(query.Collection))
                    filterClauses.Add($"collection = '{query.Collection.Trim()}'");

                if (query.MinPrice.HasValue)
                    filterClauses.Add($"finalSalePrice >= {query.MinPrice.Value.ToString(CultureInfo.InvariantCulture)}");

                if (query.MaxPrice.HasValue)
                    filterClauses.Add($"finalSalePrice <= {query.MaxPrice.Value.ToString(CultureInfo.InvariantCulture)}");

                var searchQuery = new SearchQuery
                {
                    Limit = query.Limit,
                    Offset = query.Offset,
                    Filter = filterClauses.Count > 0 ? string.Join(" AND ", filterClauses) : null,
                    Facets = new[] { "condition", "language", "collection" }
                };

                var searchResult = await index.SearchAsync<ProductSearchDocument>(query.Query, searchQuery, ct);
                stopwatch.Stop();

                var hits = searchResult.Hits.ToList();
                var totalHits = searchResult switch
                {
                    SearchResult<ProductSearchDocument> sr => (int)sr.EstimatedTotalHits,
                    PaginatedSearchResult<ProductSearchDocument> pr => pr.TotalHits,
                    _ => hits.Count
                };

                var facets = searchResult.FacetDistribution?
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => (IReadOnlyDictionary<string, int>)kvp.Value.ToDictionary(k => k.Key, v => (int)v.Value)
                    );

                return new ProductSearchResult(
                    hits,
                    totalHits,
                    (int)stopwatch.ElapsedMilliseconds,
                    query.Query ?? string.Empty,
                    facets
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na consulta ao Meilisearch. Recorrendo ao fallback local em memória.");
            }
        }

        // Busca em modo Fallback Local
        var fallbackResults = _localFallbackIndex.Values.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var term = query.Query.Trim();
            fallbackResults = fallbackResults.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Collection.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.ExternalCardId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (query.Condition.HasValue)
        {
            var condStr = query.Condition.Value.ToString();
            fallbackResults = fallbackResults.Where(p => p.Condition.Equals(condStr, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Language.HasValue)
        {
            var langStr = query.Language.Value.ToString();
            fallbackResults = fallbackResults.Where(p => p.Language.Equals(langStr, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Collection))
        {
            fallbackResults = fallbackResults.Where(p => p.Collection.Equals(query.Collection.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (query.MinPrice.HasValue)
        {
            fallbackResults = fallbackResults.Where(p => p.FinalSalePrice >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            fallbackResults = fallbackResults.Where(p => p.FinalSalePrice <= query.MaxPrice.Value);
        }

        var total = fallbackResults.Count();
        var paginatedHits = fallbackResults
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();

        stopwatch.Stop();

        return new ProductSearchResult(
            paginatedHits,
            total,
            (int)stopwatch.ElapsedMilliseconds,
            query.Query ?? string.Empty
        );
    }
}
