using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<SecurityHeadersOptions> options,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var response = context.Response;
        var headers = response.Headers;

        try
        {
            // HSTS (HTTP Strict Transport Security)
            if (_options.EnableHsts && context.Request.IsHttps)
            {
                var hstsValue = _options.HstsIncludeSubdomains 
                    ? $"max-age={_options.HstsMaxAge}; includeSubDomains" 
                    : $"max-age={_options.HstsMaxAge}";
                
                if (_options.HstsPreload)
                    hstsValue += "; preload";

                headers["Strict-Transport-Security"] = hstsValue;
            }

            // Content Security Policy
            if (!string.IsNullOrEmpty(_options.ContentSecurityPolicy))
            {
                headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;
            }

            // X-Frame-Options
            if (!string.IsNullOrEmpty(_options.XFrameOptions))
            {
                headers["X-Frame-Options"] = _options.XFrameOptions;
            }

            // X-Content-Type-Options
            if (_options.EnableNoSniff)
            {
                headers["X-Content-Type-Options"] = "nosniff";
            }

            // X-XSS-Protection
            if (_options.EnableXssProtection)
            {
                headers["X-XSS-Protection"] = "1; mode=block";
            }

            // Referrer-Policy
            if (!string.IsNullOrEmpty(_options.ReferrerPolicy))
            {
                headers["Referrer-Policy"] = _options.ReferrerPolicy;
            }

            // Permissions-Policy
            if (!string.IsNullOrEmpty(_options.PermissionsPolicy))
            {
                headers["Permissions-Policy"] = _options.PermissionsPolicy;
            }

            // X-Permitted-Cross-Domain-Policies
            if (_options.EnableCrossDomainPolicy)
            {
                headers["X-Permitted-Cross-Domain-Policies"] = "none";
            }

            // Remove server header for security
            if (_options.RemoveServerHeader)
            {
                headers.Remove("Server");
            }

            // Custom security headers
            foreach (var customHeader in _options.CustomHeaders)
            {
                headers[customHeader.Key] = customHeader.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding security headers");
        }
    }
}

public class SecurityHeadersOptions
{
    public bool EnableHsts { get; set; } = true;
    public int HstsMaxAge { get; set; } = 31536000; // 1 year
    public bool HstsIncludeSubdomains { get; set; } = true;
    public bool HstsPreload { get; set; } = false;

    public string ContentSecurityPolicy { get; set; } = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https:; " +
        "style-src 'self' 'unsafe-inline' https:; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' https:; " +
        "connect-src 'self' https: wss: ws:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    public string XFrameOptions { get; set; } = "DENY";
    public bool EnableNoSniff { get; set; } = true;
    public bool EnableXssProtection { get; set; } = true;
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
    
    public string PermissionsPolicy { get; set; } = 
        "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    public bool EnableCrossDomainPolicy { get; set; } = true;
    public bool RemoveServerHeader { get; set; } = true;

    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

// Extension method for easy registration
public static class SecurityHeadersExtensions
{
    public static IServiceCollection AddSecurityHeaders(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.Configure<SecurityHeadersOptions>(
            configuration.GetSection("SecurityHeaders"));
        
        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}