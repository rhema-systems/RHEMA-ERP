using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages exchange rates for multi-currency transactions.
    /// </summary>
    /// <remarks>
    /// This controller provides exchange rate management for converting foreign currency
    /// transactions to the base currency. All multi-currency modules should use the
    /// GetCurrentRate endpoint to get the appropriate exchange rate for transactions.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/exchange-rates")]
    public class ExchangeRateController : ControllerBase
    {
        private readonly IExchangeRateService _exchangeRateService;

        public ExchangeRateController(IExchangeRateService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        /// <summary>
        /// Retrieves exchange rates with optional filtering.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Get available exchange rates for a currency pair
        /// - Historical rate lookup
        /// - Rate management and review
        ///
        /// **Integration Pattern:**
        /// - Use GET /api/Finance/exchange-rates/current/{code} for transaction posting
        /// - Use this endpoint for rate history and management
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="fromCurrency">Filter by base currency code</param>
        /// <param name="toCurrency">Filter by target currency code</param>
        /// <param name="rateType">Filter by rate type: Daily, Average, MonthEnd, Custom</param>
        /// <param name="startDate">Filter rates effective on or after this date</param>
        /// <param name="endDate">Filter rates effective on or before this date</param>
        /// <returns>List of exchange rates</returns>
        /// <response code="200">Returns the list of exchange rates</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        public async Task<ActionResult<List<ExchangeRateDto>>> GetExchangeRates(
            [FromQuery] string? fromCurrency = null,
            [FromQuery] string? toCurrency = null,
            [FromQuery] string? rateType = null,
            [FromQuery] string? quoteSide = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var rates = await _exchangeRateService.GetExchangeRatesAsync(
                    fromCurrency, toCurrency, startDate, rateType, quoteSide);
                return Ok(rates);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves historical exchange rate trend data for a target currency.
        /// </summary>
        /// <param name="currencyCode">Target currency code to analyze (e.g., USD)</param>
        /// <param name="months">Number of months to include, from today backwards</param>
        /// <param name="baseCurrencyCode">Optional base currency code; defaults to tenant base currency</param>
        /// <param name="movingAverageWindow">Number of trend points used for moving average and volatility</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Trend points for the requested currency and period</returns>
        /// <response code="200">Returns exchange rate trend data</response>
        /// <response code="400">Invalid currency or analysis settings</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("trends/{currencyCode}")]
        public async Task<ActionResult<IReadOnlyList<TrendAnalysisDto>>> GetExchangeRateTrends(
            string currencyCode,
            [FromQuery] int months = 6,
            [FromQuery] string? baseCurrencyCode = null,
            [FromQuery] int movingAverageWindow = 7,
            CancellationToken cancellationToken = default)
        {
            if (months < 1 || months > 60)
            {
                return BadRequest("Months must be between 1 and 60.");
            }

            try
            {
                var endDate = DateTime.UtcNow.Date;
                var startDate = endDate.AddMonths(-months);
                var trends = await _exchangeRateService.GetTrendsAsync(
                    baseCurrencyCode,
                    currencyCode,
                    startDate,
                    endDate,
                    movingAverageWindow: movingAverageWindow,
                    cancellationToken: cancellationToken);

                return Ok(trends);
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
        /// Retrieves a specific exchange rate by ID.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Get rate details
        /// - Audit rate history
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="id">Exchange rate ID</param>
        /// <returns>Exchange rate details</returns>
        /// <response code="200">Returns the exchange rate</response>
        /// <response code="404">Exchange rate not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<ExchangeRateDto>> GetExchangeRateById(Guid id)
        {
            try
            {
                var rate = await _exchangeRateService.GetExchangeRateByIdAsync(id);
                if (rate == null)
                    return NotFound($"Exchange rate with ID {id} not found");

                return Ok(rate);
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
        /// Gets the current effective exchange rate for a specific currency.
        /// </summary>
        /// <remarks>
        /// **CRITICAL ENDPOINT:** Use this for all multi-currency transaction posting.
        ///
        /// **Common Use Cases:**
        /// - **Sales Module:** Convert foreign currency invoice to base currency
        /// - **Purchasing Module:** Convert foreign currency PO to base currency
        /// - **Inventory Module:** Convert foreign currency costs
        /// - **Payroll Module:** Convert foreign currency salaries
        ///
        /// **Integration Pattern for Multi-Currency Transactions:**
        /// ```
        /// 1. User enters transaction in foreign currency (e.g., USD)
        /// 2. GET /api/Finance/exchange-rates/current/USD
        /// 3. Use returned rate to convert to base currency
        /// 4. Store both foreign amount and base amount in journal entry
        /// 5. Include exchange rate in journal entry for audit trail
        /// ```
        ///
        /// **Example:**
        /// ```
        /// GET /api/Finance/exchange-rates/current/USD
        /// Returns: { rate: 14.5, effectiveDate: "2024-12-15", rateType: "Daily" }
        ///
        /// Invoice Amount: $1,000 USD
        /// Base Currency Amount: 1,000 x 14.5 = GHS 14,500
        /// ```
        ///
        /// **Business Rules:**
        /// - Returns the most recent active rate for the currency
        /// - Rate is from base currency perspective (e.g., 1 USD = X GHS)
        /// - If no rate found, returns 404 (transaction should be rejected)
        /// - Rate type priority: Daily > Average > MonthEnd > Custom
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="currencyCode">Currency code to get rate for (e.g., "USD")</param>
        /// <returns>Current exchange rate for the currency</returns>
        /// <response code="200">Returns the current exchange rate</response>
        /// <response code="404">No current rate found for this currency</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("current/{currencyCode}")]
        public async Task<ActionResult<ExchangeRateDto>> GetCurrentRate(
            string currencyCode,
            [FromQuery] string? baseCurrencyCode = null,
            [FromQuery] DateTime? effectiveDate = null,
            [FromQuery] string? rateType = null,
            [FromQuery] string? quoteSide = null)
        {
            try
            {
                var rate = await _exchangeRateService.GetCurrentRateAsync(
                    currencyCode,
                    baseCurrencyCode,
                    effectiveDate,
                    rateType,
                    quoteSide);
                if (rate == null)
                    return NotFound($"No current exchange rate found for {currencyCode}");

                return Ok(rate);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new exchange rate.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module for rate maintenance. Other modules should not call this.
        ///
        /// **Business Rules:**
        /// - Rate must be positive
        /// - Effective date must be specified
        /// - Cannot create duplicate rates for same currency/date/type
        /// - Rate is always from base currency perspective
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="dto">Exchange rate creation details</param>
        /// <returns>Created exchange rate</returns>
        /// <response code="201">Exchange rate created successfully</response>
        /// <response code="400">Invalid data or duplicate rate</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        public async Task<ActionResult<ExchangeRateDto>> CreateExchangeRate([FromBody] CreateExchangeRateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var rate = await _exchangeRateService.CreateExchangeRateAsync(dto);
                return CreatedAtAction(nameof(GetExchangeRateById), new { id = rate.Id }, rate);
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
        /// Bulk uploads multiple exchange rates at once.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module for importing rates from external sources.
        ///
        /// **Common Use Cases:**
        /// - Import daily rates from central bank
        /// - Load month-end rates
        /// - Initial system setup
        ///
        /// **Business Rules:**
        /// - All rates must be valid
        /// - Validation is all-or-nothing: no row is persisted when any row is invalid
        /// - Accepted rows enter the governed approval workflow; a workflow-start failure is retained
        ///   as rejected audit evidence rather than being silently deleted
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="dtos">List of exchange rates to create</param>
        /// <returns>List of created exchange rates</returns>
        /// <response code="200">Exchange rates uploaded successfully</response>
        /// <response code="400">Invalid data in one or more rates</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("bulk")]
        public async Task<ActionResult<List<ExchangeRateDto>>> BulkUploadRates([FromBody] List<CreateExchangeRateDto> dtos)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var rates = await _exchangeRateService.BulkUploadRatesAsync(dtos);
                return Ok(rates);
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
        /// Updates an existing exchange rate.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module for rate corrections.
        ///
        /// **Business Rules:**
        /// - Rate must be positive
        /// - Cannot update rates that have been used in posted transactions
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="id">Exchange rate ID to update</param>
        /// <param name="dto">Updated exchange rate details</param>
        /// <returns>Updated exchange rate</returns>
        /// <response code="200">Exchange rate updated successfully</response>
        /// <response code="400">Invalid data or business rule violation</response>
        /// <response code="404">Exchange rate not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{id}")]
        public async Task<ActionResult<ExchangeRateDto>> UpdateExchangeRate(Guid id, [FromBody] UpdateExchangeRateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var rate = await _exchangeRateService.UpdateExchangeRateAsync(id, dto);
                return Ok(rate);
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
        /// Deletes an exchange rate.
        /// </summary>
        /// <remarks>
        /// Typically used by Finance module for rate cleanup.
        ///
        /// **Business Rules:**
        /// - Cannot delete rates that have been used in posted transactions
        /// - Soft delete is performed
        ///
        /// **Authorization:** Requires Finance.Delete permission
        /// </remarks>
        /// <param name="id">Exchange rate ID to delete</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Exchange rate deleted successfully</response>
        /// <response code="400">Cannot delete - rate has been used in transactions</response>
        /// <response code="404">Exchange rate not found</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteExchangeRate(Guid id)
        {
            try
            {
                await _exchangeRateService.DeleteExchangeRateAsync(id);
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
    }
}
