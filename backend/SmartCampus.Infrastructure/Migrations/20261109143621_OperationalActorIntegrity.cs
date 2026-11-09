using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OperationalActorIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_History_AssigneeId",
                schema: "smart_campus",
                table: "History",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_NotReceivedBy",
                schema: "smart_campus",
                table: "Handovers",
                column: "NotReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_ReceivedBy",
                schema: "smart_campus",
                table: "Handovers",
                column: "ReceivedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_DeliveredBy",
                schema: "smart_campus",
                table: "Claims",
                column: "DeliveredBy");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ReviewedBy",
                schema: "smart_campus",
                table: "Claims",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_ActorId",
                schema: "smart_campus",
                table: "Audit",
                column: "ActorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Audit_AspNetUsers_ActorId",
                schema: "smart_campus",
                table: "Audit",
                column: "ActorId",
                principalSchema: "smart_campus",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_AspNetUsers_DeliveredBy",
                schema: "smart_campus",
                table: "Claims",
                column: "DeliveredBy",
                principalSchema: "smart_campus",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_AspNetUsers_ReviewedBy",
                schema: "smart_campus",
                table: "Claims",
                column: "ReviewedBy",
                principalSchema: "smart_campus",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Handovers_AspNetUsers_NotReceivedBy",
                schema: "smart_campus",
                table: "Handovers",
                column: "NotReceivedBy",
                principalSchema: "smart_campus",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Handovers_AspNetUsers_ReceivedBy",
                schema: "smart_campus",
                table: "Handovers",
                column: "ReceivedBy",
                principalSchema: "smart_campus",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_History_AspNetUsers_AssigneeId",
                schema: "smart_campus",
                table: "History",
                column: "AssigneeId",
                principalSchema: "smart_campus",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Audit_AspNetUsers_ActorId",
                schema: "smart_campus",
                table: "Audit");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_AspNetUsers_DeliveredBy",
                schema: "smart_campus",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_AspNetUsers_ReviewedBy",
                schema: "smart_campus",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Handovers_AspNetUsers_NotReceivedBy",
                schema: "smart_campus",
                table: "Handovers");

            migrationBuilder.DropForeignKey(
                name: "FK_Handovers_AspNetUsers_ReceivedBy",
                schema: "smart_campus",
                table: "Handovers");

            migrationBuilder.DropForeignKey(
                name: "FK_History_AspNetUsers_AssigneeId",
                schema: "smart_campus",
                table: "History");

            migrationBuilder.DropIndex(
                name: "IX_History_AssigneeId",
                schema: "smart_campus",
                table: "History");

            migrationBuilder.DropIndex(
                name: "IX_Handovers_NotReceivedBy",
                schema: "smart_campus",
                table: "Handovers");

            migrationBuilder.DropIndex(
                name: "IX_Handovers_ReceivedBy",
                schema: "smart_campus",
                table: "Handovers");

            migrationBuilder.DropIndex(
                name: "IX_Claims_DeliveredBy",
                schema: "smart_campus",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_ReviewedBy",
                schema: "smart_campus",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Audit_ActorId",
                schema: "smart_campus",
                table: "Audit");
        }
    }
}
