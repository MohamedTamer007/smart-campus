using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailProviderEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                schema: "smart_campus",
                table: "OutboundEmails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                schema: "smart_campus",
                table: "OutboundEmails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureCode",
                schema: "smart_campus",
                table: "OutboundEmails");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                schema: "smart_campus",
                table: "OutboundEmails");
        }
    }
}
