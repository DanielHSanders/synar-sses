-- Migration: sses_006_backfill_samples
-- Date: 2026-09-23
-- Purpose: Backfill synar_sample for the years already submitted, so the
-- report page can look up the correct frame instead of guessing.
--
--   2025 -- frame 4452, taken from Table 2 of Synar2025_SSES_Final.xlsx.
--           The calculator inputs used that year are not recorded anywhere,
--           so only the frame is backfilled.
--   2026 -- frame 4307, from the SSES sample-size run of 2026-05-12
--           (SynarSSES_Output_Spring_2026.xlsx), which is also the frame used
--           to generate the FFY 2027 submission.
--
-- The selected outlets are recovered from synarcheck, which holds one row per
-- drawn outlet for each year.

BEGIN;

INSERT INTO synar_sample
    (check_year, state_code, drawn_at, frame_size, frame_filter,
     expected_rvr_pct, design_effect, accuracy_rate_pct, completion_rate_pct,
     safety_margin_pct, one_sided_ci,
     effective_sample_size, target_sample_size, original_sample_size,
     is_final, note)
VALUES
    (2025, 'KY', TIMESTAMPTZ '2025-05-01 00:00:00+00', 4452, NULL,
     NULL, NULL, NULL, NULL, NULL, NULL,
     NULL, NULL, 376,
     true, 'Backfilled. Frame from Table 2 of the FFY 2026 submission; draw date approximate and calculator inputs not recorded.'),

    (2026, 'KY', TIMESTAMPTZ '2026-05-12 10:53:43+00', 4307, 'Valid outlets with 2026 ABC license',
     10.06, 1.0, 92.55, 92.55, 0, true,
     256, 256, 299,
     true, 'Backfilled from the SSES sample-size run of 2026-05-12. Drawn against the Valid + 2026 ABC frame.');

-- Recover the drawn outlets from synarcheck.
INSERT INTO synar_sample_outlet (sample_id, synar_maps_id, check_type)
SELECT s.sample_id, sc.synarmapsid, sc.checktype
  FROM synar_sample s
  JOIN synarcheck sc ON sc.checkyear = s.check_year
 WHERE s.check_year IN (2025, 2026)
   AND sc.synarmapsid IS NOT NULL
ON CONFLICT (sample_id, synar_maps_id) DO NOTHING;

-- Verify: outlet counts should track the sample sizes actually inspected.
SELECT s.check_year, s.frame_size, s.original_sample_size,
       count(o.synar_maps_id) AS outlets_recorded
  FROM synar_sample s
  LEFT JOIN synar_sample_outlet o ON o.sample_id = s.sample_id
 GROUP BY s.check_year, s.frame_size, s.original_sample_size
 ORDER BY s.check_year;

COMMIT;
