using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using OneCart.Api.Admin;
using OneCart.Api.Catalog;

namespace OneCart.Api.Data;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options)
    : IdentityDbContext<AdminUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<StoreProduct> Products => Set<StoreProduct>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<ProductAttributeGroup> ProductAttributeGroups => Set<ProductAttributeGroup>();

    public DbSet<ProductAttributeOption> ProductAttributeOptions => Set<ProductAttributeOption>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductTrustItem> ProductTrustItems => Set<ProductTrustItem>();

    public DbSet<AdminAuditEvent> AdminAuditEvents => Set<AdminAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isSqlServer = Database.IsSqlServer();

        var products = modelBuilder.Entity<StoreProduct>();
        products.ToTable("store_products", table =>
        {
            table.HasCheckConstraint(
                "ck_store_products_name_not_empty",
                isSqlServer ? "LEN([Name]) > 0" : "length(\"Name\") > 0");
            table.HasCheckConstraint(
                "ck_store_products_flash_sale_value_non_negative",
                isSqlServer ? "[FlashSaleDiscountValue] >= 0" : "\"FlashSaleDiscountValue\" >= 0");
            table.HasCheckConstraint(
                "ck_store_products_flash_sale_window",
                isSqlServer
                    ? "[FlashSaleEnabled] = 0 OR ([FlashSaleStartsAtUtc] IS NOT NULL AND [FlashSaleEndsAtUtc] IS NOT NULL AND [FlashSaleEndsAtUtc] > [FlashSaleStartsAtUtc])"
                    : "\"FlashSaleEnabled\" = FALSE OR (\"FlashSaleStartsAtUtc\" IS NOT NULL AND \"FlashSaleEndsAtUtc\" IS NOT NULL AND \"FlashSaleEndsAtUtc\" > \"FlashSaleStartsAtUtc\")");
        });
        products.HasIndex(product => product.IsPublished)
            .IsUnique()
            .HasFilter(isSqlServer ? "[IsPublished] = 1" : "\"IsPublished\" = TRUE");
        products.HasKey(product => product.Id);
        products.Property(product => product.Name).HasMaxLength(200).IsRequired();
        products.Property(product => product.Slug).HasMaxLength(180).IsRequired();
        products.HasIndex(product => product.Slug).IsUnique();
        products.Property(product => product.Description).HasMaxLength(8000).IsRequired();
        products.Property(product => product.BrandName).HasMaxLength(120).HasDefaultValue("OneCart").IsRequired();
        products.Property(product => product.StorefrontHeadline).HasMaxLength(200).IsRequired();
        products.Property(product => product.StorefrontSubtitle).HasMaxLength(500).IsRequired();
        products.Property(product => product.FlashSaleLabel).HasMaxLength(100).IsRequired();
        products.Property(product => product.FlashSaleDiscountType).HasMaxLength(20).HasDefaultValue("percentage").IsRequired();
        products.Property(product => product.FlashSaleDiscountValue).HasPrecision(18, 4);
        products.Property(product => product.WhatsAppPhoneNumber).HasMaxLength(20).IsRequired();
        products.Property(product => product.WhatsAppPrefillMessage).HasMaxLength(500).IsRequired();
        products.Property(product => product.CreatedAtUtc).IsRequired();
        products.Property(product => product.UpdatedAtUtc).IsRequired();
        products.HasMany(product => product.Variants)
            .WithOne(variant => variant.Product)
            .HasForeignKey(variant => variant.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        products.HasMany(product => product.AttributeGroups)
            .WithOne(group => group.Product)
            .HasForeignKey(group => group.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        products.HasMany(product => product.Images)
            .WithOne(image => image.Product)
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        products.HasMany(product => product.TrustItems)
            .WithOne(item => item.Product)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        var variants = modelBuilder.Entity<ProductVariant>();
        variants.ToTable("product_variants", table =>
        {
            table.HasCheckConstraint("ck_product_variants_price_non_negative", isSqlServer ? "[Price] >= 0" : "\"Price\" >= 0");
            table.HasCheckConstraint("ck_product_variants_stock_non_negative", isSqlServer ? "[StockQuantity] >= 0" : "\"StockQuantity\" >= 0");
        });
        variants.HasKey(variant => variant.Id);
        variants.Property(variant => variant.Sku).HasMaxLength(64).IsRequired();
        variants.HasIndex(variant => variant.Sku).IsUnique();
        variants.Property(variant => variant.DisplayName).HasMaxLength(200).IsRequired();
        variants.Property(variant => variant.Price).HasPrecision(18, 4);
        variants.Property(variant => variant.CurrencyCode).HasMaxLength(3).IsRequired();
        variants.Property(variant => variant.StockQuantity).IsRequired();

        var attributeGroups = modelBuilder.Entity<ProductAttributeGroup>();
        attributeGroups.ToTable("product_attribute_groups", table =>
        {
            table.HasCheckConstraint("ck_product_attribute_groups_order_non_negative", isSqlServer ? "[DisplayOrder] >= 0" : "\"DisplayOrder\" >= 0");
        });
        attributeGroups.HasKey(group => group.Id);
        attributeGroups.Property(group => group.Name).HasMaxLength(100).IsRequired();
        attributeGroups.Property(group => group.DisplayOrder).IsRequired();
        attributeGroups.HasMany(group => group.Options)
            .WithOne(option => option.AttributeGroup)
            .HasForeignKey(option => option.AttributeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        var attributeOptions = modelBuilder.Entity<ProductAttributeOption>();
        attributeOptions.ToTable("product_attribute_options", table =>
        {
            table.HasCheckConstraint("ck_product_attribute_options_adjustment_non_negative", isSqlServer ? "[PriceAdjustment] >= 0" : "\"PriceAdjustment\" >= 0");
            table.HasCheckConstraint("ck_product_attribute_options_order_non_negative", isSqlServer ? "[DisplayOrder] >= 0" : "\"DisplayOrder\" >= 0");
        });
        attributeOptions.HasKey(option => option.Id);
        attributeOptions.Property(option => option.Name).HasMaxLength(150).IsRequired();
        attributeOptions.Property(option => option.PriceAdjustment).HasPrecision(18, 4);
        attributeOptions.Property(option => option.DisplayOrder).IsRequired();

        var productImages = modelBuilder.Entity<ProductImage>();
        productImages.ToTable("product_images", table =>
        {
            table.HasCheckConstraint("ck_product_images_display_order_non_negative", isSqlServer ? "[DisplayOrder] >= 0" : "\"DisplayOrder\" >= 0");
        });
        productImages.HasKey(image => image.Id);
        productImages.HasIndex(image => image.StorageKey).IsUnique();
        productImages.HasIndex(image => image.ProductId)
            .IsUnique()
            .HasFilter(isSqlServer ? "[MediaKind] = N'logo'" : "\"MediaKind\" = 'logo'");
        productImages.Property(image => image.StorageKey).HasMaxLength(32).IsRequired();
        productImages.Property(image => image.ContentType).HasMaxLength(40).IsRequired();
        productImages.Property(image => image.MediaKind).HasMaxLength(20).IsRequired();
        productImages.Property(image => image.AltText).HasMaxLength(200).IsRequired();
        productImages.Property(image => image.DisplayOrder).IsRequired();
        productImages.Property(image => image.CreatedAtUtc).IsRequired();

        var trustItems = modelBuilder.Entity<ProductTrustItem>();
        trustItems.ToTable("product_trust_items", table =>
        {
            table.HasCheckConstraint("ck_product_trust_items_display_order_non_negative", isSqlServer ? "[DisplayOrder] >= 0" : "\"DisplayOrder\" >= 0");
        });
        trustItems.HasKey(item => item.Id);
        trustItems.Property(item => item.Title).HasMaxLength(80).IsRequired();
        trustItems.Property(item => item.Description).HasMaxLength(300).IsRequired();
        trustItems.Property(item => item.DisplayOrder).IsRequired();

        var auditEvents = modelBuilder.Entity<AdminAuditEvent>();
        auditEvents.ToTable("admin_audit_events");
        auditEvents.HasKey(auditEvent => auditEvent.Id);
        auditEvents.Property(auditEvent => auditEvent.ActorEmail).HasMaxLength(256).IsRequired();
        auditEvents.Property(auditEvent => auditEvent.Action).HasMaxLength(100).IsRequired();
        auditEvents.Property(auditEvent => auditEvent.Target).HasMaxLength(256).IsRequired();
        auditEvents.Property(auditEvent => auditEvent.Details)
            .HasColumnType(isSqlServer ? "nvarchar(max)" : "text")
            .IsRequired();
        auditEvents.Property(auditEvent => auditEvent.CreatedAtUtc).IsRequired();
        auditEvents.HasIndex(auditEvent => auditEvent.CreatedAtUtc);
    }
}
