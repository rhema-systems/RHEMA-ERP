using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages customer invoices throughout their full lifecycle within the Accounts Receivable module.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Creating, updating, and deleting draft invoices for customer billing
    /// - Sending finalized invoices to customers via email or print
    /// - Voiding invoices that were issued in error with an audit-trail reason
    /// - Querying invoices with server-side filtering, sorting, and pagination
    /// - Reviewing payment allocations applied against a specific invoice
    ///
    /// **Integration Pattern:**
    /// - Works in tandem with <see cref="PaymentController"/> for payment-to-invoice allocation
    /// - Invoice balances feed into <see cref="ArReportsController"/> aging and summary reports
    /// - Sent invoices post journal entries to the General Ledger via the GL integration layer
    ///
    /// **Business Rules:**
    /// - Invoices can only be edited while in Draft status; sent invoices are immutable
    /// - Voiding a sent invoice reverses the original GL journal entry automatically
    /// - Deleting is only permitted for Draft invoices that have never been sent
    /// - Invoice numbers are system-generated and sequential per fiscal year
    ///
    /// **Authorization:** Requires authenticated user with AR Invoice permissions
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/ar/invoices")]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _dbContext;

        public InvoiceController(
            IInvoiceService invoiceService,
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

        /// <summary>
        /// Retrieves a paginated list of customer invoices filtered by the supplied query parameters.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating the invoice list grid in the AR workspace with server-side pagination
        /// - Searching invoices by customer, date range, status, or amount thresholds
        /// - Exporting filtered invoice data for reconciliation or audit purposes
        ///
        /// **Integration Pattern:**
        /// - Supports the main AR invoice listing page and any dashboard widgets that display invoice counts
        /// - Query parameters map to indexed database columns for optimal performance
        ///
        /// **Business Rules:**
        /// - Results are scoped to the current user's authorized company/branch context
        /// - Default sort order is by invoice date descending (most recent first)
        /// - Maximum page size is capped server-side to prevent excessive payloads
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice read permission
        /// </remarks>
        /// <param name="query">Filter, sort, and pagination parameters for the invoice query.</param>
        /// <returns>A paged result set containing matching <see cref="InvoiceDto"/> records and total count metadata.</returns>
        /// <response code="200">Successfully returned the paginated invoice list.</response>
        /// <response code="400">Invalid query parameters (e.g., negative page number or invalid date range).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during invoice retrieval.</response>
        [HttpGet]
        public async Task<ActionResult<PagedResult<InvoiceDto>>> GetAll([FromQuery] InvoiceQueryDto query)
            => Ok(await _invoiceService.GetAllAsync(query));

        /// <summary>
        /// Retrieves a single customer invoice by its unique identifier, including line items and tax details.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading the full invoice detail view for review or editing
        /// - Fetching invoice data before performing actions such as send, void, or allocate
        /// - Retrieving invoice information for PDF generation or email preview
        ///
        /// **Integration Pattern:**
        /// - Commonly called before navigating to the invoice edit or detail page
        /// - The returned DTO includes nested line items, tax breakdowns, and current allocation summary
        ///
        /// **Business Rules:**
        /// - Returns the complete invoice entity including computed fields (balance due, tax totals)
        /// - Soft-deleted invoices are excluded from lookup
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice read permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the invoice to retrieve.</param>
        /// <returns>The full <see cref="InvoiceDto"/> if found.</returns>
        /// <response code="200">Successfully returned the invoice details.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No invoice exists with the specified identifier.</response>
        /// <response code="500">Internal server error during invoice retrieval.</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceDto>> GetById(Guid id)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            return invoice == null ? NotFound() : Ok(invoice);
        }

        /// <summary>
        /// Creates a new customer invoice in Draft status with the supplied line items and terms.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Creating a new sales invoice for goods or services delivered to a customer
        /// - Generating invoices from approved sales orders or delivery notes
        /// - Setting up recurring invoice templates for subscription-based billing
        ///
        /// **Integration Pattern:**
        /// - After creation, the invoice can be sent via the <c>POST {id}/send</c> endpoint
        /// - Line items reference inventory items or GL account codes for revenue recognition
        /// - Tax calculations are applied automatically based on the customer's tax profile
        ///
        /// **Business Rules:**
        /// - Invoice is created in Draft status; no GL posting occurs until it is sent
        /// - Customer must exist and be active in the system
        /// - At least one line item is required with a positive quantity and unit price
        /// - Payment terms default to the customer's configured terms if not explicitly provided
        /// - Invoice number is auto-generated upon creation
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice create permission
        /// </remarks>
        /// <param name="dto">The invoice creation payload containing customer, line items, dates, and terms.</param>
        /// <returns>The newly created <see cref="InvoiceDto"/> with its generated identifier and invoice number.</returns>
        /// <response code="201">Invoice successfully created; returns the new invoice with a Location header.</response>
        /// <response code="400">Validation failure (e.g., missing customer, invalid line items, or business rule violation).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during invoice creation.</response>
        [HttpPost]
        public async Task<ActionResult<InvoiceDto>> Create([FromBody] InvoiceCreateDto dto)
        {
            if (!await HasAnyPermissionAsync("Finance.AR.Invoices.Create", "Finance.AR.Invoices.Write"))
                return Forbid();

            try
            {
                var invoice = await _invoiceService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Updates an existing draft invoice with revised line items, dates, or terms.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Correcting line item quantities, prices, or descriptions before sending
        /// - Changing payment terms or due dates on a draft invoice
        /// - Adding or removing line items prior to finalization
        ///
        /// **Integration Pattern:**
        /// - Only invoices in Draft status can be updated; sent invoices must be voided and re-issued
        /// - The updated invoice is returned with recalculated totals and tax amounts
        ///
        /// **Business Rules:**
        /// - The route <paramref name="id"/> must match the <c>Id</c> in the request body to prevent accidental cross-updates
        /// - Invoices that have been sent or voided cannot be modified
        /// - All line-item validations from creation also apply to updates
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice update permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the invoice to update (must match dto.Id).</param>
        /// <param name="dto">The invoice update payload containing revised fields and line items.</param>
        /// <returns>The updated <see cref="InvoiceDto"/> with recalculated totals.</returns>
        /// <response code="200">Invoice successfully updated.</response>
        /// <response code="400">Validation failure or ID mismatch between route and body.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No invoice exists with the specified identifier.</response>
        /// <response code="500">Internal server error during invoice update.</response>
        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceDto>> Update(Guid id, [FromBody] InvoiceUpdateDto dto)
        {
            if (id != dto.Id) return BadRequest("ID mismatch");
            if (!await HasAnyPermissionAsync("Finance.AR.Invoices.Edit", "Finance.AR.Invoices.Write"))
                return Forbid();
            try { return Ok(await _invoiceService.UpdateAsync(dto)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Permanently deletes a draft invoice that has never been sent to a customer.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing invoices created in error before they are finalized
        /// - Cleaning up duplicate or test invoices during data entry
        ///
        /// **Integration Pattern:**
        /// - Only draft invoices can be deleted; use the <c>POST {id}/void</c> endpoint for sent invoices
        /// - Deletion is permanent and cannot be undone; the invoice number is not reused
        ///
        /// **Business Rules:**
        /// - Invoices that have been sent, partially paid, or voided cannot be deleted
        /// - Invoices with existing payment allocations cannot be deleted
        /// - An audit log entry is created for the deletion event
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice delete permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the draft invoice to delete.</param>
        /// <returns>No content on successful deletion.</returns>
        /// <response code="204">Invoice successfully deleted.</response>
        /// <response code="400">Invoice cannot be deleted (e.g., already sent, has allocations, or business rule violation).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No invoice exists with the specified identifier.</response>
        /// <response code="500">Internal server error during invoice deletion.</response>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!await HasAnyPermissionAsync("Finance.AR.Invoices.Delete", "Finance.AR.Invoices.Write"))
                return Forbid();
            try { await _invoiceService.DeleteAsync(id); return NoContent(); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Finalizes and sends a draft invoice to the customer, triggering GL journal posting.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Sending a completed invoice to the customer via email or marking it as issued
        /// - Transitioning an invoice from Draft to Sent status to begin the collection cycle
        /// - Triggering automatic GL entries for revenue recognition (Debit: Accounts Receivable, Credit: Revenue)
        ///
        /// **Integration Pattern:**
        /// - Posts a journal entry to the General Ledger upon successful status transition
        /// - Updates the customer's outstanding balance in the AR sub-ledger
        /// - The invoice becomes visible in aging reports and the collections dashboard
        ///
        /// **Business Rules:**
        /// - Only invoices in Draft status can be sent
        /// - The invoice must have at least one valid line item
        /// - Once sent, the invoice is immutable; corrections require voiding and re-issuing
        /// - The due date is calculated from the invoice date plus the payment terms
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice send permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the draft invoice to send.</param>
        /// <returns>The updated <see cref="InvoiceDto"/> reflecting its new Sent status.</returns>
        /// <response code="200">Invoice successfully sent and GL journal posted.</response>
        /// <response code="400">Invoice cannot be sent (e.g., not in Draft status or validation failure).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No invoice exists with the specified identifier.</response>
        /// <response code="500">Internal server error during invoice send process.</response>
        [HttpPost("{id}/send")]
        public async Task<ActionResult<InvoiceDto>> Send(Guid id)
        {
            if (!await HasAnyPermissionAsync("Finance.AR.Invoices.Send"))
                return Forbid();
            try { return Ok(await _invoiceService.SendInvoiceAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("{id}/post")]
        public async Task<ActionResult<InvoiceDto>> Post(Guid id)
        {
            if (!await HasAnyPermissionAsync("Finance.AR.Invoices.ApprovePost"))
                return Forbid();
            try { return Ok(await _invoiceService.PostAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Voids a previously sent invoice, reversing its GL journal entry and updating the customer balance.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Cancelling an invoice that was issued in error (wrong customer, wrong amount, duplicate)
        /// - Voiding an invoice as part of a dispute resolution with the customer
        /// - Reversing revenue recognition for goods or services not delivered
        ///
        /// **Integration Pattern:**
        /// - Automatically posts a reversing GL journal entry (Debit: Revenue, Credit: Accounts Receivable)
        /// - Releases any partial payment allocations back to unallocated status on the payment
        /// - The voided invoice remains in the system for audit trail purposes but is excluded from aging
        ///
        /// **Business Rules:**
        /// - Only sent invoices can be voided; draft invoices should be deleted instead
        /// - A reason is mandatory for audit compliance and must describe why the invoice is being voided
        /// - Fully paid invoices may require payment reversal before voiding
        /// - The void action is irreversible once confirmed
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice void permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the sent invoice to void.</param>
        /// <param name="reason">A mandatory explanation for why the invoice is being voided (for audit trail).</param>
        /// <returns>The updated <see cref="InvoiceDto"/> reflecting its new Voided status.</returns>
        /// <response code="200">Invoice successfully voided and reversing GL journal posted.</response>
        /// <response code="400">Invoice cannot be voided (e.g., not in Sent status, missing reason, or business rule violation).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No invoice exists with the specified identifier.</response>
        /// <response code="500">Internal server error during invoice void process.</response>
        [HttpPost("{id}/void")]
        public async Task<ActionResult<InvoiceDto>> Void(Guid id, [FromBody] string reason)
        {
            if (!await HasAnyPermissionAsync("Finance.AR.Invoices.Void"))
                return Forbid();
            try { return Ok(await _invoiceService.VoidInvoiceAsync(id, reason)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Retrieves all payment allocations applied against a specific invoice.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing the payment history for an invoice to understand how much has been collected
        /// - Auditing which payments were applied to a particular invoice and when
        /// - Verifying allocation accuracy during month-end reconciliation
        ///
        /// **Integration Pattern:**
        /// - Allocations are created via the <see cref="PaymentController"/> <c>POST {id}/allocate</c> endpoint
        /// - Each allocation links a customer payment to this invoice with a specific applied amount
        /// - The sum of allocations determines the invoice's outstanding balance
        ///
        /// **Business Rules:**
        /// - Only sent invoices can have payment allocations
        /// - The total allocated amount cannot exceed the invoice total
        /// - Voided allocations are included in the response with their voided status for audit completeness
        ///
        /// **Authorization:** Requires authenticated user with AR Invoice read permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the invoice whose allocations to retrieve.</param>
        /// <returns>A list of <see cref="PaymentAllocationDto"/> records showing all payments applied to this invoice.</returns>
        /// <response code="200">Successfully returned the list of payment allocations.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No invoice exists with the specified identifier.</response>
        /// <response code="500">Internal server error during allocation retrieval.</response>
        [HttpGet("{id}/allocations")]
        public async Task<ActionResult<List<PaymentAllocationDto>>> GetAllocations(Guid id)
            => Ok(await _invoiceService.GetInvoiceAllocationsAsync(id));
    }

    /// <summary>
    /// Manages customer payments, payment allocations, credit notes, and bank clearing within the Accounts Receivable module.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Recording customer payments received via cash, cheque, bank transfer, or mobile money
    /// - Allocating received payments against one or more outstanding customer invoices
    /// - Processing cheque clearances and handling bounced payment reversals
    /// - Issuing credit notes to reduce a customer's outstanding balance
    ///
    /// **Integration Pattern:**
    /// - Payments are allocated to invoices managed by <see cref="InvoiceController"/>
    /// - Cleared payments post journal entries to the General Ledger (Debit: Bank, Credit: AR)
    /// - Bounced payments reverse the original GL entries and restore invoice balances
    /// - Payment data feeds into <see cref="ArReportsController"/> for collections and trend analysis
    ///
    /// **Business Rules:**
    /// - Payments must reference a valid, active customer
    /// - Payment amounts must be positive; use credit notes for negative adjustments
    /// - Allocations cannot exceed the payment's unallocated balance or the invoice's outstanding balance
    /// - Cheque payments require bank clearing before GL posting is finalized
    ///
    /// **Authorization:** Requires authenticated user with AR Payment permissions
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/ar/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Retrieves a paginated list of customer payments filtered by the supplied query parameters.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating the payments list grid in the AR workspace with server-side pagination
        /// - Searching payments by customer, date range, payment method, or clearance status
        /// - Filtering unallocated payments to identify funds awaiting invoice matching
        ///
        /// **Integration Pattern:**
        /// - Supports the main AR payments listing page and bank reconciliation workflows
        /// - Query parameters map to indexed database columns for optimal performance
        ///
        /// **Business Rules:**
        /// - Results are scoped to the current user's authorized company/branch context
        /// - Default sort order is by payment date descending (most recent first)
        /// - Includes computed fields such as allocated amount and unallocated balance
        ///
        /// **Authorization:** Requires authenticated user with AR Payment read permission
        /// </remarks>
        /// <param name="query">Filter, sort, and pagination parameters for the payment query.</param>
        /// <returns>A paged result set containing matching <see cref="CustomerPaymentDto"/> records and total count metadata.</returns>
        /// <response code="200">Successfully returned the paginated payment list.</response>
        /// <response code="400">Invalid query parameters (e.g., negative page number or invalid date range).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during payment retrieval.</response>
        [HttpGet]
        public async Task<ActionResult<PagedResult<CustomerPaymentDto>>> GetAll([FromQuery] PaymentQueryDto query)
            => Ok(await _paymentService.GetAllAsync(query));

        /// <summary>
        /// Retrieves a single customer payment by its unique identifier, including allocation details.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading the full payment detail view for review, allocation, or clearance
        /// - Fetching payment data before performing actions such as allocate, clear, or bounce
        /// - Reviewing payment allocation breakdown across multiple invoices
        ///
        /// **Integration Pattern:**
        /// - The returned DTO includes nested allocation records showing which invoices have been paid
        /// - Used as a prerequisite call before allocation or status change operations
        ///
        /// **Business Rules:**
        /// - Returns the complete payment entity including computed fields (allocated total, unallocated balance)
        /// - Soft-deleted payments are excluded from lookup
        ///
        /// **Authorization:** Requires authenticated user with AR Payment read permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer payment to retrieve.</param>
        /// <returns>The full <see cref="CustomerPaymentDto"/> if found.</returns>
        /// <response code="200">Successfully returned the payment details.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No payment exists with the specified identifier.</response>
        /// <response code="500">Internal server error during payment retrieval.</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerPaymentDto>> GetById(Guid id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            return payment == null ? NotFound() : Ok(payment);
        }

        /// <summary>
        /// Records a new customer payment received via any supported payment method.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Recording a cash, cheque, bank transfer, or mobile money payment from a customer
        /// - Logging advance payments or deposits before invoices are issued
        /// - Capturing payment details from bank statement imports for later allocation
        ///
        /// **Integration Pattern:**
        /// - After creation, payments can be allocated to invoices via the <c>POST {id}/allocate</c> endpoint
        /// - Cheque payments proceed through the clearing workflow via <c>POST {id}/clear</c>
        /// - Posts an initial GL journal entry (Debit: Uncleared Receipts or Bank, Credit: AR Control)
        ///
        /// **Business Rules:**
        /// - Customer must exist and be active in the system
        /// - Payment amount must be a positive value
        /// - Payment method must be a valid, configured method (Cash, Cheque, Bank Transfer, Mobile Money, etc.)
        /// - Reference number (e.g., cheque number, transaction ID) should be unique per customer
        /// - Payment date cannot be in a closed accounting period
        ///
        /// **Authorization:** Requires authenticated user with AR Payment create permission
        /// </remarks>
        /// <param name="dto">The payment creation payload containing customer, amount, method, date, and reference details.</param>
        /// <returns>The newly created <see cref="CustomerPaymentDto"/> with its generated identifier.</returns>
        /// <response code="201">Payment successfully recorded; returns the new payment with a Location header.</response>
        /// <response code="400">Validation failure (e.g., invalid customer, negative amount, or business rule violation).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during payment creation.</response>
        [HttpPost]
        public async Task<ActionResult<CustomerPaymentDto>> Create([FromBody] PaymentCreateDto dto)
        {
            try
            {
                var payment = await _paymentService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = payment.Id }, payment);
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Updates an existing customer payment record with revised details.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Correcting payment amount, date, or reference number before allocation
        /// - Updating payment method or bank account details after initial entry
        /// - Amending notes or internal references on an uncleared payment
        ///
        /// **Integration Pattern:**
        /// - Only unallocated or partially allocated payments can be updated
        /// - If the payment amount is reduced below the currently allocated total, the update is rejected
        ///
        /// **Business Rules:**
        /// - The route <paramref name="id"/> must match the <c>Id</c> in the request body to prevent accidental cross-updates
        /// - Cleared or bounced payments cannot be modified
        /// - Fully allocated payments cannot have their amount reduced
        /// - Changes are audit-logged with the previous and new values
        ///
        /// **Authorization:** Requires authenticated user with AR Payment update permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the payment to update (must match dto.Id).</param>
        /// <param name="dto">The payment update payload containing revised fields.</param>
        /// <returns>The updated <see cref="CustomerPaymentDto"/> with recalculated allocation balances.</returns>
        /// <response code="200">Payment successfully updated.</response>
        /// <response code="400">Validation failure or ID mismatch between route and body.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No payment exists with the specified identifier.</response>
        /// <response code="500">Internal server error during payment update.</response>
        [HttpPut("{id}")]
        public async Task<ActionResult<CustomerPaymentDto>> Update(Guid id, [FromBody] PaymentUpdateDto dto)
        {
            if (id != dto.Id) return BadRequest("ID mismatch");
            try { return Ok(await _paymentService.UpdateAsync(dto)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Allocates a customer payment (or a portion of it) against one or more outstanding invoices.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Applying a received payment to the specific invoice(s) it was intended to settle
        /// - Splitting a lump-sum payment across multiple outstanding invoices
        /// - Matching bank statement entries to their corresponding invoices during reconciliation
        ///
        /// **Integration Pattern:**
        /// - References invoices managed by <see cref="InvoiceController"/>; reduces their outstanding balances
        /// - Allocation posts a GL journal entry transferring the amount from unallocated receipts to AR clearing
        /// - Fully settled invoices are automatically marked as Paid
        ///
        /// **Business Rules:**
        /// - The route <paramref name="id"/> must match the <c>CustomerPaymentId</c> in the request body
        /// - Allocation amount cannot exceed the payment's remaining unallocated balance
        /// - Allocation amount cannot exceed the target invoice's outstanding balance
        /// - Only sent (active) invoices can receive payment allocations
        /// - Over-allocation is not permitted; partial allocation is allowed
        ///
        /// **Authorization:** Requires authenticated user with AR Payment allocate permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the payment to allocate (must match dto.CustomerPaymentId).</param>
        /// <param name="dto">The allocation payload specifying which invoice(s) and amounts to apply.</param>
        /// <returns>A <see cref="PaymentAllocationResultDto"/> summarizing the allocation outcome and updated balances.</returns>
        /// <response code="200">Payment successfully allocated against the specified invoice(s).</response>
        /// <response code="400">Validation failure, ID mismatch, or insufficient unallocated balance.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">Payment or target invoice not found.</response>
        /// <response code="500">Internal server error during payment allocation.</response>
        [HttpPost("{id}/allocate")]
        public async Task<ActionResult<PaymentAllocationResultDto>> Allocate(Guid id, [FromBody] PaymentAllocation_CreateDto dto)
        {
            if (id != dto.CustomerPaymentId) return BadRequest("ID mismatch");
            try { return Ok(await _paymentService.AllocatePaymentAsync(dto)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Posts a customer receipt to the general ledger through the central finance posting engine.
        /// </summary>
        [HttpPost("{id}/post")]
        public async Task<ActionResult<CustomerPaymentDto>> Post(Guid id)
        {
            try { return Ok(await _paymentService.PostAsync(id)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Marks a customer payment as cleared by the bank on the specified date.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Confirming that a cheque has been honoured and funds have been received by the bank
        /// - Recording the bank clearance date for bank transfer payments during reconciliation
        /// - Updating the payment status as part of the bank statement import/matching workflow
        ///
        /// **Integration Pattern:**
        /// - Posts the final GL journal entry moving funds from Uncleared Receipts to the Bank account
        /// - The cleared date is used in bank reconciliation reports to match statement entries
        /// - Updates the customer's cleared payment balance in the AR sub-ledger
        ///
        /// **Business Rules:**
        /// - Only uncleared payments can be marked as cleared
        /// - The cleared date must be on or after the original payment date
        /// - The cleared date cannot be in a closed accounting period
        /// - Bounced payments cannot be cleared
        ///
        /// **Authorization:** Requires authenticated user with AR Payment clear permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the payment to mark as cleared.</param>
        /// <param name="clearedDate">The date the bank confirmed the payment was cleared.</param>
        /// <returns>The updated <see cref="CustomerPaymentDto"/> reflecting its new Cleared status.</returns>
        /// <response code="200">Payment successfully marked as cleared.</response>
        /// <response code="400">Payment cannot be cleared (e.g., already cleared, invalid date, or business rule violation).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No payment exists with the specified identifier.</response>
        /// <response code="500">Internal server error during payment clearance.</response>
        [HttpPost("{id}/clear")]
        public async Task<ActionResult<CustomerPaymentDto>> ClearPayment(Guid id, [FromBody] DateTime clearedDate)
        {
            try { return Ok(await _paymentService.ClearPaymentAsync(id, clearedDate)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Marks a customer payment as bounced, reversing its GL entries and restoring invoice balances.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Recording a cheque that was returned by the bank due to insufficient funds
        /// - Handling a failed bank transfer or reversed mobile money payment
        /// - Initiating the collections follow-up process for dishonoured payments
        ///
        /// **Integration Pattern:**
        /// - Reverses the original GL journal entries posted during payment creation and clearing
        /// - Automatically de-allocates any invoice allocations, restoring outstanding balances
        /// - Updates the customer's payment history and may trigger credit hold reviews
        /// - The bounced status is reflected in collections dashboard and aging reports
        ///
        /// **Business Rules:**
        /// - A reason is mandatory for audit compliance and must describe why the payment bounced
        /// - Only cleared or uncleared payments can be marked as bounced (not already bounced)
        /// - All existing allocations are reversed automatically
        /// - Previously settled invoices revert to their outstanding status
        /// - The bounce event is recorded in the customer's credit history
        ///
        /// **Authorization:** Requires authenticated user with AR Payment bounce permission
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the payment to mark as bounced.</param>
        /// <param name="reason">A mandatory explanation for why the payment bounced (e.g., "NSF - insufficient funds").</param>
        /// <returns>The updated <see cref="CustomerPaymentDto"/> reflecting its new Bounced status.</returns>
        /// <response code="200">Payment successfully marked as bounced and all reversals processed.</response>
        /// <response code="400">Payment cannot be bounced (e.g., already bounced, missing reason, or business rule violation).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No payment exists with the specified identifier.</response>
        /// <response code="500">Internal server error during bounce processing.</response>
        [HttpPost("{id}/bounced")]
        public async Task<ActionResult<CustomerPaymentDto>> BouncedPayment(Guid id, [FromBody] string reason)
        {
            try { return Ok(await _paymentService.BouncedPaymentAsync(id, reason)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Retrieves all outstanding (unpaid or partially paid) invoices for a specific customer.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating the invoice selection list when allocating a payment to invoices
        /// - Displaying a customer's unpaid invoices during payment entry for quick reference
        /// - Generating a customer-facing statement of outstanding amounts
        ///
        /// **Integration Pattern:**
        /// - Typically called in the payment allocation workflow to present available invoices
        /// - Each invoice shows its total, amount already allocated, and remaining balance
        /// - Results feed into the allocation form for selecting target invoices
        ///
        /// **Business Rules:**
        /// - Only sent invoices with a positive outstanding balance are included
        /// - Voided and draft invoices are excluded
        /// - Results are sorted by invoice date ascending (oldest first) to support FIFO allocation
        ///
        /// **Authorization:** Requires authenticated user with AR Payment read permission
        /// </remarks>
        /// <param name="customerId">The unique identifier (GUID) of the customer whose outstanding invoices to retrieve.</param>
        /// <returns>A list of <see cref="OutstandingInvoiceDto"/> records for the specified customer.</returns>
        /// <response code="200">Successfully returned the list of outstanding invoices.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No customer exists with the specified identifier.</response>
        /// <response code="500">Internal server error during outstanding invoice retrieval.</response>
        [HttpGet("customer/{customerId}/outstanding-invoices")]
        public async Task<ActionResult<List<OutstandingInvoiceDto>>> GetOutstandingInvoices(Guid customerId)
            => Ok(await _paymentService.GetOutstandingInvoicesAsync(customerId));

        /// <summary>
        /// Creates a credit note to reduce a customer's outstanding balance without a cash payment.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Issuing a credit for returned goods or disputed charges
        /// - Applying a volume discount or promotional credit retroactively
        /// - Writing off small outstanding balances that are uneconomical to collect
        ///
        /// **Integration Pattern:**
        /// - Credit notes function as negative payments and can be allocated to invoices like regular payments
        /// - Posts a GL journal entry (Debit: Sales Returns/Discounts, Credit: Accounts Receivable)
        /// - The credit note appears in customer statements and aging reports as a negative balance
        ///
        /// **Business Rules:**
        /// - Credit note amount must be positive (the system applies it as a credit automatically)
        /// - A reason or reference to the original invoice/transaction is required for audit trail
        /// - Credit notes can be allocated to outstanding invoices to reduce their balance
        /// - Unallocated credit notes appear as a credit balance on the customer's account
        /// - Credit notes cannot be issued for more than the customer's total outstanding balance unless overridden
        ///
        /// **Authorization:** Requires authenticated user with AR Credit Note create permission
        /// </remarks>
        /// <param name="dto">The credit note creation payload containing customer, amount, reason, and reference details.</param>
        /// <returns>The newly created credit note as a <see cref="CustomerPaymentDto"/> with a credit note type indicator.</returns>
        /// <response code="200">Credit note successfully created.</response>
        /// <response code="400">Validation failure (e.g., invalid customer, negative amount, or missing reason).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during credit note creation.</response>
        [HttpPost("credit-note")]
        public async Task<ActionResult<CustomerPaymentDto>> CreateCreditNote([FromBody] CreditNoteCreateDto dto)
        {
            try { return Ok(await _paymentService.CreateCreditNoteAsync(dto)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }
    }

    /// <summary>
    /// Provides Accounts Receivable reporting endpoints for aging analysis, customer statements, collections monitoring, sales summaries, and payment trend analysis.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Generating AR aging reports (summary and detailed) for credit management and collections
    /// - Producing customer statements for a specified period for mailing or dispute resolution
    /// - Monitoring the collections dashboard for real-time visibility into overdue receivables
    /// - Analyzing sales performance and payment collection trends over time
    ///
    /// **Integration Pattern:**
    /// - All reports draw from invoice and payment data managed by <see cref="InvoiceController"/> and <see cref="PaymentController"/>
    /// - Aging reports use configurable bucket ranges (Current, 30, 60, 90, 120+ days)
    /// - Reports respect the user's company/branch security context for multi-entity environments
    /// - Output DTOs are structured for direct consumption by frontend grids, charts, and PDF generators
    ///
    /// **Business Rules:**
    /// - Report data reflects the real-time state of invoices and payments (no caching)
    /// - Date parameters default to sensible values (e.g., today for aging, current month for summaries)
    /// - Voided invoices and bounced payments are excluded from active balance calculations
    /// - All monetary values are reported in the entity's base currency
    ///
    /// **Authorization:** Requires authenticated user with AR Reports read permission
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/ar/reports")]
    public class ArReportsController : ControllerBase
    {
        private readonly IArReportsService _reportsService;

        public ArReportsController(IArReportsService reportsService)
        {
            _reportsService = reportsService;
        }

        /// <summary>
        /// Generates a summary AR aging report showing outstanding balances grouped by aging buckets.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Providing management with a high-level view of overdue receivables by aging period
        /// - Supporting credit committee reviews with a snapshot of AR exposure
        /// - Feeding dashboard widgets that display aging distribution charts
        ///
        /// **Integration Pattern:**
        /// - Aging buckets are typically Current, 1-30, 31-60, 61-90, and 90+ days
        /// - Data aggregates outstanding invoice balances net of allocated payments
        /// - Can be exported to PDF or Excel for distribution to stakeholders
        ///
        /// **Business Rules:**
        /// - As-of date defaults to today if not specified, enabling point-in-time aging snapshots
        /// - Only sent invoices with outstanding balances are included
        /// - Voided invoices and bounced payments are excluded from calculations
        /// - Aging is computed from the invoice due date, not the invoice date
        ///
        /// **Authorization:** Requires authenticated user with AR Reports read permission
        /// </remarks>
        /// <param name="asOfDate">Optional point-in-time date for the aging calculation; defaults to today if not provided.</param>
        /// <returns>An <see cref="AgingReportDto"/> containing aggregated totals per aging bucket.</returns>
        /// <response code="200">Successfully generated the aging summary report.</response>
        /// <response code="400">Invalid as-of date parameter.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during report generation.</response>
        [HttpGet("aging")]
        public async Task<ActionResult<AgingReportDto>> GetAgingReport([FromQuery] DateTime? asOfDate = null)
            => Ok(await _reportsService.GetAgingReportAsync(asOfDate));

        /// <summary>
        /// Rebuilds the AR settlement read model from posted AR source documents and posting events.
        /// </summary>
        [HttpPost("settlements/rebuild")]
        public async Task<ActionResult<SubledgerSettlementRebuildResultDto>> RebuildSettlementReadModel([FromQuery] DateTime? asOfDate = null)
            => Ok(await _reportsService.RebuildSettlementReadModelAsync(asOfDate));

        /// <summary>
        /// Reconciles the AR settlement read model to the posted AR control account balance.
        /// </summary>
        [HttpGet("control-reconciliation")]
        public async Task<ActionResult<SubledgerControlReconciliationDto>> GetControlReconciliation([FromQuery] DateTime? asOfDate = null)
            => Ok(await _reportsService.GetControlReconciliationAsync(asOfDate));

        /// <summary>
        /// Generates a detailed AR aging report with per-customer and per-invoice breakdown by aging buckets.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Drilling down from the summary aging report to identify specific overdue invoices
        /// - Supporting collections staff with a prioritized list of overdue customers and invoices
        /// - Providing auditors with a detailed reconciliation of AR balances by aging period
        ///
        /// **Integration Pattern:**
        /// - Extends the summary aging report with customer-level and invoice-level detail rows
        /// - Each row includes customer name, invoice number, original amount, payments applied, and balance
        /// - Can be filtered further on the frontend by customer, salesperson, or amount threshold
        ///
        /// **Business Rules:**
        /// - As-of date defaults to today if not specified
        /// - Includes all sent invoices with outstanding balances, broken down per customer
        /// - Customers with zero outstanding balance are excluded
        /// - Totals reconcile to the summary aging report for the same as-of date
        ///
        /// **Authorization:** Requires authenticated user with AR Reports read permission
        /// </remarks>
        /// <param name="asOfDate">Optional point-in-time date for the aging calculation; defaults to today if not provided.</param>
        /// <returns>A <see cref="DetailedAgingReportDto"/> containing per-customer, per-invoice aging details.</returns>
        /// <response code="200">Successfully generated the detailed aging report.</response>
        /// <response code="400">Invalid as-of date parameter.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during report generation.</response>
        [HttpGet("aging/detailed")]
        public async Task<ActionResult<DetailedAgingReportDto>> GetDetailedAgingReport([FromQuery] DateTime? asOfDate = null)
            => Ok(await _reportsService.GetDetailedAgingReportAsync(asOfDate));

        /// <summary>
        /// Generates a customer statement showing all invoices, payments, and running balance for a date range.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Producing monthly or quarterly statements to send to customers for balance confirmation
        /// - Supporting dispute resolution by showing a complete transaction history for a customer
        /// - Providing customer account managers with a reconciliation tool during account reviews
        ///
        /// **Integration Pattern:**
        /// - Combines invoice and payment data into a chronological ledger-style statement
        /// - The statement includes opening balance, each transaction, and closing balance
        /// - Output is structured for direct rendering as a printable PDF or on-screen statement view
        ///
        /// **Business Rules:**
        /// - Both <paramref name="fromDate"/> and <paramref name="toDate"/> are required to define the statement period
        /// - The opening balance is calculated from all transactions prior to the from-date
        /// - Voided invoices and bounced payments are included with their void/bounce indicators for transparency
        /// - All amounts are in the customer's trading currency
        ///
        /// **Authorization:** Requires authenticated user with AR Reports read permission
        /// </remarks>
        /// <param name="customerId">The unique identifier (GUID) of the customer for the statement.</param>
        /// <param name="fromDate">The start date of the statement period (inclusive).</param>
        /// <param name="toDate">The end date of the statement period (inclusive).</param>
        /// <returns>A <see cref="CustomerStatementDto"/> containing the chronological transaction list and balances.</returns>
        /// <response code="200">Successfully generated the customer statement.</response>
        /// <response code="400">Invalid date range (e.g., from-date after to-date).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="404">No customer exists with the specified identifier.</response>
        /// <response code="500">Internal server error during statement generation.</response>
        [HttpGet("customer-statement/{customerId}")]
        public async Task<ActionResult<CustomerStatementDto>> GetCustomerStatement(
            Guid customerId,
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
            => Ok(await _reportsService.GetCustomerStatementAsync(customerId, fromDate, toDate));

        /// <summary>
        /// Generates a detailed customer ledger for one or more customer business partners.
        /// </summary>
        [HttpGet("customer-detailed-ledger")]
        public async Task<ActionResult<CustomerDetailedLedgerReportDto>> GetCustomerDetailedLedger(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] List<Guid>? customerIds = null,
            [FromQuery] bool showCustomerCurrency = false)
        {
            try
            {
                return Ok(await _reportsService.GetCustomerDetailedLedgerAsync(fromDate, toDate, customerIds, showCustomerCurrency));
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the collections dashboard with real-time KPIs and overdue receivable metrics.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Displaying a real-time collections overview on the AR module home page
        /// - Monitoring key collection KPIs such as DSO (Days Sales Outstanding) and overdue percentages
        /// - Alerting collections managers to customers requiring immediate follow-up
        ///
        /// **Integration Pattern:**
        /// - Aggregates data from invoices, payments, and customer records into a single dashboard payload
        /// - Includes top overdue customers, recent payment activity, and aging distribution summary
        /// - Designed for polling or periodic refresh to keep the dashboard current
        ///
        /// **Business Rules:**
        /// - All metrics are calculated in real-time against current AR data
        /// - DSO is calculated using the standard formula: (AR Balance / Total Credit Sales) x Number of Days
        /// - Overdue thresholds are configurable in system settings
        /// - Only active customers with outstanding balances appear in the top overdue list
        ///
        /// **Authorization:** Requires authenticated user with AR Reports read permission
        /// </remarks>
        /// <returns>A <see cref="CollectionsDashboardDto"/> containing KPIs, top overdue customers, and aging distribution.</returns>
        /// <response code="200">Successfully generated the collections dashboard data.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during dashboard generation.</response>
        [HttpGet("collections-dashboard")]
        public async Task<ActionResult<CollectionsDashboardDto>> GetCollectionsDashboard()
            => Ok(await _reportsService.GetCollectionsDashboardAsync());

        /// <summary>
        /// Retrieves a high-level summary of the current Accounts Receivable position.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Providing a snapshot of total AR for the finance dashboard or executive summary
        /// - Displaying key AR metrics such as total outstanding, total overdue, and collection rate
        /// - Supporting month-end reporting with a reconcilable AR balance summary
        ///
        /// **Integration Pattern:**
        /// - Aggregates all outstanding invoices and unallocated payments into summary totals
        /// - Can be combined with AP summary for a net receivables/payables position
        /// - Feeds into the main finance dashboard alongside GL and cash position widgets
        ///
        /// **Business Rules:**
        /// - Total outstanding is the sum of all sent invoice balances minus unallocated credit notes
        /// - Total overdue is the subset of outstanding where the due date has passed
        /// - Collection rate is calculated as payments received in the current period divided by opening AR balance
        /// - Voided invoices and bounced payments are excluded from all calculations
        ///
        /// **Authorization:** Requires authenticated user with AR Reports read permission
        /// </remarks>
        /// <returns>An <see cref="ArSummaryDto"/> containing total outstanding, overdue, and collection metrics.</returns>
        /// <response code="200">Successfully generated the AR summary.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during summary generation.</response>
        [HttpGet("ar-summary")]
        public async Task<ActionResult<ArSummaryDto>> GetArSummary()
            => Ok(await _reportsService.GetArSummaryAsync());

        /// <summary>
        /// Generates a sales summary report with revenue breakdown by configurable dimensions.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Analyzing sales performance by customer, product, salesperson, or region over a period
        /// - Comparing sales volumes across periods for trend identification
        /// - Supporting sales commission calculations and territory performance reviews
        ///
        /// **Integration Pattern:**
        /// - Aggregates invoice line-item data according to the grouping dimensions specified in the query
        /// - Output is structured for rendering as tables, bar charts, or pivot grids
        /// - Can be filtered by date range, customer group, product category, or salesperson
        ///
        /// **Business Rules:**
        /// - Only sent (non-voided) invoices are included in sales calculations
        /// - Revenue is recognized based on the invoice date, not the payment date
        /// - Credit notes reduce the sales totals for the period in which they were issued
        /// - Grouping dimensions and date ranges are specified via the query parameters
        ///
        /// **Authorization:** Requires authenticated user with AR Reports read permission
        /// </remarks>
        /// <param name="query">Query parameters specifying date range, grouping dimensions, and filters for the sales summary.</param>
        /// <returns>A list of <see cref="SalesSummaryDto"/> records, each representing a summary row for the specified grouping.</returns>
        /// <response code="200">Successfully generated the sales summary report.</response>
        /// <response code="400">Invalid query parameters (e.g., invalid date range or unsupported grouping dimension).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during report generation.</response>
        [HttpGet("sales-summary")]
        public async Task<ActionResult<List<SalesSummaryDto>>> GetSalesSummary([FromQuery] SalesSummaryQueryDto query)
            => Ok(await _reportsService.GetSalesSummaryAsync(query));

        /// <summary>
        /// Analyzes payment collection trends over a date range grouped by a configurable time interval.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Visualizing payment collection trends on a line or bar chart (monthly, weekly, or daily)
        /// - Identifying seasonal patterns in customer payment behavior
        /// - Comparing actual collections against forecasted or budgeted targets
        ///
        /// **Integration Pattern:**
        /// - Aggregates payment amounts by the specified time interval (Day, Week, Month, Quarter, Year)
        /// - Output is optimized for time-series chart rendering on the frontend
        /// - Can be overlaid with invoice issuance trends for a complete AR flow visualization
        ///
        /// **Business Rules:**
        /// - Only cleared (non-bounced) payments are included in trend calculations
        /// - The groupBy parameter accepts: "Day", "Week", "Month", "Quarter", or "Year"
        /// - Date range is mandatory; both <paramref name="fromDate"/> and <paramref name="toDate"/> are required
        /// - Credit notes are reported separately or netted depending on system configuration
        /// - Bounced payments are excluded to reflect actual cash collected
        ///
        /// **Authorization:** Requires authenticated user with AR Reports read permission
        /// </remarks>
        /// <param name="fromDate">The start date of the trend analysis period (inclusive).</param>
        /// <param name="toDate">The end date of the trend analysis period (inclusive).</param>
        /// <param name="groupBy">The time interval for grouping results; defaults to "Month". Accepted values: Day, Week, Month, Quarter, Year.</param>
        /// <returns>A list of <see cref="PaymentTrendDto"/> records, each representing a time-interval bucket with aggregated payment totals.</returns>
        /// <response code="200">Successfully generated the payment trends report.</response>
        /// <response code="400">Invalid parameters (e.g., from-date after to-date or unsupported groupBy value).</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="500">Internal server error during trend analysis.</response>
        [HttpGet("payment-trends")]
        public async Task<ActionResult<List<PaymentTrendDto>>> GetPaymentTrends(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string groupBy = "Month")
            => Ok(await _reportsService.GetPaymentTrendsAsync(fromDate, toDate, groupBy));
    }
}
