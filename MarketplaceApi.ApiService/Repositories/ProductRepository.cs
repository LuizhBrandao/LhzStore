using System.Data;
using Dapper;
using MarketplaceApi.ApiService.Interfaces;
using MarketplaceApi.ApiService.Models;

namespace MarketplaceApi.ApiService.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IDbConnection _dbConnection;

    // Injeção de dependência da conexão com o banco (fornecida pelo Aspire)
    public ProductRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        // O Dapper fará o match automático das colunas com as propriedades da classe Product,
        // ignorando as propriedades calculadas (FinalSalePrice, Sku, etc).
        const string sql = @"
            SELECT 
                Id, ExternalCardId, Name, Collection, ImageUrl, 
                Condition, Language, StockQuantity, 
                AveragePurchasePrice, LigaPokemonPrice
            FROM Products";

        return await _dbConnection.QueryAsync<Product>(sql);
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT 
                Id, ExternalCardId, Name, Collection, ImageUrl, 
                Condition, Language, StockQuantity, 
                AveragePurchasePrice, LigaPokemonPrice
            FROM Products 
            WHERE Id = @Id";

        return await _dbConnection.QuerySingleOrDefaultAsync<Product>(sql, new { Id = id });
    }

    public async Task<bool> AddAsync(Product product)
    {
        // Ao inserir, enviamos apenas os dados base.
        const string sql = @"
            INSERT INTO Products (
                Id, ExternalCardId, Name, Collection, ImageUrl, 
                Condition, Language, StockQuantity, 
                AveragePurchasePrice, LigaPokemonPrice
            ) VALUES (
                @Id, @ExternalCardId, @Name, @Collection, @ImageUrl, 
                @Condition, @Language, @StockQuantity, 
                @AveragePurchasePrice, @LigaPokemonPrice
            )";

        var rowsAffected = await _dbConnection.ExecuteAsync(sql, product);
        return rowsAffected > 0;
    }

    public async Task<bool> UpdateAsync(Product product)
    {
        const string sql = @"
            UPDATE Products SET 
                ExternalCardId = @ExternalCardId,
                Name = @Name,
                Collection = @Collection,
                ImageUrl = @ImageUrl,
                Condition = @Condition,
                Language = @Language,
                StockQuantity = @StockQuantity,
                AveragePurchasePrice = @AveragePurchasePrice,
                LigaPokemonPrice = @LigaPokemonPrice
            WHERE Id = @Id";

        var rowsAffected = await _dbConnection.ExecuteAsync(sql, product);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        const string sql = "DELETE FROM Products WHERE Id = @Id";
        var rowsAffected = await _dbConnection.ExecuteAsync(sql, new { Id = id });
        return rowsAffected > 0;
    }
}