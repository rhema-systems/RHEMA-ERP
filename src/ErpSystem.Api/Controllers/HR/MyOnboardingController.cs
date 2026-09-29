using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Onboarding, as the employee sees it (round 4, lane K-b2): their own plan as a new hire, the tasks
/// given to them on anybody's plan — with Mark done — and the new hires they are buddy to.
/// </summary>
/// <remarks>
/// <para>The self-scoped read <c>OnboardingPlanController</c> said belonged here and deliberately did
/// not offer. It was built when the onboarding notices landed, so that "you have been given a task"
/// leads somewhere the reader can open — before this, a task's assignee could neither see nor finish it
/// unless they were HR.</para>
///
/// <para>The employee is always the caller, from the token. A task given to somebody else is "not
/// found", never "forbidden": the door does not confirm a colleague's task exists.</para>
/// </remarks>
[ApiController]
[OrientationBusinessRules]
[Route("api/employee-portal/onboarding")]
[Authorize(Policy = "InternalOnly")]
public class MyOnboardingController : ControllerBase
{
    private readonly IOnboardingPlanService _service;
    private readonly ICurrentUserService _currentUser;

    public MyOnboardingController(IOnboardingPlanService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private IActionResult NoEmployee() =>
        Problem(
            detail:     "Your account is not linked to an employee record. Please contact HR.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    /// <summary>The caller's onboarding: their plan, their tasks, and whom they are buddy to.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid employeeId) return NoEmployee();
        return Ok(await _service.GetMyOnboardingAsync(employeeId, ct));
    }

    /// <summary>Marks a task given to the caller done. A task needing sign-off waits for it.</summary>
    [HttpPost("tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteMine(
        Guid taskId, [FromBody] CompleteMyOnboardingTaskDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId) return NoEmployee();
        return Ok(await _service.CompleteMyTaskAsync(taskId, employeeId, dto, ct));
    }
}
