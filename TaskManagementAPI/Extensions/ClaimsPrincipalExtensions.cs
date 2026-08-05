using System.Security.Claims;

namespace TaskManagementAPI.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
                ?? user.FindFirst("sub");

            if(userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }

            return userId;

        }
    }
}
