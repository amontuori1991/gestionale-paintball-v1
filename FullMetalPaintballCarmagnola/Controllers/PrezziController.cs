using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Prezzi")]
    public class PrezziController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
