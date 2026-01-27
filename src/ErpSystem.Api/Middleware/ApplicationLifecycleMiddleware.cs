using ErpSystem.Web.Services;

namespace ErpSystem.Web.Middleware
{
    public class ApplicationLifecycleMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApplicationLifecycleMiddleware> _logger;

        public ApplicationLifecycleMiddleware(
            RequestDelegate next,
            ILogger<ApplicationLifecycleMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Simple request logging
            _logger.LogDebug("Processing request: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request operation cancelled for {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                // Let the request complete normally - don't set status code
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
