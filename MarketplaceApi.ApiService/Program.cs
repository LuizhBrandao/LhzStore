using MarketplaceApi.Application.DTOs;
using MarketplaceApi.Application.Services;
using MarketplaceApi.Domain.Repositories;
using MarketplaceApi.Infrastructure.Persistence;
using MarketplaceApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurações padrão do .NET Aspire (OpenTelemetry, HealthChecks, Resiliência)
builder.AddServiceDefaults();

// 2. Registro do PostgreSQL via .NET Aspire
builder.AddNpgsqlDbContext<MarketplaceDbContext>("marketplacedb");

// 3. Injeção de Dependências das Camadas DDD
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductPriceHistoryRepository, ProductPriceHistoryRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

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

// 6. Swagger UI em desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 7. Endpoints da API REST (Minimal APIs)
var productsGroup = app.MapGroup("/api/products").WithTags("Products");

productsGroup.MapGet("/", async (IProductService productService, CancellationToken ct) =>
{
    var products = await productService.GetAllAsync(ct);
    return Results.Ok(products);
})
.WithName("GetAllProducts");

productsGroup.MapGet("/{id:guid}", async (Guid id, IProductService productService, CancellationToken ct) =>
{
    var product = await productService.GetByIdAsync(id, ct);
    return product is not null ? Results.Ok(product) : Results.NotFound();
})
.WithName("GetProductById");

productsGroup.MapPost("/", async (CreateProductRequest request, IProductService productService, CancellationToken ct) =>
{
    try
    {
        var created = await productService.CreateAsync(request, ct);
        return Results.CreatedAtRoute("GetProductById", new { id = created.Id }, created);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateProduct");

productsGroup.MapPut("/{id:guid}/pricing", async (Guid id, UpdatePricingRequest request, IProductService productService, CancellationToken ct) =>
{
    try
    {
        var updated = await productService.UpdatePricingAsync(id, request, ct);
        return updated is not null ? Results.Ok(updated) : Results.NotFound();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateProductPricing");

productsGroup.MapPut("/{id:guid}/stock", async (Guid id, UpdateStockRequest request, IProductService productService, CancellationToken ct) =>
{
    try
    {
        var updated = await productService.UpdateStockAsync(id, request, ct);
        return updated is not null ? Results.Ok(updated) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateProductStock");

productsGroup.MapDelete("/{id:guid}", async (Guid id, IProductService productService, CancellationToken ct) =>
{
    var deleted = await productService.DeleteAsync(id, ct);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteProduct");

productsGroup.MapGet("/{id:guid}/history", async (Guid id, IProductService productService, CancellationToken ct) =>
{
    var history = await productService.GetPriceHistoryAsync(id, ct);
    return Results.Ok(history);
})
.WithName("GetProductPriceHistory");

app.MapDefaultEndpoints();

app.Run();