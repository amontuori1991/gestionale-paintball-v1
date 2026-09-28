using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Services;

public sealed class CompanyProfileService(TesseramentoDbContext db)
{
    private const string Key = "CompanyProfileV1";

    public async Task<CompanyProfile> GetAsync(CancellationToken ct = default)
    {
        var json = await db.AppSettings.AsNoTracking().Where(s => s.Key == Key).Select(s => s.Value).SingleOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<CompanyProfile>(json)
            ?? throw new InvalidDataException("Profilo azienda non valido.");
    }

    public async Task SaveAsync(CompanyProfile profile, CancellationToken ct = default)
    {
        profile.Normalize();
        Validator.ValidateObject(profile, new ValidationContext(profile), true);
        var json = JsonSerializer.Serialize(profile);
        // Atomic upsert also handles two admins creating the profile for the first time.
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""AppSettings"" (""Key"", ""Value"") VALUES ({Key}, {json})
            ON CONFLICT (""Key"") DO UPDATE SET ""Value"" = EXCLUDED.""Value"";", ct);
    }
}
