using ErpSystem.Core.DTOs.Common;
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
// SHE services — Reference Catalog (A) & Incident Management (B).
// ============================================================================

#region Reference Data Service

public class SheReferenceDataService : ISheReferenceDataService
{
    private readonly ISheIncidentTypeRepository _incidentTypeRepository;
    private readonly ISheInjuryTypeRepository _injuryTypeRepository;
    private readonly ISheBodyPartRepository _bodyPartRepository;
    private readonly ISheCorrectiveActionTemplateRepository _templateRepository;
    private readonly ISheRegulatoryBodyRepository _regulatoryBodyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheReferenceDataService> _logger;

    public SheReferenceDataService(
        ISheIncidentTypeRepository incidentTypeRepository,
        ISheInjuryTypeRepository injuryTypeRepository,
        ISheBodyPartRepository bodyPartRepository,
        ISheCorrectiveActionTemplateRepository templateRepository,
        ISheRegulatoryBodyRepository regulatoryBodyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheReferenceDataService> logger)
    {
        _incidentTypeRepository = incidentTypeRepository;
        _injuryTypeRepository = injuryTypeRepository;
        _bodyPartRepository = bodyPartRepository;
        _templateRepository = templateRepository;
        _regulatoryBodyRepository = regulatoryBodyRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
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

    private async Task<SheIncidentType> GetOwnedIncidentTypeAsync(Guid id)
    {
        var entity = await _incidentTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Incident type with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheInjuryType> GetOwnedInjuryTypeAsync(Guid id)
    {
        var entity = await _injuryTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Injury type with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheBodyPart> GetOwnedBodyPartAsync(Guid id)
    {
        var entity = await _bodyPartRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Body part with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheCorrectiveActionTemplate> GetOwnedTemplateAsync(Guid id)
    {
        var entity = await _templateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Corrective action template with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheRegulatoryBody> GetOwnedRegulatoryBodyAsync(Guid id)
    {
        var entity = await _regulatoryBodyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Regulatory body with ID '{id}' not found.");
        return entity;
    }

    // ── Incident types ──
    public async Task<IEnumerable<SheIncidentTypeDto>> GetIncidentTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _incidentTypeRepository.GetActiveAsync() : await _incidentTypeRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheIncidentTypeDto> GetIncidentTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _incidentTypeRepository.GetWithDefaultActionsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Incident type with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheIncidentTypeDto>> GetReportableIncidentTypesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _incidentTypeRepository.GetReportableAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheIncidentTypeDto> CreateIncidentTypeAsync(CreateSheIncidentTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _incidentTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheIncidentTypeDto> UpdateIncidentTypeAsync(UpdateSheIncidentTypeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentTypeAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _incidentTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteIncidentTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentTypeAsync(id);
        await _incidentTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Injury types ──
    public async Task<IEnumerable<SheInjuryTypeDto>> GetInjuryTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _injuryTypeRepository.GetActiveAsync() : await _injuryTypeRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheInjuryTypeDto> CreateInjuryTypeAsync(CreateSheInjuryTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _injuryTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheInjuryTypeDto> UpdateInjuryTypeAsync(UpdateSheInjuryTypeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInjuryTypeAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _injuryTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInjuryTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInjuryTypeAsync(id);
        await _injuryTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Body parts ──
    public async Task<IEnumerable<SheBodyPartDto>> GetBodyPartsAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _bodyPartRepository.GetActiveAsync() : await _bodyPartRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheBodyPartDto> CreateBodyPartAsync(CreateSheBodyPartDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _bodyPartRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheBodyPartDto> UpdateBodyPartAsync(UpdateSheBodyPartDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBodyPartAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _bodyPartRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteBodyPartAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBodyPartAsync(id);
        await _bodyPartRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Corrective action templates ──
    public async Task<IEnumerable<SheCorrectiveActionTemplateDto>> GetCorrectiveActionTemplatesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _templateRepository.GetActiveAsync() : await _templateRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheCorrectiveActionTemplateDto> CreateCorrectiveActionTemplateAsync(CreateSheCorrectiveActionTemplateDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _templateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheCorrectiveActionTemplateDto> UpdateCorrectiveActionTemplateAsync(UpdateSheCorrectiveActionTemplateDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTemplateAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCorrectiveActionTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTemplateAsync(id);
        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Regulatory bodies ──
    public async Task<IEnumerable<SheRegulatoryBodyDto>> GetRegulatoryBodiesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _regulatoryBodyRepository.GetActiveAsync() : await _regulatoryBodyRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheRegulatoryBodyDto> CreateRegulatoryBodyAsync(CreateSheRegulatoryBodyDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _regulatoryBodyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheRegulatoryBodyDto> UpdateRegulatoryBodyAsync(UpdateSheRegulatoryBodyDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRegulatoryBodyAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _regulatoryBodyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteRegulatoryBodyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRegulatoryBodyAsync(id);
        await _regulatoryBodyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Safety Incident Service

public class SafetyIncidentService : ISafetyIncidentService
{
    private readonly ISafetyIncidentRepository _incidentRepository;
    private readonly ISafetyIncidentCorrectiveActionRepository _correctiveActionRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SafetyIncidentService> _logger;

    public SafetyIncidentService(
        ISafetyIncidentRepository incidentRepository,
        ISafetyIncidentCorrectiveActionRepository correctiveActionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SafetyIncidentService> logger)
    {
        _incidentRepository = incidentRepository;
        _correctiveActionRepository = correctiveActionRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
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

    private async Task<SafetyIncident> GetOwnedIncidentAsync(Guid id)
    {
        var entity = await _incidentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Safety incident with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyIncidentInvolvedPerson> GetOwnedInvolvedPersonAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyIncidentInvolvedPerson>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Involved person with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyIncidentInjuredBodyPart> GetOwnedInjuredBodyPartAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyIncidentInjuredBodyPart>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Injured body part with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyIncidentWitness> GetOwnedWitnessAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyIncidentWitness>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Witness with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyIncidentInvestigationTeamMember> GetOwnedInvestigationTeamMemberAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyIncidentInvestigationTeamMember>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Investigation team member with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyIncidentCorrectiveAction> GetOwnedCorrectiveActionAsync(Guid id)
    {
        var entity = await _correctiveActionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Corrective action with ID '{id}' not found.");
        return entity;
    }

    private async Task<SafetyIncidentDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _unitOfWork.Repository<SafetyIncidentDocument>().GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Document with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GenerateNextIncidentNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INC-{year}-";
        var last = await _incidentRepository.GetQueryable()
            .Where(i => i.TenantId == tenantId && i.IncidentNumber.StartsWith(prefix))
            .OrderByDescending(i => i.IncidentNumber)
            .Select(i => i.IncidentNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }

    // ── Queries ──
    public async Task<SafetyIncidentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _incidentRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Safety incident with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SafetyIncidentDto?> GetByNumberAsync(string incidentNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _incidentRepository.GetByIncidentNumberAsync(incidentNumber);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<PagedResult<SafetyIncidentSummaryDto>> GetPagedAsync(int page, int pageSize, SheIncidentStatus? status = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _incidentRepository.GetQueryable()
            .Include(i => i.IncidentType)
            .Include(i => i.Location)
            .Include(i => i.OrganizationUnit)
            .Include(i => i.ReportedBy)
            .Include(i => i.InvolvedPersons)
            .Include(i => i.CorrectiveActions)
            .Where(i => i.TenantId == tenantId && !i.IsDeleted);

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.IncidentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SafetyIncidentSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetByStatusAsync(SheIncidentStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByStatusAsync(status))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetBySeverityAsync(SheIncidentSeverity severity, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetBySeverityAsync(severity))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetByCategoryAsync(SheIncidentCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByCategoryAsync(category))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByDateRangeAsync(fromDate, toDate))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByLocationAsync(locationId))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetByInvolvedEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByInvolvedEmployeeAsync(employeeId))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetForEmployeeAsync(employeeId))
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetRequiringInvestigationAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetRequiringInvestigationAsync())
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetOpenAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetOpenAsync())
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetLostTimeInjuriesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetLostTimeInjuriesAsync())
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SafetyIncidentSummaryDto>> GetReportableNotYetNotifiedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetReportableNotYetNotifiedAsync())
            .Where(i => i.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    // ── CRUD ──
    public async Task<SafetyIncidentDto> CreateAsync(CreateSafetyIncidentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        entity.IncidentNumber = await GenerateNextIncidentNumberAsync(tenantId, cancellationToken);
        await _incidentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety incident created: {IncidentNumber}", entity.IncidentNumber);
        return entity.ToDto();
    }

    public async Task<SafetyIncidentDto> UpdateAsync(UpdateSafetyIncidentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.Id);
        if (entity.Status == SheIncidentStatus.Closed)
            throw new InvalidOperationException("A closed incident cannot be edited. Reopen it first.");

        entity.UpdateEntity(dto, userId);
        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(id);
        await _incidentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Workflow ──
    public async Task<bool> AssignInvestigationAsync(AssignSafetyIncidentInvestigationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.IncidentId);

        entity.RequiresInvestigation = true;
        entity.LeadInvestigatorId = dto.LeadInvestigatorId;
        entity.InvestigationStartDate = dto.InvestigationStartDate ?? DateTime.UtcNow;
        entity.InvestigationTargetDate = dto.InvestigationTargetDate;
        entity.RootCauseMethod = dto.RootCauseMethod;
        entity.Status = SheIncidentStatus.InvestigationInProgress;
        Touch(entity, userId);

        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordInvestigationAsync(RecordSafetyIncidentInvestigationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.IncidentId);

        entity.RootCauseAnalysis = dto.RootCauseAnalysis;
        entity.InvestigationFindings = dto.InvestigationFindings;
        if (dto.RootCauseMethod.HasValue) entity.RootCauseMethod = dto.RootCauseMethod;
        entity.InvestigationCompleteDate = dto.InvestigationCompleteDate ?? DateTime.UtcNow;
        entity.LikelihoodAfter = dto.LikelihoodAfter;
        entity.SeverityAfter = dto.SeverityAfter;
        entity.RiskScoreAfter = dto.LikelihoodAfter * dto.SeverityAfter;
        if (entity.Status == SheIncidentStatus.InvestigationInProgress)
            entity.Status = SheIncidentStatus.PendingCorrective;
        Touch(entity, userId);

        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> NotifyAuthorityAsync(NotifySafetyIncidentAuthorityDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.IncidentId);

        entity.ReportableToAuthority = true;
        entity.ReportedToBodyId = dto.ReportedToBodyId;
        entity.AuthorityNotificationDate = dto.AuthorityNotificationDate;
        entity.AuthorityReferenceNumber = dto.AuthorityReferenceNumber;
        entity.AuthorityNotifiedById = dto.AuthorityNotifiedById;
        Touch(entity, userId);

        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> FileClaimAsync(FileSafetyIncidentClaimDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.IncidentId);

        entity.InsuranceClaimFiled = true;
        entity.ClaimFiledDate = dto.ClaimFiledDate;
        entity.ClaimReferenceNumber = dto.ClaimReferenceNumber;
        entity.InsuranceProviderId = dto.InsuranceProviderId;
        entity.ClaimAmount = dto.ClaimAmount;
        entity.ClaimApproved = dto.ClaimApproved;
        entity.AmountPaid = dto.AmountPaid;
        Touch(entity, userId);

        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReviewAsync(ReviewSafetyIncidentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.IncidentId);

        entity.ReviewedById = dto.ReviewedById;
        entity.ReviewedDate = dto.ReviewedDate;
        entity.ReviewComments = dto.ReviewComments;
        if (dto.NewStatus.HasValue) entity.Status = dto.NewStatus.Value;
        Touch(entity, userId);

        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CloseAsync(CloseSafetyIncidentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.IncidentId);
        var tenantId = GetTenantId();

        var openActions = await _correctiveActionRepository.GetByIncidentIdAsync(dto.IncidentId);
        if (openActions.Any(a => a.TenantId == tenantId
                              && a.Status != SheCorrectiveActionStatus.Completed
                              && a.Status != SheCorrectiveActionStatus.Verified
                              && a.Status != SheCorrectiveActionStatus.Cancelled))
            throw new InvalidOperationException("All corrective actions must be completed before closing the incident.");

        entity.Status = SheIncidentStatus.Closed;
        entity.ClosedById = dto.ClosedById;
        entity.ClosedDate = dto.ClosedDate;
        entity.ClosureNotes = dto.ClosureNotes;
        Touch(entity, userId);

        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Safety incident closed: {IncidentNumber}", entity.IncidentNumber);
        return true;
    }

    // ── Involved persons ──
    public async Task<SafetyIncidentInvolvedPersonDto> AddInvolvedPersonAsync(CreateSafetyIncidentInvolvedPersonDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedIncidentAsync(dto.IncidentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyIncidentInvolvedPerson>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateLostTimeAsync(entity.IncidentId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyIncidentInvolvedPersonDto> UpdateInvolvedPersonAsync(UpdateSafetyIncidentInvolvedPersonDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyIncidentInvolvedPerson>();
        var entity = await GetOwnedInvolvedPersonAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateLostTimeAsync(entity.IncidentId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInvolvedPersonAsync(Guid involvedPersonId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyIncidentInvolvedPerson>();
        var entity = await GetOwnedInvolvedPersonAsync(involvedPersonId);
        var incidentId = entity.IncidentId;
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateLostTimeAsync(incidentId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SafetyIncidentInjuredBodyPartDto> AddInjuredBodyPartAsync(CreateSafetyIncidentInjuredBodyPartDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedInvolvedPersonAsync(dto.InvolvedPersonId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyIncidentInjuredBodyPart>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInjuredBodyPartAsync(Guid injuredBodyPartId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyIncidentInjuredBodyPart>();
        var entity = await GetOwnedInjuredBodyPartAsync(injuredBodyPartId);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Witnesses ──
    public async Task<SafetyIncidentWitnessDto> AddWitnessAsync(CreateSafetyIncidentWitnessDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedIncidentAsync(dto.IncidentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyIncidentWitness>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyIncidentWitnessDto> UpdateWitnessAsync(UpdateSafetyIncidentWitnessDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyIncidentWitness>();
        var entity = await GetOwnedWitnessAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWitnessAsync(Guid witnessId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyIncidentWitness>();
        var entity = await GetOwnedWitnessAsync(witnessId);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Investigation team ──
    public async Task<SafetyIncidentInvestigationTeamMemberDto> AddInvestigationTeamMemberAsync(CreateSafetyIncidentInvestigationTeamMemberDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedIncidentAsync(dto.IncidentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyIncidentInvestigationTeamMember>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveInvestigationTeamMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyIncidentInvestigationTeamMember>();
        var entity = await GetOwnedInvestigationTeamMemberAsync(memberId);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Corrective actions ──
    public async Task<SafetyIncidentCorrectiveActionDto> AddCorrectiveActionAsync(CreateSafetyIncidentCorrectiveActionDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedIncidentAsync(dto.IncidentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _correctiveActionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyIncidentCorrectiveActionDto> UpdateCorrectiveActionAsync(UpdateSafetyIncidentCorrectiveActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCorrectiveActionAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _correctiveActionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> VerifyCorrectiveActionAsync(VerifySafetyIncidentCorrectiveActionDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCorrectiveActionAsync(dto.CorrectiveActionId);

        entity.EffectivenessVerified = true;
        entity.VerificationDate = dto.VerificationDate;
        entity.EffectivenessReviewNotes = dto.EffectivenessReviewNotes;
        entity.VerifiedById = dto.VerifiedById;
        entity.Status = SheCorrectiveActionStatus.Verified;
        Touch(entity, userId);

        await _correctiveActionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCorrectiveActionAsync(Guid correctiveActionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCorrectiveActionAsync(correctiveActionId);
        await _correctiveActionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<SafetyIncidentCorrectiveActionDto>> GetCorrectiveActionsForIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        await GetOwnedIncidentAsync(incidentId);
        var tenantId = GetTenantId();
        return (await _correctiveActionRepository.GetByIncidentIdAsync(incidentId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto());
    }

    public async Task<IEnumerable<SafetyIncidentCorrectiveActionDto>> GetOverdueCorrectiveActionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _correctiveActionRepository.GetOverdueAsync())
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto());
    }

    public async Task<IEnumerable<SafetyIncidentCorrectiveActionDto>> GetCorrectiveActionsByResponsibleAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _correctiveActionRepository.GetByResponsiblePersonAsync(employeeId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto());
    }

    // ── Follow-ups & documents ──
    public async Task<SafetyIncidentFollowUpDto> AddFollowUpAsync(CreateSafetyIncidentFollowUpDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedIncidentAsync(dto.IncidentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyIncidentFollowUp>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SafetyIncidentDocumentDto> AddDocumentAsync(CreateSafetyIncidentDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedIncidentAsync(dto.IncidentId);
        var entity = dto.ToEntity(tenantId, userId);
        await _unitOfWork.Repository<SafetyIncidentDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<SafetyIncidentDocument>();
        var entity = await GetOwnedDocumentAsync(documentId);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Helpers ──
    private async Task RecalculateLostTimeAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _incidentRepository.GetByIdAsync(incidentId);
        if (incident == null || incident.TenantId != GetTenantId()) return;

        var tenantId = GetTenantId();
        var persons = await _unitOfWork.Repository<SafetyIncidentInvolvedPerson>()
            .FindAsync(p => p.IncidentId == incidentId && p.TenantId == tenantId);

        incident.TotalLostDays = persons.Sum(p => p.LostDays ?? 0);
        incident.IsLostTimeInjury = persons.Any(p =>
            p.InjuryClassification == SheInjuryClassification.LostTimeInjury || (p.LostDays ?? 0) > 0);

        await _incidentRepository.UpdateAsync(incident);
    }

    private static void Touch(SafetyIncident entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    private static void Touch(SafetyIncidentCorrectiveAction entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }
}

#endregion
