namespace MarketplaceApi.Application.Services;

public interface ICacheLockService
{
    /// <summary>
    /// Tenta adquirir um lock distribuído no Redis para uma chave específica.
    /// Retorna true se o lock foi adquirido com sucesso; false caso a chave já esteja bloqueada.
    /// </summary>
    Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>
    /// Libera o lock distribuído para a chave.
    /// </summary>
    Task ReleaseLockAsync(string key, CancellationToken ct = default);
}
