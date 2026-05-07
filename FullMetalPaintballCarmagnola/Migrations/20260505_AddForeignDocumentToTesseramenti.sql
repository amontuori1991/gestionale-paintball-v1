ALTER TABLE "Tesseramenti"
ADD COLUMN IF NOT EXISTS "TipoDocumentoEstero" character varying(50) NULL,
ADD COLUMN IF NOT EXISTS "NumeroDocumentoEstero" character varying(100) NULL;
