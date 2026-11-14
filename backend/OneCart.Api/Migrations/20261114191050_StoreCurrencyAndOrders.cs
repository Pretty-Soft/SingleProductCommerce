using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCart.Api.Migrations
{
    /// <inheritdoc />
    public partial class StoreCurrencyAndOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "store_products",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE store_products
                SET "CurrencyCode" = COALESCE(
                    (SELECT "CurrencyCode" FROM product_variants WHERE product_variants."ProductId" = store_products."Id" ORDER BY "Id" LIMIT 1),
                    'USD');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "store_products",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "store_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_orders", x => x.Id);
                    table.CheckConstraint("ck_store_orders_status", "\"Status\" IN ('Pending', 'Processing', 'Completed', 'Cancelled')");
                    table.CheckConstraint("ck_store_orders_total_non_negative", "\"Total\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "store_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VariantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SelectedOptionsJson = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_lines", x => x.Id);
                    table.CheckConstraint("ck_store_order_lines_quantity_positive", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_store_order_lines_unit_price_non_negative", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_store_order_lines_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_order_lines_store_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "store_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_store_order_lines_OrderId",
                table: "store_order_lines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_store_order_lines_VariantId",
                table: "store_order_lines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_CreatedAtUtc",
                table: "store_orders",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_store_orders_OrderNumber",
                table: "store_orders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_order_lines");

            migrationBuilder.DropTable(
                name: "store_orders");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "store_products");
        }
    }
}
