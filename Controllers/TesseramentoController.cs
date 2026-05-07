using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    public class TesseramentoController : Controller
    {
        private readonly TesseramentoDbContext _dbContext;

        public TesseramentoController(TesseramentoDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IActionResult Index()
        {
            var model = new TesseramentoViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(TesseramentoViewModel model)
        {
            Console.WriteLine($"DEBUG: Minorenne = {model.Minorenne}");

            if (model.Minorenne == "Sì")
            {
                if (string.IsNullOrWhiteSpace(model.NomeGenitore))
                {
                    ModelState.AddModelError("NomeGenitore", "Il nome del genitore è obbligatorio se il tesserato è minorenne.");
                }
                if (string.IsNullOrWhiteSpace(model.CognomeGenitore))
                {
                    ModelState.AddModelError("CognomeGenitore", "Il cognome del genitore è obbligatorio se il tesserato è minorenne.");
                }
            }

            if (ModelState.IsValid)
            {
                Console.WriteLine("DEBUG: Model is valid, proceeding to process data.");

                string firmaFilePath = null;
                byte[]? firmaBytes = null;

                string firmaBase64 = model.Firma;
                if (!string.IsNullOrEmpty(firmaBase64))
                {
                    if (firmaBase64.StartsWith("data:image/png;base64,"))
                    {
                        firmaBase64 = firmaBase64.Substring("data:image/png;base64,".Length);
                    }

                    try
                    {
                        firmaBytes = Convert.FromBase64String(firmaBase64);
                        var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        var firmaDirectoryPath = Path.Combine(webRootPath, "Firme");

                        if (!Directory.Exists(firmaDirectoryPath))
                        {
                            Directory.CreateDirectory(firmaDirectoryPath);
                        }

                        var fileName = "firma_" + Guid.NewGuid().ToString() + ".png";
                        firmaFilePath = "/Firme/" + fileName;
                        var fullFilePath = Path.Combine(firmaDirectoryPath, fileName);

                        await System.IO.File.WriteAllBytesAsync(fullFilePath, firmaBytes);
                        Console.WriteLine($"DEBUG: Firma salvata in: {fullFilePath}");
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine($"ERRORE: Formato Base64 della firma non valido: {ex.Message}");
                        ModelState.AddModelError("Firma", "Il formato della firma non è valido. Riprova.");
                        return View(model);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERRORE: Impossibile salvare la firma: {ex.Message}");
                        ModelState.AddModelError("Firma", "Si è verificato un problema durante il salvataggio della firma. Riprova.");
                        return View(model);
                    }
                }

                try
                {
                    var tesseramentoEntity = model.ToEntity(firmaFilePath);

                    _dbContext.Tesseramenti.Add(tesseramentoEntity);
                    await _dbContext.SaveChangesAsync();

                    Console.WriteLine("DEBUG: Tesseramento salvato nel database.");

                    TempData["NomeUtente"] = model.Nome + " " + model.Cognome;
                    return RedirectToAction("Successo");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERRORE: Problema durante il salvataggio nel database: {ex.Message}");
                    ModelState.AddModelError("", "Si è verificato un errore durante il salvataggio dei dati nel database. Dettagli: " + ex.Message);
                    return View(model);
                }
            }
            else
            {
                Console.WriteLine("DEBUG: Model is invalid!");
                foreach (var state in ModelState)
                {
                    if (state.Value.Errors.Any())
                    {
                        Console.WriteLine($"Campo: {state.Key}, Errori: {string.Join("; ", state.Value.Errors.Select(e => e.ErrorMessage))}");
                    }
                }
                return View(model);
            }
        }

        public IActionResult Successo()
        {
            ViewBag.NomeUtente = TempData["NomeUtente"] as string;
            Console.WriteLine("DEBUG: Redirected to Success page.");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ListaTesseramenti()
        {
            var tesseramentiEntities = await _dbContext.Tesseramenti.ToListAsync();

            var tesseramentiViewModels = tesseramentiEntities.Select(t => new TesseramentoViewModel
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
                DataCreazione = t.DataCreazione
            }).ToList();

            return View(tesseramentiViewModels);
        }
    }
}

// Estensione aggiunta a TesseramentoViewModel
namespace Full_Metal_Paintball_Carmagnola.Models
{
    public partial class TesseramentoViewModel
    {
        public Tesseramento ToEntity(string firmaPath)
        {
            return new Tesseramento
            {
                Nome = this.Nome,
                Cognome = this.Cognome,
                DataNascita = this.DataNascita,
                Genere = this.Genere,
                ComuneNascita = this.ComuneNascita,
                ComuneResidenza = this.ComuneResidenza,
                Email = this.Email,
                CodiceFiscale = this.CodiceFiscale,
                Minorenne = this.Minorenne,
                NomeGenitore = this.NomeGenitore,
                CognomeGenitore = this.CognomeGenitore,
                TerminiAccettati = this.TerminiAccettati,
                Firma = firmaPath,
                DataCreazione = this.DataCreazione
            };
        }
    }
}
