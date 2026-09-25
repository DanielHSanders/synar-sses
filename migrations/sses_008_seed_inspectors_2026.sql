-- Migration: sses_008_seed_inspectors_2026
-- Applied to production: 2026-09-23 (recorded here after the fact).
-- Purpose: 2026 youth-inspector roster for synar_inspector, derived from the
-- reviewed scans. synarcheck carries age per inspection but not gender, and
-- the SSES report joins the two -- without this roster Table 4 and the
-- Tables 6-8 pivots come out empty.

BEGIN;

INSERT INTO synar_inspector (check_year, ia_code, gender, age) VALUES
  (2026, '003-38', 'F', 19),
  (2026, '008-33', 'F', 17),
  (2026, '010-1015', 'M', 17),
  (2026, '011-1013', 'M', 17),
  (2026, '023-1016', 'M', 16),
  (2026, '024-1011', 'M', 16),
  (2026, '030-1012', 'F', 18),
  (2026, '030-38', 'M', 20),
  (2026, '034-75', 'F', 19),
  (2026, '034-79', 'F', 19),
  (2026, '035-13', 'M', 20),
  (2026, '056-1008', 'F', 16),
  (2026, '056-81', 'F', 18),
  (2026, '058-30', 'M', 19),
  (2026, '059-31', 'F', 18),
  (2026, '063-02', 'M', 20),
  (2026, '071-06', 'M', 19),
  (2026, '073-1016', 'M', 16),
  (2026, '076-1000', 'F', 19),
  (2026, '076-63', 'F', 20),
  (2026, '079-06', 'M', 18),
  (2026, '097-69', 'F', 19),
  (2026, '097-70', 'M', 18),
  (2026, '106-58', 'F', 19),
  (2026, '120-39', 'M', 19)
ON CONFLICT (check_year, ia_code) DO UPDATE
  SET gender = EXCLUDED.gender, age = EXCLUDED.age;

-- Every 2026 inspection should now resolve to a roster entry:
SELECT count(*) AS unmatched
  FROM synarcheck sc
  LEFT JOIN synar_inspector si
    ON si.check_year = sc.checkyear AND si.ia_code = sc.ia
 WHERE sc.checkyear = 2026 AND si.ia_code IS NULL;

COMMIT;
