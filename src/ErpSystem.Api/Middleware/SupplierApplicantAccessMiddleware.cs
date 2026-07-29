namespace ErpSystem.Api.Middleware;

public sealed class SupplierApplicantAccessMiddleware : IMiddleware
{
    private const string ApplicantApiPrefix =
        "/api/procurement/supplier-applicant-access";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var sessionClaim = context.User.FindFirst("supplier_applicant_session");
        if (context.User.Identity?.IsAuthenticated != true || sessionClaim is null)
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments(
                ApplicantApiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tdc.gov.gh/problems/supplier-applicant-scope",
            title = "Restricted supplier applicant session",
            status = StatusCodes.Status403Forbidden,
            code = "SUPPLIER_APPLICANT_SCOPE_DENIED",
            detail =
                "This token-authenticated session can access only application, document, payment, and status functions."
        });
    }
}

public sealed class TemporaryPasswordChangeMiddleware : IMiddleware
{
    private static readonly string[] AllowedPaths =
    [
        "/api/user/change-password",
        "/api/auth/logout"
    ];

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var required = string.Equals(
            context.User.FindFirst("password_change_required")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (context.User.Identity?.IsAuthenticated != true || !required)
        {
            await next(context);
            return;
        }

        if (AllowedPaths.Any(path => context.Request.Path.StartsWithSegments(
                path, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tdc.gov.gh/problems/password-change-required",
            title = "Password change required",
            status = StatusCodes.Status403Forbidden,
            code = "TEMPORARY_PASSWORD_CHANGE_REQUIRED",
            detail =
                "Replace the one-time temporary password before using supplier functions."
        });
    }
}
