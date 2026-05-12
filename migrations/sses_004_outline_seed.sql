-- Migration: sses_004_outline_seed
-- Date: 2026-05-12
-- Purpose: Initial outline content lifted from Synar Timeline.docx. Sections
-- are in chronological order through the year; year-tagged entries capture
-- the specific dates each task was completed.

BEGIN;

INSERT INTO outline_section (sort_order, title, date_range, body_md) VALUES
(1, 'Data Gathering', 'Feb-Mar', $$
Synar lists are obtained from three primary sources each year. Starting in
2026, Tobacco Licensing replaced the discontinued Data Axle list as the
primary source.

### ABC — Tobacco Licensing (primary 2026 onward)
- **Source:** Steve Cambron
- **Their contact:** Bell, William D (CHFS DPH DPHPS) — <william.bell@ky.gov>

### Lottery
Vendors that sell Lottery tickets in KY.
- **Source:** Steve Cambron
- **Their contact:** Kate Hanratty — <Kate.Hanratty@kylottery.com>

### SNAP
Vendors who participate in the SNAP program.
- **Source:** <SM.FN.RPMDHQ-WEB@usda.gov>

### Older Sources
- Data Axle — paid list, discontinued 2025
- Jeff County Vape Vendors
- Other vape sources from Steve
$$),

(2, 'Data Cleaning', 'Feb-May', $$
1. **Find previous-year matches.** Retailers matched in previous years that retain the same ID can be auto-matched. Handled by Tim or in Claude Code.
2. **Run Synar Matcher.** <https://synar-matcher.reacheval.com> — finds retailers that are new on a list but already have a pin in Synar Maps. Also enriches list data with Google links for new pins.
3. **Use Synar Maps.** <https://synar.reacheval.com> — once all previous-pin matches have been found, lists can be loaded into the Synar Maps "Additional Outlets" module so a new pin can be created. Peter Brothers was the primary user 2021–2025.
$$),

(3, 'Sampling', 'May', $$
<https://sses.reacheval.com> — new in 2026, this program recreates the outdated federal SSES Excel spreadsheet and draws the year's sample.
$$),

(4, 'Synar Check Sheets', 'May', $$
New application in 2026 created by Tim and LeeAnna Davis.
$$),

(5, 'Physical Synar Checks', 'May/June – Sep', $$
ABC officers conduct Synar Checks using the printed Synar Sheets over the summer.

- **State contact:** Alexis Ellis — <alexis.ellis@ky.gov>
$$),

(6, 'Data Sheet Scanning', 'Oct', $$
Tab Services scans and processes the inspection forms after the field season.

- **Contact:** Matt Lambert — <matt@tabservice.com>
- In 2025, Daniel scanned the forms at REACH and sent the file on to Tab for processing.
$$),

(7, 'Reporting', 'Oct/Nov', $$
- <https://sses.reacheval.com> generates the SAMHSA SSES workbook.
- Tableau Synar Report — update with completed data.
$$);

-- Year-tagged completion entries.
INSERT INTO outline_entry (section_id, sort_order, year, label, completed_on, notes_md) VALUES
((SELECT section_id FROM outline_section WHERE title='Data Gathering'),     10, 2026, 'ABC list provided',     '2026-01-28', ''),
((SELECT section_id FROM outline_section WHERE title='Data Gathering'),     20, 2026, 'Lottery list provided', '2026-02-04', ''),
((SELECT section_id FROM outline_section WHERE title='Data Gathering'),     30, 2026, 'SNAP list provided',    '2026-02-10', ''),
((SELECT section_id FROM outline_section WHERE title='Sampling'),           10, 2026, 'Sample run',            '2026-05-12', ''),
((SELECT section_id FROM outline_section WHERE title='Synar Check Sheets'), 10, 2025, 'Sheets picked up by Steve Cambron', '2025-06-06', ''),
((SELECT section_id FROM outline_section WHERE title='Data Sheet Scanning'),10, 2025, 'Scanned file sent to Tab Services', '2025-09-22', ''),
((SELECT section_id FROM outline_section WHERE title='Reporting'),          10, 2025, 'Files sent to Steve Cambron',       '2025-10-25', '');

COMMIT;
