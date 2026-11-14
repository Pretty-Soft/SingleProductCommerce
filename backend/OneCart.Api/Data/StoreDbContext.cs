using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;

namespace OneCart.Api.Data;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<StoreProduct> Products => Set<StoreProduct>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var products = modelBuilder.Entity<StoreProduct>();
        products.ToTable("store_products", table =>
        {
            table.HasCheckConstraint("ck_store_products_name_not_empty", "length(\"Name\") > 0");
        });
        products.HasIndex(product => product.IsPublished)
            .IsUnique()
            .HasFilter("\"IsPublished\" = TRUE");
        products.HasKey(product => product.Id);
        products.Property(product => product.Name).HasMaxLength(200).IsRequired();
        products.Property(product => product.Slug).HasMaxLength(180).IsRequired();
        products.HasIndex(product => product.Slug).IsUnique();
        products.Property(product => product.Description).HasMaxLength(8000).IsRequired();
        products.Property(product => product.CreatedAtUtc).IsRequired();
        products.Property(product => product.UpdatedAtUtc).IsRequired();
        products.HasMany(product => product.Variants)
            .WithOne(variant => variant.Product)
            .HasForeignKey(variant => variant.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        var variants = modelBuilder.Entity<ProductVariant>();
        variants.ToTable("product_variants", table =>
        {
            table.HasCheckConstraint("ck_product_variants_price_non_negative", "\"Price\" >= 0");
            table.HasCheckConstraint("ck_product_variants_stock_non_negative", "\"StockQuantity\" >= 0");
        });
        variants.HasKey(variant => variant.Id);
        variants.Property(variant => variant.Sku).HasMaxLength(64).IsRequired();
        variants.HasIndex(variant => variant.Sku).IsUnique();
        variants.Property(variant => variant.DisplayName).HasMaxLength(200).IsRequired();
        variants.Property(variant => variant.Price).HasPrecision(18, 4);
        variants.Property(variant => variant.CurrencyCode).HasMaxLength(3).IsRequired();
        variants.Property(variant => variant.StockQuantity).IsRequired();
    }
}
