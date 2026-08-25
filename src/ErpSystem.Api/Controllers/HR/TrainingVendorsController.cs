using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-vendors")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingVendorsController : ControllerBase
{
    private readonly ITrainingVendorService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingVendorsController(ITrainingVendorService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingVendorSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingVendorSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingVendorDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("code/{vendorCode}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingVendorDto?>> GetByVendorCode(string vendorCode, CancellationToken ct)
        => Ok(await _service.GetByVendorCodeAsync(vendorCode, ct));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingVendorSummaryDto>>> GetActive(CancellationToken ct)
        => Ok(await _service.GetActiveAsync(ct));

    [HttpGet("preferred")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingVendorSummaryDto>>> GetPreferred(CancellationToken ct)
        => Ok(await _service.GetPreferredAsync(ct));

    [HttpGet("blacklisted")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingVendorSummaryDto>>> GetBlacklisted(CancellationToken ct)
        => Ok(await _service.GetBlacklistedAsync(ct));

    [HttpGet("expiring-accreditation")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingVendorSummaryDto>>> GetWithExpiringAccreditation(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetWithExpiringAccreditationAsync(daysAhead, ct));

    [HttpGet("type/{vendorType}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingVendorSummaryDto>>> GetByVendorType(TrainingVendorType vendorType, CancellationToken ct)
        => Ok(await _service.GetByVendorTypeAsync(vendorType, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingVendorDto>> Create([FromBody] CreateTrainingVendorDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingVendorDto>> Update(Guid id, [FromBody] UpdateTrainingVendorDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/blacklist")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Blacklist(Guid id, [FromBody] BlacklistVendorDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.VendorId = id;
        await _service.BlacklistVendorAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Vendor blacklisted." });
    }

    [HttpPost("{id:guid}/unblacklist")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Unblacklist(Guid id, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.UnblacklistVendorAsync(id, employeeId.Value, ct);
        return Ok(new { message = "Vendor removed from blacklist." });
    }
}
