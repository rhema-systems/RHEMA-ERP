using System.Net;
using System.Text.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ErpSystem.Api.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            CorrelationId = context.TraceIdentifier,
            Instance = context.Request.Path,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case BusinessRuleException businessRuleEx:
                response.Title = businessRuleEx.StatusCode switch
                {
                    StatusCodes.Status404NotFound => "Not Found",
                    StatusCodes.Status409Conflict => "Conflict",
                    StatusCodes.Status403Forbidden => "Forbidden",
                    _ => "Request could not be completed"
                };
                response.Code = businessRuleEx.Code;
                response.Status = businessRuleEx.StatusCode;
                response.Detail = businessRuleEx.Message;
                break;

            case ValidationException validationEx:
                response.Title = "Validation Error";
                response.Code = "VALIDATION_ERROR";
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Detail = "One or more validation errors occurred.";
                response.Errors = validationEx.Errors;
                break;

            case UnauthorizedException:
                response.Title = "Unauthorized";
                response.Code = "AUTHENTICATION_REQUIRED";
                response.Status = (int)HttpStatusCode.Unauthorized;
                response.Detail = "Authentication is required to access this resource.";
                break;

            case ForbiddenException:
                response.Title = "Forbidden";
                response.Code = "ACCESS_FORBIDDEN";
                response.Status = (int)HttpStatusCode.Forbidden;
                response.Detail = "You do not have permission to access this resource.";
                break;

            case UnauthorizedAccessException:
                response.Title = "Forbidden";
                response.Code = "ACCESS_FORBIDDEN";
                response.Status = (int)HttpStatusCode.Forbidden;
                response.Detail = "You do not have permission to access this resource.";
                break;

            case NotFoundException notFoundEx:
                response.Title = "Not Found";
                response.Code = "RESOURCE_NOT_FOUND";
                response.Status = (int)HttpStatusCode.NotFound;
                response.Detail = notFoundEx.Message ?? "The requested resource was not found.";
                break;

            // The medical module raises this for domain rejections and missing records. Its own
            // contract (see MedicalWorkflowException) specifies NotFound -> 404 and everything else
            // -> 422; without this case all of them fell through to a generic 500.
            case MedicalWorkflowException medicalEx:
                var medicalStatus = medicalEx.Reason == MedicalWorkflowFailureReason.NotFound
                    ? HttpStatusCode.NotFound
                    : HttpStatusCode.UnprocessableEntity;
                response.Title = medicalEx.Reason == MedicalWorkflowFailureReason.NotFound
                    ? "Not Found"
                    : "Unprocessable Entity";
                response.Code = medicalEx.Reason == MedicalWorkflowFailureReason.NotFound
                    ? "RESOURCE_NOT_FOUND"
                    : "WORKFLOW_REJECTED";
                response.Status = (int)medicalStatus;
                response.Detail = medicalEx.Message;   // safe to display by design
                break;

            // Succession raises this when a request collides with an existing record — a position
            // that already has an active plan, or a plan number already taken. Both were enforced
            // only by unique indexes before, so they arrived as unhandled DbUpdateExceptions.
            case SuccessionConflictException successionEx:
                response.Title = "Conflict";
                response.Status = (int)HttpStatusCode.Conflict;
                response.Detail = successionEx.Message;   // safe to display by design
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                break;

            // Succession domain rules the caller can act on. ArgumentException would have replaced
            // the message with "Invalid argument provided.", which tells the user nothing.
            case SuccessionValidationException successionValidationEx:
                response.Title = "Bad Request";
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Detail = successionValidationEx.Message;   // safe to display by design
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            // Probation & confirmation. One exception, four outcomes — the medical shape, for the
            // same reason: without this case every rule in the area was flattened to one of two
            // fixed strings and said nothing. See ProbationWorkflowException.
            case ProbationWorkflowException probationEx:
                var probationStatus = probationEx.Reason switch
                {
                    ProbationFailureReason.NotFound => HttpStatusCode.NotFound,
                    ProbationFailureReason.InvalidState => HttpStatusCode.Conflict,
                    ProbationFailureReason.Conflict => HttpStatusCode.Conflict,
                    _ => HttpStatusCode.BadRequest
                };
                response.Title = probationStatus switch
                {
                    HttpStatusCode.NotFound => "Not Found",
                    HttpStatusCode.Conflict => "Conflict",
                    _ => "Bad Request"
                };
                response.Status = (int)probationStatus;
                response.Detail = probationEx.Message;   // safe to display by design
                context.Response.StatusCode = (int)probationStatus;
                break;

            // Job architecture, competency and manpower budget. Same shape again —
            // without this case, all 76 rules in those three services were flattened to one of two
            // fixed strings. See JobArchitectureException.
            case JobArchitectureException jobArchEx:
                var jobArchStatus = jobArchEx.Reason switch
                {
                    JobArchitectureFailureReason.NotFound => HttpStatusCode.NotFound,
                    JobArchitectureFailureReason.InvalidState => HttpStatusCode.Conflict,
                    JobArchitectureFailureReason.Conflict => HttpStatusCode.Conflict,
                    _ => HttpStatusCode.BadRequest
                };
                response.Title = jobArchStatus switch
                {
                    HttpStatusCode.NotFound => "Not Found",
                    HttpStatusCode.Conflict => "Conflict",
                    _ => "Bad Request"
                };
                response.Status = (int)jobArchStatus;
                response.Detail = jobArchEx.Message;   // safe to display by design
                context.Response.StatusCode = (int)jobArchStatus;
                break;

            // Staff awards. Without this case its 38
            // service rules were flattened to one fixed string, and — worse — every "not found"
            // among them answered 400, so no caller could tell a deleted award from a bad payload.
            // See AwardsWorkflowException.
            case AwardsWorkflowException awardsEx:
                var awardsStatus = awardsEx.Reason switch
                {
                    AwardsFailureReason.NotFound => HttpStatusCode.NotFound,
                    AwardsFailureReason.InvalidState => HttpStatusCode.Conflict,
                    AwardsFailureReason.Conflict => HttpStatusCode.Conflict,
                    _ => HttpStatusCode.BadRequest
                };
                response.Title = awardsStatus switch
                {
                    HttpStatusCode.NotFound => "Not Found",
                    HttpStatusCode.Conflict => "Conflict",
                    _ => "Bad Request"
                };
                response.Status = (int)awardsStatus;
                response.Detail = awardsEx.Message;   // safe to display by design
                context.Response.StatusCode = (int)awardsStatus;
                break;

            // Staff / company assets. Without this
            // case its 28 service rules were flattened to two fixed strings, and — worse — the
            // eighteen "not found" among them all answered 400, so no caller could tell a disposed
            // asset from a bad payload. See AssetsWorkflowException.
            case AssetsWorkflowException assetsEx:
                var assetsStatus = assetsEx.Reason switch
                {
                    AssetsFailureReason.NotFound => HttpStatusCode.NotFound,
                    AssetsFailureReason.InvalidState => HttpStatusCode.Conflict,
                    AssetsFailureReason.Conflict => HttpStatusCode.Conflict,
                    _ => HttpStatusCode.BadRequest
                };
                response.Title = assetsStatus switch
                {
                    HttpStatusCode.NotFound => "Not Found",
                    HttpStatusCode.Conflict => "Conflict",
                    _ => "Bad Request"
                };
                response.Status = (int)assetsStatus;
                response.Detail = assetsEx.Message;   // safe to display by design
                context.Response.StatusCode = (int)assetsStatus;
                break;

            // A punch refused by hard geofence enforcement. Until 2026-09-03 this fell through to the
            // 500 branch, so a correctly refused punch read as "Something went wrong". The message is
            // composed by GeofenceVerificationService and names the zone, so it is safe to display.
            case GeofenceVerificationRejectedException geofenceEx:
                response.Title = "Outside Work Zone";
                response.Code = "GEOFENCE_REJECTED";
                response.Status = (int)HttpStatusCode.UnprocessableEntity;
                response.Detail = geofenceEx.Message;
                context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                break;

            case ConflictException conflictEx:
                response.Title = "Conflict";
                response.Code = "RESOURCE_CONFLICT";
                response.Status = (int)HttpStatusCode.Conflict;
                response.Detail = conflictEx.Message ?? "The request conflicts with the current state of the resource.";
                break;

            case InvalidOperationException:
                response.Title = "Invalid Operation";
                response.Code = "INVALID_OPERATION";
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Detail = "The operation is not valid for the current state of the object.";
                break;

            case ArgumentException:
                response.Title = "Bad Request";
                response.Code = "INVALID_ARGUMENT";
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Detail = "Invalid argument provided.";
                break;

            case TimeoutException:
                response.Title = "Request Timeout";
                response.Code = "REQUEST_TIMEOUT";
                response.Status = (int)HttpStatusCode.RequestTimeout;
                response.Detail = "The request timed out. Please try again.";
                break;

            default:
                response.Title = "We couldn't complete your request";
                response.Code = "UNEXPECTED_ERROR";
                response.Status = (int)HttpStatusCode.InternalServerError;
                response.Detail = $"Something went wrong while processing your request. Please try again. If the problem continues, contact your administrator. Reference ID: {response.TraceId}.";
                break;
        }

        // Log the exception with different levels based on type
        LogException(exception, context);

        // Persist exception details to SQL for admin troubleshooting (best-effort).
        await TryPersistExceptionAsync(context, exception, response);

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started for {RequestPath}; skipping error response body write.", context.Request.Path);
            return;
        }

        try
        {
            // This middleware owns the public error contract. Never expose exception messages,
            // stack traces or framework diagnostics, including on Development workstations.
            context.Response.Clear();
            context.Response.StatusCode = response.Status;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            }));
        }
        catch (ObjectDisposedException writeEx)
        {
            _logger.LogWarning(writeEx, "The response stream was disposed before the error response could be written for {RequestPath}.", context.Request.Path);
        }
        catch (InvalidOperationException writeEx)
        {
            _logger.LogWarning(writeEx, "The error response could not be written for {RequestPath}.", context.Request.Path);
        }
    }

    private async Task TryPersistExceptionAsync(HttpContext context, Exception exception, ErrorResponse response)
    {
        try
        {
            using var scope = context.RequestServices.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetService<ErpSystem.Core.Interfaces.IUnitOfWork>();
            var currentUser = scope.ServiceProvider.GetService<ICurrentUserService>();

            if (unitOfWork == null || currentUser == null)
            {
                return;
            }

            var level = exception switch
            {
                ValidationException or BusinessRuleException or UnauthorizedException or ForbiddenException or UnauthorizedAccessException or NotFoundException or ConflictException or ArgumentException or MedicalWorkflowException or ProbationWorkflowException or AssetsWorkflowException => "Warning",
                InvalidOperationException => "Error",
                _ => "Critical"
            };

            var userId = (Guid?)null;
            var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var uid))
            {
                userId = uid;
            }

            var loggerName = exception.TargetSite?.DeclaringType?.FullName;
            var shortMessage = exception.Message ?? string.Empty;

            var tenantId = currentUser.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return;
            }

            var now = DateTime.UtcNow;

            var requestPath = context.Request.Path.Value;
            var queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null;
            var referrer = context.Request.Headers.Referer.FirstOrDefault();
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();

            // Redact sensitive tokens/secrets before persisting.
            var redactedShort = SensitiveDataRedactor.Redact(shortMessage);
            var redactedFull = SensitiveDataRedactor.Redact(Truncate(exception.ToString(), 20000));
            var redactedStack = SensitiveDataRedactor.Redact(Truncate(exception.StackTrace, 20000));
            var redactedQuery = SensitiveDataRedactor.Redact(Truncate(queryString, 2000));
            var redactedReferrer = SensitiveDataRedactor.Redact(Truncate(referrer, 500));
            var redactedUserAgent = SensitiveDataRedactor.Redact(Truncate(userAgent, 500));

            var fingerprint = ExceptionFingerprint.Compute(
                exceptionType: exception.GetType().FullName,
                message: redactedShort,
                requestPath: requestPath);

            var repo = unitOfWork.Repository<SystemExceptionLog>();
            var log = new SystemExceptionLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Fingerprint = fingerprint,
                OccurrenceCount = 1,
                FirstOccurredAt = now,
                LastOccurredAt = now,
                Level = level,
                Logger = loggerName,
                ShortMessage = Truncate(redactedShort, 1000),
                FullMessage = redactedFull,
                ExceptionType = exception.GetType().FullName,
                StackTrace = redactedStack,
                TraceId = response.TraceId,
                RequestMethod = context.Request.Method,
                RequestPath = requestPath,
                QueryString = redactedQuery,
                ReferrerUrl = redactedReferrer,
                RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = redactedUserAgent,
                UserId = userId,
                Username = context.User?.FindFirst(ClaimTypes.Name)?.Value,
                CreatedAt = now,
                CreatedById = userId
            };

            await repo.AddAsync(log);
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never allow exception logging to break the API response pipeline.
            _logger.LogError(ex, "Failed to persist exception log entry to SQL");
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        return value.Substring(0, maxLength) + "...[truncated]";
    }

    private void LogException(Exception exception, HttpContext context)
    {
        var logContext = new
        {
            RequestId = context.TraceIdentifier,
            RequestPath = context.Request.Path.Value,
            RequestMethod = context.Request.Method,
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
            RemoteIP = context.Connection.RemoteIpAddress?.ToString(),
            UserId = context.User?.Identity?.Name
        };

        switch (exception)
        {
            case ValidationException:
            case BusinessRuleException:
            case UnauthorizedException:
            case ForbiddenException:
            case UnauthorizedAccessException:
            case NotFoundException:
            case ConflictException:
            case ArgumentException:
            case MedicalWorkflowException:
            case AssetsWorkflowException:
            case SuccessionConflictException:
            case SuccessionValidationException:
                // These are expected exceptions - log as warnings
                _logger.LogWarning(exception,
                    "Client error occurred for {RequestMethod} {RequestPath}. Context: {@LogContext}",
                    context.Request.Method,
                    context.Request.Path,
                    logContext);
                break;

            case InvalidOperationException:
                // Could be client or server error - log as error
                _logger.LogError(exception,
                    "Invalid operation occurred for {RequestMethod} {RequestPath}. Context: {@LogContext}",
                    context.Request.Method,
                    context.Request.Path,
                    logContext);
                break;

            default:
                // Unexpected server errors - log as critical
                _logger.LogCritical(exception,
                    "Unhandled exception occurred for {RequestMethod} {RequestPath}. Context: {@LogContext}",
                    context.Request.Method,
                    context.Request.Path,
                    logContext);
                break;
        }
    }
}

