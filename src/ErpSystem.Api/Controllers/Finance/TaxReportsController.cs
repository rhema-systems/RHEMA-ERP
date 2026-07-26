using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/finance/tax-reports")]
public sealed class TaxReportsController : ControllerBase
{
    private readonly ITaxReportingService _taxReportingService;

    public TaxReportsController(ITaxReportingService taxReportingService)
    {
        _taxReportingService = taxReportingService;
    }

    [HttpPost("output-tax")]
    public async Task<ActionResult<GhanaTaxSnapshotReportDto>> GetOutputTaxReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetOutputTaxReportAsync(request, cancellationToken));

    [HttpPost("input-tax")]
    public async Task<ActionResult<GhanaTaxSnapshotReportDto>> GetInputTaxReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetInputTaxReportAsync(request, cancellationToken));

    [HttpPost("net-summary")]
    public async Task<ActionResult<GhanaTaxSnapshotReportDto>> GetNetVatSummary(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetNetVatSummaryAsync(request, cancellationToken));

    [HttpPost("non-taxable-supplies")]
    public async Task<ActionResult<GhanaTaxTreatmentReportDto>> GetExemptZeroRatedOutOfScopeReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetExemptZeroRatedOutOfScopeReportAsync(request, cancellationToken));

    [HttpPost("vat-withholding")]
    public async Task<ActionResult<GhanaTaxWithholdingReportDto>> GetVatWithholdingReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetVatWithholdingReportAsync(request, cancellationToken));

    [HttpPost("wht-payable")]
    public async Task<ActionResult<GhanaTaxWithholdingReportDto>> GetWhtPayableReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetWhtPayableReportAsync(request, cancellationToken));

    [HttpPost("wht-receivable")]
    public async Task<ActionResult<GhanaTaxWithholdingReportDto>> GetWhtReceivableReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetWhtReceivableReportAsync(request, cancellationToken));

    [HttpPost("tax-account-reconciliation")]
    public async Task<ActionResult<GhanaTaxAccountReconciliationReportDto>> GetTaxAccountReconciliationReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetTaxAccountReconciliationReportAsync(request, cancellationToken));

    [HttpPost("configuration-history")]
    public async Task<ActionResult<GhanaTaxConfigurationHistoryReportDto>> GetTaxConfigurationHistoryReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetTaxConfigurationHistoryReportAsync(request, cancellationToken));

    [HttpPost("covid-levy-diagnostic")]
    public async Task<ActionResult<GhanaTaxCovidLevyDiagnosticReportDto>> GetCurrentActiveCovidLevyDiagnosticReport(
        [FromBody] TaxReportRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _taxReportingService.GetCurrentActiveCovidLevyDiagnosticReportAsync(request, cancellationToken));
}
