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

public class UnitGoalService : IUnitGoalService
{
    private readonly IGenericRepository<UnitGoal> _unitGoalRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IGenericRepository<EmployeeGoal> _employeeGoalRepository;
    private readonly IGenericRepository<CompanyGoal> _companyGoalRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<OrganizationUnit> _unitRepository;
    private readonly IGenericRepository<OrganizationLevel> _levelRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnitGoalService> _logger;

    public UnitGoalService(
        IGenericRepository<UnitGoal> unitGoalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<EmployeeGoal> employeeGoalRepository,
        IGenericRepository<CompanyGoal> companyGoalRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<OrganizationUnit> unitRepository,
        IGenericRepository<OrganizationLevel> levelRepository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<UnitGoalService> logger)
    {
        _unitGoalRepository = unitGoalRepository;
        _attachmentRepository = attachmentRepository;
        _employeeGoalRepository = employeeGoalRepository;
        _companyGoalRepository = companyGoalRepository;
        _cycleRepository = cycleRepository;
        _unitRepository = unitRepository;
        _levelRepository = levelRepository;
        _employeeRepository = employeeRepository;
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

    /// <summary>
    /// What hangs off a unit goal (performance closure E-g1, D-78): the employee goals aligned to it, the unit goals
    /// cascaded from it, and the files attached to it. Null when nothing does.
    /// </summary>
    private async Task<string?> DescribeUseAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var employeeGoals = await _employeeGoalRepository.GetQueryable()
            .CountAsync(g => g.TenantId == tenantId && g.UnitGoalId == id, cancellationToken);
        var childGoals = await _unitGoalRepository.GetQueryable()
            .CountAsync(u => u.TenantId == tenantId && u.ParentUnitGoalId == id, cancellationToken);
        var attachments = await _attachmentRepository.GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && a.UnitGoalId == id, cancellationToken);

        return DefinitionUse.Describe(
            new DefinitionUse.Use(employeeGoals, "an employee goal", "employee goals"),
            new DefinitionUse.Use(childGoals, "a unit goal cascaded from it", "unit goals cascaded from it"),
            new DefinitionUse.Use(attachments, "an attached file", "attached files"));
    }

    /// <summary>
    /// What a unit goal names is this tenant's, and its parents are its cycle's (performance closure E-g1, D-79):
    /// create and update stored whatever ids they were sent — a parent in another cycle or tenant, a unit, level or
    /// author that did not exist, a goal its own parent. A goal with a cascade under it stays in its cycle. An update
    /// checks what it changes (a stored unit since retired does not block an edit of the title); it keeps the author.
    /// </summary>
    private async Task ValidateReferencesAsync(UnitGoal candidate, UnitGoal? stored, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var cycleChanged = stored == null || candidate.AppraisalCycleId != stored.AppraisalCycleId;
        if (cycleChanged && !await _cycleRepository.GetQueryable()
                .AnyAsync(c => c.Id == candidate.AppraisalCycleId && c.TenantId == tenantId, cancellationToken))
            throw new InvalidOperationException("The appraisal cycle named was not found.");
        if ((stored == null || candidate.OrganizationUnitId != stored.OrganizationUnitId)
            && !await _unitRepository.GetQueryable()
                .AnyAsync(u => u.Id == candidate.OrganizationUnitId && u.TenantId == tenantId, cancellationToken))
            throw new InvalidOperationException("The organisation unit named was not found.");
        if ((stored == null || candidate.OrganizationLevelId != stored.OrganizationLevelId)
            && !await _levelRepository.GetQueryable()
                .AnyAsync(l => l.Id == candidate.OrganizationLevelId && l.TenantId == tenantId, cancellationToken))
            throw new InvalidOperationException("The organisation level named was not found.");
        if (stored == null && !await _employeeRepository.GetQueryable()
                .AnyAsync(e => e.Id == candidate.CreatedByManagerId && e.TenantId == tenantId, cancellationToken))
            throw new InvalidOperationException("The manager named as the goal's author was not found.");

        if (candidate.ParentCompanyGoalId is Guid companyGoalId
            && (cycleChanged || companyGoalId != stored!.ParentCompanyGoalId))
        {
            var parentCycle = await _companyGoalRepository.GetQueryable()
                .Where(g => g.Id == companyGoalId && g.TenantId == tenantId)
                .Select(g => (Guid?)g.AppraisalCycleId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The parent company goal named was not found.");
            if (parentCycle != candidate.AppraisalCycleId)
                throw new InvalidOperationException(
                    "The parent company goal belongs to another cycle; a unit goal cascades from its own cycle's goals.");
        }

        if (candidate.ParentUnitGoalId is Guid parentId
            && (cycleChanged || parentId != stored!.ParentUnitGoalId))
        {
            // Walk up from the parent: the goal must not be found above itself.
            var seen = new HashSet<Guid>();
            Guid? cursor = parentId;
            var first = true;
            while (cursor is Guid current)
            {
                if (current == candidate.Id)
                    throw new InvalidOperationException("A unit goal cannot cascade from itself or from a goal cascaded from it.");
                if (!seen.Add(current)) break;
                var parent = await _unitGoalRepository.GetQueryable()
                    .Where(g => g.Id == current && g.TenantId == tenantId)
                    .Select(g => new { g.AppraisalCycleId, g.ParentUnitGoalId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (parent == null)
                {
                    if (first) throw new InvalidOperationException("The parent unit goal named was not found.");
                    break;
                }
                if (first && parent.AppraisalCycleId != candidate.AppraisalCycleId)
                    throw new InvalidOperationException(
                        "The parent unit goal belongs to another cycle; a unit goal cascades from its own cycle's goals.");
                first = false;
                cursor = parent.ParentUnitGoalId;
            }
        }

        if (stored != null && candidate.AppraisalCycleId != stored.AppraisalCycleId)
        {
            var uses = await DescribeUseAsync(stored.Id, cancellationToken);
            if (uses != null)
                throw new InvalidOperationException(
                    $"The unit goal \"{stored.Title}\" is used by {uses} in its cycle, so it cannot move to another cycle.");
        }
    }

    public async Task<UnitGoalDto> CreateAsync(CreateUnitGoalDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await ValidateReferencesAsync(entity, null, cancellationToken);
        await _unitGoalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal created: {Id} '{Title}'", entity.Id, entity.Title);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<UnitGoalDto> UpdateAsync(UpdateUnitGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id, cancellationToken);
        var stored = new UnitGoal
        {
            Id = entity.Id, Title = entity.Title, AppraisalCycleId = entity.AppraisalCycleId,
            OrganizationUnitId = entity.OrganizationUnitId, OrganizationLevelId = entity.OrganizationLevelId,
            ParentCompanyGoalId = entity.ParentCompanyGoalId, ParentUnitGoalId = entity.ParentUnitGoalId,
        };
        var author = entity.CreatedByManagerId;
        updateDto.UpdateEntity(entity);
        // The author is who raised it (E-g1): the body's author re-assigned the goal, and with it who may manage it.
        entity.CreatedByManagerId = author;
        await ValidateReferencesAsync(entity, stored, cancellationToken);
        await _unitGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unit goal updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        // A unit goal with anything under it is not deleted (E-g1, D-78): its employee goals and child goals lost
        // their parent, and its files their goal.
        var uses = await DescribeUseAsync(id, cancellationToken);
        if (uses != null)
            throw DefinitionUse.DeleteRefused("unit goal", entity.Title, uses);
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
