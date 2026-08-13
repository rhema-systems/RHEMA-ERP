using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/training")]
[SafetyBusinessRules]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class SheTrainingController : SheApiControllerBase
{
    private readonly ISheTrainingService _service;

    public SheTrainingController(ISheTrainingService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Plans ──
    [HttpGet("plans/{id:guid}")]
    public async Task<ActionResult<SheTrainingPlanDto>> GetPlan(Guid id)
        => Ok(await _service.GetPlanAsync(id));

    [HttpGet("plans/year/{year:int}")]
    public async Task<ActionResult<IEnumerable<SheTrainingPlanDto>>> GetPlansByYear(int year)
        => Ok(await _service.GetPlansByYearAsync(year));

    [HttpGet("plans/status/{status}")]
    public async Task<ActionResult<IEnumerable<SheTrainingPlanDto>>> GetPlansByStatus(SheTrainingPlanStatus status)
        => Ok(await _service.GetPlansByStatusAsync(status));

    [HttpPost("plans")]
    public async Task<ActionResult<SheTrainingPlanDto>> CreatePlan([FromBody] CreateSheTrainingPlanDto dto)
    {
        var created = await _service.CreatePlanAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetPlan), new { id = created.Id }, created);
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<SheTrainingPlanDto>> UpdatePlan(Guid id, [FromBody] UpdateSheTrainingPlanDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdatePlanAsync(dto, UserId));
    }

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        await _service.DeletePlanAsync(id);
        return NoContent();
    }

    // ── Programs ──
    [HttpGet("programs/{id:guid}")]
    public async Task<ActionResult<SheTrainingProgramDto>> GetProgram(Guid id)
        => Ok(await _service.GetProgramAsync(id));

    [HttpGet("plans/{planId:guid}/programs")]
    public async Task<ActionResult<IEnumerable<SheTrainingProgramSummaryDto>>> GetProgramsByPlan(Guid planId)
        => Ok(await _service.GetProgramsByPlanAsync(planId));

    [HttpGet("programs/status/{status}")]
    public async Task<ActionResult<IEnumerable<SheTrainingProgramSummaryDto>>> GetProgramsByStatus(SheTrainingStatus status)
        => Ok(await _service.GetProgramsByStatusAsync(status));

    [HttpGet("programs/category/{category}")]
    public async Task<ActionResult<IEnumerable<SheTrainingProgramSummaryDto>>> GetProgramsByCategory(SheTrainingCategory category)
        => Ok(await _service.GetProgramsByCategoryAsync(category));

    [HttpGet("programs/upcoming")]
    public async Task<ActionResult<IEnumerable<SheTrainingProgramSummaryDto>>> GetUpcomingPrograms([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetUpcomingProgramsAsync(daysAhead));

    [HttpPost("programs")]
    public async Task<ActionResult<SheTrainingProgramDto>> CreateProgram([FromBody] CreateSheTrainingProgramDto dto)
    {
        var created = await _service.CreateProgramAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetProgram), new { id = created.Id }, created);
    }

    [HttpPut("programs/{id:guid}")]
    public async Task<ActionResult<SheTrainingProgramDto>> UpdateProgram(Guid id, [FromBody] UpdateSheTrainingProgramDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateProgramAsync(dto, UserId));
    }

    [HttpDelete("programs/{id:guid}")]
    public async Task<IActionResult> DeleteProgram(Guid id)
    {
        await _service.DeleteProgramAsync(id);
        return NoContent();
    }

    [HttpPost("programs/{id:guid}/evaluate")]
    public async Task<IActionResult> EvaluateProgram(Guid id, [FromBody] EvaluateSheTrainingProgramDto dto)
    {
        dto.ProgramId = id;
        await _service.EvaluateProgramAsync(dto, UserId);
        return Ok(new { message = "Training program evaluation recorded." });
    }

    // ── Attendance ──
    [HttpGet("programs/{programId:guid}/attendances")]
    public async Task<ActionResult<IEnumerable<SheTrainingAttendanceDto>>> GetAttendances(Guid programId)
        => Ok(await _service.GetAttendancesByProgramAsync(programId));

    [HttpGet("attendances/by-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SheTrainingAttendanceDto>>> GetAttendancesByEmployee(Guid employeeId)
        => Ok(await _service.GetAttendancesByEmployeeAsync(employeeId));

    [HttpGet("attendances/expiring-certificates")]
    public async Task<ActionResult<IEnumerable<SheTrainingAttendanceDto>>> GetExpiringCertificates([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringCertificatesAsync(daysAhead));

    [HttpPost("programs/{programId:guid}/attendances")]
    public async Task<ActionResult<SheTrainingAttendanceDto>> AddAttendance(Guid programId, [FromBody] CreateSheTrainingAttendanceDto dto)
    {
        dto.ProgramId = programId;
        return Ok(await _service.AddAttendanceAsync(dto, TenantId, UserId));
    }

    [HttpPut("attendances/{attendanceId:guid}")]
    public async Task<ActionResult<SheTrainingAttendanceDto>> UpdateAttendance(Guid attendanceId, [FromBody] UpdateSheTrainingAttendanceDto dto)
    {
        if (attendanceId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAttendanceAsync(dto, UserId));
    }

    [HttpDelete("attendances/{attendanceId:guid}")]
    public async Task<IActionResult> DeleteAttendance(Guid attendanceId)
    {
        await _service.DeleteAttendanceAsync(attendanceId);
        return NoContent();
    }
}
