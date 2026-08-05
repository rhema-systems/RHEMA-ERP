using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

public class CertificateVerificationService : ICertificateVerificationService
{
    private readonly ITrainingCertificateRepository _certificateRepository;
    private readonly IGenericRepository<Tenant> _tenantRepository;

    public CertificateVerificationService(
        ITrainingCertificateRepository certificateRepository,
        IGenericRepository<Tenant> tenantRepository)
    {
        _certificateRepository = certificateRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<CertificateVerificationResultDto> VerifyByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var notFound = new CertificateVerificationResultDto { Found = false, IsValid = false, Status = "NotFound" };

        if (string.IsNullOrWhiteSpace(code))
            return notFound;

        var normalized = code.Trim().ToUpperInvariant();

        // Verification is public: bypass the tenant query filter so a certificate can be validated
        // without a tenant context. Soft-deleted rows are still excluded.
        var cert = await _certificateRepository.GetQueryable()
            .IgnoreQueryFilters()
            .Include(c => c.Employee)
            .Include(c => c.Program)
            .FirstOrDefaultAsync(c => c.VerificationCode == normalized && !c.IsDeleted, cancellationToken);

        if (cert == null)
            return notFound;

        var isExpired = cert.ExpiryDate.HasValue && cert.ExpiryDate.Value.Date < DateTime.UtcNow.Date;
        var isRevoked = cert.Status == CertificateStatus.Revoked;
        var isValid = cert.Status == CertificateStatus.Active && !isExpired;

        string? org = null;
        var tenant = await _tenantRepository.GetByIdAsync(cert.TenantId);
        org = tenant?.Name;

        return new CertificateVerificationResultDto
        {
            Found = true,
            IsValid = isValid,
            Status = isRevoked ? "Revoked" : (isExpired ? "Expired" : cert.Status.ToString()),
            CertificateNumber = cert.CertificateNumber,
            VerificationCode = cert.VerificationCode,
            CertificateName = cert.CertificateName,
            EmployeeName = cert.Employee?.FullName,
            ProgramName = cert.Program?.ProgramName,
            IssuingOrganization = org,
            IssuedDate = cert.IssuedDate,
            ExpiryDate = cert.ExpiryDate,
            IsExpired = isExpired,
            IsRevoked = isRevoked,
            RevokedReason = isRevoked ? cert.RevokedReason : null,
        };
    }
}
