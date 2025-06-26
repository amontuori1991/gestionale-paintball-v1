using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    public class DocumentiController : Controller
    {
        private readonly TesseramentoDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DocumentiController(TesseramentoDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(string filtroTipoDocumento, DateTime? filtroDataDocumento, int? filtroFornitoreId, string filtroNote)
        {
            var query = _context.Documenti.AsQueryable();

            if (!string.IsNullOrEmpty(filtroTipoDocumento))
                query = query.Where(d => d.TipoDocumento == filtroTipoDocumento);
            if (filtroDataDocumento.HasValue)
                query = query.Where(d => d.DataDocumento.Date == filtroDataDocumento.Value.Date);
            if (filtroFornitoreId.HasValue)
                query = query.Where(d => d.FornitoreId == filtroFornitoreId.Value);
            if (!string.IsNullOrEmpty(filtroNote))
                query = query.Where(d => d.Note.Contains(filtroNote));

            var documenti = await query.Include(d => d.Fornitore).OrderByDescending(d => d.DataCaricamento).ToListAsync();
            var fornitori = await _context.Fornitori.OrderBy(f => f.Nome).ToListAsync();

            var model = new DocumentiViewModel
            {
                Documenti = documenti.Select(d => new DocumentoViewModel
                {
                    Id = d.Id,
                    PdfFileName = d.PdfFileName,
                    DataCaricamento = d.DataCaricamento,
                    TipoDocumento = d.TipoDocumento,
                    DataDocumento = d.DataDocumento,
                    NumeroDocumento = d.NumeroDocumento,
                    FornitoreNome = d.Fornitore?.Nome ?? "",
                    FornitoreId = d.FornitoreId,
                    Importo = d.Importo,
                    Note = d.Note
                }).ToList(),
                Fornitori = fornitori,
                FiltroTipoDocumento = filtroTipoDocumento,
                FiltroDataDocumento = filtroDataDocumento,
                FiltroFornitoreId = filtroFornitoreId,
                FiltroNote = filtroNote
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> InserisciFornitore(string NomeFornitore)
        {
            if (string.IsNullOrWhiteSpace(NomeFornitore))
            {
                TempData["ErroreFornitore"] = "Il nome del fornitore non può essere vuoto.";
                return RedirectToAction("Index");
            }

            var nuovoFornitore = new DocumentoFornitore { Nome = NomeFornitore.Trim() };
            _context.Fornitori.Add(nuovoFornitore);
            await _context.SaveChangesAsync();

            TempData["SuccessoFornitore"] = "Fornitore inserito con successo!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UploadPdf(Microsoft.AspNetCore.Http.IFormFile PdfFile, int FornitoreId, DateTime DataDocumento, string NumeroDocumento, decimal Importo, string TipoDocumento, string Note)
        {
            if (PdfFile == null || PdfFile.Length == 0)
            {
                TempData["Errore"] = "Devi selezionare un file PDF.";
                return RedirectToAction("Index");
            }

            var allowedExtensions = new[] { ".pdf" };
            var extension = Path.GetExtension(PdfFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Errore"] = "Sono consentiti solo file PDF.";
                return RedirectToAction("Index");
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await PdfFile.CopyToAsync(fileStream);
            }

            var documento = new Documento
            {
                PdfFileName = uniqueFileName,
                DataCaricamento = DateTime.UtcNow,
                TipoDocumento = TipoDocumento,
                DataDocumento = DateTime.SpecifyKind(DataDocumento, DateTimeKind.Utc),
                NumeroDocumento = NumeroDocumento,
                FornitoreId = FornitoreId,
                Importo = Importo,
                Note = Note
            };


            _context.Documenti.Add(documento);
            await _context.SaveChangesAsync();

            TempData["Successo"] = "Documento caricato con successo!";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var documento = await _context.Documenti.FindAsync(id);
            if (documento == null)
                return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, "uploads", documento.PdfFileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return PhysicalFile(filePath, "application/pdf", documento.PdfFileName);
        }

        [HttpGet]
        public async Task<IActionResult> ModificaDocumento(int id)
        {
            var documento = await _context.Documenti.FindAsync(id);
            if (documento == null)
                return NotFound();

            var fornitori = await _context.Fornitori.OrderBy(f => f.Nome).ToListAsync();

            var model = new DocumentoViewModel
            {
                Id = documento.Id,
                PdfFileName = documento.PdfFileName,
                DataCaricamento = documento.DataCaricamento,
                TipoDocumento = documento.TipoDocumento,
                DataDocumento = documento.DataDocumento,
                NumeroDocumento = documento.NumeroDocumento,
                FornitoreId = documento.FornitoreId,
                FornitoreNome = documento.Fornitore?.Nome,
                Importo = documento.Importo,
                Note = documento.Note
            };

            ViewBag.Fornitori = fornitori;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModificaDocumento(DocumentoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // Per debug: salva in TempData e torna la view
                TempData["ModelErrors"] = string.Join("; ", errors);
                ViewBag.Fornitori = await _context.Fornitori.OrderBy(f => f.Nome).ToListAsync();
                return View("ModificaDocumento", model);
            }

            var documento = await _context.Documenti.FindAsync(model.Id);
            if (documento == null)
                return NotFound();

            documento.TipoDocumento = model.TipoDocumento;
            documento.DataDocumento = model.DataDocumento;
            documento.NumeroDocumento = model.NumeroDocumento;
            documento.FornitoreId = model.FornitoreId;
            documento.Importo = model.Importo;
            documento.Note = model.Note;

            await _context.SaveChangesAsync();

            TempData["Successo"] = "Documento modificato con successo!";
            return RedirectToAction("Index");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminaDocumento(int id)
        {
            var documento = await _context.Documenti.FindAsync(id);
            if (documento == null)
                return NotFound();

            _context.Documenti.Remove(documento);
            await _context.SaveChangesAsync();

            TempData["Successo"] = "Documento eliminato con successo!";
            return RedirectToAction("Index");
        }
    }
}
