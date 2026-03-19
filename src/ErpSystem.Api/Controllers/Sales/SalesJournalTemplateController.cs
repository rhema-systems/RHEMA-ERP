using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Api.Controllers.Sales
{
    [ApiController]
    [Route("api/sales/journal-templates")]
    [Authorize]
    public class SalesJournalTemplateController : ControllerBase
    {
        private readonly ISalesJournalTemplateService _service;

        public SalesJournalTemplateController(ISalesJournalTemplateService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetTemplates([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] bool? isActive = null)
        {
            var (items, totalCount) = await _service.GetTemplatesAsync(page, pageSize, search, isActive);
            return Ok(new { items, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTemplate(Guid id)
        {
            var t = await _service.GetTemplateByIdAsync(id);
            return t == null ? NotFound() : Ok(t);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSalesJournalTemplateDto dto)
        {
            try { return Ok(await _service.CreateTemplateAsync(dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateSalesJournalTemplateDto dto)
        {
            try { return Ok(await _service.UpdateTemplateAsync(id, dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("{id}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            return await _service.ActivateTemplateAsync(id) ? Ok() : NotFound();
        }

        [HttpPost("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            return await _service.DeactivateTemplateAsync(id) ? Ok() : NotFound();
        }
    }
}
