using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Services;

public static class AmichevoliSchema
{
    public static Task EnsureAsync(TesseramentoDbContext db) => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "PartiteAmichevoli" (
            "Id" uuid PRIMARY KEY,
            "Data" timestamp with time zone NOT NULL,
            "Tipo" text NOT NULL CHECK ("Tipo" IN ('Adulti', 'Kids')),
            "ColpiIllimitati" boolean NOT NULL,
            "Note" text NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedBy" text NULL,
            "IsDeleted" boolean NOT NULL DEFAULT false
        );
        INSERT INTO "PartiteAmichevoli"
            ("Id", "Data", "Tipo", "ColpiIllimitati", "Note", "CreatedAtUtc", "UpdatedAtUtc", "UpdatedBy", "IsDeleted")
        VALUES ('7b34f58d-79c7-4e24-b72c-e8273c271846', '2026-10-04 00:00:00+00', 'Adulti', false,
            'Animatori Oratorio', now(), now(), 'Inserimento iniziale richiesto da Admin', false)
        ON CONFLICT ("Id") DO NOTHING;
        """);
}
