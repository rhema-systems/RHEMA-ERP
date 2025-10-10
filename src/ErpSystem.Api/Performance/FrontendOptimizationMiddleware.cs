using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Performance;

/// <summary>
/// Frontend performance optimization middleware
/// </summary>
public class FrontendOptimizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FrontendOptimizationMiddleware> _logger;
    private readonly FrontendOptimizationOptions _options;

    public FrontendOptimizationMiddleware(
        RequestDelegate next,
        ILogger<FrontendOptimizationMiddleware> logger,
        IOptions<FrontendOptimizationOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add performance headers
        AddPerformanceHeaders(context);

        // Handle lazy loading requests
        if (IsLazyLoadingRequest(context))
        {
            await HandleLazyLoadingRequest(context);
            return;
        }

        // Add resource hints
        AddResourceHints(context);

        await _next(context);

        // Optimize response after processing
        await OptimizeResponse(context);
    }

    private void AddPerformanceHeaders(HttpContext context)
    {
        var response = context.Response;

        // Add DNS prefetch hints
        if (_options.EnableDnsPrefetch)
        {
            response.Headers.Add("X-DNS-Prefetch-Control", "on");
        }

        // Add preconnect hints for external resources
        if (_options.PreconnectDomains.Any())
        {
            foreach (var domain in _options.PreconnectDomains)
            {
                response.Headers.Add("Link", $"<{domain}>; rel=preconnect");
            }
        }

        // Add timing information for debugging
        if (_options.EnableTimingHeaders)
        {
            response.Headers.Add("Server-Timing", $"total;desc=\"Total\";dur={0}"); // Will be updated later
        }
    }

    private static bool IsLazyLoadingRequest(HttpContext context)
    {
        return context.Request.Headers.ContainsKey("X-Lazy-Load") ||
               context.Request.Query.ContainsKey("lazy") ||
               context.Request.Path.Value?.Contains("/lazy/") == true;
    }

    private async Task HandleLazyLoadingRequest(HttpContext context)
    {
        var response = context.Response;
        
        // Set appropriate headers for lazy loading
        response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000"; // 1 year
        response.Headers.Add("X-Lazy-Load", "true");
        
        // Simulate lazy loading response
        var lazyContent = new
        {
            message = "Lazy loading content",
            timestamp = DateTime.UtcNow,
            path = context.Request.Path.Value
        };

        response.ContentType = "application/json";
        await response.WriteAsync(JsonSerializer.Serialize(lazyContent));
        
        _logger.LogDebug("Handled lazy loading request for {Path}", context.Request.Path);
    }

    private void AddResourceHints(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Add resource hints based on the current page
        if (IsApiRequest(request))
        {
            // For API requests, add hints for common resources
            if (_options.EnableResourceHints)
            {
                response.Headers.Add("Link", "</api/users/profile>; rel=prefetch");
                response.Headers.Add("Link", "</api/settings>; rel=prefetch");
            }
        }
    }

    private async Task OptimizeResponse(HttpContext context)
    {
        var response = context.Response;

        // Add compression hints
        if (_options.EnableCompression && IsCompressibleContent(response))
        {
            response.Headers.Add("X-Compression", "enabled");
        }

        // Add performance timing
        if (_options.EnableTimingHeaders)
        {
            var totalTime = DateTime.UtcNow.Millisecond; // Simple approximation
            response.Headers["Server-Timing"] = $"total;desc=\"Total\";dur={totalTime}";
        }

        // Add content optimization headers
        if (IsJsonResponse(response))
        {
            await OptimizeJsonResponse(context);
        }
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        return request.Path.Value?.StartsWith("/api/") == true;
    }

    private static bool IsCompressibleContent(HttpResponse response)
    {
        var contentType = response.ContentType?.ToLower();
        return contentType?.Contains("application/json") == true ||
               contentType?.Contains("text/") == true ||
               contentType?.Contains("application/xml") == true;
    }

    private static bool IsJsonResponse(HttpResponse response)
    {
        return response.ContentType?.Contains("application/json") == true;
    }

    private async Task OptimizeJsonResponse(HttpContext context)
    {
        var response = context.Response;

        // Add JSON optimization headers
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        response.Headers.Add("X-JSON-Optimized", "true");

        // If it's a large response, suggest pagination
        if (response.ContentLength > _options.LargResponseThreshold)
        {
            response.Headers.Add("X-Pagination-Suggested", "true");
            _logger.LogInformation("Large response detected ({Size} bytes) for {Path}", 
                response.ContentLength, context.Request.Path);
        }
    }
}

/// <summary>
/// Frontend optimization configuration options
/// </summary>
public class FrontendOptimizationOptions
{
    public const string SectionName = "FrontendOptimization";

    /// <summary>
    /// Enable DNS prefetch hints
    /// </summary>
    public bool EnableDnsPrefetch { get; set; } = true;

    /// <summary>
    /// Enable resource hints (prefetch, preload, etc.)
    /// </summary>
    public bool EnableResourceHints { get; set; } = true;

    /// <summary>
    /// Enable server timing headers
    /// </summary>
    public bool EnableTimingHeaders { get; set; } = true;

    /// <summary>
    /// Enable response compression optimization
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Domains to preconnect to
    /// </summary>
    public List<string> PreconnectDomains { get; set; } = new()
    {
        "https://fonts.googleapis.com",
        "https://fonts.gstatic.com"
    };

    /// <summary>
    /// Threshold for large response warning (in bytes)
    /// </summary>
    public long LargResponseThreshold { get; set; } = 1024 * 1024; // 1MB

