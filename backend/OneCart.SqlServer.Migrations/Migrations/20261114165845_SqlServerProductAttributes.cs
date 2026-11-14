using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCart.SqlServer.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SqlServerProductAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_attribute_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    AllowMultiple = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attribute_groups", x => x.Id);
                    table.CheckConstraint("ck_product_attribute_groups_order_non_negative", "[DisplayOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_product_attribute_groups_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_attribute_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttributeGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PriceAdjustment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attribute_options", x => x.Id);
                    table.CheckConstraint("ck_product_attribute_options_adjustment_non_negative", "[PriceAdjustment] >= 0");
                    table.CheckConstraint("ck_product_attribute_options_order_non_negative", "[DisplayOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_product_attribute_options_product_attribute_groups_AttributeGroupId",
                        column: x => x.AttributeGroupId,
                        principalTable: "product_attribute_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_attribute_groups_ProductId",
                table: "product_attribute_groups",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_attribute_options_AttributeGroupId",
                table: "product_attribute_options",
                column: "AttributeGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_attribute_options");

            migrationBuilder.DropTable(
                name: "product_attribute_groups");
        }
    }
}
