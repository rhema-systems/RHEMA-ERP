using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Instance = context.Request.Path,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ValidationException validationEx:
                response.Title = "Validation Error";
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Detail = "One or more validation errors occurred.";
                response.Errors = validationEx.Errors;
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case UnauthorizedException unauthorizedEx:
                response.Title = "Unauthorized";
                response.Status = (int)HttpStatusCode.Unauthorized;
                response.Detail = "Authentication is required to access this resource.";
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;

            case ForbiddenException forbiddenEx:
                response.Title = "Forbidden";
                response.Status = (int)HttpStatusCode.Forbidden;
                response.Detail = "You do not have permission to access this resource.";
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                break;

            case NotFoundException notFoundEx:
                response.Title = "Not Found";
                response.Status = (int)HttpStatusCode.NotFound;
                response.Detail = notFoundEx.Message ?? "The requested resource was not found.";
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            case ConflictException conflictEx:
                response.Title = "Conflict";
                response.Status = (int)HttpStatusCode.Conflict;
                response.Detail = conflictEx.Message ?? "The request conflicts with the current state of the resource.";
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                break;

            case InvalidOperationException invalidOpEx:
                response.Title = "Invalid Operation";
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Detail = "The operation is not valid for the current state of the object.";
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case ArgumentException argumentEx:
                response.Title = "Bad Request";
                response.Status = (int)HttpStatusCode.BadRequest;
                response.Detail = "Invalid argument provided.";
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case TimeoutException timeoutEx:
                response.Title = "Request Timeout";
                response.Status = (int)HttpStatusCode.RequestTimeout;
                response.Detail = "The request timed out. Please try again.";
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                break;

            default:
                response.Title = "Internal Server Error";
                response.Status = (int)HttpStatusCode.InternalServerError;
                response.Detail = "An unexpected error occurred while processing your request.";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        // Log the exception with different levels based on type
        LogException(exception, context);

        // Only include detailed error information in development
        if (_environment.IsDevelopment())
        {
            response.DeveloperMessage = exception.Message;
            response.StackTrace = exception.StackTrace;
        }

        // Security: Don't leak sensitive information
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        }));
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
            case UnauthorizedException:
            case ForbiddenException:
            case NotFoundException:
            case ConflictException:
            case ArgumentException:
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
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    
    // Development-only properties
    public string? DeveloperMessage { get; set; }
    public string? StackTrace { get; set; }
}

// Extension method for easy registration
public static class GlobalExceptionHandlingExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}