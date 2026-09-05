using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class ComplianceTrainingService : IComplianceTrainingService
{
    private readonly IComplianceTrainingRequirementRepository _requirementRepository;
    private readonly IEmployeeComplianceRecordRepository _recordRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ComplianceTrainingService> _logger;

    public ComplianceTrainingService(
        IComplianceTrainingRequirementRepository requirementRepository,
        IEmployeeComplianceRecordRepository recordRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ComplianceTrainingService> logger)
    {
        _requirementRepository = requirementRepository;
        _recordRepository = recordRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A requirement owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<ComplianceTrainingRequirement> GetOwnedRequirementAsync(Guid id)
    {
        var entity = await _requirementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Compliance training requirement with ID '{id}' not found.");
        return entity;
    }

    // A record owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<EmployeeComplianceRecord> GetOwnedRecordAsync(Guid id)
    {
        var entity = await _recordRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employee compliance record with ID '{id}' not found.");
        return entity;
    }

    // ── Requirement queries ───────────────────────────────────────────────────

    public async Task<ComplianceTrainingRequirementDto> GetRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _requirementRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Compliance training requirement with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetAllRequirementsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requirementRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetActiveRequirementsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requirementRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetRequirementsByProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _requirementRepository.GetByProgramIdAsync(programId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Requirement CRUD ──────────────────────────────────────────────────────

    public async Task<ComplianceTrainingRequirementDto> CreateRequirementAsync(CreateComplianceTrainingRequirementDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _requirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Compliance training requirement created: {RequirementName}", dto.RequirementName);

        // Freshly written: no Program/scope/EmployeeRecords loaded, so the response would show a
        // blank programme and a 0% compliance rate on a requirement just created.
        return await GetRequirementByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ComplianceTrainingRequirementDto> UpdateRequirementAsync(UpdateComplianceTrainingRequirementDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequirementAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _requirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // A changed ProgramId or scope does not refresh the loaded navigations — re-read.
        return await GetRequirementByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRequirementAsync(id);

        await _requirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Compliance training requirement {RequirementId} deleted", id);

        return true;
    }

    // ── Employee compliance record queries ────────────────────────────────────

    public async Task<EmployeeComplianceRecordDto> GetRecordByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRecordAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordDto>> GetRecordsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _recordRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetRecordsForRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _recordRepository.GetByRequirementIdAsync(requirementId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetNonCompliantRecordsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _recordRepository.GetNonCompliantAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetOverdueRecordsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _recordRepository.GetOverdueAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Employee compliance record workflow ───────────────────────────────────

    public async Task<EmployeeComplianceRecordDto> AssignRequirementToEmployeeAsync(Guid employeeId, Guid requirementId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedRequirementAsync(requirementId);

        var existing = await _recordRepository.GetEmployeeRecordAsync(employeeId, requirementId);

        if (existing != null)
            throw new InvalidOperationException($"Employee already has a compliance record for requirement '{requirementId}'.");

        var entity = new EmployeeComplianceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = current,
            EmployeeId = employeeId,
            RequirementId = requirementId,
            Status = ComplianceStatus.NonCompliant,
            AssignedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdByUserId.ToString()
        };

        await _recordRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Compliance requirement {RequirementId} assigned to employee {EmployeeId}", requirementId, employeeId);

        // Freshly written: nothing loaded, so the response would name neither the employee nor the
        // requirement it was just assigned against.
        var savedAssignment = await _recordRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (savedAssignment ?? entity).ToDto();
    }

    public async Task<EmployeeComplianceRecordDto> ExemptEmployeeAsync(ExemptEmployeeComplianceDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRecordAsync(dto.RecordId);

        entity.IsExempt = true;
        entity.ExemptionReason = dto.ExemptionReason;
        entity.ExemptedById = updatedByUserId;
        entity.ExemptionDate = DateTime.UtcNow;
        entity.ExemptionExpiryDate = dto.ExemptionExpiryDate;
        entity.Status = ComplianceStatus.NotApplicable;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _recordRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee compliance record {RecordId} marked as exempt", dto.RecordId);

        // ExemptedById was just set, so the tracked instance still maps a null exempter — an
        // untracked re-read is the only way to get ExemptedByName onto this response.
        var savedExemption = await _recordRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (savedExemption ?? entity).ToDto();
    }

    public async Task<EmployeeComplianceRecordDto> MarkFulfilledAsync(Guid recordId, Guid nominationId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRecordAsync(recordId);

        var completedOn = DateTime.UtcNow;
        entity.FulfillingNominationId = nominationId;
        entity.LastCompletedDate = completedOn;
        entity.Status = ComplianceStatus.Compliant;

        // Recompute the next due date from the requirement's recurrence so overdue/expiring
        // queries behave correctly after fulfilment (previously left stale).
        var requirement = await _requirementRepository.GetByIdAsync(entity.RequirementId);
        if (requirement != null && requirement.TenantId == entity.TenantId)
        {
            entity.NextDueDate = ComputeNextDueDate(completedOn, requirement.Frequency, requirement.CustomFrequencyDays);
            entity.GracePeriodExpiry = entity.NextDueDate.HasValue && requirement.GracePeriodDays.HasValue
                ? entity.NextDueDate.Value.AddDays(requirement.GracePeriodDays.Value)
                : entity.NextDueDate;
        }

        entity.UpdatedAt = completedOn;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _recordRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee compliance record {RecordId} marked as fulfilled via nomination {NominationId}; next due {NextDue}", recordId, nominationId, entity.NextDueDate);

        // FulfillingNominationId was just set, so its navigation is still null — without this the
        // response omits the very nomination that satisfied the requirement.
        var savedFulfilment = await _recordRepository.GetByIdWithNavigationsAsync(entity.Id);
        return (savedFulfilment ?? entity).ToDto();
    }

    /// <summary>
    /// Computes the next-due date for a compliance requirement from its recurrence frequency.
    /// One-time requirements have no next due date.
    /// </summary>
    private static DateTime? ComputeNextDueDate(DateTime completedOn, ComplianceFrequency frequency, int? customFrequencyDays)
        => frequency switch
        {
            ComplianceFrequency.OneTime => null,
            ComplianceFrequency.Annual => completedOn.AddYears(1),
            ComplianceFrequency.BiAnnual => completedOn.AddMonths(6),
            ComplianceFrequency.Quarterly => completedOn.AddMonths(3),
            ComplianceFrequency.Monthly => completedOn.AddMonths(1),
            ComplianceFrequency.Custom => customFrequencyDays.HasValue ? completedOn.AddDays(customFrequencyDays.Value) : null,
            _ => null
        };
}
