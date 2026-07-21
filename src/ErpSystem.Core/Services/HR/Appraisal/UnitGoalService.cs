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

public class UnitGoalService : IUnitGoalService
{
    private readonly IGenericRepository<UnitGoal> _unitGoalRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnitGoalService> _logger;

    public UnitGoalService(
        IGenericRepository<UnitGoal> unitGoalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IUnitOfWork unitOfWork,
        ILogger<UnitGoalService> logger)
    {
        _unitGoalRepository = unitGoalRepository;
        _attachmentRepository = attachmentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private IQueryable<UnitGoal> BaseQuery => _unitGoalRepository.GetQueryable()
        .Include(g => g.AppraisalCycle)
        .Include(g => g.ParentCompanyGoal)
        .Include(g => g.OrganizationLevel)
        .Include(g => g.OrganizationUnit)
        .Include(g => g.CreatedByManager);

    public async Task<UnitGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Unit goal with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(g => g.AppraisalCycleId == cycleId)
            .OrderBy(g => g.Priority).ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(g => g.OrganizationUnitId == orgUnitId)
            .OrderBy(g => g.Priority).ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByCreatedByManagerIdAsync(Guid managerId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(g => g.CreatedByManagerId == managerId);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(g => g.DueDate).ThenBy(g => g.Title).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByParentCompanyGoalAsync(Guid companyGoalId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(g => g.ParentCompanyGoalId == companyGoalId)
            .OrderBy(g => g.Priority)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<UnitGoalDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.AsQueryable();
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        query = query.OrderByDescending(g => g.AppraisalCycleId).ThenBy(g => g.Priority);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<UnitGoalDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DASHBOARD — projection-based reads (AsNoTracking, no nav collection loading)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<UnitGoalListItemDto>> GetDashboardPagedAsync(
        Guid cycleId, string? search, GoalPriority? priority, Guid? orgUnitId,
        bool? isLinked, Guid? managerEmployeeId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.AppraisalCycleId == cycleId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.Title.Contains(search));

        if (priority.HasValue)
            query = query.Where(g => g.Priority == priority.Value);

        if (orgUnitId.HasValue)
            query = query.Where(g => g.OrganizationUnitId == orgUnitId.Value);

        if (isLinked.HasValue)
            query = isLinked.Value
                ? query.Where(g => g.ParentCompanyGoalId != null)
                : query.Where(g => g.ParentCompanyGoalId == null);

        // Manager scope: only see goals created by this manager
        if (managerEmployeeId.HasValue)
            query = query.Where(g => g.CreatedByManagerId == managerEmployeeId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.OrganizationUnit.Name)
            .ThenBy(g => g.Priority)
            .ThenBy(g => g.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new UnitGoalListItemDto
            {
                Id                     = g.Id,
                AppraisalCycleId       = g.AppraisalCycleId,
                CycleCode              = g.AppraisalCycle.CycleCode,
                ParentCompanyGoalId    = g.ParentCompanyGoalId,
                ParentCompanyGoalTitle = g.ParentCompanyGoal != null ? g.ParentCompanyGoal.Title : null,
                OrganizationUnitId     = g.OrganizationUnitId,
                OrganizationUnitName   = g.OrganizationUnit.Name,
                OrganizationLevelName  = g.OrganizationLevel.Name,
                CreatedByManagerId     = g.CreatedByManagerId,
                ManagerName            = g.CreatedByManager.FullName,
                Title                  = g.Title,
                DescriptionPreview     = g.Description != null
                    ? (g.Description.Length > 200 ? g.Description.Substring(0, 200) : g.Description)
                    : null,
                Priority               = g.Priority,
                TargetValue            = g.TargetValue,
                Unit                   = g.Unit,
                DueDate                = g.DueDate,
                EmployeeGoalsCount     = g.EmployeeGoals.Count()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<UnitGoalListItemDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<UnitGoalDashboardMetricsDto> GetDashboardMetricsAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var data = await _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.AppraisalCycleId == cycleId)
            .Select(g => new
            {
                IsLinked           = g.ParentCompanyGoalId != null,
                EmployeeGoalsCount = g.EmployeeGoals.Count()
            })
            .ToListAsync(cancellationToken);

        return new UnitGoalDashboardMetricsDto
        {
            CycleId                    = cycleId,
            TotalUnitGoals             = data.Count,
            LinkedToCompanyGoal        = data.Count(d => d.IsLinked),
            UnlinkedCount              = data.Count(d => !d.IsLinked),
            TotalEmployeeGoalsCascaded = data.Sum(d => d.EmployeeGoalsCount)
        };
    }

    public async Task<UnitGoalCascadeStatsDto> GetCascadeStatsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new UnitGoalCascadeStatsDto
            {
                GoalId             = g.Id,
                EmployeeGoalsCount = g.EmployeeGoals.Count()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? new UnitGoalCascadeStatsDto { GoalId = id, EmployeeGoalsCount = 0 };
    }

    public async Task<IEnumerable<UnitGoalEmployeeGoalSummaryDto>> GetEmployeeGoalSummariesAsync(Guid unitGoalId, CancellationToken cancellationToken = default)
    {
        return await _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.Id == unitGoalId)
            .SelectMany(g => g.EmployeeGoals)
            .OrderBy(eg => eg.Employee.LastName)
            .ThenBy(eg => eg.Employee.FirstName)
            .Select(eg => new UnitGoalEmployeeGoalSummaryDto
            {
                Id              = eg.Id,
                EmployeeId      = eg.EmployeeId,
                EmployeeName    = eg.Employee.FirstName + " " + eg.Employee.LastName,
                Title           = eg.Title,
                Status          = eg.Status,
                Priority        = eg.Priority,
                ProgressPercent = eg.ProgressPercent,
                DueDate         = eg.DueDate
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UnitGoalDto> CreateAsync(CreateUnitGoalDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        await _unitGoalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal created: {Id} '{Title}'", entity.Id, entity.Title);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<UnitGoalDto> UpdateAsync(UpdateUnitGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _unitGoalRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Unit goal with ID '{updateDto.Id}' not found.");
        updateDto.UpdateEntity(entity);
        await _unitGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitGoalRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Unit goal with ID '{id}' not found.");
        await _unitGoalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal deleted: {Id}", id);
        return true;
    }

    // ─── Attachments ─────────────────────────────────────────────────────────

    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(Guid goalId, CreateAppraisalAttachmentDto dto, CancellationToken cancellationToken = default)
    {
        var goalExists = await _unitGoalRepository.ExistsAsync(g => g.Id == goalId);
        if (!goalExists)
            throw new ArgumentException($"Unit goal with ID '{goalId}' not found.");

        var entity = dto.ToEntity();
        entity.UnitGoalId = goalId;

        entity.UploadDate = DateTime.UtcNow;

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added to unit goal {GoalId}: {AttachmentId}", goalId, entity.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var entities = await _attachmentRepository.GetQueryable(a => a.UnitGoalId == goalId)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid goalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _attachmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.UnitGoalId == goalId, cancellationToken);
        if (entity == null)
            throw new ArgumentException("Attachment not found.");
        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Attachment deleted from unit goal {GoalId}: {AttachmentId}", goalId, attachmentId);
        return true;
    }
}
