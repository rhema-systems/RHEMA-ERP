using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Api.Controllers.Sales
{
    [ApiController]
    [Route("api/sales/commissions")]
    [Authorize]
    public class CommissionController : ControllerBase
    {
        private readonly ICommissionService _service;

        public CommissionController(ICommissionService service) => _service = service;

        // ── Rules ──

        [HttpGet("rules")]
        public async Task<IActionResult> GetRules([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] bool? isActive = null)
        {
            var (items, totalCount) = await _service.GetRulesAsync(page, pageSize, search, isActive);
            return Ok(new { items, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
        }

        [HttpGet("rules/{id}")]
        public async Task<IActionResult> GetRule(Guid id)
        {
            var rule = await _service.GetRuleByIdAsync(id);
            return rule == null ? NotFound() : Ok(rule);
        }

        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] CreateCommissionRuleDto dto)
        {
            try { return Ok(await _service.CreateRuleAsync(dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateRule(Guid id, [FromBody] CreateCommissionRuleDto dto)
        {
            try { return Ok(await _service.UpdateRuleAsync(id, dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("rules/{id}/activate")]
        public async Task<IActionResult> ActivateRule(Guid id)
        {
            return await _service.ActivateRuleAsync(id) ? Ok() : NotFound();
        }

        [HttpPost("rules/{id}/deactivate")]
        public async Task<IActionResult> DeactivateRule(Guid id)
        {
            return await _service.DeactivateRuleAsync(id) ? Ok() : NotFound();
        }

        // ── Statements ──

        [HttpGet("statements")]
        public async Task<IActionResult> GetStatements([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null)
        {
            var (items, totalCount) = await _service.GetStatementsAsync(page, pageSize, search, status);
            return Ok(new { items, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
        }

        [HttpGet("statements/{id}")]
        public async Task<IActionResult> GetStatement(Guid id)
        {
            var statement = await _service.GetStatementByIdAsync(id);
            return statement == null ? NotFound() : Ok(statement);
        }

        [HttpPost("statements/generate")]
        public async Task<IActionResult> GenerateStatement([FromBody] GenerateStatementDto dto)
        {
            try { return Ok(await _service.GenerateStatementAsync(dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("statements/{id}/approve")]
        public async Task<IActionResult> ApproveStatement(Guid id)
        {
            try { return Ok(await _service.ApproveStatementAsync(id)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("statements/{id}/pay")]
        public async Task<IActionResult> MarkAsPaid(Guid id, [FromQuery] string? paymentReference = null)
        {
            try { return Ok(await _service.MarkAsPaidAsync(id, paymentReference)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("statements/{id}/dispute")]
        public async Task<IActionResult> DisputeStatement(Guid id, [FromQuery] string? reason = null)
        {
            try { return Ok(await _service.DisputeStatementAsync(id, reason)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}
