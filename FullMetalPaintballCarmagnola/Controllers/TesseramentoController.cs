using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Tesserati")]
    public class TesseramentoController : Controller
    {
        private readonly TesseramentoDbContext _dbContext;
        private readonly IEmailService _emailSender;
        private readonly AcsiOdsExportService _acsiOdsExportService;
        private readonly ILogger<TesseramentoController> _logger;
        private const string TesseramentoLiberoEnabledSettingKey = "TesseramentoLiberoEnabled";
        private static readonly DateTime DataAvvioFabbisognoTessereUtc =
            DateTime.SpecifyKind(new DateTime(2026, 5, 1), DateTimeKind.Utc);

        public TesseramentoController(
            TesseramentoDbContext dbContext,
            IEmailService emailSender,
            AcsiOdsExportService acsiOdsExportService,
            ILogger<TesseramentoController> logger)
        {
            _dbContext = dbContext;
            _emailSender = emailSender;
            _acsiOdsExportService = acsiOdsExportService;
            _logger = logger;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(int? partitaId = null, string lang = "it")
        {
            if (!partitaId.HasValue && !await TesseramentoLiberoAbilitatoAsync())
                return View("NonDisponibile");

            var model = new TesseramentoViewModel();
            if (partitaId.HasValue)
                model.PartitaId = partitaId;

            model.Lingua = NormalizeLanguage(lang);
            ViewBag.Lingua = model.Lingua;

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(TesseramentoViewModel model)
        {
            model.Lingua = NormalizeLanguage(model.Lingua);
            ViewBag.Lingua = model.Lingua;

            if (!model.PartitaId.HasValue && !await TesseramentoLiberoAbilitatoAsync())
                return View("NonDisponibile");

            if (model.NatoEstero)
            {
                ModelState.Remove(nameof(model.ComuneNascita));
                ModelState.Remove(nameof(model.CodiceCatastaleNascita));
                ModelState.Remove(nameof(model.CodiceFiscale));
            }
            else
            {
                ModelState.Remove(nameof(model.NazioneNascita));
                ModelState.Remove(nameof(model.CittaNascita));
                ModelState.Remove(nameof(model.NazioneCittadinanza));
                ModelState.Remove(nameof(model.NazioneResidenza));
                ModelState.Remove(nameof(model.TipoDocumentoEstero));
                ModelState.Remove(nameof(model.NumeroDocumentoEstero));
            }

            if (!model.NatoEstero)
            {
                var comuneNascita = await ResolveComuneCatastaleAsync(model.ComuneNascita);
                if (comuneNascita is not null)
                {
                    model.ComuneNascita = comuneNascita.Nome;
                    model.CodiceCatastaleNascita = comuneNascita.CodiceCatastale;
                    ModelState.Remove(nameof(model.ComuneNascita));
                    ModelState.Remove(nameof(model.CodiceCatastaleNascita));
                }
                else if (!string.IsNullOrWhiteSpace(model.ComuneNascita))
                {
                    ModelState.Remove(nameof(model.CodiceCatastaleNascita));
                    ModelState.AddModelError(nameof(model.ComuneNascita), "Seleziona il comune di nascita dall'elenco proposto.");
                }
            }

            var residenzaItaliana = IsItalia(model.NazioneResidenza) || !model.NatoEstero;
            if (residenzaItaliana)
            {
                var comuneResidenza = await ResolveComuneCatastaleAsync(model.ComuneResidenza);
                if (comuneResidenza is not null)
                {
                    model.ComuneResidenza = $"{comuneResidenza.Nome} ({comuneResidenza.Provincia})";
                    model.NazioneResidenza = "Italia";
                    ModelState.Remove(nameof(model.ComuneResidenza));
                }
                else if (!string.IsNullOrWhiteSpace(model.ComuneResidenza))
                {
                    ModelState.AddModelError(nameof(model.ComuneResidenza), "Seleziona il comune di residenza dall'elenco proposto.");
                }
            }

            if (!model.NatoEstero && model.DataNascita.HasValue && !string.IsNullOrWhiteSpace(model.CodiceCatastaleNascita))
            {
                model.CodiceFiscale = CodiceFiscaleService.Calcola(
                    model.Cognome,
                    model.Nome,
                    model.DataNascita.Value,
                    model.Genere,
                    model.CodiceCatastaleNascita);
            }

            if (model.Minorenne == "Sì")
            {
                if (string.IsNullOrWhiteSpace(model.NomeGenitore))
                    ModelState.AddModelError("NomeGenitore", "Il nome del genitore è obbligatorio se il tesserato è minorenne.");

                if (string.IsNullOrWhiteSpace(model.CognomeGenitore))
                    ModelState.AddModelError("CognomeGenitore", "Il cognome del genitore è obbligatorio se il tesserato è minorenne.");
            }

            if (ModelState.IsValid)
            {
                if (await IsStessoTesseratoGiaNellaPartitaAsync(model))
                {
                    TempData["NomeUtente"] = model.Nome + " " + model.Cognome;
                    return RedirectToAction("Duplicato");
                }

                model.NoTesseramento = await IsGiaTesseratoPerAnnoPartitaAsync(model);
                model.ComuneResidenza = await NormalizeComuneResidenzaAsync(model.ComuneResidenza, model.NazioneResidenza);

                string firmaFilePath = null;

                if (!string.IsNullOrEmpty(model.Firma))
                {
                    var base64 = model.Firma.StartsWith("data:image/png;base64,")
                        ? model.Firma.Substring("data:image/png;base64,".Length)
                        : model.Firma;

                    try
                    {
                        var firmaBytes = Convert.FromBase64String(base64);
                        var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        var firmaDirectoryPath = Path.Combine(webRootPath, "Firme");

                        if (!Directory.Exists(firmaDirectoryPath))
                            Directory.CreateDirectory(firmaDirectoryPath);

                        var fileName = "firma_" + Guid.NewGuid().ToString() + ".png";
                        firmaFilePath = "/Firme/" + fileName;
                        var fullFilePath = Path.Combine(firmaDirectoryPath, fileName);
                        await System.IO.File.WriteAllBytesAsync(fullFilePath, firmaBytes);
                    }
                    catch
                    {
                        ModelState.AddModelError("Firma", "Errore nella firma.");
                        return View(model);
                    }
                }

                try
                {
                    var entity = model.ToEntity(firmaFilePath);
                    entity.DataCreazione = DateTime.SpecifyKind(entity.DataCreazione, DateTimeKind.Utc);
                    _dbContext.Tesseramenti.Add(entity);
                    await _dbContext.SaveChangesAsync();

                    TempData["NomeUtente"] = model.Nome + " " + model.Cognome;
                    TempData["NoTesseramento"] = entity.NoTesseramento;

                    var firmaAbsoluteUrl = $"{Request.Scheme}://{Request.Host}{firmaFilePath}";
                    await _emailSender.SendTesseramentoNotification(model, firmaAbsoluteUrl);

                    return RedirectToAction("Successo");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Errore durante il salvataggio. Dettagli: " + ex.Message);
                    return View(model);
                }
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CercaComuni(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
                return Json(Array.Empty<object>());

            var value = term.Trim().ToUpper();

            var comuni = await _dbContext.ComuniCatastali
                .Where(c => c.Attivo && c.Nome.ToUpper().Contains(value))
                .OrderByDescending(c => c.Nome.ToUpper().StartsWith(value))
                .ThenBy(c => c.Nome)
                .ThenBy(c => c.Provincia)
                .Take(20)
                .Select(c => new
                {
                    label = c.Provincia == null ? c.Nome : $"{c.Nome} ({c.Provincia})",
                    value = c.Nome,
                    codiceCatastale = c.CodiceCatastale
                })
                .ToListAsync();

            return Json(comuni);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> CercaStati(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
                return Json(Array.Empty<object>());

            var value = term.Trim().ToUpper();

            var stati = await _dbContext.StatiEsteriCatastali
                .Where(s => s.Attivo && s.Nome.ToUpper().Contains(value))
                .OrderBy(s => s.Nome)
                .Take(20)
                .Select(s => new
                {
                    label = s.Nome,
                    value = s.Nome,
                    codiceCatastale = s.CodiceCatastale
                })
                .ToListAsync();

            return Json(stati);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult CalcolaCodiceFiscale([FromBody] CalcolaCodiceFiscaleRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Nome) ||
                string.IsNullOrWhiteSpace(request.Cognome) ||
                string.IsNullOrWhiteSpace(request.Sesso) ||
                string.IsNullOrWhiteSpace(request.CodiceCatastale) ||
                !request.DataNascita.HasValue)
            {
                return BadRequest(new { message = "Dati insufficienti per calcolare il codice fiscale." });
            }

            var codiceFiscale = CodiceFiscaleService.Calcola(
                request.Cognome,
                request.Nome,
                request.DataNascita.Value,
                request.Sesso,
                request.CodiceCatastale);

            return Json(new { codiceFiscale });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult CalcolaCodiceFiscale(string nome, string cognome, DateTime? dataNascita, string sesso, string codiceCatastale)
        {
            if (string.IsNullOrWhiteSpace(nome) ||
                string.IsNullOrWhiteSpace(cognome) ||
                string.IsNullOrWhiteSpace(sesso) ||
                string.IsNullOrWhiteSpace(codiceCatastale) ||
                !dataNascita.HasValue)
            {
                return BadRequest(new { message = "Dati insufficienti per calcolare il codice fiscale." });
            }

            var codiceFiscale = CodiceFiscaleService.Calcola(
                cognome,
                nome,
                dataNascita.Value,
                sesso,
                codiceCatastale);

            return Json(new { codiceFiscale });
        }

        private static string NormalizeLanguage(string? lang)
        {
            return string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "it";
        }

        [AllowAnonymous]
        public IActionResult Successo()
        {
            ViewBag.NomeUtente = TempData["NomeUtente"] as string;
            return View();
        }

        [AllowAnonymous]
        public IActionResult Duplicato()
        {
            ViewBag.NomeUtente = TempData["NomeUtente"] as string;
            return View();
        }

        public async Task<IActionResult> ListaTesseramenti(string searchNome, string searchCognome, string searchTessera, DateTime? dataDa, DateTime? dataA, int? partitaId, bool soloSenzaPartita = false, bool soloTessereDaAssociare = false)
        {
            var query = BuildListaTesseramentiQuery(searchNome, searchCognome, searchTessera, dataDa, dataA, partitaId, soloSenzaPartita, soloTessereDaAssociare);

            query = query
                .OrderByDescending(t => t.Partita != null ? t.Partita.Data : t.DataCreazione)
                .ThenByDescending(t => t.PartitaId)
                .ThenByDescending(t => t.Id);

            var tesseramenti = await query.ToListAsync();

            var viewModels = tesseramenti.Select(t => new TesseramentoViewModel
            {
                Id = t.Id,
                DataPartita = t.Partita?.Data,
                Nome = t.Nome,
                Cognome = t.Cognome,
                DataNascita = t.DataNascita,
                Genere = t.Genere,
                NatoEstero = t.NatoEstero,
                ComuneNascita = t.ComuneNascita,
                CodiceCatastaleNascita = t.CodiceCatastaleNascita,
                NazioneNascita = t.NazioneNascita,
                CittaNascita = t.CittaNascita,
                NazioneCittadinanza = t.NazioneCittadinanza,
                NazioneResidenza = t.NazioneResidenza,
                TipoDocumentoEstero = t.TipoDocumentoEstero,
                NumeroDocumentoEstero = t.NumeroDocumentoEstero,
                ComuneResidenza = t.ComuneResidenza,
                Email = t.Email,
                CodiceFiscale = t.CodiceFiscale,
                Cellulare = t.Cellulare,
                Minorenne = t.Minorenne,
                NomeGenitore = t.NomeGenitore,
                CognomeGenitore = t.CognomeGenitore,
                TerminiAccettati = t.TerminiAccettati,
                Firma = t.Firma,
                DataCreazione = t.DataCreazione,
                PartitaId = t.PartitaId,
                Tessera = t.Tessera,
                NoTesseramento = t.NoTesseramento
            }).ToList();

            ViewBag.SearchNome = searchNome;
            ViewBag.SearchCognome = searchCognome;
            ViewBag.SearchTessera = searchTessera;
            ViewBag.DataDa = dataDa?.ToString("yyyy-MM-dd");
            ViewBag.DataA = dataA?.ToString("yyyy-MM-dd");
            ViewBag.PartitaId = partitaId;
            ViewBag.SoloSenzaPartita = soloSenzaPartita;
            ViewBag.SoloTessereDaAssociare = soloTessereDaAssociare;

            return View(viewModels);
        }

        [HttpGet]
        public async Task<IActionResult> CercaPartitePerAssociazione(DateTime? data)
        {
            if (!data.HasValue)
            {
                return Json(new { success = false, message = "Seleziona una data." });
            }

            var giorno = DateTime.SpecifyKind(data.Value.Date, DateTimeKind.Utc);
            var giornoFine = giorno.AddDays(1);

            var partiteDb = await _dbContext.Partite
                .Where(p => !p.IsDeleted && p.Data >= giorno && p.Data < giornoFine)
                .OrderBy(p => p.OraInizio)
                .ToListAsync();

            var partite = partiteDb
                .Select(p => new
                {
                    id = p.Id,
                    data = p.Data.ToString("dd/MM/yyyy"),
                    ora = p.OraInizio.ToString(@"hh\:mm"),
                    durata = p.Durata,
                    riferimento = p.Riferimento,
                    tipo = p.Tipo,
                    partecipanti = p.NumeroPartecipanti
                })
                .ToList();

            return Json(new { success = true, partite });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssociaTesseratoAPartita(int tesseratoId, int partitaId)
        {
            var tesserato = await _dbContext.Tesseramenti.FirstOrDefaultAsync(t => t.Id == tesseratoId);
            if (tesserato == null)
            {
                return Json(new { success = false, message = "Tesserato non trovato." });
            }

            if (tesserato.PartitaId.HasValue)
            {
                return Json(new { success = false, message = "Questo tesserato risulta già associato a una partita." });
            }

            var partita = await _dbContext.Partite.FirstOrDefaultAsync(p => p.Id == partitaId && !p.IsDeleted);
            if (partita == null)
            {
                return Json(new { success = false, message = "Partita non trovata o eliminata." });
            }

            if (await IsStessoTesseratoGiaNellaPartitaAsync(tesserato, partita.Id))
            {
                return Json(new { success = false, message = "Questo tesserato risulta già inserito nella partita selezionata." });
            }

            tesserato.PartitaId = partita.Id;
            tesserato.NoTesseramento = await IsGiaTesseratoPerAnnoPartitaAsync(tesserato, partita);
            await _dbContext.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"{tesserato.Nome} {tesserato.Cognome} associato alla partita #{partita.Id}."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssegnaTessere(List<int> tesseratiIds)
        {
            if (tesseratiIds == null || !tesseratiIds.Any())
            {
                TempData["Messaggio"] = "Nessun tesserato selezionato.";
                return RedirectToAction("ListaTesseramenti");
            }

            var tesseratiDaAggiornare = await _dbContext.Tesseramenti
                .Where(t => tesseratiIds.Contains(t.Id) && string.IsNullOrEmpty(t.Tessera) && !t.NoTesseramento)
                .OrderBy(t => t.DataCreazione)
                .ToListAsync();

            if (!tesseratiDaAggiornare.Any())
            {
                TempData["Messaggio"] = "I tesserati selezionati hanno già una tessera, sono segnati come No Tesseramento o non sono stati trovati.";
                return RedirectToAction("ListaTesseramenti");
            }

            var oggi = DateTime.UtcNow.Date;
            var tessereAssegnateDbRaw = await _dbContext.Tesseramenti
                .Where(t => !string.IsNullOrEmpty(t.Tessera))
                .Select(t => t.Tessera)
                .ToListAsync();

            var tessereAssegnateDb = tessereAssegnateDbRaw
                .Where(t => long.TryParse(t, out _))
                .Select(long.Parse)
                .ToHashSet();

            var tessereDisponibili = await _dbContext.RangeTessereAcsi
                .Where(r => !r.Assegnata)
                .Where(r => !r.DataValidita.HasValue || r.DataValidita.Value.Date >= oggi)
                .OrderBy(r => r.NumeroDa)
                .ToListAsync();

            tessereDisponibili = tessereDisponibili
                .Where(t => !tessereAssegnateDb.Contains(t.NumeroDa))
                .ToList();

            if (tessereDisponibili.Count < tesseratiDaAggiornare.Count)
            {
                TempData["Messaggio"] = $"Tessere disponibili insufficienti. Selezionati: {tesseratiDaAggiornare.Count}, Disponibili: {tessereDisponibili.Count}.";
                return RedirectToAction("ListaTesseramenti");
            }

            int tessereAssegnateCount = 0;
            for (int i = 0; i < tesseratiDaAggiornare.Count; i++)
            {
                var tesseraDisponibile = tessereDisponibili[i];
                tesseratiDaAggiornare[i].Tessera = tesseraDisponibile.NumeroDa.ToString();
                tesseraDisponibile.Assegnata = true;
                tessereAssegnateCount++;
            }

            await _dbContext.SaveChangesAsync();

            TempData["Messaggio"] = $"{tessereAssegnateCount} tessere assegnate con successo.";
            return RedirectToAction("ListaTesseramenti");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DissociaTessera(int tesseratoId)
        {
            var tesserato = await _dbContext.Tesseramenti.FirstOrDefaultAsync(t => t.Id == tesseratoId);

            if (tesserato == null)
                return Json(new { success = false, message = "Tesserato non trovato." });

            if (string.IsNullOrEmpty(tesserato.Tessera))
                return Json(new { success = false, message = "Il tesserato non ha una tessera assegnata." });

            var tesseraDaDissociare = tesserato.Tessera;
            tesserato.Tessera = null;

            if (long.TryParse(tesseraDaDissociare, out var numeroTessera))
            {
                var rangeTessera = await _dbContext.RangeTessereAcsi
                    .FirstOrDefaultAsync(r => r.NumeroDa == numeroTessera && r.NumeroA == numeroTessera);

                if (rangeTessera != null)
                    rangeTessera.Assegnata = false;
            }

            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        // ### NUOVA AZIONE PER LA DISSOCIAZIONE MASSIVA ###
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DissociaTessereSelezionate(List<int> tesseratiIds)
        {
            if (tesseratiIds == null || !tesseratiIds.Any())
            {
                TempData["Messaggio"] = "Nessun tesserato selezionato per la dissociazione.";
                return RedirectToAction("ListaTesseramenti");
            }

            var tesseratiDaDissociare = await _dbContext.Tesseramenti
                .Where(t => tesseratiIds.Contains(t.Id) && !string.IsNullOrEmpty(t.Tessera))
                .ToListAsync();

            if (!tesseratiDaDissociare.Any())
            {
                TempData["Messaggio"] = "I tesserati selezionati non hanno tessere da dissociare o non sono stati trovati.";
                return RedirectToAction("ListaTesseramenti");
            }

            var numeriTessereDaDissociare = tesseratiDaDissociare
                .Select(t => t.Tessera)
                .Where(t => long.TryParse(t, out _))
                .Select(long.Parse)
                .ToList();

            var rangeTessereDaLiberare = await _dbContext.RangeTessereAcsi
                .Where(r => numeriTessereDaDissociare.Contains(r.NumeroDa) && r.NumeroDa == r.NumeroA)
                .ToListAsync();

            foreach (var rangeTessera in rangeTessereDaLiberare)
            {
                rangeTessera.Assegnata = false;
            }

            foreach (var tesserato in tesseratiDaDissociare)
            {
                tesserato.Tessera = null;
            }

            await _dbContext.SaveChangesAsync();

            TempData["Messaggio"] = $"{tesseratiDaDissociare.Count} tessere dissociate con successo.";
            return RedirectToAction("ListaTesseramenti");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportAcsiOds(string searchNome, string searchCognome, string searchTessera, DateTime? dataDa, DateTime? dataA, int? partitaId, bool soloSenzaPartita = false, bool soloTessereDaAssociare = false)
        {
            try
            {
                var tesseramenti = await BuildListaTesseramentiQuery(searchNome, searchCognome, searchTessera, dataDa, dataA, partitaId, soloSenzaPartita, soloTessereDaAssociare)
                    .OrderByDescending(t => t.Partita != null ? t.Partita.Data : t.DataCreazione)
                    .ThenByDescending(t => t.PartitaId)
                    .ThenByDescending(t => t.Id)
                    .ToListAsync();

                var italiani = tesseramenti.Where(t => !t.NoTesseramento && !t.NatoEstero).ToList();
                var esteri = tesseramenti.Where(t => !t.NoTesseramento && t.NatoEstero).ToList();
                var archiveBytes = _acsiOdsExportService.CreateArchive(italiani, esteri);

                var fileName = $"Tesseramenti_ACSI_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
                return File(archiveBytes, "application/zip", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la generazione export ACSI ODS.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Errore durante la generazione dell'export ACSI. Verifica i template ODS e riprova.");
            }
        }

        private IQueryable<Tesseramento> BuildListaTesseramentiQuery(string? searchNome, string? searchCognome, string? searchTessera, DateTime? dataDa, DateTime? dataA, int? partitaId, bool soloSenzaPartita = false, bool soloTessereDaAssociare = false)
        {
            var query = _dbContext.Tesseramenti
                .Include(t => t.Partita)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchNome))
                query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{searchNome.Trim()}%"));

            if (!string.IsNullOrWhiteSpace(searchCognome))
                query = query.Where(t => EF.Functions.ILike(t.Cognome, $"%{searchCognome.Trim()}%"));

            if (!string.IsNullOrWhiteSpace(searchTessera))
                query = query.Where(t => t.Tessera != null && EF.Functions.ILike(t.Tessera, $"%{searchTessera.Trim()}%"));

            if (dataDa.HasValue)
            {
                var dataDaInizio = DateTime.SpecifyKind(dataDa.Value.Date, DateTimeKind.Utc);
                query = query.Where(t => t.Partita != null && t.Partita.Data >= dataDaInizio);
            }

            if (dataA.HasValue)
            {
                var dataAEnd = DateTime.SpecifyKind(dataA.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(t => t.Partita != null && t.Partita.Data <= dataAEnd);
            }

            if (partitaId.HasValue)
                query = query.Where(t => t.PartitaId == partitaId.Value);

            if (soloSenzaPartita)
                query = query.Where(t => t.PartitaId == null);

            if (soloTessereDaAssociare)
                query = ApplicaFiltroTessereDaAssociare(query);

            return query;
        }

        private static IQueryable<Tesseramento> ApplicaFiltroTessereDaAssociare(IQueryable<Tesseramento> query)
        {
            return query.Where(t =>
                string.IsNullOrEmpty(t.Tessera) &&
                !t.NoTesseramento &&
                t.Partita != null &&
                !t.Partita.IsDeleted &&
                t.Partita.Data >= DataAvvioFabbisognoTessereUtc);
        }

        private async Task<bool> IsGiaTesseratoPerAnnoPartitaAsync(TesseramentoViewModel model)
        {
            if (!model.PartitaId.HasValue)
                return false;

            var partita = await _dbContext.Partite
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == model.PartitaId.Value && !p.IsDeleted);

            if (partita == null)
                return false;

            var tesseramento = model.ToEntity(string.Empty);
            return await IsGiaTesseratoPerAnnoPartitaAsync(tesseramento, partita);
        }

        private async Task<string?> NormalizeComuneResidenzaAsync(string? comuneResidenza, string? nazioneResidenza)
        {
            if (string.IsNullOrWhiteSpace(comuneResidenza))
                return comuneResidenza;

            var trimmed = comuneResidenza.Trim();
            if (Regex.IsMatch(trimmed, @"\([A-Z]{2}\)\s*$", RegexOptions.IgnoreCase))
                return Regex.Replace(trimmed, @"\(([a-z]{2})\)\s*$", match => $"({match.Groups[1].Value.ToUpperInvariant()})", RegexOptions.IgnoreCase);

            var nazione = string.IsNullOrWhiteSpace(nazioneResidenza) ? "Italia" : nazioneResidenza.Trim();
            if (!string.Equals(nazione, "Italia", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            var comunePulito = Regex.Replace(trimmed, @"\s*\([A-Za-z]{2}\)\s*$", string.Empty).Trim();
            var candidati = await _dbContext.ComuniCatastali
                .AsNoTracking()
                .Where(c => c.Attivo && c.Nome.ToLower() == comunePulito.ToLower())
                .Select(c => new { c.Nome, c.Provincia })
                .ToListAsync();

            return candidati.Count == 1 && !string.IsNullOrWhiteSpace(candidati[0].Provincia)
                ? $"{candidati[0].Nome} ({candidati[0].Provincia})"
                : trimmed;
        }

        private async Task<ComuneCatastale?> ResolveComuneCatastaleAsync(string? comune)
        {
            if (string.IsNullOrWhiteSpace(comune))
                return null;

            var comunePulito = Regex.Replace(comune.Trim(), @"\s*\([A-Za-z]{2}\)\s*$", string.Empty).Trim();
            var candidati = await _dbContext.ComuniCatastali
                .AsNoTracking()
                .Where(c => c.Attivo && c.Nome.ToLower() == comunePulito.ToLower())
                .ToListAsync();

            return candidati.Count == 1 ? candidati[0] : null;
        }

        private static bool IsItalia(string? nazione)
        {
            return string.IsNullOrWhiteSpace(nazione)
                || string.Equals(nazione.Trim(), "Italia", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nazione.Trim(), "Italy", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> IsStessoTesseratoGiaNellaPartitaAsync(TesseramentoViewModel model)
        {
            if (!model.PartitaId.HasValue || !model.DataNascita.HasValue)
                return false;

            var tesseramento = model.ToEntity(string.Empty);
            return await IsStessoTesseratoGiaNellaPartitaAsync(tesseramento, model.PartitaId.Value);
        }

        private async Task<bool> IsStessoTesseratoGiaNellaPartitaAsync(Tesseramento tesseramento, int partitaId)
        {
            var codiceFiscale = NormalizeCode(tesseramento.CodiceFiscale);
            var nome = NormalizePersonText(tesseramento.Nome);
            var cognome = NormalizePersonText(tesseramento.Cognome);
            var dataNascita = tesseramento.DataNascita.Date;

            var candidati = await _dbContext.Tesseramenti
                .AsNoTracking()
                .Where(t => t.Id != tesseramento.Id && t.PartitaId == partitaId && t.DataNascita.Date == dataNascita)
                .Select(t => new { t.Nome, t.Cognome, t.CodiceFiscale })
                .ToListAsync();

            return candidati.Any(t =>
                (!string.IsNullOrWhiteSpace(codiceFiscale) && NormalizeCode(t.CodiceFiscale) == codiceFiscale) ||
                (NormalizePersonText(t.Nome) == nome && NormalizePersonText(t.Cognome) == cognome));
        }

        private async Task<bool> IsGiaTesseratoPerAnnoPartitaAsync(Tesseramento tesseramento, Partita partita)
        {
            var annoPartita = partita.Data.Year;
            var start = DateTime.SpecifyKind(new DateTime(annoPartita, 1, 1), DateTimeKind.Utc);
            var end = start.AddYears(1);
            var codiceFiscale = NormalizeCode(tesseramento.CodiceFiscale);

            if (!string.IsNullOrWhiteSpace(codiceFiscale))
            {
                var candidatiCf = await _dbContext.Tesseramenti
                    .AsNoTracking()
                    .Include(t => t.Partita)
                    .Where(t => t.Id != tesseramento.Id
                        && !t.NoTesseramento
                        && t.Partita != null
                        && t.Partita.Data >= start
                        && t.Partita.Data < end
                        && t.CodiceFiscale != null)
                    .Select(t => t.CodiceFiscale)
                    .ToListAsync();

                if (candidatiCf.Any(cf => NormalizeCode(cf) == codiceFiscale))
                    return true;
            }

            var nome = NormalizePersonText(tesseramento.Nome);
            var cognome = NormalizePersonText(tesseramento.Cognome);
            var dataNascita = tesseramento.DataNascita.Date;

            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(cognome))
                return false;

            var candidatiAnagrafici = await _dbContext.Tesseramenti
                .AsNoTracking()
                .Include(t => t.Partita)
                .Where(t => t.Id != tesseramento.Id
                    && !t.NoTesseramento
                    && t.Partita != null
                    && t.Partita.Data >= start
                    && t.Partita.Data < end
                    && t.DataNascita.Date == dataNascita)
                .Select(t => new { t.Nome, t.Cognome })
                .ToListAsync();

            return candidatiAnagrafici.Any(t =>
                NormalizePersonText(t.Nome) == nome &&
                NormalizePersonText(t.Cognome) == cognome);
        }

        private static string NormalizeCode(string? value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s+", string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizePersonText(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    builder.Append(c);
            }

            return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ")
                .Trim()
                .ToUpperInvariant();
        }

        private async Task<bool> TesseramentoLiberoAbilitatoAsync()
        {
            var valore = await _dbContext.AppSettings
                .Where(s => s.Key == TesseramentoLiberoEnabledSettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return string.Equals(valore, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
