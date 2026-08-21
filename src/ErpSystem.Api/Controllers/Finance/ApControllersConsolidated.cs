using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using ErpSystem.Api.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance
{
    // ═══════════════════════════════════════════════════════════════════════
    //  VENDOR INVOICES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages vendor invoices throughout their full lifecycle: creation, matching,
    /// approval, payment, and voiding within the Accounts Payable module.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/ap/invoices")]
    public class VendorInvoiceController : ControllerBase
    {
        private readonly IVendorInvoiceService _invoiceService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IVendorInvoiceMatchExceptionService? _matchExceptionService;
        private readonly IProcurementAcceptedSupplyService? _acceptedSupplyService;

        public VendorInvoiceController(
            IVendorInvoiceService invoiceService,
            ICurrentUserService currentUserService,
            ApplicationDbContext dbContext,
            IVendorInvoiceMatchExceptionService? matchExceptionService = null,
            IProcurementAcceptedSupplyService? acceptedSupplyService = null)
        {
            _invoiceService = invoiceService;
            _currentUserService = currentUserService;
            _dbContext = dbContext;
            _matchExceptionService = matchExceptionService;
            _acceptedSupplyService = acceptedSupplyService;
        }

        private static readonly string[] PrivilegedRoles = { "SuperAdmin", "TenantAdmin" };

        private async Task<bool> HasAnyPermissionAsync(params string[] requiredPermissions)
        {
            if (requiredPermissions.Length == 0) return true;
            if (PrivilegedRoles.Any(_currentUserService.IsInRole)) return true;
            if (!Guid.TryParse(_currentUserService.UserId, out var userId)) return false;

            var userPermissions = await _dbContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
                .ToListAsync();

            return userPermissions.Any(p => requiredPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>Retrieves a paginated list of vendor invoices filtered by query parameters.</summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<VendorInvoiceDto>>> GetAll([FromQuery] VendorInvoiceQueryDto query)
            => Ok(await _invoiceService.GetAllAsync(query));

        /// <summary>Retrieves a single vendor invoice by ID, including line items and allocations.</summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<VendorInvoiceDto>> GetById(Guid id)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            return invoice == null ? NotFound() : Ok(invoice);
        }

        /// <summary>Retrieves a vendor invoice by its system-generated invoice number.</summary>
        [HttpGet("by-number/{invoiceNumber}")]
        public async Task<ActionResult<VendorInvoiceDto>> GetByNumber(string invoiceNumber)
        {
            var invoice = await _invoiceService.GetByInvoiceNumberAsync(invoiceNumber);
            return invoice == null ? NotFound() : Ok(invoice);
        }

        /// <summary>
        /// Returns only authoritative, tenant-scoped completion records that can
        /// be selected for the chosen PO. Works is intentionally routed to QS.
        /// </summary>
        [HttpGet("accepted-supply-options")]
        public async Task<ActionResult<ProcurementAcceptedSupplyOptionsDto>>
            GetAcceptedSupplyOptions([FromQuery] Guid purchaseOrderId)
        {
            if (!await HasAnyPermissionAsync(
                    "Finance.AP.Invoices.Create", "Finance.AP.Invoices.Write"))
                return Forbid();
            if (_acceptedSupplyService == null)
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Accepted-supply control unavailable",
                    detail: "The shared accepted-supply control is not registered.");
            try
            {
                return Ok(await _acceptedSupplyService.GetOptionsAsync(
                    purchaseOrderId, HttpContext.RequestAborted));
            }
            catch (ProcurementAcceptedSupplyValidationException exception)
            {
                return UnprocessableEntity(new
                {
                    code = exception.Code,
                    message = exception.Message
                });
            }
        }

        /// <summary>
        /// Returns active, tenant-scoped canonical Supplier identities for AP selection controls.
        /// Procurement owns the Supplier master; Finance exposes this read-only projection because
        /// VendorInvoice and AP report filters must use Supplier.Id, never BusinessPartner.Id.
        /// </summary>
        [HttpGet("suppliers")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        [ProducesResponseType(typeof(IReadOnlyList<ApInvoiceSupplierDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<ApInvoiceSupplierDto>>> GetSuppliers(
            CancellationToken cancellationToken)
        {
            Guid tenantId;
            try
            {
                tenantId = _currentUserService.GetRequiredFinanceTenantId();
            }
            catch (InvalidOperationException)
            {
                return Forbid();
            }

            // This is intentionally a read-only Finance boundary. Supplier activation and master
            // data remain Procurement-owned and are never repaired or mutated from this endpoint.
            var suppliers = await _dbContext.Suppliers
                .AsNoTracking()
                .Where(supplier =>
                    supplier.TenantId == tenantId &&
                    !supplier.IsDeleted &&
                    supplier.IsActive &&
                    supplier.Status == "Active")
                .OrderBy(supplier => supplier.Name)
                .ThenBy(supplier => supplier.SupplierCode)
                .Select(supplier => new ApInvoiceSupplierDto
                {
                    Id = supplier.Id,
                    Code = supplier.SupplierCode,
                    Name = supplier.Name,
                    PaymentTermId = supplier.PaymentTermId
                })
                .ToListAsync(cancellationToken);

            return Ok(suppliers);
        }

        /// <summary>Creates a new vendor invoice in Draft status with the supplied line items.</summary>
        [HttpPost]
        public async Task<ActionResult<VendorInvoiceDto>> Create([FromBody] VendorInvoiceCreateDto dto)
        {
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.Create", "Finance.AP.Invoices.Write"))
                return Forbid();

            try
            {
                var invoice = await _invoiceService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Updates an existing draft or rejected vendor invoice.</summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<VendorInvoiceDto>> Update(Guid id, [FromBody] VendorInvoiceUpdateDto dto)
        {
            if (id != dto.Id) return BadRequest("ID mismatch");
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.Edit", "Finance.AP.Invoices.Write"))
                return Forbid();
            try { return Ok(await _invoiceService.UpdateAsync(dto)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Permanently deletes a draft vendor invoice.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.Delete", "Finance.AP.Invoices.Write"))
                return Forbid();
            try { await _invoiceService.DeleteAsync(id); return NoContent(); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        // ── Approval workflow ───────────────────────────────────────────

        /// <summary>Submits a draft vendor invoice for approval.</summary>
        [HttpPost("{id}/submit")]
        public async Task<ActionResult<VendorInvoiceDto>> SubmitForApproval(Guid id)
        {
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.SubmitForApproval", "Finance.AP.Invoices.Approve"))
                return Forbid();
            try { return Ok(await _invoiceService.SubmitForApprovalAsync(id)); }
            catch (VendorInvoiceMatchControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Approves a pending vendor invoice, making it eligible for payment.</summary>
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<VendorInvoiceDto>> Approve(Guid id, [FromBody] string? comments = null)
        {
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.Approve"))
                return Forbid();
            try { return Ok(await _invoiceService.ApproveAsync(id, comments)); }
            catch (VendorInvoiceMatchControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Posts an approved vendor invoice to the general ledger through the central finance posting engine.</summary>
        [HttpPost("{id}/post")]
        public async Task<ActionResult<VendorInvoiceDto>> Post(Guid id)
        {
            if (!await HasAnyPermissionAsync(FinancePermissions.PostApInvoices))
                return Forbid();
            try { return Ok(await _invoiceService.PostAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Rejects a pending vendor invoice with a mandatory reason.</summary>
        [HttpPost("{id}/reject")]
        public async Task<ActionResult<VendorInvoiceDto>> Reject(Guid id, [FromBody] string comments)
        {
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.Approve"))
                return Forbid();
            try { return Ok(await _invoiceService.RejectAsync(id, comments)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Atomically voids a vendor invoice and reverses its posted central-Finance journal.</summary>
        [HttpPost("{id}/void")]
        public async Task<ActionResult<VendorInvoiceDto>> Void(Guid id, [FromBody] string reason)
        {
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.Void"))
                return Forbid();
            try { return Ok(await _invoiceService.VoidAsync(id, reason)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        // ── Matching ────────────────────────────────────────────────────

        /// <summary>Performs 2-way matching (invoice ↔ PO) for a vendor invoice.</summary>
        [HttpPost("{id}/match/two-way")]
        public async Task<ActionResult<InvoiceMatchingResultDto>> TwoWayMatch(Guid id)
        {
            if (!await HasAnyPermissionAsync(
                    "Finance.AP.Invoices.Edit",
                    "Finance.AP.Invoices.Write",
                    "Finance.AP.Invoices.Approve"))
                return Forbid();
            try { return Ok(await _invoiceService.PerformTwoWayMatchAsync(id)); }
            catch (VendorInvoiceMatchControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Performs 3-way matching (invoice ↔ PO ↔ goods receipt) for a vendor invoice.</summary>
        [HttpPost("{id}/match/three-way")]
        public async Task<ActionResult<InvoiceMatchingResultDto>> ThreeWayMatch(Guid id)
        {
            if (!await HasAnyPermissionAsync(
                    "Finance.AP.Invoices.Edit",
                    "Finance.AP.Invoices.Write",
                    "Finance.AP.Invoices.Approve"))
                return Forbid();
            try { return Ok(await _invoiceService.PerformThreeWayMatchAsync(id)); }
            catch (VendorInvoiceMatchControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Evaluates whether the current invoice snapshot is ready for approval without
        /// changing invoice state or creating a new control event.
        /// </summary>
        [HttpGet("{id}/match/readiness")]
        public async Task<ActionResult<InvoiceMatchingResultDto>> GetThreeWayMatchReadiness(Guid id)
        {
            try { return Ok(await _invoiceService.GetThreeWayMatchReadinessAsync(id)); }
            catch (VendorInvoiceMatchControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "AP_INVOICE_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Retrieves the current matching result/status for a vendor invoice.</summary>
        [HttpGet("{id}/match")]
        public async Task<ActionResult<InvoiceMatchingResultDto>> GetMatchingResult(Guid id)
        {
            try { return Ok(await _invoiceService.GetMatchingResultAsync(id)); }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "AP_INVOICE_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Returns the controlled AP-006 exception lifecycle and authoritative match readiness.</summary>
        [HttpGet("{id}/match-exceptions")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<VendorInvoiceMatchExceptionOverviewDto>> GetMatchExceptions(Guid id)
        {
            if (_matchExceptionService == null) return MatchExceptionServiceUnavailable();
            try { return Ok(await _matchExceptionService.GetOverviewAsync(id)); }
            catch (VendorInvoiceMatchExceptionControlException ex) { return MatchExceptionFailure(ex); }
        }

        /// <summary>Requests a dual-approved AP-006 exception for the current immutable match snapshot.</summary>
        [HttpPost("{id}/match-exceptions")]
        [Authorize(Policy = FinancePermissions.ManageApInvoices)]
        public async Task<ActionResult<VendorInvoiceMatchExceptionDto>> RequestMatchException(
            Guid id,
            [FromBody] CreateVendorInvoiceMatchExceptionDto request)
        {
            if (_matchExceptionService == null) return MatchExceptionServiceUnavailable();
            try
            {
                var item = await _matchExceptionService.RequestAsync(id, request, HttpContext.TraceIdentifier);
                return CreatedAtAction(nameof(GetMatchExceptions), new { id }, item);
            }
            catch (VendorInvoiceMatchExceptionControlException ex) { return MatchExceptionFailure(ex); }
        }

        /// <summary>Processes one independently assigned approval or rejection stage.</summary>
        [HttpPost("match-exceptions/{exceptionId}/decision")]
        [Authorize(Policy = FinancePermissions.ApproveApInvoices)]
        public async Task<ActionResult<VendorInvoiceMatchExceptionDto>> DecideMatchException(
            Guid exceptionId,
            [FromBody] DecideVendorInvoiceMatchExceptionDto request)
        {
            if (_matchExceptionService == null) return MatchExceptionServiceUnavailable();
            try
            {
                return Ok(await _matchExceptionService.DecideAsync(
                    exceptionId, request, HttpContext.TraceIdentifier));
            }
            catch (VendorInvoiceMatchExceptionControlException ex) { return MatchExceptionFailure(ex); }
        }

        /// <summary>Cancels a pending AP-006 request without changing invoice or payment state.</summary>
        [HttpPost("match-exceptions/{exceptionId}/cancel")]
        [Authorize(Policy = FinancePermissions.ManageApInvoices)]
        public async Task<ActionResult<VendorInvoiceMatchExceptionDto>> CancelMatchException(
            Guid exceptionId,
            [FromBody] CancelVendorInvoiceMatchExceptionDto request)
        {
            if (_matchExceptionService == null) return MatchExceptionServiceUnavailable();
            try
            {
                return Ok(await _matchExceptionService.CancelAsync(
                    exceptionId, request, HttpContext.TraceIdentifier));
            }
            catch (VendorInvoiceMatchExceptionControlException ex) { return MatchExceptionFailure(ex); }
        }

        /// <summary>Closes the assigned corrective action with controlled DMS/workflow evidence.</summary>
        [HttpPost("match-exceptions/{exceptionId}/corrective-action/complete")]
        [Authorize(Policy = FinancePermissions.ManageApInvoices)]
        public async Task<ActionResult<VendorInvoiceMatchExceptionDto>> CompleteMatchExceptionCorrectiveAction(
            Guid exceptionId,
            [FromBody] CompleteVendorInvoiceMatchCorrectiveActionDto request)
        {
            if (_matchExceptionService == null) return MatchExceptionServiceUnavailable();
            try
            {
                return Ok(await _matchExceptionService.CompleteCorrectiveActionAsync(
                    exceptionId, request, HttpContext.TraceIdentifier));
            }
            catch (VendorInvoiceMatchExceptionControlException ex) { return MatchExceptionFailure(ex); }
        }

        private ObjectResult MatchExceptionServiceUnavailable() => StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new
            {
                code = "AP_MATCH_EXCEPTION_SERVICE_UNAVAILABLE",
                message = "The controlled AP match-exception service is unavailable."
            });

        private ObjectResult MatchExceptionFailure(VendorInvoiceMatchExceptionControlException exception) =>
            StatusCode(exception.StatusCode, new { code = exception.Code, message = exception.Message });

        /// <summary>Checks if a vendor invoice is a potential duplicate.</summary>
        [HttpGet("duplicate-check")]
        public async Task<ActionResult<bool>> CheckDuplicate(
            [FromQuery] Guid supplierId,
            [FromQuery] string? supplierInvoiceNumber,
            [FromQuery] DateTime invoiceDate)
        {
            var isDuplicate = await _invoiceService.IsDuplicateAsync(supplierId, supplierInvoiceNumber, invoiceDate);
            return Ok(isDuplicate);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  VENDOR PAYMENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages vendor payments, allocations, early-payment discounts, and payment batches
    /// within the Accounts Payable module.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/ap/payments")]
    public class VendorPaymentController : ControllerBase
    {
        private readonly IVendorPaymentService _paymentService;
        private readonly IProcurementInvoicePaymentSodService? _invoicePaymentSod;

        public VendorPaymentController(
            IVendorPaymentService paymentService,
            IProcurementInvoicePaymentSodService? invoicePaymentSod = null)
        {
            _paymentService = paymentService;
            _invoicePaymentSod = invoicePaymentSod;
        }

        /// <summary>Retrieves a paginated list of vendor payments.</summary>
        [HttpGet]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<PagedResult<VendorPaymentDto>>> GetAll([FromQuery] VendorPaymentQueryDto query)
            => Ok(await _paymentService.GetAllAsync(query));

        /// <summary>Retrieves a single vendor payment by ID, including allocations.</summary>
        [HttpGet("{id}")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<VendorPaymentDto>> GetById(Guid id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            return payment == null ? NotFound() : Ok(payment);
        }

        /// <summary>
        /// Retrieves the complete source-to-ledger trace for a payment, including allocations,
        /// posting events, journals, reversals, and Finance audit history.
        /// </summary>
        [HttpGet("{id}/trace")]
        public async Task<ActionResult<VendorPaymentTraceDto>> GetTrace(Guid id)
        {
            try
            {
                var trace = await _paymentService.GetTraceAsync(id);
                return trace == null ? NotFound() : Ok(trace);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>Creates a new vendor payment with optional invoice allocations.</summary>
        [HttpPost]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<VendorPaymentDto>> Create([FromBody] VendorPaymentCreateDto dto)
        {
            try
            {
                var payment = await _paymentService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = payment.Id }, payment);
            }
            // Scope rules live in the service so they protect background and API callers alike.
            // Convert a deliberate scope denial to HTTP 403 rather than misreporting it as input validation.
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (VendorPaymentControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "AP_PAYMENT_SOURCE_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        // ── Allocations ─────────────────────────────────────────────────

        /// <summary>
        /// Submits a direct payment into its configured evidence and maker-checker workflow. Batch
        /// payments deliberately continue through the batch endpoint so one payment cannot acquire
        /// a second, conflicting approval source.
        /// </summary>
        [HttpPost("{id}/submit")]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<VendorPaymentDto>> Submit(
            Guid id,
            [FromBody] SubmitVendorPaymentDto dto)
        {
            try { return Ok(await _paymentService.SubmitAsync(id, dto)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Returns the payment's applied evidence/authority policy and live evidence readiness.
        /// The endpoint is bank-scope protected by the payment service.
        /// </summary>
        [HttpGet("{id}/control")]
        public async Task<ActionResult<VendorPaymentControlDto>> GetControl(Guid id)
        {
            try
            {
                var control = await _paymentService.GetControlAsync(id);
                return control == null ? NotFound() : Ok(control);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        /// <summary>Allocates a payment against one or more outstanding vendor invoices.</summary>
        [HttpPost("{id}/allocate")]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<VendorPaymentAllocationResultDto>> Allocate(
            Guid id, [FromBody] List<VendorPaymentAllocationCreateDto> allocations)
        {
            try { return Ok(await _paymentService.AllocatePaymentAsync(id, allocations)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (VendorPaymentControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "AP_PAYMENT_SOURCE_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Retrieves all allocations for a specific vendor payment.</summary>
        [HttpGet("{id}/allocations")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<List<VendorPaymentAllocationDto>>> GetAllocations(Guid id)
        {
            try { return Ok(await _paymentService.GetPaymentAllocationsAsync(id)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        /// <summary>Posts an authorized vendor payment to the general ledger through the central finance posting engine.</summary>
        [HttpPost("{id}/post")]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<VendorPaymentDto>> Post(Guid id)
        {
            try { return Ok(await _paymentService.PostAsync(id)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (ProcurementInvoicePaymentSodBlockedException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message, readiness = ex.Readiness });
            }
            catch (VendorPaymentControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Reverses a posted payment through a compensating journal. The dedicated reversal
        /// permission and Finance data scope are both required; ordinary payment processing
        /// permission is intentionally insufficient for this high-risk correction.
        /// </summary>
        [HttpPost("{id}/reverse")]
        [Authorize(Policy = FinancePermissions.ReverseApPayments)]
        public async Task<ActionResult<VendorPaymentDto>> ReversePayment(
            Guid id,
            [FromBody] ReverseVendorPaymentDto dto)
        {
            try
            {
                return Ok(await _paymentService.ReversePaymentAsync(id, dto));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Reverses a specific payment allocation with a mandatory reason.</summary>
        [HttpPost("allocations/{allocationId}/reverse")]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<IActionResult> ReverseAllocation(Guid allocationId, [FromBody] string reason)
        {
            try { await _paymentService.ReverseAllocationAsync(allocationId, reason); return Ok(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Lists outstanding (unpaid) invoices for a supplier, for allocation selection.</summary>
        [HttpGet("supplier/{supplierId}/outstanding-invoices")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<List<OutstandingVendorInvoiceDto>>> GetOutstandingInvoices(
            Guid supplierId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
            => Ok(await _paymentService.GetOutstandingInvoicesAsync(
                supplierId,
                pageNumber,
                pageSize));

        /// <summary>Returns the current read-only AP-003 payment-readiness decision for one invoice.</summary>
        [HttpGet("invoices/{invoiceId}/readiness")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<VendorPaymentInvoiceReadinessDto>> GetInvoicePaymentReadiness(Guid invoiceId)
        {
            try { return Ok(await _paymentService.GetInvoicePaymentReadinessAsync(invoiceId)); }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "AP_INVOICE_NOT_FOUND", message = ex.Message });
            }
            catch (VendorPaymentControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
        }

        /// <summary>Returns current-actor AP-004/TDC-0506 readiness for a manual payment.</summary>
        [HttpGet("{id}/sod-readiness")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<ProcurementInvoicePaymentSodReadinessDto>> GetPaymentSodReadiness(Guid id)
        {
            if (_invoicePaymentSod == null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    code = ProcurementInvoicePaymentSodRules.EvidenceCode,
                    message = "The authoritative invoice/payment SOD service is unavailable."
                });
            try
            {
                return Ok(await _invoicePaymentSod.GetPaymentReadinessAsync(id, HttpContext.TraceIdentifier));
            }
            catch (ProcurementInvoicePaymentSodNotFoundException ex)
            {
                return NotFound(new { code = "AP_PAYMENT_NOT_FOUND", message = ex.Message });
            }
        }

        // ── Payment status ──────────────────────────────────────────────

        /// <summary>Marks a payment as cleared by the bank on the specified date.</summary>
        [HttpPost("{id}/clear")]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<VendorPaymentDto>> ClearPayment(Guid id, [FromBody] DateTime clearedDate)
        {
            try { return Ok(await _paymentService.ClearPaymentAsync(id, clearedDate)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Voids a vendor payment, reversing all allocations and restoring invoice balances.</summary>
        [HttpPost("{id}/void")]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<VendorPaymentDto>> VoidPayment(Guid id, [FromBody] string reason)
        {
            try { return Ok(await _paymentService.VoidPaymentAsync(id, reason)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        // ── Early payment discount ──────────────────────────────────────

        /// <summary>Calculates the early-payment discount available for a vendor invoice.</summary>
        [HttpGet("discount/calculate")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<EarlyPaymentDiscountResultDto>> CalculateDiscount(
            [FromQuery] Guid invoiceId, [FromQuery] DateTime paymentDate)
        {
            try { return Ok(await _paymentService.CalculateEarlyPaymentDiscountAsync(invoiceId, paymentDate)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PAYMENT BATCHES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages payment batch lifecycle: creation, approval, processing, and tracking
    /// for bulk vendor payment operations.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/ap/payment-batches")]
    public class PaymentBatchController : ControllerBase
    {
        private readonly IVendorPaymentService _paymentService;
        private readonly IProcurementInvoicePaymentSodService? _invoicePaymentSod;

        public PaymentBatchController(
            IVendorPaymentService paymentService,
            IProcurementInvoicePaymentSodService? invoicePaymentSod = null)
        {
            _paymentService = paymentService;
            _invoicePaymentSod = invoicePaymentSod;
        }

        /// <summary>Retrieves a paginated list of payment batches.</summary>
        [HttpGet]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<PagedResult<PaymentBatchDto>>> GetAll([FromQuery] PaymentBatchQueryDto query)
            => Ok(await _paymentService.GetAllBatchesAsync(query));

        /// <summary>Retrieves a single payment batch by ID, including its items.</summary>
        [HttpGet("{id}")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<PaymentBatchDto>> GetById(Guid id)
        {
            var batch = await _paymentService.GetPaymentBatchAsync(id);
            return batch == null ? NotFound() : Ok(batch);
        }

        /// <summary>Returns current-actor AP-004/TDC-0506 readiness for an exact payment batch.</summary>
        [HttpGet("{id}/sod-readiness")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<ProcurementInvoicePaymentSodReadinessDto>> GetSodReadiness(Guid id)
        {
            if (_invoicePaymentSod == null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    code = ProcurementInvoicePaymentSodRules.EvidenceCode,
                    message = "The authoritative invoice/payment SOD service is unavailable."
                });
            try
            {
                return Ok(await _invoicePaymentSod.GetBatchReadinessAsync(id, HttpContext.TraceIdentifier));
            }
            catch (ProcurementInvoicePaymentSodNotFoundException ex)
            {
                return NotFound(new { code = "AP_PAYMENT_BATCH_NOT_FOUND", message = ex.Message });
            }
        }

        /// <summary>Creates a new payment batch from a list of approved vendor invoice IDs.</summary>
        [HttpPost]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<PaymentBatchDto>> Create([FromBody] PaymentBatchCreateDto dto)
        {
            try
            {
                var batch = await _paymentService.CreatePaymentBatchAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = batch.Id }, batch);
            }
            catch (VendorPaymentControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "AP_PAYMENT_SOURCE_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Approves a pending payment batch for processing.</summary>
        [HttpPost("{id}/approve")]
        [Authorize(Policy = FinancePermissions.ApproveApPayments)]
        public async Task<ActionResult<PaymentBatchDto>> Approve(Guid id)
        {
            try { return Ok(await _paymentService.ApprovePaymentBatchAsync(id)); }
            catch (ProcurementInvoicePaymentSodBlockedException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message, readiness = ex.Readiness });
            }
            catch (VendorPaymentControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Processes an approved payment batch against its immutable, revalidated invoice selections.</summary>
        [HttpPost("{id}/process")]
        [Authorize(Policy = FinancePermissions.ProcessApPayments)]
        public async Task<ActionResult<PaymentBatchDto>> Process(Guid id)
        {
            try { return Ok(await _paymentService.ProcessPaymentBatchAsync(id)); }
            catch (ProcurementInvoicePaymentSodBlockedException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message, readiness = ex.Readiness });
            }
            catch (VendorPaymentControlException ex)
            {
                return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AP REPORTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Provides Accounts Payable reporting: aging, cash requirement forecast,
    /// supplier statements, withholding tax summaries, and AP dashboard metrics.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/ap/reports")]
    public class ApReportsController : ControllerBase
    {
        private readonly IApReportsService _reportsService;

        public ApReportsController(IApReportsService reportsService)
        {
            _reportsService = reportsService;
        }

        /// <summary>Generates a summary AP aging report showing outstanding balances by aging bucket.</summary>
        [HttpGet("aging")]
        public async Task<ActionResult<ApAgingReportDto>> GetAgingReport(
            [FromQuery] DateTime? asOfDate = null, [FromQuery] Guid? supplierId = null)
            => Ok(await _reportsService.GetAgingReportAsync(asOfDate, supplierId));

        /// <summary>Rebuilds the AP settlement read model from posted AP source documents and posting events.</summary>
        [HttpPost("settlements/rebuild")]
        public async Task<ActionResult<SubledgerSettlementRebuildResultDto>> RebuildSettlementReadModel([FromQuery] DateTime? asOfDate = null)
            => Ok(await _reportsService.RebuildSettlementReadModelAsync(asOfDate));

        /// <summary>Reconciles the AP settlement read model to the posted AP control account balance.</summary>
        [HttpGet("control-reconciliation")]
        public async Task<ActionResult<SubledgerControlReconciliationDto>> GetControlReconciliation([FromQuery] DateTime? asOfDate = null)
            => Ok(await _reportsService.GetControlReconciliationAsync(asOfDate));

        /// <summary>
        /// Reconciles Procurement commitments/receipts to AP invoices/payments,
        /// central Finance postings/reversals, retention, and contract milestones.
        /// </summary>
        [HttpGet("procurement-reconciliation")]
        [Authorize(Policy = FinancePermissions.RunFinanceReports)]
        public async Task<ActionResult<ProcurementFinanceReconciliationReportDto>> GetProcurementFinanceReconciliation(
            [FromQuery] DateTime? asOfDate = null,
            [FromQuery] Guid? purchaseOrderId = null)
        {
            try
            {
                return Ok(await _reportsService.GetProcurementFinanceReconciliationAsync(
                    asOfDate, purchaseOrderId));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "PROCUREMENT_RECONCILIATION_SOURCE_NOT_FOUND", message = ex.Message });
            }
        }

        /// <summary>Exports the same AP-005/TDC-0508 reconciliation as CSV.</summary>
        [HttpGet("procurement-reconciliation/export")]
        [Authorize(Policy = FinancePermissions.ExportFinanceReports)]
        public async Task<IActionResult> ExportProcurementFinanceReconciliation(
            [FromQuery] DateTime? asOfDate = null,
            [FromQuery] Guid? purchaseOrderId = null,
            [FromQuery] string format = "Csv")
        {
            try
            {
                var content = await _reportsService.ExportProcurementFinanceReconciliationAsync(
                    asOfDate, purchaseOrderId, format);
                return File(
                    content,
                    "text/csv; charset=utf-8",
                    $"procurement-finance-reconciliation-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { code = "PROCUREMENT_RECONCILIATION_SOURCE_NOT_FOUND", message = ex.Message });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { code = "PROCUREMENT_RECONCILIATION_FORMAT_UNSUPPORTED", message = ex.Message });
            }
        }

        /// <summary>
        /// Shows supplier advances and other posted but unapplied vendor payments. These balances are
        /// intentionally not included in invoice aging because they have no invoice due date.
        /// </summary>
        [HttpGet("unapplied-settlements")]
        [Authorize(Policy = FinancePermissions.ViewFinance)]
        public async Task<ActionResult<SubledgerUnappliedSettlementReportDto>> GetUnappliedSettlements(
            [FromQuery] DateTime? asOfDate = null,
            [FromQuery] Guid? supplierId = null)
            => Ok(await _reportsService.GetUnappliedSettlementsAsync(asOfDate, supplierId));

        /// <summary>Generates a detailed AP aging report with per-supplier, per-invoice breakdown.</summary>
        [HttpGet("aging/detailed")]
        public async Task<ActionResult<ApAgingReportDto>> GetDetailedAgingReport(
            [FromQuery] DateTime? asOfDate = null, [FromQuery] Guid? supplierId = null)
            => Ok(await _reportsService.GetDetailedAgingReportAsync(asOfDate, supplierId));

        /// <summary>Generates a cash requirement forecast showing amounts due by period.</summary>
        [HttpGet("cash-forecast")]
        public async Task<ActionResult<CashRequirementForecastDto>> GetCashForecast([FromQuery] DateTime? asOfDate = null)
            => Ok(await _reportsService.GetCashRequirementForecastAsync(asOfDate));

        /// <summary>Generates a supplier statement with opening balance, transactions, and closing balance.</summary>
        [HttpGet("supplier-statement")]
        public async Task<ActionResult<SupplierStatementDto>> GetSupplierStatement(
            [FromQuery] Guid supplierId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            try { return Ok(await _reportsService.GetSupplierStatementAsync(supplierId, fromDate, toDate)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Generates a detailed supplier ledger for one or more suppliers/business partners.</summary>
        [HttpGet("supplier-detailed-ledger")]
        public async Task<ActionResult<SupplierDetailedLedgerReportDto>> GetSupplierDetailedLedger(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] List<Guid>? supplierIds = null,
            [FromQuery] bool showSupplierCurrency = false)
        {
            try
            {
                return Ok(await _reportsService.GetSupplierDetailedLedgerAsync(fromDate, toDate, supplierIds, showSupplierCurrency));
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Generates a withholding tax summary grouped by supplier for a date range.</summary>
        [HttpGet("withholding-tax")]
        public async Task<ActionResult<WithholdingTaxSummaryDto>> GetWithholdingTaxSummary(
            [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
            => Ok(await _reportsService.GetWithholdingTaxSummaryAsync(fromDate, toDate));

        /// <summary>Retrieves AP dashboard summary metrics (totals, overdue, discounts, pending items).</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<ApSummaryDto>> GetApSummary()
            => Ok(await _reportsService.GetApSummaryAsync());

        /// <summary>Returns the audited AP-006 match-exception and corrective-action register.</summary>
        [HttpGet("three-way-match-exceptions")]
        [Authorize(Policy = FinancePermissions.RunFinanceReports)]
        public async Task<ActionResult<VendorInvoiceMatchExceptionReportDto>> GetThreeWayMatchExceptions(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] VendorInvoiceMatchExceptionStatus? status = null,
            [FromQuery] Guid? supplierId = null)
            => Ok(await _reportsService.GetThreeWayMatchExceptionsAsync(fromDate, toDate, status, supplierId));

        /// <summary>Exports the AP-006 register without creating or allocating any payment.</summary>
        [HttpGet("three-way-match-exceptions/export")]
        [Authorize(Policy = FinancePermissions.ExportFinanceReports)]
        public async Task<IActionResult> ExportThreeWayMatchExceptions(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] VendorInvoiceMatchExceptionStatus? status = null,
            [FromQuery] Guid? supplierId = null,
            [FromQuery] string format = "Csv")
        {
            var content = await _reportsService.ExportThreeWayMatchExceptionsAsync(
                fromDate, toDate, status, supplierId, format);
            return File(content, "text/csv; charset=utf-8", $"ap-match-exceptions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }
    }
}
