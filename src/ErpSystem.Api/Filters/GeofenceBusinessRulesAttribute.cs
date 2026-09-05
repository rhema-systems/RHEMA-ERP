using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the geofence-zone service's own rule messages to the client, instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse every <see cref="ArgumentException"/> into
/// "Invalid argument provided." outside Development.
/// </summary>
/// <remarks>
/// <para>
/// Added 2026-09-03 with polygon parsing. The service now refuses a polygon that does not parse,
/// has fewer than three corners, or has a corner outside the latitude/longitude range, and says
/// which. Without this filter the register showed "Invalid argument provided." for all three, and
/// the user was left guessing which of a few thousand characters of JSON was wrong.
/// </para>
/// <para>
/// Unlike <see cref="CompanyScheduleBusinessRulesAttribute"/>, <see cref="ArgumentException"/> maps
/// to <b>400</b> here, not 404: the zone service uses it for validation as well as for "not found",
/// and the global middleware already answered 400 for it, so only the message changes.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class GeofenceBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<GeofenceBusinessRulesAttribute>))
            as ILogger<GeofenceBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Geofence zone request refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new BadRequestObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogInformation("Geofence zone rule refused {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogInformation("Geofence zone access refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = 403 };
                context.ExceptionHandled = true;
                break;
        }
    }
}
