using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.SqlServer.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261117150010_SqlServerStoreCurrencyLock")]
public sealed class SqlServerStoreCurrencyLock : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrencyCode", table: "admin_system_settings", type: "nvarchar(3)",
            maxLength: 3, nullable: true);
        migrationBuilder.Sql("""
            UPDATE admin_system_settings SET CurrencyCode = (SELECT TOP 1 CurrencyCode FROM store_products)
            WHERE CurrencyCode IS NULL AND EXISTS (SELECT 1 FROM store_products);
            INSERT INTO admin_system_settings (Id, MfaRequired, CurrencyCode)
            SELECT 1, 0, (SELECT TOP 1 CurrencyCode FROM store_products)
            WHERE EXISTS (SELECT 1 FROM store_products)
              AND NOT EXISTS (SELECT 1 FROM admin_system_settings);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "CurrencyCode", table: "admin_system_settings");
}
