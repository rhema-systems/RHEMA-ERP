using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Hazard Register & Risk Assessment (C) and Inspections (D).
// ============================================================================

#region Hazard Service

public class SheHazardService : ISheHazardService
{
    private readonly ISheHazardRepository _hazardRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheHazardService> _logger;

    public SheHazardService(
        ISheHazardRepository hazardRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheHazardService> logger)
    {
        _hazardRepository = hazardRepository;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    // A hazard owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<SheHazard> GetOwnedHazardAsync(Guid id)
    {
        var entity = await _hazardRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Hazard with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheHazardControl> GetOwnedControlAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheHazardControl>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Hazard control with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheHazardCorrectiveAction> GetOwnedCorrectiveActionAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheHazardCorrectiveAction>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Hazard corrective action with ID '{id}' not found.");
        return entity;
    }

    public async Task<SheHazardDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _hazardRepository.GetWithControlsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Hazard with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheHazardDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // Codes are unique per tenant, so a match owned by another tenant is reported as no match.
        var entity = await _hazardRepository.GetQueryable()
            .Include(h => h.Location)
            .Include(h => h.Owner)
            .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.Code == code && !h.IsDeleted, cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _hazardRepository.GetActiveAsync() : await _hazardRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByStatusAsync(SheHazardStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hazardRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByCategoryAsync(SheHazardCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hazardRepository.GetByCategoryAsync(category))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByResidualRiskLevelAsync(SheHazardRiskLevel level, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hazardRepository.GetByResidualRiskLevelAsync(level))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hazardRepository.GetByLocationAsync(locationId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hazardRepository.GetByOwnerAsync(ownerId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetHighResidualRiskAsync(int minimumScore, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hazardRepository.GetHighResidualRiskAsync(minimumScore))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hazardRepository.GetDueForReviewAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<SheHazardDto> CreateAsync(CreateSheHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var duplicate = await _hazardRepository.GetQueryable()
                .AnyAsync(h => h.TenantId == tenantId && h.Code == dto.Code && !h.IsDeleted, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A hazard with code '{dto.Code}' already exists.");
        }

        var entity = dto.ToEntity(tenantId, userId);
        await _hazardRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheHazardDto> UpdateAsync(UpdateSheHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _hazardRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardAsync(id);
        await _hazardRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheHazardControlDto> AddControlAsync(CreateSheHazardControlDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedHazardAsync(dto.HazardId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheHazardControl>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheHazardControlDto> UpdateControlAsync(UpdateSheHazardControlDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedControlAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheHazardControl>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteControlAsync(Guid controlId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedControlAsync(controlId);
        await _unitOfWork.Repository<SheHazardControl>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheHazardCorrectiveActionDto> AddCorrectiveActionAsync(CreateSheHazardCorrectiveActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedHazardAsync(dto.HazardId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheHazardCorrectiveAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCorrectiveActionAsync(Guid correctiveActionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCorrectiveActionAsync(correctiveActionId);
        await _unitOfWork.Repository<SheHazardCorrectiveAction>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Risk Assessment Service

public class SheRiskAssessmentService : ISheRiskAssessmentService
{
    private readonly ISheRiskAssessmentRepository _assessmentRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheRiskAssessmentService> _logger;

    public SheRiskAssessmentService(
        ISheRiskAssessmentRepository assessmentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheRiskAssessmentService> logger)
    {
        _assessmentRepository = assessmentRepository;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheRiskAssessment> GetOwnedAssessmentAsync(Guid id)
    {
        var entity = await _assessmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Risk assessment with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheRiskAssessmentHazard> GetOwnedHazardLineAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheRiskAssessmentHazard>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Risk assessment hazard with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GenerateAssessmentNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"RA-{year}-";
        var last = await _assessmentRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AssessmentNumber.StartsWith(prefix))
            .OrderByDescending(r => r.AssessmentNumber)
            .Select(r => r.AssessmentNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }

    public async Task<SheRiskAssessmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _assessmentRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Risk assessment with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheRiskAssessmentDto?> GetByNumberAsync(string assessmentNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // Assessment numbers are unique per tenant, so a match owned by another tenant is reported as no match.
        var entity = await _assessmentRepository.GetByNumberAsync(assessmentNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _assessmentRepository.GetAllSummaryAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByStatusAsync(SheRiskAssessmentStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _assessmentRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByTypeAsync(SheRiskAssessmentType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _assessmentRepository.GetByTypeAsync(type))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetByPreparerAsync(Guid preparedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _assessmentRepository.GetByPreparerAsync(preparedById))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _assessmentRepository.GetExpiringAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheRiskAssessmentSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _assessmentRepository.GetDueForReviewAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<SheRiskAssessmentDto> CreateAsync(CreateSheRiskAssessmentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var entity = dto.ToEntity(tenantId, userId);
        entity.AssessmentNumber = await GenerateAssessmentNumberAsync(tenantId, cancellationToken);
        await _assessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Risk assessment created: {AssessmentNumber}", entity.AssessmentNumber);
        return entity.ToDto();
    }

    public async Task<SheRiskAssessmentDto> UpdateAsync(UpdateSheRiskAssessmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssessmentAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _assessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssessmentAsync(id);
        await _assessmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAsync(ApproveSheRiskAssessmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssessmentAsync(dto.RiskAssessmentId);

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
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedAssessmentAsync(dto.RiskAssessmentId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheRiskAssessmentHazard>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheRiskAssessmentHazardDto> UpdateHazardAsync(UpdateSheRiskAssessmentHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardLineAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheRiskAssessmentHazard>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHazardAsync(Guid hazardLineId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardLineAsync(hazardLineId);
        await _unitOfWork.Repository<SheRiskAssessmentHazard>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheRiskAssessmentAcknowledgementDto> AddAcknowledgementAsync(CreateSheRiskAssessmentAcknowledgementDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedAssessmentAsync(dto.RiskAssessmentId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheRiskAssessmentAcknowledgement>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<MyRiskAcknowledgementDto>> GetForEmployeeAcknowledgementAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var assessments = (await _assessmentRepository.GetActiveForAcknowledgementAsync())
            .Where(r => r.TenantId == tenantId);
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheInspectionChecklistService> _logger;

    public SheInspectionChecklistService(
        ISheInspectionChecklistRepository checklistRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheInspectionChecklistService> logger)
    {
        _checklistRepository = checklistRepository;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheInspectionChecklist> GetOwnedChecklistAsync(Guid id)
    {
        var entity = await _checklistRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Inspection checklist with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheInspectionChecklistItem> GetOwnedItemAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SheInspectionChecklistItem>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Checklist item with ID '{id}' not found.");
        return entity;
    }

    public async Task<SheInspectionChecklistDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _checklistRepository.GetWithItemsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Inspection checklist with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheInspectionChecklistDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _checklistRepository.GetActiveAsync() : await _checklistRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheInspectionChecklistDto>> GetByTypeAsync(SheInspectionType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _checklistRepository.GetByTypeAsync(type))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SheInspectionChecklistDto> CreateAsync(CreateSheInspectionChecklistDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Checklist numbers are unique per tenant: an unscoped check would let one tenant's numbers block another's.
        var duplicate = await _checklistRepository.GetQueryable()
            .AnyAsync(c => c.TenantId == tenantId && c.ChecklistNumber == dto.ChecklistNumber && !c.IsDeleted, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"An inspection checklist with number '{dto.ChecklistNumber}' already exists.");

        var entity = dto.ToEntity(tenantId, userId);
        await _checklistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistDto> UpdateAsync(UpdateSheInspectionChecklistDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _checklistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistAsync(id);
        await _checklistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SheInspectionChecklistItemDto> AddItemAsync(CreateSheInspectionChecklistItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedChecklistAsync(dto.ChecklistId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheInspectionChecklistItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistItemDto> UpdateItemAsync(UpdateSheInspectionChecklistItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheInspectionChecklistItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);
        await _unitOfWork.Repository<SheInspectionChecklistItem>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Inspection Service

public class SafetyInspectionService : ISafetyInspectionService
{
    private readonly ISafetyInspectionRepository _inspectionRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyInspectionService> _logger;

    public SafetyInspectionService(
        ISafetyInspectionRepository inspectionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SafetyInspectionService> logger)
    {
        _inspectionRepository = inspectionRepository;
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

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SafetyInspection> GetOwnedInspectionAsync(Guid id)
    {
        var entity = await _inspectionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Safety inspection with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyInspectionItem> GetOwnedItemAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyInspectionItem>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Inspection item with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyInspectionHazard> GetOwnedHazardAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyInspectionHazard>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Inspection hazard with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyInspectionHazardAction> GetOwnedHazardActionAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyInspectionHazardAction>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Inspection hazard action with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyInspectionDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyInspectionDocument>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Inspection document with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GenerateInspectionNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INSP-{year}-";
        var last = await _inspectionRepository.GetQueryable()
            .Where(i => i.TenantId == tenantId && i.InspectionNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InspectionNumber)
            .Select(i => i.InspectionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }

    public async Task<SafetyInspectionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _inspectionRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Safety inspection with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SafetyInspectionDto?> GetByNumberAsync(string inspectionNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // Inspection numbers are unique per tenant, so a match owned by another tenant is reported as no match.
        var entity = await _inspectionRepository.GetByNumberAsync(inspectionNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetAllSummaryAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByStatusAsync(SheInspectionStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetByStatusAsync(status))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByTypeAsync(SheInspectionType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetByTypeAsync(type))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByCategoryAsync(SheInspectionCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetByCategoryAsync(category))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetByDateRangeAsync(fromDate, toDate))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByInspectorAsync(Guid inspectorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetByInspectorAsync(inspectorId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetByLocationAsync(locationId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetDueAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetDueAsync(daysAhead))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyInspectionSummaryDto>> GetOpenWithFindingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _inspectionRepository.GetOpenWithFindingsAsync())
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<SafetyInspectionDto> CreateAsync(CreateSafetyInspectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var entity = dto.ToEntity(tenantId, userId);
        entity.InspectionNumber = await GenerateInspectionNumberAsync(tenantId, cancellationToken);
        await _inspectionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety inspection created: {InspectionNumber}", entity.InspectionNumber);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionDto> UpdateAsync(UpdateSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionAsync(id);
        await _inspectionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CloseAsync(CloseSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionAsync(dto.InspectionId);

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
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedInspectionAsync(dto.InspectionId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionItemDto> UpdateItemAsync(UpdateSafetyInspectionItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyInspectionItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(itemId);
        await _unitOfWork.Repository<SafetyInspectionItem>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Hazards ──
    public async Task<SafetyInspectionHazardDto> AddHazardAsync(CreateSafetyInspectionHazardDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedInspectionAsync(dto.InspectionId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionHazard>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionHazardDto> UpdateHazardAsync(UpdateSafetyInspectionHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyInspectionHazard>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHazardAsync(Guid inspectionHazardId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardAsync(inspectionHazardId);
        await _unitOfWork.Repository<SafetyInspectionHazard>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Hazard actions ──
    public async Task<SafetyInspectionHazardActionDto> AddHazardActionAsync(CreateSafetyInspectionHazardActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedHazardAsync(dto.InspectionHazardId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionHazardAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyInspectionHazardActionDto> UpdateHazardActionAsync(UpdateSafetyInspectionHazardActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardActionAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyInspectionHazardAction>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHazardActionAsync(Guid hazardActionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardActionAsync(hazardActionId);
        await _unitOfWork.Repository<SafetyInspectionHazardAction>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Documents ──
    public async Task<SafetyInspectionDocumentDto> AddDocumentAsync(CreateSafetyInspectionDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedInspectionAsync(dto.InspectionId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);
        await _unitOfWork.Repository<SafetyInspectionDocument>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
