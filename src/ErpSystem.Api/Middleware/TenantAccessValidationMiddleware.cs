using System.Security.Claims;
using ErpSystem.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace ErpSystem.Api.Middleware;

/// <summary>
/// Revalidates mapped tenant access for authenticated internal ERP requests.
/// Revocation and expiry therefore take effect before an issued JWT expires.
/// Users with no mapping history retain the legacy primary-tenant behavior until
/// an administrator creates their first authoritative mapping.
/// </summary>
public sealed class TenantAccessValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantAccessValidationMiddleware> _logger;

    public TenantAccessValidationMiddleware(
        RequestDelegate next,
        ILogger<TenantAccessValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserTenantService userTenantService)
    {
        var endpoint = context.GetEndpoint();
        var requiresAuthorization = endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null &&
                                    endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;

        if (requiresAuthorization &&
            context.User.Identity?.IsAuthenticated == true &&
            !IsNonInternalPrincipal(context.User) &&
            !IsTenantRecoveryRoute(context.Request.Path) &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            TryGetTenantId(context.User, out var tenantId) &&
            !await userTenantService.HasActiveAccessAsync(userId, tenantId))
        {
            var mappingHistory = await userTenantService.GetAllUserTenantsAsync(userId);
            if (mappingHistory.Any())
            {
                _logger.LogWarning(
                    "Blocked request for user {UserId} because tenant access {TenantId} is revoked, expired, or missing. Path={Path}",
                    userId,
                    tenantId,
                    context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tdc.gov.gh/problems/tenant-access-denied",
                    title = "Tenant access denied",
                    status = StatusCodes.Status403Forbidden,
                    detail = "Your access to this tenant has expired or been removed. Select another tenant or contact an administrator.",
                    code = "TENANT_ACCESS_DENIED",
                    traceId = context.TraceIdentifier
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }

    private static bool TryGetTenantId(ClaimsPrincipal principal, out Guid tenantId)
    {
        var rawTenantId = principal.FindFirstValue("tenant_id") ??
                          principal.FindFirstValue("TenantId") ??
                          principal.FindFirstValue("tenantId");
        return Guid.TryParse(rawTenantId, out tenantId) && tenantId != Guid.Empty;
    }

    private static bool IsTenantRecoveryRoute(PathString path)
        => path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase) ||
           path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase) ||
           path.Equals("/api/auth/select-tenant", StringComparison.OrdinalIgnoreCase);

    private static bool IsNonInternalPrincipal(ClaimsPrincipal principal)
        => principal.HasClaim(claim =>
               claim.Type == "supplier_applicant_session" ||
               claim.Type == "candidate_portal_session" ||
               claim.Type == "consultant_client_session") ||
           string.Equals(
               principal.FindFirstValue("auth_provider"),
               "ApplicantToken",
               StringComparison.OrdinalIgnoreCase);
}
