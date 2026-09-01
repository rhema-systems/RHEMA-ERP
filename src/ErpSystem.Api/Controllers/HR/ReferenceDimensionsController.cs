using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The reference dimensions lane 3b introduced: the qualification ladder, the certifying-body
/// catalogue, and per-register staff-numbering rules.
/// </summary>
/// <remarks>
/// Gated on the employee ladder — read is Read, write is Write, delete is Admin — matching the
/// identification-type and qualification lookups these sit beside.
/// </remarks>
[ApiController]
[Route("api/hr/reference")]
[Authorize(Policy = "InternalOnly")]
public class ReferenceDimensionsController : ControllerBase
{
    private readonly IReferenceDimensionService _service;
    private readonly IStaffNumberService _staffNumbers;
    private readonly ILogger<ReferenceDimensionsController> _logger;

    public ReferenceDimensionsController(
        IReferenceDimensionService service,
        IStaffNumberService staffNumbers,
        ILogger<ReferenceDimensionsController> logger)
    {
        _service = service;
        _staffNumbers = staffNumbers;
        _logger = logger;
    }

    /// <remarks>
    /// ⚠ Surfaces the rule's own message. Without this the middleware maps
    /// <see cref="InvalidOperationException"/> to a fixed string and DISCARDS it, so "this format
    /// produces the same number as X" would reach the screen as a generic error.
    /// </remarks>
    private ActionResult Rejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Reference-data rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    // ── Qualification levels ────────────────────────────────────────────────

    [HttpGet("qualification-levels")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<QualificationLevelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<QualificationLevelDto>>> GetQualificationLevels(
        [FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await _service.GetQualificationLevelsAsync(activeOnly, ct));

    [HttpPost("qualification-levels")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(QualificationLevelDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<QualificationLevelDto>> CreateQualificationLevel(
        [FromBody] CreateQualificationLevelDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return StatusCode(StatusCodes.Status201Created, await _service.CreateQualificationLevelAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a qualification level"); }
    }

    [HttpPut("qualification-levels/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(QualificationLevelDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QualificationLevelDto>> UpdateQualificationLevel(
        Guid id, [FromBody] UpdateQualificationLevelDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateQualificationLevelAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a qualification level"); }
    }

    [HttpDelete("qualification-levels/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteQualificationLevel(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteQualificationLevelAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a qualification level"); }
    }

    // ── Certifying bodies ───────────────────────────────────────────────────

    [HttpGet("certifying-bodies")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertifyingBodyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertifyingBodyDto>>> GetCertifyingBodies(
        [FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await _service.GetCertifyingBodiesAsync(activeOnly, ct));

    [HttpPost("certifying-bodies")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertifyingBodyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CertifyingBodyDto>> CreateCertifyingBody(
        [FromBody] CreateCertifyingBodyDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return StatusCode(StatusCodes.Status201Created, await _service.CreateCertifyingBodyAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a certifying body"); }
    }

    [HttpPut("certifying-bodies/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertifyingBodyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CertifyingBodyDto>> UpdateCertifyingBody(
        Guid id, [FromBody] UpdateCertifyingBodyDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateCertifyingBodyAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a certifying body"); }
    }

    [HttpDelete("certifying-bodies/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCertifyingBody(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteCertifyingBodyAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a certifying body"); }
    }

    // ── Staff number formats ────────────────────────────────────────────────

    /// <remarks>
    /// ⚠ An EMPTY list is meaningful, not an unconfigured screen: with no rule, every register is
    /// numbered by hand. The screen must say so rather than showing an empty table.
    /// </remarks>
    [HttpGet("staff-number-formats")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<StaffNumberFormatDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StaffNumberFormatDto>>> GetStaffNumberFormats(CancellationToken ct)
        => Ok(await _service.GetStaffNumberFormatsAsync(ct));

    /// <summary>What a format would produce, before it is saved.</summary>
    [HttpPost("staff-number-formats/preview")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> PreviewStaffNumberFormat([FromBody] PreviewStaffNumberFormatDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(new { example = _service.PreviewStaffNumberFormat(dto) });
    }

    [HttpPost("staff-number-formats")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(StaffNumberFormatDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<StaffNumberFormatDto>> CreateStaffNumberFormat(
        [FromBody] CreateStaffNumberFormatDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return StatusCode(StatusCodes.Status201Created, await _service.CreateStaffNumberFormatAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a staff number format"); }
    }

    [HttpPut("staff-number-formats/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(StaffNumberFormatDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffNumberFormatDto>> UpdateStaffNumberFormat(
        Guid id, [FromBody] UpdateStaffNumberFormatDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateStaffNumberFormatAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a staff number format"); }
    }

    /// <remarks>
    /// Removing a rule returns that register to manual entry — the absence of a rule IS the manual
    /// setting. Numbers already issued are unaffected: they live on the employee, not on the rule.
    /// </remarks>
    [HttpDelete("staff-number-formats/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteStaffNumberFormat(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteStaffNumberFormatAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a staff number format"); }
    }

    // ── The counter behind a rule ───────────────────────────────────────────
    //
    // ⚠ A numbering rule is only half the story. The rule says what a number LOOKS like; the counter
    // says which one comes next, and it knows only about numbers it issued itself. Load a register
    // from anywhere else — SQL, a seeder, a restored database, the import below — and the counter is
    // still at zero while the register holds thousands. Nothing surfaces that until a hire fails on
    // the unique index, so these two endpoints exist to make it readable and fixable in advance.

    /// <summary>Where a rule's counter stands against the numbers already in the register.</summary>
    [HttpGet("staff-number-formats/{id:guid}/counter")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(StaffNumberCounterStateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffNumberCounterStateDto>> GetStaffNumberCounter(Guid id, CancellationToken ct)
    {
        try { return Ok(await _staffNumbers.InspectCounterAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Moves the counter past every number in the register the rule could reissue.</summary>
    /// <remarks>
    /// Forward-only and idempotent, so running it twice is safe and running it on a clean register
    /// does nothing. Admin-gated: it changes what number the next person hired will be given.
    /// </remarks>
    [HttpPost("staff-number-formats/{id:guid}/counter/reconcile")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(StaffNumberCounterStateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffNumberCounterStateDto>> ReconcileStaffNumberCounter(Guid id, CancellationToken ct)
    {
        try { return Ok(await _staffNumbers.ReconcileCounterAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "reconciling a staff number counter"); }
    }
}
