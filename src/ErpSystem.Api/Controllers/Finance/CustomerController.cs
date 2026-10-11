using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages Accounts Receivable (AR) customer master data, balances, credit checks, and transaction history.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Maintain the customer master file used across Sales, Invoicing, and Collections
    /// - Query outstanding balances and receivable aging for credit management
    /// - Perform real-time credit limit checks before order acceptance
    /// - Retrieve a customer's invoice and payment history for account reconciliation
    ///
    /// **Integration Pattern:**
    /// - Referenced by Sales Order, Invoicing, and Payment Receipt modules as the authoritative customer record
    /// - Balance and aging data feeds into the General Ledger AR control account and financial reporting
    /// - Credit check results are consumed by the Sales Order workflow to approve or hold orders
    ///
    /// **Business Rules:**
    /// - Customer codes must be unique across the organization
    /// - Customers with open balances or unposted transactions cannot be hard-deleted
    /// - Credit limit enforcement is evaluated against the sum of outstanding invoices plus the requested amount
    ///
    /// **Authorization:** All endpoints require an authenticated user (Authorize attribute)
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/ar/customers")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        /// <summary>
        /// Retrieves a paginated and filterable list of AR customers for master data browsing and lookup.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populate customer search grids in Sales Order entry and Invoice creation screens
        /// - Export filtered customer lists for marketing campaigns or credit review
        /// - Support type-ahead / autocomplete lookups by name, code, or classification
        ///
        /// **Integration Pattern:**
        /// - Called by any module that requires a customer picker or customer list view
        /// - Supports server-side paging to handle large customer bases efficiently
        ///
        /// **Business Rules:**
        /// - Soft-deleted customers are excluded from results by default
        /// - Results respect the caller's data-access scope (e.g., branch or company filter)
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="query">Filter, sort, and pagination parameters including search text, status, classification, page number, and page size.</param>
        /// <returns>A paged result set containing matching <see cref="CustomerDto"/> records and total count metadata.</returns>
        /// <response code="200">Returns the paginated list of customers matching the query criteria</response>
        /// <response code="400">Invalid query parameters (e.g., negative page number)</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="500">Internal server error during data retrieval</response>
        [HttpGet]
        public async Task<ActionResult<PagedResult<CustomerDto>>> GetAll([FromQuery] CustomerQueryDto query)
        {
            var result = await _customerService.GetAllAsync(query);
            return Ok(result);
        }

        /// <summary>
        /// Retrieves a single AR customer record by its unique identifier for detail view or editing.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Load full customer details when opening a customer profile or edit form
        /// - Fetch customer data for pre-filling Sales Order or Invoice header fields
        /// - Retrieve customer information for statement generation or correspondence
        ///
        /// **Integration Pattern:**
        /// - Used by Sales Order, Invoice, and Payment Receipt screens to resolve customer details from an ID reference
        /// - Downstream modules cache this data locally for the duration of a transaction entry session
        ///
        /// **Business Rules:**
        /// - Returns the complete customer record including addresses, contacts, and payment terms
        /// - Soft-deleted customers are still retrievable by ID for historical reference
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer to retrieve.</param>
        /// <returns>The full <see cref="CustomerDto"/> for the requested customer.</returns>
        /// <response code="200">Returns the customer record</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified ID</response>
        /// <response code="500">Internal server error during data retrieval</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerDto>> GetById(Guid id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
                return NotFound(new { error = $"Customer with ID {id} not found" });

            return Ok(customer);
        }

        /// <summary>
        /// Retrieves a single AR customer record by its human-readable business code.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Look up a customer when the user enters or scans a customer code (e.g., "CUST-00123")
        /// - Resolve customer references from imported files, EDI documents, or external system integrations
        /// - Quick lookup from printed invoices or statements that display the customer code
        ///
        /// **Integration Pattern:**
        /// - Serves as the code-based alternative to the ID-based lookup for user-facing workflows
        /// - External integrations typically reference customers by code rather than internal GUID
        ///
        /// **Business Rules:**
        /// - Customer codes are unique; exactly zero or one result is returned
        /// - Code matching is typically case-insensitive
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="code">The unique business code assigned to the customer (e.g., "CUST-00123").</param>
        /// <returns>The full <see cref="CustomerDto"/> for the requested customer.</returns>
        /// <response code="200">Returns the customer record matching the code</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified code</response>
        /// <response code="500">Internal server error during data retrieval</response>
        [HttpGet("by-code/{code}")]
        public async Task<ActionResult<CustomerDto>> GetByCode(string code)
        {
            var customer = await _customerService.GetByCodeAsync(code);
            if (customer == null)
                return NotFound(new { error = $"Customer with code '{code}' not found" });

            return Ok(customer);
        }

        /// <summary>
        /// Creates a new AR customer master record for use across the Accounts Receivable module.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Onboard a new customer during first-time sales order entry
        /// - Bulk-create customers via an import routine that calls this endpoint per record
        /// - Register a new debtor entity as part of the credit application approval workflow
        ///
        /// **Integration Pattern:**
        /// - The newly created customer becomes immediately available for Sales Order, Invoice, and Payment Receipt entry
        /// - An AR control account sub-ledger entry may be initialized upon creation depending on configuration
        /// - Returns a Location header pointing to the new resource for REST compliance
        ///
        /// **Business Rules:**
        /// - Customer code must be unique; duplicate codes are rejected
        /// - Required fields include customer name, currency, and payment terms
        /// - Default credit limit is applied if not explicitly specified
        /// - The customer is created in Active status unless otherwise indicated
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="dto">The customer creation payload containing name, code, addresses, contacts, payment terms, credit limit, and classification.</param>
        /// <returns>The newly created <see cref="CustomerDto"/> with its assigned ID and server-generated fields.</returns>
        /// <response code="201">Customer created successfully; returns the new record with a Location header</response>
        /// <response code="400">Validation failure (e.g., duplicate code, missing required fields)</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="500">Internal server error during creation</response>

        /// <summary>
        /// Updates an existing AR customer master record with revised information.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Correct or update customer contact details, addresses, or payment terms
        /// - Adjust credit limits following a periodic credit review
        /// - Change customer classification, tax registration, or currency settings
        ///
        /// **Integration Pattern:**
        /// - Changes to payment terms or credit limits take effect on subsequent transactions
        /// - Existing open invoices and orders retain the terms that were in effect at the time of creation
        /// - Audit trail captures the before and after state of modified fields
        ///
        /// **Business Rules:**
        /// - The ID in the URL path must match the ID in the request body; mismatches are rejected
        /// - Customer code changes may be restricted if the customer has posted transactions
        /// - Reducing the credit limit below the current outstanding balance triggers a warning but is permitted
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer to update, specified in the URL path.</param>
        /// <param name="dto">The customer update payload containing the modified fields and the matching customer ID.</param>
        /// <returns>The updated <see cref="CustomerDto"/> reflecting all applied changes.</returns>
        /// <response code="200">Customer updated successfully; returns the updated record</response>
        /// <response code="400">Validation failure or ID mismatch between URL and request body</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified ID</response>
        /// <response code="500">Internal server error during update</response>

        /// <summary>
        /// Soft-deletes an AR customer record, marking it as inactive while preserving historical data.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Deactivate a customer that is no longer doing business with the organization
        /// - Remove a duplicate customer record that was created in error
        /// - Archive a customer as part of a data cleanup or consolidation exercise
        ///
        /// **Integration Pattern:**
        /// - Soft-deleted customers are excluded from active customer lists and picker controls
        /// - Historical transactions (invoices, payments) referencing this customer remain intact and queryable
        /// - The customer can be restored by an administrator if needed
        ///
        /// **Business Rules:**
        /// - Customers with open (unpaid) invoices or unposted transactions cannot be deleted
        /// - Deletion is a soft operation; the record is flagged as deleted rather than physically removed
        /// - Associated addresses and contacts are also flagged as inactive
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer to soft-delete.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Customer soft-deleted successfully</response>
        /// <response code="400">Customer cannot be deleted due to open balances or business rule violation</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified ID</response>
        /// <response code="500">Internal server error during deletion</response>

        /// <summary>
        /// Retrieves the current outstanding balance and receivable aging breakdown for a specific customer.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Display account summary on the customer profile or dashboard
        /// - Feed aging data into the Collections module for follow-up prioritization
        /// - Provide balance information for customer statement generation
        ///
        /// **Integration Pattern:**
        /// - Aging buckets (Current, 30, 60, 90, 120+ days) align with the organization's AR aging policy
        /// - Balance data is derived from posted invoices minus applied payments and credit notes
        /// - Used by the credit check endpoint as the baseline for available credit calculation
        ///
        /// **Business Rules:**
        /// - Balance is calculated in the customer's default transaction currency
        /// - Only posted (finalized) invoices and applied payments are included in the balance
        /// - Aging is computed based on invoice due date relative to the current system date
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer whose balance and aging are requested.</param>
        /// <returns>A <see cref="CustomerBalanceDto"/> containing the total outstanding balance and aging bucket breakdown.</returns>
        /// <response code="200">Returns the customer balance and aging summary</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified ID</response>
        /// <response code="500">Internal server error during balance calculation</response>
        [HttpGet("{id}/balance")]
        public async Task<ActionResult<CustomerBalanceDto>> GetBalance(Guid id)
        {
            try
            {
                var balance = await _customerService.GetBalanceAsync(id);
                return Ok(balance);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Performs a real-time credit limit check for a customer against a proposed transaction amount.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Validate credit availability before confirming a new Sales Order
        /// - Pre-check credit during quotation-to-order conversion to avoid order holds
        /// - Evaluate whether a customer can place an additional order given their current exposure
        ///
        /// **Integration Pattern:**
        /// - Called by the Sales Order module before order confirmation to enforce credit policy
        /// - The result includes an approval flag, available credit, and current exposure details
        /// - Can trigger workflow notifications to credit managers when limits are exceeded
        ///
        /// **Business Rules:**
        /// - Available credit = Credit Limit - (Outstanding Balance + Pending Orders)
        /// - If the requested amount exceeds available credit, the result indicates a credit hold
        /// - Customers with a null or zero credit limit are treated as unlimited; only positive limits are enforced
        /// - Zero or negative amounts are rejected as invalid
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer to check credit for.</param>
        /// <param name="amount">The proposed transaction amount to validate against the customer's available credit.</param>
        /// <returns>A <see cref="CreditCheckResultDto"/> indicating approval status, available credit, and exposure breakdown.</returns>
        /// <response code="200">Returns the credit check result with approval status and available credit details</response>
        /// <response code="400">Invalid amount provided (e.g., zero or negative)</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified ID</response>
        /// <response code="500">Internal server error during credit evaluation</response>
        [HttpPost("{id}/check-credit")]
        public async Task<ActionResult<CreditCheckResultDto>> CheckCredit(Guid id, [FromBody] decimal amount)
        {
            try
            {
                var result = await _customerService.CheckCreditLimitAsync(id, amount);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the complete list of invoices for a specific customer for account review and reconciliation.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Display invoice history on the customer account detail screen
        /// - Generate a customer statement of account showing all invoiced amounts
        /// - Reconcile customer-reported discrepancies by reviewing the full invoice ledger
        ///
        /// **Integration Pattern:**
        /// - Returns invoices created through the Invoicing module that reference this customer
        /// - Each invoice includes status (Draft, Posted, Partially Paid, Paid, Void) for filtering
        /// - Used alongside the payments endpoint for full account reconciliation views
        ///
        /// **Business Rules:**
        /// - Includes all invoice statuses (draft, posted, paid, voided) for complete visibility
        /// - Invoices are returned in reverse chronological order (most recent first)
        /// - Voided invoices are included for audit trail completeness but clearly marked
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer whose invoices are requested.</param>
        /// <returns>A list of <see cref="InvoiceDto"/> records representing all invoices for the customer.</returns>
        /// <response code="200">Returns the list of customer invoices (empty list if none exist)</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified ID</response>
        /// <response code="500">Internal server error during invoice retrieval</response>
        [HttpGet("{id}/invoices")]
        public async Task<ActionResult<List<InvoiceDto>>> GetInvoices(Guid id)
        {
            var invoices = await _customerService.GetCustomerInvoicesAsync(id);
            return Ok(invoices);
        }

        /// <summary>
        /// Retrieves the complete list of payments received from a specific customer for account reconciliation.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Display payment history on the customer account detail screen
        /// - Reconcile bank deposits against recorded customer payments
        /// - Generate a customer statement showing all received payments and their application to invoices
        ///
        /// **Integration Pattern:**
        /// - Returns payment receipts processed through the AR Payment Receipt module for this customer
        /// - Each payment includes allocation details showing which invoices were settled
        /// - Used alongside the invoices endpoint for full account reconciliation views
        ///
        /// **Business Rules:**
        /// - Includes all payment statuses (pending, applied, reversed) for complete visibility
        /// - Payments are returned in reverse chronological order (most recent first)
        /// - Reversed or bounced payments are included for audit trail completeness
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the customer whose payments are requested.</param>
        /// <returns>A list of <see cref="CustomerPaymentDto"/> records representing all payments from the customer.</returns>
        /// <response code="200">Returns the list of customer payments (empty list if none exist)</response>
        /// <response code="401">Caller is not authenticated</response>
        /// <response code="404">No customer exists with the specified ID</response>
        /// <response code="500">Internal server error during payment retrieval</response>
        [HttpGet("{id}/payments")]
        public async Task<ActionResult<List<CustomerPaymentDto>>> GetPayments(Guid id)
        {
            var payments = await _customerService.GetCustomerPaymentsAsync(id);
            return Ok(payments);
        }
    }
}
