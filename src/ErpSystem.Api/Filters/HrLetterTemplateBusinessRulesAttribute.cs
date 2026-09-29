using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Filters;

/// <summary>
/// Surfaces the HR letter-template service's own messages (round 4, lane N) instead of letting
/// <c>GlobalExceptionHandlingMiddleware</c> collapse them into canned strings — the same shape as
/// <see cref="OrientationBusinessRulesAttribute"/>. A save refused for naming a token the email does not
/// supply must SAY which token; a refusal that cannot explain itself leaves the author guessing.
/// <list type="bullet">
///   <item><description><see cref="ArgumentException"/> → <b>404</b> — no such email.</description></item>
///   <item><description><see cref="InvalidOperationException"/> → <b>422</b> with every reason.</description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class HrLetterTemplateBusinessRulesAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetService(typeof(ILogger<HrLetterTemplateBusinessRulesAttribute>)) as ILogger<HrLetterTemplateBusinessRulesAttribute>;

        switch (context.Exception)
        {
            case ArgumentException ex:
                logger?.LogInformation("Letter template not found on {Path}: {Message}", context.HttpContext.Request.Path, ex.Message);
                context.Result = new NotFoundObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;

            case InvalidOperationException ex:
                logger?.LogWarning("Letter template refused on {Path}: {Message}", context.HttpContext.Request.Path, ex.Message);
                context.Result = new UnprocessableEntityObjectResult(new { message = ex.Message });
                context.ExceptionHandled = true;
                break;
        }
    }
}
