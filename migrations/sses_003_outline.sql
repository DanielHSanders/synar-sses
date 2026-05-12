-- Migration: sses_003_outline
-- Date: 2026-05-12
-- Purpose: Tables for the Outline page (process steps + year-tagged completion
-- records). One outline shared across years; each step can have many entries
-- tagged with the year they happened. Schema is fully editable via the UI --
-- no auth in v1, anyone can add / edit / delete.

BEGIN;

CREATE TABLE outline_section (
    section_id  serial      PRIMARY KEY,
    sort_order  int         NOT NULL,
    title       text        NOT NULL,
    date_range  text        NOT NULL DEFAULT '',   -- "Feb-Mar" free-text label
    body_md     text        NOT NULL DEFAULT '',
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE outline_entry (
    entry_id     serial      PRIMARY KEY,
    section_id   int         NOT NULL
                 REFERENCES outline_section(section_id) ON DELETE CASCADE,
    sort_order   int         NOT NULL DEFAULT 0,
    year         smallint,                          -- nullable: not all entries are year-specific
    label        text        NOT NULL,
    completed_on date,
    notes_md     text        NOT NULL DEFAULT '',
    updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_outline_entry_section ON outline_entry(section_id, sort_order);
CREATE INDEX idx_outline_entry_year    ON outline_entry(year);

-- Grant SELECT/INSERT/UPDATE/DELETE to the app role.
GRANT SELECT, INSERT, UPDATE, DELETE ON outline_section, outline_entry TO synar_app;
GRANT USAGE ON SEQUENCE outline_section_section_id_seq TO synar_app;
GRANT USAGE ON SEQUENCE outline_entry_entry_id_seq     TO synar_app;

COMMIT;
