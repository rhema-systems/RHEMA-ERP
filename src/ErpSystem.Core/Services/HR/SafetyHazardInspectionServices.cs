using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Hazard Register & Risk Assessment (C) and Inspections (D).
// ============================================================================

#region Hazard Service

public class SheHazardService : ISheHazardService
{
    private readonly ISheHazardRepository _hazardRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheHazardService> _logger;

    public SheHazardService(ISheHazardRepository hazardRepository, IUnitOfWork unitOfWork, ILogger<SheHazardService> logger)
    {
        _hazardRepository = hazardRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SheHazardDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _hazardRepository.GetWithControlsAsync(id)
            ?? throw new ArgumentException($"Hazard with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheHazardDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _hazardRepository.GetByCodeAsync(code);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var entities = activeOnly ? await _hazardRepository.GetActiveAsync() : await _hazardRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByStatusAsync(SheHazardStatus status, CancellationToken cancellationToken = default)
        => (await _hazardRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByCategoryAsync(SheHazardCategory category, CancellationToken cancellationToken = default)
        => (await _hazardRepository.GetByCategoryAsync(category)).ToSummaryDtoList();

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByResidualRiskLevelAsync(SheHazardRiskLevel level, CancellationToken cancellationToken = default)
        => (await _hazardRepository.GetByResidualRiskLevelAsync(level)).ToSummaryDtoList();

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _hazardRepository.GetByLocationAsync(locationId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
        => (await _hazardRepository.GetByOwnerAsync(ownerId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheHazardSummaryDto>> GetHighResidualRiskAsync(int minimumScore, CancellationToken cancellationToken = default)
        => (await _hazardRepository.GetHighResidualRiskAsync(minimumScore)).ToSummaryDtoList();

    public async Task<IEnumerable<SheHazardSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _hazardRepository.GetDueForReviewAsync(daysAhead)).ToSummaryDtoList();

    public async Task<SheHazardDto> CreateAsync(CreateSheHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _hazardRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheHazardDto> UpdateAsync(UpdateSheHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _hazardRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Hazard with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _hazardRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _hazardRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Hazard with ID '{id}' not found.");
        await _hazardRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheHazardControlDto> AddControlAsync(CreateSheHazardControlDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheHazardControl>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheHazardControlDto> UpdateControlAsync(UpdateSheHazardControlDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheHazardControl>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Hazard control with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteControlAsync(Guid controlId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheHazardControl>();
        var entity = await repo.GetByIdAsync(controlId)
            ?? throw new ArgumentException($"Hazard control with ID '{controlId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheHazardCorrectiveActionDto> AddCorrectiveActionAsync(CreateSheHazardCorrectiveActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheHazardCorrectiveAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCorrectiveActionAsync(Guid correctiveActionId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheHazardCorrectiveAction>();
        var entity = await repo.GetByIdAsync(correctiveActionId)
            ?? throw new ArgumentException($"Hazard corrective action with ID '{correctiveActionId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Risk Assessment Service

public class SheRiskAssessmentService : ISheRiskAssessmentService
{
    private readonly ISheRiskAssessmentRepository _assessmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheRiskAssessmentService> _logger;

    public SheRiskAssessmentService(ISheRiskAssessmentRepository assessmentRepository, IUnitOfWork unitOfWork, ILogger<SheRiskAssessmentService> logger)
    {
        _assessmentRepository = assessmentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SheRiskAssessmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Risk assessment with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheRiskAssessmentDto?> GetByNumberAsync(string assessmentNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetByNumberAsync(assessmentNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _assessmentRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByStatusAsync(SheRiskAssessmentStatus status, CancellationToken cancellationToken = default)
        => (await _assessmentRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByTypeAsync(SheRiskAssessmentType type, CancellationToken cancellationToken = default)
        => (await _assessmentRepository.GetByTypeAsync(type)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByPreparerAsync(Guid preparedById, CancellationToken cancellationToken = default)
        => (await _assessmentRepository.GetByPreparerAsync(preparedById)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _assessmentRepository.GetExpiringAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _assessmentRepository.GetDueForReviewAsync(daysAhead)).ToSummaryDtoList();

    public async Task<SheRiskAssessmentDto> CreateAsync(CreateSheRiskAssessmentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        entity.AssessmentNumber = await _assessmentRepository.GetNextAssessmentNumberAsync();
        await _assessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Risk assessment created: {AssessmentNumber}", entity.AssessmentNumber);
        return entity.ToDto();
    }

    public async Task<SheRiskAssessmentDto> UpdateAsync(UpdateSheRiskAssessmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Risk assessment with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _assessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Risk assessment with ID '{id}' not found.");
        await _assessmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAsync(ApproveSheRiskAssessmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetByIdAsync(dto.RiskAssessmentId)
            ?? throw new ArgumentException($"Risk assessment with ID '{dto.RiskAssessmentId}' not found.");

        if (entity.Status is SheRiskAssessmentStatus.Approved or SheRiskAssessmentStatus.Active)
            throw new InvalidOperationException("Risk assessment has already been approved.");

        entity.ApprovedById = dto.ApprovedById;
        entity.ApprovedDate = dto.ApprovedDate;
        entity.ValidFrom = dto.ValidFrom ?? entity.ValidFrom;
        entity.ValidUntil = dto.ValidUntil ?? entity.ValidUntil;
        entity.NextReviewDate = dto.NextReviewDate ?? entity.NextReviewDate;
        entity.Status = SheRiskAssessmentStatus.Approved;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _assessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Risk assessment approved: {AssessmentNumber}", entity.AssessmentNumber);
        return true;
    }

    public async Task<SheRiskAssessmentHazardDto> AddHazardAsync(CreateSheRiskAssessmentHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheRiskAssessmentHazard>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheRiskAssessmentHazardDto> UpdateHazardAsync(UpdateSheRiskAssessmentHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheRiskAssessmentHazard>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Risk assessment hazard with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHazardAsync(Guid hazardLineId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheRiskAssessmentHazard>();
        var entity = await repo.GetByIdAsync(hazardLineId)
            ?? throw new ArgumentException($"Risk assessment hazard with ID '{hazardLineId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheRiskAssessmentAcknowledgementDto> AddAcknowledgementAsync(CreateSheRiskAssessmentAcknowledgementDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheRiskAssessmentAcknowledgement>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<MyRiskAcknowledgementDto>> GetForEmployeeAcknowledgementAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var assessments = await _assessmentRepository.GetActiveForAcknowledgementAsync();
        return assessments.Select(r =>
        {
            var ack = r.Acknowledgements.FirstOrDefault(a => a.EmployeeId == employeeId && !a.IsDeleted);
            return new MyRiskAcknowledgementDto
            {
                RiskAssessmentId = r.Id,
                AssessmentNumber = r.AssessmentNumber,
                Title = r.Title,
                Type = r.Type,
                LocationName = r.Location?.Name,
                ValidUntil = r.ValidUntil,
                AcknowledgedByMe = ack != null,
                AcknowledgedDate = ack?.AcknowledgedDate,
            };
        }).ToList();
    }
}

#endregion

#region Inspection Checklist Service

public class SheInspectionChecklistService : ISheInspectionChecklistService
{
    private readonly ISheInspectionChecklistRepository _checklistRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheInspectionChecklistService> _logger;

    public SheInspectionChecklistService(ISheInspectionChecklistRepository checklistRepository, IUnitOfWork unitOfWork, ILogger<SheInspectionChecklistService> logger)
    {
        _checklistRepository = checklistRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SheInspectionChecklistDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _checklistRepository.GetWithItemsAsync(id)
            ?? throw new ArgumentException($"Inspection checklist with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheInspectionChecklistDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var entities = activeOnly ? await _checklistRepository.GetActiveAsync() : await _checklistRepository.GetAllAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheInspectionChecklistDto>> GetByTypeAsync(SheInspectionType type, CancellationToken cancellationToken = default)
        => (await _checklistRepository.GetByTypeAsync(type)).Select(e => e.ToDto());

    public async Task<SheInspectionChecklistDto> CreateAsync(CreateSheInspectionChecklistDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _checklistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistDto> UpdateAsync(UpdateSheInspectionChecklistDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _checklistRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Inspection checklist with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _checklistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _checklistRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Inspection checklist with ID '{id}' not found.");
        await _checklistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheInspectionChecklistItemDto> AddItemAsync(CreateSheInspectionChecklistItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheInspectionChecklistItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistItemDto> UpdateItemAsync(UpdateSheInspectionChecklistItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheInspectionChecklistItem>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Checklist item with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SheInspectionChecklistItem>();
        var entity = await repo.GetByIdAsync(itemId)
            ?? throw new ArgumentException($"Checklist item with ID '{itemId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Inspection Service

public class SafetyInspectionService : ISafetyInspectionService
{
    private readonly ISafetyInspectionRepository _inspectionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyInspectionService> _logger;

    public SafetyInspectionService(ISafetyInspectionRepository inspectionRepository, IUnitOfWork unitOfWork, ILogger<SafetyInspectionService> logger)
    {
        _inspectionRepository = inspectionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SafetyInspectionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _inspectionRepository.GetWithFullDetailsAsync(id)
            ?? throw new ArgumentException($"Safety inspection with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SafetyInspectionDto?> GetByNumberAsync(string inspectionNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _inspectionRepository.GetByNumberAsync(inspectionNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByStatusAsync(SheInspectionStatus status, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByTypeAsync(SheInspectionType type, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByTypeAsync(type)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByCategoryAsync(SheInspectionCategory category, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByCategoryAsync(category)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByDateRangeAsync(fromDate, toDate)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByInspectorAsync(Guid inspectorId, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByInspectorAsync(inspectorId)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetByLocationAsync(locationId)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetDueAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetDueAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetOpenWithFindingsAsync(CancellationToken cancellationToken = default)
        => (await _inspectionRepository.GetOpenWithFindingsAsync()).ToSummaryDtoList();

    public async Task<SafetyInspectionDto> CreateAsync(CreateSafetyInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        entity.InspectionNumber = await _inspectionRepository.GetNextInspectionNumberAsync();
        await _inspectionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety inspection created: {InspectionNumber}", entity.InspectionNumber);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionDto> UpdateAsync(UpdateSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _inspectionRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Safety inspection with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _inspectionRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Safety inspection with ID '{id}' not found.");
        await _inspectionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CloseAsync(CloseSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _inspectionRepository.GetByIdAsync(dto.InspectionId)
            ?? throw new ArgumentException($"Safety inspection with ID '{dto.InspectionId}' not found.");

        entity.Status = SheInspectionStatus.Closed;
        entity.ClosedById = dto.ClosedById;
        entity.ClosedDate = dto.ClosedDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Items ──
    public async Task<SafetyInspectionItemDto> AddItemAsync(CreateSafetyInspectionItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionItemDto> UpdateItemAsync(UpdateSafetyInspectionItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyInspectionItem>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Inspection item with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyInspectionItem>();
        var entity = await repo.GetByIdAsync(itemId)
            ?? throw new ArgumentException($"Inspection item with ID '{itemId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Hazards ──
    public async Task<SafetyInspectionHazardDto> AddHazardAsync(CreateSafetyInspectionHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionHazard>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionHazardDto> UpdateHazardAsync(UpdateSafetyInspectionHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyInspectionHazard>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Inspection hazard with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHazardAsync(Guid inspectionHazardId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyInspectionHazard>();
        var entity = await repo.GetByIdAsync(inspectionHazardId)
            ?? throw new ArgumentException($"Inspection hazard with ID '{inspectionHazardId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Hazard actions ──
    public async Task<SafetyInspectionHazardActionDto> AddHazardActionAsync(CreateSafetyInspectionHazardActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionHazardAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionHazardActionDto> UpdateHazardActionAsync(UpdateSafetyInspectionHazardActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyInspectionHazardAction>();
        var entity = await repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Inspection hazard action with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHazardActionAsync(Guid hazardActionId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyInspectionHazardAction>();
        var entity = await repo.GetByIdAsync(hazardActionId)
            ?? throw new ArgumentException($"Inspection hazard action with ID '{hazardActionId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Documents ──
    public async Task<SafetyInspectionDocumentDto> AddDocumentAsync(CreateSafetyInspectionDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyInspectionDocument>();
        var entity = await repo.GetByIdAsync(documentId)
            ?? throw new ArgumentException($"Inspection document with ID '{documentId}' not found.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
