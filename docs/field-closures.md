# Field closures

`CampoChiusure.OraInizio` and `OraFine` are nullable time columns added idempotently
at application startup. Existing records remain full-day closures.

Leave both times empty to close whole days. Otherwise supply both times, with the
end strictly after the start and within the same day. The interval repeats on
each day in the inclusive date range. Add separate entries for disjoint periods.
Overnight closures require two entries. Existing bookings are not canceled.

Internal availability and public weekend slots use the same interval calculation.
Closures do not add the booking-only 30-minute buffer. Overlapping closed/busy
periods merge; a game may end at closure start or begin at closure end. The public
weekday inquiry also rejects requests overlapping closure hours. Like the
existing booking page, these client-side options are a snapshot at page load.

Staff absence/presence calendars only treat full-day closures as closed days,
so partial closures never disable the whole day's staff controls.

Tests: `dotnet run --project tests/DisponibilitaCampo.Checks` and
`dotnet run --project tests/PhotoAlbums.Checks` (isolated local PostgreSQL).
