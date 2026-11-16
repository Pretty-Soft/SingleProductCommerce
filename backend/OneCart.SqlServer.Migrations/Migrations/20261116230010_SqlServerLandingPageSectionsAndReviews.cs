using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.SqlServer.Migrations.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261116230010_SqlServerLandingPageSectionsAndReviews")]
public sealed class SqlServerLandingPageSectionsAndReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "StorefrontBenefitsJson", table: "store_products", type: "nvarchar(max)", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>(name: "StorefrontUsageStepsJson", table: "store_products", type: "nvarchar(max)", nullable: false, defaultValue: "[]");
        migrationBuilder.CreateTable(
            name: "product_reviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Rating = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                IsPublished = table.Column<bool>(type: "bit", nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_reviews", item => item.Id);
                table.CheckConstraint("ck_product_reviews_rating", "[Rating] BETWEEN 1 AND 5");
                table.CheckConstraint("ck_product_reviews_display_order_non_negative", "[DisplayOrder] >= 0");
                table.ForeignKey("FK_product_reviews_store_products_ProductId", item => item.ProductId, "store_products", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_product_reviews_ProductId_IsPublished_DisplayOrder", "product_reviews", new[] { "ProductId", "IsPublished", "DisplayOrder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("product_reviews");
        migrationBuilder.DropColumn(name: "StorefrontBenefitsJson", table: "store_products");
        migrationBuilder.DropColumn(name: "StorefrontUsageStepsJson", table: "store_products");
    }
}
