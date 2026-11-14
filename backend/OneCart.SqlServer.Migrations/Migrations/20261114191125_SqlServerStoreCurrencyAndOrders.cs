using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCart.SqlServer.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SqlServerStoreCurrencyAndOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "store_products",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE store_products
                SET [CurrencyCode] = COALESCE(
                    (SELECT TOP(1) [CurrencyCode] FROM product_variants WHERE product_variants.[ProductId] = store_products.[Id] ORDER BY [Id]),
                    N'USD');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "store_products",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "store_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_orders", x => x.Id);
                    table.CheckConstraint("ck_store_orders_status", "[Status] IN (N'Pending', N'Processing', N'Completed', N'Cancelled')");
                    table.CheckConstraint("ck_store_orders_total_non_negative", "[Total] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "store_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VariantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SelectedOptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_lines", x => x.Id);
                    table.CheckConstraint("ck_store_order_lines_quantity_positive", "[Quantity] > 0");
                    table.CheckConstraint("ck_store_order_lines_unit_price_non_negative", "[UnitPrice] >= 0");
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
