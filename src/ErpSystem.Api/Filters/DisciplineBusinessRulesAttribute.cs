using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the staff-discipline services' own rule messages to the client, instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse every <see cref="InvalidOperationException"/>
/// and <see cref="ArgumentException"/> into one of two canned strings outside Development.
///
/// The discipline services raise specific, actionable messages this way — only Draft cases can be
/// submitted, a closed or dismissed case cannot be edited, a decision cannot be recorded for a case
/// in this status, only Draft cases can be deleted — none of which reached the caller before this
/// filter. Same shape and same fix as <see cref="MovementBusinessRulesAttribute"/> and
/// <see cref="SafetyBusinessRulesAttribute"/>.
///
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — the services' "not found" idiom.</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message.</description></item>
/// </list>
///
/// Applied per controller rather than globally — changing the middleware would alter the contract of
/// every module at once.
///
/// The log messages deliberately carry only the request path and the rule text. A disciplinary rule
/// rejection names a case, not the allegation, and this filter runs on the most sensitive records in
/// the module — nothing here should widen who can read the substance of a case by reading the logs.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DisciplineBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<DisciplineBusinessRulesAttribute>)) as ILogger<DisciplineBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Discipline record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("Discipline rule rejected on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogWarning("Discipline authorization refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;
        }
    }
}
