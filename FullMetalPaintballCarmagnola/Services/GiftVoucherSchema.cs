using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Services;

public static class GiftVoucherSchema
{
    public static Task EnsureAsync(TesseramentoDbContext db) => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "GiftVouchers" (
            "Id" uuid PRIMARY KEY, "Code" text NOT NULL UNIQUE,
            "Payload" text NOT NULL, "CompanyJson" text NOT NULL,
            "IssuedOn" date NOT NULL, "ExpiresOn" date NOT NULL,
            "Paid" boolean NOT NULL DEFAULT false,
            "Redeemed" boolean NOT NULL DEFAULT false,
            "Cancelled" boolean NOT NULL DEFAULT false,
            "Version" integer NOT NULL DEFAULT 0, "HistoryJson" text NOT NULL,
            CHECK (NOT "Redeemed" OR ("Paid" AND NOT "Cancelled"))
        );
        INSERT INTO "RolePermissions" ("RoleName", "FeatureName", "IsAllowed")
        SELECT 'Staff', 'Buoni regalo', true
        WHERE NOT EXISTS (SELECT 1 FROM "RolePermissions" WHERE "RoleName" = 'Staff' AND "FeatureName" = 'Buoni regalo');
        """);
}
