using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/mentoring")]
[Authorize]
public class MentoringController : ControllerBase
{
    private readonly IMentoringService _service;
    private readonly ICurrentUserService _currentUser;

    // Simple request model for closing a mentoring pair
    public sealed record ClosePairRequest(string ClosureNotes);

    public MentoringController(IMentoringService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // MENTORING PROGRAMS
    // =========================================================================

    [HttpGet("programs")]
    public async Task<ActionResult<IEnumerable<MentoringProgramSummaryDto>>> GetAllPrograms(CancellationToken ct)
        => Ok(await _service.GetAllProgramsAsync(ct));

    [HttpGet("programs/active")]
    public async Task<ActionResult<IEnumerable<MentoringProgramSummaryDto>>> GetActivePrograms(CancellationToken ct)
        => Ok(await _service.GetActiveProgramsAsync(ct));

    [HttpGet("programs/{id:guid}")]
    public async Task<ActionResult<MentoringProgramDto>> GetProgramById(Guid id, CancellationToken ct)
        => Ok(await _service.GetProgramByIdAsync(id, ct));

    [HttpPost("programs")]
    public async Task<ActionResult<MentoringProgramDto>> CreateProgram([FromBody] CreateMentoringProgramDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateProgramAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetProgramById), new { id = created.Id }, created);
    }

    [HttpPut("programs/{id:guid}")]
    public async Task<ActionResult<MentoringProgramDto>> UpdateProgram(Guid id, [FromBody] UpdateMentoringProgramDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateProgramAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("programs/{id:guid}")]
    public async Task<IActionResult> DeleteProgram(Guid id, CancellationToken ct)
    {
        await _service.DeleteProgramAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // MENTORING PAIRS
    // =========================================================================

    [HttpGet("pairs/{id:guid}")]
    public async Task<ActionResult<MentoringPairDto>> GetPairById(Guid id, CancellationToken ct)
        => Ok(await _service.GetPairByIdAsync(id, ct));

    [HttpGet("programs/{programId:guid}/pairs")]
    public async Task<ActionResult<IEnumerable<MentoringPairSummaryDto>>> GetPairsForProgram(Guid programId, CancellationToken ct)
        => Ok(await _service.GetPairsForProgramAsync(programId, ct));

    [HttpGet("pairs/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<MentoringPairSummaryDto>>> GetPairsForEmployee(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetPairsForEmployeeAsync(employeeId, ct));

    [HttpGet("pairs/active")]
    public async Task<ActionResult<IEnumerable<MentoringPairSummaryDto>>> GetActivePairs(CancellationToken ct)
        => Ok(await _service.GetActivePairsAsync(ct));

    [HttpPost("pairs")]
    public async Task<ActionResult<MentoringPairDto>> CreatePair([FromBody] CreateMentoringPairDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreatePairAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetPairById), new { id = created.Id }, created);
    }

    [HttpPut("pairs/{id:guid}")]
    public async Task<ActionResult<MentoringPairDto>> UpdatePair(Guid id, [FromBody] UpdateMentoringPairDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdatePairAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("pairs/{id:guid}/close")]
    public async Task<IActionResult> ClosePair(Guid id, [FromBody] ClosePairRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ClosePairAsync(id, request.ClosureNotes, employeeId.Value, ct);
        return Ok(new { message = "Mentoring pair closed." });
    }

    // =========================================================================
    // MENTORING SESSIONS
    // =========================================================================

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<MentoringSessionDto>> GetSessionById(Guid id, CancellationToken ct)
        => Ok(await _service.GetSessionByIdAsync(id, ct));

    [HttpGet("pairs/{pairId:guid}/sessions")]
    public async Task<ActionResult<IEnumerable<MentoringSessionDto>>> GetSessionsForPair(Guid pairId, CancellationToken ct)
        => Ok(await _service.GetSessionsForPairAsync(pairId, ct));

    [HttpPost("sessions")]
    public async Task<ActionResult<MentoringSessionDto>> LogSession([FromBody] CreateMentoringSessionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.LogSessionAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetSessionById), new { id = created.Id }, created);
    }

    [HttpPut("sessions/{id:guid}")]
    public async Task<ActionResult<MentoringSessionDto>> UpdateSession(Guid id, [FromBody] UpdateMentoringSessionDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateSessionAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken ct)
    {
        await _service.DeleteSessionAsync(id, ct);
        return NoContent();
    }
}
