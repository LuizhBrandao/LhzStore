using MarketplaceApi.Domain.Entities;
using MarketplaceApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace MarketplaceApi.Infrastructure.Persistence;

public class MarketplaceDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPriceHistory> ProductPriceHistories => Set<ProductPriceHistory>();

    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("Products");

            builder.HasKey(p => p.Id);

            // Mapeamento do Value Object Sku usando conversor de valor do EF Core
            builder.Property(p => p.Sku)
                   .HasConversion(sku => sku.Value, str => Sku.FromString(str))
                   .HasMaxLength(150)
                   .IsRequired();

            builder.HasIndex(p => p.Sku).IsUnique();

            builder.Property(p => p.ExternalCardId)
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(p => p.Name)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(p => p.Collection)
                   .HasMaxLength(150)
                   .IsRequired();

            builder.Property(p => p.ImageUrl)
                   .HasMaxLength(500);

            builder.Property(p => p.Condition)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(p => p.Language)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(p => p.StockQuantity)
                   .IsRequired();

            builder.Property(p => p.AveragePurchasePrice)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(p => p.LigaPokemonPrice)
                   .HasPrecision(18, 2)
                   .IsRequired();

            // Ignorar propriedades computadas de domínio
            builder.Ignore(p => p.BaseCost);
            builder.Ignore(p => p.CompetitivePrice);
            builder.Ignore(p => p.FinalSalePrice);
        });

        modelBuilder.Entity<ProductPriceHistory>(builder =>
        {
            builder.ToTable("ProductPriceHistory");

            builder.HasKey(h => h.Id);

            builder.Property(h => h.ProductId)
                   .IsRequired();

            builder.Property(h => h.AveragePurchasePrice)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(h => h.LigaPokemonPrice)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(h => h.ChangeDate)
                   .IsRequired();

            builder.HasIndex(h => new { h.ProductId, h.ChangeDate });

            builder.HasOne<Product>()
                   .WithMany()
                   .HasForeignKey(h => h.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
