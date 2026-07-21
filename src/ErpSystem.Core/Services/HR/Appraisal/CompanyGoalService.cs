using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class CompanyGoalService : ICompanyGoalService
{
    private readonly IGenericRepository<CompanyGoal> _companyGoalRepository;
    private readonly IGenericRepository<UnitGoal> _unitGoalRepository;
    private readonly IGenericRepository<EmployeeGoal> _employeeGoalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanyGoalService> _logger;

    public CompanyGoalService(
        IGenericRepository<CompanyGoal> companyGoalRepository,
        IGenericRepository<UnitGoal> unitGoalRepository,
        IGenericRepository<EmployeeGoal> employeeGoalRepository,
        IUnitOfWork unitOfWork,
        ILogger<CompanyGoalService> logger)
    {
        _companyGoalRepository = companyGoalRepository;
        _unitGoalRepository = unitGoalRepository;
        _employeeGoalRepository = employeeGoalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CompanyGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _companyGoalRepository.GetQueryable()
            .Include(g => g.AppraisalCycle)
            .Include(g => g.StrategicGoal)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Company goal with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<CompanyGoalDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await _companyGoalRepository.GetQueryable(g => g.AppraisalCycleId == cycleId)
            .Include(g => g.AppraisalCycle)
            .OrderBy(g => g.Priority)
            .ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<CompanyGoalDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = _companyGoalRepository.GetQueryable()
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
        var entities = await _companyGoalRepository.GetQueryable(g => g.AppraisalCycleId == cycleId && g.IsVisible)
            .Include(g => g.AppraisalCycle)
            .OrderBy(g => g.Priority)
            .ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<CompanyGoalDto> CreateAsync(CreateCompanyGoalDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();

        await _companyGoalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal created: {Id} '{Title}'", entity.Id, entity.Title);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<CompanyGoalDto> UpdateAsync(UpdateCompanyGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _companyGoalRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Company goal with ID '{updateDto.Id}' not found.");

        updateDto.UpdateEntity(entity);

        await _companyGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _companyGoalRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Company goal with ID '{id}' not found.");

        await _companyGoalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal deleted: {Id}", id);
        return true;
    }

    public async Task<bool> SetVisibilityAsync(Guid id, bool isVisible, CancellationToken cancellationToken = default)
    {
        var entity = await _companyGoalRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Company goal with ID '{id}' not found.");

        entity.IsVisible = isVisible;
        await _companyGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company goal {Id} visibility set to {Visible}", id, isVisible);
        return true;
    }

    public async Task<CompanyGoalCascadeStatsDto> GetCascadeStatsAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var goal = await _companyGoalRepository.GetByIdAsync(goalId);
        if (goal == null)
            throw new ArgumentException($"Company goal with ID '{goalId}' not found.");

        var unitGoalsCount = await _unitGoalRepository.GetQueryable(u => u.ParentCompanyGoalId == goalId)
            .CountAsync(cancellationToken);

        // Employee goals can be linked directly to company goal via ParentGoalId
        var employeeGoalsCount = await _employeeGoalRepository.GetQueryable(
            e => e.ParentGoalId == goalId && e.ParentType == GoalParentType.Company)
            .CountAsync(cancellationToken);

        var avgProgress = await _employeeGoalRepository.GetQueryable(
            e => e.ParentGoalId == goalId && e.ParentType == GoalParentType.Company && 
                 (e.Status == GoalStatus.Approved || e.Status == GoalStatus.InProgress))
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
        var query = _companyGoalRepository.GetQueryable(g => g.AppraisalCycleId == cycleId)
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
        var baseQuery = _companyGoalRepository.GetQueryable(g => g.AppraisalCycleId == cycleId)
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
