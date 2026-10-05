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

public class CompanyGoalService : ICompanyGoalService
{
    private readonly IGenericRepository<CompanyGoal> _companyGoalRepository;
    private readonly IGenericRepository<UnitGoal> _unitGoalRepository;
    private readonly IGenericRepository<EmployeeGoal> _employeeGoalRepository;
    private readonly IGenericRepository<CheckInObjectiveLink> _objectiveLinkRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<StrategicGoal> _strategicGoalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanyGoalService> _logger;

    public CompanyGoalService(
        IGenericRepository<CompanyGoal> companyGoalRepository,
        IGenericRepository<UnitGoal> unitGoalRepository,
        IGenericRepository<EmployeeGoal> employeeGoalRepository,
        IGenericRepository<CheckInObjectiveLink> objectiveLinkRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<StrategicGoal> strategicGoalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<CompanyGoalService> logger)
    {
        _companyGoalRepository = companyGoalRepository;
        _unitGoalRepository = unitGoalRepository;
        _employeeGoalRepository = employeeGoalRepository;
        _objectiveLinkRepository = objectiveLinkRepository;
        _cycleRepository = cycleRepository;
        _strategicGoalRepository = strategicGoalRepository;
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

    // A company goal owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<CompanyGoal> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _companyGoalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Company goal with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<CompanyGoal> BaseQuery()
    {
        var tenantId = GetTenantId();
        return _companyGoalRepository.GetQueryable().Where(g => g.TenantId == tenantId);
    }

    public async Task<CompanyGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .Include(g => g.AppraisalCycle)
            .Include(g => g.StrategicGoal)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Company goal with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<CompanyGoalDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.AppraisalCycleId == cycleId)
            .Include(g => g.AppraisalCycle)
            .OrderBy(g => g.Priority)
            .ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<CompanyGoalDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery()
            .Include(g => g.AppraisalCycle)
            .AsQueryable();

        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);

        query = query.OrderByDescending(g => g.AppraisalCycle.Year).ThenBy(g => g.Priority);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<CompanyGoalDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<CompanyGoalDto>> GetVisibleGoalsAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.AppraisalCycleId == cycleId && g.IsVisible)
            .Include(g => g.AppraisalCycle)
            .OrderBy(g => g.Priority)
            .ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    /// <summary>
    /// What hangs off a company goal (performance closure E-g1, D-78): the unit goals cascaded from it, the employee
    /// goals aligned to it, and the check-ins it anchors. Null when nothing does. A link on a deleted check-in is not
    /// a use.
    /// </summary>
    private async Task<string?> DescribeUseAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var unitGoals = await _unitGoalRepository.GetQueryable()
            .CountAsync(u => u.TenantId == tenantId && u.ParentCompanyGoalId == id, cancellationToken);
        var employeeGoals = await _employeeGoalRepository.GetQueryable()
            .CountAsync(g => g.TenantId == tenantId && g.CompanyGoalId == id, cancellationToken);
        var checkIns = await _objectiveLinkRepository.GetQueryable()
            .Where(l => l.TenantId == tenantId && l.CompanyGoalId == id && !l.CheckIn.IsDeleted)
            .Select(l => l.CheckInId)
            .Distinct()
            .CountAsync(cancellationToken);

        return DefinitionUse.Describe(
            new DefinitionUse.Use(unitGoals, "a unit goal", "unit goals"),
            new DefinitionUse.Use(employeeGoals, "an employee goal", "employee goals"),
            new DefinitionUse.Use(checkIns, "a check-in", "check-ins"));
    }

    /// <summary>
    /// The cycle and strategic goal a company goal names are this tenant's (performance closure E-g1, D-79): create and
    /// update stored whatever ids they were sent. A strategic goal newly named must be active — the picker offers only
    /// those; one already linked stays linked. A goal with a cascade under it stays in its cycle: its unit and employee
    /// goals are that cycle's.
    /// </summary>
    private async Task ValidateReferencesAsync(CompanyGoal candidate, CompanyGoal? stored, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if ((stored == null || candidate.AppraisalCycleId != stored.AppraisalCycleId)
            && !await _cycleRepository.GetQueryable()
                .AnyAsync(c => c.Id == candidate.AppraisalCycleId && c.TenantId == tenantId, cancellationToken))
            throw new InvalidOperationException("The appraisal cycle named was not found.");

        if (candidate.StrategicGoalId is Guid strategicId && strategicId != stored?.StrategicGoalId)
        {
            var strategic = await _strategicGoalRepository.GetQueryable()
                .Where(s => s.Id == strategicId && s.TenantId == tenantId)
                .Select(s => new { s.IsActive })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The strategic goal named was not found.");
            if (!strategic.IsActive)
                throw new InvalidOperationException("The strategic goal named is inactive; link the objective to an active one.");
        }

        if (stored != null && candidate.AppraisalCycleId != stored.AppraisalCycleId)
        {
            var uses = await DescribeUseAsync(stored.Id, cancellationToken);
            if (uses != null)
                throw new InvalidOperationException(
                    $"The company goal \"{stored.Title}\" is used by {uses} in its cycle, so it cannot move to another cycle.");
        }
    }

    public async Task<CompanyGoalDto> CreateAsync(CreateCompanyGoalDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await ValidateReferencesAsync(entity, null, cancellationToken);

        await _companyGoalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal created: {Id} '{Title}'", entity.Id, entity.Title);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<CompanyGoalDto> UpdateAsync(UpdateCompanyGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);

        var stored = new CompanyGoal { Id = entity.Id, Title = entity.Title, AppraisalCycleId = entity.AppraisalCycleId, StrategicGoalId = entity.StrategicGoalId };
        updateDto.UpdateEntity(entity);
        await ValidateReferencesAsync(entity, stored, cancellationToken);

        await _companyGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        // A company goal with a cascade is not deleted (E-g1, D-78): its unit and employee goals lost their parent
        // and the check-ins it anchored lost their objective.
        var uses = await DescribeUseAsync(id, cancellationToken);
        if (uses != null)
            throw DefinitionUse.DeleteRefused("company goal", entity.Title, uses,
                "Hide it instead, and it is no longer offered for alignment.");

        await _companyGoalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal deleted: {Id}", id);
        return true;
    }

    public async Task<bool> SetVisibilityAsync(Guid id, bool isVisible, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);

        entity.IsVisible = isVisible;
        await _companyGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal {Id} visibility set to {Visible}", id, isVisible);
        return true;
    }

    public async Task<CompanyGoalCascadeStatsDto> GetCascadeStatsAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var goal = await GetOwnedAsync(goalId, cancellationToken);
        var tenantId = GetTenantId();

        var unitGoalsCount = await _unitGoalRepository.GetQueryable(u =>
                u.TenantId == tenantId && u.ParentCompanyGoalId == goalId)
            .CountAsync(cancellationToken);

        // An employee goal aligned straight to a company goal carries it on CompanyGoalId — the
        // FK behind CompanyGoal.EmployeeGoals. ParentGoalId is the self-referencing parent (another
        // EmployeeGoal), so matching a company goal id against it never found anything and this
        // count and average came back 0 / null on every company goal.
        var employeeGoalsCount = await _employeeGoalRepository.GetQueryable(
            e => e.TenantId == tenantId
              && e.CompanyGoalId == goalId)
            .CountAsync(cancellationToken);

        var avgProgress = await _employeeGoalRepository.GetQueryable(
            e => e.TenantId == tenantId
              && e.CompanyGoalId == goalId
              && (e.Status == GoalStatus.Approved || e.Status == GoalStatus.InProgress))
            .AverageAsync(e => (decimal?)e.ProgressPercent, cancellationToken);

        return new CompanyGoalCascadeStatsDto
        {
            CompanyGoalId = goalId,
            Title = goal.Title,
            UnitGoalsCount = unitGoalsCount,
            EmployeeGoalsCount = employeeGoalsCount,
            AverageEmployeeProgress = avgProgress
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<CompanyGoalListItemDto>> GetDashboardPagedAsync(
        Guid cycleId,
        string? search,
        GoalPriority? priority,
        bool? isVisible,
        DateOnly? dueDateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery()
            .Where(g => g.AppraisalCycleId == cycleId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(g => g.Title.ToLower().Contains(lower) ||
                                     (g.Description != null && g.Description.ToLower().Contains(lower)));
        }

        if (priority.HasValue)
            query = query.Where(g => g.Priority == priority.Value);

        if (isVisible.HasValue)
            query = query.Where(g => g.IsVisible == isVisible.Value);

        if (dueDateTo.HasValue)
            query = query.Where(g => g.DueDate != null && g.DueDate <= dueDateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.Priority)   // Critical → High → Medium → Low
            .ThenBy(g => g.DueDate)
            .ThenBy(g => g.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new CompanyGoalListItemDto
            {
                Id                = g.Id,
                AppraisalCycleId  = g.AppraisalCycleId,
                CycleCode         = g.AppraisalCycle.CycleCode,
                Title             = g.Title,
                DescriptionPreview = g.Description != null && g.Description.Length > 200
                    ? g.Description.Substring(0, 200)
                    : g.Description,
                SuccessCriteria   = g.SuccessCriteria,
                Priority          = g.Priority,
                TargetValue       = g.TargetValue,
                Unit              = g.Unit,
                DueDate           = g.DueDate,
                IsVisible         = g.IsVisible,
                UnitGoalCount     = g.UnitGoals.Count(),
                EmployeeGoalCount = g.EmployeeGoals.Count()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyGoalListItemDto>
        {
            Items     = items,
            TotalCount = totalCount,
            Page      = pageNumber,
            PageSize  = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<CompanyGoalDashboardMetricsDto> GetDashboardMetricsAsync(
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = BaseQuery()
            .Where(g => g.AppraisalCycleId == cycleId)
            .AsNoTracking();

        var totalGoals    = await baseQuery.CountAsync(cancellationToken);
        var visibleGoals  = await baseQuery.CountAsync(g => g.IsVisible, cancellationToken);
        var unitGoals     = await baseQuery.SelectMany(g => g.UnitGoals).CountAsync(cancellationToken);
        var employeeGoals = await baseQuery.SelectMany(g => g.EmployeeGoals).CountAsync(cancellationToken);

        return new CompanyGoalDashboardMetricsDto
        {
            CycleId                   = cycleId,
            TotalGoals                = totalGoals,
            TotalUnitGoalsCascaded    = unitGoals,
            TotalEmployeeGoalsAligned = employeeGoals,
            VisibleGoalsCount         = visibleGoals
        };
    }
}
