using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.Api.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261116234000_ProductHeadingSize")]
public sealed class ProductHeadingSize : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE store_products SET \"ThemeProductTitleFontSizePx\" = \"ThemeProductTitleFontSizePx\" * 2 WHERE \"ThemeProductTitleFontSizePx\" BETWEEN 14 AND 16");
        migrationBuilder.AlterColumn<int>(name: "ThemeProductTitleFontSizePx", table: "store_products", type: "integer", nullable: false, defaultValue: 30, oldClrType: typeof(int), oldType: "integer", oldDefaultValue: 15);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(name: "ThemeProductTitleFontSizePx", table: "store_products", type: "integer", nullable: false, defaultValue: 15, oldClrType: typeof(int), oldType: "integer", oldDefaultValue: 30);
        migrationBuilder.Sql("UPDATE store_products SET \"ThemeProductTitleFontSizePx\" = \"ThemeProductTitleFontSizePx\" / 2 WHERE \"ThemeProductTitleFontSizePx\" BETWEEN 28 AND 32");
    }
}
