using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/fixed-assets")]
public class FixedAssetsController : ControllerBase
{
    private readonly IFixedAssetService _fixedAssetService;
    private readonly IFixedAssetDepreciationService _depreciationService;
    private readonly IAssetTransferService _transferService;
    private readonly IAssetDisposalService _disposalService;
    private readonly IAssetVerificationService _verificationService;
    private readonly IFixedAssetReportsService _reportsService;
    private readonly IAssetValuationService _valuationService;
    private readonly IProcurementFixedAssetCapitalizationAdapter _procurementCapitalization;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorizationService;

    public FixedAssetsController(
        IFixedAssetService fixedAssetService,
        IFixedAssetDepreciationService depreciationService,
        IAssetTransferService transferService,
        IAssetDisposalService disposalService,
        IAssetVerificationService verificationService,
        IFixedAssetReportsService reportsService,
        IAssetValuationService valuationService,
        IProcurementFixedAssetCapitalizationAdapter procurementCapitalization,
        ICurrentUserService currentUser,
        IAuthorizationService authorizationService)
    {
        _fixedAssetService = fixedAssetService;
        _depreciationService = depreciationService;
        _transferService = transferService;
        _disposalService = disposalService;
        _verificationService = verificationService;
        _reportsService = reportsService;
        _valuationService = valuationService;
        _procurementCapitalization = procurementCapitalization;
        _currentUser = currentUser;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FixedAssetDto>>> GetAll()
    {
        var result = await _fixedAssetService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<FinanceRecordSearchDto>>> Search(
        [FromQuery] string? search = null, [FromQuery] int take = 5, CancellationToken cancellationToken = default)
        => Ok(await _fixedAssetService.SearchAsync(search, take, cancellationToken));

    /// <summary>
    /// Returns active, tenant-scoped HR/Payroll organization locations through a Finance-owned
    /// read-only contract for fixed-asset forms.
    /// </summary>
    [HttpGet("location-options")]
    public async Task<ActionResult<IReadOnlyList<FixedAssetLocationOptionDto>>> GetLocationOptions(
        CancellationToken cancellationToken)
        => Ok(await _fixedAssetService.GetLocationOptionsAsync(cancellationToken));

    [HttpGet("{id}")]
    public async Task<ActionResult<FixedAssetDto>> GetById(Guid id)
    {
        var asset = await _fixedAssetService.GetByIdAsync(id);
        if (asset == null) return NotFound();
        return Ok(asset);
    }

    [HttpPost]
    public async Task<ActionResult<FixedAssetDto>> Create(CreateFixedAssetDto dto)
    {
        try
        {
            var result = await _fixedAssetService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Shows only accepted Procurement lines whose Inventory master data classifies them as fixed
    /// assets. The adapter reads approved source evidence; this endpoint does not alter Procurement.
    /// </summary>
    [HttpGet("procurement-capitalization/candidates")]
    public async Task<ActionResult<IReadOnlyList<ProcurementFixedAssetCandidateDto>>> GetProcurementCapitalizationCandidates(
        [FromQuery] Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _procurementCapitalization.GetCandidatesAsync(purchaseOrderId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("procurement-capitalizations/{capitalizationId:guid}")]
    public async Task<ActionResult<ProcurementFixedAssetCapitalizationDto>> GetProcurementCapitalization(
        Guid capitalizationId,
        CancellationToken cancellationToken)
    {
        var result = await _procurementCapitalization.GetByIdAsync(capitalizationId, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Reserves one accepted unit and creates the Finance asset draft. Existing fixed-asset
    /// approval endpoints remain authoritative before the handoff can be posted.
    /// </summary>
    [HttpPost("procurement-capitalizations")]
    public async Task<ActionResult<ProcurementFixedAssetCapitalizationDto>> CreateProcurementCapitalizationDraft(
        [FromBody] CreateProcurementFixedAssetDraftDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _procurementCapitalization.CreateDraftAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetProcurementCapitalization), new { capitalizationId = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Posts only after the existing Finance maker-checker workflow has moved the asset to
    /// Acquired. Accounting is a reclassification of the already-posted inventory carrying value.
    /// </summary>
    [HttpPost("procurement-capitalizations/{capitalizationId:guid}/post")]
    public async Task<ActionResult<ProcurementFixedAssetCapitalizationDto>> PostProcurementCapitalization(
        Guid capitalizationId,
        [FromBody] PostProcurementFixedAssetCapitalizationDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _procurementCapitalization.PostAsync(capitalizationId, dto, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FixedAssetDto>> Update(Guid id, UpdateFixedAssetDto dto)
    {
        try
        {
            var result = await _fixedAssetService.UpdateAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _fixedAssetService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/capitalize")]
    [Authorize(Policy = FinancePermissions.ManageFixedAssets)]
    public async Task<ActionResult<FixedAssetDto>> Capitalize(Guid id, [FromBody] FixedAssetApprovalActionRequest? request)
    {
        try
        {
            // Accounting values are intentionally absent here. The service reconstructs the
            // posting instruction from the immutable snapshot approved for this asset.
            var result = await _fixedAssetService.CapitalizeAsync(id, new CapitalizeFixedAssetDto
            {
                Reason = request?.Comments ?? request?.Reason ?? "Post approved direct capitalization"
            });
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/capitalization/submit")]
    [Authorize(Policy = FinancePermissions.ManageFixedAssets)]
    public async Task<ActionResult<FixedAssetDto>> SubmitCapitalizationForApproval(
        Guid id,
        [FromBody] SubmitFixedAssetCapitalizationDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _fixedAssetService.SubmitCapitalizationForApprovalAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/capitalization-reversals")]
    public async Task<ActionResult<IReadOnlyList<FixedAssetCapitalizationReversalDto>>> GetCapitalizationReversals(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _fixedAssetService.GetCapitalizationReversalsAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Captures a reasoned request without touching posted cost. An independent authorised user
    /// must review it before the central posting engine can create the linked compensating entry.
    /// </summary>
    [HttpPost("{id}/capitalization-reversals")]
    [Authorize(Policy = FinancePermissions.ReverseFixedAssetCapitalization)]
    public async Task<ActionResult<FixedAssetCapitalizationReversalDto>> RequestCapitalizationReversal(
        Guid id,
        [FromBody] RequestFixedAssetCapitalizationReversalDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _fixedAssetService.RequestCapitalizationReversalAsync(id, dto, cancellationToken);
            return CreatedAtAction(nameof(GetCapitalizationReversals), new { id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/capitalization-reversals/{requestId}/review")]
    [Authorize(Policy = FinancePermissions.ApproveFixedAssetCapitalizationReversal)]
    public async Task<ActionResult<FixedAssetCapitalizationReversalDto>> ReviewCapitalizationReversal(
        Guid id,
        Guid requestId,
        [FromBody] ReviewFixedAssetCapitalizationReversalDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _fixedAssetService.ReviewCapitalizationReversalAsync(id, requestId, dto, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/capitalization-reversals/{requestId}/post")]
    [Authorize(Policy = FinancePermissions.ReverseFixedAssetCapitalization)]
    public async Task<ActionResult<FixedAssetCapitalizationReversalDto>> PostCapitalizationReversal(
        Guid id,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _fixedAssetService.PostCapitalizationReversalAsync(id, requestId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("depreciation/run")]
    public async Task<ActionResult<IReadOnlyList<AssetDepreciationScheduleDto>>> RunDepreciation(RunDepreciationDto dto)
    {
        try
        {
            var result = await _depreciationService.RunDepreciationAsync(dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("depreciation/runs/{runId}/post-approved")]
    [Authorize(Policy = FinancePermissions.PostJournalEntries)]
    public async Task<ActionResult<IReadOnlyList<AssetDepreciationScheduleDto>>> PostApprovedDepreciationRun(Guid runId)
    {
        try
        {
            var result = await _depreciationService.PostApprovedRunAsync(runId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("depreciation/runs")]
    public async Task<ActionResult<IReadOnlyList<FixedAssetDepreciationRunDto>>> GetDepreciationRuns(
        [FromQuery] Guid? fiscalPeriodId,
        CancellationToken cancellationToken)
    {
        return Ok(await _depreciationService.GetRunsAsync(fiscalPeriodId, cancellationToken));
    }

    [HttpGet("depreciation/runs/{runId}/reversals")]
    public async Task<ActionResult<IReadOnlyList<FixedAssetDepreciationReversalDto>>> GetDepreciationReversals(
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _depreciationService.GetReversalsAsync(runId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Starts the maker-checker correction workflow without altering the posted run. The original
    /// schedules and journal remain immutable until a different authorised user approves the
    /// request and the posting endpoint creates the compensating journal.
    /// </summary>
    [HttpPost("depreciation/runs/{runId}/reversals")]
    [Authorize(Policy = FinancePermissions.ReverseFixedAssetDepreciation)]
    public async Task<ActionResult<FixedAssetDepreciationReversalDto>> RequestDepreciationReversal(
        Guid runId,
        [FromBody] RequestFixedAssetDepreciationReversalDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _depreciationService.RequestReversalAsync(runId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetDepreciationReversals), new { runId }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("depreciation/runs/{runId}/reversals/{reversalId}/review")]
    [Authorize(Policy = FinancePermissions.ApproveFixedAssetDepreciationReversal)]
    public async Task<ActionResult<FixedAssetDepreciationReversalDto>> ReviewDepreciationReversal(
        Guid runId,
        Guid reversalId,
        [FromBody] ReviewFixedAssetDepreciationReversalDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _depreciationService.ReviewReversalAsync(runId, reversalId, dto, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("depreciation/runs/{runId}/reversals/{reversalId}/post")]
    [Authorize(Policy = FinancePermissions.ReverseFixedAssetDepreciation)]
    public async Task<ActionResult<FixedAssetDepreciationReversalDto>> PostDepreciationReversal(
        Guid runId,
        Guid reversalId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _depreciationService.PostReversalAsync(runId, reversalId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/depreciation-schedule")]
    public async Task<ActionResult<IReadOnlyList<AssetDepreciationScheduleDto>>> GetAssetSchedule(Guid id)
    {
        var result = await _depreciationService.GetSchedulesForAssetAsync(id);
        return Ok(result);
    }

    [HttpGet("depreciation/period/{fiscalPeriodId}")]
    public async Task<ActionResult<IReadOnlyList<AssetDepreciationScheduleDto>>> GetPeriodSchedule(Guid fiscalPeriodId)
    {
        var result = await _depreciationService.GetSchedulesForPeriodAsync(fiscalPeriodId);
        return Ok(result);
    }

    // --- Asset Transfers ---

    [HttpGet("transfers")]
    public async Task<ActionResult<IEnumerable<AssetTransferDto>>> GetAllTransfers()
    {
        var result = await _transferService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("transfers/{id}")]
    public async Task<ActionResult<AssetTransferDto>> GetTransferById(Guid id)
    {
        var result = await _transferService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id}/transfers")]
    public async Task<ActionResult<IEnumerable<AssetTransferDto>>> GetAssetTransfers(Guid id)
    {
        var result = await _transferService.GetByAssetIdAsync(id);
        return Ok(result);
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<AssetTransferDto>> RequestTransfer(RequestAssetTransferDto dto)
    {
        if (dto.TransferType == ErpSystem.Core.Enums.AssetTransferType.GlReclassification &&
            !(await _authorizationService.AuthorizeAsync(User, FinancePermissions.ReclassifyFixedAssets)).Succeeded)
        {
            return Forbid();
        }

        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        var result = await _transferService.RequestTransferAsync(dto, employeeId);
        return CreatedAtAction(nameof(GetTransferById), new { id = result.Id }, result);
    }

    [HttpPost("transfers/{id}/approve")]
    public async Task<ActionResult<AssetTransferDto>> ApproveTransfer(Guid id, ApproveAssetTransferDto dto)
    {
        var requestedTransfer = await _transferService.GetByIdAsync(id);
        if (requestedTransfer == null) return NotFound();
        if (requestedTransfer.TransferType == ErpSystem.Core.Enums.AssetTransferType.GlReclassification &&
            !(await _authorizationService.AuthorizeAsync(User, FinancePermissions.ApproveFixedAssetReclassification)).Succeeded)
        {
            return Forbid();
        }

        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        try
        {
            var result = await _transferService.ApproveTransferAsync(id, employeeId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("transfers/{id}/reject")]
    public async Task<ActionResult<AssetTransferDto>> RejectTransfer(Guid id, ApproveAssetTransferDto dto)
    {
        var requestedTransfer = await _transferService.GetByIdAsync(id);
        if (requestedTransfer == null) return NotFound();
        if (requestedTransfer.TransferType == ErpSystem.Core.Enums.AssetTransferType.GlReclassification &&
            !(await _authorizationService.AuthorizeAsync(User, FinancePermissions.ApproveFixedAssetReclassification)).Succeeded)
        {
            return Forbid();
        }

        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        try
        {
            var result = await _transferService.RejectTransferAsync(id, employeeId, dto.Comments ?? string.Empty);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ===== DISPOSALS =====

    [HttpGet("disposals")]
    public async Task<ActionResult<IEnumerable<AssetDisposalDto>>> GetDisposals()
    {
        var result = await _disposalService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("disposals/{id}")]
    public async Task<ActionResult<AssetDisposalDto>> GetDisposalById(Guid id)
    {
        var result = await _disposalService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("disposals")]
    public async Task<ActionResult<AssetDisposalDto>> RequestDisposal(RequestAssetDisposalDto dto)
    {
        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        var result = await _disposalService.RequestDisposalAsync(dto, employeeId);
        return CreatedAtAction(nameof(GetDisposalById), new { id = result.Id }, result);
    }

    [HttpPost("disposals/{id}/approve")]
    public async Task<ActionResult<AssetDisposalDto>> ApproveDisposal(Guid id, ApproveAssetDisposalDto dto)
    {
        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        try
        {
            var result = await _disposalService.ApproveDisposalAsync(id, employeeId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("disposals/{id}/reject")]
    public async Task<ActionResult<AssetDisposalDto>> RejectDisposal(Guid id, ApproveAssetDisposalDto dto)
    {
        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        try
        {
            var result = await _disposalService.RejectDisposalAsync(id, employeeId, dto.Comments ?? "Rejected");
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ===== PHYSICAL VERIFICATION =====

    [HttpGet("verification/sessions")]
    public async Task<ActionResult<IEnumerable<AssetVerificationSessionDto>>> GetVerificationSessions()
    {
        var result = await _verificationService.GetAllSessionsAsync();
        return Ok(result);
    }

    [HttpGet("verification/sessions/{id}")]
    public async Task<ActionResult<AssetVerificationSessionDto>> GetVerificationSession(Guid id)
    {
        var result = await _verificationService.GetSessionByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("verification/sessions")]
    public async Task<ActionResult<AssetVerificationSessionDto>> CreateVerificationSession(CreateAssetVerificationSessionDto dto)
    {
        var result = await _verificationService.CreateSessionAsync(dto);
        return CreatedAtAction(nameof(GetVerificationSession), new { id = result.Id }, result);
    }

    [HttpPost("verification/sessions/{id}/start")]
    public async Task<ActionResult<AssetVerificationSessionDto>> StartVerificationSession(Guid id)
    {
        try
        {
            var result = await _verificationService.StartSessionAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("verification/sessions/{id}/complete")]
    public async Task<ActionResult<AssetVerificationSessionDto>> CompleteVerificationSession(Guid id)
    {
        try
        {
            var result = await _verificationService.CompleteSessionAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("verification/sessions/{id}/items")]
    public async Task<ActionResult<IEnumerable<AssetVerificationItemDto>>> GetSessionItems(Guid id)
    {
        var result = await _verificationService.GetSessionItemsAsync(id);
        return Ok(result);
    }

    [HttpPost("verification/sessions/{id}/items/{assetId}/verify")]
    public async Task<ActionResult<AssetVerificationItemDto>> VerifyAsset(Guid id, Guid assetId, VerifyAssetDto dto)
    {
        try
        {
            var result = await _verificationService.VerifyAssetAsync(id, assetId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ===== REPORTS =====

    [HttpGet("reports/register")]
    public async Task<ActionResult<FixedAssetRegisterDto>> GetAssetRegister([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetAssetRegisterAsync(query));
    }

    [HttpGet("reports/additions")]
    public async Task<ActionResult<FixedAssetAdditionsReportDto>> GetAdditionsReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetAdditionsReportAsync(query));
    }

    [HttpGet("reports/depreciation")]
    public async Task<ActionResult<FixedAssetDepreciationReportDto>> GetDepreciationReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetDepreciationReportAsync(query));
    }

    [HttpGet("reports/accumulated-depreciation")]
    public async Task<ActionResult<FixedAssetAccumulatedDepreciationReportDto>> GetAccumulatedDepreciationReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetAccumulatedDepreciationReportAsync(query));
    }

    [HttpGet("reports/valuations")]
    public async Task<ActionResult<FixedAssetValuationMovementReportDto>> GetValuationMovementReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetValuationMovementReportAsync(query));
    }

    [HttpGet("reports/disposals")]
    public async Task<ActionResult<List<AssetDisposalReportDto>>> GetDisposalReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetDisposalReportAsync(query));
    }

    [HttpGet("reports/transfers")]
    public async Task<ActionResult<List<AssetTransferReportDto>>> GetTransferReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetTransferReportAsync(query));
    }

    [HttpGet("reports/roll-forward")]
    public async Task<ActionResult<FixedAssetRollForwardReportDto>> GetRollForwardReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetRollForwardReportAsync(query));
    }

    [HttpGet("reports/gl-reconciliation")]
    public async Task<ActionResult<FixedAssetGlReconciliationReportDto>> GetGlReconciliationReport([FromQuery] FixedAssetReportQueryDto query)
    {
        return Ok(await _reportsService.GetGlReconciliationReportAsync(query));
    }

    [HttpGet("reports/export/excel")]
    public async Task<IActionResult> ExportToExcel([FromQuery] string reportType, [FromQuery] FixedAssetReportQueryDto query)
    {
        try
        {
            var fileBytes = await _reportsService.ExportToExcelAsync(reportType, query);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{reportType}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("reports/export/pdf")]
    public async Task<IActionResult> ExportToPdf([FromQuery] string reportType, [FromQuery] FixedAssetReportQueryDto query)
    {
        try
        {
            var fileBytes = await _reportsService.ExportToPdfAsync(reportType, query);
            return File(fileBytes, "application/pdf", $"{reportType}_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Bulk Import
    [HttpPost("bulk-import")]
    public async Task<ActionResult<BulkImportResultDto>> BulkImport(IFormFile file, [FromQuery] bool dryRun = false)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only Excel files (.xlsx) are supported.");

            using var stream = file.OpenReadStream();
            var result = await _fixedAssetService.ImportAssetsFromExcelAsync(stream, file.FileName, dryRun);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "An error occurred during bulk import.", details = ex.Message });
        }
    }

    [HttpGet("import-template")]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        try
        {
            var fileBytes = await _fixedAssetService.GenerateImportTemplateAsync();
            var fileName = $"FixedAsset_Import_Template_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "An error occurred while generating the template.", details = ex.Message });
        }
    }

    // ========== Valuations ==========

    [HttpPost("valuations")]
    public async Task<IActionResult> CreateValuation([FromBody] CreateAssetValuationDto dto)
    {
        try
        {
            var userId = Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;
            var result = await _valuationService.CreateValuationAsync(dto, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("valuations/bulk")]
    public async Task<IActionResult> CreateBulkValuation([FromBody] CreateBulkAssetValuationDto dto)
    {
        try
        {
            var userId = Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;
            var result = await _valuationService.CreateBulkValuationAsync(dto, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{assetId}/valuations")]
    public async Task<IActionResult> GetValuationsByAsset(Guid assetId)
    {
        var result = await _valuationService.GetValuationsByAssetAsync(assetId);
        return Ok(result);
    }

    [HttpPost("valuations/{valuationId}/post-to-gl")]
    public async Task<IActionResult> PostValuationToGL(Guid valuationId)
    {
        try
        {
            var result = await _valuationService.PostValuationToGLAsync(valuationId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("valuations/{valuationId}/corrections")]
    public async Task<IActionResult> GetValuationCorrections(Guid valuationId, CancellationToken cancellationToken)
    {
        try { return Ok(await _valuationService.GetCorrectionsAsync(valuationId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>
    /// Captures the correction rationale without changing a posted valuation. A different user
    /// must approve the request before the compensating journal can be posted.
    /// </summary>
    [HttpPost("valuations/{valuationId}/corrections")]
    [Authorize(Policy = FinancePermissions.ReverseFixedAssetValuation)]
    public async Task<IActionResult> RequestValuationCorrection(
        Guid valuationId, [FromBody] RequestAssetValuationCorrectionDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _valuationService.RequestCorrectionAsync(valuationId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetValuationCorrections), new { valuationId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("valuations/{valuationId}/corrections/{correctionId}/review")]
    [Authorize(Policy = FinancePermissions.ApproveFixedAssetValuationReversal)]
    public async Task<IActionResult> ReviewValuationCorrection(
        Guid valuationId, Guid correctionId, [FromBody] ReviewAssetValuationCorrectionDto dto,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _valuationService.ReviewCorrectionAsync(valuationId, correctionId, dto, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("valuations/{valuationId}/corrections/{correctionId}/post")]
    [Authorize(Policy = FinancePermissions.ReverseFixedAssetValuation)]
    public async Task<IActionResult> PostValuationCorrection(
        Guid valuationId, Guid correctionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _valuationService.PostCorrectionAsync(valuationId, correctionId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ========== Disposal Complete ==========

    [HttpPost("disposals/{disposalId}/complete")]
    public async Task<IActionResult> CompleteDisposal(Guid disposalId)
    {
        try
        {
            var result = await _disposalService.CompleteDisposalAsync(disposalId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ========== Bulk Disposals ==========

    [HttpPost("disposals/bulk")]
    public async Task<IActionResult> RequestBulkDisposal([FromBody] RequestBulkAssetDisposalDto dto)
    {
        try
        {
            var userId = Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;
            var result = await _disposalService.RequestBulkDisposalAsync(dto, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ========== Lifecycle Management ==========

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> ActivateAsset(Guid id, [FromQuery] DateTime? placedInServiceDate)
    {
        try
        {
            var result = await _fixedAssetService.ActivateAsync(id, placedInServiceDate);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/hold")]
    public async Task<IActionResult> PutOnHold(Guid id, [FromQuery] string reason = "")
    {
        try
        {
            var result = await _fixedAssetService.PutOnHoldAsync(id, reason);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> ResumeAsset(Guid id)
    {
        try
        {
            var result = await _fixedAssetService.ResumeAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ========== Dashboard ==========

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _fixedAssetService.GetDashboardAsync();
        return Ok(result);
    }

    // ========== Asset Code Generation ==========

    [HttpGet("generate-code")]
    public async Task<IActionResult> GenerateAssetCode([FromQuery] Guid categoryId)
    {
        try
        {
            var code = await _fixedAssetService.GenerateAssetCodeAsync(categoryId);
            return Ok(new { code });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

public sealed class FixedAssetApprovalActionRequest
{
    public string? Comments { get; set; }
    public string? Reason { get; set; }
}
