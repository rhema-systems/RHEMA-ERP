using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/segments")]
    public class SegmentConfigurationController : ControllerBase
    {
        private readonly ISegmentConfigurationService _segmentService;

        public SegmentConfigurationController(ISegmentConfigurationService segmentService)
        {
            _segmentService = segmentService;
        }

        /// <summary>
        /// Get all segment structures for the current tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<SegmentStructureDto>>> GetAllSegments()
        {
            var segments = await _segmentService.GetAllSegmentStructuresAsync();
            return Ok(segments);
        }

        /// <summary>
        /// Get a specific segment structure by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SegmentStructureDto>> GetSegment(Guid id)
        {
            var segment = await _segmentService.GetSegmentStructureAsync(id);
            if (segment == null)
                return NotFound($"Segment structure {id} not found");

            return Ok(segment);
        }

        /// <summary>
        /// Create a new segment structure
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SegmentStructureDto>> CreateSegment([FromBody] SegmentStructureCreateDto dto)
        {
            try
            {
                var segment = await _segmentService.CreateSegmentStructureAsync(dto);
                return CreatedAtAction(nameof(GetSegment), new { id = segment.Id }, segment);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message} | Inner: {ex.InnerException?.Message}");
            }
        }

        /// <summary>
        /// Update an existing segment structure
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<SegmentStructureDto>> UpdateSegment(Guid id, [FromBody] SegmentStructureUpdateDto dto)
        {
            if (id != dto.Id)
                return BadRequest("ID mismatch");

            try
            {
                var segment = await _segmentService.UpdateSegmentStructureAsync(dto);
                return Ok(segment);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Delete a segment structure
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteSegment(Guid id)
        {
            try
            {
                await _segmentService.DeleteSegmentStructureAsync(id);
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
        }

        /// <summary>
        /// Validate a segmented account number
        /// </summary>
        [HttpPost("validate")]
        public async Task<ActionResult<SegmentedAccountValidationDto>> ValidateAccountNumber([FromBody] string accountNumber)
        {
            var result = await _segmentService.ValidateSegmentedAccountAsync(accountNumber);
            return Ok(result);
        }

        /// <summary>
        /// Construct an account number from segment values
        /// </summary>
        [HttpPost("construct")]
        public async Task<ActionResult<AccountNumberConstructionDto>> ConstructAccountNumber([FromBody] Dictionary<int, string> segmentValues)
        {
            try
            {
                var result = await _segmentService.ConstructAccountNumberAsync(segmentValues);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Parse an account number into its segment values
        /// </summary>
        [HttpGet("parse/{accountNumber}")]
        public async Task<ActionResult<List<SegmentValueDto>>> ParseAccountNumber(string accountNumber)
        {
            var result = await _segmentService.ParseAccountNumberAsync(accountNumber);
            return Ok(result);
        }
    }
}
