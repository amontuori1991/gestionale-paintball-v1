# Tournament Registration Checks

Run from the repository root:

```powershell
dotnet run --project tests/TorneoRegistrations.Checks
```

Uses the local disposable PostgreSQL test server on port 55439, user
`bonus_tests`, or the `TORNEO_TEST_POSTGRES` connection string. The account must
be able to create databases. A uniquely named database is created for each run
and dropped in `finally`; application/development databases are never used.

The suite exercises production EF mappings, idempotent tournament schema setup,
registration and deletion transactions, year boundaries, real public MVC forms,
anonymous privacy/authentication, antiforgery, actual ACSI export templates and
export markers. Mail is recorded, not delivered. Generated signature files use
a temporary directory and are removed after the run.

Deletion document cleanup contract:

1. Within the parent's tournament-locked transaction, call `GetSignatureCandidatesAsync(id)`.
2. Call `DeleteRegistrationsAsync(id)` and delete the tournament.
3. Commit the transaction before calling `CleanupSignaturesAsync(candidates, environment.WebRootPath)`.
4. Treat cleanup as best effort and log failures; never undo committed deletion because filesystem cleanup failed.

Cleanup preserves referenced documents, excludes preexisting membership originals
from collection, accepts only application-generated flat PNG filenames, and skips
symbolic links/reparse points. Legacy filenames remain untouched intentionally.
