using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SmartCampus.Infrastructure.Migrations;

[DbContext(typeof(CampusDb))]
[Migration("20261109141000_RetrievalVectors")]
public sealed class RetrievalVectors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE vector_schema text;
            BEGIN
              SELECT n.nspname INTO vector_schema FROM pg_extension e JOIN pg_namespace n ON e.extnamespace=n.oid WHERE e.extname='vector';
              IF vector_schema IS NULL THEN RAISE EXCEPTION 'Install pgvector before migrating Smart Campus'; END IF;
              EXECUTE format('CREATE TABLE smart_campus."Embeddings" (
                "ItemId" uuid NOT NULL REFERENCES smart_campus."Items"("Id") ON DELETE CASCADE,
                "SourceId" uuid NOT NULL, "Kind" text NOT NULL CHECK ("Kind" IN (''description'',''image'')),
                "Model" text NOT NULL, "Revision" text NOT NULL, "Dimension" integer NOT NULL CHECK ("Dimension">0),
                "Vector" %I.vector NOT NULL,
                PRIMARY KEY ("SourceId","Revision"), CHECK (%I.vector_dims("Vector")="Dimension"))', vector_schema, vector_schema);
            END $$;
            CREATE INDEX "IX_Embeddings_Revision" ON smart_campus."Embeddings"("Revision","Dimension");
            REVOKE ALL ON SCHEMA smart_campus FROM PUBLIC;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE smart_campus.\"Embeddings\"");
}
