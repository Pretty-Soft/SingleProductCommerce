using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCart.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProductPromotionsAndContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlashSaleDiscountType",
                table: "store_products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "percentage");

            migrationBuilder.AddColumn<decimal>(
                name: "FlashSaleDiscountValue",
                table: "store_products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "FlashSaleEnabled",
                table: "store_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FlashSaleEndsAtUtc",
                table: "store_products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlashSaleLabel",
                table: "store_products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FlashSaleStartsAtUtc",
                table: "store_products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SocialSharingEnabled",
                table: "store_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                table: "store_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppPhoneNumber",
                table: "store_products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppPrefillMessage",
                table: "store_products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "ck_store_products_flash_sale_value_non_negative",
                table: "store_products",
                sql: "\"FlashSaleDiscountValue\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_store_products_flash_sale_window",
                table: "store_products",
                sql: "\"FlashSaleEnabled\" = FALSE OR (\"FlashSaleStartsAtUtc\" IS NOT NULL AND \"FlashSaleEndsAtUtc\" IS NOT NULL AND \"FlashSaleEndsAtUtc\" > \"FlashSaleStartsAtUtc\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_store_products_flash_sale_value_non_negative",
                table: "store_products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_store_products_flash_sale_window",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "FlashSaleDiscountType",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "FlashSaleDiscountValue",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "FlashSaleEnabled",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "FlashSaleEndsAtUtc",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "FlashSaleLabel",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "FlashSaleStartsAtUtc",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "SocialSharingEnabled",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "WhatsAppPhoneNumber",
                table: "store_products");

            migrationBuilder.DropColumn(
                name: "WhatsAppPrefillMessage",
                table: "store_products");
        }
    }
}
