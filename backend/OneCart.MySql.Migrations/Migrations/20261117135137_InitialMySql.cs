using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OneCart.MySql.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialMySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "admin_audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ActorEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Target = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit_events", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "admin_system_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    MfaRequired = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true),
                    DefaultLanguageCode = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false, defaultValue: "en")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_system_settings", x => x.Id);
                    table.CheckConstraint("ck_admin_system_settings_singleton", "`Id` = 1");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true),
                    SecurityStamp = table.Column<string>(type: "longtext", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true),
                    PhoneNumber = table.Column<string>(type: "longtext", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "store_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrderNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CustomerName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DeliveryAddress = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_orders", x => x.Id);
                    table.CheckConstraint("ck_store_orders_status", "`Status` IN ('Pending', 'Processing', 'Completed', 'Cancelled')");
                    table.CheckConstraint("ck_store_orders_total_non_negative", "`Total` >= 0");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "store_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "longtext", maxLength: 8000, nullable: false),
                    CatalogTranslationsJson = table.Column<string>(type: "longtext", nullable: false),
                    StorefrontTranslationsJson = table.Column<string>(type: "longtext", nullable: false),
                    BrandName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, defaultValue: "OneCart"),
                    StorefrontHeadline = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    AboutUs = table.Column<string>(type: "longtext", maxLength: 5000, nullable: false),
                    StorefrontBenefitsJson = table.Column<string>(type: "longtext", nullable: false),
                    StorefrontUsageStepsJson = table.Column<string>(type: "longtext", nullable: false),
                    StorefrontSubtitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ThemePrimaryColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false, defaultValue: "#242020"),
                    ThemeAccentColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false, defaultValue: "#c17b88"),
                    ThemeTextColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false, defaultValue: "#282326"),
                    ThemeMutedTextColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false, defaultValue: "#777078"),
                    ThemeBackgroundColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false, defaultValue: "#fbf8f8"),
                    ThemeSurfaceColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false, defaultValue: "#ffffff"),
                    ThemeBorderColor = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false, defaultValue: "#eee5e8"),
                    ThemeProductTitleFontSizePx = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    ThemeDetailsFontSizePx = table.Column<int>(type: "int", nullable: false, defaultValue: 13),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    FlashSaleEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FlashSaleLabel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FlashSaleDiscountType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "percentage"),
                    FlashSaleDiscountValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    FlashSaleStartsAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    FlashSaleEndsAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    SocialSharingEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WhatsAppEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WhatsAppPhoneNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    WhatsAppPrefillMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    PublishedProductKey = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `IsPublished` = 1 THEN 1 ELSE NULL END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_products", x => x.Id);
                    table.CheckConstraint("ck_store_products_flash_sale_value_non_negative", "`FlashSaleDiscountValue` >= 0");
                    table.CheckConstraint("ck_store_products_flash_sale_window", "`FlashSaleEnabled` = FALSE OR (`FlashSaleStartsAtUtc` IS NOT NULL AND `FlashSaleEndsAtUtc` IS NOT NULL AND `FlashSaleEndsAtUtc` > `FlashSaleStartsAtUtc`)");
                    table.CheckConstraint("ck_store_products_name_not_empty", "length(`Name`) > 0");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false),
                    ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false),
                    Value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_attribute_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    IsRequired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowMultiple = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attribute_groups", x => x.Id);
                    table.CheckConstraint("ck_product_attribute_groups_order_non_negative", "`DisplayOrder` >= 0");
                    table.ForeignKey(
                        name: "FK_product_attribute_groups_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    MediaKind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AltText = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    LogoKey = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `MediaKind` = 'logo' THEN 1 ELSE NULL END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.Id);
                    table.CheckConstraint("ck_product_images_display_order_non_negative", "`DisplayOrder` >= 0");
                    table.ForeignKey(
                        name: "FK_product_images_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_reviews", x => x.Id);
                    table.CheckConstraint("ck_product_reviews_display_order_non_negative", "`DisplayOrder` >= 0");
                    table.CheckConstraint("ck_product_reviews_rating", "`Rating` BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_product_reviews_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_trust_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_trust_items", x => x.Id);
                    table.CheckConstraint("ck_product_trust_items_display_order_non_negative", "`DisplayOrder` >= 0");
                    table.ForeignKey(
                        name: "FK_product_trust_items_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Sku = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.Id);
                    table.CheckConstraint("ck_product_variants_price_non_negative", "`Price` >= 0");
                    table.CheckConstraint("ck_product_variants_stock_non_negative", "`StockQuantity` >= 0");
                    table.ForeignKey(
                        name: "FK_product_variants_store_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "store_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_attribute_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    AttributeGroupId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    PriceAdjustment = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attribute_options", x => x.Id);
                    table.CheckConstraint("ck_product_attribute_options_adjustment_non_negative", "`PriceAdjustment` >= 0");
                    table.CheckConstraint("ck_product_attribute_options_order_non_negative", "`DisplayOrder` >= 0");
                    table.ForeignKey(
                        name: "FK_product_attribute_options_product_attribute_groups_Attribute~",
                        column: x => x.AttributeGroupId,
                        principalTable: "product_attribute_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "store_order_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false),
                    VariantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    VariantName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SelectedOptionsJson = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_order_lines", x => x.Id);
                    table.CheckConstraint("ck_store_order_lines_quantity_positive", "`Quantity` > 0");
                    table.CheckConstraint("ck_store_order_lines_unit_price_non_negative", "`UnitPrice` >= 0");
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_events_CreatedAtUtc",
                table: "admin_audit_events",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_attribute_groups_ProductId",
                table: "product_attribute_groups",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_attribute_options_AttributeGroupId",
                table: "product_attribute_options",
                column: "AttributeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_product_images_ProductId_LogoKey",
                table: "product_images",
                columns: new[] { "ProductId", "LogoKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_images_StorageKey",
                table: "product_images",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_ProductId_IsPublished_DisplayOrder",
                table: "product_reviews",
                columns: new[] { "ProductId", "IsPublished", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_product_trust_items_ProductId",
                table: "product_trust_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId",
                table: "product_variants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_Sku",
                table: "product_variants",
                column: "Sku",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_store_products_PublishedProductKey",
                table: "store_products",
                column: "PublishedProductKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_products_Slug",
                table: "store_products",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_audit_events");

            migrationBuilder.DropTable(
                name: "admin_system_settings");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "product_attribute_options");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "product_reviews");

            migrationBuilder.DropTable(
                name: "product_trust_items");

            migrationBuilder.DropTable(
                name: "store_order_lines");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "product_attribute_groups");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "store_orders");

            migrationBuilder.DropTable(
                name: "store_products");
        }
    }
}
