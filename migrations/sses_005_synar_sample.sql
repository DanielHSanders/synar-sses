-- Migration: sses_005_synar_sample
-- Date: 2026-09-23
-- Purpose: Record each drawn Synar sample.
--
-- Why this exists
--   The frame size that belongs in the SSES report is the population the
--   sample was actually drawn from at sampling time. map_location changes
--   continuously, so a count taken months later is not that number -- the
--   2026 sample was drawn against 4307 outlets but the same filter returns
--   4308 today. Without this table the report page can only guess, and a
--   wrong frame silently changes the weights and the standard error.
--
--   Recording the calculator inputs alongside the frame also makes a draw
--   reproducible and gives SAMHSA an audit trail for how the sample size
--   was arrived at.

BEGIN;

CREATE TABLE synar_sample (
    sample_id     serial      PRIMARY KEY,
    check_year    smallint    NOT NULL,
    state_code    char(2)     NOT NULL DEFAULT 'KY',
    drawn_at      timestamptz NOT NULL DEFAULT now(),

    -- The population the draw was made against, and how it was filtered.
    frame_size    int         NOT NULL,
    frame_filter  text,

    -- Sample-size calculator inputs. Nullable so historical samples can be
    -- backfilled with just the frame size when the inputs are not known.
    expected_rvr_pct      numeric,
    design_effect         numeric,
    accuracy_rate_pct     numeric,
    completion_rate_pct   numeric,
    safety_margin_pct     numeric,
    one_sided_ci          boolean,
    effective_sample_size int,
    target_sample_size    int,
    original_sample_size  int,

    -- Several draws may happen while planning; this marks the one that was
    -- actually sent to the field. The report falls back to the most recent
    -- draw for the year when nothing is marked.
    is_final      boolean     NOT NULL DEFAULT false,
    note          text
);

CREATE INDEX idx_synar_sample_year ON synar_sample(check_year, drawn_at DESC);

CREATE TABLE synar_sample_outlet (
    sample_id     int      NOT NULL
                  REFERENCES synar_sample(sample_id) ON DELETE CASCADE,
    synar_maps_id int      NOT NULL,
    check_type    text,
    PRIMARY KEY (sample_id, synar_maps_id)
);

COMMENT ON TABLE  synar_sample        IS 'One row per drawn Synar sample, with the frame it was drawn against.';
COMMENT ON COLUMN synar_sample.frame_size IS 'Population size at draw time -- this is the N the SSES report must use.';
COMMENT ON TABLE  synar_sample_outlet IS 'The outlets selected in a given draw.';

GRANT SELECT, INSERT, UPDATE, DELETE ON synar_sample, synar_sample_outlet TO synar_app;
GRANT USAGE ON SEQUENCE synar_sample_sample_id_seq TO synar_app;

COMMIT;
