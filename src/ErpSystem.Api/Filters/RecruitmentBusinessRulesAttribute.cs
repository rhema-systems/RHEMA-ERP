using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the recruitment services' own rule messages to the client.
///
/// <para>Without this, every business rule in the area is answered by
/// <c>GlobalExceptionHandlingMiddleware</c>, which maps <see cref="InvalidOperationException"/> to
/// <b>400</b> and replaces the message with the generic <i>"The operation is not valid for the
/// current state of the object."</i> (the real text survives only as <c>developerMessage</c>, and
/// only in Development). For most of the codebase that is tolerable. It is not here: the budget
/// enforcement on a staff requisition exists precisely to tell the requester <i>why</i> a
/// submission was refused and by how much it exceeds the approved manpower budget, and that
/// sentence was being computed, thrown, and thrown away. Likewise <see cref="ArgumentException"/>
/// — which these services raise for "not found" — was answered <b>400 "Invalid argument
/// provided."</b> rather than 404.</para>
///
/// <para>Mapping is deliberately the same as the <c>BusinessRuleRejected</c> helper the appraisal
/// controllers use, so a rule reads the same way whichever HR area raised it:
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — the services' "not found" idiom.</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with the rule's own message.</description></item>
///   <item><description><see cref="UnauthorizedAccessException"/> → <b>403</b> with its message.</description></item>
/// </list>
/// 422 rather than 409 matches <c>WorkflowError</c>'s existing <c>GoalLocked → 422</c>, so the same
/// record answers the same way on every endpoint.</para>
///
/// <para>Applied per controller rather than globally: changing the middleware would alter the
/// contract of every module at once, which is the same blast-radius reasoning that kept the tenant
/// query-filter fix surgical.</para>
///
/// <para>⚠ The copy-pasted <c>GetTenantId()</c> guard in these services throws
/// <see cref="InvalidOperationException"/> too, so a tenant-less token reads as 422 rather than
/// 500. Unreachable for an authenticated caller; the same known residue as
/// <c>EmployeeGoalsController</c>.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RecruitmentBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<RecruitmentBusinessRulesAttribute>)) as ILogger<RecruitmentBusinessRulesAttribute>;

        switch (context.Exception)
        {
            // The RHEMA-native recruitment services (ApplicationPipelineService, PositionVacancyService)
            // raise KeyNotFoundException where the ported ones raise ArgumentException. Both mean
            // "not found", so both answer 404 — otherwise the same missing id reads as 404 on one route
            // and 500 on the next depending on which service happens to be behind it.
            case KeyNotFoundException ex:
                logger?.LogInformation("Recruitment record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            // Order matters: ArgumentException is not an InvalidOperationException, but both are
            // caught before the generic middleware ever sees them.
            case ArgumentException ex:
                logger?.LogInformation("Recruitment record not found on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("Recruitment rule rejected on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                logger?.LogWarning("Recruitment authorization refused on {Path}: {Message}",
                    context.HttpContext.Request.Path, ex.Message);
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;
        }
    }
}
