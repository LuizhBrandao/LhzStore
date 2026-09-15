using MarketplaceApi.Application.DTOs;
using MarketplaceApi.Application.Services;
using MarketplaceApi.Domain.Repositories;
using MarketplaceApi.Infrastructure.Cache;
using MarketplaceApi.Infrastructure.Persistence;
using MarketplaceApi.Infrastructure.Repositories;
using MarketplaceApi.Infrastructure.Storage;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurações padrão do .NET Aspire (OpenTelemetry, HealthChecks, Resiliência)
builder.AddServiceDefaults();

// 2. Recursos de Persistência Poliglota via .NET Aspire
// PostgreSQL (Banco Transacional ACID)
builder.AddNpgsqlDbContext<MarketplaceDbContext>("marketplacedb");

// Redis (Cache Distribuído, Output Cache e Conexão para Locks)
builder.AddRedisClient("redis");
builder.AddRedisOutputCache("redis");
builder.AddRedisDistributedCache("redis");

// 3. Injeção de Dependências das Camadas DDD & Infraestrutura
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductPriceHistoryRepository, ProductPriceHistoryRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddSingleton<IImageStorageService, MinioImageStorageService>();
builder.Services.AddSingleton<ICacheLockService, RedisLockService>();

// 4. OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 5. Garante a criação do esquema no PostgreSQL ao iniciar
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

// 6. Ativação dos middlewares
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 7. Endpoints da API REST (Minimal APIs)
var productsGroup = app.MapGroup("/api/products").WithTags("Products");

// Listagem pública com Cache de Saída no Redis (Tag: "products-cache", TTL: 5 min)
productsGroup.MapGet("/", async (IProductService productService, CancellationToken ct) =>
{
    var products = await productService.GetAllAsync(ct);
    return Results.Ok(products);
})
.CacheOutput(policy => policy.Tag("products-cache").Expire(TimeSpan.FromMinutes(5)))
.WithName("GetAllProducts");

productsGroup.MapGet("/{id:guid}", async (Guid id, IProductService productService, CancellationToken ct) =>
{
    var product = await productService.GetByIdAsync(id, ct);
    return product is not null ? Results.Ok(product) : Results.NotFound();
})
.WithName("GetProductById");

productsGroup.MapPost("/", async (
    CreateProductRequest request,
    IProductService productService,
    IOutputCacheStore cacheStore,
    CancellationToken ct) =>
{
    try
    {
        var created = await productService.CreateAsync(request, ct);
        // Invalida o cache da vitrine pública no Redis
        await cacheStore.EvictByTagAsync("products-cache", ct);
        return Results.CreatedAtRoute("GetProductById", new { id = created.Id }, created);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateProduct");

productsGroup.MapPut("/{id:guid}/pricing", async (
    Guid id,
    UpdatePricingRequest request,
    IProductService productService,
    IOutputCacheStore cacheStore,
    CancellationToken ct) =>
{
    try
    {
        var updated = await productService.UpdatePricingAsync(id, request, ct);
        if (updated is null)
            return Results.NotFound();

        // Invalida o cache no Redis para refletir o novo preço imediatamente
        await cacheStore.EvictByTagAsync("products-cache", ct);
        return Results.Ok(updated);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateProductPricing");

productsGroup.MapPut("/{id:guid}/stock", async (
    Guid id,
    UpdateStockRequest request,
    IProductService productService,
    ICacheLockService lockService,
    IOutputCacheStore cacheStore,
    CancellationToken ct) =>
{
    // Adquire lock distribuído no Redis para evitar concorrência em itens de estoque unitário
    var lockKey = $"stock-{id}";
    var acquired = await lockService.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(5), ct);

    if (!acquired)
    {
        return Results.Conflict(new { error = "O estoque deste item está sendo atualizado por outra operação. Tente novamente." });
    }

    try
    {
        var updated = await productService.UpdateStockAsync(id, request, ct);
        if (updated is null)
            return Results.NotFound();

        await cacheStore.EvictByTagAsync("products-cache", ct);
        return Results.Ok(updated);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    finally
    {
        await lockService.ReleaseLockAsync(lockKey, ct);
    }
})
.WithName("UpdateProductStock");

productsGroup.MapDelete("/{id:guid}", async (
    Guid id,
    IProductService productService,
    IOutputCacheStore cacheStore,
    CancellationToken ct) =>
{
    var deleted = await productService.DeleteAsync(id, ct);
    if (!deleted)
        return Results.NotFound();

    await cacheStore.EvictByTagAsync("products-cache", ct);
    return Results.NoContent();
})
.WithName("DeleteProduct");

productsGroup.MapGet("/{id:guid}/history", async (Guid id, IProductService productService, CancellationToken ct) =>
{
    var history = await productService.GetPriceHistoryAsync(id, ct);
    return Results.Ok(history);
})
.WithName("GetProductPriceHistory");

// 8. Endpoints de Armazenamento de Imagens das Cartas (Object Storage / MinIO)
var imagesGroup = app.MapGroup("/api").WithTags("Images");

imagesGroup.MapPost("/products/{id:guid}/images", async (
    Guid id,
    IFormFile file,
    IProductService productService,
    IImageStorageService storageService,
    IOutputCacheStore cacheStore,
    CancellationToken ct) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "Arquivo de imagem inválido ou vazio." });

    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(extension))
        return Results.BadRequest(new { error = "Formato de arquivo não suportado. Use JPG, PNG ou WEBP." });

    var product = await productService.GetByIdAsync(id, ct);
    if (product is null)
        return Results.NotFound(new { error = "Produto não encontrado." });

    using var stream = file.OpenReadStream();
    var imageUrl = await storageService.UploadImageAsync(stream, file.FileName, file.ContentType, ct);

    var updated = await productService.UpdateImageUrlAsync(id, imageUrl, ct);
    await cacheStore.EvictByTagAsync("products-cache", ct);

    return Results.Ok(new { message = "Imagem enviada com sucesso.", imageUrl, product = updated });
})
.DisableAntiforgery()
.WithName("UploadProductImage");

imagesGroup.MapGet("/images/{fileName}", async (string fileName, IImageStorageService storageService, CancellationToken ct) =>
{
    var stream = await storageService.GetImageAsync(fileName, ct);
    if (stream is null)
        return Results.NotFound();

    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    var contentType = extension switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg"
    };

    return Results.File(stream, contentType);
})
.WithName("GetImage");

app.MapDefaultEndpoints();

app.Run();