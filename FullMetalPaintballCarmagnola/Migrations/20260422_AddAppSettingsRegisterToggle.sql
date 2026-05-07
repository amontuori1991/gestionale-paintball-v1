CREATE TABLE IF NOT EXISTS "AppSettings" (
    "Key" character varying(100) NOT NULL,
    "Value" character varying(2000),
    CONSTRAINT "PK_AppSettings" PRIMARY KEY ("Key")
);

INSERT INTO "AppSettings" ("Key", "Value")
VALUES ('RegisterEnabled', 'false')
ON CONFLICT ("Key") DO NOTHING;
