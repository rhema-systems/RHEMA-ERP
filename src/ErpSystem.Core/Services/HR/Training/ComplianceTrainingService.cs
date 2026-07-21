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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ComplianceTrainingService> _logger;

    public ComplianceTrainingService(
        IComplianceTrainingRequirementRepository requirementRepository,
        IEmployeeComplianceRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        ILogger<ComplianceTrainingService> logger)
    {
        _requirementRepository = requirementRepository;
        _recordRepository = recordRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Requirement queries ───────────────────────────────────────────────────

    public async Task<ComplianceTrainingRequirementDto> GetRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requirementRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Compliance training requirement with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetAllRequirementsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _requirementRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetActiveRequirementsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _requirementRepository.GetActiveAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetRequirementsByProgramAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var entities = await _requirementRepository.GetByProgramIdAsync(programId);
        return entities.ToSummaryDtoList();
    }

    // ── Requirement CRUD ──────────────────────────────────────────────────────

    public async Task<ComplianceTrainingRequirementDto> CreateRequirementAsync(CreateComplianceTrainingRequirementDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _requirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Compliance training requirement created: {RequirementName}", dto.RequirementName);

        return entity.ToDto();
    }

    public async Task<ComplianceTrainingRequirementDto> UpdateRequirementAsync(UpdateComplianceTrainingRequirementDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _requirementRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Compliance training requirement with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _requirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requirementRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Compliance training requirement with ID '{id}' not found.");

        await _requirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Compliance training requirement {RequirementId} deleted", id);

        return true;
    }

    // ── Employee compliance record queries ────────────────────────────────────

    public async Task<EmployeeComplianceRecordDto> GetRecordByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _recordRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee compliance record with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordDto>> GetRecordsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _recordRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetRecordsForRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        var entities = await _recordRepository.GetByRequirementIdAsync(requirementId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetNonCompliantRecordsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _recordRepository.GetNonCompliantAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetOverdueRecordsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _recordRepository.GetOverdueAsync();
        return entities.ToSummaryDtoList();
    }

    // ── Employee compliance record workflow ───────────────────────────────────

    public async Task<EmployeeComplianceRecordDto> AssignRequirementToEmployeeAsync(Guid employeeId, Guid requirementId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var requirement = await _requirementRepository.GetByIdAsync(requirementId);

        if (requirement == null)
            throw new ArgumentException($"Compliance training requirement with ID '{requirementId}' not found.");

        var existing = await _recordRepository.GetEmployeeRecordAsync(employeeId, requirementId);

        if (existing != null)
            throw new InvalidOperationException($"Employee already has a compliance record for requirement '{requirementId}'.");

        var entity = new EmployeeComplianceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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

        return entity.ToDto();
    }

    public async Task<EmployeeComplianceRecordDto> ExemptEmployeeAsync(ExemptEmployeeComplianceDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _recordRepository.GetByIdAsync(dto.RecordId);

        if (entity == null)
            throw new ArgumentException($"Employee compliance record with ID '{dto.RecordId}' not found.");

        entity.IsExempt = true;
        entity.ExemptionReason = dto.ExemptionReason;
        entity.ExemptedById = dto.ExemptedById;
        entity.ExemptionDate = dto.ExemptionDate;
        entity.ExemptionExpiryDate = dto.ExemptionExpiryDate;
        entity.Status = ComplianceStatus.NotApplicable;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _recordRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee compliance record {RecordId} marked as exempt", dto.RecordId);

        return entity.ToDto();
    }

    public async Task<EmployeeComplianceRecordDto> MarkFulfilledAsync(Guid recordId, Guid nominationId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _recordRepository.GetByIdAsync(recordId);

        if (entity == null)
            throw new ArgumentException($"Employee compliance record with ID '{recordId}' not found.");

        var completedOn = DateTime.UtcNow;
        entity.FulfillingNominationId = nominationId;
        entity.LastCompletedDate = completedOn;
        entity.Status = ComplianceStatus.Compliant;

        // Recompute the next due date from the requirement's recurrence so overdue/expiring
        // queries behave correctly after fulfilment (previously left stale).
        var requirement = await _requirementRepository.GetByIdAsync(entity.RequirementId);
        if (requirement != null)
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

        return entity.ToDto();
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
