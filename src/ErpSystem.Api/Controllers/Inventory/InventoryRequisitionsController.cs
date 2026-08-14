using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing inventory requisitions (department issues)
/// </summary>
[ApiController]
[Route("api/inventory/requisitions")]
[Authorize(Policy = "InternalOnly")]
public class InventoryRequisitionsController : ControllerBase
{
    private readonly IInventoryRequisitionService _requisitionService;
    private readonly IWorkflowService _workflowService;
    private readonly IDocumentOutputService _documentOutputService;
    private readonly IInventoryReturnControlService _returnControls;
    private readonly ILogger<InventoryRequisitionsController> _logger;

    public InventoryRequisitionsController(
        IInventoryRequisitionService requisitionService,
        IWorkflowService workflowService,
        IDocumentOutputService documentOutputService,
        IInventoryReturnControlService returnControls,
        ILogger<InventoryRequisitionsController> logger)
    {
        _requisitionService = requisitionService;
        _workflowService = workflowService;
        _documentOutputService = documentOutputService;
        _returnControls = returnControls;
        _logger = logger;
    }

    /// <summary>
    /// Gets all inventory requisitions with optional date filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryRequisitionDto>>> GetAll(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var requisitions = (await _requisitionService.GetAllAsync(fromDate, toDate)).ToList();

            // Provide more accurate UX for pending approvals: show the actual current workflow step name.
            var pending = requisitions.Where(r => r.Status == RequisitionStatus.Submitted).ToList();
            if (pending.Count > 0)
            {
                await Task.WhenAll(pending.Select(async r =>
                {
                    try
                    {
                        var step = await _workflowService.GetCurrentWorkflowStepAsync("InventoryRequisition", r.Id);
                        r.CurrentWorkflowStepName = step?.StepName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to resolve current workflow step for InventoryRequisition {RequisitionId}", r.Id);
                    }
                }));
            }

