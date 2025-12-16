using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/segments")]
    public class SegmentStructureController : ControllerBase
    {
        private readonly ISegmentStructureService _segmentStructureService;

        public SegmentStructureController(ISegmentStructureService segmentStructureService)
        {
            _segmentStructureService = segmentStructureService;
        }

        #region Segment Structures

        /// <summary>
        /// Retrieves all segment structures for segmented Chart of Accounts.
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
        /// </summary>
        /// <returns>List of segment structures ordered by position</returns>
        /// <response code="200">Returns the list of segment structures</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        public async Task<ActionResult<List<SegmentStructureDto>>> GetSegmentStructures()
        {
            try
            {
                var segments = await _segmentStructureService.GetSegmentStructuresAsync();
                return Ok(segments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get segment structure by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SegmentStructureDto>> GetSegmentStructureById(Guid id)
        {
            try
            {
                var segment = await _segmentStructureService.GetSegmentStructureByIdAsync(id);
                if (segment == null)
                    return NotFound($"Segment structure with ID {id} not found");

                return Ok(segment);
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
        /// Create a new segment structure
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SegmentStructureDto>> CreateSegmentStructure([FromBody] SegmentStructureCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var segment = await _segmentStructureService.CreateSegmentStructureAsync(dto);
                return CreatedAtAction(nameof(GetSegmentStructureById), new { id = segment.Id }, segment);
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
        /// Update an existing segment structure
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<SegmentStructureDto>> UpdateSegmentStructure(Guid id, [FromBody] SegmentStructureUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var segment = await _segmentStructureService.UpdateSegmentStructureAsync(id, dto);
                return Ok(segment);
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
        /// Delete a segment structure (if not in use)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteSegmentStructure(Guid id)
        {
            try
            {
                await _segmentStructureService.DeleteSegmentStructureAsync(id);
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

        #region Segment Lookup Values

        /// <summary>
        /// Retrieves all lookup values for a specific segment.
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
        /// </summary>
        /// <param name="id">Segment structure ID</param>
        /// <returns>List of lookup values for the segment</returns>
        /// <response code="200">Returns the list of lookup values</response>
        /// <response code="404">Segment not found</response>
        /// <response code="500">Internal server error</response>
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
        /// Add a lookup value to a segment (with parent support for hierarchies)
        /// </summary>
        [HttpPost("{id}/values")]
        public async Task<ActionResult<SegmentLookupValueDto>> AddSegmentLookupValue(
            Guid id, 
            [FromBody] SegmentLookupValueCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var value = await _segmentStructureService.AddSegmentLookupValueAsync(id, dto);
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
        /// Update a segment lookup value
        /// </summary>
        [HttpPut("{id}/values/{valueId}")]
        public async Task<ActionResult<SegmentLookupValueDto>> UpdateSegmentLookupValue(
            Guid id,
            Guid valueId,
            [FromBody] SegmentLookupValueUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var value = await _segmentStructureService.UpdateSegmentLookupValueAsync(valueId, dto);
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
        /// Delete a segment lookup value
        /// </summary>
        [HttpDelete("{id}/values/{valueId}")]
        public async Task<ActionResult> DeleteSegmentLookupValue(Guid id, Guid valueId)
        {
            try
            {
                await _segmentStructureService.DeleteSegmentLookupValueAsync(valueId);
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
        /// **IMPORTANT:** Use this before creating accounts in segmented COA.
        /// 
        /// **Common Use Cases:**
        /// - Validate user-entered account numbers
        /// - Check account number format before creation
        /// - Provide real-time validation feedback
        /// 
        /// **Integration Pattern:**
        /// ```
        /// 1. User enters account number
        /// 2. POST /api/Finance/segments/validate with account number
        /// 3. If valid, allow account creation
        /// 4. If invalid, show error message
        /// ```
        /// 
        /// **Validation Rules:**
        /// - Checks segment count matches structure
        /// - Validates each segment value exists in lookup
        /// - Checks segment length constraints
        /// - Verifies separator usage
        /// 
        /// **Authorization:** Requires Finance.Read permission
        /// </summary>
        /// <param name="dto">Account number to validate</param>
        /// <returns>Validation result</returns>
        /// <response code="200">Returns validation result (true/false)</response>
        /// <response code="400">Invalid request</response>
        /// <response code="500">Internal server error</response>
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
        /// Use this to build account numbers programmatically.
        /// 
        /// **Common Use Cases:**
        /// - Build account numbers from segment selections
        /// - Auto-generate account numbers
        /// - Ensure proper formatting
        /// 
        /// **Integration Pattern:**
        /// ```
        /// User selects:
        /// - Department: "100"
        /// - Account Type: "1000"
        /// - Location: "01"
        /// 
        /// POST /api/Finance/segments/construct
        /// { "segmentValues": ["100", "1000", "01"] }
        /// 
        /// Returns: "100-1000-01"
        /// ```
        /// 
        /// **Authorization:** Requires Finance.Read permission
        /// </summary>
        /// <param name="dto">Segment values to construct account number from</param>
        /// <returns>Constructed account number</returns>
        /// <response code="200">Returns the constructed account number</response>
        /// <response code="400">Invalid segment values</response>
        /// <response code="500">Internal server error</response>
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
        /// Use this to decompose account numbers for analysis or editing.
        /// 
        /// **Common Use Cases:**
        /// - Break down account number for editing
        /// - Extract segment values for reporting
        /// - Analyze account structure
        /// 
        /// **Integration Pattern:**
        /// ```
        /// GET /api/Finance/segments/parse/100-1000-01
        /// 
        /// Returns:
        /// [
        ///   { "segmentName": "Department", "value": "100" },
        ///   { "segmentName": "Account Type", "value": "1000" },
        ///   { "segmentName": "Location", "value": "01" }
        /// ]
        /// ```
        /// 
        /// **Authorization:** Requires Finance.Read permission
        /// </summary>
        /// <param name="accountNumber">Account number to parse</param>
        /// <returns>List of segment values</returns>
        /// <response code="200">Returns the parsed segment values</response>
        /// <response code="400">Invalid account number format</response>
        /// <response code="500">Internal server error</response>
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
