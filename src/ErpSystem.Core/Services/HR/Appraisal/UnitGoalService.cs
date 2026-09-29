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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnitGoalService> _logger;

    public UnitGoalService(
        IGenericRepository<UnitGoal> unitGoalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<UnitGoalService> logger)
    {
        _unitGoalRepository = unitGoalRepository;
        _attachmentRepository = attachmentRepository;
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

    // A unit goal owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<UnitGoal> GetOwnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitGoalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Unit goal with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<UnitGoal> BaseQuery()
    {
        var tenantId = GetTenantId();
        return _unitGoalRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId)
            .Include(g => g.AppraisalCycle)
            .Include(g => g.ParentCompanyGoal)
            .Include(g => g.OrganizationLevel)
            .Include(g => g.OrganizationUnit)
            .Include(g => g.CreatedByManager);
    }

    public async Task<UnitGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Unit goal with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.AppraisalCycleId == cycleId)
            .OrderBy(g => g.Priority).ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.OrganizationUnitId == orgUnitId)
            .OrderBy(g => g.Priority).ThenBy(g => g.Title)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByCreatedByManagerIdAsync(Guid managerId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().Where(g => g.CreatedByManagerId == managerId);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(g => g.DueDate).ThenBy(g => g.Title).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<UnitGoalDto>> GetByParentCompanyGoalAsync(Guid companyGoalId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery()
            .Where(g => g.ParentCompanyGoalId == companyGoalId)
            .OrderBy(g => g.Priority)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<UnitGoalDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery().AsQueryable();
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
        var tenantId = GetTenantId();
        var query = _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.AppraisalCycleId == cycleId);

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
        var tenantId = GetTenantId();
        var data = await _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.AppraisalCycleId == cycleId)
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
        var tenantId = GetTenantId();
        // The same employee goals the per-employee read lists (tenant-scoped), so the count
        // everyone sees agrees with the rows the desk and the unit's line see (P11).
        var stats = await _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.Id == id)
            .Select(g => new
            {
                g.Id,
                Count = g.EmployeeGoals.Count(eg => eg.TenantId == tenantId),
                Average = g.EmployeeGoals.Where(eg => eg.TenantId == tenantId).Average(eg => (decimal?)eg.ProgressPercent),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats is null
            ? new UnitGoalCascadeStatsDto { GoalId = id, EmployeeGoalsCount = 0 }
            : new UnitGoalCascadeStatsDto
            {
                GoalId                 = stats.Id,
                EmployeeGoalsCount     = stats.Count,
                AverageProgressPercent = stats.Average is decimal average ? Math.Round(average, 1) : null,
            };
    }

    public async Task<IEnumerable<UnitGoalEmployeeGoalSummaryDto>> GetEmployeeGoalSummariesAsync(Guid unitGoalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await _unitGoalRepository.GetQueryable()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.Id == unitGoalId)
            .SelectMany(g => g.EmployeeGoals.Where(eg => eg.TenantId == tenantId))
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
        entity.TenantId = GetTenantId();
        await _unitGoalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal created: {Id} '{Title}'", entity.Id, entity.Title);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<UnitGoalDto> UpdateAsync(UpdateUnitGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);
        updateDto.UpdateEntity(entity);
        await _unitGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        await _unitGoalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal deleted: {Id}", id);
        return true;
    }

    // ─── Attachments ─────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a file to a unit goal.
    ///
    /// <para>Replaces a path that could never have run — see the note on
    /// <c>CheckInService.AddAttachmentAsync</c>. This one additionally never set
    /// <c>EntityType</c>, so even had the FK held, the row would have carried the default
    /// discriminator rather than <c>Goal</c>.</para>
    /// </summary>
    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid goalId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null)
    {
        await GetOwnedAsync(goalId, cancellationToken);
        var tenantId = GetTenantId();

        var entity = new AppraisalAttachment
        {
            TenantId           = tenantId,
            UnitGoalId         = goalId,
            EntityType         = AppraisalAttachmentEntityType.Goal,
            FileName           = fileName,
            FilePath           = string.Empty,
            FileSizeBytes      = fileSizeBytes,
            Description        = description,
            UploadDate         = DateTime.UtcNow,
            UploadedById       = uploadedById,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId   = documentRecordId,
            DocumentVersionId  = documentVersionId,
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _attachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == entity.Id && a.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Attachment added to unit goal {GoalId}: {AttachmentId}", goalId, entity.Id);
        return saved!.ToDto();
    }

    public async Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid goalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.UnitGoalId == goalId && a.TenantId == tenantId, cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(goalId, cancellationToken);
        var tenantId = GetTenantId();
        var entities = await _attachmentRepository.GetQueryable(a => a.UnitGoalId == goalId && a.TenantId == tenantId)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid goalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(goalId, cancellationToken);
        var tenantId = GetTenantId();
        var entity = await _attachmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.UnitGoalId == goalId && a.TenantId == tenantId, cancellationToken);
        if (entity == null)
            throw new ArgumentException("Attachment not found.");
        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Attachment deleted from unit goal {GoalId}: {AttachmentId}", goalId, attachmentId);
        return true;
    }
}
