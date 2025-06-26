using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Tesserati")]
    public class TesseramentoController : Controller
    {
        private readonly TesseramentoDbContext _dbContext;
        private readonly IEmailService _emailSender;

        public TesseramentoController(TesseramentoDbContext dbContext, IEmailService emailSender)
        {
            _dbContext = dbContext;
            _emailSender = emailSender;
        }

        public IActionResult Index(int? partitaId = null)
        {
            var model = new TesseramentoViewModel();
            if (partitaId.HasValue)
                model.PartitaId = partitaId;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(TesseramentoViewModel model)
        {
            if (model.Minorenne == "Sì")
            {
                if (string.IsNullOrWhiteSpace(model.NomeGenitore))
                    ModelState.AddModelError("NomeGenitore", "Il nome del genitore è obbligatorio se il tesserato è minorenne.");

                if (string.IsNullOrWhiteSpace(model.CognomeGenitore))
                    ModelState.AddModelError("CognomeGenitore", "Il cognome del genitore è obbligatorio se il tesserato è minorenne.");
            }

            if (ModelState.IsValid)
            {
                int annoCorrente = DateTime.Now.Year;

                // MODIFICATO QUI: Accesso a DataNascita.Value.Date del MODEL.
                // L'entità Tesseramento (t.DataNascita) è DateTime non nullable,
                // quindi non ha bisogno di .Value o .HasValue.
                bool isDuplicato = await _dbContext.Tesseramenti.AnyAsync(t =>
                    t.Nome.ToLower() == model.Nome.ToLower() &&
                    t.Cognome.ToLower() == model.Cognome.ToLower() &&
                    model.DataNascita.HasValue && // <<< AGGIUNTO: Controlla se il MODEL ha un valore
                    t.DataNascita.Date == model.DataNascita.Value.Date && // <<< t.DataNascita è OK, ma model.DataNascita ora vuole .Value
                    t.DataCreazione.Year == annoCorrente);

                if (isDuplicato)
                {
                    TempData["NomeUtente"] = model.Nome + " " + model.Cognome;
                    return RedirectToAction("Duplicato");
                }

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
                    var entity = model.ToEntity(firmaFilePath); // Qui DataNascita.GetValueOrDefault() è già gestito
                    _dbContext.Tesseramenti.Add(entity);
                    await _dbContext.SaveChangesAsync();

                    TempData["NomeUtente"] = model.Nome + " " + model.Cognome;

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

        public IActionResult Successo()
        {
            ViewBag.NomeUtente = TempData["NomeUtente"] as string;
            return View();
        }

        public IActionResult Duplicato()
        {
            ViewBag.NomeUtente = TempData["NomeUtente"] as string;
            return View();
        }

        // ListaTesseramenti con filtro per Nome, Cognome e DataCreazione (da - a)
        public async Task<IActionResult> ListaTesseramenti(string searchNome, string searchCognome, DateTime? dataDa, DateTime? dataA, int? partitaId)
        {
            var query = _dbContext.Tesseramenti.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchNome))
            {
                query = query.Where(t => t.Nome.Contains(searchNome));
            }

            if (!string.IsNullOrWhiteSpace(searchCognome))
            {
                query = query.Where(t => t.Cognome.Contains(searchCognome));
            }

            if (dataDa.HasValue)
            {
                query = query.Where(t => t.DataCreazione >= dataDa.Value.Date);
            }

            if (dataA.HasValue)
            {
                var dataAEnd = dataA.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.DataCreazione <= dataAEnd);
            }

            if (partitaId.HasValue)
            {
                query = query.Where(t => t.PartitaId == partitaId.Value);
            }

            var tesseramenti = await query.ToListAsync();

            // QUi i tesseramenti sono entità DB, quindi t.DataNascita è DateTime non nullable.
            // Il problema è solo la conversione a TesseramentoViewModel se DataNascita è un DateTime? lì.
            var viewModels = tesseramenti.Select(t => new TesseramentoViewModel
            {
                Id = t.Id,
                Nome = t.Nome,
                Cognome = t.Cognome,
                DataNascita = t.DataNascita, // Qui t.DataNascita è DateTime, e TesseramentoViewModel.DataNascita è DateTime?. Va bene.
                Genere = t.Genere,
                ComuneNascita = t.ComuneNascita,
                ComuneResidenza = t.ComuneResidenza,
                Email = t.Email,
                CodiceFiscale = t.CodiceFiscale,
                Minorenne = t.Minorenne,
                NomeGenitore = t.NomeGenitore,
                CognomeGenitore = t.CognomeGenitore,
                TerminiAccettati = t.TerminiAccettati,
                Firma = t.Firma,
                DataCreazione = t.DataCreazione,
                PartitaId = t.PartitaId,
                Tessera = t.Tessera
            }).ToList();

            ViewBag.SearchNome = searchNome;
            ViewBag.SearchCognome = searchCognome;
            ViewBag.DataDa = dataDa?.ToString("yyyy-MM-dd");
            ViewBag.DataA = dataA?.ToString("yyyy-MM-dd");
            ViewBag.PartitaId = partitaId;

            return View(viewModels);
        }

        // Export Excel usando ClosedXML (mantenuto come prima)
        [HttpGet]
        public async Task<IActionResult> ExportExcel(string searchNome, string searchCognome, DateTime? dataDa, DateTime? dataA)
        {
            var query = _dbContext.Tesseramenti.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchNome))
            {
                query = query.Where(t => t.Nome.Contains(searchNome));
            }

            if (!string.IsNullOrWhiteSpace(searchCognome))
            {
                query = query.Where(t => t.Cognome.Contains(searchCognome));
            }

            if (dataDa.HasValue)
            {
                query = query.Where(t => t.DataCreazione >= dataDa.Value.Date);
            }

            if (dataA.HasValue)
            {
                var dataAEnd = dataA.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.DataCreazione <= dataAEnd);
            }

            var tesseramenti = await query.ToListAsync();

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Tesseramenti");

                // Intestazioni e layout simili a prima, ad esempio:
                worksheet.Range("A4:C4").Merge().Value = "Comitato Provinciale di:";
                worksheet.Cell("A4").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                worksheet.Range("A5:C5").Merge();

                // Bordo contenitore A4:C5
                worksheet.Range("A4:C5").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                worksheet.Range("A7:B7").Merge().Value = "Sodalizio";
                worksheet.Cell("A7").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell("C7").Value = "Codice ACSI";
                worksheet.Cell("C8").Value = "107743";

                worksheet.Range("A8:B8").Merge();

                worksheet.Range("A7:B8").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                worksheet.Cell("E4").Value = "Campi obbligatori";
                worksheet.Cell("E4").Style.Fill.BackgroundColor = XLColor.Yellow;

                worksheet.Cell("F4").Value = "Inserire e-mail e/o cellulare";
                worksheet.Cell("F4").Style.Fill.BackgroundColor = XLColor.Orange;

                // Intestazioni colonna da riga 12
                var headers = new[]
                {
                    "N.Tessera", "Cognome", "Nome", "Codice Fiscale", "Qualifica", "Email", "Cellulare",
                    "Assicurazione", "Inserire Disciplina CONI 1", "Inserire Disciplina CONI 2", "Inserire Disciplina CONI 3",
                    "Inserire Disciplina ACSI 1", "Inserire Disciplina ACSI 2", "Inserire Disciplina ACSI 3"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(12, i + 1).Value = headers[i];

                    // Colorazione
                    if (i <= 4 || (i >= 7 && i <= 8) || i == 11)
                        worksheet.Cell(12, i + 1).Style.Fill.BackgroundColor = XLColor.Yellow;
                    else if (i == 5)
                        worksheet.Cell(12, i + 1).Style.Fill.BackgroundColor = XLColor.Orange;
                    else
                        worksheet.Cell(12, i + 1).Style.Fill.BackgroundColor = XLColor.White;

                    worksheet.Cell(12, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(12, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    worksheet.Cell(12, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
                }

                // Riga dati da 13
                int row = 13;
                foreach (var t in tesseramenti)
                {
                    worksheet.Cell(row, 1).Value = t.Tessera ?? "";
                    worksheet.Cell(row, 2).Value = t.Cognome;
                    worksheet.Cell(row, 3).Value = t.Nome;
                    worksheet.Cell(row, 4).Value = t.CodiceFiscale;
                    worksheet.Cell(row, 5).Value = "Socio - 2116";
                    worksheet.Cell(row, 6).Value = t.Email;
                    worksheet.Cell(row, 7).Value = ""; // Cellulare vuoto
                    worksheet.Cell(row, 8).Value = "Base Sport - 102";
                    worksheet.Cell(row, 9).Value = "Attività sportiva ginnastica finalizzata alla salute ed al fitness - BI001";
                    worksheet.Cell(row, 10).Value = ""; // CONI 2 vuoto
                    worksheet.Cell(row, 11).Value = ""; // CONI 3 vuoto
                    worksheet.Cell(row, 12).Value = "PAINTBALL - 514";
                    worksheet.Cell(row, 13).Value = ""; // ACSI 2 vuoto
                    worksheet.Cell(row, 14).Value = ""; // ACSI 3 vuoto

                    for (int col = 1; col <= 14; col++)
                    {
                        worksheet.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
                    }

                    row++;
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Tesseramenti_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        // Nuova azione per assegnare tessere ACSI ai tesserati selezionati
        [HttpPost]
        public async Task<IActionResult> AssegnaTessere([FromForm] List<int> tesseratiIds)
        {
            if (tesseratiIds == null || !tesseratiIds.Any())
            {
                TempData["Error"] = "Nessun tesserato selezionato per l'assegnazione tessere.";
                return RedirectToAction("ListaTesseramenti");
            }

            // Recupera le tessere disponibili non assegnate e singole (NumeroDa == NumeroA)
            var tessereDisponibili = await _dbContext.RangeTessereAcsi
                .Where(t => !t.Assegnata && t.NumeroDa == t.NumeroA)
                .OrderBy(t => t.NumeroDa)
                .ToListAsync();


            if (tessereDisponibili.Count < tesseratiIds.Count)
            {
                TempData["Error"] = $"Non ci sono abbastanza tessere disponibili per assegnare a tutti i tesserati selezionati. Tessere disponibili: {tessereDisponibili.Count}, tesserati selezionati: {tesseratiIds.Count}.";
                return RedirectToAction("ListaTesseramenti");
            }

            int i = 0;
            foreach (var tesseratoId in tesseratiIds)
            {
                var tesserato = await _dbContext.Tesseramenti.FindAsync(tesseratoId);
                if (tesserato == null) continue;

                var tessera = tessereDisponibili[i];
                // Assegna la tessera (usa NumeroDa come stringa)
                tesserato.Tessera = tessera.NumeroDa.ToString();

                // Imposta la tessera come assegnata
                tessera.Assegnata = true;

                i++;
            }

            await _dbContext.SaveChangesAsync();

            TempData["Success"] = $"{i} tessere assegnate correttamente.";
            return RedirectToAction("ListaTesseramenti");


        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DissociaTessera(int tesseratoId)
        {
            var tesserato = await _dbContext.Tesseramenti.FindAsync(tesseratoId);
            if (tesserato == null)
            {
                TempData["Error"] = "Tesserato non trovato.";
                return RedirectToAction("ListaTesseramenti");
            }

            if (string.IsNullOrEmpty(tesserato.Tessera))
            {
                TempData["Error"] = "Il tesserato non ha una tessera assegnata.";
                return RedirectToAction("ListaTesseramenti");
            }

            // Recupera la tessera assegnata (con NumeroDa uguale al valore della tessera)
            if (long.TryParse(tesserato.Tessera, out long tesseraNumero))
            {
                var tesseraRange = await _dbContext.RangeTessereAcsi
                    .FirstOrDefaultAsync(r => r.NumeroDa == tesseraNumero);

                if (tesseraRange != null)
                {
                    tesseraRange.Assegnata = false; // libera la tessera
                }
            }

            // Rimuove l'associazione della tessera dal tesserato
            tesserato.Tessera = null;

            await _dbContext.SaveChangesAsync();

            TempData["Success"] = "Tessera dissociata correttamente.";

            return RedirectToAction("ListaTesseramenti");
        }


    }
}