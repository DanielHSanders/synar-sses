-- Migration: sses_007_2026_ocr_corrections
-- Applied to production: 2026-09-23 (recorded here after the fact).
-- Purpose: Inspector-identity corrections to the 2026 synarcheck rows, from
-- review of the OCR extraction of the scanned inspection forms.
--
--   * Misread IA codes merged into the inspector they belong to, where the
--     age, gender and counties worked all agreed with the target code.
--   * "43" was an investigator number written in the IA box; reassigned to
--     030-38 (same date, county and investigator pair as the adjacent form).
--   * 063-02 appeared at two ages; the older age is used throughout.
--
-- SSES rejects an input file in which one inspector has two ages or two
-- genders, so these had to be resolved before the FFY 2027 run.

BEGIN;

UPDATE synarcheck SET ia = '010-1015'
 WHERE checkyear = 2026 AND synarfullid = 'SY26-3495';
UPDATE synarcheck SET ia = '010-1015'
 WHERE checkyear = 2026 AND synarfullid = 'SY26-3524';
UPDATE synarcheck SET ia = '010-1015'
 WHERE checkyear = 2026 AND synarfullid = 'SY26-3507';
UPDATE synarcheck SET iaage = 20
 WHERE checkyear = 2026 AND synarfullid = 'SY26-455';
UPDATE synarcheck SET ia = '079-06'
 WHERE checkyear = 2026 AND synarfullid = 'SY26-1193';
UPDATE synarcheck SET ia = '079-06'
 WHERE checkyear = 2026 AND synarfullid = 'SY26-1171';
UPDATE synarcheck SET ia = '056-81'
 WHERE checkyear = 2026 AND synarfullid = 'SY26-8970';
UPDATE synarcheck SET iaage = 20
 WHERE checkyear = 2026 AND synarfullid = 'SY26-5837';
UPDATE synarcheck SET ia = '030-38'
 WHERE checkyear = 2026 AND synarfullid = 'SY26-5816';

-- Verify before committing:
SELECT status, count(*) FROM synarcheck WHERE checkyear=2026 GROUP BY 1;
SELECT count(*) FILTER (WHERE soldtobacco::int=1) AS sales,
       count(*) FILTER (WHERE ineligible::int=1) AS ineligible,
       count(DISTINCT ia) AS inspectors
  FROM synarcheck WHERE checkyear=2026;

COMMIT;
