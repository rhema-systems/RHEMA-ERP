using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// API controller for Allocation Rule operations.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/finance/allocations")]
    public class AllocationController : ControllerBase
    {
        private readonly IAllocationService _allocationService;

        public AllocationController(IAllocationService allocationService)
        {
            _allocationService = allocationService;
        }

        /// <summary>
        /// Get all allocation rules.
        /// </summary>
        [HttpGet("rules")]
        public async Task<ActionResult<IReadOnlyList<AllocationRuleDto>>> GetAllRules()
        {
            var rules = await _allocationService.GetAllRulesAsync();
            return Ok(rules);
        }

        /// <summary>
        /// Get active allocation rules only.
        /// </summary>
        [HttpGet("rules/active")]
        public async Task<ActionResult<IReadOnlyList<AllocationRuleDto>>> GetActiveRules()
        {
            var rules = await _allocationService.GetActiveRulesAsync();
            return Ok(rules);
        }

        /// <summary>
        /// Get a specific allocation rule by ID.
        /// </summary>
        [HttpGet("rules/{id}")]
        public async Task<ActionResult<AllocationRuleDto>> GetRuleById(Guid id)
        {
            var rule = await _allocationService.GetRuleByIdAsync(id);
            if (rule == null)
                return NotFound(new { error = $"Allocation rule with ID '{id}' not found" });
            return Ok(rule);
        }

        /// <summary>
        /// Get a specific allocation rule by code.
        /// </summary>
        [HttpGet("rules/by-code/{code}")]
        public async Task<ActionResult<AllocationRuleDto>> GetRuleByCode(string code)
        {
            var rule = await _allocationService.GetRuleByCodeAsync(code);
            if (rule == null)
                return NotFound(new { error = $"Allocation rule with code '{code}' not found" });
            return Ok(rule);
        }

        /// <summary>
        /// Create a new allocation rule.
        /// </summary>
        [HttpPost("rules")]
        public async Task<ActionResult<AllocationRuleDto>> CreateRule([FromBody] CreateAllocationRuleDto dto)
        {
            try
            {
                var rule = await _allocationService.CreateRuleAsync(dto);
                return CreatedAtAction(nameof(GetRuleById), new { id = rule.Id }, rule);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing allocation rule.
        /// </summary>
        [HttpPut("rules/{id}")]
        public async Task<ActionResult<AllocationRuleDto>> UpdateRule(Guid id, [FromBody] UpdateAllocationRuleDto dto)
        {
            try
            {
                var rule = await _allocationService.UpdateRuleAsync(id, dto);
                return Ok(rule);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Delete an allocation rule.
        /// </summary>
        [HttpDelete("rules/{id}")]
        public async Task<ActionResult> DeleteRule(Guid id)
        {
            await _allocationService.DeleteRuleAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Activate an allocation rule.
        /// </summary>
        [HttpPatch("rules/{id}/activate")]
        public async Task<ActionResult<AllocationRuleDto>> ActivateRule(Guid id)
        {
            try
            {
                var rule = await _allocationService.ActivateRuleAsync(id);
                return Ok(rule);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Deactivate an allocation rule.
        /// </summary>
        [HttpPatch("rules/{id}/deactivate")]
        public async Task<ActionResult<AllocationRuleDto>> DeactivateRule(Guid id)
        {
            try
            {
                var rule = await _allocationService.DeactivateRuleAsync(id);
                return Ok(rule);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Run an allocation.
        /// </summary>
        [HttpPost("rules/{id}/run")]
        public async Task<ActionResult<AllocationResultDto>> RunAllocation(Guid id, [FromBody] RunAllocationDto dto)
        {
            try
            {
                // Ensure the ID in the route matches the DTO
                if (id != dto.AllocationRuleId)
                {
                    dto = dto with { AllocationRuleId = id };
                }
                
                var result = await _allocationService.RunAllocationAsync(dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
