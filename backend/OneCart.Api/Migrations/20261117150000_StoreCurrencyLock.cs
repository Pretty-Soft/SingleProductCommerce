using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.Api.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261117150000_StoreCurrencyLock")]
public sealed class StoreCurrencyLock : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrencyCode", table: "admin_system_settings", type: "character varying(3)",
            maxLength: 3, nullable: true);
        migrationBuilder.Sql("""
            UPDATE admin_system_settings AS setting
            SET "CurrencyCode" = product."CurrencyCode"
            FROM (SELECT "CurrencyCode" FROM store_products LIMIT 1) AS product
            WHERE setting."CurrencyCode" IS NULL;
            INSERT INTO admin_system_settings ("Id", "MfaRequired", "CurrencyCode")
            SELECT 1, FALSE, product."CurrencyCode"
            FROM (SELECT "CurrencyCode" FROM store_products LIMIT 1) AS product
            WHERE NOT EXISTS (SELECT 1 FROM admin_system_settings);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "CurrencyCode", table: "admin_system_settings");
}
