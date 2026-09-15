using MarketplaceApi.Application.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace MarketplaceApi.Infrastructure.Cache;

public class RedisLockService : ICacheLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisLockService> _logger;
    private readonly string _lockValue = Guid.NewGuid().ToString("N");

    public RedisLockService(IConnectionMultiplexer redis, ILogger<RedisLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"lock:{key}";

        try
        {
            var acquired = await db.StringSetAsync(lockKey, _lockValue, expiry, When.NotExists);
            if (acquired)
            {
                _logger.LogInformation("Distributed lock adquirido com sucesso para a chave: {Key}", key);
            }
            else
            {
                _logger.LogWarning("Falha ao adquirir lock para a chave: {Key}. Recurso já em processamento.", key);
            }

            return acquired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tentar adquirir lock distribuído no Redis para a chave: {Key}", key);
            return false;
        }
    }

    public async Task ReleaseLockAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = $"lock:{key}";

        try
        {
            // Libera a chave apenas se o valor for o mesmo que adquiriu
            const string luaScript = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            await db.ScriptEvaluateAsync(luaScript, new RedisKey[] { lockKey }, new RedisValue[] { _lockValue });
            _logger.LogInformation("Distributed lock liberado para a chave: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao liberar lock distribuído no Redis para a chave: {Key}", key);
        }
    }
}
