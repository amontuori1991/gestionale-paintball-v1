ALTER TABLE "Tesseramenti"
ADD COLUMN IF NOT EXISTS "NewsletterConsent" BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE "Tesseramenti"
ADD COLUMN IF NOT EXISTS "NewsletterUnsubscribeToken" VARCHAR(64);

UPDATE "Tesseramenti"
SET "NewsletterConsent" = TRUE
WHERE "NewsletterConsent" IS NULL;

UPDATE "Tesseramenti"
SET "NewsletterUnsubscribeToken" = md5(random()::text || clock_timestamp()::text || "Id"::text)
WHERE "NewsletterUnsubscribeToken" IS NULL
   OR btrim("NewsletterUnsubscribeToken") = '';

ALTER TABLE "Tesseramenti"
ALTER COLUMN "NewsletterUnsubscribeToken" SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tesseramenti_NewsletterUnsubscribeToken"
ON "Tesseramenti" ("NewsletterUnsubscribeToken");
