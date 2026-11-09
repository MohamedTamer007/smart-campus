-- Explicit operator action only, after backup/review/approval. Never applied at startup.
-- Preserve legacy access outside the application's private object prefix.
BEGIN;
SELECT pg_advisory_xact_lock(762342102);
DO $review$
DECLARE
    current_qual text;
    expected_qual text := $policy$(bucket_id = ANY (ARRAY['maintenance-images'::text, 'lost-found-images'::text]))$policy$;
BEGIN
    IF (SELECT count(*) FROM pg_policies WHERE schemaname='storage' AND tablename='objects' AND cmd IN ('SELECT','ALL')) <> 1 THEN
        RAISE EXCEPTION 'Additional storage read policies require review before applying this change';
    END IF;
    IF (SELECT count(*) FROM storage.buckets WHERE id IN ('maintenance-images','lost-found-images') AND public=false) <> 2 THEN
        RAISE EXCEPTION 'Both application buckets must already exist and be private';
    END IF;
    SELECT qual INTO current_qual FROM pg_policies
    WHERE schemaname = 'storage' AND tablename = 'objects' AND policyname = 'campus_images_read'
      AND cmd = 'SELECT' AND roles = ARRAY['authenticated']::name[];
    IF current_qual IS DISTINCT FROM expected_qual THEN
        RAISE EXCEPTION 'Storage policy differs from reviewed baseline; stop and review current permissions';
    END IF;
END
$review$;
ALTER POLICY campus_images_read ON storage.objects
USING (bucket_id = ANY (ARRAY['maintenance-images'::text, 'lost-found-images'::text])
       AND split_part(name, '/', 1) <> 'smart-campus');
COMMIT;
