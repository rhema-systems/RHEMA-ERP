using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Public (tenant-agnostic) verification of an issued training certificate by its verification code.
/// </summary>
public interface ICertificateVerificationService
{
    Task<CertificateVerificationResultDto> VerifyByCodeAsync(string code, CancellationToken cancellationToken = default);
}
