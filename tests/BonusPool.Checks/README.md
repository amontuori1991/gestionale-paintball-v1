# Bonus Pool checks

Run from the repository root:

```powershell
dotnet run --project tests/BonusPool.Checks -- --view
```

Checks the original allocation example, duration rates, refused/cancelled/pending
requests, rounding, week boundaries, snapshots, mutation authorization attributes
and rendering of the compiled Razor view for Admin and Staff. No production data
or email service is used.

`--preview` serves sample data on `http://127.0.0.1:55440/` for responsive checks.
This is a rendering-only preview; its form actions are not wired to a database.

`--database` additionally checks persistence, concurrency and closing using a
dedicated fresh PostgreSQL database named `bonus_pool_checks` on loopback port
55439, with local test user `bonus_tests`. The connection is deliberately fixed
and never reads application credentials. Create this database on an isolated
local test cluster before running; do not point it at an existing application
database. It retains test records and refuses to run again against used data.

Weekly rates are captured with the first request. Only explicitly confirmed
played games with verified attendees contribute. A staff refusal blocks all
weekly payments; customer refusal and cancelled games add no money or attendance.
Closing is available from Saturday and requires all relevant outcomes resolved.
Closed records retain their rates, requests and final allocation independently
of subsequent booking or default-rate changes.
