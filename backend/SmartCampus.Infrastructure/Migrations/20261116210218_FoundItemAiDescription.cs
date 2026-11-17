using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FoundItemAiDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiCategory",
                schema: "smart_campus",
                table: "Items",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiDescription",
                schema: "smart_campus",
                table: "Items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiStatus",
                schema: "smart_campus",
                table: "Items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "none");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiCategory",
                schema: "smart_campus",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "AiDescription",
                schema: "smart_campus",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "AiStatus",
                schema: "smart_campus",
                table: "Items");
        }
    }
}
