using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Api.Controllers.Sales
{
    [ApiController]
    [Route("api/sales/reports")]
    [Authorize]
    public class SalesReportingController : ControllerBase
    {
        private readonly ISalesReportingService _service;

        public SalesReportingController(ISalesReportingService service) => _service = service;

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try { return Ok(await _service.GetSalesSummaryAsync(from, to)); }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
    }
}
