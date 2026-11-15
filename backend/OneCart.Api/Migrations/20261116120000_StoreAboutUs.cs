using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.Api.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261116120000_StoreAboutUs")]
public sealed class StoreAboutUs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "AboutUs", table: "store_products", type: "character varying(5000)", maxLength: 5000, nullable: false, defaultValue: "");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "AboutUs", table: "store_products");
}
