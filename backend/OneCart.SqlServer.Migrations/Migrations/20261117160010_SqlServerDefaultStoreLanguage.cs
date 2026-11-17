using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.SqlServer.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261117160010_SqlServerDefaultStoreLanguage")]
public sealed class SqlServerDefaultStoreLanguage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "DefaultLanguageCode", table: "admin_system_settings", type: "nvarchar(2)",
        maxLength: 2, nullable: false, defaultValue: "en");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "DefaultLanguageCode", table: "admin_system_settings");
}
