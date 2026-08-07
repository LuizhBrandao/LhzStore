using System.Data;
using Dapper;
using MarketplaceApi.ApiService.Interfaces;
using MarketplaceApi.ApiService.Models;

namespace MarketplaceApi.ApiService.Repositories;

public class ProductPriceHistoryRepository : IProductPriceHistoryRepository
{
    private readonly IDbConnection _dbConnection;

    public ProductPriceHistoryRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<bool> AddAsync(ProductPriceHistory priceHistory)
    {
        const string sql = @"
            INSERT INTO ProductPriceHistory (
                Id, ProductId, AveragePurchasePrice, LigaPokemonPrice, ChangeDate
            ) VALUES (
                @Id, @ProductId, @AveragePurchasePrice, @LigaPokemonPrice, @ChangeDate
            )";

        var rowsAffected = await _dbConnection.ExecuteAsync(sql, priceHistory);
        return rowsAffected > 0;
    }

    public async Task<IEnumerable<ProductPriceHistory>> GetByProductIdAsync(Guid productId)
    {
        // Trazemos o histórico ordenado do mais recente para o mais antigo
        const string sql = @"
            SELECT * FROM ProductPriceHistory 
            WHERE ProductId = @ProductId 
            ORDER BY ChangeDate DESC";

        return await _dbConnection.QueryAsync<ProductPriceHistory>(sql, new { ProductId = productId });
    }
}