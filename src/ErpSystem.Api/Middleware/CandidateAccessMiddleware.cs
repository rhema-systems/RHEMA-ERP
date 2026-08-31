using System.Security.Claims;
using ErpSystem.Shared;

namespace ErpSystem.Api.Middleware;

/// <summary>
/// Enforces a hard boundary between self-registered job candidates and the rest of the ERP's
/// APIs. Candidates hold main-scheme JWTs (the PortalBearer candidate portal was retired
/// 2026-08-30), so without this fence every endpoint their role satisfies would be reachable.
///
/// <para>Deliberately a SIBLING of <see cref="ExternalUserAccessMiddleware"/> rather than a
/// widening of it: the ExternalUser allowlist includes <c>/api/procurement</c> wholesale plus
/// the projects/estate/support portals — surfaces for vetted counterparties that an anonymous
/// careers signup must never inherit. A candidate's world is auth, their own profile,
/// notifications, the public job board and the candidate recruitment surface — nothing else.</para>
///
/// <para>⚠ Every prefix added here must first be audited for bare <c>[Authorize]</c> underneath:
/// the moment a prefix is allowlisted, everything under it that lacks a stronger policy is
/// reachable by any member of the public who signed up. The current list is a strict subset of
/// the ExternalUser allowlist plus <c>/api/public</c> (anonymous by design) and
/// <c>/api/candidate</c> (every endpoint gated <c>CandidateOnly</c>).</para>
/// </summary>
public sealed class CandidateAccessMiddleware : IMiddleware
{
    private static readonly string[] AllowedPathPrefixes =
    {
        // Auth flows (login/refresh/otp/change-password, candidate registration)
        "/api/auth",

        // Tenant info used by login/selection flows
        "/api/tenant",

        // Unified notifications endpoints + SignalR hub
        "/api/notifications",
        "/api/hubs",

        // Profile self-service endpoints
        "/api/user/profile",
        "/api/user/change-password",

        // The anonymous public job board (also serves the logged-in careers pages' reads)
        "/api/public",

        // The candidate-facing recruitment surface — every endpoint on it is CandidateOnly
        "/api/candidate",

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

        if (!user.IsInRole(Constants.Roles.Candidate))
        {
            await next(context);
            return;
        }

        // Unlike ExternalUserAccessMiddleware there is no admin bypass: an account that is both
        // Candidate and an admin role is a misconfiguration, and the safe reading is the narrow one.
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
            message = "Candidate accounts are not permitted to access this resource."
        });
    }
}
