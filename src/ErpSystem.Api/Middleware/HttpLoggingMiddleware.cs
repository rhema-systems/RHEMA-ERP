using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ErpSystem.Api.Middleware;

public class HttpLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpLoggingMiddleware> _logger;
    private readonly bool _logBodies;
    private readonly string _httpLogsDirectory;
    private readonly int _maxBodyLength;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HashSet<string> _sensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "X-API-Key", "X-Auth-Token"
    };

    private readonly HashSet<string> _excludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health", "/swagger", "/api/health"
    };

    public HttpLoggingMiddleware(
        RequestDelegate next,
        ILogger<HttpLoggingMiddleware> logger,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _logBodies = configuration.GetValue("HttpRequestResponseLogging:LogBodies", false);
        _maxBodyLength = Math.Max(256, configuration.GetValue("HttpRequestResponseLogging:MaxBodyLength", 4000));
        _httpLogsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "http-logs");
        if (!Directory.Exists(_httpLogsDirectory))
        {
            Directory.CreateDirectory(_httpLogsDirectory);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_excludedPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var requestData = await CaptureRequestAsync(context, requestId);

        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var responseData = await CaptureResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);
            
            // Write combined request and response
            await WriteHttpLogAsync(requestData, responseData);
            
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task<object> CaptureRequestAsync(HttpContext context, string requestId)
    {
        try
        {
            var request = context.Request;
            request.EnableBuffering();
            var requestBody = _logBodies ? await ReadRequestBodyAsync(request) : null;

            return new
            {
                RequestId = requestId,
                Timestamp = DateTime.UtcNow,
                Method = request.Method,
                Path = request.Path.Value,
                QueryString = request.QueryString.Value,
                Headers = GetSafeHeaders(request.Headers),
                ContentType = request.ContentType,
                ContentLength = request.ContentLength,
                Body = requestBody,
                RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = request.Headers["User-Agent"].ToString(),
                User = context.User?.Identity?.Name ?? "Anonymous"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing HTTP request for {RequestId}", requestId);
            return new { RequestId = requestId, Error = ex.Message };
        }
    }

    private async Task<object> CaptureResponseAsync(HttpContext context, string requestId, long elapsedMs)
    {
        try
        {
            var response = context.Response;
            var responseBody = _logBodies ? await ReadResponseBodyAsync(response) : null;

            return new
            {
                RequestId = requestId,
                Timestamp = DateTime.UtcNow,
                StatusCode = response.StatusCode,
                StatusDescription = GetStatusDescription(response.StatusCode),
                Headers = GetSafeHeaders(response.Headers),
                ContentType = response.ContentType,
                ContentLength = response.ContentLength ?? responseBody?.Length ?? 0,
                Body = responseBody,
                ElapsedMilliseconds = elapsedMs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing HTTP response for {RequestId}", requestId);
            return new { RequestId = requestId, Error = ex.Message };
        }
    }

    private async Task WriteHttpLogAsync(object requestData, object responseData)
    {
        try
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var fileName = $"http-{date}.json";
            var filePath = Path.Combine(_httpLogsDirectory, fileName);
            
            var logEntry = new
            {
                Request = requestData,
                Response = responseData
            };
            
            var json = JsonSerializer.Serialize(logEntry, _jsonOptions);
            
            await _fileLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(filePath, json + Environment.NewLine);
            }
            finally
            {
                _fileLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing HTTP log");
        }
    }

    private async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength == null || request.ContentLength == 0)
            return null;

        if (!IsTextContentType(request.ContentType))
            return $"[Binary content: {request.ContentType}]";

        request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);

        return TruncateIfNeeded(body, _maxBodyLength);
    }

    private async Task<string?> ReadResponseBodyAsync(HttpResponse response)
    {
        if (response.Body.Length == 0)
            return null;

        if (!IsTextContentType(response.ContentType))
            return $"[Binary content: {response.ContentType}]";

        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);

        return TruncateIfNeeded(body, _maxBodyLength);
    }

    private Dictionary<string, string> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            if (_sensitiveHeaders.Contains(header.Key))
                safeHeaders[header.Key] = "[REDACTED]";
            else
                safeHeaders[header.Key] = header.Value.ToString();
        }
        return safeHeaders;
    }

    private bool IsTextContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        var textTypes = new[] { "application/json", "application/xml", "text/", "application/x-www-form-urlencoded" };
        return textTypes.Any(type => contentType.Contains(type, StringComparison.OrdinalIgnoreCase));
    }

    private string TruncateIfNeeded(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + $"... [Truncated, total length: {text.Length}]";
    }

    private string GetStatusDescription(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            422 => "Unprocessable Entity",
            500 => "Internal Server Error",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            _ => statusCode.ToString()
        };
    }
}