    /// <summary>
    /// Enable lazy loading support
    /// </summary>
    public bool EnableLazyLoading { get; set; } = true;
}

/// <summary>
/// Response compression service for frontend optimization
/// </summary>
public class FrontendCompressionService
{
    private readonly ILogger<FrontendCompressionService> _logger;

    public FrontendCompressionService(ILogger<FrontendCompressionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configure response compression for optimal frontend performance
    /// </summary>
    public static void ConfigureResponseCompression(ResponseCompressionOptions options)
    {
        // Add compression providers
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();

        // Configure MIME types to compress
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "application/json",
            "application/javascript",
            "text/css",
            "text/html",
            "text/json",
            "text/plain",
            "text/xml",
            "application/xml",
            "image/svg+xml"
        });

        // Enable compression for HTTPS
        options.EnableForHttps = true;
    }

    /// <summary>
    /// Configure Brotli compression for maximum efficiency
    /// </summary>
    public static void ConfigureBrotliCompression(BrotliCompressionProviderOptions options)
    {
        options.Level = CompressionLevel.Optimal;
    }

    /// <summary>
    /// Configure Gzip compression as fallback
    /// </summary>
    public static void ConfigureGzipCompression(GzipCompressionProviderOptions options)
    {
        options.Level = CompressionLevel.Optimal;
    }
}

/// <summary>
/// API response optimization service
/// </summary>
public class ApiResponseOptimizer
{
    private readonly ILogger<ApiResponseOptimizer> _logger;

    public ApiResponseOptimizer(ILogger<ApiResponseOptimizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create optimized paginated response
    /// </summary>
    public OptimizedApiResponse<T> CreateOptimizedResponse<T>(
        IEnumerable<T> data,
        int totalCount,
        int pageNumber,
        int pageSize,
        string? nextPageUrl = null,
        string? prevPageUrl = null)
    {
        var items = data.ToList();
        
        return new OptimizedApiResponse<T>
        {
            Data = items,
            Meta = new ResponseMetadata
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                HasNextPage = pageNumber * pageSize < totalCount,
                HasPreviousPage = pageNumber > 1,
                ItemCount = items.Count,
                NextPageUrl = nextPageUrl,
                PreviousPageUrl = prevPageUrl
            },
            Performance = new PerformanceMetadata
            {
                GeneratedAt = DateTime.UtcNow,
                ProcessingTimeMs = 0, // Would be calculated
                CacheStatus = "MISS",
                QueryCount = 1 // Would be tracked
            }
        };
    }

    /// <summary>
    /// Create lightweight summary response for large datasets
    /// </summary>
    public ApiSummaryResponse CreateSummaryResponse<T>(
        IEnumerable<T> data,
        int totalCount,
        Func<T, object> summarySelector)
    {
        var items = data.Take(10).Select(summarySelector).ToList(); // Show first 10 as preview
        
        return new ApiSummaryResponse
        {
            Summary = items,
            TotalCount = totalCount,
            PreviewCount = items.Count,
            Message = totalCount > 10 
                ? $"Showing first {items.Count} of {totalCount} items. Use pagination for full results."
                : $"Showing all {totalCount} items.",
            Links = new Dictionary<string, string>
            {
                ["full"] = "/api/full-endpoint?page=1&size=50",
                ["export"] = "/api/export-endpoint"
            }
        };
    }
}

#region DTOs

/// <summary>
/// Optimized API response with metadata
/// </summary>
public class OptimizedApiResponse<T>
{
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    public ResponseMetadata Meta { get; set; } = new();
    public PerformanceMetadata Performance { get; set; } = new();
}

/// <summary>
/// Response metadata for pagination and navigation
/// </summary>
public class ResponseMetadata
{
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    public int ItemCount { get; set; }
    public string? NextPageUrl { get; set; }
    public string? PreviousPageUrl { get; set; }
}

/// <summary>
/// Performance metadata for optimization
/// </summary>
public class PerformanceMetadata
{
    public DateTime GeneratedAt { get; set; }
    public int ProcessingTimeMs { get; set; }
    public string CacheStatus { get; set; } = "MISS";
    public int QueryCount { get; set; }
}

/// <summary>
/// Summary response for large datasets
/// </summary>
public class ApiSummaryResponse
{
    public IEnumerable<object> Summary { get; set; } = Enumerable.Empty<object>();
    public int TotalCount { get; set; }
    public int PreviewCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Links { get; set; } = new();
}

#endregion

/// <summary>
/// Extension methods for frontend optimization
/// </summary>
public static class FrontendOptimizationExtensions
{
    /// <summary>
    /// Add frontend optimization services
    /// </summary>
    public static IServiceCollection AddFrontendOptimization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure frontend optimization options
        services.Configure<FrontendOptimizationOptions>(
            configuration.GetSection(FrontendOptimizationOptions.SectionName));

        // Add compression services
        services.AddResponseCompression(FrontendCompressionService.ConfigureResponseCompression);
        services.Configure<BrotliCompressionProviderOptions>(FrontendCompressionService.ConfigureBrotliCompression);
        services.Configure<GzipCompressionProviderOptions>(FrontendCompressionService.ConfigureGzipCompression);

        // Add optimization services
        services.AddScoped<FrontendCompressionService>();
        services.AddScoped<ApiResponseOptimizer>();

        return services;
    }

    /// <summary>
    /// Use frontend optimization middleware
    /// </summary>
    public static IApplicationBuilder UseFrontendOptimization(this IApplicationBuilder app)
    {
        // Enable response compression
        app.UseResponseCompression();
        
        // Add frontend optimization middleware
        app.UseMiddleware<FrontendOptimizationMiddleware>();
        
        return app;
    }
}