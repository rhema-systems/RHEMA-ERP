using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The exit register (area 9b) — FRD §A1.10 <i>Separation, Clearance &amp; Exit</i> and §3.A.2
/// <i>Separation &amp; Final Settlement</i>.
/// </summary>
/// <remarks>
/// <para>One record per exit, whatever the route. Before this controller the only way out of the
/// organisation was a disciplinary case, so resignation, retirement, contract expiry and death had
/// enum members and no route. The disciplinary route writes here too — area 9 keeps owning the
/// decision and the hearing; the exit itself lives here, so the register, the clearance run and the
/// settlement are the same for a dismissal as for a resignation.</para>
///
/// <para><b>Every action carries its own gate.</b> There is no class-level <c>[Authorize]</c>,
/// because later slices must be able to open individual actions to the MD (FR-HR-092) and to
/// Internal Audit (FR-HR-185), and stacked <c>[Authorize]</c> attributes are ANDed — a class-level
/// role gate would keep applying however the method were marked. The cost of that choice is that a
/// new endpoint added here with no attribute is an <b>open</b> endpoint. Add the gate first.</para>
///
/// <para><b>There is deliberately no by-number lookup.</b> Separation numbers are sequential, so a
/// by-number endpoint would let anyone walk the range and harvest who is leaving — which is exactly
/// what an exit register must not leak. The number is a display and search field; lookups are by
/// id.</para>
/// </remarks>
[ApiController]
[Route("api/hr/separations")]
public class SeparationsController : ControllerBase
{
    private readonly ISeparationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SeparationsController> _logger;

