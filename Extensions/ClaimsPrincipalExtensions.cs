using System.Security.Claims;

namespace TestingAppWeb.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal principal)
        {
            var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var userId) ? userId : null;
        }

        public static int GetRequiredUserId(this ClaimsPrincipal principal)
        {
            return principal.GetUserId()
                ?? throw new InvalidOperationException("Authenticated user has no valid name identifier claim.");
        }
    }
}
