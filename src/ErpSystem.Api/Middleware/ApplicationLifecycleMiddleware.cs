using ErpSystem.Web.Services;

namespace ErpSystem.Web.Middleware
{
    public class ApplicationLifecycleMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IGracefulShutdownService _shutdownService;
        private readonly ILogger<ApplicationLifecycleMiddleware> _logger;
        private static volatile bool _shutdownInProgress = false;
        private static readonly object _shutdownLock = new object();

        public ApplicationLifecycleMiddleware(
            RequestDelegate next,
            IGracefulShutdownService shutdownService,
            ILogger<ApplicationLifecycleMiddleware> logger)
        {
            _next = next;
            _shutdownService = shutdownService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if shutdown is in progress
            if (_shutdownInProgress)
            {
                _logger.LogWarning("Request received during shutdown: {Method} {Path}", 
                    context.Request.Method, context.Request.Path);
                
                context.Response.StatusCode = 503; // Service Unavailable
                context.Response.Headers["Retry-After"] = "30";
                await context.Response.WriteAsync("Service is shutting down. Please try again later.");
                return;
            }

            // Handle shutdown signals
            if (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request cancelled for {Method} {Path}", 
                    context.Request.Method, context.Request.Path);
                context.Response.StatusCode = 499; // Client Closed Request
                return;
            }

            // Add shutdown detection
            context.RequestAborted.Register(() =>
            {
                if (!_shutdownInProgress)
                {
                    lock (_shutdownLock)
                    {
                        if (!_shutdownInProgress)
                        {
                            _shutdownInProgress = true;
                            _logger.LogInformation("Shutdown signal detected via request cancellation");
                            
                            // Trigger graceful shutdown in background
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _shutdownService.BeginShutdownAsync();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error during graceful shutdown initiation");
                                }
                            });
                        }
                    }
                }
            });

            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request operation cancelled for {Method} {Path}", 
                    context.Request.Method, context.Request.Path);
                context.Response.StatusCode = 499;
            }
        }

        public static bool IsShutdownInProgress => _shutdownInProgress;

        public static void SetShutdownInProgress()
        {
            lock (_shutdownLock)
            {
                _shutdownInProgress = true;
            }
        }
    }

    public static class ApplicationLifecycleMiddlewareExtensions
    {
        public static IApplicationBuilder UseApplicationLifecycleManagement(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApplicationLifecycleMiddleware>();
        }
    }
}