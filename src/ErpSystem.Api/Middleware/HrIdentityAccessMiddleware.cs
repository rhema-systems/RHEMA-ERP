using System.Security.Claims;
using ErpSystem.Core.Interfaces.Identity;

namespace ErpSystem.Api.Middleware;

/// <summary>
/// Revalidates authoritative HR employment state on every authenticated internal bearer request.
/// This closes the interval between an HR change and the scheduled reconciliation run.
/// </summary>
public sealed class HrIdentityAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HrIdentityAccessMiddleware> _logger;

    public HrIdentityAccessMiddleware(RequestDelegate next, ILogger<HrIdentityAccessMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IHrIdentityAccessService accessService)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !IsNonInternalPrincipal(context.User) &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var decision = await accessService.EvaluateAsync(userId, context.RequestAborted);
            if (!decision.IsAllowed)
            {
                _logger.LogWarning(
                    "Blocked request for HR-ineligible identity {UserId}. Code={Code}, Path={Path}",
                    userId,
                    decision.Code,
                    context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tdc.gov.gh/problems/hr-identity-access-denied",
                    title = "Identity access denied",
                    status = StatusCodes.Status401Unauthorized,
                    detail = decision.Message,
                    code = decision.Code,
                    traceId = context.TraceIdentifier
                }, context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }

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
