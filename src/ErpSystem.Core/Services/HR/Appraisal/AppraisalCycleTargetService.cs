using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Appraisal Cycle Target

/// <summary>
/// Who a cycle covers — its targets and their exclusions — written through one door: the cycle's nested
/// routes (the cycle page, the demo pack) and this service's own routes (the harness) both come here. The
/// cycle service kept a second copy of the target writes, with no duplicate check (performance closure
/// E-c).
/// </summary>
/// <remarks>
/// A target stays in its cycle; names the one scope its type says; is the only one for that scope in its
/// cycle; and, like its exclusions, changes only while the cycle is not closed. Its live count is read
/// through <see cref="AppraisalCycleScope"/>, the rule generation uses.
/// </remarks>
public class AppraisalCycleTargetService : IAppraisalCycleTargetService
{
    private readonly IGenericRepository<AppraisalCycleTarget> _targetRepository;
    private readonly IGenericRepository<AppraisalCycleTargetExclusion> _exclusionRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<EmployeePosition> _positionRepository;
    private readonly IGenericRepository<OrganizationUnit> _unitRepository;
    private readonly IGenericRepository<OrganizationLevel> _levelRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleTargetService> _logger;

    public AppraisalCycleTargetService(
        IGenericRepository<AppraisalCycleTarget> targetRepository,
        IGenericRepository<AppraisalCycleTargetExclusion> exclusionRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<EmployeePosition> positionRepository,
        IGenericRepository<OrganizationUnit> unitRepository,
        IGenericRepository<OrganizationLevel> levelRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleTargetService> logger)
    {
        _targetRepository = targetRepository;
        _exclusionRepository = exclusionRepository;
        _cycleRepository = cycleRepository;
        _employeeRepository = employeeRepository;
        _positionRepository = positionRepository;
        _unitRepository = unitRepository;
        _levelRepository = levelRepository;
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

    // An appraisal cycle owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalCycle> GetOwnedCycleAsync(Guid cycleId)
    {
        var entity = await _cycleRepository.GetByIdAsync(cycleId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");
        return entity;
    }

    /// <summary>
    /// The cycle a target or exclusion write changes, refused once it is closed: a closed cycle's scope is
    /// the record of whom it appraised (the writes took any status).
    /// </summary>
    private async Task<AppraisalCycle> GetWritableCycleAsync(Guid cycleId)
    {
        var cycle = await GetOwnedCycleAsync(cycleId);
        if (cycle.Status == AppraisalCycleStatus.Closed || cycle.ClosedDate.HasValue)
            throw new InvalidOperationException($"Cycle '{cycle.CycleCode}' is closed: who it covers can no longer change.");
        return cycle;
    }

    // A cycle target owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere. Through a cycle's nested routes, only that cycle's.
    private async Task<AppraisalCycleTarget> GetOwnedAsync(Guid id, Guid? cycleId = null)
    {
        var entity = await _targetRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId() || (cycleId.HasValue && entity.AppraisalCycleId != cycleId.Value))
            throw new ArgumentException($"Appraisal cycle target with ID '{id}' not found.");
        return entity;
    }

    // An exclusion owned by another tenant is reported as missing rather than forbidden.
    private async Task<AppraisalCycleTargetExclusion> GetOwnedExclusionAsync(Guid targetId, Guid exclusionId)
    {
        var entity = await _exclusionRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == exclusionId && e.AppraisalCycleTargetId == targetId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Exclusion not found for this cycle target.");
        return entity;
    }

    /// <summary>
    /// The one scope a target of this type names; the other two are cleared, as the target dialog says
    /// they are. It picks a unit under its level, so a unit target arrives with the level beside it — which
    /// the old "exactly one id" check refused, so no unit target could be made from the screen — while a
    /// position target naming only a unit was accepted and covered nobody. The scope must be this
    /// organisation's. A type that is none of the three is refused: <c>Employee</c> (4) is gone, and a
    /// number still binds.
    /// </summary>
    private async Task<(Guid? LevelId, Guid? UnitId, Guid? PositionId)> ScopeOfAsync(
        AppraisalTargetType type, Guid? levelId, Guid? unitId, Guid? positionId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        switch (type)
        {
            case AppraisalTargetType.Position:
                if (positionId is not { } position)
                    throw new InvalidOperationException("A position target needs the position it covers.");
                if (!await _positionRepository.GetQueryable().AnyAsync(p => p.Id == position && p.TenantId == tenantId, cancellationToken))
                    throw new InvalidOperationException("The target's position was not found.");
                return (null, null, position);

            case AppraisalTargetType.OrganizationUnit:
                if (unitId is not { } unit)
                    throw new InvalidOperationException("An organisation unit target needs the unit it covers.");
                if (!await _unitRepository.GetQueryable().AnyAsync(u => u.Id == unit && u.TenantId == tenantId, cancellationToken))
                    throw new InvalidOperationException("The target's organisation unit was not found.");
                return (null, unit, null);

            case AppraisalTargetType.OrganizationLevel:
                if (levelId is not { } level)
                    throw new InvalidOperationException("An organisation level target needs the level it covers.");
                if (!await _levelRepository.GetQueryable().AnyAsync(l => l.Id == level && l.TenantId == tenantId, cancellationToken))
                    throw new InvalidOperationException("The target's organisation level was not found.");
                return (level, null, null);

            default:
                throw new InvalidOperationException(
                    "A target covers a position, an organisation unit or an organisation level. One person is covered through their position, or left out by an exclusion.");
        }
    }

    /// <summary>
    /// One target per scope in a cycle — the nested create never checked, and no update did, so a target
    /// could be added twice or edited into another's scope.
    /// </summary>
    private async Task EnsureNotDuplicateAsync(
        Guid cycleId, AppraisalTargetType type, (Guid? LevelId, Guid? UnitId, Guid? PositionId) scope,
        Guid? exceptTargetId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var sameType = _targetRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId && t.TargetType == type);
        if (exceptTargetId is { } except)
            sameType = sameType.Where(t => t.Id != except);

        var duplicate = type switch
        {
            AppraisalTargetType.Position => await sameType.AnyAsync(t => t.PositionId == scope.PositionId, cancellationToken),
            AppraisalTargetType.OrganizationUnit => await sameType.AnyAsync(t => t.OrganizationUnitId == scope.UnitId, cancellationToken),
            _ => await sameType.AnyAsync(t => t.OrganizationLevelId == scope.LevelId, cancellationToken),
        };

        if (duplicate)
            throw new InvalidOperationException(
                "This cycle already has a target for that scope — edit that one, or switch it back on, instead.");
    }

