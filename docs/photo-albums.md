# Booking photo albums

## Production configuration

Set the following Render environment variables (never commit their values):

- `R2__BucketName`: dedicated private bucket, e.g. `paintball-foto-partite`.
- `R2__ServiceUrl`: the bucket's S3 endpoint. EU jurisdiction requires
  `https://<ACCOUNT_ID>.eu.r2.cloudflarestorage.com`.
- `R2__AccessKeyId`: R2 S3 Access Key ID.
- `R2__SecretAccessKey`: R2 S3 Secret Access Key, not the API token value.

Use Object Read & Write permissions scoped to this bucket only. Do not enable
public access, r2.dev or a public custom domain. No CORS rule is necessary:
uploads pass through the authenticated application, not browser-to-R2 PUTs.

Configure an enabled R2 object lifecycle rule to delete uploaded objects after
7 days, covering all prefixes in this dedicated bucket. Keep the default rule
that aborts incomplete multipart uploads. Do not add a bucket lock. Physical
deletion follows Cloudflare's lifecycle processing schedule; independently,
the application excludes expired images and refuses new download URLs once
their seven-day availability ends. Signed URLs last at most five minutes and
never beyond the image expiration. Already downloaded images cannot be revoked.

The application creates `PhotoAlbums` and its unique token index on startup,
following the existing database initialization pattern. No manual SQL is needed.
Only the booking ID and random album token are stored in Neon; image bytes and
upload timestamps are in R2. No persistent Render disk is required.

## Behavior and limits

- Upload/manage/delete require Admin or Staff plus the Prenotazioni permission.
- The public album exposes no participant names, phone numbers or upload controls.
- Possession of the random album link grants read access. Share it only with the
  corresponding group. Canceled bookings are not publicly accessible.
- Album links are created on first management-page visit or booking-summary
  generation and stay stable across deployments and credential rotation.
- Italian and English summary messages include the album URL in HTML and plain text.
- Each file expires individually seven days after upload, not after the booking.
- Up to 60 currently available images per album; input <=15 MiB and <=32 MP.
- Supported: static JPEG, PNG, WebP, BMP, GIF. Convert HEIC/HEIF and other formats before uploading.
- Video and animated images are rejected by server-side decoding, even with an image extension.
- Upload percentage measures browser transfer only; logo processing and R2 saving have a separate indeterminate phase.
- The WhatsApp quick-action menu includes a dedicated photo download message in the booking language.
- Images are re-encoded to JPEG, <=2200 px longest edge, quality 85, <=5 MiB.
  All EXIF orientations are normalized; source metadata is not copied. The
  existing `wwwroot/img/logo.gif` is applied at bottom right. Originals are not kept.
- Uploads run sequentially in the browser. The per-process semaphore bounds
  native decoding memory and coordinates uploads on the current single-instance
  Render service. Before scaling to multiple instances, replace this with a
  distributed album quota/lock if a strict global 60-file cap is required.
- No claim of unlimited free usage: monitor R2 storage and operation billing.

## Verification

`dotnet run --project tests/PhotoAlbums.Checks` uses an isolated PostgreSQL test
database on **127.0.0.1:55439**, role **bonus_tests**, and an in-memory photo store.
It never loads production database/R2 credentials. Tests cover watermarking,
resizing, eight EXIF orientations, metadata removal, image/album limits, expiry,
signed URL generation, stable tokens, IT/EN summaries, HTTP permissions, CSRF,
multipart upload, deletion, canceled bookings and cross-album isolation.

`--preview` serves disposable test pages on port 55443. `/preview/login` supplies
a test-only staff cookie, and `/preview/table` renders sample bookings. These
routes and the test authentication handler exist only in the test executable.

After deployment, verify the real R2 configuration using one non-sensitive test
photo from an authorized staff account. Check upload, visible logo, public
download, and deletion. Then check that the object lifecycle rule is enabled in
Cloudflare. Local tests cannot validate credentials configured only on Render.
