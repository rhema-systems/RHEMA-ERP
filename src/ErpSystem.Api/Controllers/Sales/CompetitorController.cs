using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Api.Controllers.Sales
{
    [ApiController]
    [Route("api/sales/competitors")]
    [Authorize]
    public class CompetitorController : ControllerBase
    {
        private readonly ICompetitorService _service;

        public CompetitorController(ICompetitorService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetCompetitors([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? threatLevel = null)
        {
            var (items, totalCount) = await _service.GetCompetitorsAsync(page, pageSize, search, threatLevel);
            return Ok(new { items, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCompetitor(Guid id)
        {
            var c = await _service.GetCompetitorByIdAsync(id);
            return c == null ? NotFound() : Ok(c);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCompetitorDto dto)
        {
            try { return Ok(await _service.CreateCompetitorAsync(dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCompetitorDto dto)
        {
            try { return Ok(await _service.UpdateCompetitorAsync(id, dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("deals")]
        public async Task<IActionResult> TrackDeal([FromBody] CreateCompetitorDealDto dto)
        {
            try { return Ok(await _service.TrackDealAsync(dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("deals/{dealId}/outcome")]
        public async Task<IActionResult> UpdateOutcome(Guid dealId, [FromQuery] string outcome, [FromQuery] string? lessonsLearned = null)
        {
            try { return Ok(await _service.UpdateDealOutcomeAsync(dealId, outcome, lessonsLearned)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}
