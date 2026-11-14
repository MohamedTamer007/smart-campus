using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiEvaluationRuntimePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'smart_campus_runtime') THEN
                        EXECUTE 'GRANT SELECT, INSERT, UPDATE ON TABLE
                            smart_campus."AiEvaluationRuns",
                            smart_campus."AiEvaluationCases",
                            smart_campus."AiEvaluationVectors"
                            TO smart_campus_runtime';
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'smart_campus_runtime') THEN
                        EXECUTE 'REVOKE SELECT, INSERT, UPDATE ON TABLE
                            smart_campus."AiEvaluationRuns",
                            smart_campus."AiEvaluationCases",
                            smart_campus."AiEvaluationVectors"
                            FROM smart_campus_runtime';
                    END IF;
                END
                $$;
                """);
        }
    }
}
