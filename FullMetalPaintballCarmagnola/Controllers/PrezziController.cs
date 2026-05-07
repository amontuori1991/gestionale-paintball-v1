using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Prezzi")]
    public class PrezziController : Controller
    {
        private readonly PricingCatalogService _pricingCatalogService;

        public PrezziController(PricingCatalogService pricingCatalogService)
        {
            _pricingCatalogService = pricingCatalogService;
        }

        public async Task<IActionResult> Index()
        {
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            ViewBag.CanManagePricing = User.IsInRole("Admin");
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
