using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Api.Controllers.Sales
{
    [ApiController]
    [Route("api/sales/forecasts")]
    [Authorize]
    public class ForecastController : ControllerBase
    {
        private readonly IForecastService _service;

        public ForecastController(IForecastService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetForecasts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null)
        {
            var (items, totalCount) = await _service.GetForecastsAsync(page, pageSize, search, status);
            return Ok(new { items, totalCount, page, pageSize, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetForecast(Guid id)
        {
            var f = await _service.GetForecastByIdAsync(id);
            return f == null ? NotFound() : Ok(f);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateForecastDto dto)
        {
            try { return Ok(await _service.CreateForecastAsync(dto)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("{id}/submit")]
        public async Task<IActionResult> Submit(Guid id)
        {
            try { return Ok(await _service.SubmitForecastAsync(id)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> Approve(Guid id)
        {
            try { return Ok(await _service.ApproveForecastAsync(id)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("{id}/lock")]
        public async Task<IActionResult> Lock(Guid id)
        {
            try { return Ok(await _service.LockForecastAsync(id)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpPost("{id}/refresh-actuals")]
        public async Task<IActionResult> RefreshActuals(Guid id)
        {
            try { return Ok(await _service.RefreshActualsAsync(id)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}
