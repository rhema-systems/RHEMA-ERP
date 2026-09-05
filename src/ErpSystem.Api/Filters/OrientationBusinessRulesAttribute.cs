using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the Orientation &amp; Onboarding services' own rule messages to the client, instead of
/// letting <c>GlobalExceptionHandlingMiddleware</c> collapse every <see cref="InvalidOperationException"/>
/// and <see cref="ArgumentException"/> into one of two canned strings outside Development. Same shape
/// and same fix as <see cref="TrainingBusinessRulesAttribute"/> and
/// <see cref="RecruitmentBusinessRulesAttribute"/>; area 15 was ported without one.
///
/// The messages this recovers are the whole point of the rules. Without it, an HR user who tries to
/// sign off a task they completed themselves is told "The operation is not valid for the current
/// state of the object" — a refusal that fires correctly and cannot explain itself is still a defect,
/// because the user is left staring at a rejected action with no idea what to do about it. The same
/// applies to "an active program cannot be deleted", "the selected session is full and does not allow
/// a waitlist", "this employee is already enrolled in the program" and the rest.
///
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — the services' "not found" idiom (<c>GetOwnedXAsync</c>).</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message — the record-level entitlement checks.</description></item>
/// </list>
///
/// Applied per controller rather than globally, for the same reason as the other two: changing the
/// middleware would alter the contract of every module at once.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class OrientationBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<OrientationBusinessRulesAttribute>)) as ILogger<OrientationBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Orientation record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("Orientation rule rejected on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogWarning("Orientation authorization refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;
        }
    }
}
