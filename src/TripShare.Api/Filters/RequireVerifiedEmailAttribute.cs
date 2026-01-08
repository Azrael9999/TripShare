using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TripShare.Api.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireVerifiedEmailAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
            return;

        var ev = user.Claims.FirstOrDefault(c => c.Type == "ev")?.Value;
        var pv = user.Claims.FirstOrDefault(c => c.Type == "pv")?.Value;
        var emailVerified = string.Equals(ev, "true", StringComparison.OrdinalIgnoreCase);
        var phoneVerified = string.Equals(pv, "true", StringComparison.OrdinalIgnoreCase);
        if (!emailVerified || !phoneVerified)
        {
            context.Result = new ObjectResult(new
            {
                error = "contact_not_verified",
                message = "Email and phone number must be verified to perform this action."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
