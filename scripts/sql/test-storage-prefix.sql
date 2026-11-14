-- Isolated CI database only; this file creates synthetic storage/role fixtures.
\set ON_ERROR_STOP on
SELECT current_database() = 'smart_campus_test' AS isolated_target \gset
\if :isolated_target
\else
  \quit 1
\endif
CREATE ROLE authenticated NOLOGIN;
CREATE SCHEMA storage;
CREATE TABLE storage.buckets (id text PRIMARY KEY, public boolean NOT NULL);
INSERT INTO storage.buckets VALUES ('maintenance-images',false),('lost-found-images',false);
CREATE TABLE storage.objects (bucket_id text NOT NULL, name text NOT NULL);
ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;
GRANT USAGE ON SCHEMA storage TO authenticated;
GRANT SELECT ON storage.objects TO authenticated;
CREATE POLICY campus_images_read ON storage.objects FOR SELECT TO authenticated
USING (bucket_id = ANY (ARRAY['maintenance-images'::text, 'lost-found-images'::text]));
INSERT INTO storage.objects VALUES
('maintenance-images','legacy/test-only.jpg'),
('maintenance-images','smart-campus/test-only.jpg'),
('lost-found-images','legacy/test-only.jpg'),
('lost-found-images','smart-campus/test-only.jpg');
SET ROLE authenticated;
SELECT count(*) = 4 AS baseline_access FROM storage.objects \gset
\if :baseline_access
\else
  \quit 1
\endif
RESET ROLE;
\ir restrict-storage-prefix.sql
SET ROLE authenticated;
SELECT count(*) = 2 AND bool_and(name LIKE 'legacy/%') AS legacy_only FROM storage.objects \gset
\if :legacy_only
\else
  \quit 1
\endif
RESET ROLE;
SELECT count(*) = 4 AS owner_access FROM storage.objects \gset
\if :owner_access
\else
  \quit 1
\endif
