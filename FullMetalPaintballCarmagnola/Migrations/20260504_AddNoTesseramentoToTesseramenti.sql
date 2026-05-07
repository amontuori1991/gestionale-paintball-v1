ALTER TABLE "Tesseramenti"
ADD COLUMN IF NOT EXISTS "NoTesseramento" boolean NOT NULL DEFAULT false;
