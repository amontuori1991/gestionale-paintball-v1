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

        [AllowAnonymous]
        public IActionResult Index(int? partitaId = null)
        {
            var model = new TesseramentoViewModel();
            if (partitaId.HasValue)
                model.PartitaId = partitaId;

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
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
                int annoCorrente = DateTime.UtcNow.Year;

                bool isDuplicato = await _dbContext.Tesseramenti.AnyAsync(t =>
                    t.Nome.ToLower() == model.Nome.ToLower() &&
                    t.Cognome.ToLower() == model.Cognome.ToLower() &&
                    model.DataNascita.HasValue &&
                    t.DataNascita.Date == DateTime.SpecifyKind(model.DataNascita.Value.Date, DateTimeKind.Utc).Date &&
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
                    var entity = model.ToEntity(firmaFilePath);
                    entity.DataCreazione = DateTime.SpecifyKind(entity.DataCreazione, DateTimeKind.Utc);
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

        public async Task<IActionResult> ListaTesseramenti(string searchNome, string searchCognome, DateTime? dataDa, DateTime? dataA, int? partitaId)
        {
            var query = _dbContext.Tesseramenti.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchNome))
                query = query.Where(t => t.Nome.Contains(searchNome));

            if (!string.IsNullOrWhiteSpace(searchCognome))
                query = query.Where(t => t.Cognome.Contains(searchCognome));

            if (dataDa.HasValue)
                query = query.Where(t => t.DataCreazione >= DateTime.SpecifyKind(dataDa.Value.Date, DateTimeKind.Utc));

            if (dataA.HasValue)
            {
                var dataAEnd = DateTime.SpecifyKind(dataA.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(t => t.DataCreazione <= dataAEnd);
            }

            if (partitaId.HasValue)
                query = query.Where(t => t.PartitaId == partitaId.Value);

            var tesseramenti = await query.ToListAsync();

            var viewModels = tesseramenti.Select(t => new TesseramentoViewModel
            {
                Id = t.Id,
                Nome = t.Nome,
                Cognome = t.Cognome,
                DataNascita = t.DataNascita,
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

        [HttpPost]
        public async Task<IActionResult> AssegnaTessere()
        {
            var intervalli = await _dbContext.RangeTessereAcsi.Where(r => r.NumeroDa <= r.NumeroA).ToListAsync();

            var tutteLeTessere = intervalli
                .SelectMany(r => Enumerable.Range((int)r.NumeroDa, (int)(r.NumeroA - r.NumeroDa + 1)))
                .ToList();

            var tessereAssegnate = await _dbContext.Tesseramenti
                .Where(t => !string.IsNullOrEmpty(t.Tessera))
                .Select(t => long.Parse(t.Tessera))
                .ToListAsync();

            var prossimaTessera = tutteLeTessere
                .Where(numero => !tessereAssegnate.Contains(numero))
                .OrderBy(numero => numero)
                .FirstOrDefault();

            if (prossimaTessera == 0)
            {
                TempData["Messaggio"] = "Nessuna tessera disponibile.";
                return RedirectToAction("ListaTesseramenti");
            }

            var tesserato = await _dbContext.Tesseramenti
                .Where(t => string.IsNullOrEmpty(t.Tessera))
                .OrderBy(t => t.DataCreazione)
                .FirstOrDefaultAsync();

            if (tesserato == null)
            {
                TempData["Messaggio"] = "Nessun tesserato in attesa di tessera.";
                return RedirectToAction("ListaTesseramenti");
            }

            tesserato.Tessera = prossimaTessera.ToString();
            await _dbContext.SaveChangesAsync();

            TempData["Messaggio"] = $"Tessera {prossimaTessera} assegnata a {tesserato.Nome} {tesserato.Cognome}.";
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

            tesserato.Tessera = null;
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string searchNome, string searchCognome, DateTime? dataDa, DateTime? dataA)
        {
            var query = _dbContext.Tesseramenti.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchNome))
                query = query.Where(t => t.Nome.Contains(searchNome));

            if (!string.IsNullOrWhiteSpace(searchCognome))
                query = query.Where(t => t.Cognome.Contains(searchCognome));

            if (dataDa.HasValue)
                query = query.Where(t => t.DataCreazione >= DateTime.SpecifyKind(dataDa.Value.Date, DateTimeKind.Utc));

            if (dataA.HasValue)
            {
                var dataAEnd = DateTime.SpecifyKind(dataA.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(t => t.DataCreazione <= dataAEnd);
            }

            var tesseramenti = await query.ToListAsync();

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Tesseramenti");

                worksheet.Range("A4:C4").Merge().Value = "Comitato Provinciale di:";
                worksheet.Cell("A4").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                worksheet.Range("A5:C5").Merge();
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

                var headers = new[]
                {
                    "N.Tessera", "Cognome", "Nome", "Codice Fiscale", "Qualifica", "Email", "Cellulare",
                    "Assicurazione", "Inserire Disciplina CONI 1", "Inserire Disciplina CONI 2", "Inserire Disciplina CONI 3",
                    "Inserire Disciplina ACSI 1", "Inserire Disciplina ACSI 2", "Inserire Disciplina ACSI 3"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(12, i + 1).Value = headers[i];
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

                int row = 13;
                foreach (var t in tesseramenti)
                {
                    worksheet.Cell(row, 1).Value = t.Tessera ?? "";
                    worksheet.Cell(row, 2).Value = t.Cognome;
                    worksheet.Cell(row, 3).Value = t.Nome;
                    worksheet.Cell(row, 4).Value = t.CodiceFiscale;
                    worksheet.Cell(row, 5).Value = "Socio - 2116";
                    worksheet.Cell(row, 6).Value = t.Email;
                    worksheet.Cell(row, 7).Value = "";
                    worksheet.Cell(row, 8).Value = "Base Sport - 102";
                    worksheet.Cell(row, 9).Value = "Attività sportiva ginnastica finalizzata alla salute ed al fitness - BI001";
                    worksheet.Cell(row, 10).Value = "";
                    worksheet.Cell(row, 11).Value = "";
                    worksheet.Cell(row, 12).Value = "PAINTBALL - 514";
                    worksheet.Cell(row, 13).Value = "";
                    worksheet.Cell(row, 14).Value = "";

                    for (int col = 1; col <= 14; col++)
                        worksheet.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thick;

                    row++;
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Tesseramenti_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
    }
}