    private IQueryable<AppraisalCycleTarget> WithNames() => _targetRepository.GetQueryable()
        .Include(t => t.AppraisalCycle)
        .Include(t => t.OrganizationLevel)
        .Include(t => t.OrganizationUnit)
        .Include(t => t.Position);

    /// <summary>
    /// The targets as DTOs, each with its live count: how many of its staff the cycle appraises, through
    /// the scope rule generation uses. It was the typed estimate less exclusions that were never loaded —
    /// so the estimate, however wrong, and it could go below zero. An inactive target reaches no one.
    /// </summary>
    private async Task<List<AppraisalCycleTargetDto>> ToDtosAsync(
        IReadOnlyList<AppraisalCycleTarget> targets, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var counts = new Dictionary<Guid, int>();
        foreach (var cycleId in targets.Select(t => t.AppraisalCycleId).Distinct())
        {
            var scope = await AppraisalCycleScope.ResolveCycleAsync(
                _targetRepository.GetQueryable(), _employeeRepository.GetQueryable(), _unitRepository.GetQueryable(),
                tenantId, cycleId, cancellationToken);
            foreach (var target in targets.Where(t => t.AppraisalCycleId == cycleId))
                counts[target.Id] = scope.InScopeCountOf(target.Id);
        }

        return targets.Select(t =>
        {
            var dto = t.ToDto();
            dto.ActiveEmployeeCount = counts.GetValueOrDefault(t.Id);
            return dto;
        }).ToList();
    }

    public async Task<AppraisalCycleTargetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await WithNames()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Appraisal cycle target with ID '{id}' not found.");

