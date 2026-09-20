using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/ap/payments/{id:guid}/evidence")]
public sealed class VendorPaymentEvidenceController : ControllerBase
{
    private readonly IVendorPaymentService _payments;
    public VendorPaymentEvidenceController(IVendorPaymentService payments) => _payments = payments;

    [HttpPost]
    [Authorize(Policy = FinancePermissions.ProcessApPayments)]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, [FromForm] PaymentEvidenceForm request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length <= 0 || request.File.Length > 25 * 1024 * 1024)
            return Error("AP_PAYMENT_EVIDENCE_INVALID_FILE", "Choose a file up to 25 MB.", 400);
        try
        {
            using var content = new MemoryStream();
            await request.File.CopyToAsync(content, cancellationToken);
            return Ok(await _payments.UploadEvidenceAsync(id, new VendorPaymentEvidenceUploadDto
            {
                RequirementKey = request.RequirementKey, ClientRequestId = request.ClientRequestId,
                ExpiryDate = request.ExpiryDate, FileName = Path.GetFileName(request.File.FileName),
                ContentType = request.File.ContentType, Content = content.ToArray()
            }, cancellationToken));
        }
        catch (ControlledFileUploadException error) { return Error(error.Code, error.Message, error.StatusCode); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException error) { return Error("AP_PAYMENT_EVIDENCE_INVALID", error.Message, 409); }
    }

    [HttpGet("{evidenceId:guid}/content")]
    [Authorize(Policy = FinancePermissions.ViewFinance)]
    public async Task<IActionResult> Download(Guid id, Guid evidenceId, CancellationToken cancellationToken)
    {
        try
        {
            var content = await _payments.OpenEvidenceAsync(id, evidenceId, cancellationToken);
            if (content == null) return NotFound();
            // MVC owns the returned stream and disposes it after sending the response.
            return File(content.Content, content.ContentType, content.FileName);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException error) { return Error("AP_PAYMENT_EVIDENCE_UNAVAILABLE", error.Message, 409); }
    }

    private ObjectResult Error(string code, string detail, int status)
    {
        var problem = new ProblemDetails { Status = status, Title = "Payment document could not be processed", Detail = detail };
        problem.Extensions["code"] = code;
        return StatusCode(status, problem);
    }

    public sealed class PaymentEvidenceForm
    {
        [Required] public IFormFile File { get; set; } = null!;
        [Required, MaxLength(150)] public string RequirementKey { get; set; } = string.Empty;
        public Guid ClientRequestId { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
