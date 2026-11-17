using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SmartCampus.Infrastructure.Migrations;

/// <summary>One text vector per maintenance report, used to recognise repeated problems worded differently.</summary>
[DbContext(typeof(CampusDb))]
[Migration("20261116220000_ReportEmbeddings")]
public sealed class ReportEmbeddings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE vector_schema text;
            BEGIN
              SELECT n.nspname INTO vector_schema FROM pg_extension e JOIN pg_namespace n ON e.extnamespace=n.oid WHERE e.extname='vector';
              IF vector_schema IS NULL THEN RAISE EXCEPTION 'Install pgvector before migrating Smart Campus'; END IF;
              EXECUTE format('CREATE TABLE smart_campus."ReportEmbeddings" (
                "ReportId" uuid NOT NULL PRIMARY KEY REFERENCES smart_campus."Reports"("Id") ON DELETE CASCADE,
                "ContentVersion" integer NOT NULL, "Revision" text NOT NULL,
                "Vector" %I.vector NOT NULL)', vector_schema);
              IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'smart_campus_runtime') THEN
                GRANT SELECT, INSERT, UPDATE ON smart_campus."ReportEmbeddings" TO smart_campus_runtime;
              END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE smart_campus.\"ReportEmbeddings\"");
}
