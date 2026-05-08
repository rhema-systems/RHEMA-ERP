using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Tax configuration and calculation API.
    /// </summary>
    /// <remarks>
    /// Simplified model with Taxes and Tax Groups.
    /// </remarks>
    [ApiController]
    [Route("api/finance/tax")]
    [Authorize]
    public class TaxConfigurationController : ControllerBase
    {
        private readonly ITaxConfigurationService _taxConfigService;
        private readonly ITaxCalculationEngine _taxCalculationEngine;

        public TaxConfigurationController(
            ITaxConfigurationService taxConfigService,
            ITaxCalculationEngine taxCalculationEngine)
        {
            _taxConfigService = taxConfigService;
            _taxCalculationEngine = taxCalculationEngine;
        }

        #region Taxes

        /// <summary>
        /// Retrieves all taxes.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading the complete tax catalog for administrative review
        /// - Populating dropdown lists or selection controls in the UI
        /// - Exporting all tax definitions for reporting or auditing purposes
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint on page load for tax management screens
        /// - Cache results client-side and refresh periodically or on mutation
        /// - For filtered results, prefer `GET /taxes/active` with the applicability parameter
        ///
        /// **Business Rules:**
        /// - Returns both active and inactive taxes
        /// - Results are scoped to the current tenant
        /// - Includes all tax categories: Standard, Levy, and Withholding
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of all taxes.</returns>
        /// <response code="200">Returns the list of taxes.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("taxes")]
        public async Task<ActionResult<IReadOnlyList<TaxDto>>> GetAllTaxes()
        {
            var taxes = await _taxConfigService.GetAllTaxesAsync();
            return Ok(taxes);
        }

        /// <summary>
        /// Retrieves active taxes, optionally filtered by applicability.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating tax selection dropdowns on invoice or purchase order forms
        /// - Filtering taxes applicable only to Sales, Purchases, or Both
        /// - Retrieving the current working set of taxes for transaction entry
        ///
        /// **Integration Pattern:**
        /// - Use `applicability=Sales` when creating sales invoices or AR transactions
        /// - Use `applicability=Purchases` when creating purchase orders or AP transactions
        /// - Omit the applicability parameter to retrieve all active taxes regardless of type
        ///
        /// **Business Rules:**
        /// - Only returns taxes where `IsActive` is true
        /// - Taxes with `Applicability = Both` are included in both Sales and Purchases filters
        /// - Results are scoped to the current tenant
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="applicability">Optional filter by tax applicability. Valid values: Sales (1), Purchases (2), Both (3). When omitted, all active taxes are returned.</param>
        /// <returns>List of active taxes matching the filter criteria.</returns>
        /// <response code="200">Returns the list of active taxes.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("taxes/active")]
        public async Task<ActionResult<IReadOnlyList<TaxDto>>> GetActiveTaxes([FromQuery] TaxApplicability? applicability = null)
        {
            var taxes = await _taxConfigService.GetActiveTaxesAsync(applicability);
            return Ok(taxes);
        }

        /// <summary>
        /// Retrieves a tax by ID.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading tax details for viewing or editing in an admin form
        /// - Fetching a specific tax definition after creation (via the CreatedAtAction location header)
        /// - Resolving tax details from a tax ID referenced in a transaction
        ///
        /// **Integration Pattern:**
        /// - Use the ID returned from `POST /taxes` or from list endpoints
        /// - Suitable for detail/edit views that need the full tax record
        /// - If you only have the tax code, use `GET /taxes/by-code/{code}` instead
        ///
        /// **Business Rules:**
        /// - Returns the tax regardless of its active/inactive status
        /// - Returns 404 if no tax exists with the given ID in the current tenant
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax.</param>
        /// <returns>The tax with the specified ID.</returns>
        /// <response code="200">Returns the tax.</response>
        /// <response code="404">Tax not found.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("taxes/{id}")]
        public async Task<ActionResult<TaxDto>> GetTaxById(Guid id)
        {
            var tax = await _taxConfigService.GetTaxByIdAsync(id);
            if (tax == null)
                return NotFound(new { message = "Tax not found" });

            return Ok(tax);
        }

        /// <summary>
        /// Retrieves a tax by code.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Looking up a tax by its human-readable code (e.g., "VAT", "NHIL", "GETFL")
        /// - Resolving tax references from imported data or external systems that use codes
        /// - Quick lookup when the tax code is known but the GUID is not
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint when integrating with external systems that reference taxes by code
        /// - Tax codes are unique within a tenant
        /// - For GUID-based lookups, use `GET /taxes/{id}` instead
        ///
        /// **Business Rules:**
        /// - Tax codes are case-sensitive
        /// - Returns the tax regardless of its active/inactive status
        /// - Returns 404 if no tax exists with the given code in the current tenant
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="code">The unique code of the tax (e.g., "VAT", "NHIL", "GETFL").</param>
        /// <returns>The tax with the specified code.</returns>
        /// <response code="200">Returns the tax.</response>
        /// <response code="404">Tax not found.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("taxes/by-code/{code}")]
        public async Task<ActionResult<TaxDto>> GetTaxByCode(string code)
        {
            var tax = await _taxConfigService.GetTaxByCodeAsync(code);
            if (tax == null)
                return NotFound(new { message = $"Tax '{code}' not found" });

            return Ok(tax);
        }

        /// <summary>
        /// Creates a new tax.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Setting up new tax types during system configuration (e.g., VAT, NHIL, WHT)
        /// - Adding new levies or withholding taxes introduced by regulatory changes
        /// - Configuring tax-to-GL-account mappings for payable and receivable accounts
        ///
        /// **Integration Pattern:**
        /// - After creation, the response includes a `Location` header pointing to `GET /taxes/{id}`
        /// - Returns HTTP 201 with the created tax DTO on success
        /// - Link the tax to GL accounts via `TaxPayableAccountId` and `TaxReceivableAccountId`
        ///
        /// **Business Rules:**
        /// - Tax code must be unique within the tenant
        /// - Rate is expressed as a percentage (e.g., 15 for 15%)
        /// - EffectiveFrom defaults to the current date if not specified
        /// - Applicability defaults to Both if not specified
        /// - Category defaults to Standard if not specified
        /// - Rate changes are tracked in the tax rate history
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="dto">The tax data to create, including code, name, rate, applicability, category, and optional GL account links.</param>
        /// <returns>The created tax.</returns>
        /// <response code="201">Tax created successfully.</response>
        /// <response code="400">Invalid request data (e.g., duplicate code, invalid rate).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("taxes")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<TaxDto>> CreateTax([FromBody] CreateTaxDto dto)
        {
            try
            {
                var tax = await _taxConfigService.CreateTaxAsync(dto);
                return CreatedAtAction(nameof(GetTaxById), new { id = tax.Id }, tax);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing tax.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Changing a tax rate when regulations are updated (e.g., VAT rate change)
        /// - Activating or deactivating a tax
        /// - Updating tax descriptions or GL account mappings
        /// - Modifying threshold amounts for withholding taxes
        ///
        /// **Integration Pattern:**
        /// - Only include fields that need updating; null fields are left unchanged
        /// - After updating, use `GET /taxes/{id}` to fetch the latest state
        /// - Rate changes automatically create a history record accessible via `GET /taxes/{id}/history`
        ///
        /// **Business Rules:**
        /// - Cannot change the tax code (immutable after creation)
        /// - Rate changes are recorded in the tax rate history for audit purposes
        /// - Deactivating a tax does not remove it from existing transactions
        /// - Threshold changes take effect immediately for new transactions
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax to update.</param>
        /// <param name="dto">The updated tax data. Only non-null fields will be applied.</param>
        /// <returns>The updated tax.</returns>
        /// <response code="200">Tax updated successfully.</response>
        /// <response code="400">Invalid request data or business rule violation.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPut("taxes/{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<TaxDto>> UpdateTax(Guid id, [FromBody] UpdateTaxDto dto)
        {
            try
            {
                var tax = await _taxConfigService.UpdateTaxAsync(id, dto);
                return Ok(tax);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a tax.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing a tax that was created in error
        /// - Cleaning up unused tax definitions during system configuration
        ///
        /// **Integration Pattern:**
        /// - This is a permanent deletion; consider deactivating via `PUT /taxes/{id}` instead
        /// - Returns a success message on completion
        /// - After deletion, the tax ID will no longer resolve via `GET /taxes/{id}`
        ///
        /// **Business Rules:**
        /// - Cannot delete a tax that is currently referenced by tax group components
        /// - Cannot delete a tax that has been used in posted transactions
        /// - Consider deactivating (`IsActive = false`) instead of deleting for audit trail preservation
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax to delete.</param>
        /// <returns>A success message confirming deletion.</returns>
        /// <response code="200">Tax deleted successfully.</response>
        /// <response code="400">Tax cannot be deleted (e.g., in use by tax groups or transactions).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpDelete("taxes/{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> DeleteTax(Guid id)
        {
            try
            {
                await _taxConfigService.DeleteTaxAsync(id);
                return Ok(new { message = "Tax deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves tax rate history for a specific tax.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Viewing the historical rate changes for a tax (e.g., when VAT rate changed from 12.5% to 15%)
        /// - Auditing tax rate modifications for compliance reporting
        /// - Determining which rate was in effect for a specific transaction date
        ///
        /// **Integration Pattern:**
        /// - Returns all historical rates ordered by effective date
        /// - Each history record includes the rate, effective-from date, effective-to date, and who made the change
        /// - Use this alongside `GET /taxes/{id}` to get both current and historical data
        ///
        /// **Business Rules:**
        /// - History records are created automatically when a tax rate is updated
        /// - The most recent record has a null `EffectiveTo` date, indicating the current rate
        /// - History is immutable and cannot be edited or deleted
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax to retrieve history for.</param>
        /// <returns>List of historical tax rates ordered by effective date.</returns>
        /// <response code="200">Returns the tax rate history.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("taxes/{id}/history")]
        public async Task<ActionResult<IReadOnlyList<TaxRateHistoryDto>>> GetTaxRateHistory(Guid id)
        {
            var history = await _taxConfigService.GetTaxRateHistoryAsync(id);
            return Ok(history);
        }

        #endregion

        #region Tax Groups

        /// <summary>
        /// Retrieves all tax groups.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading the complete list of tax groups for administrative review
        /// - Populating tax group selection controls in the UI
        /// - Reviewing which tax combinations are available in the system
        ///
        /// **Integration Pattern:**
        /// - Each tax group includes its component taxes with calculation order and compound basis
        /// - Cache results client-side and refresh on mutation
        /// - For filtered results, prefer `GET /groups/active` with the applicability parameter
        ///
        /// **Business Rules:**
        /// - Returns both active and inactive tax groups
        /// - Each group includes its full list of component taxes
        /// - Results are scoped to the current tenant
        /// - Components are returned in their defined calculation order
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of all tax groups with their components.</returns>
        /// <response code="200">Returns the list of tax groups.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("groups")]
        public async Task<ActionResult<IReadOnlyList<TaxGroupDto>>> GetAllTaxGroups()
        {
            var groups = await _taxConfigService.GetAllTaxGroupsAsync();
            return Ok(groups);
        }

        /// <summary>
        /// Retrieves active tax groups, optionally filtered by applicability.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating tax group dropdowns on invoice or purchase order forms
        /// - Filtering tax groups applicable to Sales, Purchases, or Both
        /// - Retrieving the working set of tax groups for transaction entry
        ///
        /// **Integration Pattern:**
        /// - Use `applicability=Sales` when creating sales invoices
        /// - Use `applicability=Purchases` when creating purchase orders
        /// - Omit the parameter to retrieve all active tax groups
        /// - For the default tax group, use `GET /groups/default` instead
        ///
        /// **Business Rules:**
        /// - Only returns tax groups where `IsActive` is true
        /// - Groups with `Applicability = Both` are included in both Sales and Purchases filters
        /// - Each group includes its component taxes with calculation order
        /// - Results are scoped to the current tenant
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="applicability">Optional filter by tax applicability. Valid values: Sales (1), Purchases (2), Both (3). When omitted, all active tax groups are returned.</param>
        /// <returns>List of active tax groups matching the filter criteria.</returns>
        /// <response code="200">Returns the list of active tax groups.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("groups/active")]
        public async Task<ActionResult<IReadOnlyList<TaxGroupDto>>> GetActiveTaxGroups([FromQuery] TaxApplicability? applicability = null)
        {
            var groups = await _taxConfigService.GetActiveTaxGroupsAsync(applicability);
            return Ok(groups);
        }

        /// <summary>
        /// Retrieves a tax group by ID.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading tax group details for viewing or editing
        /// - Fetching a specific tax group after creation (via the CreatedAtAction location header)
        /// - Resolving tax group details from an ID referenced in a transaction
        ///
        /// **Integration Pattern:**
        /// - Use the ID returned from `POST /groups` or from list endpoints
        /// - The response includes the full component list with calculation order and compound basis
        /// - If you only have the group code, use `GET /groups/by-code/{code}` instead
        ///
        /// **Business Rules:**
        /// - Returns the tax group regardless of its active/inactive status
        /// - Components are returned in calculation order
        /// - Returns 404 if no tax group exists with the given ID in the current tenant
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax group.</param>
        /// <returns>The tax group with the specified ID, including its components.</returns>
        /// <response code="200">Returns the tax group.</response>
        /// <response code="404">Tax group not found.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("groups/{id}")]
        public async Task<ActionResult<TaxGroupDto>> GetTaxGroupById(Guid id)
        {
            var group = await _taxConfigService.GetTaxGroupByIdAsync(id);
            if (group == null)
                return NotFound(new { message = "Tax group not found" });

            return Ok(group);
        }

        /// <summary>
        /// Retrieves a tax group by code.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Looking up a tax group by its human-readable code (e.g., "GH-STD-SALES")
        /// - Resolving tax group references from imported data or external systems
        /// - Quick lookup when the group code is known but the GUID is not
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint when integrating with external systems that reference tax groups by code
        /// - Tax group codes are unique within a tenant
        /// - For GUID-based lookups, use `GET /groups/{id}` instead
        ///
        /// **Business Rules:**
        /// - Tax group codes are case-sensitive
        /// - Returns the tax group regardless of its active/inactive status
        /// - Returns 404 if no tax group exists with the given code in the current tenant
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="code">The unique code of the tax group (e.g., "GH-STD-SALES").</param>
        /// <returns>The tax group with the specified code, including its components.</returns>
        /// <response code="200">Returns the tax group.</response>
        /// <response code="404">Tax group not found.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("groups/by-code/{code}")]
        public async Task<ActionResult<TaxGroupDto>> GetTaxGroupByCode(string code)
        {
            var group = await _taxConfigService.GetTaxGroupByCodeAsync(code);
            if (group == null)
                return NotFound(new { message = $"Tax group '{code}' not found" });

            return Ok(group);
        }

        /// <summary>
        /// Creates a new tax group.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Setting up a new tax combination (e.g., Ghana standard sales taxes: NHIL + GETFL + COVID + VAT)
        /// - Creating separate tax groups for different transaction types (Sales vs Purchases)
        /// - Defining compound tax calculation sequences with specific ordering
        ///
        /// **Integration Pattern:**
        /// - After creation, the response includes a `Location` header pointing to `GET /groups/{id}`
        /// - Returns HTTP 201 with the created tax group DTO on success
        /// - Components can be included in the creation request or added later via `POST /groups/{groupId}/components`
        ///
        /// **Business Rules:**
        /// - Tax group code must be unique within the tenant
        /// - Only one tax group can be marked as default per applicability type
        /// - Components define the calculation order and compound basis for each tax
        /// - CompoundBasis determines how each component tax is calculated (BaseOnly, Cumulative, or Specific)
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="dto">The tax group data to create, including code, name, applicability, default flag, and optional initial components.</param>
        /// <returns>The created tax group with its components.</returns>
        /// <response code="201">Tax group created successfully.</response>
        /// <response code="400">Invalid request data (e.g., duplicate code, invalid component references).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("groups")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<TaxGroupDto>> CreateTaxGroup([FromBody] CreateTaxGroupDto dto)
        {
            try
            {
                var group = await _taxConfigService.CreateTaxGroupAsync(dto);
                return CreatedAtAction(nameof(GetTaxGroupById), new { id = group.Id }, group);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing tax group.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Changing the name, description, or applicability of a tax group
        /// - Marking a tax group as the default for its applicability type
        /// - Activating or deactivating a tax group
        ///
        /// **Integration Pattern:**
        /// - Only include fields that need updating; null fields are left unchanged
        /// - To modify components, use the dedicated component endpoints (`POST`, `PUT`, `DELETE` under `/groups/components`)
        /// - After updating, use `GET /groups/{id}` to fetch the latest state
        ///
        /// **Business Rules:**
        /// - Cannot change the tax group code (immutable after creation)
        /// - Setting `IsDefault = true` will unset the default flag on any other group with the same applicability
        /// - Deactivating a tax group does not affect existing transactions that reference it
        /// - Only metadata is updated through this endpoint; use component endpoints for tax composition changes
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax group to update.</param>
        /// <param name="dto">The updated tax group data. Only non-null fields will be applied.</param>
        /// <returns>The updated tax group with its components.</returns>
        /// <response code="200">Tax group updated successfully.</response>
        /// <response code="400">Invalid request data or business rule violation.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPut("groups/{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<TaxGroupDto>> UpdateTaxGroup(Guid id, [FromBody] UpdateTaxGroupDto dto)
        {
            try
            {
                var group = await _taxConfigService.UpdateTaxGroupAsync(id, dto);
                return Ok(group);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a tax group.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing a tax group that was created in error
        /// - Cleaning up unused tax group definitions during system configuration
        ///
        /// **Integration Pattern:**
        /// - This is a permanent deletion; consider deactivating via `PUT /groups/{id}` instead
        /// - Returns a success message on completion
        /// - After deletion, the tax group ID will no longer resolve via `GET /groups/{id}`
        ///
        /// **Business Rules:**
        /// - Cannot delete a tax group that has been used in posted transactions
        /// - Cannot delete the default tax group without first assigning another group as default
        /// - Consider deactivating (`IsActive = false`) instead of deleting for audit trail preservation
        /// - Deleting a group also removes its component associations
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the tax group to delete.</param>
        /// <returns>A success message confirming deletion.</returns>
        /// <response code="200">Tax group deleted successfully.</response>
        /// <response code="400">Tax group cannot be deleted (e.g., in use by transactions or is the default group).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpDelete("groups/{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> DeleteTaxGroup(Guid id)
        {
            try
            {
                await _taxConfigService.DeleteTaxGroupAsync(id);
                return Ok(new { message = "Tax group deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Tax Group Components

        /// <summary>
        /// Adds a component to a tax group.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Adding a new tax (e.g., a new levy) to an existing tax group
        /// - Building up a tax group incrementally after initial creation
        /// - Inserting a tax into a specific position in the calculation sequence
        ///
        /// **Integration Pattern:**
        /// - The `TaxId` in the request body must reference an existing, active tax
        /// - If `CalculationOrder` is omitted, the component is appended at the end
        /// - Use `CompoundBasis` to control how this tax interacts with others in the group
        /// - After adding, use `GET /groups/{groupId}` to see the updated group with all components
        ///
        /// **Business Rules:**
        /// - A tax can only appear once within a tax group
        /// - CalculationOrder determines the sequence in which taxes are applied
        /// - CompoundBasis options: BaseOnly (tax on base amount), Cumulative (tax on base + all prior taxes), Specific (tax on base + specified prior taxes)
        /// - When CompoundBasis is Specific, provide `AppliesOnTaxCodes` to list which prior tax codes to compound on
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="groupId">The unique identifier (GUID) of the tax group to add the component to.</param>
        /// <param name="dto">The component data including the tax ID, calculation order, compound basis, and optional compounding tax codes.</param>
        /// <returns>The added component with resolved tax details.</returns>
        /// <response code="200">Component added successfully.</response>
        /// <response code="400">Invalid request data (e.g., duplicate tax in group, invalid tax ID, invalid compound configuration).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("groups/{groupId}/components")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<TaxGroupComponentDto>> AddComponentToGroup(Guid groupId, [FromBody] AddTaxGroupComponentDto dto)
        {
            try
            {
                var component = await _taxConfigService.AddComponentToGroupAsync(groupId, dto);
                return Ok(component);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates a tax group component.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Changing the calculation order of a tax within a group
        /// - Modifying the compound basis (e.g., switching from BaseOnly to Cumulative)
        /// - Updating the list of tax codes that a Specific compound basis applies on
        ///
        /// **Integration Pattern:**
        /// - Only include fields that need updating; null fields are left unchanged
        /// - The component ID is obtained from `GET /groups/{id}` response (within the Components array)
        /// - After updating, use `GET /groups/{id}` to see the updated group with recalculated component details
        ///
        /// **Business Rules:**
        /// - Cannot change the underlying tax reference (TaxId); remove and re-add instead
        /// - Changing CompoundBasis to Specific requires providing `AppliesOnTaxCodes`
        /// - Calculation order changes may affect the tax amounts for compound taxes
        /// - Changes take effect for new transactions only; existing transactions are not recalculated
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="componentId">The unique identifier (GUID) of the component to update.</param>
        /// <param name="dto">The updated component data. Only non-null fields will be applied.</param>
        /// <returns>The updated component with resolved tax details.</returns>
        /// <response code="200">Component updated successfully.</response>
        /// <response code="400">Invalid request data or business rule violation.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPut("groups/components/{componentId}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<TaxGroupComponentDto>> UpdateComponent(Guid componentId, [FromBody] UpdateTaxGroupComponentDto dto)
        {
            try
            {
                var component = await _taxConfigService.UpdateComponentAsync(componentId, dto);
                return Ok(component);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Removes a component from a tax group.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing a tax from a tax group when it is no longer applicable
        /// - Restructuring the tax composition of a group
        /// - Removing a levy that has been repealed or replaced
        ///
        /// **Integration Pattern:**
        /// - The component ID is obtained from `GET /groups/{id}` response (within the Components array)
        /// - Returns a success message on completion
        /// - After removal, remaining components retain their calculation order (use reorder endpoint if needed)
        ///
        /// **Business Rules:**
        /// - Removing a component does not delete the underlying tax definition
        /// - If other components in the group have a Specific compound basis referencing the removed tax code, those references should be reviewed
        /// - Changes take effect for new transactions only; existing transactions are not recalculated
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="componentId">The unique identifier (GUID) of the component to remove.</param>
        /// <returns>A success message confirming removal.</returns>
        /// <response code="200">Component removed successfully.</response>
        /// <response code="400">Component cannot be removed.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpDelete("groups/components/{componentId}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> RemoveComponentFromGroup(Guid componentId)
        {
            try
            {
                await _taxConfigService.RemoveComponentFromGroupAsync(componentId);
                return Ok(new { message = "Component removed successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Reorders components within a tax group.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Changing the calculation sequence of taxes within a group (e.g., ensuring levies are calculated before VAT)
        /// - Correcting the order after adding new components
        /// - Aligning calculation order with regulatory requirements
        ///
        /// **Integration Pattern:**
        /// - Send the complete list of component IDs in the desired order
        /// - The list must contain all component IDs belonging to the group (no additions or omissions)
        /// - The position in the array determines the new `CalculationOrder` (index 0 = order 1, etc.)
        ///
        /// **Business Rules:**
        /// - All component IDs in the request must belong to the specified tax group
        /// - The list must contain exactly the same set of component IDs as the group currently has
        /// - Reordering affects compound tax calculations: taxes with Cumulative or Specific compound basis depend on the order
        /// - Example order for Ghana: NHIL (1) -> GETFL (2) -> COVID (3) -> VAT (4, Cumulative on prior three)
        /// - Changes take effect for new transactions only
        ///
        /// **Authorization:** Requires SuperAdmin or TenantAdmin role
        /// </remarks>
        /// <param name="groupId">The unique identifier (GUID) of the tax group whose components should be reordered.</param>
        /// <param name="orderedComponentIds">List of component IDs in the desired calculation order. Must include all components of the group.</param>
        /// <returns>A success message confirming the reorder.</returns>
        /// <response code="200">Components reordered successfully.</response>
        /// <response code="400">Invalid reorder request (e.g., missing component IDs, IDs not belonging to the group).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SuperAdmin or TenantAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("groups/{groupId}/reorder")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> ReorderComponents(Guid groupId, [FromBody] List<Guid> orderedComponentIds)
        {
            try
            {
                await _taxConfigService.ReorderComponentsAsync(groupId, orderedComponentIds);
                return Ok(new { message = "Components reordered successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Tax Calculation

        /// <summary>
        /// Calculates taxes for a transaction.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Computing tax breakdown for a sales invoice line item
        /// - Previewing tax amounts before committing a transaction
        /// - Calculating withholding tax for a supplier payment
        /// - Determining the total tax-inclusive amount for a purchase order
        ///
        /// **Integration Pattern:**
        /// - Provide a `TaxGroupId` to calculate using a predefined tax group, or omit it to use the default group for the transaction type
        /// - Alternatively, provide `ManualTaxIds` to calculate using a custom selection of individual taxes (overrides TaxGroupId)
        /// - The response includes a detailed breakdown showing each tax component, its taxable amount, rate, and calculated amount
        /// - Use the `EffectiveTaxRate` in the response for display purposes
        ///
        /// **Business Rules:**
        /// - Taxes are calculated in their defined calculation order within the group
        /// - Compound taxes (Cumulative or Specific basis) are computed on the base amount plus the specified prior taxes
        /// - Withholding taxes are subject to threshold checks when `CustomerId` or `SupplierId` is provided
        /// - The `TransactionDate` determines which tax rates are in effect
        /// - Transaction type (SaleOfGoods, PurchaseOfServices, etc.) determines tax applicability
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="request">The tax calculation request containing base amount, tax group or manual tax selection, transaction date, type, and optional entity IDs for threshold checks.</param>
        /// <returns>The tax calculation result with total amounts and detailed per-tax breakdown.</returns>
        /// <response code="200">Returns the calculation result with tax breakdown.</response>
        /// <response code="400">Invalid calculation request (e.g., invalid tax group ID, no applicable taxes found).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("calculate")]
        public async Task<ActionResult<TaxCalculationResultDto>> CalculateTaxes([FromBody] TaxCalculationRequestDto request)
        {
            try
            {
                var result = await _taxCalculationEngine.CalculateTaxesAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the default tax group for a given applicability.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Automatically selecting the appropriate tax group when creating a new invoice or purchase order
        /// - Pre-populating the tax group field on transaction entry forms
        /// - Determining which taxes apply by default for a given transaction type
        ///
        /// **Integration Pattern:**
        /// - Call with `applicability=Sales` when initiating a sales transaction
        /// - Call with `applicability=Purchases` when initiating a purchase transaction
        /// - The returned group includes all component taxes with their calculation order and compound basis
        /// - Falls back to groups with `Applicability = Both` if no specific default is found
        ///
        /// **Business Rules:**
        /// - Only one tax group can be marked as default per applicability type within a tenant
        /// - Returns 404 if no default group has been configured for the specified applicability
        /// - The default group must be active; inactive default groups are not returned
        /// - Default groups are typically set up during initial system configuration or via the seed endpoint
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="applicability">The tax applicability type. Valid values: Sales (1), Purchases (2), Both (3).</param>
        /// <returns>The default tax group for the specified applicability, including its components.</returns>
        /// <response code="200">Returns the default tax group.</response>
        /// <response code="404">No default tax group found for the specified applicability.</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("groups/default")]
        public async Task<ActionResult<TaxGroupDto>> GetDefaultTaxGroup([FromQuery] TaxApplicability applicability)
        {
            var group = await _taxCalculationEngine.GetDefaultTaxGroupAsync(applicability);
            if (group == null)
                return NotFound(new { message = "No default tax group found for this applicability" });

            return Ok(group);
        }

        /// <summary>
        /// Checks withholding tax threshold status for an entity.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Determining whether a supplier has exceeded the withholding tax threshold for the fiscal year
        /// - Checking cumulative transaction amounts against regulatory thresholds before processing payments
        /// - Displaying threshold utilization percentage on supplier or customer dashboards
        ///
        /// **Integration Pattern:**
        /// - Call before processing a payment to determine if withholding tax should be applied
        /// - The `entityType` parameter distinguishes between customers and suppliers (e.g., "Customer", "Supplier")
        /// - The `amount` parameter represents the current transaction amount to check against the remaining threshold
        /// - Use the `IsThresholdExceeded` flag in the response to decide whether to apply withholding tax
        ///
        /// **Business Rules:**
        /// - Thresholds are tracked per fiscal year and reset at year-end
        /// - The `CumulativeAmount` reflects total transactions for the entity in the current fiscal year
        /// - `RemainingAmount` shows how much more can be transacted before the threshold is exceeded
        /// - `PercentageUsed` provides a quick indicator of threshold utilization (0-100+)
        /// - Once the threshold is exceeded, withholding tax applies to the full transaction amount, not just the excess
        /// - The `ThresholdExceededDate` records when the threshold was first surpassed
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="taxId">The unique identifier (GUID) of the withholding tax to check the threshold for.</param>
        /// <param name="entityType">The type of entity (e.g., "Customer", "Supplier").</param>
        /// <param name="entityId">The unique identifier (GUID) of the entity (customer or supplier).</param>
        /// <param name="amount">The current transaction amount to evaluate against the threshold.</param>
        /// <returns>The threshold status including cumulative amounts, remaining threshold, and whether the threshold has been exceeded.</returns>
        /// <response code="200">Returns the threshold status.</response>
        /// <response code="400">Invalid request (e.g., tax does not have a threshold configured, invalid entity type).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("threshold/status")]
        public async Task<ActionResult<TaxThresholdStatusDto>> CheckThreshold(
            [FromQuery] Guid taxId,
            [FromQuery] string entityType,
            [FromQuery] Guid entityId,
            [FromQuery] decimal amount)
        {
            try
            {
                var status = await _taxCalculationEngine.CheckThresholdAsync(
                    taxId,
                    entityType,
                    entityId,
                    amount);

                return Ok(status);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        #region Seed Data

        /// <summary>
        /// Seeds default Ghana tax configurations.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Initializing the system with Ghana-specific tax rates during first-time setup
        /// - Resetting tax configurations to Ghana defaults after testing
        /// - Bootstrapping a new tenant with standard Ghana taxes (VAT, NHIL, GETFL, COVID Levy, WHT)
        ///
        /// **Integration Pattern:**
        /// - This is a one-time setup operation, typically called during system provisioning
        /// - After seeding, use `GET /taxes` and `GET /groups` to verify the created configurations
        /// - Seeded taxes include standard rates, levies, and withholding taxes as per Ghana Revenue Authority regulations
        ///
        /// **Business Rules:**
        /// - Creates standard Ghana taxes: VAT (15%), NHIL (2.5%), GETFL (1%), COVID Levy (1%)
        /// - Creates withholding tax configurations for various transaction types
        /// - Sets up default tax groups with proper calculation order and compound basis
        /// - Skips creation of taxes or groups that already exist (idempotent for existing codes)
        /// - Links taxes to appropriate GL accounts if chart of accounts is configured
        ///
        /// **Authorization:** Requires SystemAdmin role
        /// </remarks>
        /// <returns>A success message confirming the seeding operation.</returns>
        /// <response code="200">Ghana taxes seeded successfully.</response>
        /// <response code="400">Seeding failed (e.g., prerequisite configurations missing).</response>
        /// <response code="401">Unauthorized - user is not authenticated.</response>
        /// <response code="403">Forbidden - user does not have SystemAdmin role.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("seed/ghana")]
        [Authorize(Roles = "SystemAdmin")]
        public async Task<IActionResult> SeedGhanaTaxes()
        {
            try
            {
                await _taxConfigService.SeedGhanaTaxesAsync();
                return Ok(new { message = "Ghana taxes seeded successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion
    }
}
