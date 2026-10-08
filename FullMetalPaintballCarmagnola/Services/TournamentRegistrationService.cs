using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Services;

public class TournamentRegistrationService(TesseramentoDbContext db, ILogger<TournamentRegistrationService>? logger = null)
{
    public Task<TorneoSquadra?> FindTeamAsync(Guid token) => db.TorneoSquadre.Include(s => s.Torneo)
        .SingleOrDefaultAsync(s => s.Token == token && token != Guid.Empty);

    public async Task<(Tesseramento Membership, bool Duplicate, bool Existing)> RegisterAsync(
        Guid token, Tesseramento submitted)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var team = await FindTeamAsync(token)
            ?? throw new InvalidOperationException("Link squadra non disponibile.");
        // Serialize registration with the parent's tournament deletion lock.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tornei\" WHERE \"Id\" = {team.TorneoId} FOR UPDATE");
        var registrations = await db.TorneoIscrizioni.Include(i => i.Tesseramento)
            .Where(i => i.TorneoSquadra.TorneoId == team.TorneoId).ToListAsync();
        var duplicate = registrations.FirstOrDefault(i => SamePerson(i.Tesseramento, submitted));
        if (duplicate != null)
        {
            await transaction.CommitAsync();
            return (duplicate.Tesseramento, true, !duplicate.NuovoTesseramento);
        }

        var start = DateTime.SpecifyKind(new DateTime(team.Torneo.Data.Year, 1, 1), DateTimeKind.Utc);
        var end = start.AddYears(1);
        var candidates = await db.Tesseramenti.Where(t => !t.NoTesseramento &&
            (t.AnnoValiditaTesseramento == team.Torneo.Data.Year ||
            (t.AnnoValiditaTesseramento == null &&
            ((t.Partita != null && t.Partita.Data >= start && t.Partita.Data < end) ||
             (t.PartitaId == null && t.TorneoOrigineId == null && t.DataCreazione >= start && t.DataCreazione < end) ||
             db.Tornei.Any(evento => evento.Id == t.TorneoOrigineId && evento.Data >= start && evento.Data < end) ||
             db.TorneoIscrizioni.Any(i => i.TesseramentoId == t.Id &&
                 i.TorneoSquadra.Torneo.Data >= start && i.TorneoSquadra.Torneo.Data < end)))))
            .ToListAsync();
        var existing = candidates.FirstOrDefault(t => SamePerson(t, submitted));
        var membership = existing ?? submitted;
        membership.AnnoValiditaTesseramento = team.Torneo.Data.Year;
        if (existing != null && existing.TorneoOrigineId != team.TorneoId)
            existing.TorneoOrigineId = null;
        if (existing == null)
        {
            membership.Id = 0;
            membership.PartitaId = null;
            membership.Tessera = null;
            membership.NoTesseramento = false;
            membership.TorneoOrigineId = team.TorneoId;
            membership.DataCreazione = DateTime.UtcNow;
            db.Tesseramenti.Add(membership);
        }
        db.TorneoIscrizioni.Add(new TorneoIscrizione
        {
            TorneoSquadraId = team.Id,
            Tesseramento = membership,
            NuovoTesseramento = existing == null,
            Firma = submitted.Firma,
            DataCreazione = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (membership, false, existing != null);
    }

    // Caller must hold a transaction and a FOR UPDATE lock on the tournament row.
    public async Task<int> DeleteRegistrationsAsync(int torneoId)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Tournament deletion requires a transaction and tournament lock.");
        // Also serialize with export marking, which updates the same membership rows.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tesseramenti\" WHERE \"TorneoOrigineId\" = {torneoId} FOR UPDATE");
        var registrations = await db.TorneoIscrizioni
            .Where(i => i.TorneoSquadra.TorneoId == torneoId).ToListAsync();
        var owned = await db.Tesseramenti.Where(t => t.TorneoOrigineId == torneoId).ToListAsync();
        var removable = new List<Tesseramento>();
        foreach (var membership in owned)
        {
            // A card is conservative protection, including historical exports predating the marker.
            if (membership.PartitaId != null || membership.EsportatoAcsiIl != null ||
                !string.IsNullOrWhiteSpace(membership.Tessera))
                continue;
            if (await db.TorneoIscrizioni.AnyAsync(i => i.TesseramentoId == membership.Id &&
                i.TorneoSquadra.TorneoId != torneoId))
                continue;
            // Legacy match attendance is stored as separate rows, not references to membership.
            var others = await db.Tesseramenti.Where(t => t.Id != membership.Id &&
                t.DataNascita == membership.DataNascita).ToListAsync();
            if (others.Any(t => SamePerson(t, membership)))
                continue;
            removable.Add(membership);
        }
        db.TorneoIscrizioni.RemoveRange(registrations);
        foreach (var membership in owned)
            membership.TorneoOrigineId = null;
        await db.SaveChangesAsync();
        db.Tesseramenti.RemoveRange(removable);
        await db.SaveChangesAsync();
        return removable.Count;
    }

    public async Task<IReadOnlyList<string>> GetSignatureCandidatesAsync(int torneoId)
    {
        var candidates = await db.Tesseramenti.AsNoTracking().Where(t => t.TorneoOrigineId == torneoId)
            .Select(t => t.Firma).ToListAsync();
        candidates.AddRange(await db.TorneoIscrizioni.AsNoTracking()
            .Where(i => i.TorneoSquadra.TorneoId == torneoId && i.Firma != i.Tesseramento.Firma)
            .Select(i => i.Firma).ToListAsync());
        // Never collect the original signature of a preexisting membership.
        var protectedSignatures = await db.Tesseramenti.AsNoTracking()
            .Where(t => t.TorneoOrigineId != torneoId && candidates.Contains(t.Firma))
            .Select(t => t.Firma).ToListAsync();
        return candidates.Except(protectedSignatures, StringComparer.Ordinal)
            .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
    }

    // Call only after committing event deletion; a rollback must never remove documents.
    public async Task<int> CleanupSignaturesAsync(IEnumerable<string> candidates, string webRoot)
    {
        if (string.IsNullOrWhiteSpace(webRoot)) return 0;
        var directory = Path.GetFullPath(Path.Combine(webRoot, "Firme"));
        if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            return 0;
        var removed = 0;
        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            // Accept only the application-generated flat PNG names, never URLs or relative paths.
            if (!Regex.IsMatch(candidate ?? "", @"^/Firme/firma_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.png$"))
                continue;
            var path = Path.GetFullPath(Path.Combine(directory, Path.GetFileName(candidate!)));
            if (!string.Equals(Path.GetDirectoryName(path), directory,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                continue;
            if (await db.Tesseramenti.AsNoTracking().AnyAsync(t => t.Firma == candidate) ||
                await db.TorneoIscrizioni.AsNoTracking().AnyAsync(i => i.Firma == candidate))
                continue;
            try
            {
                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                File.Delete(path);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger?.LogWarning(ex, "Could not remove unreferenced tournament signature {Signature}.", candidate);
            }
        }
        return removed;
    }

    private static bool SamePerson(Tesseramento first, Tesseramento second)
    {
        var cf = Normalize(first.CodiceFiscale);
        return (cf.Length > 0 && cf == Normalize(second.CodiceFiscale)) ||
            (first.DataNascita.Date == second.DataNascita.Date &&
             Normalize(first.Nome) == Normalize(second.Nome) &&
             Normalize(first.Cognome) == Normalize(second.Cognome));
    }

    private static string Normalize(string? value)
    {
        var text = new string((value ?? "").Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(text.Normalize(NormalizationForm.FormC), @"\s+", " ").Trim().ToUpperInvariant();
    }
}