    public SeparationsController(
        ISeparationService service,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ILogger<SeparationsController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// The acting employee, or null when the caller's login is not linked to an employee record.
    /// </summary>
    /// <remarks>
    /// Null rather than a throw: an HR officer whose user account has no employee link must still
    /// be able to raise a separation for somebody else, and recording <c>Guid.Empty</c> as the
    /// initiator would be a lie the audit trail cannot tell apart from a real one. The service
    /// distinguishes "nobody was named" from "the system raised it" via
    /// <c>IsSystemInitiated</c>.
    /// </remarks>
    private Guid? ActorEmployeeId() => _currentUser.EmployeeId;

    private ActionResult ToClientError(Exception ex) => ex switch
    {
        ArgumentException => NotFound(new { message = ex.Message }),
        InvalidOperationException => BadRequest(new { message = ex.Message }),
        _ => BadRequest(new { message = ex.Message }),
    };

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>The exit register, filtered and paged.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeSeparationListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeSeparationListDto>>> GetPaged(
        [FromQuery] EmployeeSeparationQueryDto query, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(query, cancellationToken));

    /// <summary>One separation in full.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>Every separation on record for one employee, newest first.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSeparationListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeSeparationListDto>>> GetForEmployee(
        Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest(new { message = "Invalid employee id." });
        return Ok(await _service.GetForEmployeeAsync(employeeId, cancellationToken));
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>Raise a separation. It starts as a draft; nothing is decided by creating it.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Create(
        [FromBody] CreateEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (dto == null) return BadRequest(new { message = "A separation payload is required." });

        try
        {
            var created = await _service.CreateAsync(dto, ActorEmployeeId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Amend a draft separation.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Update(
        Guid id, [FromBody] UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "An update payload is required." });

        try
        {
            return Ok(await _service.UpdateAsync(id, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Submit a draft into the approval queue, deriving the dates that follow from the notice
    /// already recorded. After this the notice facts are fixed — the settlement is computed from
    /// them.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Submit(
        Guid id, [FromBody] SubmitEmployeeSeparationDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.SubmitAsync(
                id, dto ?? new SubmitEmployeeSeparationDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── FR-HR-092: the decision ───────────────────────────────────────────────

    /// <summary>
    /// Sign off a separation awaiting approval, settling any unserved notice (FR-HR-092).
    /// </summary>
    /// <remarks>
    /// ⚠ <b>A plain <c>[Authorize]</c>, and that is deliberate.</b> Who may decide depends on the
    /// record: the Managing Director may sign any separation, HR only a procedural one — a
    /// termination for absence beyond the tenant's threshold. No permission can express "may
    /// approve this one but not that one", and stacking a role attribute onto a policy attribute
    /// would AND them and admit nobody. The service reads entitlement off the record and answers
    /// 403 with the reason.
    /// </remarks>
    [Authorize]
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Approve(
        Guid id, [FromBody] ApproveEmployeeSeparationDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.ApproveAsync(
                id, dto ?? new ApproveEmployeeSeparationDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Refuse a separation awaiting approval. Same entitlement rule as approving.</summary>
    [Authorize]
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Reject(
        Guid id, [FromBody] RejectEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "A reason is required to refuse a separation." });

        try
        {
            return Ok(await _service.RejectAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Final settlement (FR-HR-184) ──────────────────────────────────────────

    /// <summary>
    /// Build the settlement statement. Refused before clearance is complete — FR-HR-091 puts the
    /// clearance form ahead of computing entitlements.
    /// </summary>
    /// <remarks>
    /// Lines the system cannot value carry <b>no amount</b> rather than zero, and hold the
    /// statement open until somebody supplies the figure and names its source.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/settlement/prepare")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> PrepareSettlement(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.PrepareSettlementAsync(id, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>The settlement statement, its totals, and whether it can be closed for review.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}/settlement")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> GetSettlement(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.GetSettlementAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Add a line by hand — the FR-HR-184 items the system cannot work out for itself.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/settlement/lines")]
    [ProducesResponseType(typeof(SeparationSettlementLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementLineDto>> AddSettlementLine(
        Guid id, [FromBody] AddSettlementLineDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "A settlement line is required." });

        try
        {
            return Ok(await _service.AddSettlementLineAsync(id, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Amend a line while the statement is a draft — including supplying an amount the system could
    /// not compute, which is what releases the block on finalising.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPut("settlement-lines/{lineId:guid}")]
    [ProducesResponseType(typeof(SeparationSettlementLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementLineDto>> UpdateSettlementLine(
        Guid lineId, [FromBody] UpdateSettlementLineDto dto, CancellationToken cancellationToken)
    {
        if (lineId == Guid.Empty) return BadRequest(new { message = "Invalid settlement line id." });
        if (dto == null) return BadRequest(new { message = "An update payload is required." });

        try
        {
            return Ok(await _service.UpdateSettlementLineAsync(lineId, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Remove a line while the statement is a draft.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpDelete("settlement-lines/{lineId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSettlementLine(Guid lineId, CancellationToken cancellationToken)
    {
        if (lineId == Guid.Empty) return BadRequest(new { message = "Invalid settlement line id." });

        try
        {
            await _service.DeleteSettlementLineAsync(lineId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Close the statement for Internal Audit's review (FR-HR-185). Refused while any line could not
    /// be valued — a settlement is not finalised with an unknown amount showing as zero.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/settlement/finalise")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> FinaliseSettlement(
        Guid id, [FromBody] FinaliseSettlementDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.FinaliseSettlementAsync(
                id, dto ?? new FinaliseSettlementDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── FR-HR-185: Internal Audit's review ────────────────────────────────────

    /// <summary>
    /// Internal Audit passes the settlement. Payment may be released after this, and not before.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>A role gate, not a permission, and not the record.</b> Unlike the MD's signature —
    /// where who may sign depends on whether the separation is procedural — Internal Audit reviews
    /// <i>every</i> settlement. The entitled party is a job title, so it is stated as a role.
    /// SuperAdmin and TenantAdmin are excluded on the same reasoning as the MD: a technical
    /// superuser passing a financial control is exactly what the control exists to prevent.</para>
    ///
    /// <para>⚠ <b>Nobody holds this role on the live tenant</b> (measured 2026-08-20). Until it is
    /// granted, every settlement will sit unreviewed and unpaid — the control holding rather than
    /// failing open, which is correct but will look like a stuck queue. Raised with TDC as an
    /// operational prerequisite.</para>
    /// </remarks>
    [Authorize(Roles = Constants.Roles.InternalAudit)]
    [HttpPost("{id:guid}/settlement/review/approve")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> ApproveSettlementReview(
        Guid id, [FromBody] ReviewSettlementDto? dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.ApproveSettlementReviewAsync(
                id, dto ?? new ReviewSettlementDto(), ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Internal Audit returns the settlement with findings. It becomes editable again and must be
    /// corrected and re-finalised.
    /// </summary>
    [Authorize(Roles = Constants.Roles.InternalAudit)]
    [HttpPost("{id:guid}/settlement/review/return")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> ReturnSettlement(
        Guid id, [FromBody] ReviewSettlementDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "Findings are required when returning a settlement." });

        try
        {
            return Ok(await _service.ReturnSettlementAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Clearance: the tenant's form ──────────────────────────────────────────

    /// <summary>The clearance form — the catalogue every separation's checklist is built from.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("clearance-templates")]
    [ProducesResponseType(typeof(IEnumerable<SeparationClearanceTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeparationClearanceTemplateDto>>> GetClearanceTemplates(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken)
        => Ok(await _service.GetClearanceTemplatesAsync(includeInactive, cancellationToken));

    /// <summary>Add a line to the clearance form. Configuration, so administration.</summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpPost("clearance-templates")]
    [ProducesResponseType(typeof(SeparationClearanceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeparationClearanceTemplateDto>> CreateClearanceTemplate(
        [FromBody] CreateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken)
    {
        if (dto == null) return BadRequest(new { message = "A clearance line is required." });

        try
        {
            return Ok(await _service.CreateClearanceTemplateAsync(dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Amend a line of the clearance form.</summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpPut("clearance-templates/{id:guid}")]
    [ProducesResponseType(typeof(SeparationClearanceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceTemplateDto>> UpdateClearanceTemplate(
        Guid id, [FromBody] UpdateSeparationClearanceTemplateDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid clearance line id." });
        if (dto == null) return BadRequest(new { message = "An update payload is required." });

        try
        {
            return Ok(await _service.UpdateClearanceTemplateAsync(id, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Remove a line from the clearance form. Forms already issued keep their copy — items snapshot
    /// their template and hold no key to it — so this affects future clearances only. Retiring the
    /// line (<c>isActive: false</c>) is usually the better move.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpDelete("clearance-templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClearanceTemplate(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid clearance line id." });

        try
        {
            await _service.DeleteClearanceTemplateAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Create FR-HR-183's seven default lines — outstanding loans, salary advances, company
    /// property, office equipment, duty-post keys, documents and records, payroll recoveries.
    /// Skips any that already exist by name, so it is safe to run twice.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpPost("clearance-templates/seed-defaults")]
    [ProducesResponseType(typeof(IEnumerable<SeparationClearanceTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeparationClearanceTemplateDto>>> SeedDefaultClearanceTemplates(
        CancellationToken cancellationToken)
        => Ok(await _service.SeedDefaultClearanceTemplatesAsync(cancellationToken));

    // ── Clearance: one separation's form ──────────────────────────────────────

    /// <summary>Build this separation's clearance form from the active catalogue.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/clearance/start")]
    [ProducesResponseType(typeof(SeparationClearanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceDto>> StartClearance(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.StartClearanceAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>This separation's clearance form, its totals, and whether the gate can open.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}/clearance")]
    [ProducesResponseType(typeof(SeparationClearanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceDto>> GetClearance(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.GetClearanceAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Record one line's answer, and who in the owning unit gave it.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("clearance-items/{itemId:guid}")]
    [ProducesResponseType(typeof(SeparationClearanceItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationClearanceItemDto>> RecordClearanceItem(
        Guid itemId, [FromBody] RecordClearanceItemDto dto, CancellationToken cancellationToken)
    {
        if (itemId == Guid.Empty) return BadRequest(new { message = "Invalid clearance item id." });
        if (dto == null) return BadRequest(new { message = "An answer is required." });

        try
        {
            return Ok(await _service.RecordClearanceItemAsync(itemId, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Close the clearance — FR-HR-091's gate. Refused while any mandatory line is still pending or
    /// blocked, because entitlements are computed only after the form is complete.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/clearance/complete")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> CompleteClearance(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.CompleteClearanceAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    /// <summary>Every file attached to a separation, newest first.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSeparationDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeSeparationDocumentDto>>> GetDocuments(
        Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            return Ok(await _service.GetDocumentsAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Attach a file — the resignation letter, a medical report, later the signed clearance form
    /// and the settlement statement.
    /// </summary>
    /// <remarks>
    /// Goes through the controlled-upload gate, which scans it and registers it in the central
    /// repository. Entitlement is checked before storage, so an id that is not this tenant's never
    /// reaches the scanner.
    /// </remarks>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(EmployeeSeparationDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AttachDocument(
        Guid id,
        IFormFile? file,
        [FromForm] SeparationDocumentCategory category = SeparationDocumentCategory.Other,
        [FromForm] string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        // Entitlement first, storage second: an id that is not ours must 404 before a file is
        // written and scanned.
        var separation = await _service.GetByIdAsync(id, cancellationToken);
        if (separation is null) return NotFound(new { message = $"Separation '{id}' was not found." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: nameof(EmployeeSeparation),
            sourceRecordId: id,
            sourceLabel: "Separation",
            documentType: category.ToString(),
            description: description,
            persist: (uploadedById, document) => _service.AttachDocumentAsync(
                id,
                category,
                document.OriginalFileName,
                document.FilePath,
                document.FileUploadRecordId,
                document.DocumentRecordId,
                document.DocumentVersionId,
                description,
                uploadedById,
                cancellationToken),
            cancellationToken: cancellationToken,
            category: ControlledFileUploadCategories.HrSeparationDocuments);
    }

    /// <summary>Streams an attached file back, byte-for-byte.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty) return BadRequest(new { message = "Invalid document id." });
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest(new { message = "Tenant context could not be resolved." });

        var document = await _service.GetDocumentEntityAsync(documentId, cancellationToken);
        if (document is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId,
            legacyPath: document.FilePath,
            document.FileName,
            fallbackContentType: null,
            inline: false, cancellationToken);
    }

    /// <summary>
    /// Remove an attached file. Allowed only while the separation is a draft — after submission the
    /// attachments are part of what was approved.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpDelete("documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty) return BadRequest(new { message = "Invalid document id." });

        try
        {
            await _service.DeleteDocumentAsync(documentId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Withdraw a separation — a resignation retracted, a retirement deferred.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Cancel(
        Guid id, [FromBody] CancelEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "A cancellation reason is required." });

        try
        {
            return Ok(await _service.CancelAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Delete a separation. Administration, not maintenance: a completed one is refused outright,
    /// because it is the record of somebody's exit and of what they were paid.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }
}
