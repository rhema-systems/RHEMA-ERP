using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// COMPETENCY SERVICE
// ============================================================================

#region Competency Service

public class CompetencyService : ICompetencyService
{
    private readonly ICompetencyRepository _competencyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompetencyService> _logger;

    public CompetencyService(
        ICompetencyRepository competencyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CompetencyService> logger)
    {
        _competencyRepository = competencyRepository;
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

    private async Task<Competency> GetOwnedCompetencyAsync(Guid id)
    {
        var entity = await _competencyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Competency with ID '{id}' not found.");
        return entity;
    }

    public async Task<CompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCompetencyAsync(id);
        return entity.ToDto();
    }

    public async Task<CompetencyDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _competencyRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Competency with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<CompetencyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<PagedResult<CompetencyDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _competencyRepository.GetQueryable().Where(c => c.TenantId == tenantId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.CompetencyCategory)
            .ThenBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CompetencyDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize,
        };
    }

    public async Task<CompetencyDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _competencyRepository.GetByCodeAsync(code);
        // Codes are unique per tenant, so a match owned by another tenant is reported as no match.
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<CompetencyDto>> GetByCategoryAsync(CompetencyCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetByCategoryAsync(category);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<CompetencyDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<CompetencyLookupDto>> GetLookupListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetActiveAsync();
        return entities.Where(e => e.TenantId == tenantId).ToLookupDtoList();
    }

    public async Task<IEnumerable<CompetencyDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetByPositionAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<CompetencyDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetByEmployeeAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var normalized = (code ?? string.Empty).Trim().ToLower();
        var query = _competencyRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && c.Code.ToLower() == normalized);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<CompetencyDto> CreateAsync(CreateCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Codes are unique per tenant: an unscoped check would let one tenant's codes block another's.
        var normalized = (createDto.Code ?? string.Empty).Trim().ToLower();
        var duplicate = await _competencyRepository.GetQueryable()
            .AnyAsync(c => c.TenantId == tenantId && c.Code.ToLower() == normalized, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A competency with code '{createDto.Code}' already exists.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _competencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency created: {Code} — {Name}", entity.Code, entity.Name);

        return entity.ToDto();
    }

    public async Task<CompetencyDto> UpdateAsync(UpdateCompetencyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCompetencyAsync(updateDto.Id);
        var tenantId = GetTenantId();

        if (!string.Equals(entity.Code, updateDto.Code, StringComparison.OrdinalIgnoreCase))
        {
            var normalized = (updateDto.Code ?? string.Empty).Trim().ToLower();
            var duplicate = await _competencyRepository.GetQueryable()
                .AnyAsync(c => c.TenantId == tenantId && c.Id != entity.Id && c.Code.ToLower() == normalized, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"A competency with code '{updateDto.Code}' already exists.");
        }

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _competencyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency updated: {Code} — {Name}", entity.Code, entity.Name);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCompetencyAsync(id);

        await _competencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency deleted: {Code} — {Name}", entity.Code, entity.Name);

        return true;
    }
}

#endregion

// ============================================================================
// COMPETENCY SKILL INDICATOR SERVICE
// ============================================================================

#region Competency Skill Indicator Service

public class CompetencySkillIndicatorService : ICompetencySkillIndicatorService
{
    private readonly ICompetencySkillIndicatorRepository _indicatorRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompetencySkillIndicatorService> _logger;

