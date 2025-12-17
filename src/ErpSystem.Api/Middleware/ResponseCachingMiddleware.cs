using System.Text;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace ErpSystem.Api.Middleware;

/// <summary>
/// Advanced response caching middleware with smart cache control and ETags
/// </summary>
public class AdvancedResponseCachingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdvancedResponseCachingMiddleware> _logger;
    private readonly ResponseCachingOptions _options;

    public AdvancedResponseCachingMiddleware(
        RequestDelegate next,
        ILogger<AdvancedResponseCachingMiddleware> logger,
        IOptions<ResponseCachingOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip caching for certain conditions
        if (!ShouldCache(context))
        {
            await _next(context);
            return;
        }

        // Add ETag support for conditional requests
        await HandleConditionalRequests(context);

        // Set cache headers based on route and content
        SetCacheHeaders(context);

        await _next(context);
    }

    private static bool ShouldCache(HttpContext context)
    {
        var request = context.Request;

        // Don't cache non-GET requests
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        // Don't cache requests with authorization unless explicitly allowed
        if (request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            // Allow caching for public API endpoints
            var path = request.Path.Value?.ToLower();
            if (!path?.Contains("/api/public/") == true)
            {
                return false;
            }
        }

        // Don't cache requests with query parameters that indicate dynamic content
        if (request.QueryString.HasValue)
        {
            var queryString = request.QueryString.Value?.ToLower();
            if (queryString?.Contains("nocache") == true ||
                queryString?.Contains("refresh") == true ||
                queryString?.Contains("timestamp") == true)
            {
                return false;
            }
        }

        return true;
    }

    private async Task HandleConditionalRequests(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Handle If-None-Match (ETag) header
        if (request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatch))
        {
            // Generate ETag based on route and user context
            var etag = GenerateETag(context);

            if (ifNoneMatch.ToString().Contains(etag))
            {
                response.StatusCode = 304; // Not Modified
                response.Headers[HeaderNames.ETag] = etag;
                _logger.LogDebug("Returning 304 Not Modified for ETag: {ETag}", etag);
                return;
            }
        }

        // Handle If-Modified-Since header
        if (request.Headers.TryGetValue(HeaderNames.IfModifiedSince, out var ifModifiedSince))
        {
            if (DateTime.TryParse(ifModifiedSince, out var modifiedSince))
            {
                var lastModified = GetLastModifiedDate(context);
                if (lastModified <= modifiedSince)
                {
                    response.StatusCode = 304; // Not Modified
                    _logger.LogDebug("Returning 304 Not Modified for If-Modified-Since: {Date}", modifiedSince);
                    return;
                }
            }
        }
    }

    private void SetCacheHeaders(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Path.Value?.ToLower();

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // Set cache headers based on content type and path
        var cacheProfile = GetCacheProfile(path);

        if (cacheProfile != null)
        {
            if (cacheProfile.Duration.HasValue)
            {
                response.Headers[HeaderNames.CacheControl] = $"public, max-age={cacheProfile.Duration.Value}";
            }

            if (cacheProfile.VaryByHeaders?.Any() == true)
            {
                response.Headers[HeaderNames.Vary] = string.Join(", ", cacheProfile.VaryByHeaders);
            }

            // Set ETag for cacheable content
            if (cacheProfile.EnableETag)
            {
                response.Headers[HeaderNames.ETag] = GenerateETag(context);
            }

            // Set Last-Modified header
            response.Headers[HeaderNames.LastModified] = GetLastModifiedDate(context).ToString("R");

            _logger.LogDebug("Applied cache profile {Profile} to {Path}", cacheProfile.Name, path);
        }
    }

    private static CacheProfile? GetCacheProfile(string path)
    {
        return path switch
        {
            _ when path.Contains("/api/public/") => new CacheProfile
            {
                Name = "Public",
                Duration = 3600, // 1 hour
                VaryByHeaders = new[] { "Accept-Language" },
                EnableETag = true
            },
            _ when path.Contains("/api/v2/auditlog") && path.Contains("/stats") => new CacheProfile
            {
                Name = "Stats",
                Duration = 600, // 10 minutes
                VaryByHeaders = new[] { "Authorization" },
                EnableETag = true
            },
            _ when path.Contains("/api/v2/auditlog/") && !path.Contains("export") => new CacheProfile
            {
                Name = "AuditDetails",
                Duration = 3600, // 1 hour (audit logs don't change)
                VaryByHeaders = new[] { "Authorization" },
                EnableETag = true
            },
            _ when path.Contains("/api/settings") => new CacheProfile
            {
                Name = "Settings",
                Duration = 1800, // 30 minutes
                VaryByHeaders = new[] { "Authorization", "X-Tenant-Id" },
                EnableETag = true
            },
            _ when path.Contains("/api/users/profile") => new CacheProfile
            {
                Name = "UserProfile",
                Duration = 900, // 15 minutes
                VaryByHeaders = new[] { "Authorization" },
                EnableETag = true
            },
            _ when path.Contains("/api/tenants") && HttpMethods.IsGet(path) => new CacheProfile
            {
                Name = "Tenants",
                Duration = 1800, // 30 minutes
                VaryByHeaders = new[] { "Authorization" },
                EnableETag = true
            },
            _ => null // No caching by default
        };
    }

    private static string GenerateETag(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? "";
        var query = request.QueryString.Value ?? "";
        var userContext = context.User?.Identity?.Name ?? "anonymous";

        // Include tenant context if available
        var tenantId = request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "";

        var etag = $"{path}{query}{userContext}{tenantId}";
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(etag));
        return $"\"{Convert.ToHexString(hash)}\"";
    }

    private static DateTime GetLastModifiedDate(HttpContext context)
    {
        // For demo purposes, use a recent timestamp
        // In production, this would be based on actual data modification times
        return DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour);
    }
}

