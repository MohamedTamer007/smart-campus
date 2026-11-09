using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewedAssessmentInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                schema: "smart_campus",
                table: "Assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                schema: "smart_campus",
                table: "Assessments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedImageId",
                schema: "smart_campus",
                table: "Assessments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_ReviewedBy",
                schema: "smart_campus",
                table: "Assessments",
                column: "ReviewedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_AspNetUsers_ReviewedBy",
                schema: "smart_campus",
                table: "Assessments",
                column: "ReviewedBy",
                principalSchema: "smart_campus",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_AspNetUsers_ReviewedBy",
                schema: "smart_campus",
                table: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_ReviewedBy",
                schema: "smart_campus",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                schema: "smart_campus",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                schema: "smart_campus",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ReviewedImageId",
                schema: "smart_campus",
                table: "Assessments");
        }
    }
}
