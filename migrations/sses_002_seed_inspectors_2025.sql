-- Migration: sses_002_seed_inspectors_2025
-- Date: 2026-05-11
-- Purpose: One-time seed of synar_inspector with the 2025 KY youth-inspector
-- roster, extracted from the "IA Ratio" sheet of
-- `2025 Synar Master Spreadsheet - Completed .xlsx`. Future years will be
-- loaded by a UI in the SSES app rather than via SQL migrations.

BEGIN;

INSERT INTO synar_inspector (check_year, ia_code, gender, age) VALUES
  (2025, '097-70', 'F', 17),
  (2025, '008-33', 'F', 16),
  (2025, '015-25', 'F', 17),
  (2025, '120-40', 'M', 18),
  (2025, '097-69', 'F', 18),
  (2025, '079-06', 'M', 17),
  (2025, '030-39', 'F', 17),
  (2025, '091-32', 'M', 17),
  (2025, '056-81', 'F', 17),
  (2025, '120-39', 'M', 18),
  (2025, '024-62', 'F', 19),
  (2025, '109-05', 'F', 18),
  (2025, '047-44', 'F', 19),
  (2025, '030-38', 'M', 19),
  (2025, '106-58', 'F', 18),
  (2025, '093-58', 'M', 19),
  (2025, '058-30', 'M', 18),
  (2025, '022-54', 'F', 17),
  (2025, '063-02', 'M', 18),
  (2025, '084-01', 'M', 17),
  (2025, '032-11', 'F', 19),
  (2025, '042-24', 'M', 19),
  (2025, '022-53', 'M', 17),
  -- 063-02 appears twice in the IA Ratio source (M18 and M19; inspector aged
  -- during the year). Gender is identical so we keep one row; per-inspection
  -- age comes from synarcheck.iaage at calculation time.
  (2025, '025-59', 'M', 19),
  (2025, '118-26', 'M', 19),
  (2025, '076-67', 'F', 20),
  (2025, '071-06', 'M', 18)
ON CONFLICT (check_year, ia_code) DO UPDATE SET gender=EXCLUDED.gender, age=EXCLUDED.age;

COMMIT;
