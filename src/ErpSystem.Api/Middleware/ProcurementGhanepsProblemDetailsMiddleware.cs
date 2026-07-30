using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Middleware;

public sealed class ProcurementGhanepsProblemDetailsMiddleware
{
    private static readonly PathString RoutePrefix =
        new("/api/procurement/ghaneps-exchanges");

    private readonly RequestDelegate _next;

    public ProcurementGhanepsProblemDetailsMiddleware(RequestDelegate next) =>
        _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (!context.Request.Path.StartsWithSegments(
                RoutePrefix,
                StringComparison.OrdinalIgnoreCase) ||
            context.Response.HasStarted ||
            !IsEmpty(context.Response))
            return;

        var (code, title, detail) = context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => (
                "AUTHENTICATION_REQUIRED",
                "Authentication required",
                "Authentication is required to access the GHANEPS exchange resource."),
            StatusCodes.Status405MethodNotAllowed => (
                "METHOD_NOT_ALLOWED",
                "Method not allowed",
                "The requested HTTP method is not supported for this GHANEPS exchange resource."),
            _ => default
        };
        if (string.IsNullOrWhiteSpace(code))
            return;

        var supplied = context.Request.Headers["X-Correlation-ID"]
            .FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(supplied)
            ? string.IsNullOrWhiteSpace(context.TraceIdentifier)
                ? Guid.NewGuid().ToString("N")
                : context.TraceIdentifier
            : supplied.Trim();
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        context.Response.ContentLength = null;

        var problem = new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = correlationId;

        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private static bool IsEmpty(HttpResponse response) =>
        response.ContentLength is null or 0 &&
        string.IsNullOrWhiteSpace(response.ContentType);
}
