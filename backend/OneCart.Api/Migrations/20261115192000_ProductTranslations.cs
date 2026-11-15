using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.Api.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261115192000_ProductTranslations")]
public partial class ProductTranslations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CatalogTranslationsJson", table: "store_products", type: "jsonb", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<string>(name: "StorefrontTranslationsJson", table: "store_products", type: "jsonb", nullable: false, defaultValue: "{}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CatalogTranslationsJson", table: "store_products");
        migrationBuilder.DropColumn(name: "StorefrontTranslationsJson", table: "store_products");
    }
}
