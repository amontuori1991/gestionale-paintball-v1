using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using Full_Metal_Paintball_Carmagnola.Models;

namespace Full_Metal_Paintball_Carmagnola.Authorization
{
    public class FeatureRequirement : IAuthorizationRequirement
    {
        public string FeatureName { get; }
        public FeatureRequirement(string featureName) => FeatureName = featureName;
    }

    public class FeatureAuthorizationHandler : AuthorizationHandler<FeatureRequirement>
    {
        private readonly TesseramentoDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FeatureAuthorizationHandler(TesseramentoDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated)
            {
                context.Fail();
                return;
            }

            var roleClaim = user.FindFirst(ClaimTypes.Role);
            if (roleClaim == null)
            {
                context.Fail();
                return;
            }

            var roleName = roleClaim.Value;

            var permesso = await _dbContext.RolePermissions
                .FirstOrDefaultAsync(p => p.RoleName == roleName && p.FeatureName == requirement.FeatureName);

            if (permesso != null && permesso.IsAllowed)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
    }
}
