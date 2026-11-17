using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.Api.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261117160000_DefaultStoreLanguage")]
public sealed class DefaultStoreLanguage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "DefaultLanguageCode", table: "admin_system_settings", type: "character varying(2)",
        maxLength: 2, nullable: false, defaultValue: "en");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "DefaultLanguageCode", table: "admin_system_settings");
}
