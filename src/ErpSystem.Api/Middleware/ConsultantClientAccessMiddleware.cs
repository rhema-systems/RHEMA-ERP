using ErpSystem.Shared;

namespace ErpSystem.Api.Middleware;

/// <summary>
/// Enforces a hard boundary between consultant-client contacts and the rest of the ERP's APIs.
/// Client contacts hold main-scheme JWTs (the bespoke PortalBearer consultant-client portal was
/// retired 2026-08-31 — it was that scheme's last tenant), so without this fence every endpoint
/// their role satisfies would be reachable.
///
/// <para>Deliberately a SIBLING of <see cref="ExternalUserAccessMiddleware"/> and
/// <see cref="CandidateAccessMiddleware"/> rather than a widening of either: the ExternalUser
/// allowlist includes <c>/api/procurement</c> wholesale plus the projects/estate/support portals —
/// surfaces for vetted counterparties that a client's billing contact must never inherit. A
/// client contact's world is auth, their own profile, notifications and the client-portal
/// timesheet surface — nothing else. Accounts are invite-only (HR invites from the client
/// screen); there is no self-registration path for this role.</para>
///
/// <para>⚠ Every prefix added here must first be audited for bare <c>[Authorize]</c> underneath:
/// the moment a prefix is allowlisted, everything under it that lacks a stronger policy is
/// reachable by any invited outsider. The current list is a strict subset of the ExternalUser
/// allowlist plus <c>/api/client-portal</c> (every endpoint gated <c>ConsultantClientOnly</c>).</para>
/// </summary>
public sealed class ConsultantClientAccessMiddleware : IMiddleware
{
    private static readonly string[] AllowedPathPrefixes =
    {
        // Auth flows (login/refresh/change-password, invite setup completion)
        "/api/auth",

        // Tenant info used by login/selection flows
        "/api/tenant",

        // Unified notifications endpoints + SignalR hub
        "/api/notifications",
        "/api/hubs",

        // Profile self-service endpoints
        "/api/user/profile",
        "/api/user/change-password",

        // The consultant-client surface — every endpoint on it is ConsultantClientOnly
        "/api/client-portal",

        // Health checks (non-sensitive)
        "/health",

        // Swagger (kept aligned with ExternalUserAccessMiddleware)
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

        if (!user.IsInRole(Constants.Roles.ConsultantClient))
        {
            await next(context);
            return;
        }

        // Like CandidateAccessMiddleware there is no admin bypass: an account that is both
        // ConsultantClient and an admin role is a misconfiguration, and the safe reading is the
        // narrow one.
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
            message = "Client portal accounts are not permitted to access this resource."
        });
    }
}
