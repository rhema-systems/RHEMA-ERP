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

    [HttpGet("{vendorPaymentId:guid}/print")]
    public async Task<IActionResult> GetApCertificatePrintView(
        Guid vendorPaymentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var html = await _certificateService.GetApCertificateHtmlAsync(vendorPaymentId, cancellationToken);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
