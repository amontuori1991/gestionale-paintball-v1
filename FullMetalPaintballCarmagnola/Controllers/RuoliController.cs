using System.Linq;
using System.Threading.Tasks;
using Full_Metal_Paintball_Carmagnola.Helpers;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = "Gestione Utenti")]
public class RuoliController : Controller
{
    private readonly TesseramentoDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public RuoliController(TesseramentoDbContext dbContext, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> GestionePermessi()
    {
        var ruoli = new[] { "Admin", "Staff" };
        var permessi = await _dbContext.RolePermissions.ToListAsync();

        var permessiModel = (from ruolo in ruoli
                             from feature in Features.AllFeatures
                             join p in permessi
                                  on new { RoleName = ruolo, FeatureName = feature }
                                  equals new { p.RoleName, p.FeatureName }
                                  into gj
                             from subp in gj.DefaultIfEmpty()
                             select new RolePermissionViewModel
                             {
                                 RoleName = ruolo,
                                 FeatureName = feature,
                                 IsAllowed = subp?.IsAllowed
                             }).ToList();

        var utenti = _userManager.Users.ToList();
        var utentiModel = new List<UtenteRuoloViewModel>();

        foreach (var utente in utenti)
        {
            var ruoliUtente = await _userManager.GetRolesAsync(utente);
            utentiModel.Add(new UtenteRuoloViewModel
            {
                Id = utente.Id,
                NomeCompleto = $"{utente.FirstName} {utente.LastName}",
                Email = utente.Email,
                Ruolo = ruoliUtente.FirstOrDefault() ?? "Nessuno",
                IsApproved = utente.IsApproved
            });
        }

        var model = new GestionePermessiViewModel
        {
            Permessi = permessiModel,
            Utenti = utentiModel
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SalvaPermessi()
    {
        var ruoli = new[] { "Admin", "Staff" };
        var features = Features.AllFeatures;

        foreach (var ruolo in ruoli)
        {
            foreach (var feature in features)
            {
                var key = $"permessi[{feature}]_{ruolo}";
                var valore = Request.Form[key];

                if (string.IsNullOrEmpty(valore))
                    continue;

                bool? isAllowed = null;
                if (valore == "consenti") isAllowed = true;
                else if (valore == "nega") isAllowed = false;

                var esistente = await _dbContext.RolePermissions
                    .FirstOrDefaultAsync(p => p.RoleName == ruolo && p.FeatureName == feature);

                if (esistente != null)
                {
                    if (isAllowed == null)
                        _dbContext.RolePermissions.Remove(esistente);
                    else
                    {
                        esistente.IsAllowed = isAllowed.Value;
                        _dbContext.RolePermissions.Update(esistente);
                    }
                }
                else if (isAllowed != null)
                {
                    _dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleName = ruolo,
                        FeatureName = feature,
                        IsAllowed = isAllowed.Value
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Permessi aggiornati con successo!"; // <<< AGGIUNTO QUI IL MESSAGGIO DI SUCCESSO
        return RedirectToAction(nameof(GestionePermessi));
    }

    [HttpPost]
    public async Task<IActionResult> AssegnaRuolo(string userId, string ruolo)
    {
        var utente = await _userManager.FindByIdAsync(userId);
        if (utente != null)
        {
            var ruoliAttuali = await _userManager.GetRolesAsync(utente);
            await _userManager.RemoveFromRolesAsync(utente, ruoliAttuali);
            await _userManager.AddToRoleAsync(utente, ruolo);
        }
        TempData["SuccessMessage"] = "Ruolo assegnato con successo!"; // <<< AGGIUNTO ANCHE QUI PER COERENZA
        return RedirectToAction(nameof(GestionePermessi));
    }

    [HttpPost]
    public async Task<IActionResult> CambiaApprovazione(string userId, string isApproved)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest();

        var utente = await _userManager.FindByIdAsync(userId);
        if (utente == null)
            return NotFound();

        utente.IsApproved = isApproved == "on";

        var result = await _userManager.UpdateAsync(utente);
        if (!result.Succeeded)
        {
            // gestione errori
            TempData["ErrorMessage"] = "Errore durante l'aggiornamento dell'approvazione."; // <<< AGGIUNTO MESSAGGIO DI ERRORE
        }
        else
        {
            TempData["SuccessMessage"] = "Stato di approvazione aggiornato con successo!"; // <<< AGGIUNTO MESSAGGIO DI SUCCESSO
        }


        return RedirectToAction(nameof(GestionePermessi));
    }

    [HttpPost]
    public async Task<IActionResult> EliminaUtente(string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        var utente = await _userManager.FindByIdAsync(id);
        if (utente == null)
            return NotFound();

        var result = await _userManager.DeleteAsync(utente);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Utente eliminato con successo!";
        }
        else
        {
            TempData["ErrorMessage"] = "Errore durante l'eliminazione dell'utente.";
        }

        return RedirectToAction(nameof(GestionePermessi));
    }


}