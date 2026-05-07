using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace Full_Metal_Paintball_Carmagnola.Helpers
{
    public static class PermessiHelper
    {
        public static bool PuòVedere(ClaimsPrincipal user, string ruolo)
        {
            return user != null && user.IsInRole(ruolo);
        }

        public static bool ÈAdmin(ClaimsPrincipal user)
        {
            return user != null && user.IsInRole("Admin");
        }

        public static bool ÈStaff(ClaimsPrincipal user)
        {
            return user != null && user.IsInRole("Staff");
        }
    }
}
