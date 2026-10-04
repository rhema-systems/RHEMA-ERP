using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using ErpSystem.Core.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Controller for managing segment structures in a segmented Chart of Accounts.
    /// </summary>
    /// <remarks>
    /// This controller is only relevant when the COA type is set to "Segmented".
    /// Segments define how account numbers are constructed and validated.
    /// </remarks>
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    [ApiController]
    [Route("api/finance/segments")]
    public class SegmentStructureController : ControllerBase
    {
        private readonly ISegmentStructureService _segmentStructureService;
        private readonly IAccountSegmentStructureService _accountSegmentStructureService;
        private readonly ISegmentLookupValueService _segmentLookupValueService;

        public SegmentStructureController(
            ISegmentStructureService segmentStructureService,
            IAccountSegmentStructureService accountSegmentStructureService,
            ISegmentLookupValueService segmentLookupValueService)
        {
            _segmentStructureService = segmentStructureService;
            _accountSegmentStructureService = accountSegmentStructureService;
            _segmentLookupValueService = segmentLookupValueService;
        }

        #region Segment Structures

        /// <summary>
        /// Retrieves all segment structures for segmented Chart of Accounts.
        /// </summary>
        /// <remarks>
        /// Only relevant when COA type is "Segmented".
        ///
        /// **Common Use Cases:**
        /// - Get segment definitions for account number construction
        /// - Display segment structure to users
        /// - Validate account number format
        ///
        /// **Integration Pattern:**
        /// - Check Finance settings to see if COA is Segmented
        /// - If segmented, call this to get segment structure
        /// - Use segments to construct/validate account numbers
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <returns>List of segment structures ordered by position.</returns>
        /// <response code="200">Returns the list of segment structures.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet]
        public async Task<ActionResult<List<SegmentStructureDto>>> GetSegmentStructures()
        {
            try
            {
                var segments = await _accountSegmentStructureService.GetAllAsync();
                return Ok(segments.Select(MapToSegmentStructureDto).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all segment structures configured as reporting dimensions.
        /// </summary>
        /// <remarks>
        /// Returns only the segments where IsReportingDimension is true, including their lookup values.
        /// Useful for building dynamic filter UIs in financial reports.
        /// </remarks>
        /// <returns>List of reporting dimension segment structures.</returns>
        [HttpGet("reporting-dimensions")]
        public async Task<ActionResult<List<SegmentStructureDto>>> GetReportingDimensions(CancellationToken cancellationToken)
        {
            try
            {
                var segments = await _accountSegmentStructureService.GetAllAsync(cancellationToken);
                var dimensions = segments.Where(s => s.IsReportingDimension && s.IsActive)
                    .Select(MapToSegmentStructureDto).ToList();
                return Ok(dimensions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves valid account-backed choices for a financial-report dimension.
        /// </summary>
        [HttpGet("{id}/reporting-options")]
        public async Task<ActionResult<ReportingSegmentOptionsDto>> GetReportingOptions(
            Guid id,
            [FromQuery] string? search = null,
            [FromQuery] int take = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var options = await _segmentStructureService.GetReportingOptionsAsync(
                    id,
                    search,
                    take,
                    cancellationToken);
                return Ok(options);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a segment structure by its unique identifier.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Fetch details of a specific segment for editing or display
        /// - Verify segment configuration before assigning values
        /// - Inspect segment properties such as length, data type, and separator
        ///
        /// **Integration Pattern:**
        /// - Use the segment ID obtained from the list endpoint (GET /api/finance/segments)
        /// - Retrieve the segment details to render an edit form or inspect configuration
        ///
        /// **Business Rules:**
        /// - Returns 404 if no segment exists with the given ID
        /// - Inactive segments are still returned; check the IsActive flag
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="id">The unique identifier of the segment structure.</param>
        /// <returns>The segment structure with the specified ID.</returns>
        /// <response code="200">Returns the segment structure.</response>
        /// <response code="404">Segment structure not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("{id}")]
        public async Task<ActionResult<SegmentStructureDto>> GetSegmentStructureById(Guid id)
        {
            try
            {
                var segment = await _accountSegmentStructureService.GetByIdAsync(id);
                if (segment == null)
                    return NotFound($"Segment structure with ID {id} not found");

                return Ok(MapToSegmentStructureDto(segment));
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
        /// Creates a new segment structure.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Define a new segment (e.g., Department, Location, Cost Center) for the Chart of Accounts
        /// - Extend the account number structure with additional dimensions
        /// - Set up the initial segmented COA layout
        ///
        /// **Integration Pattern:**
        /// - POST the segment definition with name, code, position, length, and data type
        /// - The segment is created with IsActive defaulting to true
        /// - Use the returned ID to add lookup values via POST /api/finance/segments/{id}/values
        ///
        /// **Business Rules:**
        /// - Segment code and position must be unique across all segments
        /// - Segment length determines the maximum characters for values in that segment
        /// - IsNaturalAccount can only be true for one segment (the primary account segment)
        /// - If LookupTableRequired is true, values must be pre-defined before use
        /// - Adding a segment may require regeneration of existing account numbers
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="dto">The segment structure data to create.</param>
        /// <returns>The created segment structure.</returns>
        /// <response code="201">Segment structure created successfully.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult<SegmentStructureDto>> CreateSegmentStructure([FromBody] SegmentStructureCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var accountSegmentDto = new AccountSegmentStructureCreateDto
                {
                    SegmentName = dto.SegmentName,
                    SegmentCode = dto.SegmentCode,
                    SegmentPosition = dto.SegmentPosition,
                    SegmentLength = dto.SegmentLength,
                    DataType = dto.DataType,
                    SeparatorCharacter = dto.SeparatorCharacter,
                    LookupTableRequired = dto.LookupTableRequired,
                    IsNaturalAccount = dto.IsNaturalAccount,
                    Description = dto.Description
                };

                var segment = await _accountSegmentStructureService.CreateAsync(accountSegmentDto);
                var resultDto = MapToSegmentStructureDto(segment);

                return CreatedAtAction(nameof(GetSegmentStructureById), new { id = segment.Id }, resultDto);
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
        /// Updates an existing segment structure.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Rename a segment (e.g., rename "Dept" to "Department")
        /// - Update the description for clarity
        /// - Clarify the name or description of an unused Draft identity segment
        ///
        /// **Integration Pattern:**
        /// - Fetch the current segment via GET /api/finance/segments/{id}
        /// - Modify allowed Draft fields and return the original row version
        /// - PUT the updated payload; the route ID must match the dto.Id
        ///
        /// **Business Rules:**
        /// - Structural fields (Code, Position, Length, DataType, Separator) are preserved and cannot be changed via update
        /// - The route parameter ID must match the ID in the request body; otherwise a 400 is returned
        /// - Null fields in the request body default to the existing values (partial update semantics)
        /// - Activation and freezing use their dedicated governed endpoints
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="id">The unique identifier of the segment structure to update.</param>
        /// <param name="dto">The updated segment structure data.</param>
        /// <returns>The updated segment structure.</returns>
        /// <response code="200">Segment structure updated successfully.</response>
        /// <response code="400">Invalid request data or ID mismatch.</response>
        /// <response code="404">Segment structure not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPut("{id}")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult<SegmentStructureDto>> UpdateSegmentStructure(Guid id, [FromBody] SegmentStructureUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (id != dto.Id)
                return BadRequest("ID mismatch");

            try
            {
                // Fetch existing to preserve non-updatable fields (Code, Position, Length, etc.)
                var existing = await _accountSegmentStructureService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound($"Segment with ID {id} not found.");

                var accountSegmentUpdateDto = new AccountSegmentStructureUpdateDto
                {
                    Id = id,
                    // Mapped fields
                    SegmentName = dto.SegmentName ?? existing.SegmentName,
                    Description = dto.Description ?? existing.Description,
                    RowVersion = dto.RowVersion,

                    // Preserved fields
                    SegmentCode = existing.SegmentCode,
                    SegmentPosition = existing.SegmentPosition,
                    SegmentLength = existing.SegmentLength,
                    DataType = existing.DataType,
                    SeparatorCharacter = existing.SeparatorCharacter,
                    LookupTableRequired = existing.LookupTableRequired,
                    IsNaturalAccount = existing.IsNaturalAccount
                };

                var segment = await _accountSegmentStructureService.UpdateAsync(accountSegmentUpdateDto);
                var resultDto = MapToSegmentStructureDto(segment);

                return Ok(resultDto);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "The segment changed; refresh and retry." });
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

        [HttpPost("{id}/activate")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult<SegmentStructureDto>> Activate(Guid id, [FromBody] AccountSegmentLifecycleTransitionDto dto)
        {
            try { return Ok(MapToSegmentStructureDto(await _accountSegmentStructureService.ActivateAsync(id, dto))); }
            catch (DbUpdateConcurrencyException) { return Conflict(new { message = "The segment changed; refresh and retry." }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{id}/freeze")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult<SegmentStructureDto>> Freeze(Guid id, [FromBody] AccountSegmentLifecycleTransitionDto dto)
        {
            try { return Ok(MapToSegmentStructureDto(await _accountSegmentStructureService.FreezeAsync(id, dto))); }
            catch (DbUpdateConcurrencyException) { return Conflict(new { message = "The segment changed; refresh and retry." }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Deletes a segment structure if not in use.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Remove an unused segment from the Chart of Accounts structure
        /// - Clean up segments created during initial setup that are no longer needed
        ///
        /// **Integration Pattern:**
        /// - Verify the segment is not referenced by any accounts before calling delete
        /// - On success (204), refresh the segment list in the UI
        /// - On failure (400), display the error message indicating the segment is in use
        ///
        /// **Business Rules:**
        /// - A segment that is referenced by existing accounts cannot be deleted (returns 400)
        /// - Deletion is permanent and cannot be undone; consider deactivating via PUT instead
        /// - After deletion, remaining segments retain their positions; consider reordering if needed
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="id">The unique identifier of the segment structure to delete.</param>
        /// <returns>No content on successful deletion.</returns>
        /// <response code="204">Segment structure deleted successfully.</response>
        /// <response code="400">Segment is in use and cannot be deleted.</response>
        /// <response code="404">Segment structure not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpDelete("{id}")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult> DeleteSegmentStructure(Guid id, [FromBody] AccountSegmentDeleteDto dto)
        {
            try
            {
                await _accountSegmentStructureService.DeleteAsync(id, dto);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "The segment changed; refresh and retry." });
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
        /// Reorders segments according to the provided list of new positions.
        /// </summary>
        /// <remarks>
        /// **Warning:** Reordering is allowed only while every segment is Draft and no account identity exists.
        ///
        /// **Common Use Cases:**
        /// - Finalize the order of Draft segments during initial setup
        /// - Correct Draft structure ordering before any GL account is assigned
        ///
        /// **Integration Pattern:**
        /// - Fetch current segments via GET /api/finance/segments
        /// - Build a reorder list mapping each segment ID to its new position
        /// - Include the row version returned for every segment
        /// - POST the list and refresh the structure after completion
        ///
        /// **Business Rules:**
        /// - The reorder list must include every configured Draft segment with unique sequential positions
        /// - Reordering is rejected after any segment is activated or any account identity exists
        /// - A stale row version returns 409 and requires the caller to refresh
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="reorderList">List of segment IDs with their new positions.</param>
        /// <returns>Success message confirming the Draft structure was reordered.</returns>
        /// <response code="200">Segments reordered successfully.</response>
        /// <response code="400">Invalid reorder list.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("reorder")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult> ReorderSegments([FromBody] List<ReorderSegmentDto> reorderList)
        {
            if (reorderList == null || !reorderList.Any())
                return BadRequest("Reorder list cannot be empty.");

            try
            {
                await _accountSegmentStructureService.ReorderSegmentsAsync(reorderList);
                return Ok(new { message = "Draft account-number segments reordered successfully." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "The segment structure changed; refresh and retry." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
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

        private SegmentStructureDto MapToSegmentStructureDto(AccountSegmentStructureDto source)
        {
             return new SegmentStructureDto
             {
                 Id = source.Id,
                 SegmentName = source.SegmentName,
                 SegmentCode = source.SegmentCode,
                 SegmentPosition = source.SegmentPosition,
                 SegmentLength = source.SegmentLength,
                 DataType = source.DataType,
                 SeparatorCharacter = source.SeparatorCharacter,
                 LookupTableRequired = source.LookupTableRequired,
                 IsRequired = true,
                 IsReportingDimension = source.IsReportingDimension,
                 IsNaturalAccount = source.IsNaturalAccount,
                 IsActive = source.IsActive,
                 LifecycleStatus = source.LifecycleStatus,
                 RowVersion = source.RowVersion,
                 AccountUsageCount = source.AccountUsageCount,
                 TotalAccountCount = source.TotalAccountCount,
                 CanActivate = source.CanActivate,
                 CanFreeze = source.CanFreeze,
                 IsSystemDefined = source.IsSystemDefined,
                 Description = source.Description,
                 LookupValueCount = source.LookupValuesCount,
                 LookupValues = source.LookupValues.Select(value => new SegmentLookupValueDto
                 {
                     Id = value.Id,
                     TenantId = source.TenantId,
                     SegmentStructureId = source.Id,
                     SegmentValue = value.SegmentValue,
                     Description = value.Description,
                     IsActive = value.IsActive,
                     DisplayOrder = value.DisplayOrder
                 }).ToList()
             };
        }

        #endregion

        #region Segment Lookup Values

        /// <summary>
        /// Retrieves all lookup values for a specific segment.
        /// </summary>
        /// <remarks>
        /// Used to populate segment value dropdowns in account creation.
        ///
        /// **Common Use Cases:**
        /// - Display available values for a segment (e.g., departments, locations)
        /// - Validate segment values in account numbers
        /// - Build account number construction UI
        ///
        /// **Integration Pattern:**
        /// - Call when user needs to select a segment value
        /// - Use returned values to populate dropdowns
        /// - Support hierarchical values if segment allows parent-child
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="id">Segment structure ID.</param>
        /// <returns>List of lookup values for the segment.</returns>
        /// <response code="200">Returns the list of lookup values.</response>
        /// <response code="404">Segment not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("{id}/values")]
        public async Task<ActionResult<List<SegmentLookupValueDto>>> GetSegmentLookupValues(Guid id)
        {
            try
            {
                var values = await _segmentStructureService.GetSegmentLookupValuesAsync(id);
                return Ok(values);
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
        /// Adds a lookup value to a segment.
        /// </summary>
        /// <remarks>
        /// Supports parent-child relationships for hierarchical segments.
        ///
        /// **Common Use Cases:**
        /// - Add a new department code (e.g., "FIN" for Finance) to the Department segment
        /// - Define a new cost center or location value
        /// - Create child values under a parent for hierarchical reporting
        ///
        /// **Integration Pattern:**
        /// - Ensure the target segment exists via GET /api/finance/segments/{id}
        /// - POST the value with code, name, and optional parent ID for hierarchy
        /// - The created value becomes immediately available for account number construction
        ///
        /// **Business Rules:**
        /// - The value code must be unique within the segment
        /// - The value code length must not exceed the segment's defined SegmentLength
        /// - If the segment has LookupTableRequired = true, only pre-defined values are valid in account numbers
        /// - Parent ID is optional; when provided, it establishes a parent-child hierarchy
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="id">The segment structure ID.</param>
        /// <param name="dto">The lookup value data to create.</param>
        /// <returns>The created lookup value.</returns>
        /// <response code="201">Lookup value created successfully.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="404">Segment not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("{id}/values")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult<SegmentLookupValueDto>> AddSegmentLookupValue(
            Guid id,
            [FromBody] SegmentLookupValueCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                dto.SegmentStructureId = id;
                var value = await _segmentLookupValueService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetSegmentLookupValues), new { id }, value);
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
        /// Updates a segment lookup value.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Rename a lookup value (e.g., change department name from "HR" to "Human Resources")
        /// - Update the description or display label of a segment value
        /// - Activate or deactivate a lookup value
        /// - Change parent assignment in hierarchical segments
        ///
        /// **Integration Pattern:**
        /// - Fetch the current value via GET /api/finance/segments/{id}/values
        /// - Modify the desired fields in the update DTO
        /// - PUT to this endpoint with both the segment ID and the value ID in the route
        ///
        /// **Business Rules:**
        /// - The value code typically cannot be changed if it is in use by existing accounts
        /// - Deactivating a value prevents its use in new accounts but does not affect existing ones
        /// - Changing the parent ID restructures the hierarchy for reporting purposes
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="id">The segment structure ID.</param>
        /// <param name="valueId">The lookup value ID to update.</param>
        /// <param name="dto">The updated lookup value data.</param>
        /// <returns>The updated lookup value.</returns>
        /// <response code="200">Lookup value updated successfully.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="404">Lookup value not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPut("{id}/values/{valueId}")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult<SegmentLookupValueDto>> UpdateSegmentLookupValue(
            Guid id,
            Guid valueId,
            [FromBody] SegmentLookupValueUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                dto.Id = valueId;
                var value = await _segmentLookupValueService.UpdateAsync(dto);
                return Ok(value);
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
        /// Deletes a segment lookup value.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Remove an unused lookup value from a segment (e.g., a decommissioned department code)
        /// - Clean up test or incorrectly created values
        ///
        /// **Integration Pattern:**
        /// - Verify the value is not referenced by any account numbers before attempting deletion
        /// - On success (204), refresh the lookup values list in the UI
        /// - On failure (400), display the error indicating the value is in use
        ///
        /// **Business Rules:**
        /// - A lookup value that is referenced by existing accounts cannot be deleted (returns 400)
        /// - Deletion is permanent; consider deactivating the value via PUT instead if it may be needed later
        /// - Child values must be removed or reassigned before deleting a parent value
        ///
        /// **Authorization:** Requires Finance.Write permission
        /// </remarks>
        /// <param name="id">The segment structure ID.</param>
        /// <param name="valueId">The lookup value ID to delete.</param>
        /// <returns>No content on successful deletion.</returns>
        /// <response code="204">Lookup value deleted successfully.</response>
        /// <response code="400">Lookup value is in use and cannot be deleted.</response>
        /// <response code="404">Lookup value not found.</response>
        /// <response code="500">Internal server error.</response>
        [HttpDelete("{id}/values/{valueId}")]
        [Authorize(Policy = FinancePermissions.ConfigureChartOfAccountsPolicy)]
        public async Task<ActionResult> DeleteSegmentLookupValue(Guid id, Guid valueId)
        {
            try
            {
                await _segmentLookupValueService.DeleteAsync(valueId);
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

        #endregion

        #region Utility Endpoints

        /// <summary>
        /// Validates an account number against the configured segment structure.
        /// </summary>
        /// <remarks>
        /// **IMPORTANT:** Use this before creating accounts in segmented COA.
        ///
        /// **Common Use Cases:**
        /// - Validate user-entered account numbers
        /// - Check account number format before creation
        /// - Provide real-time validation feedback
        ///
        /// **Integration Pattern:**
        /// 1. User enters account number
        /// 2. POST /api/Finance/segments/validate with account number
        /// 3. If valid, allow account creation
        /// 4. If invalid, show error message
        ///
        /// **Validation Rules:**
        /// - Checks segment count matches structure
        /// - Validates each segment value exists in lookup
        /// - Checks segment length constraints
        /// - Verifies separator usage
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="dto">Account number to validate.</param>
        /// <returns>Validation result.</returns>
        /// <response code="200">Returns validation result (true/false).</response>
        /// <response code="400">Invalid request.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("validate")]
        public async Task<ActionResult<bool>> ValidateAccountNumber([FromBody] SegmentedAccountValidationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var isValid = await _segmentStructureService.ValidateAccountNumberAsync(dto.AccountNumber);
                return Ok(new { isValid, accountNumber = dto.AccountNumber });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Constructs a valid account number from individual segment values.
        /// </summary>
        /// <remarks>
        /// Use this to build account numbers programmatically.
        ///
        /// **Common Use Cases:**
        /// - Build account numbers from segment selections
        /// - Auto-generate account numbers
        /// - Ensure proper formatting
        ///
        /// **Integration Pattern:**
        /// User selects:
        /// - Department: "100"
        /// - Account Type: "1000"
        /// - Location: "01"
        ///
        /// POST /api/Finance/segments/construct
        /// { "segmentValues": ["100", "1000", "01"] }
        ///
        /// Returns: "100-1000-01"
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="dto">Segment values to construct account number from.</param>
        /// <returns>Constructed account number.</returns>
        /// <response code="200">Returns the constructed account number.</response>
        /// <response code="400">Invalid segment values.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("construct")]
        public async Task<ActionResult<string>> ConstructAccountNumber([FromBody] AccountNumberConstructionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var accountNumber = await _segmentStructureService.ConstructAccountNumberAsync(dto.SegmentValues);
                return Ok(new { accountNumber });
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
        /// Parses an account number into its individual segment values.
        /// </summary>
        /// <remarks>
        /// Use this to decompose account numbers for analysis or editing.
        ///
        /// **Common Use Cases:**
        /// - Break down account number for editing
        /// - Extract segment values for reporting
        /// - Analyze account structure
        ///
        /// **Integration Pattern:**
        /// GET /api/Finance/segments/parse/100-1000-01
        ///
        /// Returns:
        /// [
        ///   { "segmentName": "Department", "value": "100" },
        ///   { "segmentName": "Account Type", "value": "1000" },
        ///   { "segmentName": "Location", "value": "01" }
        /// ]
        ///
        /// **Authorization:** Requires Finance.Read permission
        /// </remarks>
        /// <param name="accountNumber">Account number to parse.</param>
        /// <returns>List of segment values.</returns>
        /// <response code="200">Returns the parsed segment values.</response>
        /// <response code="400">Invalid account number format.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("parse/{accountNumber}")]
        public async Task<ActionResult<List<SegmentValueDto>>> ParseAccountNumber(string accountNumber)
        {
            try
            {
                var segments = await _segmentStructureService.ParseAccountNumberAsync(accountNumber);
                return Ok(segments);
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

        #endregion
    }
}
