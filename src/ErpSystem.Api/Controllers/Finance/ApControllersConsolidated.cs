using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
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

        public VendorInvoiceController(
            IVendorInvoiceService invoiceService,
            ICurrentUserService currentUserService,
            ApplicationDbContext dbContext)
        {
            _invoiceService = invoiceService;
            _currentUserService = currentUserService;
            _dbContext = dbContext;
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
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Approves a pending vendor invoice, making it eligible for payment.</summary>
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<VendorInvoiceDto>> Approve(Guid id, [FromBody] string? comments = null)
        {
            if (!await HasAnyPermissionAsync("Finance.AP.Invoices.Approve"))
                return Forbid();
            try { return Ok(await _invoiceService.ApproveAsync(id, comments)); }
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

        /// <summary>Voids a vendor invoice, marking it as cancelled (requires no active allocations).</summary>
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
            try { return Ok(await _invoiceService.PerformTwoWayMatchAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Performs 3-way matching (invoice ↔ PO ↔ goods receipt) for a vendor invoice.</summary>
        [HttpPost("{id}/match/three-way")]
        public async Task<ActionResult<InvoiceMatchingResultDto>> ThreeWayMatch(Guid id)
        {
            try { return Ok(await _invoiceService.PerformThreeWayMatchAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Retrieves the current matching result/status for a vendor invoice.</summary>
        [HttpGet("{id}/match")]
        public async Task<ActionResult<InvoiceMatchingResultDto>> GetMatchingResult(Guid id)
        {
            try { return Ok(await _invoiceService.GetMatchingResultAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

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

        public VendorPaymentController(IVendorPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>Retrieves a paginated list of vendor payments.</summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<VendorPaymentDto>>> GetAll([FromQuery] VendorPaymentQueryDto query)
            => Ok(await _paymentService.GetAllAsync(query));

        /// <summary>Retrieves a single vendor payment by ID, including allocations.</summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<VendorPaymentDto>> GetById(Guid id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            return payment == null ? NotFound() : Ok(payment);
        }

        /// <summary>Creates a new vendor payment with optional invoice allocations.</summary>
        [HttpPost]
        public async Task<ActionResult<VendorPaymentDto>> Create([FromBody] VendorPaymentCreateDto dto)
        {
            try
            {
                var payment = await _paymentService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = payment.Id }, payment);
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        // ── Allocations ─────────────────────────────────────────────────

        /// <summary>Allocates a payment against one or more outstanding vendor invoices.</summary>
        [HttpPost("{id}/allocate")]
        public async Task<ActionResult<VendorPaymentAllocationResultDto>> Allocate(
            Guid id, [FromBody] List<VendorPaymentAllocationCreateDto> allocations)
        {
            try { return Ok(await _paymentService.AllocatePaymentAsync(id, allocations)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Retrieves all allocations for a specific vendor payment.</summary>
        [HttpGet("{id}/allocations")]
        public async Task<ActionResult<List<VendorPaymentAllocationDto>>> GetAllocations(Guid id)
            => Ok(await _paymentService.GetPaymentAllocationsAsync(id));

        /// <summary>Reverses a specific payment allocation with a mandatory reason.</summary>
        [HttpPost("allocations/{allocationId}/reverse")]
        public async Task<IActionResult> ReverseAllocation(Guid allocationId, [FromBody] string reason)
        {
            try { await _paymentService.ReverseAllocationAsync(allocationId, reason); return Ok(); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Lists outstanding (unpaid) invoices for a supplier, for allocation selection.</summary>
        [HttpGet("supplier/{supplierId}/outstanding-invoices")]
        public async Task<ActionResult<List<OutstandingVendorInvoiceDto>>> GetOutstandingInvoices(Guid supplierId)
            => Ok(await _paymentService.GetOutstandingInvoicesAsync(supplierId));

        // ── Payment status ──────────────────────────────────────────────

        /// <summary>Marks a payment as cleared by the bank on the specified date.</summary>
        [HttpPost("{id}/clear")]
        public async Task<ActionResult<VendorPaymentDto>> ClearPayment(Guid id, [FromBody] DateTime clearedDate)
        {
            try { return Ok(await _paymentService.ClearPaymentAsync(id, clearedDate)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Voids a vendor payment, reversing all allocations and restoring invoice balances.</summary>
        [HttpPost("{id}/void")]
        public async Task<ActionResult<VendorPaymentDto>> VoidPayment(Guid id, [FromBody] string reason)
        {
            try { return Ok(await _paymentService.VoidPaymentAsync(id, reason)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        // ── Early payment discount ──────────────────────────────────────

        /// <summary>Calculates the early-payment discount available for a vendor invoice.</summary>
        [HttpGet("discount/calculate")]
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

        public PaymentBatchController(IVendorPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>Retrieves a paginated list of payment batches.</summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<PaymentBatchDto>>> GetAll([FromQuery] PaymentBatchQueryDto query)
            => Ok(await _paymentService.GetAllBatchesAsync(query));

        /// <summary>Retrieves a single payment batch by ID, including its items.</summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentBatchDto>> GetById(Guid id)
        {
            var batch = await _paymentService.GetPaymentBatchAsync(id);
            return batch == null ? NotFound() : Ok(batch);
        }

        /// <summary>Creates a new payment batch from a list of approved vendor invoice IDs.</summary>
        [HttpPost]
        public async Task<ActionResult<PaymentBatchDto>> Create([FromBody] PaymentBatchCreateDto dto)
        {
            try
            {
                var batch = await _paymentService.CreatePaymentBatchAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = batch.Id }, batch);
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Approves a pending payment batch for processing.</summary>
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<PaymentBatchDto>> Approve(Guid id)
        {
            try { return Ok(await _paymentService.ApprovePaymentBatchAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Processes an approved payment batch, auto-allocating payments to invoices by due date.</summary>
        [HttpPost("{id}/process")]
        public async Task<ActionResult<PaymentBatchDto>> Process(Guid id)
        {
            try { return Ok(await _paymentService.ProcessPaymentBatchAsync(id)); }
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

        /// <summary>Generates a withholding tax summary grouped by supplier for a date range.</summary>
        [HttpGet("withholding-tax")]
        public async Task<ActionResult<WithholdingTaxSummaryDto>> GetWithholdingTaxSummary(
            [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
            => Ok(await _reportsService.GetWithholdingTaxSummaryAsync(fromDate, toDate));

        /// <summary>Retrieves AP dashboard summary metrics (totals, overdue, discounts, pending items).</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<ApSummaryDto>> GetApSummary()
            => Ok(await _reportsService.GetApSummaryAsync());
    }
}
