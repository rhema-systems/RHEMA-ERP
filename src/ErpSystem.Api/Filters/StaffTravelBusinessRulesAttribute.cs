using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the staff-travel services' own rule messages to the client, instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse every <see cref="InvalidOperationException"/>
/// and <see cref="ArgumentException"/> into one of two canned strings outside Development.
///
/// The travel services already raise specific, actionable messages — only draft or returned
/// requests can be submitted, only submitted requests can be approved, a request in status X
/// cannot be cancelled, only draft requests can be deleted (cancel it instead) — and **none of
/// them reached the caller**. Every refusal arrived as
/// <c>400 "The operation is not valid for the current state of the object."</c>, which tells the
/// user that something is wrong and nothing about what to do next. Measured in slice 1: five
/// refusals, all correct, all mute.
///
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — the services' "not found" idiom, used for a
///   missing record and for one belonging to another tenant, which must be indistinguishable.</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message.</description></item>
///   <item><description><see cref="DbUpdateException"/> → <b>409</b> with a fixed sentence (added
///   2026-10-01, travel final closure lane 0). A unique-index collision — a claim or advance number
///   issued twice after a delete is the known case (finding B9, fixed in lane 3) — reached the client
///   as a bare 500 naming nothing. The database's own text is logged, never returned: it names
///   tables and indexes.</description></item>
/// </list>
///
/// Applied per controller rather than globally — changing the middleware would alter the contract
/// of every module at once. Same shape as <see cref="MovementBusinessRulesAttribute"/> and
/// <see cref="DisciplineBusinessRulesAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class StaffTravelBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<StaffTravelBusinessRulesAttribute>)) as ILogger<StaffTravelBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Staff travel record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("Staff travel rule rejected on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogWarning("Staff travel authorization refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;

            case DbUpdateException ex:
                logger?.LogWarning(ex, "Staff travel save conflicted on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.InnerException?.Message ?? ex.Message);
                context.Result = new ConflictObjectResult(new
                {
                    message = "This could not be saved because it conflicts with a record that already exists — " +
                              "for example a number that has already been issued. Refresh and try again; if it " +
                              "happens again, tell your system administrator.",
                });
                context.ExceptionHandled = true;
                break;
        }
    }
}
