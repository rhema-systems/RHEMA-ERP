using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages currencies for multi-currency support in the ERP system.
    /// </summary>
    /// <remarks>
    /// This controller provides currency configuration and management for all modules
    /// that support multi-currency transactions including Sales, Purchasing, and Inventory.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/currency")]
    public class CurrencyController : ControllerBase
    {
        private readonly ICurrencyService _currencyService;

        public CurrencyController(ICurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        /// <summary>
        /// Retrieves all currencies configured in the system.
        /// </summary>
        /// <remarks>
        /// Used by all multi-currency modules to display currency options.
        ///
        /// **Common Use Cases:**
        /// - **Multi-Currency Modules:** Display currency dropdown in transaction entry
        /// - **Sales Module:** Select invoice currency
        /// - **Purchasing Module:** Select PO currency
        /// - **Inventory Module:** Multi-currency pricing
        ///
        /// **Integration Pattern:**
        /// - Call on module initialization to cache currencies
        /// - Filter for isActive=true for transaction entry
        /// - Identify base currency for reporting
        ///
        /// **Business Rules:**
        /// - Returns both active and inactive currencies
        /// - Base currency is marked with isBaseCurrency=true
        /// - Inactive currencies cannot be used in new transactions
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <returns>List of all currencies</returns>
        /// <response code="200">Returns the list of currencies</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        public async Task<ActionResult<List<CurrencyDto>>> GetCurrencies()
        {
            try
            {
                var currencies = await _currencyService.GetCurrenciesAsync();
                return Ok(currencies);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific currency by its ISO currency code.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Validate currency exists before using in transactions
        /// - Get currency formatting details (symbol, decimal places)
        /// - Check if currency is active
        ///
        /// **Integration Pattern:**
        /// - Use to validate currency codes from external sources
        /// - Get formatting rules for display
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="code">ISO currency code (e.g., "USD", "EUR", "GHS")</param>
        /// <returns>Currency details including formatting rules</returns>
        /// <response code="200">Returns the currency</response>
        /// <response code="404">Currency not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{code}")]
        public async Task<ActionResult<CurrencyDto>> GetCurrencyByCode(string code)
        {
            try
            {
                var currency = await _currencyService.GetCurrencyByCodeAsync(code);
                if (currency == null)
                    return NotFound($"Currency with code {code} not found");

                return Ok(currency);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new currency in the system.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module during setup. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Currency code must be valid ISO 4217 code
        /// - Currency code must be unique
        /// - Only one base currency allowed per tenant
        /// - Decimal places typically 0-4
        ///
        /// **Authorization:** Requires Finance.Admin permission
        /// </remarks>
        /// <param name="dto">Currency creation details</param>
        /// <returns>Created currency</returns>
        /// <response code="201">Currency created successfully</response>
        /// <response code="400">Invalid data or duplicate currency code</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        public async Task<ActionResult<CurrencyDto>> CreateCurrency([FromBody] CreateCurrencyDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var currency = await _currencyService.CreateCurrencyAsync(dto);
                return CreatedAtAction(nameof(GetCurrencyByCode), new { code = currency.CurrencyCode }, currency);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing currency's properties.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Cannot change currency code
        /// - Cannot change decimal places if transactions exist
        /// - Cannot change base currency flag if transactions exist
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="code">Currency code to update</param>
        /// <param name="dto">Updated currency properties</param>
        /// <returns>Updated currency</returns>
        /// <response code="200">Currency updated successfully</response>
        /// <response code="400">Invalid data or business rule violation</response>
        /// <response code="404">Currency not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{code}")]
        public async Task<ActionResult<CurrencyDto>> UpdateCurrency(string code, [FromBody] UpdateCurrencyDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var currency = await _currencyService.UpdateCurrencyAsync(code, dto);
                return Ok(currency);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a currency from the system.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module during cleanup. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Cannot delete currency with transaction history
        /// - Cannot delete base currency
        /// - Cannot delete currency linked to accounts
        /// - Soft delete is performed
        ///
        /// **Authorization:** Requires Finance.Delete permission
        /// </remarks>
        /// <param name="code">Currency code to delete</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Currency deleted successfully</response>
        /// <response code="400">Cannot delete - has transactions or is base currency</response>
        /// <response code="404">Currency not found</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("{code}")]
        public async Task<ActionResult> DeleteCurrency(string code)
        {
            try
            {
                await _currencyService.DeleteCurrencyAsync(code);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Activates a currency, making it available for use in transactions.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Re-enable a previously deactivated currency
        /// - Enable newly configured currency
        ///
        /// **Effects:**
        /// - Currency becomes available in transaction entry dropdowns
        /// - Exchange rates can be configured
        /// - Accounts can be linked to this currency
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="code">Currency code to activate</param>
        /// <returns>Updated currency with isActive=true</returns>
        /// <response code="200">Currency activated successfully</response>
        /// <response code="404">Currency not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPatch("{code}/activate")]
        public async Task<ActionResult<CurrencyDto>> ActivateCurrency(string code)
        {
            try
            {
                var currency = await _currencyService.ToggleCurrencyStatusAsync(code, true);
                return Ok(currency);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deactivates a currency, preventing it from being used in new transactions.
        /// </summary>
        /// <remarks>
        /// Historical transactions remain unaffected.
        ///
        /// **Common Use Cases:**
        /// - Discontinue use of a currency
        /// - Temporarily disable a currency
        ///
        /// **Business Rules:**
        /// - Cannot deactivate base currency
        /// - Historical transactions remain accessible
        /// - Currency removed from transaction entry dropdowns
        ///
        /// **Effects:**
        /// - Currency no longer available for new transactions
        /// - Existing transactions remain valid
        /// - Can be reactivated later
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="code">Currency code to deactivate</param>
        /// <returns>Updated currency with isActive=false</returns>
        /// <response code="200">Currency deactivated successfully</response>
        /// <response code="400">Cannot deactivate base currency</response>
        /// <response code="404">Currency not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPatch("{code}/deactivate")]
        public async Task<ActionResult<CurrencyDto>> DeactivateCurrency(string code)
        {
            try
            {
                var currency = await _currencyService.ToggleCurrencyStatusAsync(code, false);
                return Ok(currency);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
