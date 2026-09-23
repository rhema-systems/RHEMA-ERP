using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// ONBOARDING PLAN TEMPLATE SERVICE
// ============================================================================

public class OnboardingPlanTemplateService : IOnboardingPlanTemplateService
{
    private readonly IOnboardingPlanTemplateRepository _templateRepository;
    private readonly IOnboardingTaskTemplateRepository _taskTemplateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingPlanTemplateService> _logger;

    public OnboardingPlanTemplateService(
        IOnboardingPlanTemplateRepository templateRepository,
        IOnboardingTaskTemplateRepository taskTemplateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<OnboardingPlanTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _taskTemplateRepository = taskTemplateRepository;
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

    // A template owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<OnboardingPlanTemplate> GetOwnedTemplateAsync(Guid id)
    {
        var entity = await _templateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Onboarding plan template with ID '{id}' not found.");
        return entity;
    }

    // A task template owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<OnboardingTaskTemplate> GetOwnedTaskTemplateAsync(Guid id)
    {
        var entity = await _taskTemplateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Task template with ID '{id}' not found.");
        return entity;
    }

    private async Task ClearDefaultAsync(Guid tenantId, Guid? exceptId, CancellationToken cancellationToken)
    {
        var existingDefault = await _templateRepository.GetQueryable()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IsDefault && (!exceptId.HasValue || t.Id != exceptId.Value), cancellationToken);
        if (existingDefault == null)
            return;

        existingDefault.IsDefault = false;
        await _templateRepository.UpdateAsync(existingDefault);
    }

