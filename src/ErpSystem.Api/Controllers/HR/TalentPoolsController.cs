using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/talent-pools")]
[Authorize(Policy = HrPermissions.SuccessionReadPolicy)]
public class TalentPoolsController : ControllerBase
{
    private readonly ITalentPoolService _service;
    private readonly ICurrentUserService _currentUser;

    public TalentPoolsController(ITalentPoolService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // POOL QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<TalentPoolSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<TalentPoolSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<TalentPoolSummaryDto>>> GetActivePools()
        => Ok(await _service.GetActivePoolsAsync());

    [HttpGet("type/{poolTypeId:guid}")]
    public async Task<ActionResult<IEnumerable<TalentPoolSummaryDto>>> GetByType(Guid poolTypeId)
        => Ok(await _service.GetByPoolTypeAsync(poolTypeId));

    [HttpGet("owner/{ownerEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TalentPoolSummaryDto>>> GetByOwner(Guid ownerEmployeeId)
        => Ok(await _service.GetByOwnerAsync(ownerEmployeeId));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TalentPoolDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{id:guid}/with-members")]
    public async Task<ActionResult<TalentPoolDto>> GetWithMembers(Guid id)
        => Ok(await _service.GetWithMembersAsync(id));

    // =========================================================================
    // POOL CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<TalentPoolDto>> Create([FromBody] CreateTalentPoolDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TalentPoolDto>> Update(Guid id, [FromBody] UpdateTalentPoolDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // MEMBER QUERIES
    // =========================================================================

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IEnumerable<TalentPoolMemberSummaryDto>>> GetMembers(Guid id)
        => Ok(await _service.GetMembersAsync(id));

    [HttpGet("members/{memberId:guid}")]
    public async Task<ActionResult<TalentPoolMemberDto>> GetMemberById(Guid memberId)
        => Ok(await _service.GetMemberByIdAsync(memberId));

    [HttpGet("{id:guid}/members/readiness/{readiness}")]
    public async Task<ActionResult<IEnumerable<TalentPoolMemberSummaryDto>>> GetMembersByReadiness(Guid id, ReadinessLevel readiness)
        => Ok(await _service.GetMembersByReadinessAsync(id, readiness));

    [HttpGet("members/due-for-review")]
    public async Task<ActionResult<IEnumerable<TalentPoolMemberSummaryDto>>> GetMembersDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetMembersDueForReviewAsync(daysAhead));

    // =========================================================================
    // MEMBER CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<TalentPoolMemberDto>> AddMember(Guid id, [FromBody] CreateTalentPoolMemberDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.TalentPoolId = id;
        var created = await _service.AddMemberAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetMemberById), new { memberId = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("members/{memberId:guid}")]
    public async Task<ActionResult<TalentPoolMemberDto>> UpdateMember(Guid memberId, [FromBody] UpdateTalentPoolMemberDto dto)
    {
        if (memberId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateMemberAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("members/{memberId:guid}/remove")]
    public async Task<IActionResult> RemoveMember(Guid memberId, [FromBody] RemoveTalentPoolMemberDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RemoveMemberAsync(memberId, dto.RemovalReason, employeeId.Value);
        return Ok(new { message = "Member removed from talent pool." });
    }

    // =========================================================================
    // MEMBER DEVELOPMENT ACTIVITIES
    // =========================================================================

    [HttpGet("members/{memberId:guid}/development-activities")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivitySummaryDto>>> GetDevelopmentActivities(Guid memberId)
        => Ok(await _service.GetDevelopmentActivitiesForMemberAsync(memberId));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("members/{memberId:guid}/development-activities")]
    public async Task<ActionResult<SuccessionDevelopmentActivityDto>> AddDevelopmentActivity(
        Guid memberId, [FromBody] CreateSuccessionDevelopmentActivityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.TalentPoolMemberId = memberId;
        var created = await _service.AddDevelopmentActivityForMemberAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetDevelopmentActivities), new { memberId }, created);
    }

    // =========================================================================
    // MEMBER DOCUMENTS
    // =========================================================================

    [HttpGet("members/{memberId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<SuccessionDocumentDto>>> GetDocuments(Guid memberId)
        => Ok(await _service.GetDocumentsForMemberAsync(memberId));

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("members/{memberId:guid}/documents")]
    public async Task<ActionResult<SuccessionDocumentDto>> AddDocument(
        Guid memberId, [FromBody] CreateSuccessionDocumentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.TalentPoolMemberId = memberId;
        var created = await _service.AddDocumentForMemberAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetDocuments), new { memberId }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}

// Simple request body DTO for remove-member (defined here to avoid a separate file for a one-liner)
public sealed record RemoveTalentPoolMemberDto(string RemovalReason);
