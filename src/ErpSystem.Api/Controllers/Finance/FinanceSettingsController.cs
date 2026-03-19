using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages tenant-level finance module configuration and settings.
    /// </summary>
    /// <remarks>
    /// **Responsibilities:**
    /// - Retrieving the current finance settings for the authenticated tenant
    /// - Updating global finance configuration (fiscal year, base currency, COA type, etc.)
    /// - Validating whether structural changes (e.g., COA type switch) are safe to perform
    ///
    /// **Integration Pattern:**
    /// - Finance settings are consumed by virtually all other finance sub-modules (General Ledger,
    ///   Accounts Payable, Accounts Receivable, Fixed Assets, Taxation, etc.) to determine default
    ///   behaviors such as fiscal year boundaries, base currency, and chart of accounts structure.
    /// - Settings must be configured before any transactional data is entered into the finance module.
    ///
    /// **Multi-Tenancy:**
    /// - All operations are scoped to the current tenant via <see cref="ICurrentUserService"/>.
    ///   Each tenant maintains its own independent finance configuration.
    ///
    /// **Authorization:** Requires authenticated access. Write operations typically require Finance Admin permissions.
    /// </remarks>
    [ApiController]
    [Route("api/finance")]
    public class FinanceSettingsController : ControllerBase
    {
        private readonly IFinanceSettingsService _financeSettingsService;
        private readonly ICurrentUserService _currentUserService;

        public FinanceSettingsController(
            IFinanceSettingsService financeSettingsService,
            ICurrentUserService currentUserService)
        {
            _financeSettingsService = financeSettingsService;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Retrieves the complete finance settings configuration for the current tenant.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading the finance settings page in the administration UI
        /// - Other finance modules querying current configuration (fiscal year, base currency, COA type)
        /// - Determining whether the finance module has been initially configured for a new tenant
        ///
        /// **Integration Pattern:**
        /// - Called by the frontend settings page on load to populate all configuration fields
        /// - Referenced internally by GL, AP, AR, and Fixed Assets modules to resolve default values
        /// - Returns a single settings object representing the tenant's full finance configuration
        ///
        /// **Business Rules:**
        /// - Settings are automatically created with system defaults when a new tenant is provisioned
        /// - The returned object reflects the most recent saved configuration
        /// - If settings have never been explicitly saved, default values are returned
        ///
        /// **Authorization:** Requires authenticated access with Finance read permissions.
        /// </remarks>
        /// <returns>The current tenant's <see cref="FinanceSettingsDto"/> containing all finance configuration values.</returns>
        /// <response code="200">Finance settings retrieved successfully.</response>
        /// <response code="400">An error occurred while retrieving settings (e.g., tenant resolution failure).</response>
        /// <response code="401">Not authenticated or session has expired.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var settings = await _financeSettingsService.GetSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Updates the finance settings configuration for the current tenant.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Administrators configuring the finance module for the first time after tenant setup
        /// - Changing the fiscal year start month or reporting currency
        /// - Switching the Chart of Accounts type (e.g., from IFRS to custom) before accounts are created
        /// - Updating default tax, rounding, or numbering preferences
        ///
        /// **Integration Pattern:**
        /// - Called from the finance administration settings page when the user saves changes
        /// - Changes propagate to all downstream finance modules that depend on global settings
        /// - Certain changes (e.g., COA type) may be irreversible once transactional data exists;
        ///   use the <c>GET settings/can-change-coa-type</c> endpoint to check feasibility first
        ///
        /// **Business Rules:**
        /// - COA type cannot be changed if chart of accounts entries already exist; an
        ///   <see cref="InvalidOperationException"/> is thrown in that case
        /// - Fiscal year changes may be restricted if periods have already been closed
        /// - Base currency changes may be restricted if transactions have been posted
        /// - All validation is performed server-side by <see cref="IFinanceSettingsService"/>
        ///
        /// **Authorization:** Requires authenticated access with Finance Admin or Settings Management permissions.
        /// </remarks>
        /// <param name="dto">The <see cref="UpdateFinanceSettingsDto"/> containing the updated configuration values to persist.</param>
        /// <returns>The updated <see cref="FinanceSettingsDto"/> reflecting the newly saved configuration.</returns>
        /// <response code="200">Settings updated successfully. Returns the updated settings object.</response>
        /// <response code="400">Validation or business rule violation (e.g., attempting to change COA type when accounts exist).</response>
        /// <response code="401">Not authenticated or session has expired.</response>
        /// <response code="500">An unexpected error occurred while updating settings.</response>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateFinanceSettingsDto dto)
        {
            try
            {
                var settings = await _financeSettingsService.UpdateSettingsAsync(dto);
                return Ok(settings);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating settings", details = ex.Message });
            }
        }

        /// <summary>
        /// Checks whether the Chart of Accounts (COA) type can still be changed for the current tenant.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - The settings UI calling this endpoint on load to conditionally enable or disable the COA type dropdown
        /// - Pre-validation before presenting the COA type change option to administrators
        /// - Audit or diagnostic checks to confirm whether the finance module is still in its initial setup phase
        ///
        /// **Integration Pattern:**
        /// - Should be called before displaying the COA type selector on the finance settings page
        /// - If <c>canChange</c> is <c>false</c>, the frontend should disable the COA type field and display
        ///   an informational message explaining that accounts already exist
        /// - This is a read-only check with no side effects
        ///
        /// **Business Rules:**
        /// - Returns <c>true</c> only if zero chart of accounts entries exist for the current tenant
        /// - Once any account has been created under the current COA structure, the type becomes locked
        /// - This restriction exists because switching COA types after accounts are created would
        ///   orphan existing account mappings and break reporting consistency
        /// - Deleting all accounts would allow the COA type to be changed again
        ///
        /// **Authorization:** Requires authenticated access with Finance read permissions.
        /// </remarks>
        /// <returns>An object containing a boolean <c>canChange</c> property indicating whether the COA type can be modified.</returns>
        /// <response code="200">Check completed successfully. Returns <c>{ canChange: true/false }</c>.</response>
        /// <response code="400">An error occurred while performing the check (e.g., tenant resolution failure).</response>
        /// <response code="401">Not authenticated or session has expired.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("settings/can-change-coa-type")]
        public async Task<IActionResult> CanChangeCOAType()
        {
            try
            {
                var canChange = await _financeSettingsService.CanChangeCOATypeAsync();
                return Ok(new { canChange });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
