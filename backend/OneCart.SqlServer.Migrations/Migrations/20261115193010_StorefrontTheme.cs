using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.SqlServer.Migrations.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261115193010_StorefrontTheme")]
public sealed class StorefrontTheme : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ThemePrimaryColor", table: "store_products", type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#242020");
        migrationBuilder.AddColumn<string>(name: "ThemeAccentColor", table: "store_products", type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#c17b88");
        migrationBuilder.AddColumn<string>(name: "ThemeTextColor", table: "store_products", type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#282326");
        migrationBuilder.AddColumn<string>(name: "ThemeMutedTextColor", table: "store_products", type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#777078");
        migrationBuilder.AddColumn<string>(name: "ThemeBackgroundColor", table: "store_products", type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#fbf8f8");
        migrationBuilder.AddColumn<string>(name: "ThemeSurfaceColor", table: "store_products", type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#ffffff");
        migrationBuilder.AddColumn<string>(name: "ThemeBorderColor", table: "store_products", type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#eee5e8");
        migrationBuilder.AddColumn<int>(name: "ThemeProductTitleFontSizePx", table: "store_products", type: "int", nullable: false, defaultValue: 15);
        migrationBuilder.AddColumn<int>(name: "ThemeDetailsFontSizePx", table: "store_products", type: "int", nullable: false, defaultValue: 13);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ThemePrimaryColor", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeAccentColor", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeTextColor", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeMutedTextColor", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeBackgroundColor", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeSurfaceColor", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeBorderColor", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeProductTitleFontSizePx", table: "store_products");
        migrationBuilder.DropColumn(name: "ThemeDetailsFontSizePx", table: "store_products");
    }
}
