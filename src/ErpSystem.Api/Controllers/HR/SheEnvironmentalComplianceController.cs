using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Part D environmental core registers (slice 17): the environmental permit
/// and licence register (FR-ENV-017–019, document versions on the central DMS
/// via the controlled-upload gate), monitoring schedules (FR-ENV-023–024),
/// the regulatory updates register (FR-ENV-030–032 / FR-SHE-182) and
/// sustainability initiatives (FR-ENV-028–029). HR-gated end to end — these
/// are governance registers, not employee self-service.
/// </summary>
[ApiController]
[Route("api/safety/environmental")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheEnvironmentalComplianceController : SheApiControllerBase
{
    private readonly ISheEnvironmentalPermitService _permits;
    private readonly ISheEnvironmentalGovernanceService _governance;

    public SheEnvironmentalComplianceController(
        ISheEnvironmentalPermitService permits,
        ISheEnvironmentalGovernanceService governance,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _permits = permits;
        _governance = governance;
    }

    // ── Permit & licence register (FR-ENV-017–019) ──
    [HttpGet("permits")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalPermitSummaryDto>>> GetPermits(
        [FromQuery] SheEnvironmentalPermitStatus? status,
        [FromQuery] SheEnvironmentalPermitType? type,
        [FromQuery] string? search,
        [FromQuery] int? expiringInDays)
        => Ok(await _permits.GetAllAsync(status, type, search, expiringInDays));

    [HttpGet("permits/search")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<IActionResult> SearchPermits([FromQuery] string? search = null, [FromQuery] int take = 5)
    {
        var term = search?.Trim() ?? string.Empty;
        if (term.Length < 2 || term.Length > 100)
            return Ok(Array.Empty<object>());

        var permits = await _permits.GetAllAsync(search: term);
        return Ok(permits.Take(Math.Clamp(take, 1, 20)).Select(permit => new
        {
            permit.Id,
            permit.RegisterNumber,
            permit.PermitName,
            Status = permit.StatusName,
        }).ToArray());
    }

    [HttpGet("permits/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> GetPermit(Guid id)
        => Ok(await _permits.GetByIdAsync(id));

    [HttpPost("permits")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> CreatePermit([FromBody] CreateSheEnvironmentalPermitDto dto)
    {
        var created = await _permits.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetPermit), new { id = created.Id }, created);
    }

    [HttpPut("permits/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> UpdatePermit(Guid id, [FromBody] UpdateSheEnvironmentalPermitDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _permits.UpdateAsync(dto, UserId));
    }

    /// <summary>Rows without an uploaded document only — a documented permit archives, never deletes.</summary>
    [HttpDelete("permits/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeletePermit(Guid id)
    {
        await _permits.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("permits/{id:guid}/mark-renewal")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> MarkPermitRenewal(Guid id)
        => Ok(await _permits.MarkRenewalInProgressAsync(id, UserId));

    [HttpPost("permits/{id:guid}/renew")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> RenewPermit(Guid id, [FromBody] RenewSheEnvironmentalPermitDto dto)
        => Ok(await _permits.RenewAsync(id, dto, UserId));

    [HttpPost("permits/{id:guid}/suspend")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> SuspendPermit(Guid id)
        => Ok(await _permits.SuspendAsync(id, UserId));

    [HttpPost("permits/{id:guid}/archive")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> ArchivePermit(Guid id)
        => Ok(await _permits.ArchiveAsync(id, UserId));

    /// <summary>
    /// Uploads the permit document through the controlled-upload gate. Refused
    /// with the gate's own {code, message} contract for rejected files (type,
    /// size, quota, scan), and 422 on an archived permit.
    /// </summary>
    [HttpPost("permits/{id:guid}/document")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalPermitDto>> UploadPermitDocument(
        Guid id, IFormFile? file, [FromForm] string? changeSummary, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (!Guid.TryParse(CurrentUser.UserId, out var actorUserId))
            return Unauthorized("User context could not be resolved");

        try
        {
            var updated = await _permits.UploadDocumentAsync(new SheEnvironmentalPermitDocumentUpload
            {
                PermitId = id,
                ActorUserId = actorUserId,
                ActorName = CurrentUser.UserName,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                OpenReadStream = file.OpenReadStream,
                ChangeSummary = changeSummary,
            }, UserId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, updated);
        }
        catch (ControlledFileUploadException ex)
        {
            // The gate's own {code, message} contract — a refused file is not a server fault.
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    /// <summary>Streams a permit document version's bytes; refuses anything without a clean scan verdict.</summary>
    [HttpGet("permits/{id:guid}/document/{versionId:guid}/download")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<IActionResult> DownloadPermitDocument(
        Guid id, Guid versionId,
        [FromServices] ICentralDocumentRepositoryFileService centralDocuments,
        [FromServices] IFileStorageService storage,
        [FromServices] ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        // Resolves the version against the permit row (tenant + record match)
        // before anything is served — that is this endpoint's entitlement check.
        var fileInfo = await _permits.GetDocumentFileAsync(id, versionId, cancellationToken);

        return await HrDocumentDownload.ServeAsync(
            this, centralDocuments, storage, db,
            TenantId,
            fileInfo.DocumentRecordId,
            fileInfo.VersionId,
            fileInfo.FileUploadRecordId,
            legacyPath: null,
            fallbackFileName: fileInfo.FileName,
            fallbackContentType: fileInfo.ContentType,
            inline: false,
            cancellationToken);
    }

    // ── Monitoring schedules (FR-ENV-023–024) ──
    [HttpGet("monitoring/schedules")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringScheduleDto>>> GetSchedules(
        [FromQuery] bool? activeOnly,
        [FromQuery] SheEnvironmentalMonitoringType? type,
        [FromQuery] int? dueInDays)
        => Ok(await _governance.GetSchedulesAsync(activeOnly, type, dueInDays));

    [HttpGet("monitoring/schedules/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheEnvironmentalMonitoringScheduleDto>> GetSchedule(Guid id)
        => Ok(await _governance.GetScheduleAsync(id));

    [HttpPost("monitoring/schedules")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalMonitoringScheduleDto>> CreateSchedule([FromBody] CreateSheEnvironmentalMonitoringScheduleDto dto)
    {
        var created = await _governance.CreateScheduleAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetSchedule), new { id = created.Id }, created);
    }

    [HttpPut("monitoring/schedules/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalMonitoringScheduleDto>> UpdateSchedule(Guid id, [FromBody] UpdateSheEnvironmentalMonitoringScheduleDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _governance.UpdateScheduleAsync(dto, UserId));
    }

    /// <summary>Schedules without linked records only — a schedule with history deactivates instead.</summary>
    [HttpDelete("monitoring/schedules/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        await _governance.DeleteScheduleAsync(id);
        return NoContent();
    }

    /// <summary>Records a completed cycle and advances the next due date by the schedule's interval.</summary>
    [HttpPost("monitoring/schedules/{id:guid}/complete")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheEnvironmentalMonitoringScheduleDto>> CompleteScheduleCycle(Guid id, [FromBody] CompleteSheMonitoringScheduleDto dto)
        => Ok(await _governance.CompleteScheduleCycleAsync(id, dto, UserId));

    // ── Regulatory updates register (FR-ENV-030–032 / FR-SHE-182) ──
    [HttpGet("regulatory-updates")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheRegulatoryUpdateSummaryDto>>> GetRegulatoryUpdates(
        [FromQuery] SheRegulatoryUpdateStatus? status,
        [FromQuery] SheRegulatoryDomain? domain,
        [FromQuery] string? search)
        => Ok(await _governance.GetRegulatoryUpdatesAsync(status, domain, search));

    [HttpGet("regulatory-updates/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheRegulatoryUpdateDto>> GetRegulatoryUpdate(Guid id)
        => Ok(await _governance.GetRegulatoryUpdateAsync(id));

    [HttpPost("regulatory-updates")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheRegulatoryUpdateDto>> CreateRegulatoryUpdate([FromBody] CreateSheRegulatoryUpdateDto dto)
    {
        var created = await _governance.CreateRegulatoryUpdateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetRegulatoryUpdate), new { id = created.Id }, created);
    }

    [HttpPut("regulatory-updates/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheRegulatoryUpdateDto>> UpdateRegulatoryUpdate(Guid id, [FromBody] UpdateSheRegulatoryUpdateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _governance.UpdateRegulatoryUpdateAsync(dto, UserId));
    }

    /// <summary>Undecided rows only — a closed or management-communicated update is history.</summary>
    [HttpDelete("regulatory-updates/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteRegulatoryUpdate(Guid id)
    {
        await _governance.DeleteRegulatoryUpdateAsync(id);
        return NoContent();
    }

    /// <summary>FR-ENV-031 — one-shot management notification through the escalated topic.</summary>
    [HttpPost("regulatory-updates/{id:guid}/notify-management")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheRegulatoryUpdateDto>> NotifyManagement(Guid id)
        => Ok(await _governance.NotifyManagementAsync(id, UserId));

    [HttpPost("regulatory-updates/{id:guid}/close")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheRegulatoryUpdateDto>> CloseRegulatoryUpdate(Guid id, [FromBody] CloseSheRegulatoryUpdateDto dto)
        => Ok(await _governance.CloseRegulatoryUpdateAsync(id, dto, UserId));

    // ── Sustainability initiatives (FR-ENV-028–029) ──
    [HttpGet("sustainability")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheSustainabilityInitiativeDto>>> GetInitiatives(
        [FromQuery] SheSustainabilityCategory? category,
        [FromQuery] SheSustainabilityStatus? status)
        => Ok(await _governance.GetInitiativesAsync(category, status));

    [HttpGet("sustainability/kpis")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheSustainabilityKpiDto>> GetSustainabilityKpis([FromQuery] int? year)
        => Ok(await _governance.GetSustainabilityKpisAsync(year));

    [HttpGet("sustainability/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheSustainabilityInitiativeDto>> GetInitiative(Guid id)
        => Ok(await _governance.GetInitiativeAsync(id));

    [HttpPost("sustainability")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheSustainabilityInitiativeDto>> CreateInitiative([FromBody] CreateSheSustainabilityInitiativeDto dto)
    {
        var created = await _governance.CreateInitiativeAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetInitiative), new { id = created.Id }, created);
    }

    [HttpPut("sustainability/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheSustainabilityInitiativeDto>> UpdateInitiative(Guid id, [FromBody] UpdateSheSustainabilityInitiativeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _governance.UpdateInitiativeAsync(dto, UserId));
    }

    [HttpDelete("sustainability/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteInitiative(Guid id)
    {
        await _governance.DeleteInitiativeAsync(id);
        return NoContent();
    }
}
