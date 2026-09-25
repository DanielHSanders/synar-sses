-- Migration: sses_009_backfill_operator_sample_sizes
-- Date: 2026-09-25
-- Purpose: Record the effective and target sample sizes that were typed into
-- SSES for the FFY 2026 submission.
--
-- SSES does not compute these two when it produces the report; it asks the
-- operator for them and prints them on Table 1 under "Sample Size for Current
-- Year". Table 1 of Synar2025_SSES_Final.xlsx shows 303 for both. The 2026
-- row already carries 256/256 from the May sample-size run.

BEGIN;

UPDATE synar_sample
   SET effective_sample_size = 303,
       target_sample_size    = 303
 WHERE check_year = 2025 AND state_code = 'KY'
   AND effective_sample_size IS NULL;

SELECT check_year, frame_size, effective_sample_size, target_sample_size,
       original_sample_size
  FROM synar_sample ORDER BY check_year;

COMMIT;
