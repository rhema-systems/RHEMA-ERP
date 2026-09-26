using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the team-activity service's own rule messages to the client, instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse every <see cref="InvalidOperationException"/>
/// into the canned "The operation is not valid for the current state of the object."
///
/// The rules this carries are the whole point of the sub-module and every one of them is a sentence
/// the user can act on: approved terms of reference cannot be edited, take a new version; an
/// objective at 60 % needs an outcome summary before it can be completed; a blocked task must say
/// what is blocking it; that person is not a member of this team; this task is not assigned to you.
///
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — the services' "not found" idiom.</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message.</description></item>
/// </list>
///
/// ⚠ The tenth filter of this exact shape in the codebase, and the mapping is identical to the
/// other nine on purpose. Round-2 lane D2 measured the cost of the alternative: the employee
/// controllers hand-roll their own mapping and answer <b>400</b> where every filtered area answers
/// <b>422</b>, so the same rule gets two status codes depending on which door it is raised behind.
/// A new controller joins the majority.
///
/// Applied per controller rather than globally — changing the middleware would alter the contract of
/// every module at once.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class TeamActivityBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<TeamActivityBusinessRulesAttribute>)) as ILogger<TeamActivityBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Team activity record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("Team activity rule rejected on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogWarning("Team activity authorization refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;
        }
    }
}
