using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the HR Finance posting services' own rule messages to the client, instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse them into a canned string outside Development.
/// Same shape as <see cref="StaffTravelBusinessRulesAttribute"/>: a refusal that cannot say why is
/// still a defect, and here the "why" is usually Finance's ("Posting period is not open",
/// "One or more posting accounts are not enabled for the requested accounting book") — exactly
/// what the desk must relay to Finance.
/// </summary>
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — a record that does not exist or belongs to another tenant, indistinguishable by design.</description></item>
///   <item><description><see cref="InvalidOperationException"/> (including <c>HrFinancePostingException</c>) → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message.</description></item>
/// </list>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class HrFinancePostingBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<HrFinancePostingBusinessRulesAttribute>)) as ILogger<HrFinancePostingBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("HR Finance posting record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("HR Finance posting rule rejected on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogWarning("HR Finance posting authorization refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;
        }
    }
}
