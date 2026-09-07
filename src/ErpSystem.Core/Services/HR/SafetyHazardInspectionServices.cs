using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
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
            .Include(h => h.ReportedBy)
            .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.Code == code && !h.IsDeleted, cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheHazardSummaryDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // GetAllListAsync, not the generic GetAllAsync: the summary rows carry location and owner
        // names, and the generic read loads no navigations, so they came back blank.
        var entities = activeOnly ? await _hazardRepository.GetActiveAsync() : await _hazardRepository.GetAllListAsync();
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
        // Write responses re-read through the include-bearing path — the tracked entity's navs
        // (location, owner) are unloaded and would map blank.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SheHazardDto> UpdateAsync(UpdateSheHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _hazardRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
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
        // Child write responses re-read through the parent's include-bearing path — the tracked
        // entity's responsible-person nav is unloaded and would map blank.
        var full = await GetByIdAsync(dto.HazardId, cancellationToken);
        return full.Controls.First(c => c.Id == entity.Id);
    }

    public async Task<SheHazardControlDto> UpdateControlAsync(UpdateSheHazardControlDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedControlAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheHazardControl>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var full = await GetByIdAsync(entity.HazardId, cancellationToken);
        return full.Controls.First(c => c.Id == entity.Id);
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
        // Re-read for the template title — the tracked entity's template nav is unloaded.
        var full = await GetByIdAsync(dto.HazardId, cancellationToken);
        return full.CorrectiveActions.First(a => a.Id == entity.Id);
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

        // Numeric max, not string ordering: the seeder wrote 3-digit suffixes (RA-2026-001) while
        // this generator emits 4-digit ones, and across mixed widths string ordering picks the
        // wrong "latest" ("002" sorts above "0003"), silently re-issuing taken numbers.
        // Soft-deleted assessments keep their number, so they count toward the max too.
        var numbers = await _assessmentRepository
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId && r.AssessmentNumber.StartsWith(prefix))
            .Select(r => r.AssessmentNumber)
            .ToListAsync(cancellationToken);

        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{max + 1:D4}";
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
        // Write responses re-read through the include-bearing path — the tracked entity's navs
        // (location, org unit, preparer) are unloaded and would map blank.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SheRiskAssessmentDto> UpdateAsync(UpdateSheRiskAssessmentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssessmentAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _assessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
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
        // A retired assessment cannot be quietly revived through approval — it needs a new version.
        if (entity.Status is SheRiskAssessmentStatus.Expired or SheRiskAssessmentStatus.Superseded or SheRiskAssessmentStatus.Withdrawn)
            throw new InvalidOperationException($"A {entity.Status} risk assessment cannot be approved. Prepare a new version instead.");

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
        var assessment = await GetOwnedAssessmentAsync(dto.RiskAssessmentId);

        // Signing proves the worker read the APPROVED document — a draft or retired assessment
        // has nothing valid to acknowledge, and the endpoint is open to every employee.
        if (assessment.Status is not (SheRiskAssessmentStatus.Approved or SheRiskAssessmentStatus.Active))
            throw new InvalidOperationException("Only an approved or active risk assessment can be acknowledged.");

        // The endpoint is open to every employee; without this guard a repeat sign quietly stacks duplicate rows.
        var alreadySigned = await _unitOfWork.Repository<SheRiskAssessmentAcknowledgement>().GetQueryable()
            .AnyAsync(a => a.RiskAssessmentId == dto.RiskAssessmentId && a.EmployeeId == dto.EmployeeId && !a.IsDeleted, cancellationToken);
        if (alreadySigned)
            throw new InvalidOperationException("This risk assessment has already been acknowledged by this employee.");

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheRiskAssessmentAcknowledgement>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Re-read for the employee name — the signature is confirmation UI, and a blank name on
        // it reads as "someone else signed".
        var full = await GetByIdAsync(dto.RiskAssessmentId, cancellationToken);
        return full.Acknowledgements.First(a => a.Id == entity.Id);
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