        return (await ToDtosAsync(new[] { entity }, cancellationToken))[0];
    }

    public async Task<IEnumerable<AppraisalCycleTargetDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        var entities = await WithNames()
            .Where(t => t.TenantId == tenantId && t.AppraisalCycleId == cycleId)
            .OrderBy(t => t.TargetType)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(entities, cancellationToken);
    }

    public async Task<IEnumerable<AppraisalCycleTargetDto>> GetByTargetTypeAsync(AppraisalTargetType targetType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await WithNames()
            .Where(t => t.TenantId == tenantId && t.TargetType == targetType)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(entities, cancellationToken);
    }

    public async Task<AppraisalCycleTargetDto> CreateAsync(CreateAppraisalCycleTargetDto createDto, CancellationToken cancellationToken = default)
    {
        await GetWritableCycleAsync(createDto.AppraisalCycleId);
        var scope = await ScopeOfAsync(createDto.TargetType, createDto.OrganizationLevelId, createDto.OrganizationUnitId, createDto.PositionId, cancellationToken);
        await EnsureNotDuplicateAsync(createDto.AppraisalCycleId, createDto.TargetType, scope, null, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        (entity.OrganizationLevelId, entity.OrganizationUnitId, entity.PositionId) = scope;

        await _targetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle target created successfully: {targetId}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AppraisalCycleTargetDto> UpdateAsync(UpdateAppraisalCycleTargetDto updateDto, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cycleId);
        await GetWritableCycleAsync(entity.AppraisalCycleId);
        var scope = await ScopeOfAsync(updateDto.TargetType, updateDto.OrganizationLevelId, updateDto.OrganizationUnitId, updateDto.PositionId, cancellationToken);
        await EnsureNotDuplicateAsync(entity.AppraisalCycleId, updateDto.TargetType, scope, entity.Id, cancellationToken);

        updateDto.UpdateEntity(entity);
        (entity.OrganizationLevelId, entity.OrganizationUnitId, entity.PositionId) = scope;

        await _targetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle target updated successfully: {targetId}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cycleId);
        await GetWritableCycleAsync(entity.AppraisalCycleId);

        await _targetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal cycle target deleted: {targetId}", id);

        return true;
    }

    public async Task<bool> ValidateTargetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // Valid when it names exactly the scope its type says.
        var isValid = entity.TargetType switch
        {
            AppraisalTargetType.Position => entity.PositionId.HasValue && entity.OrganizationUnitId == null && entity.OrganizationLevelId == null,
            AppraisalTargetType.OrganizationUnit => entity.OrganizationUnitId.HasValue && entity.PositionId == null && entity.OrganizationLevelId == null,
            AppraisalTargetType.OrganizationLevel => entity.OrganizationLevelId.HasValue && entity.PositionId == null && entity.OrganizationUnitId == null,
            _ => false,
        };

        if (!isValid)
        {
            _logger.LogWarning("Appraisal cycle target {targetId} does not name the one scope its type {targetType} says", id, entity.TargetType);
        }

        return isValid;
    }

    // ─── Exclusions ───────────────────────────────────────────────────────────

    public async Task<AppraisalCycleTargetExclusionDto> AddExclusionAsync(
        Guid targetId, CreateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default)
    {
        var target = await GetOwnedAsync(targetId);
        await GetWritableCycleAsync(target.AppraisalCycleId);
        var tenantId = GetTenantId();

        var entity = dto.ToEntity();
        entity.AppraisalCycleTargetId = targetId;
        entity.IsActive = true;
        entity.TenantId = tenantId;

        await _exclusionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Exclusion added to cycle target {TargetId}: {ExclusionId}", targetId, entity.Id);

        entity = await _exclusionRepository.GetQueryable()
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == entity.Id && e.TenantId == tenantId, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTargetExclusionDto>> GetExclusionsAsync(
        Guid targetId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(targetId);
        var tenantId = GetTenantId();

        var entities = await _exclusionRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.AppraisalCycleTargetId == targetId)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.Position)
            .Include(e => e.Employee)
            .OrderByDescending(e => e.IsActive)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalCycleTargetExclusionDto> UpdateExclusionAsync(
        Guid targetId, UpdateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default)
    {
        var target = await GetOwnedAsync(targetId);
        await GetWritableCycleAsync(target.AppraisalCycleId);
        var entity = await GetOwnedExclusionAsync(targetId, dto.Id);

        // The exclusion stays on its target, whatever the body says.
        dto.AppraisalCycleTargetId = targetId;
        dto.UpdateEntity(entity);
        await _exclusionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle target exclusion updated: {ExclusionId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> RemoveExclusionAsync(
        Guid targetId, Guid exclusionId, CancellationToken cancellationToken = default)
    {
        var target = await GetOwnedAsync(targetId);
        await GetWritableCycleAsync(target.AppraisalCycleId);
        var entity = await GetOwnedExclusionAsync(targetId, exclusionId);

        await _exclusionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cycle target exclusion removed: {ExclusionId}", exclusionId);
        return true;
    }
}

#endregion Appraisal Cycle Target
