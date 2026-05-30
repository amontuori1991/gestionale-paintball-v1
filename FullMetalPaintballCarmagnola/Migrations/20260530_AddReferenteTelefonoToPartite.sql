ALTER TABLE "Partite"
    ADD COLUMN IF NOT EXISTS "NomeRiferimento" text NULL,
    ADD COLUMN IF NOT EXISTS "PrefissoTelefonoRiferimento" text NULL,
    ADD COLUMN IF NOT EXISTS "TelefonoRiferimento" text NULL;
