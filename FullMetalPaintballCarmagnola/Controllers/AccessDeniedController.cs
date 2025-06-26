using Microsoft.AspNetCore.Mvc;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    public class AccessDeniedController : Controller
    {
        public IActionResult Custom()
        {
            return View();
        }
    }
}
