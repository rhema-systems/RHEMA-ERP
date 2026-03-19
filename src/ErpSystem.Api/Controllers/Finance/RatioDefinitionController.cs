using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Ratio Definitions and performing calculations.
    /// </summary>
    /// <remarks>
    /// Ratios combine financial accounts, unit accounts, and constants to calculate KPIs.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/ratio-definitions")]
    public class RatioDefinitionController : ControllerBase
    {
        private readonly IRatioDefinitionService _ratioDefinitionService;

        public RatioDefinitionController(IRatioDefinitionService ratioDefinitionService)
        {
            _ratioDefinitionService = ratioDefinitionService;
        }

        /// <summary>
        /// Retrieves all ratio definitions configured in the system.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading the full list of ratio definitions for an admin management screen
        /// - Populating dropdown selectors where users choose a ratio to calculate
        /// - Exporting all ratio configurations for backup or audit purposes
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint on page load to populate ratio definition lists
        /// - Results include both active and inactive definitions; use `GET /active` if only active ones are needed
        /// - Combine with calculation endpoints to display ratios alongside their definitions
        ///
        /// **Business Rules:**
        /// - Returns all ratio definitions regardless of their active/inactive status
        /// - Each ratio definition includes its numerator and denominator configuration
        /// - Results are not paginated; all definitions are returned in a single response
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of all ratio definitions including active and inactive ones.</returns>
        /// <response code="200">Returns the complete list of ratio definitions.</response>
        /// <response code="500">Internal server error occurred while retrieving ratio definitions.</response>
        [HttpGet]
        public async Task<ActionResult<List<RatioDefinitionDto>>> GetRatioDefinitions()
        {
            try
            {
                var ratios = await _ratioDefinitionService.GetAllAsync();
                return Ok(ratios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves only active ratio definitions.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Building a dashboard that only shows currently active KPI ratios
        /// - Populating calculation forms with ratios that are eligible for computation
        /// - Displaying a filtered list of ratios that are in production use
        ///
        /// **Integration Pattern:**
        /// - Preferred over `GET /` when only operational ratios are needed
        /// - Use this endpoint to feed the `calculate-all` endpoint's input selection
        /// - Ideal for end-user-facing views where inactive ratios should be hidden
        ///
        /// **Business Rules:**
        /// - Only returns ratio definitions where `IsActive` is true
        /// - Inactive ratios (deactivated via `PATCH /{id}/deactivate`) are excluded
        /// - Newly created ratios default to active unless explicitly deactivated
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of active ratio definitions only.</returns>
        /// <response code="200">Returns the list of active ratio definitions.</response>
        /// <response code="500">Internal server error occurred while retrieving active ratio definitions.</response>
        [HttpGet("active")]
        public async Task<ActionResult<List<RatioDefinitionDto>>> GetActiveRatioDefinitions()
        {
            try
            {
                var ratios = await _ratioDefinitionService.GetActiveAsync();
                return Ok(ratios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific ratio definition by its unique identifier.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading full details for a ratio definition edit form
        /// - Viewing the numerator/denominator configuration of a specific ratio
        /// - Fetching a single ratio definition after it has been created or updated
        ///
        /// **Integration Pattern:**
        /// - Typically called after selecting a ratio from a list view
        /// - Used by the `CreateRatioDefinition` endpoint's `CreatedAtAction` response to return the created resource
        /// - Combine with calculation endpoints to display configuration alongside computed results
        ///
        /// **Business Rules:**
        /// - Returns 404 if no ratio definition exists with the given ID
        /// - Returns the full configuration including numerator type, denominator type, result format, and precision
        /// - Includes audit fields (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to retrieve.</param>
        /// <returns>The ratio definition matching the specified ID.</returns>
        /// <response code="200">Returns the ratio definition with full configuration details.</response>
        /// <response code="404">No ratio definition was found with the specified ID.</response>
        /// <response code="500">Internal server error occurred while retrieving the ratio definition.</response>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RatioDefinitionDto>> GetRatioDefinitionById(Guid id)
        {
            try
            {
                var ratio = await _ratioDefinitionService.GetByIdAsync(id);
                if (ratio == null)
                    return NotFound($"Ratio definition with ID {id} not found");

                return Ok(ratio);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a ratio definition by its unique business code.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Looking up a ratio definition using a human-readable code (e.g., "CURRENT_RATIO", "DEBT_EQUITY")
        /// - Integrating with external systems that reference ratios by code rather than GUID
        /// - Quick lookup without needing to know the internal identifier
        ///
        /// **Integration Pattern:**
        /// - Use when the ratio code is known from configuration files or external references
        /// - Useful for seed data verification and configuration scripts
        /// - Prefer this over ID-based lookup when codes are stable business identifiers
        ///
        /// **Business Rules:**
        /// - Ratio codes are unique across the system
        /// - Returns 404 if no ratio definition matches the specified code
        /// - Code matching may be case-sensitive depending on database collation
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="code">The unique business code of the ratio definition (e.g., "CURRENT_RATIO").</param>
        /// <returns>The ratio definition matching the specified code.</returns>
        /// <response code="200">Returns the ratio definition with the matching code.</response>
        /// <response code="404">No ratio definition was found with the specified code.</response>
        /// <response code="500">Internal server error occurred while retrieving the ratio definition.</response>
        [HttpGet("code/{code}")]
        public async Task<ActionResult<RatioDefinitionDto>> GetRatioDefinitionByCode(string code)
        {
            try
            {
                var ratio = await _ratioDefinitionService.GetByCodeAsync(code);
                if (ratio == null)
                    return NotFound($"Ratio definition with code {code} not found");

                return Ok(ratio);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new ratio definition with numerator and denominator configuration.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Defining a new financial KPI ratio (e.g., Current Ratio, Debt-to-Equity)
        /// - Setting up ratios that combine financial accounts, unit accounts, or constant values
        /// - Configuring custom performance metrics for management reporting
        ///
        /// **Integration Pattern:**
        /// - Send the complete ratio configuration in the request body
        /// - On success, returns 201 with a `Location` header pointing to the new resource
        /// - The returned object includes the generated ID for subsequent operations
        ///
        /// **Business Rules:**
        /// - The `Code` field must be unique across all ratio definitions
        /// - `NumeratorType` and `DenominatorType` can be "FinancialAccount", "UnitAccount", or "Constant"
        /// - When type is "FinancialAccount", the corresponding `AccountId` must be provided
        /// - When type is "UnitAccount", the corresponding `UnitAccountId` must be provided
        /// - When type is "Constant", the corresponding `ConstantValue` must be provided
        /// - `ResultFormat` controls display formatting (e.g., "Decimal", "Percentage")
        /// - `FormatPrecision` defaults to 2 decimal places
        /// - New ratio definitions are created as active by default
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="dto">The ratio definition configuration including code, name, numerator/denominator types, and formatting options.</param>
        /// <returns>The newly created ratio definition with its generated ID and audit fields.</returns>
        /// <response code="201">Ratio definition created successfully. Returns the created resource.</response>
        /// <response code="400">Invalid request data, such as duplicate code or missing required fields.</response>
        /// <response code="500">Internal server error occurred while creating the ratio definition.</response>
        [HttpPost]
        public async Task<ActionResult<RatioDefinitionDto>> CreateRatioDefinition([FromBody] CreateRatioDefinitionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var ratio = await _ratioDefinitionService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetRatioDefinitionById), new { id = ratio.Id }, ratio);
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
        /// Updates an existing ratio definition by its unique identifier.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Changing the name or description of an existing ratio definition
        /// - Correcting metadata on a previously created ratio
        /// - Updating documentation or display labels for dashboard ratios
        ///
        /// **Integration Pattern:**
        /// - First retrieve the current definition with `GET /{id}` to display in an edit form
        /// - Submit only the fields that need updating; null fields are typically ignored
        /// - The response returns the full updated ratio definition
        ///
        /// **Business Rules:**
        /// - The ratio definition must exist; returns 404 if the ID is not found
        /// - Only `Name` and `Description` fields can be updated via this endpoint
        /// - To change the active status, use the dedicated `activate` or `deactivate` endpoints
        /// - Audit fields (UpdatedAt, UpdatedBy) are automatically set on successful update
        /// - Updating a ratio definition does not affect previously calculated results
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to update.</param>
        /// <param name="dto">The fields to update on the ratio definition (Name, Description).</param>
        /// <returns>The updated ratio definition reflecting the applied changes.</returns>
        /// <response code="200">Ratio definition updated successfully. Returns the updated resource.</response>
        /// <response code="400">Invalid request data or model validation failed.</response>
        /// <response code="404">No ratio definition was found with the specified ID.</response>
        /// <response code="500">Internal server error occurred while updating the ratio definition.</response>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RatioDefinitionDto>> UpdateRatioDefinition(Guid id, [FromBody] UpdateRatioDefinitionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var ratio = await _ratioDefinitionService.UpdateAsync(id, dto);
                return Ok(ratio);
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
        /// Permanently deletes a ratio definition from the system.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing ratio definitions that are no longer needed
        /// - Cleaning up test or incorrectly configured ratio definitions
        /// - Removing deprecated KPI metrics from the system
        ///
        /// **Integration Pattern:**
        /// - This is a destructive operation; consider using `PATCH /{id}/deactivate` instead for soft removal
        /// - On success, returns 204 No Content with an empty response body
        /// - After deletion, the ratio ID can no longer be used for calculations or lookups
        ///
        /// **Business Rules:**
        /// - The ratio definition must exist; returns 404 if the ID is not found
        /// - Deletion is permanent and cannot be undone
        /// - Consider deactivating the ratio instead of deleting if historical calculations reference it
        /// - Any previously calculated results may become orphaned after deletion
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to delete.</param>
        /// <returns>No content on successful deletion.</returns>
        /// <response code="204">Ratio definition deleted successfully.</response>
        /// <response code="404">No ratio definition was found with the specified ID.</response>
        /// <response code="500">Internal server error occurred while deleting the ratio definition.</response>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteRatioDefinition(Guid id)
        {
            try
            {
                await _ratioDefinitionService.DeleteAsync(id);
                return NoContent();
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
        /// Activates a ratio definition, making it eligible for calculations.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Re-enabling a previously deactivated ratio definition
        /// - Making a newly configured ratio available for calculation workflows
        /// - Restoring a ratio that was temporarily disabled during maintenance
        ///
        /// **Integration Pattern:**
        /// - This is an idempotent operation; activating an already active ratio has no adverse effect
        /// - On success, returns 204 No Content with an empty response body
        /// - After activation, the ratio will appear in results from `GET /active` and `GET /calculate-all`
        ///
        /// **Business Rules:**
        /// - The ratio definition must exist; returns 404 if the ID is not found
        /// - Sets the `IsActive` flag to true
        /// - Activated ratios are included in bulk calculation operations (`calculate-all`)
        /// - Activation does not trigger any automatic recalculation
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to activate.</param>
        /// <returns>No content on successful activation.</returns>
        /// <response code="204">Ratio definition activated successfully.</response>
        /// <response code="404">No ratio definition was found with the specified ID.</response>
        /// <response code="500">Internal server error occurred while activating the ratio definition.</response>
        [HttpPatch("{id:guid}/activate")]
        public async Task<ActionResult> ActivateRatioDefinition(Guid id)
        {
            try
            {
                await _ratioDefinitionService.ActivateAsync(id);
                return NoContent();
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
        /// Deactivates a ratio definition, excluding it from calculations.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Temporarily disabling a ratio without permanently deleting it
        /// - Removing a ratio from active calculation workflows during review or maintenance
        /// - Soft-removing deprecated KPI metrics while preserving their configuration
        ///
        /// **Integration Pattern:**
        /// - This is an idempotent operation; deactivating an already inactive ratio has no adverse effect
        /// - On success, returns 204 No Content with an empty response body
        /// - After deactivation, the ratio will no longer appear in results from `GET /active` or `GET /calculate-all`
        /// - Use `PATCH /{id}/activate` to reverse this operation
        ///
        /// **Business Rules:**
        /// - The ratio definition must exist; returns 404 if the ID is not found
        /// - Sets the `IsActive` flag to false
        /// - Deactivated ratios are excluded from bulk calculation operations (`calculate-all`)
        /// - Individual calculations via `GET /{id}/calculate` may still work on deactivated ratios
        /// - Previously calculated results are not affected by deactivation
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to deactivate.</param>
        /// <returns>No content on successful deactivation.</returns>
        /// <response code="204">Ratio definition deactivated successfully.</response>
        /// <response code="404">No ratio definition was found with the specified ID.</response>
        /// <response code="500">Internal server error occurred while deactivating the ratio definition.</response>
        [HttpPatch("{id:guid}/deactivate")]
        public async Task<ActionResult> DeactivateRatioDefinition(Guid id)
        {
            try
            {
                await _ratioDefinitionService.DeactivateAsync(id);
                return NoContent();
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

        // ===== CALCULATION ENDPOINTS =====

        /// <summary>
        /// Calculates a ratio for a specific fiscal period.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Computing a specific KPI (e.g., Current Ratio) for a given accounting period
        /// - Generating point-in-time ratio values for monthly or quarterly reports
        /// - Validating ratio configuration by running a test calculation against a known period
        ///
        /// **Integration Pattern:**
        /// - Pass the ratio definition ID as a path parameter and the fiscal period ID as a query parameter
        /// - The result includes both raw values (numerator, denominator, result) and a formatted string
        /// - Use alongside `GET /{id}/trend` to provide both current value and historical context
        ///
        /// **Business Rules:**
        /// - The ratio definition must exist; returns 404 if the ID is not found
        /// - The fiscal period must be valid and accessible
        /// - If the denominator evaluates to zero, the calculation may return an error or special value
        /// - The result is formatted according to the ratio's `ResultFormat` and `FormatPrecision` settings
        /// - Calculations use the account balances as of the specified fiscal period
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to calculate.</param>
        /// <param name="fiscalPeriodId">The fiscal period ID to calculate the ratio for (passed as a query parameter).</param>
        /// <returns>The calculation result including numerator, denominator, raw result, and formatted value.</returns>
        /// <response code="200">Returns the calculation result with numerator, denominator, and formatted value.</response>
        /// <response code="404">Ratio definition or fiscal period not found.</response>
        /// <response code="500">Internal server error occurred during ratio calculation.</response>
        [HttpGet("{id:guid}/calculate")]
        public async Task<ActionResult<RatioCalculationResultDto>> CalculateRatio(Guid id, [FromQuery] Guid fiscalPeriodId)
        {
            try
            {
                var result = await _ratioDefinitionService.CalculateAsync(id, fiscalPeriodId);
                return Ok(result);
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
        /// Calculates a ratio for a custom date range.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Computing ratios for ad-hoc date ranges that do not align with fiscal periods
        /// - Generating ratio values for custom reporting windows (e.g., last 90 days)
        /// - Calculating ratios across partial periods or multi-period spans
        ///
        /// **Integration Pattern:**
        /// - Pass the ratio definition ID as a path parameter and start/end dates as query parameters
        /// - Dates should be provided in ISO 8601 format (e.g., `2025-01-01`)
        /// - The result structure is the same as the fiscal period calculation endpoint
        ///
        /// **Business Rules:**
        /// - The ratio definition must exist; returns 404 if the ID is not found
        /// - `startDate` must be earlier than or equal to `endDate`
        /// - Account balances are aggregated across all transactions within the specified date range
        /// - The result is formatted according to the ratio's `ResultFormat` and `FormatPrecision` settings
        /// - If the denominator evaluates to zero, the calculation may return an error or special value
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to calculate.</param>
        /// <param name="startDate">The start date of the calculation range (inclusive), in ISO 8601 format.</param>
        /// <param name="endDate">The end date of the calculation range (inclusive), in ISO 8601 format.</param>
        /// <returns>The calculation result including numerator, denominator, raw result, and formatted value for the date range.</returns>
        /// <response code="200">Returns the calculation result for the specified date range.</response>
        /// <response code="404">Ratio definition not found.</response>
        /// <response code="500">Internal server error occurred during ratio calculation.</response>
        [HttpGet("{id:guid}/calculate-range")]
        public async Task<ActionResult<RatioCalculationResultDto>> CalculateRatioForRange(
            Guid id,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var result = await _ratioDefinitionService.CalculateForDateRangeAsync(id, startDate, endDate);
                return Ok(result);
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
        /// Calculates all active ratios for a given fiscal period.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Generating a complete KPI dashboard for a specific accounting period
        /// - Running end-of-period ratio calculations for all active financial metrics
        /// - Producing a comprehensive ratio report for management review
        ///
        /// **Integration Pattern:**
        /// - Pass the fiscal period ID as a query parameter to calculate all active ratios at once
        /// - Returns a list of results, one per active ratio definition
        /// - More efficient than calling `GET /{id}/calculate` individually for each ratio
        /// - Combine with `GET /active` to display ratio names alongside their calculated values
        ///
        /// **Business Rules:**
        /// - Only active ratio definitions are included in the calculation batch
        /// - Inactive ratios (deactivated via `PATCH /{id}/deactivate`) are excluded
        /// - If a specific ratio calculation fails (e.g., division by zero), it may be omitted or flagged in results
        /// - Each result includes the ratio code and name for identification
        /// - All results are calculated against the same fiscal period for consistency
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="fiscalPeriodId">The fiscal period ID to calculate all active ratios for (passed as a query parameter).</param>
        /// <returns>List of calculation results for all active ratio definitions in the specified fiscal period.</returns>
        /// <response code="200">Returns the list of calculation results for all active ratios.</response>
        /// <response code="500">Internal server error occurred during bulk ratio calculation.</response>
        [HttpGet("calculate-all")]
        public async Task<ActionResult<List<RatioCalculationResultDto>>> CalculateAllRatios([FromQuery] Guid fiscalPeriodId)
        {
            try
            {
                var results = await _ratioDefinitionService.CalculateAllAsync(fiscalPeriodId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves trend data for a ratio across all periods in a fiscal year.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Displaying a line chart showing how a ratio has changed over the course of a fiscal year
        /// - Analyzing period-over-period trends for KPI monitoring and variance analysis
        /// - Providing historical context alongside current ratio values on dashboards
        ///
        /// **Integration Pattern:**
        /// - Pass the ratio definition ID as a path parameter and the fiscal year ID as a query parameter
        /// - Returns an ordered list of data points, one per fiscal period within the year
        /// - Each data point includes the period name and calculated value for charting
        /// - Combine with `GET /{id}/calculate` to show current value with trend context
        ///
        /// **Business Rules:**
        /// - The ratio definition must exist; returns 404 if the ID is not found
        /// - The fiscal year must be valid and contain defined fiscal periods
        /// - Data points are returned in chronological order by period
        /// - Periods where calculation is not possible (e.g., no data) may be omitted or show zero
        /// - The trend includes all periods within the fiscal year, including future open periods if applicable
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the ratio definition to get trend data for.</param>
        /// <param name="fiscalYearId">The fiscal year ID to retrieve trend data across (passed as a query parameter).</param>
        /// <returns>Trend data containing a list of period-by-period data points for the specified ratio and fiscal year.</returns>
        /// <response code="200">Returns the trend data with period-by-period data points.</response>
        /// <response code="404">Ratio definition or fiscal year not found.</response>
        /// <response code="500">Internal server error occurred while retrieving trend data.</response>
        [HttpGet("{id:guid}/trend")]
        public async Task<ActionResult<RatioTrendResultDto>> GetRatioTrend(Guid id, [FromQuery] Guid fiscalYearId)
        {
            try
            {
                var result = await _ratioDefinitionService.GetTrendAsync(id, fiscalYearId);
                return Ok(result);
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
    }
}
