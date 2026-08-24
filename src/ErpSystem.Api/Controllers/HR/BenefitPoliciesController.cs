using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/benefit-policies")]
[Authorize(Policy = "InternalOnly")]
public class BenefitPoliciesController : ControllerBase
{
    private readonly IBenefitPolicyService _benefitPolicyService;
    private readonly ILogger<BenefitPoliciesController> _logger;

    public BenefitPoliciesController(IBenefitPolicyService benefitPolicyService, ILogger<BenefitPoliciesController> logger)
    {
        _benefitPolicyService = benefitPolicyService ?? throw new ArgumentNullException(nameof(benefitPolicyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all benefit policies.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BenefitPolicyDto>>> GetAllAsync()
    {
        var results = await _benefitPolicyService.GetAllAsync();
        return Ok(results);
    }

    /// <summary>
    /// Retrieves benefit policies with pagination.
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<BenefitPolicyListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BenefitPolicyListDto>>> GetPagedAsync(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var results = await _benefitPolicyService.GetPagedAsync(pageNumber, pageSize);
            return Ok(results);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves a benefit policy by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BenefitPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BenefitPolicyDto>> GetByIdAsync([FromRoute] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Id is required." });
        }

        var result = await _benefitPolicyService.GetByIdAsync(id);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves all active benefit policies.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BenefitPolicyDto>>> GetActiveAsync()
    {
        var results = await _benefitPolicyService.GetAllActiveAsync();
        return Ok(results);
    }

    /// <summary>
    /// Retrieves benefit policies by policy type.
    /// </summary>
    [HttpGet("type/{policyType}")]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BenefitPolicyDto>>> GetByPolicyTypeAsync(
        [FromRoute] BenefitPolicyType policyType)
    {
        var results = await _benefitPolicyService.GetByPolicyTypeAsync(policyType);
        return Ok(results);
    }

    /// <summary>
    /// Retrieves benefit policies effective as of a given date.
    /// </summary>
    [HttpGet("effective")]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitPolicyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BenefitPolicyDto>>> GetEffectiveAsync(
        [FromQuery] DateTime asOfDate)
    {
        if (asOfDate == default)
        {
            return BadRequest(new { message = "asOfDate is required." });
        }

        var results = await _benefitPolicyService.GetEffectivePoliciesAsync(asOfDate);
        return Ok(results);
    }

    /// <summary>
    /// Creates a new benefit policy.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BenefitPolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BenefitPolicyDto>> CreateAsync([FromBody] CreateBenefitPolicyDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _benefitPolicyService.CreateAsync(dto);
            return Created($"/api/hr/benefit-policies/{result.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating benefit policy");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the benefit policy");
        }
    }

    /// <summary>
    /// Updates an existing benefit policy.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BenefitPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BenefitPolicyDto>> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateBenefitPolicyDto dto)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Id is required." });
        }

        if (dto == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (dto.Id != Guid.Empty && dto.Id != id)
        {
            return BadRequest(new { message = "ID mismatch." });
        }

        dto.Id = id;

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _benefitPolicyService.UpdateAsync(id, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = ex.Message });
            }

            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating benefit policy with Id {BenefitPolicyId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the benefit policy");
        }
    }

    /// <summary>
    /// Deactivates (soft deactivation) a benefit policy.
    /// </summary>
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateAsync([FromRoute] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Id is required." });
        }

        var deactivated = await _benefitPolicyService.DeactivateAsync(id);
        if (!deactivated)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Returns the enum option sets used to drive benefit-policy configuration UIs.
    /// </summary>
    [HttpGet("lookups")]
    [ProducesResponseType(typeof(BenefitPolicyLookupsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BenefitPolicyLookupsDto>> GetLookupsAsync()
    {
        var lookups = await _benefitPolicyService.GetLookupsAsync();
        return Ok(lookups);
    }

    /// <summary>
    /// Gets the per-grade value rows for a policy.
    /// </summary>
    [HttpGet("{id:guid}/grade-values")]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitGradeValueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BenefitGradeValueDto>>> GetGradeValuesAsync([FromRoute] Guid id)
    {
        var results = await _benefitPolicyService.GetGradeValuesAsync(id);
        return Ok(results);
    }

    /// <summary>
    /// Adds a per-grade value row to a policy.
    /// </summary>
    [HttpPost("{id:guid}/grade-values")]
    [ProducesResponseType(typeof(BenefitGradeValueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BenefitGradeValueDto>> AddGradeValueAsync([FromRoute] Guid id, [FromBody] CreateBenefitGradeValueDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _benefitPolicyService.AddGradeValueAsync(id, dto);
            return Created($"/api/hr/benefit-policies/{id}/grade-values", result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates a per-grade value row on a policy.
    /// </summary>
    [HttpPut("{id:guid}/grade-values/{gradeValueId:guid}")]
    [ProducesResponseType(typeof(BenefitGradeValueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BenefitGradeValueDto>> UpdateGradeValueAsync(
        [FromRoute] Guid id,
        [FromRoute] Guid gradeValueId,
        [FromBody] UpdateBenefitGradeValueDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _benefitPolicyService.UpdateGradeValueAsync(id, gradeValueId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a per-grade value row from a policy.
    /// </summary>
    [HttpDelete("{id:guid}/grade-values/{gradeValueId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGradeValueAsync([FromRoute] Guid id, [FromRoute] Guid gradeValueId)
    {
        var deleted = await _benefitPolicyService.DeleteGradeValueAsync(id, gradeValueId);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes (soft delete) a benefit policy.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new { message = "Id is required." });
        }

        try
        {
            var deleted = await _benefitPolicyService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
