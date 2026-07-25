using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Authorization;

/// <summary>
/// Returns actionable RFC 7807 feedback for authenticated API callers that fail
/// authorization. The default ASP.NET Core handler emits an empty 403 response,
/// which prevents the client from explaining which permission is missing.
/// </summary>
public sealed class StructuredAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
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

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
