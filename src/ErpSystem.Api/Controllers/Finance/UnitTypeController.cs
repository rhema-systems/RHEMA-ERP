using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Unit Types.
    /// Unit Types define the measurement units for non-financial quantities 
    /// (e.g., Employees, Square Feet, Hours).
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/finance/unit-types")]
    public class UnitTypeController : ControllerBase
    {
        private readonly IUnitTypeService _unitTypeService;

        public UnitTypeController(IUnitTypeService unitTypeService)
        {
            _unitTypeService = unitTypeService;
        }

        /// <summary>
        /// Retrieves all unit types.
        /// </summary>
        /// <returns>List of all unit types</returns>
        /// <response code="200">Returns the list of unit types</response>
        [HttpGet]
        public async Task<ActionResult<List<UnitTypeDto>>> GetUnitTypes()
        {
            try
            {
                var unitTypes = await _unitTypeService.GetAllAsync();
                return Ok(unitTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves only active unit types.
        /// </summary>
        /// <returns>List of active unit types</returns>
        /// <response code="200">Returns the list of active unit types</response>
        [HttpGet("active")]
        public async Task<ActionResult<List<UnitTypeDto>>> GetActiveUnitTypes()
        {
            try
            {
                var unitTypes = await _unitTypeService.GetActiveAsync();
                return Ok(unitTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific unit type by ID.
        /// </summary>
        /// <param name="id">Unit type ID</param>
        /// <returns>Unit type details</returns>
        /// <response code="200">Returns the unit type</response>
        /// <response code="404">Unit type not found</response>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UnitTypeDto>> GetUnitTypeById(Guid id)
        {
            try
            {
                var unitType = await _unitTypeService.GetByIdAsync(id);
                if (unitType == null)
                    return NotFound($"Unit type with ID {id} not found");

                return Ok(unitType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a unit type by its code.
        /// </summary>
        /// <param name="code">Unit type code (e.g., "EMP", "SQFT")</param>
        /// <returns>Unit type details</returns>
        /// <response code="200">Returns the unit type</response>
        /// <response code="404">Unit type not found</response>
        [HttpGet("code/{code}")]
        public async Task<ActionResult<UnitTypeDto>> GetUnitTypeByCode(string code)
        {
            try
            {
                var unitType = await _unitTypeService.GetByCodeAsync(code);
                if (unitType == null)
                    return NotFound($"Unit type with code {code} not found");

                return Ok(unitType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new unit type.
        /// </summary>
        /// <param name="dto">Unit type creation details</param>
        /// <returns>Created unit type</returns>
        /// <response code="201">Unit type created successfully</response>
        /// <response code="400">Invalid data or duplicate code</response>
        [HttpPost]
        public async Task<ActionResult<UnitTypeDto>> CreateUnitType([FromBody] CreateUnitTypeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var unitType = await _unitTypeService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetUnitTypeById), new { id = unitType.Id }, unitType);
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
        /// Updates an existing unit type.
        /// </summary>
        /// <param name="id">Unit type ID</param>
        /// <param name="dto">Updated unit type properties</param>
        /// <returns>Updated unit type</returns>
        /// <response code="200">Unit type updated successfully</response>
        /// <response code="404">Unit type not found</response>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UnitTypeDto>> UpdateUnitType(Guid id, [FromBody] UpdateUnitTypeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var unitType = await _unitTypeService.UpdateAsync(id, dto);
                return Ok(unitType);
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
        /// Deletes a unit type.
        /// Cannot delete if unit accounts exist for this type.
        /// </summary>
        /// <param name="id">Unit type ID</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit type deleted successfully</response>
        /// <response code="400">Cannot delete - has associated accounts</response>
        /// <response code="404">Unit type not found</response>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> DeleteUnitType(Guid id)
        {
            try
            {
                await _unitTypeService.DeleteAsync(id);
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
        /// Activates a unit type.
        /// </summary>
        /// <param name="id">Unit type ID</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit type activated</response>
        /// <response code="404">Unit type not found</response>
        [HttpPatch("{id:guid}/activate")]
        public async Task<ActionResult> ActivateUnitType(Guid id)
        {
            try
            {
                await _unitTypeService.ActivateAsync(id);
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
        /// Deactivates a unit type.
        /// Cannot deactivate if active unit accounts exist for this type.
        /// </summary>
        /// <param name="id">Unit type ID</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit type deactivated</response>
        /// <response code="400">Cannot deactivate - has active accounts</response>
        /// <response code="404">Unit type not found</response>
        [HttpPatch("{id:guid}/deactivate")]
        public async Task<ActionResult> DeactivateUnitType(Guid id)
        {
            try
            {
                await _unitTypeService.DeactivateAsync(id);
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
