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
[Route("api/safety/emergency")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheEmergencyController : SheApiControllerBase
{
    private readonly ISheEmergencyService _service;

    public SheEmergencyController(ISheEmergencyService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Plans ──
    [HttpGet("plans")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyPlanSummaryDto>>> GetAllPlans()
        => Ok(await _service.GetAllPlansAsync());

    [HttpGet("plans/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<EmergencyPlanDto>> GetPlan(Guid id)
        => Ok(await _service.GetPlanAsync(id));

    [HttpGet("plans/number/{planNumber}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<EmergencyPlanDto?>> GetPlanByNumber(string planNumber)
        => Ok(await _service.GetPlanByNumberAsync(planNumber));

    [HttpGet("plans/type/{type}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyPlanSummaryDto>>> GetPlansByType(SheEmergencyType type)
        => Ok(await _service.GetPlansByTypeAsync(type));

    [HttpGet("plans/active")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyPlanSummaryDto>>> GetActivePlans()
        => Ok(await _service.GetActivePlansAsync());

    [HttpGet("plans/due-for-review")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyPlanSummaryDto>>> GetPlansDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetPlansDueForReviewAsync(daysAhead));

    [HttpPost("plans")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyPlanDto>> CreatePlan([FromBody] CreateEmergencyPlanDto dto)
    {
        var created = await _service.CreatePlanAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetPlan), new { id = created.Id }, created);
    }

    [HttpPut("plans/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyPlanDto>> UpdatePlan(Guid id, [FromBody] UpdateEmergencyPlanDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdatePlanAsync(dto, UserId));
    }

    [HttpDelete("plans/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        await _service.DeletePlanAsync(id);
        return NoContent();
    }

    // ── Assembly points ──
    [HttpPost("plans/{id:guid}/assembly-points")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheAssemblyPointDto>> AddAssemblyPoint(Guid id, [FromBody] CreateSheAssemblyPointDto dto)
    {
        dto.EmergencyPlanId = id;
        return Ok(await _service.AddAssemblyPointAsync(dto, TenantId, UserId));
    }

    [HttpPut("assembly-points/{assemblyPointId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheAssemblyPointDto>> UpdateAssemblyPoint(Guid assemblyPointId, [FromBody] UpdateSheAssemblyPointDto dto)
    {
        if (assemblyPointId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAssemblyPointAsync(dto, UserId));
    }

    [HttpDelete("assembly-points/{assemblyPointId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteAssemblyPoint(Guid assemblyPointId)
    {
        await _service.DeleteAssemblyPointAsync(assemblyPointId);
        return NoContent();
    }

    // ── Emergency contacts ──
    [HttpPost("plans/{id:guid}/contacts")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyContactDto>> AddContact(Guid id, [FromBody] CreateEmergencyContactDto dto)
    {
        dto.EmergencyPlanId = id;
        return Ok(await _service.AddContactAsync(dto, TenantId, UserId));
    }

    [HttpPut("contacts/{contactId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyContactDto>> UpdateContact(Guid contactId, [FromBody] UpdateEmergencyContactDto dto)
    {
        if (contactId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateContactAsync(dto, UserId));
    }

    [HttpDelete("contacts/{contactId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteContact(Guid contactId)
    {
        await _service.DeleteContactAsync(contactId);
        return NoContent();
    }

    // ── Drills ──
    [HttpGet("plans/{id:guid}/drills")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyDrillDto>>> GetDrillsForPlan(Guid id)
        => Ok(await _service.GetDrillsForPlanAsync(id));

    [HttpGet("drills/upcoming")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyDrillDto>>> GetUpcomingDrills([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetUpcomingDrillsAsync(daysAhead));

    [HttpPost("plans/{id:guid}/drills")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyDrillDto>> AddDrill(Guid id, [FromBody] CreateEmergencyDrillDto dto)
    {
        dto.EmergencyPlanId = id;
        return Ok(await _service.AddDrillAsync(dto, TenantId, UserId));
    }

    [HttpPut("drills/{drillId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyDrillDto>> UpdateDrill(Guid drillId, [FromBody] UpdateEmergencyDrillDto dto)
    {
        if (drillId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateDrillAsync(dto, UserId));
    }

    [HttpDelete("drills/{drillId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteDrill(Guid drillId)
    {
        await _service.DeleteDrillAsync(drillId);
        return NoContent();
    }

    // ── Response team ──
    [HttpGet("team/expiring-certificates")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyResponseTeamDto>>> GetExpiringTeamCertificates([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringTeamCertificatesAsync(daysAhead));

    [HttpGet("team/by-employee/{employeeId:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmergencyResponseTeamDto>>> GetTeamMembershipsByEmployee(Guid employeeId)
        => Ok(await _service.GetTeamMembershipsByEmployeeAsync(employeeId));

    [HttpPost("plans/{id:guid}/team")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyResponseTeamDto>> AddTeamMember(Guid id, [FromBody] CreateEmergencyResponseTeamDto dto)
    {
        dto.EmergencyPlanId = id;
        return Ok(await _service.AddTeamMemberAsync(dto, TenantId, UserId));
    }

    [HttpPut("team/{teamMemberId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<EmergencyResponseTeamDto>> UpdateTeamMember(Guid teamMemberId, [FromBody] UpdateEmergencyResponseTeamDto dto)
    {
        if (teamMemberId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateTeamMemberAsync(dto, UserId));
    }

    [HttpDelete("team/{teamMemberId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteTeamMember(Guid teamMemberId)
    {
        await _service.DeleteTeamMemberAsync(teamMemberId);
        return NoContent();
    }
}
