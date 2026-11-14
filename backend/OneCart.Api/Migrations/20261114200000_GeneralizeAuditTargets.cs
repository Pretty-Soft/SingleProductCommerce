using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCart.Api.Migrations;

public partial class GeneralizeAuditTargets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Details",
            table: "admin_audit_events",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000);

        migrationBuilder.RenameColumn(
            name: "TargetUserId",
            table: "admin_audit_events",
            newName: "TargetId");

        migrationBuilder.RenameColumn(
            name: "TargetEmail",
            table: "admin_audit_events",
            newName: "Target");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "TargetId",
            table: "admin_audit_events",
            newName: "TargetUserId");

        migrationBuilder.RenameColumn(
            name: "Target",
            table: "admin_audit_events",
            newName: "TargetEmail");

        migrationBuilder.AlterColumn<string>(
            name: "Details",
            table: "admin_audit_events",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");
    }
}
