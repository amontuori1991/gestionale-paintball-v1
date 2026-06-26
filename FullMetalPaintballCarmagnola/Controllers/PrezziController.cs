using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Prezzi")]
    public class PrezziController : Controller
    {
        private readonly PricingCatalogService _pricingCatalogService;
        private readonly TesseramentoDbContext _dbContext;

        public PrezziController(
            PricingCatalogService pricingCatalogService,
            TesseramentoDbContext dbContext)
        {
            _pricingCatalogService = pricingCatalogService;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            var previousListinoId = catalog.GetLegacyListino().Id;
            var today = DateTime.UtcNow.Date;
            var showPreviousListino = previousListinoId != catalog.CurrentListinoId
                && await _dbContext.Partite.AnyAsync(p =>
                    !p.IsDeleted
                    && p.Data >= today
                    && p.Listino == previousListinoId);

            ViewBag.CanManagePricing = User.IsInRole("Admin");
            ViewBag.ShowPreviousListino = showPreviousListino;
            return View(catalog);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            return View(catalog);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage(PricingCatalog model)
        {
            await _pricingCatalogService.SaveCatalogAsync(model);
            TempData["PricingSaved"] = "Listini aggiornati correttamente.";
            return RedirectToAction(nameof(Manage));
        }
    }
}
