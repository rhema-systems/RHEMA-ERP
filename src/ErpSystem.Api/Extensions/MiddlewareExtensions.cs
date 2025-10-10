using ErpSystem.Api.Middleware;

namespace ErpSystem.Api.Extensions
{
    /// <summary>
    /// Extension methods for registering custom middleware in the pipeline
    /// </summary>
    public static class MiddlewareExtensions
    {
        /// <summary>
        /// Adds global exception handling middleware to the pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for chaining</returns>
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        }

        /// <summary>
        /// Adds security headers middleware to the pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for chaining</returns>
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            return app.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}