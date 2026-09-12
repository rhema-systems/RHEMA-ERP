using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the Training &amp; Learning services' own rule messages to the client, instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse every <see cref="InvalidOperationException"/> and
/// <see cref="ArgumentException"/> into one of two canned strings outside Development. The Training
/// services raise dozens of specific, useful messages this way (schedule full, bond already exists,
/// nomination not in the right status for this transition, etc.) that would otherwise never reach the
/// caller — same shape and same fix as <see cref="RecruitmentBusinessRulesAttribute"/>.
///
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — the services' "not found" idiom (<c>GetOwnedXAsync</c>).</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message.</description></item>
/// </list>
///
/// Applied per controller rather than globally — same reasoning as the recruitment filter: changing
/// the middleware would alter the contract of every module at once.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class TrainingBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<TrainingBusinessRulesAttribute>)) as ILogger<TrainingBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Training record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("Training rule rejected on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogWarning("Training authorization refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;
        }
    }
}
