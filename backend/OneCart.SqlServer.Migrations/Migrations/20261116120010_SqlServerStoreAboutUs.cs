using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.SqlServer.Migrations.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261116120010_SqlServerStoreAboutUs")]
public sealed class SqlServerStoreAboutUs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "AboutUs", table: "store_products", type: "nvarchar(max)", maxLength: 5000, nullable: false, defaultValue: "");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "AboutUs", table: "store_products");
}
