using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Ratio Definitions and performing calculations.
    /// Ratios combine financial accounts, unit accounts, and constants to calculate KPIs.
    /// </summary>
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
        /// Retrieves all ratio definitions.
        /// </summary>
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
        /// Retrieves a specific ratio definition by ID.
        /// </summary>
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
        /// Retrieves a ratio definition by its code.
        /// </summary>
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
        /// Creates a new ratio definition.
        /// </summary>
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
        /// Updates an existing ratio definition.
        /// </summary>
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
        /// Deletes a ratio definition.
        /// </summary>
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
        /// Activates a ratio definition.
        /// </summary>
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
        /// Deactivates a ratio definition.
        /// </summary>
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
        /// Calculates a ratio for a date range.
        /// </summary>
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
        /// Calculates all active ratios for a fiscal period.
        /// </summary>
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
        /// Gets the trend data for a ratio across a fiscal year.
        /// </summary>
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
