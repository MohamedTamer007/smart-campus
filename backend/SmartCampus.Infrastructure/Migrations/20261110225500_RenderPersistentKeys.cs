using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SmartCampus.Infrastructure.Migrations;

/// <summary>
/// Durable encrypted Data Protection key ring for stateless Linux/Render hosts.
/// Applied by an explicit migration action, never automatically by the web app.
/// </summary>
[DbContext(typeof(CampusDb))]
[Migration("20261110225500_RenderPersistentKeys")]
public sealed class RenderPersistentKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DataProtectionKeys",
            schema: "smart_campus",
            columns: table => new
            {
                FriendlyName = table.Column<string>(type: "text", nullable: false),
                Xml = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DataProtectionKeys", x => x.FriendlyName));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DataProtectionKeys", schema: "smart_campus");
    }
}
