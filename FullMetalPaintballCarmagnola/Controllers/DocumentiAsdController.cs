using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    public class DocumentiAsdController : Controller
    {
        private readonly TesseramentoDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DocumentiAsdController(TesseramentoDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(string filtroDescrizione)
        {
            var query = _context.DocumentiAsd.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtroDescrizione))
                query = query.Where(d => d.Descrizione.Contains(filtroDescrizione));

            var documenti = await query.OrderByDescending(d => d.DataCaricamento).ToListAsync();

            var model = new DocumentiAsdViewModelList
            {
                Documenti = documenti.Select(d => new DocumentoAsdViewModel
                {
                    Id = d.Id,
                    OriginalFileName = d.OriginalFileName,
                    StoredFileName = d.StoredFileName,
                    Descrizione = d.Descrizione,
                    DataCaricamento = d.DataCaricamento
                }).ToList(),
                FiltroDescrizione = filtroDescrizione
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new DocumentoAsdViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DocumentoAsdViewModel model)
        {
            if (model.File == null || model.File.Length == 0)
            {
                ModelState.AddModelError("File", "Devi selezionare un file.");
            }
            else
            {
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("File", "Sono consentiti solo file PDF, DOC, DOCX.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploadsAsd");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.File.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.File.CopyToAsync(fileStream);
            }

            var documento = new DocumentoAsd
            {
                OriginalFileName = model.File.FileName,
                StoredFileName = uniqueFileName,
                Descrizione = model.Descrizione,
                DataCaricamento = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc)
            };


            _context.DocumentiAsd.Add(documento);
            await _context.SaveChangesAsync();

            TempData["Successo"] = "Documento caricato con successo!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var documento = await _context.DocumentiAsd.FindAsync(id);
            if (documento == null) return NotFound();

            var model = new DocumentoAsdViewModel
            {
                Id = documento.Id,
                OriginalFileName = documento.OriginalFileName,
                StoredFileName = documento.StoredFileName,
                Descrizione = documento.Descrizione,
                DataCaricamento = documento.DataCaricamento
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DocumentoAsdViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var documento = await _context.DocumentiAsd.FindAsync(model.Id);
            if (documento == null) return NotFound();

            documento.Descrizione = model.Descrizione;
            await _context.SaveChangesAsync();

            TempData["Successo"] = "Documento modificato con successo!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var documento = await _context.DocumentiAsd.FindAsync(id);
            if (documento == null) return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, "uploadsAsd", documento.StoredFileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _context.DocumentiAsd.Remove(documento);
            await _context.SaveChangesAsync();

            TempData["Successo"] = "Documento eliminato con successo!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Download(int id)
        {
            var documento = await _context.DocumentiAsd.FindAsync(id);
            if (documento == null) return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, "uploadsAsd", documento.StoredFileName);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var contentType = "application/octet-stream";
            var ext = Path.GetExtension(documento.StoredFileName).ToLowerInvariant();

            if (ext == ".pdf")
                contentType = "application/pdf";
            else if (ext == ".doc")
                contentType = "application/msword";
            else if (ext == ".docx")
                contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            return PhysicalFile(filePath, contentType, documento.OriginalFileName);
        }
    }
}
