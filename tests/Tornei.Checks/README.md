# Tournament checks

Standalone .NET 8 console checks referencing the real application; no test-framework packages or changes to other harnesses.

From the repository root:

```powershell
dotnet run --project tests/Tornei.Checks
dotnet run --project tests/Tornei.Checks -- --postgres
```

The default run exhaustively checks 2..64 teams, every balanced group count with at least two teams per group, both single and home/away schedules, and brackets with 2..64 qualifiers (both winner orientations and optional third place). It also covers configurable decimal standings, score tie-breakers, qualification ties, seeding, generation guards and form validation. Each scenario runs independently; failures print their parameters and produce exit code 1.

The optional PostgreSQL check uses only the existing local test-server convention: `127.0.0.1:55439`, user `bonus_tests`, maintenance database `postgres`. It creates a uniquely named `tornei_checks_*` database, exercises the production tournament DDL twice, round-trips teams/results/decimal points/finals, checks optimistic concurrency and database cascades, then drops only its own database in `finally`. It never reads application configuration. An unavailable server is a failure when explicitly requested, not a silent skip. HTTP authorization and browser tests are outside this harness.

Additional checks exhaust every form-valid combination of multiple groups and qualifiers up to 64 teams: opening fixtures must cross groups, preserve higher seeds/byes, include each qualifier exactly once and remain deterministic. The knockout-result regressions require `Advance` to reject draws and unknown results instead of silently advancing the away team.

The PostgreSQL run also executes 14 direct controller checks for frozen group/semifinal results, knockout draws, invalid/missing scores, explicit winners after tied knockout scores, stale writes, third-place results, frozen group/priority/rule edits and allowed team edits. These call real actions against the disposable database, not an HTTP server; authentication, antiforgery, model binding and rendered browser behavior remain the parent's HTTP harness responsibility.

Verified on 2026-10-08: 2,488 scenarios passed, zero failures with `--postgres`, including the 14 nested controller checks. Disposable database cleanup was verified. Engine regressions fixed during this work: same-group opening fixtures (including A1-A2/B1-B2 semifinals) and silent away-team advancement for invalid knockout outcomes.
