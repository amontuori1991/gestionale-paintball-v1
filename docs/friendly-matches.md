# Friendly matches

`/Amichevoli` is reachable from Statistics and requires its existing feature policy.
Readers can consult the register; only Admin can save, edit or remove records.
POST actions require antiforgery tokens and use a separate validated input model.

The `PartiteAmichevoli` table has no booking/staff/calendar foreign keys. A UNION ALL
projection inside Statistics adds active friendly matches to confirmed monthly,
annual, year-to-date, type and adult-only shot-mode totals. Staff, cancellations,
memberships, geography, financial data and booking calendars remain untouched.

Removal preserves the record in the register but excludes it from every count.
Creation time and last update time/operator are retained. A stable per-form UUID
prevents duplicate inserts from retries; separate matches on the same date are allowed.

Startup creates the table idempotently and seeds the requested 2026-10-04 Adults,
standard shots, `Animatori Oratorio` entry with a stable UUID. ON CONFLICT leaves
later edits or removals intact and prevents duplicates after redeployment.
No manual production SQL is required.

Integration coverage in PhotoAlbums.Checks uses a disposable local PostgreSQL
database to test initial schema creation, repeated seed, aggregation, date cutoff,
admin-only writes, CSRF, validation, edits, soft deletion and operational isolation.
