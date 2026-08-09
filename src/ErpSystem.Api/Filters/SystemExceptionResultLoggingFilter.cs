using System.Security.Claims;
using System.Text.Json;
using ErpSystem.Api.Middleware;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Persists RFC 7807 failures that are returned by controllers or by the
/// automatic ApiController model-state filter. These failures do not throw,
/// so the global exception middleware cannot observe them.
/// </summary>
public sealed class SystemExceptionResultLoggingFilter : IAsyncResultFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemExceptionResultLoggingFilter> _logger;

    public SystemExceptionResultLoggingFilter(
        IServiceScopeFactory scopeFactory,
        ILogger<SystemExceptionResultLoggingFilter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (TryGetProblem(context.Result, out var problem, out var statusCode) &&
            statusCode >= StatusCodes.Status400BadRequest &&
            context.HttpContext.Request.Path.StartsWithSegments("/api") &&
            !context.HttpContext.Request.Path.StartsWithSegments(
                "/api/admin/system-exception-logs"))
        {
            await TryPersistAsync(context, problem, statusCode);
        }

        await next();
    }

    private async Task TryPersistAsync(
        ResultExecutingContext context,
        ProblemDetails problem,
        int statusCode)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
            var currentUser = scope.ServiceProvider.GetService<ICurrentUserService>();
            if (unitOfWork is null || currentUser is null) return;

            var http = context.HttpContext;
            var tenantId = currentUser.TenantId ?? Guid.Empty;
            var now = DateTime.UtcNow;
            var detail = string.IsNullOrWhiteSpace(problem.Detail)
                ? problem.Title ?? $"HTTP {statusCode} request failure"
                : problem.Detail;
            var exceptionType = problem is ValidationProblemDetails
                ? "HandledApiValidationProblem"
                : "HandledApiProblem";
            var payload = problem is ValidationProblemDetails validation
                ? JsonSerializer.Serialize(new
                {
                    problem.Type,
                    problem.Title,
                    problem.Status,
                    problem.Detail,
                    problem.Instance,
                    validation.Errors,
                    problem.Extensions
                })
                : JsonSerializer.Serialize(new
                {
                    problem.Type,
                    problem.Title,
                    problem.Status,
                    problem.Detail,
                    problem.Instance,
                    problem.Extensions
                });

            var redactedDetail = SensitiveDataRedactor.Redact(detail);
            var redactedPayload = SensitiveDataRedactor.Redact(Truncate(payload, 20000));
            var requestPath = http.Request.Path.Value;
            var fingerprint = ExceptionFingerprint.Compute(
                exceptionType,
                redactedDetail,
                requestPath);
            var userId = Guid.TryParse(
                http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (Guid?)null;
            var level = statusCode >= StatusCodes.Status500InternalServerError
                ? "Critical"
                : "Warning";
            var repository = unitOfWork.Repository<SystemExceptionLog>();
            var existing = await repository.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.Fingerprint == fingerprint);

            if (existing is not null && !existing.IsDeleted)
            {
                existing.OccurrenceCount += 1;
                existing.LastOccurredAt = now;
                existing.Level = level;
                existing.Logger = Truncate(context.ActionDescriptor.DisplayName, 200);
                existing.ShortMessage = Truncate(redactedDetail, 1000);
                existing.FullMessage = redactedPayload;
                existing.ExceptionType = exceptionType;
                existing.StackTrace = null;
                existing.TraceId = http.TraceIdentifier;
                existing.RequestMethod = http.Request.Method;
                existing.RequestPath = requestPath;
                existing.QueryString = SensitiveDataRedactor.Redact(
                    Truncate(http.Request.QueryString.Value, 2000));
                existing.ReferrerUrl = SensitiveDataRedactor.Redact(
                    Truncate(http.Request.Headers.Referer.FirstOrDefault(), 500));
                existing.RemoteIpAddress = http.Connection.RemoteIpAddress?.ToString();
                existing.UserAgent = SensitiveDataRedactor.Redact(
                    Truncate(http.Request.Headers.UserAgent.FirstOrDefault(), 500));
                existing.UserId = userId;
                existing.Username = http.User.FindFirstValue(ClaimTypes.Name);
                existing.UpdatedAt = now;
                existing.LastModifiedById = userId;
                await repository.UpdateAsync(existing);
            }
            else
            {
                await repository.AddAsync(new SystemExceptionLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Fingerprint = fingerprint,
                    OccurrenceCount = 1,
                    FirstOccurredAt = now,
                    LastOccurredAt = now,
                    Level = level,
                    Logger = Truncate(context.ActionDescriptor.DisplayName, 200),
                    ShortMessage = Truncate(redactedDetail, 1000),
                    FullMessage = redactedPayload,
                    ExceptionType = exceptionType,
                    TraceId = http.TraceIdentifier,
                    RequestMethod = http.Request.Method,
                    RequestPath = requestPath,
                    QueryString = SensitiveDataRedactor.Redact(
                        Truncate(http.Request.QueryString.Value, 2000)),
                    ReferrerUrl = SensitiveDataRedactor.Redact(
                        Truncate(http.Request.Headers.Referer.FirstOrDefault(), 500)),
                    RemoteIpAddress = http.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = SensitiveDataRedactor.Redact(
                        Truncate(http.Request.Headers.UserAgent.FirstOrDefault(), 500)),
                    UserId = userId,
                    Username = http.User.FindFirstValue(ClaimTypes.Name),
                    CreatedAt = now,
                    CreatedById = userId
                });
            }

            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            // Failure telemetry must never replace the original API response.
            _logger.LogError(exception,
                "Failed to persist handled API problem for {RequestPath}",
                context.HttpContext.Request.Path);
        }
    }

    private static bool TryGetProblem(
        IActionResult result,
        out ProblemDetails problem,
        out int statusCode)
    {
        if (result is ObjectResult { Value: ProblemDetails value } objectResult)
        {
            problem = value;
            statusCode = objectResult.StatusCode ?? value.Status ??
                StatusCodes.Status500InternalServerError;
            return true;
        }

        problem = null!;
        statusCode = 0;
        return false;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...[truncated]";
    }
}
