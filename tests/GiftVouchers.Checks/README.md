# Gift voucher checks

Run from the repository root with a disposable local PostgreSQL server on
127.0.0.1:55439 and a `bonus_tests` role allowed to create databases:

```powershell
dotnet run --project tests/GiftVouchers.Checks
```

The checks create a uniquely named database and remove it on normal completion.
They never read the application connection string or connect to Neon.
For browser tests add `-- --preview`; then run `node tests/GiftVouchers.Checks/browser-check.cjs`
with Playwright installed and Edge available. The preview server uses test-only
authentication, bound to loopback. Do not deploy this test project.

An optional `--signature <local-image-path>` argument exercises an actual signature;
otherwise a synthetic image is used. Generated assets stay under `.codex-build/vouchers`.

Coverage: payment and role restrictions, redemption/undo with mandatory reason,
expiration including leap anniversary and inclusive last day, optimistic concurrency,
CSRF, production DDL idempotence, independent booking/calendar tables, signature upload,
five templates, PDF/JPG, actual MVC pages, mobile/desktop overflow, QR file scanning,
Kids defaults and decimal-comma form submission.

Deployment: `GiftVoucherSchema` creates the new table at startup and enables the
new feature for Staff once, without overriding later revocations. Admin manages
vouchers and uploads the signature in the new dashboard section. No signature or
customer data is committed. The original signature is stored in AppSettings,
not wwwroot. Issued company/package data is snapshotted; replacing the signature
affects subsequent exports. Riscatto never creates a booking or attendance day.
Real-device camera access and WhatsApp file sharing still require a device test.
