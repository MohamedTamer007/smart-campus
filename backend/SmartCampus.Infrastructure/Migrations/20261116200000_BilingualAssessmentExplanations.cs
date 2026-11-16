using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SmartCampus.Infrastructure.Migrations;

[DbContext(typeof(CampusDb))]
[Migration("20261116200000_BilingualAssessmentExplanations")]
public sealed class BilingualAssessmentExplanations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ExplanationAr", schema: "smart_campus", table: "Assessments",
            type: "character varying(4000)", maxLength: 4000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ExplanationEn", schema: "smart_campus", table: "Assessments",
            type: "character varying(4000)", maxLength: 4000, nullable: true);
        migrationBuilder.Sql("""
            UPDATE smart_campus."Assessments"
            SET "ExplanationEn" = "Explanation"
            WHERE "ExplanationEn" IS NULL AND "Explanation" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExplanationAr", schema: "smart_campus", table: "Assessments");
        migrationBuilder.DropColumn(name: "ExplanationEn", schema: "smart_campus", table: "Assessments");
    }
}
