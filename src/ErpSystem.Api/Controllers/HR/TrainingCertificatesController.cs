using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-certificates")]
public class TrainingCertificatesController : ControllerBase
{
    private readonly ICertificateVerificationService _verification;

    public TrainingCertificatesController(ICertificateVerificationService verification)
    {
        _verification = verification;
    }

    /// <summary>
    /// Public certificate verification by code. Anonymous by design so a third party can validate a
    /// presented certificate; returns only non-sensitive attestation details.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("verify/{code}")]
    public async Task<ActionResult<CertificateVerificationResultDto>> Verify(string code, CancellationToken ct)
        => Ok(await _verification.VerifyByCodeAsync(code, ct));
}
