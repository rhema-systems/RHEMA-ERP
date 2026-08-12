using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErpSystem.Api.HealthChecks;

/// <summary>
/// Produces a stable, operator-friendly health response without exposing exception stack traces or
/// connection details. The same shape is used by aggregate, readiness, and liveness endpoints so
/// monitoring and support tooling do not need endpoint-specific parsers.
/// </summary>
public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            observedAtUtc = DateTimeOffset.UtcNow,
            durationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    // Some third-party checks place endpoint or provider exception text in the
                    // description as well as Exception. Public probe responses therefore expose
                    // the helpful success description but use a stable failure message; detailed
                    // failures remain in protected application logs for support staff.
                    description = entry.Value.Status == HealthStatus.Healthy
                        ? entry.Value.Description
                        : "Health check did not pass.",
                    durationMilliseconds = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                    tags = entry.Value.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray()
                })
                .ToArray()
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
