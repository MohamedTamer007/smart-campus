using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InferenceBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InferenceUsage",
                schema: "smart_campus",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Window = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InferenceUsage", x => x.Key);
                    table.CheckConstraint("inference_count_positive", "\"Count\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InferenceUsage_Provider_WindowStart",
                schema: "smart_campus",
                table: "InferenceUsage",
                columns: new[] { "Provider", "WindowStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InferenceUsage",
                schema: "smart_campus");
        }
    }
}
