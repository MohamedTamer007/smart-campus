using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SmartCampus.Infrastructure.Migrations;

[DbContext(typeof(CampusDb))]
[Migration("20261112110000_LocationMapPositions")]
public sealed class LocationMapPositions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(name: "MapX", schema: "smart_campus", table: "Locations", type: "double precision", nullable: true);
        migrationBuilder.AddColumn<double>(name: "MapY", schema: "smart_campus", table: "Locations", type: "double precision", nullable: true);
        migrationBuilder.AddColumn<int>(name: "PositionVersion", schema: "smart_campus", table: "Locations", type: "integer", nullable: false, defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MapX", schema: "smart_campus", table: "Locations");
        migrationBuilder.DropColumn(name: "MapY", schema: "smart_campus", table: "Locations");
        migrationBuilder.DropColumn(name: "PositionVersion", schema: "smart_campus", table: "Locations");
    }
}
