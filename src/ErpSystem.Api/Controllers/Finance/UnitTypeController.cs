using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing Unit Types.
    /// </summary>
    /// <remarks>
    /// Unit Types define the measurement units for non-financial quantities
    /// (e.g., Employees, Square Feet, Hours).
    /// </remarks>
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
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating dropdown lists for unit type selection in forms
        /// - Loading all unit types for administrative management screens
        /// - Exporting the complete unit type catalog for reporting
        ///
        /// **Integration Pattern:**
        /// - Call this endpoint on page load for admin views that need the full list
        /// - Cache the result client-side and refresh on create/update/delete operations
        /// - Use GET active endpoint instead if you only need selectable unit types
        ///
        /// **Business Rules:**
        /// - Returns both active and inactive unit types
        /// - Results are not paginated; the full list is returned
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of all unit types</returns>
        /// <response code="200">Returns the list of unit types</response>
        /// <response code="500">Internal server error</response>
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
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populating dropdown selectors where only active unit types should be offered
        /// - Filtering available unit types for new unit account creation
        /// - Displaying active measurement categories in operational views
        ///
        /// **Integration Pattern:**
        /// - Prefer this endpoint over GET all when binding to user-facing selection controls
        /// - Cache the result and invalidate when activate/deactivate operations occur
        ///
        /// **Business Rules:**
        /// - Only unit types with an active status are returned
        /// - Inactive unit types are excluded from the result set
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <returns>List of active unit types</returns>
        /// <response code="200">Returns the list of active unit types</response>
        /// <response code="500">Internal server error</response>
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
        /// <remarks>
        /// **Common Use Cases:**
        /// - Loading unit type details for an edit form
        /// - Resolving a unit type reference from another entity (e.g., unit account)
        /// - Viewing full details of a single unit type
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint when you already have the unit type GUID
        /// - Returned as the Location header target after a successful POST creation
        /// - If you have a code instead of an ID, use the GET by code endpoint
        ///
        /// **Business Rules:**
        /// - Returns the unit type regardless of active/inactive status
        /// - Returns 404 if no unit type exists with the given ID
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the unit type to retrieve</param>
        /// <returns>Unit type details</returns>
        /// <response code="200">Returns the unit type</response>
        /// <response code="404">Unit type not found</response>
        /// <response code="500">Internal server error</response>
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
        /// <remarks>
        /// **Common Use Cases:**
        /// - Looking up a unit type using a human-readable code (e.g., "EMP", "SQFT", "HRS")
        /// - Resolving unit type references from imported data that uses codes instead of IDs
        /// - Validating that a specific unit type code exists before processing
        ///
        /// **Integration Pattern:**
        /// - Use this endpoint when you have the short code rather than the GUID
        /// - Useful for integrations where external systems reference unit types by code
        /// - Code matching is typically case-sensitive
        ///
        /// **Business Rules:**
        /// - Returns the unit type regardless of active/inactive status
        /// - Each unit type code must be unique across the system
        /// - Returns 404 if no unit type exists with the given code
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="code">The unique short code of the unit type (e.g., "EMP", "SQFT", "HRS")</param>
        /// <returns>Unit type details</returns>
        /// <response code="200">Returns the unit type</response>
        /// <response code="404">Unit type not found</response>
        /// <response code="500">Internal server error</response>
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
        /// <remarks>
        /// **Common Use Cases:**
        /// - Adding a new measurement category (e.g., Employees, Square Feet, Hours)
        /// - Extending the system with custom unit types for specific business needs
        /// - Setting up unit types during initial system configuration
        ///
        /// **Integration Pattern:**
        /// - Send a POST request with a JSON body containing the unit type details
        /// - On success, the response includes a Location header pointing to the new resource
        /// - The created unit type is returned in the response body with its assigned ID
        ///
        /// **Business Rules:**
        /// - The unit type code must be unique across all unit types
        /// - Duplicate codes will result in a 400 Bad Request response
        /// - Model validation is enforced; invalid payloads return 400 with validation details
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="dto">The unit type creation payload containing name, code, and other details</param>
        /// <returns>The newly created unit type with its assigned ID</returns>
        /// <response code="201">Unit type created successfully</response>
        /// <response code="400">Invalid data or duplicate code</response>
        /// <response code="500">Internal server error</response>
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
        /// <remarks>
        /// **Common Use Cases:**
        /// - Correcting the name or description of an existing unit type
        /// - Updating unit type properties from an administrative edit form
        /// - Modifying configuration details of a measurement category
        ///
        /// **Integration Pattern:**
        /// - Send a PUT request with the unit type ID in the route and updated fields in the body
        /// - The full updated unit type is returned in the response body
        /// - Refresh any cached unit type lists after a successful update
        ///
        /// **Business Rules:**
        /// - The unit type must exist; otherwise a 404 is returned
        /// - Model validation is enforced; invalid payloads return 400 with validation details
        /// - Changes apply immediately to any referencing entities
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the unit type to update</param>
        /// <param name="dto">The updated unit type properties</param>
        /// <returns>The updated unit type</returns>
        /// <response code="200">Unit type updated successfully</response>
        /// <response code="400">Invalid data provided</response>
        /// <response code="404">Unit type not found</response>
        /// <response code="500">Internal server error</response>
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
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Removing a unit type that was created in error
        /// - Cleaning up unused measurement categories during system maintenance
        /// - Deleting test or temporary unit types from the system
        ///
        /// **Integration Pattern:**
        /// - Send a DELETE request with the unit type ID in the route
        /// - On success, returns 204 No Content with an empty body
        /// - Invalidate any cached unit type lists after a successful deletion
        ///
        /// **Business Rules:**
        /// - Cannot delete a unit type if unit accounts exist for this type; returns 400
        /// - The unit type must exist; otherwise a 404 is returned
        /// - Deletion is permanent and cannot be undone; consider deactivating instead
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the unit type to delete</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit type deleted successfully</response>
        /// <response code="400">Cannot delete - unit type has associated unit accounts</response>
        /// <response code="404">Unit type not found</response>
        /// <response code="500">Internal server error</response>
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
        /// <remarks>
        /// **Common Use Cases:**
        /// - Re-enabling a previously deactivated unit type
        /// - Making a unit type available again for selection in forms and dropdowns
        /// - Restoring a unit type after a temporary suspension
        ///
        /// **Integration Pattern:**
        /// - Send a PATCH request with the unit type ID in the route
        /// - On success, returns 204 No Content with an empty body
        /// - Refresh cached active unit type lists after activation
        ///
        /// **Business Rules:**
        /// - The unit type must exist; otherwise a 404 is returned
        /// - Activating an already active unit type is idempotent
        /// - Once activated, the unit type will appear in active-only queries
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the unit type to activate</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit type activated successfully</response>
        /// <response code="404">Unit type not found</response>
        /// <response code="500">Internal server error</response>
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
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Temporarily disabling a unit type without permanently deleting it
        /// - Phasing out a measurement category that is no longer needed
        /// - Preventing new unit accounts from being created under this type
        ///
        /// **Integration Pattern:**
        /// - Send a PATCH request with the unit type ID in the route
        /// - On success, returns 204 No Content with an empty body
        /// - Refresh cached active unit type lists after deactivation
        ///
        /// **Business Rules:**
        /// - Cannot deactivate if active unit accounts exist for this type; returns 400
        /// - The unit type must exist; otherwise a 404 is returned
        /// - Deactivated unit types will no longer appear in active-only queries
        /// - Prefer deactivation over deletion to preserve historical data integrity
        ///
        /// **Authorization:** Requires authenticated user
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the unit type to deactivate</param>
        /// <returns>No content on success</returns>
        /// <response code="204">Unit type deactivated successfully</response>
        /// <response code="400">Cannot deactivate - unit type has active unit accounts</response>
        /// <response code="404">Unit type not found</response>
        /// <response code="500">Internal server error</response>
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
