using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class EmployeeCertificateService : IEmployeeCertificateService
{
    private readonly IEmployeeCertificateRepository _certificateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeCertificateService> _logger;

    public EmployeeCertificateService(
        IEmployeeCertificateRepository certificateRepository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeCertificateService> logger)
    {
        _certificateRepository = certificateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<EmployeeCertificateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _certificateRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee certificate with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeCertificateDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _certificateRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeCertificateSummaryDto>> GetUnverifiedAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _certificateRepository.GetUnverifiedAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeCertificateSummaryDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _certificateRepository.GetExpiringAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<EmployeeCertificateDto> CreateAsync(CreateEmployeeCertificateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _certificateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee certificate created for employee {EmployeeId}", dto.EmployeeId);

        return entity.ToDto();
    }

    public async Task<EmployeeCertificateDto> UpdateAsync(UpdateEmployeeCertificateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _certificateRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Employee certificate with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _certificateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee certificate {CertificateId} updated", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _certificateRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee certificate with ID '{id}' not found.");

        await _certificateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee certificate {CertificateId} deleted", id);

        return true;
    }

    // ── Verification ──────────────────────────────────────────────────────────

    public async Task<EmployeeCertificateDto> VerifyAsync(VerifyEmployeeCertificateDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _certificateRepository.GetByIdAsync(dto.CertificateId);

        if (entity == null)
            throw new ArgumentException($"Employee certificate with ID '{dto.CertificateId}' not found.");

        if (entity.IsVerified)
            throw new InvalidOperationException("This certificate has already been verified.");

        entity.IsVerified = true;
        entity.VerifiedById = dto.VerifiedById;
        entity.VerifiedDate = dto.VerifiedDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _certificateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee certificate {CertificateId} verified by {VerifierId}", dto.CertificateId, dto.VerifiedById);

        return entity.ToDto();
    }
}
