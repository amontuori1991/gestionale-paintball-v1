using System.Reflection;
using System.Security.Claims;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

var update = typeof(PartiteController).GetMethod("AggiornaStaff")!;
if (!update.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Roles == "Admin")
    || update.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() == null)
    throw new Exception("Staff updates must require Admin and antiforgery validation.");

foreach (var field in new[] { "Staff1", "Staff2", "Staff3", "Staff4" })
{
    var controller = new PartiteController(null!, null!, null!, null!, null!, null!, null!, null!)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Staff") }, "Test"))
            }
        }
    };
    var partita = new Partita();
    typeof(Partita).GetProperty(field)!.SetValue(partita, "Simone");
    if (await controller.Create(partita) is not ForbidResult)
        throw new Exception("Staff must not assign " + field + " through Create.");
}
Console.WriteLine("PASS: Admin-only update, antiforgery and all four overposted staff assignments rejected before accessing the database.");
