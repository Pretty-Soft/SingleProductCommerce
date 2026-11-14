using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCart.Api.Migrations
{
    /// <inheritdoc />
    public partial class StorefrontBrandAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandName",
                table: "store_products",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "OneCart");

            migrationBuilder.AddColumn<string>(
                name: "StorefrontHeadline",
                table: "store_products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StorefrontSubtitle",
                table: "store_products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MediaKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AltText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.Id);
                    table.CheckConstraint("ck_product_images_display_order_non_negative", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_product_images_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_trust_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_trust_items", x => x.Id);
                    table.CheckConstraint("ck_product_trust_items_display_order_non_negative", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_product_trust_items_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_images_ProductId",
                table: "product_images",
                column: "ProductId",
                unique: true,
                filter: "\"MediaKind\" = 'logo'");

            migrationBuilder.CreateIndex(
                name: "IX_product_images_StorageKey",
                table: "product_images",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_trust_items_ProductId",
                table: "product_trust_items",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "product_trust_items");

            migrationBuilder.DropColumn(
                name: "BrandName",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "StorefrontHeadline",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "StorefrontSubtitle",
                table: "store_products");
        }
    }
}
