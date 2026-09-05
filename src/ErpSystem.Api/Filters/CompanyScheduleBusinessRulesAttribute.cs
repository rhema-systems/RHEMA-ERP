using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the company-schedule services' own rule messages to the client, instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse every <see cref="InvalidOperationException"/>
/// and <see cref="ArgumentException"/> into one of two canned strings outside Development.
/// </summary>
/// <remarks>
/// <para>
/// Added 2026-08-28, when the harness caught it. Creating a meeting room with a duplicate code
/// answered <c>"The operation is not valid for the current state of the object."</c> — the service
/// had said <c>"Room code 'PRB1' already exists for this tenant."</c> and the middleware threw that
/// away. A rule that fires correctly but cannot explain itself leaves the user staring at a form
/// that will not save, with nothing to act on.
/// </para>
/// <para>
/// Same shape and same mapping as <see cref="MovementBusinessRulesAttribute"/>,
/// <see cref="SafetyBusinessRulesAttribute"/> and <see cref="TrainingBusinessRulesAttribute"/>:
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — the services' "not found" idiom.</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message.</description></item>
/// </list>
/// Applied per controller rather than globally — changing the middleware would alter the contract
/// of every module at once.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompanyScheduleBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<CompanyScheduleBusinessRulesAttribute>))
            as ILogger<CompanyScheduleBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Company schedule record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogInformation("Company schedule rule refused {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogInformation("Company schedule access refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = 403 };
                context.ExceptionHandled = true;
                break;
        }
    }
}
