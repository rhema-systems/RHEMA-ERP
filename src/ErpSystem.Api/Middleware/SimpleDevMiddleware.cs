using System.Diagnostics;

namespace ErpSystem.Web.Middleware
{
    public class SimpleDevMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SimpleDevMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public SimpleDevMiddleware(
            RequestDelegate next,
            ILogger<SimpleDevMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_environment.IsDevelopment())
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogDebug("[{RequestId}] {Method} {Path} - Start",
                requestId, context.Request.Method, context.Request.Path);

            // Set up to add response time header when response starts
            context.Response.OnStarting(() =>
            {
                try
                {
                    if (!context.Response.Headers.ContainsKey("X-Response-Time-Ms"))
                    {
                        context.Response.Headers["X-Response-Time-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
                    }
                }
                catch
                {
                    // Ignore header setting errors
                }
                return Task.CompletedTask;
            });

            try
            {
                await _next(context);

                _logger.LogDebug("[{RequestId}] {Method} {Path} - {StatusCode} - {ElapsedMs}ms",
                    requestId, context.Request.Method, context.Request.Path,
                    context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                stopwatch.Stop();
            }
        }
    }

    public class DevSecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;

        public DevSecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // Add security headers if not already present
                if (!headers.ContainsKey("X-Content-Type-Options"))
                {
                    headers.XContentTypeOptions = "nosniff";
                }

                if (!headers.ContainsKey("X-Frame-Options"))
                {
                    headers["X-Frame-Options"] = "DENY";
                }

                if (!headers.ContainsKey("X-XSS-Protection"))
                {
                    headers["X-XSS-Protection"] = "1; mode=block";
                }

                if (!headers.ContainsKey("Referrer-Policy"))
                {
                    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                }

                // In development, add some debugging headers
                if (_environment.IsDevelopment())
                {
                    headers["X-Environment"] = "Development";
                    headers["X-Debug"] = "true";
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