            return Ok(requisitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory requisitions");
            return StatusCode(500, "An error occurred while retrieving inventory requisitions");
        }
    }

    /// <summary>
    /// Gets an inventory requisition by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryRequisitionDetailDto>> GetById(Guid id)
    {
        try
        {
            var requisition = await _requisitionService.GetByIdAsync(id);
            if (requisition == null)
                return NotFound($"Inventory requisition with ID {id} not found");

            if (requisition.Status == RequisitionStatus.Submitted)
            {
                try
                {
                    var step = await _workflowService.GetCurrentWorkflowStepAsync("InventoryRequisition", id);
                    requisition.CurrentWorkflowStepName = step?.StepName;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve current workflow step for InventoryRequisition {RequisitionId}", id);
                }
            }

            return Ok(requisition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory requisition {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the inventory requisition");
        }
    }

    /// <summary>
    /// Gets an inventory requisition by requisition number
    /// </summary>
    [HttpGet("by-number/{requisitionNumber}")]
    public async Task<ActionResult<InventoryRequisitionDetailDto>> GetByNumber(string requisitionNumber)
    {
        try
        {
            var requisition = await _requisitionService.GetByRequisitionNumberAsync(requisitionNumber);
            if (requisition == null)
                return NotFound($"Inventory requisition with number '{requisitionNumber}' not found");

            return Ok(requisition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory requisition {RequisitionNumber}", requisitionNumber);
            return StatusCode(500, "An error occurred while retrieving the inventory requisition");
        }
    }

    /// <summary>
    /// Gets requisitions by warehouse
    /// </summary>
    [HttpGet("by-warehouse/{warehouseId}")]
    public async Task<ActionResult<IEnumerable<InventoryRequisitionDto>>> GetByWarehouse(Guid warehouseId)
    {
        try
        {
            var requisitions = await _requisitionService.GetByWarehouseAsync(warehouseId);
            return Ok(requisitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requisitions for warehouse {WarehouseId}", warehouseId);
            return StatusCode(500, "An error occurred while retrieving requisitions");
        }
    }

    /// <summary>
    /// Gets requisitions by department
    /// </summary>
    [HttpGet("by-department/{departmentId}")]
    public async Task<ActionResult<IEnumerable<InventoryRequisitionDto>>> GetByDepartment(Guid departmentId)
    {
        try
        {
            var requisitions = await _requisitionService.GetByDepartmentAsync(departmentId);
            return Ok(requisitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requisitions for department {DepartmentId}", departmentId);
            return StatusCode(500, "An error occurred while retrieving requisitions");
        }
    }

    /// <summary>
    /// Gets requisitions pending approval
    /// </summary>
    [HttpGet("pending-approval")]
    public async Task<ActionResult<IEnumerable<InventoryRequisitionDto>>> GetPendingApproval()
    {
        try
        {
            var requisitions = await _requisitionService.GetPendingApprovalAsync();
            return Ok(requisitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approval requisitions");
            return StatusCode(500, "An error occurred while retrieving requisitions");
        }
    }

    /// <summary>
    /// Gets requisitions pending issue
    /// </summary>
    [HttpGet("pending-issue")]
    public async Task<ActionResult<IEnumerable<InventoryRequisitionDto>>> GetPendingIssue()
    {
        try
        {
            var requisitions = await _requisitionService.GetPendingIssueAsync();
            return Ok(requisitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending issue requisitions");
            return StatusCode(500, "An error occurred while retrieving requisitions");
        }
    }

    /// <summary>
    /// Creates a new inventory requisition
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InventoryRequisitionDetailDto>> Create([FromBody] CreateInventoryRequisitionDto dto)
    {
        try
        {
            var requisition = await _requisitionService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = requisition.Id }, requisition);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inventory requisition");
            return StatusCode(500, "An error occurred while creating the inventory requisition");
        }
    }

    /// <summary>
    /// Updates an inventory requisition
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<InventoryRequisitionDetailDto>> Update(Guid id, [FromBody] UpdateInventoryRequisitionDto dto)
    {
        try
        {
            var requisition = await _requisitionService.UpdateAsync(id, dto);
            return Ok(requisition);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory requisition {Id}", id);
            return StatusCode(500, "An error occurred while updating the inventory requisition");
        }
    }

    /// <summary>
    /// Submits a requisition for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult> Submit(Guid id)
    {
        try
        {
            await _requisitionService.SubmitAsync(id);
            return Ok(new { message = "Requisition submitted successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting requisition {Id}", id);
            return StatusCode(500, "An error occurred while submitting the requisition");
        }
    }

    /// <summary>
    /// Approves a requisition
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(Guid id, [FromBody] ApproveRequisitionRequest? request = null)
    {
        try
        {
            await _requisitionService.ApproveAsync(id, request?.Notes);
            return Ok(new { message = "Approval processed successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving requisition {Id}", id);
            return StatusCode(500, "An error occurred while approving the requisition");
        }
    }

    /// <summary>
    /// Rejects a requisition
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] RejectRequisitionRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest("Rejection comment is required.");
            }

            await _requisitionService.RejectAsync(id, request.Reason);
            return Ok(new { message = "Rejection processed successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting requisition {Id}", id);
            return StatusCode(500, "An error occurred while rejecting the requisition");
        }
    }

    /// <summary>
    /// Issues items for a requisition
    /// </summary>
    [HttpPost("{id}/issue")]
    public async Task<ActionResult> Issue(Guid id, [FromBody] IssueRequisitionDto dto)
    {
        try
        {
            dto.CorrelationId = HttpContext.TraceIdentifier;
            await _requisitionService.IssueAsync(id, dto);
            var vouchers = await _requisitionService.GetIssueVouchersAsync(id, HttpContext.RequestAborted);
            return Ok(new { message = "Items issued successfully", voucher = vouchers.FirstOrDefault() });
        }
        catch (InventoryIssueNotFoundException ex)
        {
            return NotFound(new { code = "INV_ISSUE_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_ISSUE_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (ProcurementAccessAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_ISSUE_ACCESS_DENIED", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (ProcurementAccessValidationException ex)
        {
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueControlException ex)
        {
            var status = ex.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         ex.Code.Contains("CONCURRENCY", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueAccountingControlException ex)
        {
            var status = ex.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         ex.Code.Contains("LINEAGE", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InventoryTrackingAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRACKING_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryTrackingControlException ex)
        {
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing items for requisition {Id}", id);
            return Unexpected(ex, "Inventory issue failed",
                "The inventory issue could not be completed. Use the correlation ID when contacting support.");
        }
    }

    [HttpGet("issue-receivers")]
    public async Task<ActionResult<IReadOnlyList<InventoryIssueReceiverDto>>> GetIssueReceivers()
    {
        try
        {
            return Ok(await _requisitionService.GetIssueReceiversAsync(HttpContext.RequestAborted));
        }
        catch (InventoryIssueAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_ISSUE_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("{id}/issue-vouchers")]
    public async Task<ActionResult<IReadOnlyList<InventoryIssueVoucherDto>>> GetIssueVouchers(Guid id)
    {
        try
        {
            return Ok(await _requisitionService.GetIssueVouchersAsync(id, HttpContext.RequestAborted));
        }
        catch (InventoryIssueNotFoundException ex)
        {
            return NotFound(new { code = "INV_ISSUE_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_ISSUE_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("issue-vouchers/{voucherId}")]
    public async Task<ActionResult<InventoryIssueVoucherDto>> GetIssueVoucher(Guid voucherId)
    {
        try
        {
            var voucher = await _requisitionService.GetIssueVoucherAsync(voucherId, HttpContext.RequestAborted);
            return voucher is null
                ? NotFound(new { code = "INV_ISSUE_VOUCHER_NOT_FOUND", message = "The Store Issue Voucher was not found in the current tenant.", correlationId = HttpContext.TraceIdentifier })
                : Ok(voucher);
        }
        catch (InventoryIssueAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_ISSUE_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("issue-vouchers/{voucherId}/acknowledge")]
    public async Task<ActionResult<InventoryIssueVoucherDto>> AcknowledgeIssueVoucher(
        Guid voucherId,
        [FromBody] AcknowledgeInventoryIssueVoucherRequest request)
    {
        try
        {
            return Ok(await _requisitionService.AcknowledgeIssueVoucherAsync(
                voucherId, request, HttpContext.TraceIdentifier, HttpContext.RequestAborted));
        }
        catch (InventoryIssueNotFoundException ex)
        {
            return NotFound(new { code = "INV_ISSUE_VOUCHER_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_ISSUE_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueControlException ex)
        {
            var status = ex.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         ex.Code.Contains("CONCURRENCY", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet("issue-vouchers/{voucherId}/download")]
    public async Task<IActionResult> DownloadIssueVoucher(Guid voucherId)
    {
        try
        {
            var voucher = await _requisitionService.GetIssueVoucherAsync(voucherId, HttpContext.RequestAborted);
            if (voucher is null)
                return NotFound(new { code = "INV_ISSUE_VOUCHER_NOT_FOUND", message = "The Store Issue Voucher was not found in the current tenant.", correlationId = HttpContext.TraceIdentifier });
            var rendered = await _documentOutputService.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = DocumentTypes.InventoryStoreIssueVoucher,
                EntityId = voucherId,
                Format = "pdf",
                CopyType = "Original"
            }, HttpContext.RequestAborted);
            return File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (InventoryIssueAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_ISSUE_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "INV_ISSUE_VOUCHER_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
    }

    /// <summary>
    /// Returns previously issued items back to stock
    /// </summary>
    [HttpPost("{id}/return")]
    public async Task<ActionResult<InventoryReturnVoucherDto>> Return(Guid id, [FromBody] ReturnRequisitionDto dto)
    {
        try
        {
            return Ok(await _returnControls.RequestAsync(id, dto, HttpContext.RequestAborted));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InventoryTrackingAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRACKING_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryTrackingControlException ex)
        {
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryReturnAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_RETURN_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryReturnControlException ex)
        {
            var status = ex.Code.Contains("NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
                : ex.Code.Contains("STALE", StringComparison.Ordinal) || ex.Code.Contains("CONFLICT", StringComparison.Ordinal) || ex.Code.Contains("STATE", StringComparison.Ordinal)
                    ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueAccountingControlException ex)
        {
            var status = ex.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         ex.Code.Contains("LINEAGE", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning items for requisition {Id}", id);
            return Unexpected(ex, "Inventory return failed",
                "The inventory return could not be completed. Use the correlation ID when contacting support.");
        }
    }

    [HttpGet("return-reasons")]
    public ActionResult<IReadOnlyDictionary<string, string>> GetReturnReasons() => Ok(InventoryReturnReasonCodes.All);

    [HttpGet("return-vouchers")]
    public async Task<ActionResult<IReadOnlyList<InventoryReturnVoucherDto>>> GetReturnVouchers([FromQuery] Guid? requisitionId = null)
    {
        try { return Ok(await _returnControls.GetAsync(requisitionId, HttpContext.RequestAborted)); }
        catch (InventoryReturnAuthorizationException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_RETURN_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
    }

    [HttpGet("return-vouchers/{voucherId:guid}")]
    public async Task<ActionResult<InventoryReturnVoucherDto>> GetReturnVoucher(Guid voucherId)
    {
        try
        {
            var voucher = await _returnControls.GetAsync(voucherId, HttpContext.RequestAborted);
            return voucher is null ? NotFound(new { code = "INV_RETURN_VOUCHER_NOT_FOUND", message = "The Store Return Voucher was not found in the current tenant.", correlationId = HttpContext.TraceIdentifier }) : Ok(voucher);
        }
        catch (InventoryReturnAuthorizationException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_RETURN_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
    }

    [HttpGet("return-vouchers/{voucherId:guid}/download")]
    public async Task<IActionResult> DownloadReturnVoucher(Guid voucherId)
    {
        try
        {
            var voucher = await _returnControls.GetAsync(voucherId, HttpContext.RequestAborted);
            if (voucher is null)
                return NotFound(new { code = "INV_RETURN_VOUCHER_NOT_FOUND", message = "The Store Return Voucher was not found in the current tenant.", correlationId = HttpContext.TraceIdentifier });
            var rendered = await _documentOutputService.RenderAsync(new DocumentRenderRequestDto
            {
                DocumentType = DocumentTypes.InventoryStoreReturnVoucher,
                EntityId = voucherId,
                Format = "pdf",
                CopyType = "Original"
            }, HttpContext.RequestAborted);
            return File(rendered.Content, rendered.ContentType, rendered.FileName);
        }
        catch (InventoryReturnAuthorizationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_RETURN_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "INV_RETURN_VOUCHER_NOT_FOUND", message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost("return-vouchers/{voucherId:guid}/decision")]
    public async Task<ActionResult<InventoryReturnVoucherDto>> DecideReturnVoucher(Guid voucherId, [FromBody] DecideInventoryReturnVoucherRequest request) =>
        await ExecuteReturnActionAsync(() => _returnControls.DecideAsync(voucherId, request, HttpContext.RequestAborted));

    [HttpPost("return-vouchers/{voucherId:guid}/post")]
    public async Task<ActionResult<InventoryReturnVoucherDto>> PostReturnVoucher(Guid voucherId, [FromBody] PostInventoryReturnVoucherRequest request) =>
        await ExecuteReturnActionAsync(() => _returnControls.PostAsync(voucherId, request, HttpContext.RequestAborted));

    [HttpPost("return-vouchers/{voucherId:guid}/reverse")]
    public async Task<ActionResult<InventoryReturnVoucherDto>> ReverseReturnVoucher(Guid voucherId, [FromBody] ReverseInventoryReturnVoucherRequest request) =>
        await ExecuteReturnActionAsync(() => _returnControls.ReverseAsync(voucherId, request, HttpContext.RequestAborted));

    private async Task<ActionResult<InventoryReturnVoucherDto>> ExecuteReturnActionAsync(Func<Task<InventoryReturnVoucherDto>> action)
    {
        try { return Ok(await action()); }
        catch (InventoryReturnAuthorizationException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_RETURN_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (InventoryTrackingAuthorizationException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { code = "INV_TRACKING_FORBIDDEN", message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (InventoryTrackingControlException ex) { return UnprocessableEntity(new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier }); }
        catch (InventoryReturnControlException ex)
        {
            var status = ex.Code.Contains("NOT_FOUND", StringComparison.Ordinal) ? StatusCodes.Status404NotFound
                : ex.Code.Contains("STALE", StringComparison.Ordinal) || ex.Code.Contains("CONFLICT", StringComparison.Ordinal) || ex.Code.Contains("STATE", StringComparison.Ordinal)
                    ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
        catch (InventoryIssueAccountingControlException ex)
        {
            var status = ex.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         ex.Code.Contains("LINEAGE", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity;
            return StatusCode(status, new { code = ex.Code, message = ex.Message, correlationId = HttpContext.TraceIdentifier });
        }
    }

    /// <summary>
    /// Completes a requisition
    /// </summary>
    [HttpPost("{id}/complete")]
    public async Task<ActionResult> Complete(Guid id)
    {
        try
        {
            await _requisitionService.CompleteAsync(id);
            return Ok(new { message = "Requisition completed successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing requisition {Id}", id);
            return StatusCode(500, "An error occurred while completing the requisition");
        }
    }

    /// <summary>
    /// Cancels a requisition
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, [FromBody] CancelRequisitionRequest request)
    {
        try
        {
            await _requisitionService.CancelAsync(id, request.Reason);
            return Ok(new { message = "Requisition cancelled successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling requisition {Id}", id);
            return StatusCode(500, "An error occurred while cancelling the requisition");
        }
    }

    /// <summary>
    /// Adds an item to a requisition
    /// </summary>
    [HttpPost("{id}/items")]
    public async Task<ActionResult<InventoryRequisitionItemDto>> AddItem(Guid id, [FromBody] AddRequisitionItemDto dto)
    {
        try
        {
            var item = await _requisitionService.AddItemAsync(id, dto);
            return Ok(item);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to requisition {Id}", id);
            return StatusCode(500, "An error occurred while adding the item");
        }
    }

    /// <summary>
    /// Updates an item in a requisition
    /// </summary>
    [HttpPut("{id}/items/{itemId}")]
    public async Task<ActionResult<InventoryRequisitionItemDto>> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateRequisitionItemDto dto)
    {
        try
        {
            var item = await _requisitionService.UpdateItemAsync(id, itemId, dto);
            return Ok(item);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item {ItemId} in requisition {Id}", itemId, id);
            return StatusCode(500, "An error occurred while updating the item");
        }
    }

    /// <summary>
    /// Removes an item from a requisition
    /// </summary>
    [HttpDelete("{id}/items/{itemId}")]
    public async Task<ActionResult> RemoveItem(Guid id, Guid itemId)
    {
        try
        {
            await _requisitionService.RemoveItemAsync(id, itemId);
            return Ok(new { message = "Item removed successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId} from requisition {Id}", itemId, id);
            return StatusCode(500, "An error occurred while removing the item");
        }
    }

    private ObjectResult Unexpected(Exception exception, string title, string detail)
    {
        HttpContext.Items[ErpSystem.Api.Filters.SystemExceptionResultLoggingFilter.HandledExceptionItemKey] = exception;
        return new ObjectResult(new ProblemDetails
        {
            Type = "https://tdc.gov.gh/problems/inventory-unexpected",
            Title = title,
            Status = StatusCodes.Status500InternalServerError,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = new Dictionary<string, object?>
            {
                ["code"] = "INVENTORY_UNEXPECTED",
                ["correlationId"] = HttpContext.TraceIdentifier
            }
        }) { StatusCode = StatusCodes.Status500InternalServerError };
    }
}

public class ApproveRequisitionRequest
{
    public string? Notes { get; set; }
}

public class RejectRequisitionRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class CancelRequisitionRequest
{
    public string Reason { get; set; } = string.Empty;
}
