using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/finance/financial-statement-layouts")]
public sealed class FinancialStatementLayoutsController : ControllerBase
{
    private const long MaximumWorkbookBytes = 5 * 1024 * 1024;
    private readonly IFinancialStatementLayoutService _service;
    private readonly IFinancialStatementLayoutExecutionService _executionService;
    private readonly IFinancialStatementLayoutImportService _importService;

    public FinancialStatementLayoutsController(
        IFinancialStatementLayoutService service,
        IFinancialStatementLayoutExecutionService executionService,
        IFinancialStatementLayoutImportService importService)
    {
        _service = service;
        _executionService = executionService;
        _importService = importService;
    }

    [HttpGet]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<FinancialStatementLayoutSummaryDto>>> GetLayouts(
        [FromQuery] FinancialStatementType? statementType = null,
        [FromQuery] Guid? accountingBookId = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var layouts = await _service.GetLayoutsAsync(
            statementType,
            accountingBookId,
            includeInactive,
            cancellationToken);
        return Ok(layouts);
    }

    [HttpGet("{layoutId:guid}")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<FinancialStatementLayoutDto>> GetLayout(
        Guid layoutId,
        CancellationToken cancellationToken = default)
    {
        var layout = await _service.GetLayoutAsync(layoutId, cancellationToken);
        return layout == null ? NotFound() : Ok(layout);
    }

    [HttpGet("{layoutId:guid}/audit-trail")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<IReadOnlyList<FinancialStatementLayoutAuditEventDto>>> GetAuditTrail(
        Guid layoutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetAuditTrailAsync(layoutId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutDto>> CreateLayout(
        [FromBody] CreateFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var layout = await _service.CreateLayoutAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetLayout), new { layoutId = layout.Id }, layout);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{layoutId:guid}")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutDto>> UpdateLayout(
        Guid layoutId,
        [FromBody] UpdateFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.UpdateLayoutAsync(layoutId, request, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{layoutId:guid}/versions")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutVersionDto>> CreateDraftVersion(
        Guid layoutId,
        [FromBody] CreateFinancialStatementLayoutVersionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await _service.CreateDraftVersionAsync(
                layoutId,
                request,
                cancellationToken);
            return Ok(version);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{layoutId:guid}/clone")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutDto>> CloneProtectedStandard(
        Guid layoutId,
        [FromBody] CloneFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var layout = await _service.CloneProtectedStandardAsync(layoutId, request, cancellationToken);
            return CreatedAtAction(nameof(GetLayout), new { layoutId = layout.Id }, layout);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("versions/{versionId:guid}/rows")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutVersionDto>> ReplaceDraftRows(
        Guid versionId,
        [FromBody] ReplaceFinancialStatementRowsDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.ReplaceDraftRowsAsync(
                versionId,
                request,
                cancellationToken));
        }
        catch (FinancialStatementLayoutValidationException exception)
        {
            return BadRequest(exception.Validation);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("versions/{versionId:guid}/validate")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutValidationResultDto>> ValidateVersion(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.ValidateVersionAsync(versionId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPost("versions/{versionId:guid}/publish")]
    [Authorize(Policy = FinancePermissions.PublishFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutVersionDto>> PublishVersion(
        Guid versionId,
        [FromBody] PublishFinancialStatementLayoutVersionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.PublishVersionAsync(
                versionId,
                request,
                cancellationToken));
        }
        catch (FinancialStatementLayoutValidationException exception)
        {
            return BadRequest(exception.Validation);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("versions/{versionId:guid}/preview")]
    [Authorize(Policy = FinancePermissions.RunFinanceReports)]
    public async Task<ActionResult<FinancialStatementLayoutExecutionDto>> PreviewVersion(
        Guid versionId,
        [FromBody] FinancialStatementLayoutPreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _executionService.PreviewVersionAsync(
                versionId,
                request,
                cancellationToken));
        }
        catch (FinancialStatementLayoutValidationException exception)
        {
            return BadRequest(exception.Validation);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("execute")]
    [Authorize(Policy = FinancePermissions.RunFinanceReports)]
    public async Task<ActionResult<FinancialStatementLayoutExecutionDto>> ExecutePublished(
        [FromBody] FinancialStatementLayoutExecutionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _executionService.ExecutePublishedAsync(
                request,
                cancellationToken));
        }
        catch (FinancialStatementLayoutValidationException exception)
        {
            return BadRequest(exception.Validation);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("import-template")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<IActionResult> DownloadImportTemplate(
        CancellationToken cancellationToken = default)
    {
        var file = await _importService.CreateWorkbookTemplateAsync(
            cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("versions/{versionId:guid}/exports/json")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<ActionResult<FinancialStatementLayoutImportDefinitionDto>> ExportJson(
        Guid versionId, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _importService.ExportDefinitionAsync(versionId, cancellationToken)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpGet("versions/{versionId:guid}/exports/workbook")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> ExportWorkbook(
        Guid versionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _importService.ExportWorkbookAsync(versionId, cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("imports/json/preview")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutImportPreviewDto>>
        PreviewJsonImport(
            [FromBody] FinancialStatementLayoutImportDefinitionDto request,
            CancellationToken cancellationToken = default)
        => Ok(await _importService.PreviewDefinitionAsync(
            request,
            cancellationToken));

    [HttpPost("imports/json/commit")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutImportResultDto>>
        CommitJsonImport(
            [FromBody] FinancialStatementLayoutImportCommitDto request,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _importService.CommitDefinitionAsync(
                request,
                cancellationToken));
        }
        catch (FinancialStatementLayoutValidationException exception)
        {
            return BadRequest(exception.Validation);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("imports/workbook/preview")]
    [RequestSizeLimit(MaximumWorkbookBytes)]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutImportPreviewDto>>
        PreviewWorkbookImport(
            IFormFile file,
            CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Select a non-empty .xlsx workbook."
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _importService.PreviewWorkbookAsync(
                stream,
                file.FileName,
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("imports/workbook/commit")]
    [RequestSizeLimit(MaximumWorkbookBytes)]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutImportResultDto>>
        CommitWorkbookImport(
            IFormFile file,
            [FromForm] string expectedDefinitionHash,
            CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Select a non-empty .xlsx workbook."
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _importService.CommitWorkbookAsync(
                stream,
                file.FileName,
                expectedDefinitionHash,
                cancellationToken));
        }
        catch (FinancialStatementLayoutValidationException exception)
        {
            return BadRequest(exception.Validation);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("legacy-migration/preview")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutImportPreviewDto>>
        PreviewLegacyMigration(
            [FromBody] LegacyFinancialStatementLayoutMigrationRequestDto request,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _importService.PreviewLegacyMigrationAsync(
                request,
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("legacy-migration/commit")]
    [Authorize(Policy = FinancePermissions.ManageFinancialStatementLayouts)]
    public async Task<ActionResult<FinancialStatementLayoutImportResultDto>>
        CommitLegacyMigration(
            [FromBody] LegacyFinancialStatementLayoutMigrationCommitDto request,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _importService.CommitLegacyMigrationAsync(
                request,
                cancellationToken));
        }
        catch (FinancialStatementLayoutValidationException exception)
        {
            return BadRequest(exception.Validation);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
