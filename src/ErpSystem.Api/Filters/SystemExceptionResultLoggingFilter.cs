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
    public const string HandledExceptionItemKey =
        "ErpSystem.Api.HandledExceptionForSystemLog";
    public const string HandledFailurePayloadItemKey =
        "ErpSystem.Api.HandledFailurePayloadForSystemLog";

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
        NormalizeFailureResult(context);

        if (TryGetProblem(
                context.Result,
                context.HttpContext,
                out var problem,
                out var statusCode) &&
            statusCode >= StatusCodes.Status400BadRequest &&
            context.HttpContext.Request.Path.StartsWithSegments("/api") &&
            !context.HttpContext.Request.Path.StartsWithSegments(
                "/api/admin/system-exception-logs"))
        {
            await TryPersistAsync(context, problem, statusCode);
        }

        await next();
    }

    /// <summary>
    /// Older controllers still return plain strings from catch blocks. Normalize
    /// those responses globally so every API consumer receives the same safe,
    /// user-readable RFC 7807 contract while those controllers are migrated.
    /// </summary>
    private static void NormalizeFailureResult(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult objectResult)
        {
            return;
        }

        var statusCode = objectResult.StatusCode ?? StatusCodes.Status500InternalServerError;
        if (statusCode < StatusCodes.Status400BadRequest)
        {
            return;
        }

        if (objectResult.Value is ProblemDetails)
        {
            return;
        }

        // Retain the original payload for the administrator-only diagnostic
        // entry before replacing unsafe legacy 500 responses.
        context.HttpContext.Items[HandledFailurePayloadItemKey] = objectResult.Value;

        // Preserve existing anonymous 4xx contracts. TryGetProblem still creates
        // an internal ProblemDetails snapshot so the failure is searchable.
        if (statusCode < StatusCodes.Status500InternalServerError &&
            objectResult.Value is not string)
        {
            return;
        }

        var isServerFailure = statusCode >= StatusCodes.Status500InternalServerError;
        var traceId = context.HttpContext.TraceIdentifier;
        var legacyDetail = objectResult.Value as string;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = isServerFailure ? "We couldn't complete your request" : "Request could not be completed",
            Detail = isServerFailure
                ? $"Something went wrong while processing your request. Please try again. If the problem continues, contact your administrator. Reference ID: {traceId}."
                : string.IsNullOrWhiteSpace(legacyDetail)
                    ? "The request could not be completed."
                    : legacyDetail.Trim(),
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = isServerFailure ? "UNEXPECTED_ERROR" : $"HTTP_{statusCode}";
        problem.Extensions["correlationId"] = traceId;

        context.Result = new ObjectResult(problem) { StatusCode = statusCode };
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
            if (tenantId == Guid.Empty) return;
            var now = DateTime.UtcNow;
            var problemDetail = string.IsNullOrWhiteSpace(problem.Detail)
                ? problem.Title ?? $"HTTP {statusCode} request failure"
                : problem.Detail;
            var handledException = http.Items.TryGetValue(
                    HandledExceptionItemKey, out var captured)
                ? captured as Exception
                : null;
            var hasHandledPayload = http.Items.TryGetValue(
                HandledFailurePayloadItemKey, out var handledPayload);
            var detail = handledException?.Message ?? problemDetail;
            var exceptionType = handledException?.GetType().FullName ??
                (problem is ValidationProblemDetails
                    ? "HandledApiValidationProblem"
                    : "HandledApiProblem");
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

            var diagnosticPayload = handledException is not null
                ? $"ProblemDetails: {payload}\n\nHandled exception:\n{handledException}"
                : hasHandledPayload
                    ? $"ProblemDetails: {payload}\n\nHandled response payload:\n{SafeSerialize(handledPayload)}"
                    : payload;
            var redactedDetail = SensitiveDataRedactor.Redact(detail);
            var redactedPayload = SensitiveDataRedactor.Redact(
                Truncate(diagnosticPayload, 20000));
            var redactedStackTrace = handledException?.StackTrace is { Length: > 0 } stackTrace
                ? SensitiveDataRedactor.Redact(Truncate(stackTrace, 20000))
                : null;
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
                StackTrace = redactedStackTrace,
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
        HttpContext httpContext,
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

        if (result is ObjectResult objectFailure)
        {
            statusCode = objectFailure.StatusCode ??
                StatusCodes.Status500InternalServerError;
            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                httpContext.Items.TryAdd(
                    HandledFailurePayloadItemKey,
                    objectFailure.Value);
                problem = CreateProblemSnapshot(
                    httpContext,
                    statusCode,
                    objectFailure.Value);
                return true;
            }
        }

        problem = null!;
        statusCode = 0;
        return false;
    }

    private static ProblemDetails CreateProblemSnapshot(
        HttpContext httpContext,
        int statusCode,
        object? payload)
    {
        var isServerFailure = statusCode >= StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = isServerFailure
                ? "We couldn't complete your request"
                : "Request could not be completed",
            Detail = isServerFailure
                ? $"Something went wrong while processing your request. Please try again. If the problem continues, contact your administrator. Reference ID: {httpContext.TraceIdentifier}."
                : ExtractDetail(payload) ?? $"HTTP {statusCode} request failure",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = ExtractCode(payload) ??
            (isServerFailure ? "UNEXPECTED_ERROR" : $"HTTP_{statusCode}");
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;
        return problem;
    }

    private static string? ExtractDetail(object? payload) =>
        ExtractStringProperty(payload, "detail", "message", "error", "title");

    private static string? ExtractCode(object? payload) =>
        ExtractStringProperty(payload, "code", "errorCode");

    private static string? ExtractStringProperty(
        object? payload,
        params string[] propertyNames)
    {
        if (payload is null) return null;
        if (payload is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        try
        {
            var element = JsonSerializer.SerializeToElement(payload);
            if (element.ValueKind != JsonValueKind.Object) return null;

            foreach (var propertyName in propertyNames)
            {
                var property = element.EnumerateObject().FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase));
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
            }
        }
        catch
        {
            // A malformed response payload must not replace the original failure.
        }

        return null;
    }

    private static string SafeSerialize(object? value)
    {
        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch
        {
            return "[Response payload could not be serialized]";
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...[truncated]";
    }
}
