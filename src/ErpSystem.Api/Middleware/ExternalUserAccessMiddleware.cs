using System.Security.Claims;
using ErpSystem.Shared;

namespace ErpSystem.Api.Middleware;

/// <summary>
/// Enforces a hard boundary between external (Local auth) portal users and internal ERP APIs.
/// External users are only allowed to access a curated list of API route prefixes.
/// </summary>
public sealed class ExternalUserAccessMiddleware : IMiddleware
{
    private static readonly string[] AllowedPathPrefixes =
    {
        // Auth flows (login/register/otp/refresh/select-tenant, etc.)
        "/api/auth",

        // Tenant info used by login/selection flows
        "/api/tenant",

        // Existing supplier/external portal features live under procurement
        "/api/procurement",

        // Unified notifications endpoints + SignalR hub
        "/api/notifications",
        "/api/hubs",

        // File upload used by portals (attachments, registrations, etc.)
        "/api/fileupload",

        // Support portal (external)
        "/api/ehc/external",

        // Project portal endpoints for linked external parties
        "/api/projects/external",

        // Profile self-service endpoints
        "/api/user/profile",
        "/api/user/change-password",

        // Health checks (non-sensitive)
        "/health",

        // Swagger (keep enabled for now; can be disabled in production)
        "/swagger"
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var authProvider = user.FindFirstValue("auth_provider");
        var isExternalUser = string.Equals(authProvider, AuthenticationProvider.Local.ToString(), StringComparison.OrdinalIgnoreCase);
        if (!isExternalUser)
        {
            await next(context);
            return;
        }

        // Allow admin-privileged users through even if their auth provider is Local
        if (user.IsInRole("SuperAdmin") || user.IsInRole("TenantAdmin"))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (AllowedPathPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "External users are not permitted to access this resource."
        });
    }
}