    public async Task<OnboardingPlanTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTemplateAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OnboardingPlanTemplateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetActiveTemplatesAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    /// <summary>
    /// Copies a template and its tasks under a new name (round 4, lane J1). Mirrors
    /// <c>JobPostingPipelineService.ClonePipelineAsync</c>: the root is added, then each child through
    /// its own repository with its key set — never through the new parent's navigation, which EF
    /// would take for an UPDATE of a row that was never inserted.
    /// </summary>
    /// <remarks>
    /// <para><b>Never the default</b> — at most one per tenant, and a copy stealing the fallback
    /// silently would change which checklist every unmatched hire gets.</para>
    ///
    /// <para><b>Its audience is NOT copied.</b> Since lane I4 a confirmed hire gets the most specific
    /// template whose audience reaches them. A copy carrying the same audience would tie with the
    /// original, and the tie is settled by NAME — the copy could start being handed to new hires by
    /// accident of the alphabet. Uncopied, the copy is chosen by hand until it is given an audience
    /// of its own, which is what a copy is usually made for.</para>
    /// </remarks>
    public async Task<OnboardingPlanTemplateDetailDto> CloneAsync(
        Guid sourceId, CloneOnboardingPlanTemplateDto dto, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var name = dto.NewName?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new InvalidOperationException("The copy needs a name.");

        var source = await _templateRepository.GetWithTaskTemplatesAsync(sourceId);
        if (source == null || source.TenantId != tenantId || source.IsDeleted)
            throw new ArgumentException($"Onboarding plan template with ID '{sourceId}' not found.");

        if (await _templateRepository.GetQueryable().AnyAsync(
                t => t.TenantId == tenantId && !t.IsDeleted && t.Name == name, cancellationToken))
            throw new InvalidOperationException($"A template named \"{name}\" already exists. Choose another name for the copy.");

        var clone = new OnboardingPlanTemplate
        {
            TenantId = tenantId,
            Name = name,
            Description = source.Description,
            IsDefault = false,
            IsActive = source.IsActive,
            CreatedById = createdByUserId,
            CreatedBy = createdByUserId.ToString(),
        };
        await _templateRepository.AddAsync(clone);

        foreach (var task in source.TaskTemplates.Where(t => !t.IsDeleted).OrderBy(t => t.DisplayOrder))
        {
            await _taskTemplateRepository.AddAsync(new OnboardingTaskTemplate
            {
                TenantId = tenantId,
                PlanTemplateId = clone.Id,
                TaskName = task.TaskName,
                Description = task.Description,
                Category = task.Category,
                DueDaysFromStartDate = task.DueDaysFromStartDate,
                IsMandatory = task.IsMandatory,
                DisplayOrder = task.DisplayOrder,
                InstructionsUrl = task.InstructionsUrl,
                OwnerPositionId = task.OwnerPositionId,
                CreatedById = createdByUserId,
                CreatedBy = createdByUserId.ToString(),
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Onboarding plan template copied: {Source} -> {Copy}", source.Name, clone.Name);

        return await GetWithTaskTemplatesAsync(clone.Id, cancellationToken);
    }

    public async Task<OnboardingPlanTemplateDetailDto> GetWithTaskTemplatesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _templateRepository.GetWithTaskTemplatesAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Onboarding plan template with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<OnboardingPlanTemplateDto?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _templateRepository.GetQueryable()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IsDefault, cancellationToken);
        return entity?.ToDto();
    }

    public async Task<OnboardingPlanTemplateDto> CreateAsync(CreateOnboardingPlanTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        if (createDto.IsDefault)
            await ClearDefaultAsync(tenantId, exceptId: null, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _templateRepository.AddAsync(entity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Onboarding plan template created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<OnboardingPlanTemplateDto> UpdateAsync(UpdateOnboardingPlanTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTemplateAsync(updateDto.Id);

        if (updateDto.IsDefault && !entity.IsDefault)
            await ClearDefaultAsync(entity.TenantId, exceptId: entity.Id, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTemplateAsync(id);

        if (entity.IsDefault)
            throw new InvalidOperationException("The default onboarding template cannot be deleted. Assign a new default first.");

        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OnboardingTaskTemplateDto> AddTaskTemplateAsync(CreateOnboardingTaskTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedTemplateAsync(createDto.PlanTemplateId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _taskTemplateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await _taskTemplateRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<IEnumerable<OnboardingTaskTemplateDto>> GetTaskTemplatesAsync(Guid planTemplateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedTemplateAsync(planTemplateId);
        var entities = await _taskTemplateRepository.GetByPlanTemplateIdAsync(planTemplateId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<OnboardingTaskTemplateDto> UpdateTaskTemplateAsync(UpdateOnboardingTaskTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTaskTemplateAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _taskTemplateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTaskTemplateAsync(Guid taskTemplateId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTaskTemplateAsync(taskTemplateId);

        await _taskTemplateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

// ============================================================================
// ONBOARDING PLAN SERVICE
// ============================================================================

public class OnboardingPlanService : IOnboardingPlanService
{
    private readonly IOnboardingPlanRepository _planRepository;
    private readonly IOnboardingTaskRepository _taskRepository;
    private readonly IOnboardingAssetRepository _assetItemRepository;
    private readonly IOnboardingTaskCommentRepository _commentRepository;
    private readonly IOnboardingPlanTemplateRepository _templateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingPlanService> _logger;

    public OnboardingPlanService(
        IOnboardingPlanRepository planRepository,
        IOnboardingTaskRepository taskRepository,
        IOnboardingAssetRepository assetItemRepository,
        IOnboardingTaskCommentRepository commentRepository,
        IOnboardingPlanTemplateRepository templateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<OnboardingPlanService> logger)
    {
        _planRepository = planRepository;
        _taskRepository = taskRepository;
        _assetItemRepository = assetItemRepository;
        _commentRepository = commentRepository;
        _templateRepository = templateRepository;
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

    // A plan owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<OnboardingPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Onboarding plan with ID '{id}' not found.");
        return entity;
    }

    // A task owned by another tenant is reported as missing rather than forbidden, so the endpoints do not
    // confirm that the id exists elsewhere.
    private async Task<OnboardingTask> GetOwnedTaskAsync(Guid id)
    {
        var entity = await _taskRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Onboarding task with ID '{id}' not found.");
        return entity;
    }

    // An asset item owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<OnboardingAsset> GetOwnedAssetAsync(Guid id)
    {
        var entity = await _assetItemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Onboarding asset item with ID '{id}' not found.");
        return entity;
    }

    private async Task<OnboardingPlanTemplate?> EnsureOwnedTemplateAsync(Guid? templatePlanId)
    {
        if (!templatePlanId.HasValue || templatePlanId.Value == Guid.Empty)
            return null;

        var template = await _templateRepository.GetWithTaskTemplatesAsync(templatePlanId.Value);
        if (template == null || template.TenantId != GetTenantId())
            throw new ArgumentException($"Onboarding plan template with ID '{templatePlanId}' not found.");
        return template;
    }

    /// <summary>
    /// Materialises the template's task templates as real tasks on the plan. Both entities have always
    /// documented this step ("Instantiated as an OnboardingTask when a plan is assigned to a new hire")
    /// and nothing performed it, so every plan created from a template came out empty — the template
    /// feature validated the id and then discarded it.
    ///
    /// Due dates are derived from the plan's start date plus the template's DueDaysFromStartDate, which
    /// is the only reason that column exists. OwnerPositionId is carried across so an unassigned task
    /// still records the role that owes it, which is what the entity's own remarks promise.
    /// </summary>
    private async Task InstantiateTemplateTasksAsync(
        OnboardingPlan plan, OnboardingPlanTemplate template, Guid createdByUserId, CancellationToken cancellationToken)
    {
        var taskTemplates = template.TaskTemplates
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.DueDaysFromStartDate)
            .ThenBy(t => t.DisplayOrder)
            .ToList();

        if (taskTemplates.Count == 0)
            return;

        var tasks = taskTemplates.Select((t, index) => new OnboardingTask
        {
            TenantId = plan.TenantId,
            OnboardingPlanId = plan.Id,
            TaskTemplateId = t.Id,
            TaskName = t.TaskName,
            Description = t.Description,
            Category = t.Category,
            Status = OnboardingTaskStatus.Pending,
            DueDate = plan.StartDate.AddDays(t.DueDaysFromStartDate),
            IsMandatory = t.IsMandatory,
            OwnerPositionId = t.OwnerPositionId,
            DisplayOrder = t.DisplayOrder != 0 ? t.DisplayOrder : index + 1,
            CreatedBy = createdByUserId.ToString(),
        }).ToList();

        await _taskRepository.AddRangeAsync(tasks);
        _logger.LogInformation(
            "Instantiated {Count} task(s) from template {TemplateId} onto onboarding plan {PlanId}",
            tasks.Count, template.Id, plan.Id);
    }

    public async Task<OnboardingPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(id);
        return entity.ToDto();
    }

    public async Task<OnboardingPlanDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _planRepository.GetByEmployeeIdAsync(employeeId);
        var scoped = entities.Where(p => p.TenantId == tenantId).ToList();
        var active = scoped.FirstOrDefault(p => p.Status == OnboardingStatus.InProgress)
            ?? scoped.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        return active?.ToDto();
    }

    public async Task<OnboardingPlanDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _planRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Onboarding plan with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<OnboardingPlanSummaryDto>> GetByStatusAsync(OnboardingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _planRepository.GetByStatusAsync(status);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<OnboardingPlanDto> CreateAsync(CreateOnboardingPlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var template = await EnsureOwnedTemplateAsync(createDto.TemplatePlanId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.Status = OnboardingStatus.NotStarted;

        await _planRepository.AddAsync(entity);

        if (template != null)
            await InstantiateTemplateTasksAsync(entity, template, createdByUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Onboarding plan created for employee {EmployeeId}", createDto.EmployeeId);

        // Re-read: the in-memory entity has no Employee/TemplatePlan loaded and its Tasks collection is
        // whatever we just attached, so returning it would blank the names and mis-state the roll-ups.
        return (await _planRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<OnboardingPlanDto> UpdateAsync(UpdateOnboardingPlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(updateDto.Id);

        if (entity.Status == OnboardingStatus.Completed)
            throw new InvalidOperationException("A completed onboarding plan cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> StartAsync(Guid planId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(planId);

        entity.Status = OnboardingStatus.InProgress;

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid planId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPlanAsync(planId);

        entity.Status = OnboardingStatus.Completed;
        entity.ActualCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GetOverdueTasksCountAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await GetOwnedPlanAsync(planId);
        return await _planRepository.GetOverdueTasksCountAsync(planId);
    }

    // ── Tasks ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<OnboardingTaskDto>> GetTasksAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedPlanAsync(planId);
        var entities = await _taskRepository.GetByPlanIdAsync(planId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<OnboardingTaskDto> AddTaskAsync(CreateOnboardingTaskDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(createDto.OnboardingPlanId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _taskRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read for the assignee / owning-unit names the DTO promises; the entity we just built has
        // FKs but no navigations, so returning it raw gives a row whose every name column is blank.
        return (await _taskRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<OnboardingTaskDto> UpdateTaskAsync(UpdateOnboardingTaskDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTaskAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<OnboardingTaskDto> CompleteTaskAsync(
        CompleteOnboardingTaskDto completeDto, Guid completedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTaskAsync(completeDto.TaskId);

        if (entity.Status == OnboardingTaskStatus.Completed)
            throw new InvalidOperationException("This task has already been completed.");

        // A task that asks for a second-party sign-off is not finished until it gets one; parking it in
        // Completed would make RequiresVerification a decorative flag that refuses nothing.
        entity.Status = entity.RequiresVerification
            ? OnboardingTaskStatus.PendingVerification
            : OnboardingTaskStatus.Completed;

        entity.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        entity.CompletedById = completedByEmployeeId;
        entity.CompletionNotes = completeDto.CompletionNotes;
        entity.EvidenceFilePath = completeDto.EvidenceFilePath;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = completedByEmployeeId.ToString();

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _taskRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<OnboardingTaskDto> VerifyTaskAsync(
        VerifyOnboardingTaskDto verifyDto, Guid verifiedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedTaskAsync(verifyDto.TaskId);

        if (!entity.RequiresVerification)
            throw new InvalidOperationException("This task does not require verification.");
        if (entity.CompletedById == null)
            throw new InvalidOperationException("This task cannot be verified before it has been completed.");
        if (entity.CompletedById == verifiedByEmployeeId)
            throw new InvalidOperationException("A task cannot be verified by the person who completed it.");

        entity.Status = OnboardingTaskStatus.Completed;
        entity.VerifiedById = verifiedByEmployeeId;
        entity.VerifiedDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = verifiedByEmployeeId.ToString();

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _taskRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    // ── Asset items ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<OnboardingAssetDto>> GetAssetItemsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedPlanAsync(planId);
        var entities = await _assetItemRepository.GetByPlanIdAsync(planId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<OnboardingAssetDto> AddAssetItemAsync(CreateOnboardingAssetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(createDto.OnboardingPlanId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _assetItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await _assetItemRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<OnboardingAssetDto> UpdateAssetItemAsync(UpdateOnboardingAssetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssetAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _assetItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    // ── Task comments ─────────────────────────────────────────────────────────

    public async Task<OnboardingTaskCommentDto> AddTaskCommentAsync(CreateOnboardingTaskCommentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedTaskAsync(createDto.TaskId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _commentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read for AuthorName — a comment thread that renders the new post with no author beside it
        // reads as a bug to every user who posts one.
        return (await _commentRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<IEnumerable<OnboardingTaskCommentDto>> GetTaskCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedTaskAsync(taskId);
        var entities = await _commentRepository.GetByTaskIdAsync(taskId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<OnboardingTaskDto>> GetTasksByStatusAsync(OnboardingTaskStatus status, Guid? planId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (planId.HasValue)
            await GetOwnedPlanAsync(planId.Value);

        var entities = await _taskRepository.GetByStatusAsync(status, planId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<OnboardingTaskDto>> GetOverdueTasksAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _taskRepository.GetOverdueTasksAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<OnboardingTaskDto>> GetTasksByAssigneeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _taskRepository.GetByAssignedToAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<OnboardingAssetDto>> GetAssetsByStatusAsync(OnboardingAssetProvisionStatus status, Guid? planId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (planId.HasValue)
            await GetOwnedPlanAsync(planId.Value);

        var entities = await _assetItemRepository.GetByStatusAsync(status, planId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }
}
