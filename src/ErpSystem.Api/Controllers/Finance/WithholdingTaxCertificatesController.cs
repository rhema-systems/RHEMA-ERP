using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/finance/tax/wht-certificates")]
public sealed class WithholdingTaxCertificatesController : ControllerBase
{
    private readonly IWithholdingTaxCertificateService _certificateService;

    public WithholdingTaxCertificatesController(IWithholdingTaxCertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WhtCertificateDto>>> GetApCertificates(
        [FromQuery] WhtCertificateQueryDto query,
        CancellationToken cancellationToken)
        => Ok(await _certificateService.GetApCertificatesAsync(query, cancellationToken));

    [HttpGet("{vendorPaymentId:guid}")]
    public async Task<ActionResult<WhtCertificateDto>> GetApCertificate(
        Guid vendorPaymentId,
        CancellationToken cancellationToken)
    {
        var certificate = await _certificateService.GetApCertificateAsync(vendorPaymentId, cancellationToken);
        return certificate == null ? NotFound() : Ok(certificate);
    }

    [HttpPost("{vendorPaymentId:guid}/generate")]
    public async Task<ActionResult<WhtCertificateDto>> GenerateApCertificate(
        Guid vendorPaymentId,
        [FromBody] GenerateWhtCertificateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.GenerateApCertificateAsync(vendorPaymentId, dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{vendorPaymentId:guid}/reissue")]
    public async Task<ActionResult<WhtCertificateDto>> ReissueApCertificate(
        Guid vendorPaymentId,
        [FromBody] ReissueWhtCertificateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.ReissueApCertificateAsync(vendorPaymentId, dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{vendorPaymentId:guid}/cancel")]
    public async Task<ActionResult<WhtCertificateDto>> CancelApCertificate(
        Guid vendorPaymentId,
        [FromBody] CancelWhtCertificateDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.CancelApCertificateAsync(vendorPaymentId, dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{vendorPaymentId:guid}/print")]
    public async Task<IActionResult> GetApCertificatePrintView(
        Guid vendorPaymentId,
        [FromQuery] Guid? certificateId,
        CancellationToken cancellationToken)
    {
        try
        {
            var html = await _certificateService.GetApCertificateHtmlAsync(vendorPaymentId, certificateId, cancellationToken);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<WhtCalculationResultDto>> CalculateApWithholding(
        [FromBody] WhtCalculationRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.CalculateApWithholdingAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("remittances/liabilities")]
    public async Task<ActionResult<IReadOnlyList<WhtRemittanceLiabilityDto>>> GetUnremittedLiabilities(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? currencyCode,
        CancellationToken cancellationToken)
        => Ok(await _certificateService.GetUnremittedLiabilitiesAsync(fromDate, toDate, currencyCode, cancellationToken));

    [HttpGet("remittances")]
    public async Task<ActionResult<PagedResult<WhtRemittanceDto>>> GetRemittances(
        [FromQuery] WhtRemittanceQueryDto query,
        CancellationToken cancellationToken)
        => Ok(await _certificateService.GetRemittancesAsync(query, cancellationToken));

    [HttpGet("remittances/{remittanceId:guid}")]
    public async Task<ActionResult<WhtRemittanceDto>> GetRemittance(
        Guid remittanceId,
        CancellationToken cancellationToken)
    {
        var result = await _certificateService.GetRemittanceAsync(remittanceId, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("remittances")]
    public async Task<ActionResult<WhtRemittanceDto>> CreateRemittance(
        [FromBody] CreateWhtRemittanceDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.CreateRemittanceAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("remittances/{remittanceId:guid}/submit")]
    public async Task<ActionResult<WhtRemittanceDto>> SubmitRemittance(
        Guid remittanceId,
        [FromBody] SubmitWhtRemittanceDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.SubmitRemittanceAsync(remittanceId, dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("remittances/{remittanceId:guid}/paid")]
    public async Task<ActionResult<WhtRemittanceDto>> MarkRemittancePaid(
        Guid remittanceId,
        [FromBody] PayWhtRemittanceDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.MarkRemittancePaidAsync(remittanceId, dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("remittances/{remittanceId:guid}/cancel")]
    public async Task<ActionResult<WhtRemittanceDto>> CancelRemittance(
        Guid remittanceId,
        [FromBody] CancelWhtRemittanceDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _certificateService.CancelRemittanceAsync(remittanceId, dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("register/export")]
    public async Task<IActionResult> ExportRegister(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var export = await _certificateService.ExportRegisterAsync(fromDate, toDate, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
