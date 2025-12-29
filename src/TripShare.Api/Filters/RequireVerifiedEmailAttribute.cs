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
        if (!string.Equals(ev, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new
            {
                error = "email_not_verified",
                message = "Email must be verified to perform this action."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
