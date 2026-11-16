using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.SqlServer.Migrations.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261116234010_SqlServerProductHeadingSize")]
public sealed class SqlServerProductHeadingSize : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE [store_products] SET [ThemeProductTitleFontSizePx] = [ThemeProductTitleFontSizePx] * 2 WHERE [ThemeProductTitleFontSizePx] BETWEEN 14 AND 16");
        migrationBuilder.AlterColumn<int>(name: "ThemeProductTitleFontSizePx", table: "store_products", type: "int", nullable: false, defaultValue: 30, oldClrType: typeof(int), oldType: "int", oldDefaultValue: 15);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(name: "ThemeProductTitleFontSizePx", table: "store_products", type: "int", nullable: false, defaultValue: 15, oldClrType: typeof(int), oldType: "int", oldDefaultValue: 30);
        migrationBuilder.Sql("UPDATE [store_products] SET [ThemeProductTitleFontSizePx] = [ThemeProductTitleFontSizePx] / 2 WHERE [ThemeProductTitleFontSizePx] BETWEEN 28 AND 32");
    }
}
