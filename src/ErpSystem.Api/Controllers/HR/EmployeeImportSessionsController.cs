using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Employee bulk import: download the template, upload a filled one, review what the checker found,
/// commit, watch progress, collect the follow-up list.
/// </summary>
/// <remarks>
/// <para><b>Write-gated, not admin-gated</b> (decided 2026-09-03). Creating and amending employee
/// records is what the write tier means in the permission catalogue, and the HR role holds write
/// but not admin — admin-gating hid the screen from every HR desk user and the demo's head of HR.
/// The single-record <c>POST api/hr/Employees/import</c> stays admin-gated on the argument that
/// accepting a staff number as given bypasses the numbering rule; in a manual register (TDC's) an
/// HR officer types the number with write anyway, and the bulk path advances the counter past every
/// number it loads, so the rule is not bypassed here.</para>
/// <para>See <c>docs/HR/HR-EMPLOYEE-IMPORT-DESIGN.md</c>.</para>
/// </remarks>
[ApiController]
[Route("api/hr/employees/import-sessions")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
public sealed class EmployeeImportSessionsController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IEmployeeImportService _service;
    private readonly ILogger<EmployeeImportSessionsController> _logger;

    public EmployeeImportSessionsController(IEmployeeImportService service, ILogger<EmployeeImportSessionsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>The template, with this tenant's live reference sheets.</summary>
    [HttpGet("template")]
    public async Task<IActionResult> Template(CancellationToken ct)
    {
        var bytes = await _service.GenerateTemplateAsync(ct);
        return File(bytes, XlsxContentType, $"Employee Import Template {DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    /// <summary>The column catalogue, for the guide page.</summary>
    [HttpGet("columns")]
    [ProducesResponseType(typeof(List<EmployeeImportColumnGuideDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmployeeImportColumnGuideDto>>> Columns(CancellationToken ct)
        => Ok(await _service.GetColumnGuideAsync(ct));

    /// <summary>
    /// Upload a filled template. Checks every row; writes nothing to the register.
    /// <paramref name="mode"/> decides what a staff number already in the register means: an error
    /// (CreateOnly, the default), an update of that employee (UpdateOnly), or either (CreateOrUpdate).
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(15_728_640)]
    [ProducesResponseType(typeof(EmployeeImportSessionSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeImportSessionSummaryDto>> Upload(
        IFormFile? file, [FromForm] EmployeeImportMode? mode, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Select a filled employee import template (.xlsx)." });
        if (mode.HasValue && !Enum.IsDefined(mode.Value))
            return BadRequest(new { message = "Mode must be CreateOnly, UpdateOnly or CreateOrUpdate." });

        try
        {
            await using var stream = file.OpenReadStream();
            var session = await _service.CreateSessionAsync(
                stream, file.FileName, file.ContentType, mode ?? EmployeeImportMode.CreateOnly, ct);
            return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
        }
        catch (EmployeeImportFileRejectedException ex)
        {
            return BadRequest(new { message = ex.Message, findings = ex.Findings });
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<EmployeeImportSessionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmployeeImportSessionSummaryDto>>> List(CancellationToken ct)
        => Ok(await _service.ListSessionsAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeImportSessionSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeImportSessionSummaryDto>> Get(Guid id, CancellationToken ct)
    {
        var session = await _service.GetSessionAsync(id, ct);
        return session == null ? NotFound(new { message = "Import session not found." }) : Ok(session);
    }

    /// <summary>Rows with their findings, paged; filter by outcome and search by staff number or name.</summary>
    [HttpGet("{id:guid}/rows")]
    [ProducesResponseType(typeof(EmployeeImportRowPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeImportRowPageDto>> Rows(
        Guid id, [FromQuery] EmployeeImportRowOutcome? outcome, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _service.GetRowsAsync(id, outcome, search, page, pageSize, ct));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Take a row out of the commit, or put it back.</summary>
    [HttpPatch("{id:guid}/rows/{rowId:guid}/skip")]
    [ProducesResponseType(typeof(EmployeeImportRowDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeImportRowDto>> Skip(Guid id, Guid rowId, [FromBody] EmployeeImportSkipRowDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.SetRowSkipAsync(id, rowId, dto.Skip, ct));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>The uploaded workbook with a Result column and a comment on every problem cell.</summary>
    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, CancellationToken ct)
    {
        try
        {
            var session = await _service.GetSessionAsync(id, ct);
            if (session == null) return NotFound(new { message = "Import session not found." });
            var bytes = await _service.BuildAnnotatedWorkbookAsync(id, ct);
            return File(bytes, XlsxContentType, $"{session.Reference} checked.xlsx");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Confirm the commit. The background committer writes the rows; poll <c>progress</c>.</summary>
    [HttpPost("{id:guid}/commit")]
    [ProducesResponseType(typeof(EmployeeImportSessionSummaryDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeImportSessionSummaryDto>> Commit(Guid id, [FromBody] EmployeeImportCommitRequestDto? dto, CancellationToken ct)
    {
        try
        {
            var session = await _service.RequestCommitAsync(id, dto?.Policy ?? EmployeeImportCommitPolicy.ValidRowsOnly, ct);
            return Accepted(session);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpGet("{id:guid}/progress")]
    [ProducesResponseType(typeof(EmployeeImportProgressDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeImportProgressDto>> Progress(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.GetProgressAsync(id, ct));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(EmployeeImportSessionSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeImportSessionSummaryDto>> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.CancelSessionAsync(id, ct));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>The employees this import created whose profiles still need completing.</summary>
    [HttpGet("{id:guid}/follow-up")]
    [ProducesResponseType(typeof(List<EmployeeImportFollowUpDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FollowUp(Guid id, [FromQuery] string? format, CancellationToken ct)
    {
        try
        {
            if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
            {
                var session = await _service.GetSessionAsync(id, ct);
                if (session == null) return NotFound(new { message = "Import session not found." });
                var bytes = await _service.BuildFollowUpWorkbookAsync(id, ct);
                return File(bytes, XlsxContentType, $"{session.Reference} profiles to complete.xlsx");
            }
            return Ok(await _service.GetFollowUpAsync(id, ct));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    private ActionResult ToClientError(Exception ex)
    {
        if (ex is ArgumentException && ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = ex.Message });
        _logger.LogDebug(ex, "Employee import request refused");
        return BadRequest(new { message = ex.Message });
    }
}
