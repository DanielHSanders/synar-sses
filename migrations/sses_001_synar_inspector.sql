-- Migration: sses_001_synar_inspector
-- Date: 2026-05-11
-- Purpose: Add the synar_inspector table to the shared `synar` database.
--
-- Why this table exists
--   synarcheck has iaage for each inspection but no gender. The SSES output
--   tables (Table 4 inspector demographics, Tables 6/7/8 buy-rate cross-tabs)
--   need gender per inspection. Inspector demographics are a property of the
--   inspector, not of the inspection -- a single IA does many inspections in
--   a year. So we factor them out into a per-year roster keyed by ia_code.
--
-- Cohabitation with SynarMatcher
--   SynarMatcher owns migrations 001-009 in its repo. This file lives in the
--   synar-sses repo and is prefixed `sses_` to make ownership obvious.
--
-- Run inside a transaction. ROLLBACK on any failure.

BEGIN;

CREATE TABLE synar_inspector (
    check_year smallint    NOT NULL,
    ia_code    varchar(20) NOT NULL,
    gender     char(1)     NOT NULL CHECK (gender IN ('M', 'F')),
    age        smallint    CHECK (age BETWEEN 14 AND 20),
    PRIMARY KEY (check_year, ia_code)
);

COMMENT ON TABLE  synar_inspector       IS 'Youth-inspector demographics per checkyear, used by the SSES app to populate gender/age in the SAMHSA output tables.';
COMMENT ON COLUMN synar_inspector.ia_code IS 'Matches synarcheck.ia (e.g. "118-26").';

COMMIT;