// Custom exception classes
public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(Dictionary<string, string[]> errors) : this()
    {
        Errors = errors;
    }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message, Exception innerException) : base(message, innerException)
    {
        Errors = new Dictionary<string, string[]>();
    }
}

internal static class ExceptionFingerprint
{
    private static readonly Regex GuidRegex = new(
        @"\b[a-fA-F0-9]{8}\-[a-fA-F0-9]{4}\-[a-fA-F0-9]{4}\-[a-fA-F0-9]{4}\-[a-fA-F0-9]{12}\b",
        RegexOptions.Compiled);

    private static readonly Regex LongNumberRegex = new(@"\b\d{4,}\b", RegexOptions.Compiled);

    public static string Compute(string? exceptionType, string message, string? requestPath)
    {
        var type = exceptionType ?? "UnknownException";
        var normalized = Normalize(message);
        var path = requestPath ?? string.Empty;

        // Stable fingerprint input; keep small and deterministic.
        var input = $"{type}|{path}|{normalized}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant(); // 64 chars
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Trim();
        s = GuidRegex.Replace(s, "{guid}");
        s = LongNumberRegex.Replace(s, "{n}");
        return s;
    }
}

internal static class SensitiveDataRedactor
{
    // Conservative redaction: remove secrets/tokens but keep debugging value.
    private static readonly Regex BearerRegex = new(@"Bearer\s+[A-Za-z0-9\-\._~\+\/]+=*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ApiKeyRegex = new(@"(?i)\b(api[-_ ]?key|x-api-key|token|access[-_ ]?token|refresh[-_ ]?token|secret)\b\s*[:=]\s*([^\s;,'\""]+)", RegexOptions.Compiled);
    private static readonly Regex PasswordEqRegex = new(@"(?i)\b(password|pwd)\b\s*=\s*([^;]+)", RegexOptions.Compiled);
    private static readonly Regex PasswordColonRegex = new(@"(?i)\b(password|pwd)\b\s*:\s*([^\s;,'\""]+)", RegexOptions.Compiled);

    public static string Redact(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;

        var s = input;
        s = BearerRegex.Replace(s, "Bearer [REDACTED]");
        s = ApiKeyRegex.Replace(s, m => $"{m.Groups[1].Value}=[REDACTED]");
        s = PasswordEqRegex.Replace(s, m => $"{m.Groups[1].Value}=[REDACTED]");
        s = PasswordColonRegex.Replace(s, m => $"{m.Groups[1].Value}:[REDACTED]");
        return s;
    }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("Authentication is required to access this resource.")
    {
    }

    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ForbiddenException : Exception
{
    public ForbiddenException() : base("You do not have permission to access this resource.")
    {
    }

    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class NotFoundException : Exception
{
    public NotFoundException() : base("The requested resource was not found.")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ConflictException : Exception
{
    public ConflictException() : base("The request conflicts with the current state of the resource.")
    {
    }

    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

// Error response model following RFC 7807 Problem Details standard
public class ErrorResponse
{
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = "UNEXPECTED_ERROR";
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}

// Extension method for easy registration
public static class GlobalExceptionHandlingExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}
