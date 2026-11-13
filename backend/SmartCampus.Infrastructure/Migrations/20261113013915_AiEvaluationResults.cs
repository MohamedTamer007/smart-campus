using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations;

public partial class AiEvaluationResults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiEvaluationRuns",
            schema: "smart_campus",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Suite = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SuiteVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ActiveUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AiEvaluationRuns", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AiEvaluationVectors",
            schema: "smart_campus",
            columns: table => new
            {
                CacheKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Revision = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Dimension = table.Column<int>(type: "integer", nullable: false),
                VectorJson = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AiEvaluationVectors", x => new { x.CacheKey, x.Revision }));

        migrationBuilder.CreateTable(
            name: "AiEvaluationCases",
            schema: "smart_campus",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RunId = table.Column<Guid>(type: "uuid", nullable: false),
                CaseKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                InputJson = table.Column<string>(type: "text", nullable: false),
                ExpectedJson = table.Column<string>(type: "text", nullable: false),
                State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                ActualJson = table.Column<string>(type: "text", nullable: true),
                ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ElapsedMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EmbeddingCacheKey = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiEvaluationCases", x => x.Id);
                table.ForeignKey(name: "FK_AiEvaluationCases_AiEvaluationRuns_RunId", column: x => x.RunId,
                    principalSchema: "smart_campus", principalTable: "AiEvaluationRuns", principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_AiEvaluationCases_RunId_CaseKey", schema: "smart_campus",
            table: "AiEvaluationCases", columns: new[] { "RunId", "CaseKey" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_AiEvaluationCases_RunId_State", schema: "smart_campus",
            table: "AiEvaluationCases", columns: new[] { "RunId", "State" });
        migrationBuilder.CreateIndex(name: "IX_AiEvaluationRuns_IsActive", schema: "smart_campus",
            table: "AiEvaluationRuns", column: "IsActive", unique: true, filter: "\"IsActive\"");
        migrationBuilder.CreateIndex(name: "IX_AiEvaluationRuns_State_CreatedAt", schema: "smart_campus",
            table: "AiEvaluationRuns", columns: new[] { "State", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AiEvaluationCases", schema: "smart_campus");
        migrationBuilder.DropTable(name: "AiEvaluationVectors", schema: "smart_campus");
        migrationBuilder.DropTable(name: "AiEvaluationRuns", schema: "smart_campus");
    }
}