/// <summary>
/// Inspection checklist templates and the builder that shapes them
/// (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md). Structure — fields, sections, items,
/// outcomes, signatories and the header's scoring / print columns — is writable only while the template
/// is Draft. Publishing validates and freezes it; "new version" clones it under the same number.
/// </summary>
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

    /// <summary>The template with its whole structure loaded, or 404.</summary>
    private async Task<SheInspectionChecklist> GetOwnedChecklistAsync(Guid id)
    {
        var entity = await _checklistRepository.GetWithItemsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Inspection checklist with ID '{id}' not found.");
        return entity;
    }

    /// <summary>The template, and it must still be editable.</summary>
    private async Task<SheInspectionChecklist> GetOwnedDraftAsync(Guid id)
    {
        var entity = await GetOwnedChecklistAsync(id);
        EnsureDraft(entity);
        return entity;
    }

    private static void EnsureDraft(SheInspectionChecklist e)
    {
        if (e.Status != SheChecklistStatus.Draft)
            throw new InvalidOperationException(
                $"Checklist {e.ChecklistNumber} v{e.Version} is {e.Status}; its structure is locked. Create a new version to change it.");
    }

    private async Task<T> GetOwnedChildAsync<T>(Guid id, string what) where T : TenantEntity
    {
        var entity = await _unitOfWork.Repository<T>().GetByIdAsync(id);
        if (entity == null || entity.IsDeleted || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Checklist {what} with ID '{id}' not found.");
        return entity;
    }

    private static string Describe(SheInspectionChecklist e) => $"{e.ChecklistNumber} v{e.Version}";

    // ── Reads ──

    public async Task<SheInspectionChecklistDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => (await GetOwnedChecklistAsync(id)).ToDto();

    public async Task<IEnumerable<SheInspectionChecklistDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (activeOnly)
            return (await _checklistRepository.GetActiveAsync()).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());

        // Items ride along for the ItemCount column; the rest of the structure is a detail-read concern.
        var entities = await _checklistRepository.GetQueryable()
            .Include(c => c.Items)
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .OrderBy(c => c.ChecklistNumber).ThenByDescending(c => c.Version)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheInspectionChecklistDto>> GetByTypeAsync(SheInspectionType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _checklistRepository.GetByTypeAsync(type))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    // ── Header ──

    public async Task<SheInspectionChecklistDto> CreateAsync(CreateSheInspectionChecklistDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Number + version is unique per tenant (versions share the number): an unscoped check would let
        // one tenant's numbers block another's. Soft-deleted rows still hold the unique index, so they
        // count too — otherwise the insert fails with a 500 instead of this 422.
        var duplicate = await _checklistRepository
            .GetQueryableIncludingDeleted(c => c.TenantId == tenantId && c.ChecklistNumber == dto.ChecklistNumber && c.Version == dto.Version)
            .AnyAsync(cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"An inspection checklist with number '{dto.ChecklistNumber}' (version {dto.Version}) already exists.");

        var entity = dto.ToEntity(tenantId, userId);
        await _checklistRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistDto> UpdateAsync(UpdateSheInspectionChecklistDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistAsync(dto.Id);
        if (entity.Status != SheChecklistStatus.Draft && entity.ChangesLockedStructure(dto))
            throw new InvalidOperationException(
                $"Checklist {Describe(entity)} is {entity.Status}; only its name, description and active flag can change. Create a new version to change the form.");
        entity.UpdateEntity(dto, userId);
        await _checklistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistAsync(id);
        var inUse = await _unitOfWork.Repository<SafetyInspection>().CountAsync(i => i.ChecklistId == id && !i.IsDeleted);
        if (inUse > 0)
            throw new InvalidOperationException($"Checklist {Describe(entity)} is referenced by {inUse} inspection(s) and cannot be deleted. Retire it instead.");
        await _checklistRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Lifecycle ──

    public async Task<SheInspectionChecklistDto> PublishAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistAsync(id);
        if (entity.Status == SheChecklistStatus.Published)
            throw new InvalidOperationException($"Checklist {Describe(entity)} is already published.");
        if (entity.Status == SheChecklistStatus.Retired)
            throw new InvalidOperationException($"Checklist {Describe(entity)} is retired. Create a new version to publish a change.");

        var problems = ValidateForPublish(entity);
        if (problems.Count > 0)
            throw new InvalidOperationException($"Checklist {Describe(entity)} cannot be published: {string.Join(" ", problems)}");

        entity.Status = SheChecklistStatus.Published;
        entity.PublishedAt = DateTime.UtcNow;
        entity.PublishedById = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        // One published version per number: the one being replaced retires as this one goes live.
        var siblings = await _checklistRepository.GetQueryable()
            .Where(c => c.TenantId == entity.TenantId && c.ChecklistNumber == entity.ChecklistNumber
                     && c.Id != entity.Id && c.Status == SheChecklistStatus.Published && !c.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var sibling in siblings)
        {
            sibling.Status = SheChecklistStatus.Retired;
            sibling.RetiredAt = DateTime.UtcNow;
            sibling.IsActive = false;
            sibling.UpdatedAt = DateTime.UtcNow;
            sibling.UpdatedBy = userId.ToString();
            await _checklistRepository.UpdateAsync(sibling);
        }

        await _checklistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Inspection checklist published: {Number} v{Version}", entity.ChecklistNumber, entity.Version);
        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>The publish gate — every rule the form must satisfy before it can be run against.</summary>
    private static List<string> ValidateForPublish(SheInspectionChecklist e)
    {
        var problems = new List<string>();
        var sections = e.Sections.Where(s => !s.IsDeleted).ToList();
        var items = e.Items.Where(i => !i.IsDeleted).ToList();
        var outcomes = e.Outcomes.Where(o => !o.IsDeleted).ToList();

        var standard = sections.Where(s => s.Kind == SheChecklistSectionKind.Standard).ToList();
        if (!standard.Any(s => items.Any(i => i.SectionId == s.Id)))
            problems.Add("It needs at least one standard section with at least one item.");

        var orphans = items.Count(i => i.SectionId == null);
        if (orphans > 0)
            problems.Add($"{orphans} item(s) are not in a section.");

        foreach (var s in sections.Where(s => !items.Any(i => i.SectionId == s.Id)))
            problems.Add($"Section '{s.Title}' has no items.");

        var hasCritical = sections.Any(s => s.Kind == SheChecklistSectionKind.Critical);
        var disqualifying = outcomes.Count(o => o.IsDisqualifying);
        if (disqualifying > 1)
            problems.Add("Only one outcome may be marked as the disqualifying outcome.");

        switch (e.ScoringMode)
        {
            case SheChecklistScoringMode.CompliancePercentage:
            {
                var bands = outcomes.Where(o => o.MinPercent != null).OrderBy(o => o.MinPercent).ToList();
                if (bands.Count == 0)
                    problems.Add("Percentage scoring needs at least one outcome with a score band.");
                else
                {
                    if (bands[0].MinPercent != 0m)
                        problems.Add("One score band must start at 0%.");
                    for (var i = 0; i < bands.Count; i++)
                    {
                        var b = bands[i];
                        if (b.MaxPercent != null && b.MaxPercent < b.MinPercent)
                            problems.Add($"Outcome '{b.Label}': the band's upper bound is below its lower bound.");
                        if (i > 0 && bands[i - 1].MinPercent == b.MinPercent)
                            problems.Add($"Outcomes '{bands[i - 1].Label}' and '{b.Label}' start at the same percentage.");
                        if (i > 0 && bands[i - 1].MaxPercent != null && bands[i - 1].MaxPercent >= b.MinPercent)
                            problems.Add($"Outcomes '{bands[i - 1].Label}' and '{b.Label}' overlap.");
                    }
                }
                if (hasCritical && disqualifying != 1)
                    problems.Add("A critical section needs exactly one outcome marked as the disqualifying outcome.");
                break;
            }
            case SheChecklistScoringMode.QualitativeRating:
                if (outcomes.Count < 2)
                    problems.Add("Qualitative rating needs at least two outcomes to choose from.");
                if (outcomes.Any(o => o.MinPercent != null || o.MaxPercent != null))
                    problems.Add("Qualitative rating outcomes do not carry score bands.");
                if (hasCritical && disqualifying != 1)
                    problems.Add("A critical section needs exactly one outcome marked as the disqualifying outcome.");
                break;
            case SheChecklistScoringMode.None:
                if (outcomes.Count > 0)
                    problems.Add("A template without scoring has no outcomes; remove them or choose a scoring mode.");
                break;
        }

        foreach (var f in e.Fields.Where(f => !f.IsDeleted && f.FieldType == SheChecklistFieldType.Choice))
            if (SheChecklistChoiceOptions.Split(f.ChoiceOptions).Count < 2)
                problems.Add($"Choice field '{f.Label}' needs at least two options.");

        return problems;
    }

    public async Task<SheInspectionChecklistDto> RetireAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChecklistAsync(id);
        if (entity.Status != SheChecklistStatus.Published)
            throw new InvalidOperationException($"Checklist {Describe(entity)} is {entity.Status}; only a published template can be retired.");
        entity.Status = SheChecklistStatus.Retired;
        entity.RetiredAt = DateTime.UtcNow;
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
        await _checklistRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<SheInspectionChecklistDto> CreateNewVersionAsync(Guid id, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var source = await GetOwnedChecklistAsync(id);
        if (source.Status == SheChecklistStatus.Draft)
            throw new InvalidOperationException($"Checklist {Describe(source)} is still a draft — edit it directly.");

        // Soft-deleted versions keep their (number, version) slot in the unique index, so the next
        // version number is taken over every row that ever existed; the draft guard is over live rows.
        var family = await _checklistRepository
            .GetQueryableIncludingDeleted(c => c.TenantId == tenantId && c.ChecklistNumber == source.ChecklistNumber)
            .Select(c => new { c.Version, c.Status, c.IsDeleted })
            .ToListAsync(cancellationToken);
        if (family.Any(c => !c.IsDeleted && c.Status == SheChecklistStatus.Draft))
            throw new InvalidOperationException($"Checklist {source.ChecklistNumber} already has a draft version; finish or delete it first.");

        var clone = new SheInspectionChecklist
        {
            TenantId = tenantId,
            ChecklistNumber = source.ChecklistNumber,
            Name = source.Name,
            Description = source.Description,
            Type = source.Type,
            Version = family.Max(c => c.Version) + 1,
            IsActive = true,
            Status = SheChecklistStatus.Draft,
            ScoringMode = source.ScoringMode,
            AllowPartialCompliance = source.AllowPartialCompliance,
            PrintTitle = source.PrintTitle,
            PrintSubtitle = source.PrintSubtitle,
            Instructions = source.Instructions,
            CriticalSectionNote = source.CriticalSectionNote,
            PreviousVersionId = source.Id,
            CreatedBy = userId.ToString(),
        };

        foreach (var f in source.Fields.Where(x => !x.IsDeleted))
            clone.Fields.Add(new SheInspectionChecklistField
            {
                TenantId = tenantId, DisplayOrder = f.DisplayOrder, Label = f.Label, FieldType = f.FieldType,
                IsRequired = f.IsRequired, ChoiceOptions = f.ChoiceOptions, HelpText = f.HelpText, CreatedBy = userId.ToString(),
            });

        var sectionMap = new Dictionary<Guid, SheInspectionChecklistSection>();
        foreach (var s in source.Sections.Where(x => !x.IsDeleted))
        {
            var ns = new SheInspectionChecklistSection
            {
                TenantId = tenantId, DisplayOrder = s.DisplayOrder, Code = s.Code, Title = s.Title,
                Description = s.Description, Kind = s.Kind, CreatedBy = userId.ToString(),
            };
            sectionMap[s.Id] = ns;
            clone.Sections.Add(ns);
        }

        foreach (var i in source.Items.Where(x => !x.IsDeleted))
        {
            var ni = new SheInspectionChecklistItem
            {
                TenantId = tenantId, ItemOrder = i.ItemOrder, Category = i.Category, ItemDescription = i.ItemDescription,
                IsMandatory = i.IsMandatory, RegulatoryReference = i.RegulatoryReference,
                AssociatedRiskLevel = i.AssociatedRiskLevel, CreatedBy = userId.ToString(),
            };
            if (i.SectionId != null && sectionMap.TryGetValue(i.SectionId.Value, out var ns))
                ni.Section = ns;
            clone.Items.Add(ni);
        }

        foreach (var o in source.Outcomes.Where(x => !x.IsDeleted))
            clone.Outcomes.Add(new SheInspectionChecklistOutcome
            {
                TenantId = tenantId, DisplayOrder = o.DisplayOrder, Label = o.Label, Description = o.Description,
                MinPercent = o.MinPercent, MaxPercent = o.MaxPercent, ReinspectionWithinDays = o.ReinspectionWithinDays,
                IsDisqualifying = o.IsDisqualifying, CreatedBy = userId.ToString(),
            });

        foreach (var sg in source.Signatories.Where(x => !x.IsDeleted))
            clone.Signatories.Add(new SheInspectionChecklistSignatory
            {
                TenantId = tenantId, DisplayOrder = sg.DisplayOrder, RoleLabel = sg.RoleLabel, Kind = sg.Kind,
                IsRequired = sg.IsRequired, CreatedBy = userId.ToString(),
            });

        await _checklistRepository.AddAsync(clone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Inspection checklist {Number} cloned to v{Version}", clone.ChecklistNumber, clone.Version);
        return await GetByIdAsync(clone.Id, cancellationToken);
    }

    // ── Items ──

    public async Task<SheInspectionChecklistItemDto> AddItemAsync(CreateSheInspectionChecklistItemDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var checklist = await GetOwnedDraftAsync(dto.ChecklistId);
        var section = ResolveSection(checklist, dto.SectionId);

        var entity = dto.ToEntity(tenantId, userId);
        if (string.IsNullOrWhiteSpace(entity.Category))
            entity.Category = section?.Title;
        await _unitOfWork.Repository<SheInspectionChecklistItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistItemDto> UpdateItemAsync(UpdateSheInspectionChecklistItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistItem>(dto.Id, "item");
        var checklist = await GetOwnedDraftAsync(entity.ChecklistId);
        var section = ResolveSection(checklist, dto.SectionId);
        entity.UpdateEntity(dto, userId);
        if (string.IsNullOrWhiteSpace(entity.Category))
            entity.Category = section?.Title;
        await _unitOfWork.Repository<SheInspectionChecklistItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistItem>(itemId, "item");
        await GetOwnedDraftAsync(entity.ChecklistId);
        await _unitOfWork.Repository<SheInspectionChecklistItem>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SheInspectionChecklistSection? ResolveSection(SheInspectionChecklist checklist, Guid? sectionId)
    {
        if (sectionId == null) return null;
        var section = checklist.Sections.FirstOrDefault(s => s.Id == sectionId && !s.IsDeleted);
        if (section == null)
            throw new InvalidOperationException($"Section '{sectionId}' does not belong to checklist {Describe(checklist)}.");
        return section;
    }

    // ── Fields ──

    public async Task<SheInspectionChecklistFieldDto> AddFieldAsync(CreateSheInspectionChecklistFieldDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedDraftAsync(dto.ChecklistId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheInspectionChecklistField>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistFieldDto> UpdateFieldAsync(UpdateSheInspectionChecklistFieldDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistField>(dto.Id, "field");
        await GetOwnedDraftAsync(entity.ChecklistId);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheInspectionChecklistField>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteFieldAsync(Guid fieldId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistField>(fieldId, "field");
        await GetOwnedDraftAsync(entity.ChecklistId);
        await _unitOfWork.Repository<SheInspectionChecklistField>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Sections ──

    public async Task<SheInspectionChecklistSectionDto> AddSectionAsync(CreateSheInspectionChecklistSectionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedDraftAsync(dto.ChecklistId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheInspectionChecklistSection>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistSectionDto> UpdateSectionAsync(UpdateSheInspectionChecklistSectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistSection>(dto.Id, "section");
        var checklist = await GetOwnedDraftAsync(entity.ChecklistId);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheInspectionChecklistSection>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var dtoOut = entity.ToDto();
        dtoOut.Items = checklist.Items.Where(i => i.SectionId == entity.Id && !i.IsDeleted).OrderBy(i => i.ItemOrder).Select(i => i.ToDto()).ToList();
        return dtoOut;
    }

    public async Task<bool> DeleteSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistSection>(sectionId, "section");
        var checklist = await GetOwnedDraftAsync(entity.ChecklistId);
        // The section owns its items on the form; the FK is Restrict, so they go explicitly.
        var items = checklist.Items.Where(i => i.SectionId == sectionId && !i.IsDeleted).ToList();
        if (items.Count > 0)
            await _unitOfWork.Repository<SheInspectionChecklistItem>().DeleteRangeAsync(items);
        await _unitOfWork.Repository<SheInspectionChecklistSection>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Outcomes ──

    public async Task<SheInspectionChecklistOutcomeDto> AddOutcomeAsync(CreateSheInspectionChecklistOutcomeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedDraftAsync(dto.ChecklistId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheInspectionChecklistOutcome>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistOutcomeDto> UpdateOutcomeAsync(UpdateSheInspectionChecklistOutcomeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistOutcome>(dto.Id, "outcome");
        await GetOwnedDraftAsync(entity.ChecklistId);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheInspectionChecklistOutcome>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteOutcomeAsync(Guid outcomeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistOutcome>(outcomeId, "outcome");
        await GetOwnedDraftAsync(entity.ChecklistId);
        await _unitOfWork.Repository<SheInspectionChecklistOutcome>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Signatories ──

    public async Task<SheInspectionChecklistSignatoryDto> AddSignatoryAsync(CreateSheInspectionChecklistSignatoryDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedDraftAsync(dto.ChecklistId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SheInspectionChecklistSignatory>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInspectionChecklistSignatoryDto> UpdateSignatoryAsync(UpdateSheInspectionChecklistSignatoryDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistSignatory>(dto.Id, "signatory");
        await GetOwnedDraftAsync(entity.ChecklistId);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SheInspectionChecklistSignatory>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSignatoryAsync(Guid signatoryId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedChildAsync<SheInspectionChecklistSignatory>(signatoryId, "signatory");
        await GetOwnedDraftAsync(entity.ChecklistId);
        await _unitOfWork.Repository<SheInspectionChecklistSignatory>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Reorders (replace-set: every live child id, in its new order) ──

    public Task<SheInspectionChecklistDto> ReorderFieldsAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default)
        => ReorderAsync<SheInspectionChecklistField>(id, dto, userId, c => c.Fields, (x, n) => x.DisplayOrder = n, "field", cancellationToken);

    public Task<SheInspectionChecklistDto> ReorderSectionsAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default)
        => ReorderAsync<SheInspectionChecklistSection>(id, dto, userId, c => c.Sections, (x, n) => x.DisplayOrder = n, "section", cancellationToken);

    public Task<SheInspectionChecklistDto> ReorderOutcomesAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default)
        => ReorderAsync<SheInspectionChecklistOutcome>(id, dto, userId, c => c.Outcomes, (x, n) => x.DisplayOrder = n, "outcome", cancellationToken);

    public Task<SheInspectionChecklistDto> ReorderSignatoriesAsync(Guid id, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default)
        => ReorderAsync<SheInspectionChecklistSignatory>(id, dto, userId, c => c.Signatories, (x, n) => x.DisplayOrder = n, "signatory", cancellationToken);

    public async Task<SheInspectionChecklistDto> ReorderSectionItemsAsync(Guid sectionId, SheChecklistReorderDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var section = await GetOwnedChildAsync<SheInspectionChecklistSection>(sectionId, "section");
        var checklist = await GetOwnedDraftAsync(section.ChecklistId);
        var items = checklist.Items.Where(i => i.SectionId == sectionId && !i.IsDeleted).ToList();
        ApplyOrder(items, dto.OrderedIds, (x, n) => x.ItemOrder = n, userId, $"section '{section.Title}' item");
        await _unitOfWork.Repository<SheInspectionChecklistItem>().UpdateRangeAsync(items);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(checklist.Id, cancellationToken);
    }

    private async Task<SheInspectionChecklistDto> ReorderAsync<T>(
        Guid id, SheChecklistReorderDto dto, Guid userId,
        Func<SheInspectionChecklist, IEnumerable<T>> children, Action<T, int> setOrder, string what,
        CancellationToken cancellationToken) where T : TenantEntity
    {
        var checklist = await GetOwnedDraftAsync(id);
        var live = children(checklist).Where(c => !c.IsDeleted).ToList();
        ApplyOrder(live, dto.OrderedIds, setOrder, userId, what);
        await _unitOfWork.Repository<T>().UpdateRangeAsync(live);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private static void ApplyOrder<T>(List<T> live, List<Guid> orderedIds, Action<T, int> setOrder, Guid userId, string what) where T : TenantEntity
    {
        var liveIds = live.Select(x => x.Id).ToHashSet();
        var given = orderedIds.ToHashSet();
        if (given.Count != orderedIds.Count)
            throw new InvalidOperationException($"The {what} order repeats an id.");
        if (!liveIds.SetEquals(given))
            throw new InvalidOperationException($"The {what} order must list every current {what} exactly once ({live.Count} expected, {orderedIds.Count} given).");
        var position = 0;
        foreach (var childId in orderedIds)
        {
            var child = live.First(x => x.Id == childId);
            setOrder(child, ++position);
            child.UpdatedAt = DateTime.UtcNow;
            child.UpdatedBy = userId.ToString();
        }
    }
}

#endregion

#region Safety Inspection Service

public class SafetyInspectionService : ISafetyInspectionService
{
    private readonly ISafetyInspectionRepository _inspectionRepository;
    private readonly ISheInspectionChecklistRepository _checklistRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyInspectionService> _logger;

    public SafetyInspectionService(
        ISafetyInspectionRepository inspectionRepository,
        ISheInspectionChecklistRepository checklistRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SafetyInspectionService> logger)
    {
        _inspectionRepository = inspectionRepository;
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

        // Numeric max, not string ordering: the seeder wrote 3-digit suffixes (INSP-2026-001)
        // while this generator emits 4-digit ones, and across mixed widths string ordering picks
        // the wrong "latest" ("002" sorts above "0003"), silently re-issuing taken numbers.
        // Soft-deleted inspections keep their number, so they count toward the max too.
        var numbers = await _inspectionRepository
            .GetQueryableIncludingDeleted(i => i.TenantId == tenantId && i.InspectionNumber.StartsWith(prefix))
            .Select(i => i.InspectionNumber)
            .ToListAsync(cancellationToken);

        var max = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{max + 1:D4}";
    }

    public async Task<SafetyInspectionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _inspectionRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Safety inspection with ID '{id}' not found.");
        var dto = entity.ToDto();
        await ResolveFieldValueDisplaysAsync(dto);
        return dto;
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
        // Choosing a template loads its items onto the inspection, unassessed, and starts the walk
        // (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §2.4). Free-form stays Scheduled.
        if (dto.ChecklistId != null)
        {
            var checklist = await GetRunnableChecklistAsync(dto.ChecklistId.Value, tenantId);
            Materialise(entity, checklist, tenantId, userId);
        }
        await _inspectionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety inspection created: {InspectionNumber}", entity.InspectionNumber);
        // Write responses re-read through the include-bearing path — the tracked entity's navs
        // (location, org unit, checklist, inspector) are unloaded and would map blank.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SafetyInspectionDto> UpdateAsync(UpdateSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionAsync(dto.Id);
        // A materialised run is pinned to its template: the items on it came from that version.
        if (entity.ChecklistId != null && dto.ChecklistId != entity.ChecklistId
            && await _unitOfWork.Repository<SafetyInspectionItem>().ExistsAsync(i => i.InspectionId == entity.Id && i.ChecklistItemId != null && !i.IsDeleted))
            throw new InvalidOperationException($"Inspection {entity.InspectionNumber} was run against a checklist and cannot be moved to another; schedule a new inspection instead.");
        var computedScore = entity.CompletedAt != null ? entity.ComplianceScore : null;
        entity.UpdateEntity(dto, userId);
        // The score of a completed checklist run is computed, not typed (§2.5).
        if (computedScore != null)
            entity.ComplianceScore = computedScore;
        await _inspectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
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

        if (entity.Status == SheInspectionStatus.Closed)
            throw new InvalidOperationException("Inspection has already been closed.");

        // Findings are tracked to closure (FRD §4): an inspection cannot close over an unresolved
        // item or an open corrective action on a discovered hazard.
        var openItems = await _unitOfWork.Repository<SafetyInspectionItem>().GetQueryable()
            .CountAsync(t => t.InspectionId == entity.Id && !t.IsResolved && !t.IsDeleted, cancellationToken);
        if (openItems > 0)
            throw new InvalidOperationException($"Inspection cannot be closed: {openItems} finding(s) are still unresolved.");

        var openActions = await _unitOfWork.Repository<SafetyInspectionHazardAction>().GetQueryable()
            .CountAsync(a => a.InspectionHazard.InspectionId == entity.Id && !a.IsDeleted
                          && a.Status != SheCorrectiveActionStatus.Completed
                          && a.Status != SheCorrectiveActionStatus.Verified
                          && a.Status != SheCorrectiveActionStatus.Cancelled, cancellationToken);
        if (openActions > 0)
            throw new InvalidOperationException($"Inspection cannot be closed: {openActions} corrective action(s) are still open.");

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
        // Child write responses re-read through the parent's include-bearing path — the tracked
        // entity's navs (responsible/resolved-by) are unloaded and map blank.
        var full = await GetByIdAsync(entity.InspectionId, cancellationToken);
        return full.Items.First(t => t.Id == entity.Id);
    }

    public async Task<SafetyInspectionItemDto> UpdateItemAsync(UpdateSafetyInspectionItemDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyInspectionItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var full = await GetByIdAsync(entity.InspectionId, cancellationToken);
        return full.Items.First(t => t.Id == entity.Id);
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
        var full = await GetByIdAsync(entity.InspectionId, cancellationToken);
        return full.Hazards.First(h => h.Id == entity.Id);
    }

    public async Task<SafetyInspectionHazardDto> UpdateHazardAsync(UpdateSafetyInspectionHazardDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyInspectionHazard>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var full = await GetByIdAsync(entity.InspectionId, cancellationToken);
        return full.Hazards.First(h => h.Id == entity.Id);
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
        var hazard = await GetOwnedHazardAsync(dto.InspectionHazardId);

        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionHazardAction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Grandchild write responses re-read through the inspection's include-bearing path — the
        // tracked entity's navs (template, assignee) are unloaded and map blank.
        var full = await GetByIdAsync(hazard.InspectionId, cancellationToken);
        return full.Hazards.First(h => h.Id == hazard.Id).Actions.First(a => a.Id == entity.Id);
    }

    public async Task<SafetyInspectionHazardActionDto> UpdateHazardActionAsync(UpdateSafetyInspectionHazardActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHazardActionAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _unitOfWork.Repository<SafetyInspectionHazardAction>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var hazard = await GetOwnedHazardAsync(entity.InspectionHazardId);
        var full = await GetByIdAsync(hazard.InspectionId, cancellationToken);
        return full.Hazards.First(h => h.Id == hazard.Id).Actions.First(a => a.Id == entity.Id);
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
        var full = await GetByIdAsync(entity.InspectionId, cancellationToken);
        return full.Documents.First(d => d.Id == entity.Id);
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(documentId);
        await _unitOfWork.Repository<SafetyInspectionDocument>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Checklist run — docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §4
    // ═════════════════════════════════════════════════════════════════════════════

    private async Task<SafetyInspection> GetOwnedInspectionWithDetailsAsync(Guid id)
    {
        var entity = await _inspectionRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Safety inspection with ID '{id}' not found.");
        return entity;
    }

    private static void EnsureAnswersOpen(SafetyInspection e)
    {
        if (e.CompletedAt != null || e.Status == SheInspectionStatus.Closed)
            throw new InvalidOperationException($"Inspection {e.InspectionNumber} has been completed; its checklist answers are locked.");
    }

    private static SheInspectionChecklist RequireChecklist(SafetyInspection e)
        => e.Checklist ?? throw new InvalidOperationException($"Inspection {e.InspectionNumber} is free-form (no checklist template).");

    /// <summary>A template an inspection may be run against: owned, published and active.</summary>
    private async Task<SheInspectionChecklist> GetRunnableChecklistAsync(Guid checklistId, Guid tenantId)
    {
        var checklist = await _checklistRepository.GetWithItemsAsync(checklistId);
        if (checklist == null || checklist.TenantId != tenantId)
            throw new ArgumentException($"Inspection checklist with ID '{checklistId}' not found.");
        if (checklist.Status != SheChecklistStatus.Published)
            throw new InvalidOperationException($"Checklist {checklist.ChecklistNumber} v{checklist.Version} is {checklist.Status}; only a published template can be inspected against.");
        if (!checklist.IsActive)
            throw new InvalidOperationException($"Checklist {checklist.ChecklistNumber} v{checklist.Version} is inactive and not offered for new inspections.");
        return checklist;
    }

    /// <summary>
    /// Copies every template item onto the inspection, unassessed, in form order, and returns the new
    /// rows. On a NEW inspection the rows ride along with the parent's Add. On a TRACKED one the caller
    /// must add them through the repository: navigation fixup sees their pre-set Guid keys and tracks
    /// them as Modified, and the UPDATEs then affect no rows (DbUpdateConcurrencyException).
    /// </summary>
    private static List<SafetyInspectionItem> Materialise(SafetyInspection inspection, SheInspectionChecklist checklist, Guid tenantId, Guid userId)
    {
        var created = new List<SafetyInspectionItem>();
        var order = 0;
        foreach (var section in checklist.LayOutSections())
            foreach (var item in section.Items)
                created.Add(new SafetyInspectionItem
                {
                    TenantId = tenantId,
                    InspectionId = inspection.Id,
                    ChecklistItemId = item.Id,
                    DisplayOrder = ++order,
                    ItemDescription = item.ItemDescription,
                    Status = SheComplianceStatus.NotAssessed,
                    CreatedBy = userId.ToString(),
                });
        foreach (var row in created)
            inspection.Items.Add(row);
        if (inspection.Status == SheInspectionStatus.Scheduled)
            inspection.Status = SheInspectionStatus.InProgress;
        return created;
    }

    public async Task<SafetyInspectionDto> ApplyChecklistAsync(Guid inspectionId, ApplySafetyInspectionChecklistDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionWithDetailsAsync(inspectionId);
        EnsureAnswersOpen(entity);
        var existing = entity.Items.Count(i => !i.IsDeleted);
        if (existing > 0)
            throw new InvalidOperationException($"Inspection {entity.InspectionNumber} already carries {existing} item(s); a checklist can only be applied to an empty inspection.");

        var checklist = await GetRunnableChecklistAsync(dto.ChecklistId, entity.TenantId);
        entity.ChecklistId = checklist.Id;
        var rows = Materialise(entity, checklist, entity.TenantId, userId);
        await _unitOfWork.Repository<SafetyInspectionItem>().AddRangeAsync(rows);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
        // The inspection is tracked from the include-bearing read: SaveChanges persists its scalar
        // changes and discovers the new child rows as Added. Re-attaching the graph with Update()
        // would mark those new rows Modified and issue UPDATEs that affect no rows.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(inspectionId, cancellationToken);
    }

    public async Task<SafetyInspectionDto> SaveResponsesAsync(Guid inspectionId, IReadOnlyList<SafetyInspectionResponseDto> responses, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionWithDetailsAsync(inspectionId);
        EnsureAnswersOpen(entity);
        var checklist = RequireChecklist(entity);
        var items = entity.Items.Where(i => !i.IsDeleted).ToDictionary(i => i.Id);

        foreach (var r in responses)
        {
            if (!items.TryGetValue(r.ItemId, out var item))
                throw new InvalidOperationException($"Item '{r.ItemId}' does not belong to inspection {entity.InspectionNumber}.");

            var kind = item.ChecklistItem?.Section?.Kind ?? SheChecklistSectionKind.Standard;
            if (kind == SheChecklistSectionKind.Critical
                && r.Status is not (SheComplianceStatus.Compliant or SheComplianceStatus.NonCompliant or SheComplianceStatus.NotAssessed))
                throw new InvalidOperationException($"'{item.ItemDescription}' is a critical item: answer Yes (non-compliant) or No (compliant).");
            if (r.Status == SheComplianceStatus.PartiallyCompliant && !checklist.AllowPartialCompliance)
                throw new InvalidOperationException($"Checklist {checklist.ChecklistNumber} does not allow 'Partially compliant'; use Compliant or Non-compliant.");

            item.Status = r.Status;
            item.DeficiencyNoted = r.DeficiencyNoted;
            item.ActionRequired = r.ActionRequired;
            item.RiskLevel = r.RiskLevel;
            // Compliant / N/A items have nothing to resolve, and the close-out gate counts unresolved
            // rows regardless of status — so they resolve themselves. Non-conformities re-open.
            switch (r.Status)
            {
                case SheComplianceStatus.Compliant:
                case SheComplianceStatus.NotApplicable:
                    item.IsResolved = true;
                    break;
                default:
                    item.IsResolved = false;
                    item.ResolvedDate = null;
                    item.ResolvedById = null;
                    item.ResolutionNotes = null;
                    break;
            }
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = userId.ToString();
        }

        if (entity.Status == SheInspectionStatus.Scheduled)
            entity.Status = SheInspectionStatus.InProgress;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
        // The inspection is tracked from the include-bearing read: SaveChanges persists its scalar
        // changes and discovers the new child rows as Added. Re-attaching the graph with Update()
        // would mark those new rows Modified and issue UPDATEs that affect no rows.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(inspectionId, cancellationToken);
    }

    public async Task<SafetyInspectionDto> SaveFieldValuesAsync(Guid inspectionId, IReadOnlyList<SafetyInspectionFieldValueWriteDto> values, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await GetOwnedInspectionWithDetailsAsync(inspectionId);
        EnsureAnswersOpen(entity);
        var checklist = RequireChecklist(entity);
        var fields = checklist.Fields.Where(f => !f.IsDeleted).ToDictionary(f => f.Id);

        if (values.Select(v => v.ChecklistFieldId).Distinct().Count() != values.Count)
            throw new InvalidOperationException("A field appears more than once in the payload.");

        var repo = _unitOfWork.Repository<SafetyInspectionFieldValue>();
        var current = entity.FieldValues.Where(v => !v.IsDeleted).ToList();
        var keep = new HashSet<Guid>();

        foreach (var v in values)
        {
            if (!fields.TryGetValue(v.ChecklistFieldId, out var field))
                throw new InvalidOperationException($"Field '{v.ChecklistFieldId}' does not belong to checklist {checklist.ChecklistNumber}.");

            var (text, reference) = await NormaliseFieldValueAsync(field, v, tenantId);
            var row = current.FirstOrDefault(c => c.ChecklistFieldId == field.Id);
            if (row == null)
            {
                row = new SafetyInspectionFieldValue
                {
                    TenantId = tenantId, InspectionId = entity.Id, ChecklistFieldId = field.Id,
                    ValueText = text, ValueReferenceId = reference, CreatedBy = userId.ToString(),
                };
                await repo.AddAsync(row);
            }
            else
            {
                row.ValueText = text;
                row.ValueReferenceId = reference;
                row.UpdatedAt = DateTime.UtcNow;
                row.UpdatedBy = userId.ToString();
                await repo.UpdateAsync(row);
            }
            keep.Add(field.Id);
        }

        // Replace-set: a field missing from the payload is cleared.
        foreach (var stale in current.Where(c => !keep.Contains(c.ChecklistFieldId)))
            await repo.DeleteAsync(stale);

        if (entity.Status == SheInspectionStatus.Scheduled)
            entity.Status = SheInspectionStatus.InProgress;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
        // The inspection is tracked from the include-bearing read: SaveChanges persists its scalar
        // changes and discovers the new child rows as Added. Re-attaching the graph with Update()
        // would mark those new rows Modified and issue UPDATEs that affect no rows.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(inspectionId, cancellationToken);
    }

    /// <summary>Type-checks one value against its field and returns the canonical (text, reference) pair.</summary>
    private async Task<(string? Text, Guid? Reference)> NormaliseFieldValueAsync(SheInspectionChecklistField field, SafetyInspectionFieldValueWriteDto v, Guid tenantId)
    {
        var text = string.IsNullOrWhiteSpace(v.ValueText) ? null : v.ValueText.Trim();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        switch (field.FieldType)
        {
            case SheChecklistFieldType.Text:
            case SheChecklistFieldType.LongText:
                return (text, null);
            case SheChecklistFieldType.Number:
                if (text == null) return (null, null);
                if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, inv, out var number))
                    throw new InvalidOperationException($"'{field.Label}' must be a number.");
                return (number.ToString(inv), null);
            case SheChecklistFieldType.Date:
                if (text == null) return (null, null);
                if (!DateTime.TryParse(text, inv, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var date))
                    throw new InvalidOperationException($"'{field.Label}' must be a date (YYYY-MM-DD).");
                return (date.ToString("yyyy-MM-dd", inv), null);
            case SheChecklistFieldType.Time:
                if (text == null) return (null, null);
                if (!TimeSpan.TryParse(text, inv, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
                    throw new InvalidOperationException($"'{field.Label}' must be a time of day (HH:mm).");
                return (time.ToString(@"hh\:mm", inv), null);
            case SheChecklistFieldType.YesNo:
                if (text == null) return (null, null);
                if (!bool.TryParse(text, out var flag))
                    throw new InvalidOperationException($"'{field.Label}' must be true or false.");
                return (flag ? "true" : "false", null);
            case SheChecklistFieldType.Choice:
            {
                if (text == null) return (null, null);
                var match = SheChecklistChoiceOptions.Split(field.ChoiceOptions)
                    .FirstOrDefault(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new InvalidOperationException($"'{text}' is not one of the options for '{field.Label}'.");
                return (match, null);
            }
            case SheChecklistFieldType.Employee:
            case SheChecklistFieldType.Location:
            case SheChecklistFieldType.OrganizationUnit:
            {
                if (v.ValueReferenceId == null) return (null, null);
                var id = v.ValueReferenceId.Value;
                var exists = field.FieldType switch
                {
                    SheChecklistFieldType.Employee => await _unitOfWork.Repository<Employee>().ExistsAsync(e => e.Id == id && e.TenantId == tenantId && !e.IsDeleted),
                    SheChecklistFieldType.Location => await _unitOfWork.Repository<Location>().ExistsAsync(l => l.Id == id && l.TenantId == tenantId && !l.IsDeleted),
                    _ => await _unitOfWork.Repository<OrganizationUnit>().ExistsAsync(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted),
                };
                if (!exists)
                    throw new InvalidOperationException($"'{field.Label}': the selected {field.FieldType} was not found.");
                return (null, id);
            }
            default:
                return (text, null);
        }
    }

    /// <summary>Fills ValueDisplay for reference-typed header values (names live on other tables).</summary>
    private async Task ResolveFieldValueDisplaysAsync(SafetyInspectionDto dto)
    {
        var refs = dto.FieldValues.Where(v => v.ValueReferenceId != null).ToList();
        if (refs.Count == 0) return;

        var employeeIds = refs.Where(v => v.FieldType == SheChecklistFieldType.Employee).Select(v => v.ValueReferenceId!.Value).Distinct().ToList();
        var locationIds = refs.Where(v => v.FieldType == SheChecklistFieldType.Location).Select(v => v.ValueReferenceId!.Value).Distinct().ToList();
        var unitIds = refs.Where(v => v.FieldType == SheChecklistFieldType.OrganizationUnit).Select(v => v.ValueReferenceId!.Value).Distinct().ToList();

        var names = new Dictionary<Guid, string>();
        // FullName is [NotMapped], so employees are named in memory rather than projected in SQL.
        if (employeeIds.Count > 0)
            foreach (var e in await _unitOfWork.Repository<Employee>().FindAsync(e => employeeIds.Contains(e.Id)))
                names[e.Id] = e.FullName;
        if (locationIds.Count > 0)
            foreach (var l in await _unitOfWork.Repository<Location>().FindAsync(l => locationIds.Contains(l.Id)))
                names[l.Id] = l.Name;
        if (unitIds.Count > 0)
            foreach (var u in await _unitOfWork.Repository<OrganizationUnit>().FindAsync(u => unitIds.Contains(u.Id)))
                names[u.Id] = u.Name;

        foreach (var v in refs)
            v.ValueDisplay = names.TryGetValue(v.ValueReferenceId!.Value, out var name) ? name : null;
    }

    /// <summary>The scoring rules (§2.5–2.6): applicable = standard items answered other than N/A; partial counts as not compliant.</summary>
    private static SafetyInspectionScoreDto ComputeScore(SafetyInspection e)
    {
        var checklist = RequireChecklist(e);
        var items = e.Items.Where(i => !i.IsDeleted && i.ChecklistItemId != null).ToList();
        var standard = items.Where(i => (i.ChecklistItem?.Section?.Kind ?? SheChecklistSectionKind.Standard) == SheChecklistSectionKind.Standard).ToList();
        var critical = items.Where(i => i.ChecklistItem?.Section?.Kind == SheChecklistSectionKind.Critical).ToList();

        var score = new SafetyInspectionScoreDto
        {
            ScoringMode = checklist.ScoringMode,
            TotalItems = standard.Count,
            TotalCompliantItems = standard.Count(i => i.Status == SheComplianceStatus.Compliant),
            TotalNonCompliantItems = standard.Count(i => i.Status == SheComplianceStatus.NonCompliant),
            TotalPartiallyCompliantItems = standard.Count(i => i.Status == SheComplianceStatus.PartiallyCompliant),
            TotalNotApplicableItems = standard.Count(i => i.Status == SheComplianceStatus.NotApplicable),
            TotalNotAssessedItems = items.Count(i => i.Status == SheComplianceStatus.NotAssessed),
            CriticalNonConformityCount = critical.Count(i => i.Status == SheComplianceStatus.NonCompliant),
        };
        score.TotalApplicableItems = score.TotalCompliantItems + score.TotalNonCompliantItems + score.TotalPartiallyCompliantItems;
        score.CompliancePercentage = score.TotalApplicableItems > 0
            ? Math.Round(score.TotalCompliantItems * 100m / score.TotalApplicableItems, 2)
            : null;
        score.IsDisqualified = checklist.ScoringMode != SheChecklistScoringMode.None && score.CriticalNonConformityCount > 0;

        var outcomes = checklist.Outcomes.Where(o => !o.IsDeleted).ToList();
        SheInspectionChecklistOutcome? recommended = null;
        if (score.IsDisqualified)
            recommended = outcomes.FirstOrDefault(o => o.IsDisqualifying);
        else if (checklist.ScoringMode == SheChecklistScoringMode.CompliancePercentage && score.CompliancePercentage != null)
            // The band whose lower bound is the greatest one at or below the score — gapless by construction.
            recommended = outcomes.Where(o => o.MinPercent != null && o.MinPercent <= score.CompliancePercentage)
                .OrderByDescending(o => o.MinPercent).FirstOrDefault();
        score.RecommendedOutcomeId = recommended?.Id;
        score.RecommendedOutcomeLabel = recommended?.Label;

        var values = e.FieldValues.Where(v => !v.IsDeleted && (!string.IsNullOrWhiteSpace(v.ValueText) || v.ValueReferenceId != null))
            .Select(v => v.ChecklistFieldId).ToHashSet();
        score.MissingRequiredFields = checklist.Fields.Where(f => !f.IsDeleted && f.IsRequired && !values.Contains(f.Id))
            .OrderBy(f => f.DisplayOrder).Select(f => f.Label).ToList();
        score.IsReadyToComplete = items.Count > 0 && score.TotalNotAssessedItems == 0 && score.MissingRequiredFields.Count == 0;
        return score;
    }

    public async Task<SafetyInspectionScoreDto> GetScoreAsync(Guid inspectionId, CancellationToken cancellationToken = default)
        => ComputeScore(await GetOwnedInspectionWithDetailsAsync(inspectionId));

    public async Task<SafetyInspectionDto> CompleteAsync(Guid inspectionId, CompleteSafetyInspectionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInspectionWithDetailsAsync(inspectionId);
        if (entity.CompletedAt != null)
            throw new InvalidOperationException($"Inspection {entity.InspectionNumber} was completed on {entity.CompletedAt:yyyy-MM-dd}.");
        if (entity.Status == SheInspectionStatus.Closed)
            throw new InvalidOperationException($"Inspection {entity.InspectionNumber} is closed.");
        var checklist = RequireChecklist(entity);

        var score = ComputeScore(entity);
        if (!score.IsReadyToComplete)
        {
            var reasons = new List<string>();
            if (score.TotalItems == 0 && score.CriticalNonConformityCount == 0 && !entity.Items.Any(i => !i.IsDeleted && i.ChecklistItemId != null))
                reasons.Add("no checklist items have been loaded");
            if (score.TotalNotAssessedItems > 0)
                reasons.Add($"{score.TotalNotAssessedItems} item(s) are not yet assessed");
            if (score.MissingRequiredFields.Count > 0)
                reasons.Add($"required field(s) missing: {string.Join(", ", score.MissingRequiredFields)}");
            throw new InvalidOperationException($"Inspection {entity.InspectionNumber} cannot be completed: {string.Join("; ", reasons)}.");
        }

        var outcomes = checklist.Outcomes.Where(o => !o.IsDeleted).ToDictionary(o => o.Id);
        SheInspectionChecklistOutcome? chosen = null;
        switch (checklist.ScoringMode)
        {
            case SheChecklistScoringMode.None:
                break;
            case SheChecklistScoringMode.CompliancePercentage:
            {
                var chosenId = dto.OutcomeId ?? score.RecommendedOutcomeId;
                if (chosenId == null || !outcomes.TryGetValue(chosenId.Value, out chosen))
                    throw new InvalidOperationException("Choose an outcome for this inspection — no band matched the score.");
                if (score.RecommendedOutcomeId != null && chosen.Id != score.RecommendedOutcomeId)
                {
                    if (string.IsNullOrWhiteSpace(dto.OutcomeOverrideReason))
                        throw new InvalidOperationException($"The score recommends '{score.RecommendedOutcomeLabel}'; give a reason to record '{chosen.Label}' instead.");
                    if (score.IsDisqualified && !chosen.IsDisqualifying)
                        throw new InvalidOperationException($"{score.CriticalNonConformityCount} critical non-conformity(ies) were recorded: the outcome must be '{score.RecommendedOutcomeLabel}'.");
                }
                break;
            }
            case SheChecklistScoringMode.QualitativeRating:
                if (dto.OutcomeId == null || !outcomes.TryGetValue(dto.OutcomeId.Value, out chosen))
                    throw new InvalidOperationException("Choose the overall rating for this inspection.");
                if (score.IsDisqualified && !chosen.IsDisqualifying)
                    throw new InvalidOperationException($"{score.CriticalNonConformityCount} critical non-conformity(ies) were recorded: the rating must be '{score.RecommendedOutcomeLabel}'.");
                break;
        }

        entity.TotalApplicableItems = score.TotalApplicableItems;
        entity.TotalCompliantItems = score.TotalCompliantItems;
        entity.TotalNonCompliantItems = score.TotalNonCompliantItems;
        entity.TotalPartiallyCompliantItems = score.TotalPartiallyCompliantItems;
        entity.CriticalNonConformityCount = score.CriticalNonConformityCount;
        entity.CompliancePercentage = score.CompliancePercentage;
        entity.ComplianceScore = score.CompliancePercentage == null ? null : (int)Math.Round(score.CompliancePercentage.Value, MidpointRounding.AwayFromZero);
        entity.RecommendedOutcomeId = score.RecommendedOutcomeId;
        entity.OutcomeId = chosen?.Id;
        entity.OutcomeOverrideReason = chosen != null && chosen.Id != score.RecommendedOutcomeId ? dto.OutcomeOverrideReason : null;
        entity.SubjectComments = dto.SubjectComments ?? entity.SubjectComments;
        entity.FindingsAndObservations = dto.FindingsAndObservations ?? entity.FindingsAndObservations;
        entity.RecommendedActions = dto.RecommendedActions ?? entity.RecommendedActions;
        entity.OverallRiskRating = dto.OverallRiskRating ?? entity.OverallRiskRating;
        entity.ComplianceDeadline = dto.ComplianceDeadline ?? entity.ComplianceDeadline;
        entity.NextInspectionDueDate = dto.NextInspectionDueDate ?? entity.NextInspectionDueDate;
        if (entity.NextInspectionDueDate == null && chosen?.ReinspectionWithinDays is int days)
            entity.NextInspectionDueDate = DateTime.UtcNow.Date.AddDays(days);
        entity.CompletedAt = DateTime.UtcNow;
        entity.CompletedById = userId;
        var openFindings = score.TotalNonCompliantItems + score.TotalPartiallyCompliantItems + score.CriticalNonConformityCount;
        entity.Status = openFindings > 0 ? SheInspectionStatus.PendingCorrectiveActions : SheInspectionStatus.Completed;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        // The inspection is tracked from the include-bearing read: SaveChanges persists its scalar
        // changes and discovers the new child rows as Added. Re-attaching the graph with Update()
        // would mark those new rows Modified and issue UPDATEs that affect no rows.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety inspection completed: {Number} — {Score}% → {Outcome}", entity.InspectionNumber, entity.CompliancePercentage, chosen?.Label);
        return await GetByIdAsync(inspectionId, cancellationToken);
    }

    public async Task<SafetyInspectionSignatureDto> AddSignatureAsync(Guid inspectionId, CreateSafetyInspectionSignatureDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await GetOwnedInspectionWithDetailsAsync(inspectionId);
        var checklist = RequireChecklist(entity);
        if (entity.Status == SheInspectionStatus.Scheduled)
            throw new InvalidOperationException($"Inspection {entity.InspectionNumber} has not started; record the walk before signing.");
        if (entity.Status == SheInspectionStatus.Closed)
            throw new InvalidOperationException($"Inspection {entity.InspectionNumber} is closed.");

        var signatory = checklist.Signatories.FirstOrDefault(s => s.Id == dto.ChecklistSignatoryId && !s.IsDeleted)
            ?? throw new InvalidOperationException($"Signatory '{dto.ChecklistSignatoryId}' does not belong to checklist {checklist.ChecklistNumber}.");
        if (entity.Signatures.Any(s => s.ChecklistSignatoryId == signatory.Id && !s.IsDeleted))
            throw new InvalidOperationException($"'{signatory.RoleLabel}' has already signed inspection {entity.InspectionNumber}.");

        var signature = new SafetyInspectionSignature
        {
            TenantId = tenantId,
            InspectionId = entity.Id,
            ChecklistSignatoryId = signatory.Id,
            RoleLabel = signatory.RoleLabel,
            SignedAt = dto.SignedAt ?? DateTime.UtcNow,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
        if (signatory.Kind == SheChecklistSignatoryKind.SystemUser)
        {
            // The actor is the token's employee, never the body: a system signature is a sign-off.
            var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(userId)
                ?? throw new InvalidOperationException("Your account is not linked to an employee record and cannot sign.");
            signature.SignedByEmployeeId = employee.Id;
            signature.SignedName = employee.FullName;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.SignedName))
                throw new InvalidOperationException($"'{signatory.RoleLabel}' is an external signatory: enter the name of the person who signed.");
            signature.SignedName = dto.SignedName.Trim();
        }

        await _unitOfWork.Repository<SafetyInspectionSignature>().AddAsync(signature);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var full = await GetByIdAsync(inspectionId, cancellationToken);
        return full.Signatures.First(s => s.Id == signature.Id);
    }

    public async Task<bool> DeleteSignatureAsync(Guid signatureId, CancellationToken cancellationToken = default)
    {
        var signature = await _unitOfWork.Repository<SafetyInspectionSignature>().GetByIdAsync(signatureId);
        if (signature == null || signature.IsDeleted || signature.TenantId != GetTenantId())
            throw new ArgumentException($"Signature with ID '{signatureId}' not found.");
        var inspection = await GetOwnedInspectionAsync(signature.InspectionId);
        if (inspection.Status == SheInspectionStatus.Closed)
            throw new InvalidOperationException($"Inspection {inspection.InspectionNumber} is closed; its signatures are final.");
        await _unitOfWork.Repository<SafetyInspectionSignature>().DeleteAsync(signature);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