    public CompetencySkillIndicatorService(
        ICompetencySkillIndicatorRepository indicatorRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CompetencySkillIndicatorService> logger)
    {
        _indicatorRepository = indicatorRepository;
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

    private async Task<CompetencySkillIndicator> GetOwnedIndicatorAsync(Guid id)
    {
        var entity = await _indicatorRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Competency skill indicator with ID '{id}' not found.");
        return entity;
    }

    public async Task<CompetencySkillIndicatorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIndicatorAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CompetencySkillIndicatorDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _indicatorRepository.GetByCompetencyIdAsync(competencyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<CompetencySkillIndicatorDto>> GetBySkillIdAsync(Guid skillId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _indicatorRepository.GetBySkillIdAsync(skillId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<CompetencySkillIndicatorDto?> GetByCompetencyAndSkillAsync(Guid competencyId, Guid skillId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _indicatorRepository.GetByCompetencyAndSkillAsync(competencyId, skillId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<CompetencySkillIndicatorDto> CreateAsync(CreateCompetencySkillIndicatorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var existing = await _indicatorRepository.GetByCompetencyAndSkillAsync(createDto.CompetencyId, createDto.SkillId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("A skill indicator for this competency–skill pairing already exists. Update the existing record instead.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _indicatorRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency skill indicator created: CompetencyId={CompetencyId}, SkillId={SkillId}", entity.CompetencyId, entity.SkillId);

        return entity.ToDto();
    }

    public async Task<CompetencySkillIndicatorDto> UpdateAsync(UpdateCompetencySkillIndicatorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIndicatorAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _indicatorRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency skill indicator updated: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIndicatorAsync(id);

        await _indicatorRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency skill indicator deleted: {Id}", id);

        return true;
    }
}

#endregion

// ============================================================================
// POSITION COMPETENCY SERVICE
// ============================================================================

#region Position Competency Service

public class PositionCompetencyService : IPositionCompetencyService
{
    private readonly IPositionCompetencyRepository _positionCompetencyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PositionCompetencyService> _logger;

    public PositionCompetencyService(
        IPositionCompetencyRepository positionCompetencyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PositionCompetencyService> logger)
    {
        _positionCompetencyRepository = positionCompetencyRepository;
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

    private async Task<PositionCompetency> GetOwnedPositionCompetencyAsync(Guid id)
    {
        var entity = await _positionCompetencyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Position competency with ID '{id}' not found.");
        return entity;
    }

    public async Task<PositionCompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPositionCompetencyAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<PositionCompetencyDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _positionCompetencyRepository.GetByPositionIdAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<PositionCompetencyDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _positionCompetencyRepository.GetByCompetencyIdAsync(competencyId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<PositionCompetencyDto?> GetByPositionAndCompetencyAsync(Guid positionId, Guid competencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _positionCompetencyRepository.GetByPositionAndCompetencyAsync(positionId, competencyId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<PositionCompetencyDto> CreateAsync(CreatePositionCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var existing = await _positionCompetencyRepository.GetByPositionAndCompetencyAsync(createDto.PositionId, createDto.CompetencyId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("This competency is already assigned to the position. Update the existing record instead.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _positionCompetencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position competency created: PositionId={PositionId}, CompetencyId={CompetencyId}", entity.PositionId, entity.CompetencyId);

        return entity.ToDto();
    }

    public async Task<PositionCompetencyDto> UpdateAsync(UpdatePositionCompetencyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPositionCompetencyAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _positionCompetencyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position competency updated: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPositionCompetencyAsync(id);

        await _positionCompetencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position competency deleted: {Id}", id);

        return true;
    }

    public async Task<IEnumerable<PositionCompetencyDto>> BulkSetForPositionAsync(BulkSetPositionCompetenciesDto bulkSetDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var newRequirements = bulkSetDto.Competencies
            .Select(c => c.ToEntity(bulkSetDto.PositionId, tenantId, createdByUserId))
            .ToList();

        await _positionCompetencyRepository.BulkReplaceForPositionAsync(bulkSetDto.PositionId, newRequirements);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Position competency set bulk-replaced: PositionId={PositionId}, Count={Count}", bulkSetDto.PositionId, newRequirements.Count);

        var result = await _positionCompetencyRepository.GetByPositionIdAsync(bulkSetDto.PositionId);
        return result.Where(e => e.TenantId == tenantId).ToDtoList();
    }
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY SERVICE
// ============================================================================

#region Employee Competency Service

public class EmployeeCompetencyService : IEmployeeCompetencyService
{
    private readonly IEmployeeCompetencyRepository _employeeCompetencyRepository;
    private readonly IEmployeeCompetencyHistoryRepository _historyRepository;
    private readonly IPositionCompetencyRepository _positionCompetencyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeCompetencyService> _logger;

    public EmployeeCompetencyService(
        IEmployeeCompetencyRepository employeeCompetencyRepository,
        IEmployeeCompetencyHistoryRepository historyRepository,
        IPositionCompetencyRepository positionCompetencyRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeCompetencyService> logger)
    {
        _employeeCompetencyRepository = employeeCompetencyRepository;
        _historyRepository = historyRepository;
        _positionCompetencyRepository = positionCompetencyRepository;
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

    private async Task<EmployeeCompetency> GetOwnedEmployeeCompetencyAsync(Guid id)
    {
        var entity = await _employeeCompetencyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employee competency with ID '{id}' not found.");
        return entity;
    }

    public async Task<EmployeeCompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEmployeeCompetencyAsync(id);
        return entity.ToDto();
    }

    public async Task<EmployeeCompetencyDetailDto> GetWithHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _employeeCompetencyRepository.GetWithHistoryAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Employee competency with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeCompetencyDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _employeeCompetencyRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<EmployeeCompetencyDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _employeeCompetencyRepository.GetByCompetencyIdAsync(competencyId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<EmployeeCompetencyDto?> GetByEmployeeAndCompetencyAsync(Guid employeeId, Guid competencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _employeeCompetencyRepository.GetByEmployeeAndCompetencyAsync(employeeId, competencyId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<EmployeeCompetencyDto> CreateAsync(CreateEmployeeCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        var existing = await _employeeCompetencyRepository.GetByEmployeeAndCompetencyAsync(createDto.EmployeeId, createDto.CompetencyId);
        if (existing != null && existing.TenantId == tenantId)
            throw new InvalidOperationException("An assessment record already exists for this employee–competency pair. Use the update operation to record a re-assessment.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _employeeCompetencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee competency assessment created: EmployeeId={EmployeeId}, CompetencyId={CompetencyId}", entity.EmployeeId, entity.CompetencyId);

        return entity.ToDto();
    }

    public async Task<EmployeeCompetencyDto> UpdateAsync(UpdateEmployeeCompetencyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEmployeeCompetencyAsync(updateDto.Id);
        var tenantId = GetTenantId();

        // Snapshot current values before applying the update (tenant-scoped history write).
        var snapshot = entity.ToHistorySnapshot(updateDto.ChangeReason, tenantId, updatedByUserId);
        await _historyRepository.AddAsync(snapshot);

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _employeeCompetencyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee competency re-assessed: EmployeeId={EmployeeId}, CompetencyId={CompetencyId}, NewLevel={Level}", entity.EmployeeId, entity.CompetencyId, entity.CurrentProficiencyLevel);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedEmployeeCompetencyAsync(id);

        await _employeeCompetencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee competency deleted: {Id}", id);

        return true;
    }

    public async Task<EmployeePositionCompetencyGapSummaryDto> GetGapsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Load employee for display fields and current position
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        // Load position for title
        var position = await _unitOfWork.Repository<EmployeePosition>().GetByIdAsync(employee.PositionId);
        if (position != null && position.TenantId != tenantId)
            position = null;

        // Load all position requirements and employee assessments
        var requirements = (await _positionCompetencyRepository.GetByPositionIdAsync(employee.PositionId))
            .Where(r => r.TenantId == tenantId)
            .ToList();
        var assessments   = (await _employeeCompetencyRepository.GetByEmployeeIdAsync(employeeId))
            .Where(a => a.TenantId == tenantId);
        var assessmentMap = assessments.ToDictionary(a => a.CompetencyId);

        // Build per-competency gap entries
        var gapEntries = requirements
            .Select(req =>
            {
                assessmentMap.TryGetValue(req.CompetencyId, out var assessment);

                GapStatus? gapStatus = assessment == null
                    ? null
                    : assessment.CurrentProficiencyLevel > req.RequiredProficiencyLevel
                        ? GapStatus.Exceeded
                        : assessment.CurrentProficiencyLevel == req.RequiredProficiencyLevel
                            ? GapStatus.Met
                            : GapStatus.Gap;

                return new EmployeeCompetencyGapDto
                {
                    CompetencyId          = req.CompetencyId,
                    CompetencyCode        = req.Competency?.Code ?? string.Empty,
                    CompetencyName        = req.Competency?.Name ?? string.Empty,
                    CompetencyCategory    = req.Competency?.CompetencyCategory ?? default,
                    ProficiencyScaleMax   = req.Competency?.ProficiencyScaleMax ?? 5,
                    RequiredLevel         = req.RequiredProficiencyLevel,
                    CurrentLevel          = assessment?.CurrentProficiencyLevel,
                    GapStatus             = gapStatus,
                };
            })
            .ToList();

        return new EmployeePositionCompetencyGapSummaryDto
        {
            EmployeeId         = employee.Id,
            EmployeeName       = employee.FullName,
            EmployeeNumber     = employee.EmployeeNumber,
            PositionId         = employee.PositionId,
            PositionTitle      = position?.Title ?? string.Empty,
            TotalCompetencies  = requirements.Count,
            MetOrExceeded      = gapEntries.Count(g => g.GapStatus == GapStatus.Met || g.GapStatus == GapStatus.Exceeded),
            HasGap             = gapEntries.Count(g => g.GapStatus == GapStatus.Gap),
            NotAssessed        = gapEntries.Count(g => g.CurrentLevel == null),
            CompetencyGaps     = gapEntries,
        };
    }

    public async Task<EmployeeCompetencyProfileDto> GetEmployeeProfileAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var position = await _unitOfWork.Repository<EmployeePosition>().GetByIdAsync(employee.PositionId);
        if (position != null && position.TenantId != tenantId)
            position = null;

        var assessments = (await _employeeCompetencyRepository.GetByEmployeeIdAsync(employeeId))
            .Where(a => a.TenantId == tenantId)
            .ToList();
        var assessmentDtos = assessments.ToDtoList().ToList();

        return new EmployeeCompetencyProfileDto
        {
            EmployeeId        = employee.Id,
            EmployeeName      = employee.FullName,
            EmployeeNumber    = employee.EmployeeNumber,
            PositionTitle     = position?.Title ?? string.Empty,
            Competencies      = assessmentDtos,
            TotalAssessed     = assessments.Count,
            LastAssessmentDate = assessments.Count > 0 ? assessments.Max(a => a.AssessmentDate) : null,
        };
    }

    public async Task<IEnumerable<EmployeeCompetencyDto>> GetStaleAssessmentsAsync(int monthsOld = 12, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var cutoff = DateTime.UtcNow.AddMonths(-monthsOld);
        var entities = await _employeeCompetencyRepository.GetAssessmentsOlderThanAsync(cutoff);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<EmployeeCompetencyDto>> GetQualifiedEmployeesForPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _employeeCompetencyRepository.GetQualifiedEmployeesForPositionAsync(positionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<BatchAssessmentResultDto> BatchAssessAsync(
        EmployeeCompetencyAssessmentUpdateDto dto,
        Guid tenantId,
        Guid assessorUserId,
        CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var result = new BatchAssessmentResultDto { TotalSubmitted = dto.Updates.Count };

        foreach (var item in dto.Updates)
        {
            try
            {
                if (item.EmployeeCompetencyId.HasValue)
                {
                    // Re-assessment: history is snapshotted inside UpdateAsync
                    var updateDto = new UpdateEmployeeCompetencyDto
                    {
                        Id                      = item.EmployeeCompetencyId.Value,
                        CurrentProficiencyLevel = item.NewLevel,
                        AssessmentDate          = DateTime.UtcNow,
                        AssessedById            = assessorUserId,
                        AssessmentMethod        = item.AssessmentMethod,
                        EvidenceNotes           = item.EvidenceNotes,
                        ChangeReason            = item.ChangeReason ?? "Assessment update",
                    };
                    await UpdateAsync(updateDto, assessorUserId, cancellationToken);
                }
                else if (item.CompetencyId.HasValue)
                {
                    // First assessment: create a new record
                    var createDto = new CreateEmployeeCompetencyDto
                    {
                        EmployeeId              = dto.EmployeeId,
                        CompetencyId            = item.CompetencyId.Value,
                        CurrentProficiencyLevel = item.NewLevel,
                        AssessmentDate          = DateTime.UtcNow,
                        AssessedById            = assessorUserId,
                        AssessmentMethod        = item.AssessmentMethod,
                        EvidenceNotes           = item.EvidenceNotes,
                    };
                    await CreateAsync(createDto, tenantId, assessorUserId, cancellationToken);
                }
                else
                {
                    throw new ArgumentException("Each update item must supply either EmployeeCompetencyId (re-assessment) or CompetencyId (first assessment).");
                }

                result.Succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Batch assessment error: EmployeeCompetencyId={EcId}, CompetencyId={CId}",
                    item.EmployeeCompetencyId, item.CompetencyId);

                result.Failed++;
                result.Errors.Add(new BatchAssessmentErrorDto
                {
                    EmployeeCompetencyId = item.EmployeeCompetencyId,
                    CompetencyId         = item.CompetencyId,
                    ErrorMessage         = ex.Message,
                });
            }
        }

        _logger.LogInformation(
            "Batch assessment: EmployeeId={EmployeeId}, Succeeded={Succeeded}/{Total}",
            dto.EmployeeId, result.Succeeded, result.TotalSubmitted);

        return result;
    }
}

#endregion

// ============================================================================
// EMPLOYEE COMPETENCY HISTORY SERVICE
// ============================================================================

#region Employee Competency History Service

public class EmployeeCompetencyHistoryService : IEmployeeCompetencyHistoryService
{
    private readonly IEmployeeCompetencyHistoryRepository _historyRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeCompetencyHistoryService> _logger;

    public EmployeeCompetencyHistoryService(
        IEmployeeCompetencyHistoryRepository historyRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeCompetencyHistoryService> logger)
    {
        _historyRepository = historyRepository;
        _currentUserProvider = currentUserProvider;
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

    private async Task<EmployeeCompetencyHistory> GetOwnedHistoryAsync(Guid id)
    {
        var entity = await _historyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Competency history record with ID '{id}' not found.");
        return entity;
    }

    public async Task<EmployeeCompetencyHistoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHistoryAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeCompetencyHistoryDto>> GetByEmployeeCompetencyIdAsync(Guid employeeCompetencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _historyRepository.GetByEmployeeCompetencyIdAsync(employeeCompetencyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeCompetencyHistoryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _historyRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeCompetencyHistoryDto>> GetByCompetencyIdAsync(Guid competencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _historyRepository.GetByCompetencyIdAsync(competencyId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<EmployeeCompetencyHistoryDto?> GetLatestAsync(Guid employeeCompetencyId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _historyRepository.GetLatestAsync(employeeCompetencyId);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }
}

#endregion
