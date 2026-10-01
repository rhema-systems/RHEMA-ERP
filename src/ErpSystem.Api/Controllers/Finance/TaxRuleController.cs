using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Shared.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages the full lifecycle of tax rules within the finance taxation module.
    /// </summary>
    /// <remarks>
    /// **Domain Responsibility:**
    /// - Tax rules define the conditions under which specific tax groups (and their component tax rates) apply
    ///   to financial transactions such as invoices, purchase orders, and service billings
    /// - Each rule binds a <see cref="TaxGroup"/> to supported matching criteria: transaction type and
    ///   the canonical customer classification stored on the Business Partner
    /// - Rules are evaluated by priority order during tax calculation to determine which taxes apply
    ///
    /// **Tax Rule Lifecycle:**
    /// 1. **Seed** -- Populate default tax rules from the <see cref="ITaxConfigurationService"/> (typically Ghana-specific defaults)
    /// 2. **Create** -- Define new rules mapping tax groups to transaction conditions
    /// 3. **Read** -- List all rules (ordered by priority) or retrieve a single rule by ID
    /// 4. **Update** -- Modify rule criteria, priority, or active status
    /// 5. **Deactivate** -- Soft-delete a rule that is no longer applicable while retaining audit history
    ///
    /// **Integration Pattern:**
    /// - Tax rules reference <see cref="TaxGroup"/> entities; each TaxGroup contains one or more TaxRates
    /// - The tax calculation engine evaluates rules in priority order against transaction attributes
    ///   to resolve the applicable tax group and its component rates
    /// - Consumed by Invoicing, Accounts Payable, and Point-of-Sale modules during line-item tax computation
    /// - Seeding is delegated to <see cref="ITaxConfigurationService"/> for centralised default configuration
    ///
    /// **Authorization:** All endpoints require authentication (controller-level [Authorize])
    /// </remarks>
    [ApiController]
    [Route("api/finance/tax/rules")]
    [Authorize]
    public class TaxRuleController : ControllerBase
    {
        private static readonly HashSet<string> CanonicalCustomerTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Retail",
            "Wholesale",
            "Corporate",
            "Government",
            "Non-Profit"
        };
        private readonly ApplicationDbContext _context;
        private readonly ITaxConfigurationService _taxService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IFinanceAuditService _financeAuditService;

        /// <summary>
        /// Initializes a new instance of <see cref="TaxRuleController"/> with required dependencies.
        /// </summary>
        /// <param name="context">The application database context for tax rule entity operations.</param>
        /// <param name="taxService">The tax configuration service used for seeding default tax rules.</param>
        public TaxRuleController(
            ApplicationDbContext context,
            ITaxConfigurationService taxService,
            ICurrentUserService currentUserService,
            IFinanceAuditService financeAuditService)
        {
            _context = context;
            _taxService = taxService;
            _currentUserService = currentUserService;
            _financeAuditService = financeAuditService;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        /// <summary>
        /// Seeds the system with default tax rules using the tax configuration service.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Initial system setup to populate standard tax rules (e.g., Ghana VAT, NHIL, GETFund, WHT)
        /// - Resetting tax rules to factory defaults after configuration changes
        ///
        /// **Integration Pattern:**
        /// - Delegates to <see cref="ITaxConfigurationService.SeedTaxRulesAsync"/> which creates
        ///   default TaxGroup-to-condition mappings based on jurisdiction-specific tax legislation
        /// - Seeded rules become immediately available for tax calculation on new transactions
        ///
        /// **Business Rules:**
        /// - Idempotency depends on the implementation of <see cref="ITaxConfigurationService"/>
        /// - Typically safe to call multiple times; duplicates are handled by the service layer
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>A JSON object with a confirmation message indicating successful seeding.</returns>
        /// <response code="200">Tax rules seeded successfully</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error during seed operation</response>
        [HttpPost("seed")]
        public async Task<IActionResult> SeedTaxRules()
        {
            await _taxService.SeedTaxRulesAsync();
            return Ok(new { message = "Tax rules seeded successfully" });
        }

        /// <summary>
        /// Retrieves all tax rules ordered by priority, including their associated tax group names.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating the tax rules management grid in the administration UI
        /// - Reviewing the complete rule evaluation order for tax calculation debugging
        /// - Exporting the current tax rule configuration for audit or compliance review
        ///
        /// **Integration Pattern:**
        /// - Eagerly loads the related <see cref="TaxGroup"/> to resolve TaxGroupName
        /// - Returns rules sorted by Priority (ascending), reflecting the order in which the
        ///   tax calculation engine evaluates them
        ///
        /// **Business Rules:**
        /// - Returns all rules regardless of IsActive status; filtering is left to the client
        /// - ProductCategoryName is omitted until an authoritative category adapter is available
        /// - Priority ordering is critical: lower values are evaluated first during tax resolution
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>A list of <see cref="TaxRuleDto"/> objects representing all tax rules in priority order.</returns>
        /// <response code="200">Returns the list of all tax rules</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaxRuleDto>>> GetTaxRules()
        {
            var tenantId = TenantId;
            var rules = await _context.TaxRules
                .Include(r => r.TaxGroup)
                .Where(r => r.TenantId == tenantId && !r.IsDeleted)
                .OrderBy(r => r.Priority)
                .Select(r => new TaxRuleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Priority = r.Priority,
                    TaxGroupId = r.TaxGroupId,
                    TaxGroupName = r.TaxGroup != null ? r.TaxGroup.Name : string.Empty,
                    TransactionType = r.TransactionType,
                    ProductCategoryId = r.ProductCategoryId,
                    ProductCategoryName = null,
                    CustomerType = r.CustomerType,
                    ServiceType = r.ServiceType,
                    IsActive = r.IsActive
                })
                .ToListAsync();

            return Ok(rules);
        }

        /// <summary>
        /// Retrieves a single tax rule by its unique identifier, including the associated tax group.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading a specific tax rule for editing in the administration UI
        /// - Inspecting rule details during tax calculation troubleshooting
        /// - Viewing the full configuration of a rule referenced in audit logs
        ///
        /// **Integration Pattern:**
        /// - Eagerly loads the related <see cref="TaxGroup"/> to resolve TaxGroupName
        /// - Returns the complete rule definition including all matching criteria fields
        ///
        /// **Business Rules:**
        /// - Returns the rule regardless of its IsActive status
        /// - If no rule exists with the given ID, returns 404 Not Found
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax rule to retrieve.</param>
        /// <returns>A <see cref="TaxRuleDto"/> containing the full tax rule details.</returns>
        /// <response code="200">Returns the requested tax rule</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="404">No tax rule found with the specified ID</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<TaxRuleDto>> GetTaxRule(Guid id)
        {
            var tenantId = TenantId;
            var r = await _context.TaxRules
                .Include(r => r.TaxGroup)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted);

            if (r == null) return NotFound();

            return Ok(new TaxRuleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Priority = r.Priority,
                TaxGroupId = r.TaxGroupId,
                TaxGroupName = r.TaxGroup?.Name ?? string.Empty,
                TransactionType = r.TransactionType,
                ProductCategoryId = r.ProductCategoryId,
                CustomerType = r.CustomerType,
                ServiceType = r.ServiceType,
                IsActive = r.IsActive
            });
        }

        /// <summary>
        /// Creates a new tax rule that maps a tax group to a set of transaction matching criteria.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Defining a new tax applicability rule when tax legislation changes
        /// - Adding product-category-specific or customer-type-specific tax rules
        /// - Configuring rules for new transaction types (e.g., digital services, exports)
        ///
        /// **Integration Pattern:**
        /// - The created rule is immediately available for tax calculation on subsequent transactions
        /// - References an existing <see cref="TaxGroup"/> by TaxGroupId; the tax group must exist
        /// - Returns a 201 Created response with a Location header pointing to the new rule's GET endpoint
        ///
        /// **Business Rules:**
        /// - A new GUID is generated server-side for the rule ID
        /// - TenantId is currently hardcoded to a development default; production implementations
        ///   should resolve this from the authenticated user's tenant context
        /// - Priority determines evaluation order: assign carefully to avoid unintended rule shadowing
        /// - CreatedAt is set to the current UTC timestamp
        /// - The TaxGroupId must reference a valid, existing TaxGroup entity
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="dto">The <see cref="CreateTaxRuleDto"/> containing the new rule's configuration.</param>
        /// <returns>A <see cref="TaxRuleDto"/> containing the created rule's ID and Name.</returns>
        /// <response code="201">Tax rule created successfully; Location header contains the resource URI</response>
        /// <response code="400">Validation failure (e.g., missing required fields, invalid TaxGroupId)</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="500">Internal server error (e.g., foreign key constraint violation)</response>
        [HttpPost]
        public Task<ActionResult<TaxRuleDto>> CreateTaxRule(CreateTaxRuleDto dto) =>
            ExecuteRuleMutationAsync(() => CreateTaxRuleCoreAsync(dto));

        private async Task<ActionResult<TaxRuleDto>> CreateTaxRuleCoreAsync(CreateTaxRuleDto dto)
        {
            var tenantId = TenantId;
            var validationError = NormalizeAndValidate(dto);
            if (validationError != null) return BadRequest(validationError);
            var taxGroupExists = await _context.TaxGroups
                .AnyAsync(g => g.TenantId == tenantId && g.Id == dto.TaxGroupId && g.IsActive && !g.IsDeleted);
            if (!taxGroupExists)
            {
                return BadRequest("An active tax group was not found for this tenant.");
            }
            if (await HasConflictingActiveRuleAsync(dto, null))
                return Conflict("An active tax rule already uses the same matching criteria. Deactivate or amend that rule before creating another.");

            var rule = new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                Priority = dto.Priority,
                TaxGroupId = dto.TaxGroupId,
                TransactionType = dto.TransactionType,
                ProductCategoryId = dto.ProductCategoryId,
                CustomerType = dto.CustomerType,
                ServiceType = dto.ServiceType,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                TenantId = tenantId
            };

            _context.TaxRules.Add(rule);
            await _context.SaveChangesAsync();
            await RecordRuleAuditAsync(FinanceAuditEvents.TaxRuleCreated, rule, null, RuleSnapshot(rule));

            return CreatedAtAction(nameof(GetTaxRule), new { id = rule.Id }, new TaxRuleDto { Id = rule.Id, Name = rule.Name });
        }

        /// <summary>
        /// Updates an existing tax rule's configuration, including its matching criteria, priority, and active status.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Adjusting rule priority to change the tax calculation evaluation order
        /// - Reassigning a rule to a different tax group after legislative changes
        /// - Deactivating a rule (setting IsActive to false) without deleting it for audit trail preservation
        /// - Updating matching criteria (transaction type, customer type, service type, product category)
        ///
        /// **Integration Pattern:**
        /// - Changes take effect immediately for all subsequent tax calculations
        /// - The UpdatedAt timestamp is set to the current UTC time to support audit tracking
        /// - Does not return the updated entity; clients should re-fetch via GET if needed
        ///
        /// **Business Rules:**
        /// - All mutable fields are overwritten from the DTO; partial updates are not supported
        /// - The rule must exist; attempting to update a non-existent rule returns 404
        /// - Changing Priority may alter which rule matches first during tax resolution -- exercise caution
        /// - TaxGroupId must reference a valid, existing TaxGroup entity
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax rule to update.</param>
        /// <param name="dto">The <see cref="UpdateTaxRuleDto"/> containing the updated rule configuration.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Tax rule updated successfully</response>
        /// <response code="400">Validation failure (e.g., invalid TaxGroupId or missing required fields)</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="404">No tax rule found with the specified ID</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{id}")]
        public Task<IActionResult> UpdateTaxRule(Guid id, UpdateTaxRuleDto dto) =>
            ExecuteRuleMutationAsync(() => UpdateTaxRuleCoreAsync(id, dto));

        private async Task<IActionResult> UpdateTaxRuleCoreAsync(Guid id, UpdateTaxRuleDto dto)
        {
            var tenantId = TenantId;
            var validationError = NormalizeAndValidate(dto);
            if (validationError != null) return BadRequest(validationError);
            var rule = await _context.TaxRules
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted);
            if (rule == null) return NotFound();

            var taxGroupExists = await _context.TaxGroups
                .AnyAsync(g => g.TenantId == tenantId && g.Id == dto.TaxGroupId && g.IsActive && !g.IsDeleted);
            if (!taxGroupExists)
            {
                return BadRequest("An active tax group was not found for this tenant.");
            }
            if (await HasConflictingActiveRuleAsync(dto, id))
                return Conflict("An active tax rule already uses the same matching criteria. Deactivate or amend that rule before activating this rule.");

            var before = RuleSnapshot(rule);

            rule.Name = dto.Name;
            rule.Description = dto.Description;
            rule.Priority = dto.Priority;
            rule.TaxGroupId = dto.TaxGroupId;
            rule.TransactionType = dto.TransactionType;
            rule.ProductCategoryId = dto.ProductCategoryId;
            rule.CustomerType = dto.CustomerType;
            rule.ServiceType = dto.ServiceType;
            rule.IsActive = dto.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;

            await _context.SaveChangesAsync();
            await RecordRuleAuditAsync(FinanceAuditEvents.TaxRuleUpdated, rule, before, RuleSnapshot(rule));

            return NoContent();
        }

        /// <summary>
        /// Deactivates and soft-deletes a tax rule by its unique identifier.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing obsolete tax rules that no longer apply under current legislation
        /// - Cleaning up incorrectly configured rules during initial setup
        /// - Deleting test rules after development or QA validation
        ///
        /// **Integration Pattern:**
        /// - Retains the rule as soft-deleted audit evidence
        /// - The rule is immediately excluded from future tax calculations
        /// - Historical transactions that were processed under this rule are not affected;
        ///   their tax amounts remain as originally calculated
        ///
        /// **Business Rules:**
        /// - The rule must exist; attempting to delete a non-existent rule returns 404
        /// - The rule is deactivated and soft-deleted; historical configuration evidence remains available
        /// - No cascading effects on existing transactions or journal entries
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax rule to delete.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Tax rule deleted successfully</response>
        /// <response code="401">Not authenticated</response>
        /// <response code="404">No tax rule found with the specified ID</response>
        /// <response code="500">Internal server error (e.g., foreign key constraint if rule is referenced elsewhere)</response>
        [HttpDelete("{id}")]
        public Task<IActionResult> DeleteTaxRule(Guid id) =>
            ExecuteRuleMutationAsync(() => DeleteTaxRuleCoreAsync(id));

        private async Task<IActionResult> DeleteTaxRuleCoreAsync(Guid id)
        {
            var tenantId = TenantId;
            var rule = await _context.TaxRules
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted);
            if (rule == null) return NotFound();

            var before = RuleSnapshot(rule);
            rule.IsActive = false;
            rule.IsDeleted = true;
            rule.DeletedAt = DateTime.UtcNow;
            rule.DeletedBy = UserName;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;
            await _context.SaveChangesAsync();
            await RecordRuleAuditAsync(FinanceAuditEvents.TaxRuleDeactivated, rule, before, RuleSnapshot(rule));

            return NoContent();
        }

        private async Task<bool> HasConflictingActiveRuleAsync(CreateTaxRuleDto dto, Guid? excludeId)
        {
            if (!dto.IsActive)
                return false;

            return await _context.TaxRules.AnyAsync(rule =>
                rule.TenantId == TenantId
                && !rule.IsDeleted
                && rule.IsActive
                && (!excludeId.HasValue || rule.Id != excludeId.Value)
                && rule.TransactionType == dto.TransactionType
                && rule.ProductCategoryId == dto.ProductCategoryId
                && rule.CustomerType == dto.CustomerType
                && rule.ServiceType == dto.ServiceType);
        }

        private static string? NormalizeAndValidate(CreateTaxRuleDto dto)
        {
            dto.Name = dto.Name?.Trim() ?? string.Empty;
            dto.Description = Normalize(dto.Description);
            dto.TransactionType = Normalize(dto.TransactionType);
            dto.CustomerType = Normalize(dto.CustomerType);
            dto.ServiceType = Normalize(dto.ServiceType);
            if (dto.Name.Length == 0)
                return "Tax rule name is required.";
            if (dto.Priority < 0)
                return "Tax rule priority cannot be negative.";
            if (dto.TaxGroupId == Guid.Empty)
                return "Tax group is required.";
            if (dto.ProductCategoryId.HasValue || dto.ServiceType != null)
                return "Product category and service type conditions are not supported by the tax calculation request. Use transaction type and customer type only.";
            if (dto.CustomerType != null && !CanonicalCustomerTypes.Contains(dto.CustomerType))
                return $"Customer type must be one of: {string.Join(", ", CanonicalCustomerTypes)}.";
            return null;
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static object RuleSnapshot(TaxRule rule) => new
        {
            rule.Id,
            rule.Name,
            rule.Description,
            rule.Priority,
            rule.TaxGroupId,
            rule.TransactionType,
            rule.ProductCategoryId,
            rule.CustomerType,
            rule.ServiceType,
            rule.IsActive,
            rule.IsDeleted
        };

        private Task RecordRuleAuditAsync(string eventType, TaxRule rule, object? before, object? after) =>
            _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = TenantId,
                SourceModule = "Tax",
                SourceDocumentType = "TaxRule",
                SourceDocumentId = rule.Id,
                Resource = "Finance.TaxRule",
                ResourceId = rule.Id.ToString(),
                BeforeValues = before,
                AfterValues = after
            });

        private async Task<T> ExecuteRuleMutationAsync<T>(Func<Task<T>> action)
        {
            if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
                return await action();

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var result = await action();
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
    }
}
