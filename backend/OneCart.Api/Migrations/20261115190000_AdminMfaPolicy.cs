using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OneCart.Api.Data;

#nullable disable

namespace OneCart.Api.Migrations;

[DbContext(typeof(StoreDbContext))]
[Migration("20261115190000_AdminMfaPolicy")]
public partial class AdminMfaPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name: "admin_system_settings",
        columns: table => new
        {
            Id = table.Column<int>(type: "integer", nullable: false),
            MfaRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_admin_system_settings", item => item.Id);
            table.CheckConstraint("ck_admin_system_settings_singleton", "\"Id\" = 1");
        });

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("admin_system_settings");
}