/// <summary>
/// Cache profile configuration
/// </summary>
public class CacheProfile
{
    public string Name { get; set; } = string.Empty;
    public int? Duration { get; set; }
    public string[]? VaryByHeaders { get; set; }
    public bool EnableETag { get; set; } = false;
}

/// <summary>
/// Response caching configuration options
/// </summary>
public class ResponseCachingOptions
{
    public const string SectionName = "ResponseCaching";

    /// <summary>
    /// Enable response caching
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum size of response cache in MB
    /// </summary>
    public long MaximumBodySize { get; set; } = 64; // 64MB

    /// <summary>
    /// Size limit for response cache in MB
    /// </summary>
    public long SizeLimit { get; set; } = 100; // 100MB

    /// <summary>
    /// Use case-sensitive paths for caching
    /// </summary>
    public bool UseCaseSensitivePaths { get; set; } = false;

    /// <summary>
    /// Default cache duration in seconds
    /// </summary>
    public int DefaultCacheDuration { get; set; } = 300; // 5 minutes
}

/// <summary>
/// Extension methods for response caching
/// </summary>
public static class ResponseCachingExtensions
{
    /// <summary>
    /// Add advanced response caching services
    /// </summary>
    public static IServiceCollection AddAdvancedResponseCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure response caching options
        services.Configure<ResponseCachingOptions>(
            configuration.GetSection(ResponseCachingOptions.SectionName));

        var options = configuration.GetSection(ResponseCachingOptions.SectionName)
            .Get<ResponseCachingOptions>() ?? new ResponseCachingOptions();

        // Add response caching services
        services.AddResponseCaching(cachingOptions =>
        {
            cachingOptions.MaximumBodySize = options.MaximumBodySize * 1024 * 1024; // Convert MB to bytes
            cachingOptions.SizeLimit = options.SizeLimit * 1024 * 1024; // Convert MB to bytes
            cachingOptions.UseCaseSensitivePaths = options.UseCaseSensitivePaths;
        });

        return services;
    }

    /// <summary>
    /// Use advanced response caching middleware
    /// </summary>
    public static IApplicationBuilder UseAdvancedResponseCaching(this IApplicationBuilder app)
    {
        app.UseResponseCaching();
        app.UseMiddleware<AdvancedResponseCachingMiddleware>();
        return app;
    }
}
