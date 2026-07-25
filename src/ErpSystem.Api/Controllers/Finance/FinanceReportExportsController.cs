using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/finance/report-exports")]
public sealed class FinanceReportExportsController : ControllerBase
{
    private readonly IFinanceReportExportService _reportExportService;

    public FinanceReportExportsController(IFinanceReportExportService reportExportService)
    {
        _reportExportService = reportExportService;
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromBody] FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _reportExportService.ExportAsync(request, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("print")]
    public async Task<IActionResult> Print(
        [FromBody] FinanceReportExportRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _reportExportService.PrintAsync(request, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
