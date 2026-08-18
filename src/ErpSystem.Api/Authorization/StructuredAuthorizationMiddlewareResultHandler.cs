using System.Security.Claims;
using ErpSystem.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ErpSystem.Api.Authorization;

/// <summary>
/// Returns actionable RFC 7807 feedback for authenticated API callers that fail
/// authorization. The default ASP.NET Core handler emits an empty 403 response,
/// which prevents the client from explaining which permission is missing.
/// </summary>
public sealed class StructuredAuthorizationMiddlewareResultHandler(
    IServiceScopeFactory? scopeFactory = null,
    ILogger<StructuredAuthorizationMiddlewareResultHandler>? logger = null)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallbackHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden || !context.Request.Path.StartsWithSegments("/api"))
        {
            await _fallbackHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var requiredPermissions = authorizeResult.AuthorizationFailure?.FailedRequirements
            .OfType<PermissionRequirement>()
            .SelectMany(requirement => requirement.Permissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission)
            .ToArray() ?? Array.Empty<string>();

        var detail = requiredPermissions.Length == 0
            ? "You are signed in, but your assigned roles do not authorize this action."
            : $"Your assigned roles do not include the required permission: {string.Join(" or ", requiredPermissions)}.";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Permission denied",
            Detail = detail,
            Type = "https://httpstatuses.com/403",
            Instance = context.Request.Path
        };
        problem.Extensions["requiredPermissions"] = requiredPermissions;

        await RecordDeniedAuditAsync(context, requiredPermissions);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private async Task RecordDeniedAuditAsync(HttpContext context, IReadOnlyCollection<string> requiredPermissions)
    {
        if (scopeFactory is null || context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            userId == Guid.Empty)
            return;

        var isQuantitySurvey = requiredPermissions.Any(permission =>
            permission.StartsWith("quantity-survey.", StringComparison.OrdinalIgnoreCase));
        var username = Bounded(
            context.User.Identity.Name ??
            context.User.FindFirstValue(ClaimTypes.Email) ??
            context.User.FindFirstValue("preferred_username") ??
            userId.ToString(),
            255);
        var remoteAddress = Bounded(context.Connection.RemoteIpAddress?.ToString() ?? "Unknown", 45);
        var userAgent = Bounded(context.Request.Headers.UserAgent.ToString(), 500);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            await audit.LogUserActionAsync(
                userId,
                username,
                isQuantitySurvey ? "QuantitySurveyAuthorizationDenied" : "AuthorizationDenied",
                isQuantitySurvey ? "QuantitySurveyApi" : "ApiAuthorization",
                Bounded(context.TraceIdentifier, 100),
                newValues: new
                {
                    Method = context.Request.Method,
                    Path = context.Request.Path.Value,
                    RequiredPermissions = requiredPermissions,
                    CorrelationId = context.TraceIdentifier
                },
                ipAddress: remoteAddress,
                userAgent: string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
        }
        catch (Exception exception)
        {
            logger?.LogError(
                exception,
                "Authorization denial audit failed for request {TraceIdentifier}.",
                context.TraceIdentifier);
        }
    }

    private static string Bounded(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
