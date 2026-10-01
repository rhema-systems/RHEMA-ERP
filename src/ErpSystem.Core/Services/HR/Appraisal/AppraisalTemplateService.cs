using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class AppraisalTemplateService : IAppraisalTemplateService
{
    private readonly IGenericRepository<AppraisalTemplate> _templateRepository;
    private readonly IGenericRepository<AppraisalTemplateSection> _sectionRepository;
    private readonly IGenericRepository<AppraisalTemplateItem> _itemRepository;
    private readonly IGenericRepository<TemplateItemGradeRange> _gradeRangeRepository;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly IGenericRepository<AppraisalCompetency> _competencyRepository;
    private readonly IGenericRepository<KpiDefinition> _kpiRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalCycleTemplate> _cycleTemplateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalTemplateService> _logger;

    /// <summary>
    /// Entity type registered with the workflow engine. Must match the catalog entry in
    /// <c>WorkflowEntityTypeCatalogService</c> and the aliases on
    /// <c>AppraisalTemplateWorkflowStatusAdapter</c>.
    /// </summary>
    private const string EntityType = "AppraisalTemplate";

    public AppraisalTemplateService(
        IGenericRepository<AppraisalTemplate> templateRepository,
        IGenericRepository<AppraisalTemplateSection> sectionRepository,
        IGenericRepository<AppraisalTemplateItem> itemRepository,
        IGenericRepository<TemplateItemGradeRange> gradeRangeRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        IGenericRepository<AppraisalCompetency> competencyRepository,
        IGenericRepository<KpiDefinition> kpiRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _sectionRepository = sectionRepository;
        _itemRepository = itemRepository;
        _gradeRangeRepository = gradeRangeRepository;
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _competencyRepository = competencyRepository;
        _kpiRepository = kpiRepository;
        _appraisalRepository = appraisalRepository;
        _cycleTemplateRepository = cycleTemplateRepository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
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

    // An appraisal template owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalTemplate> GetOwnedAsync(Guid id)
    {
        var entity = await _templateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal template with ID '{id}' not found.");
        return entity;
    }

    private async Task<AppraisalTemplateSection> GetOwnedSectionAsync(Guid templateId, Guid sectionId)
    {
        var entity = await _sectionRepository.GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.AppraisalTemplateId == templateId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Template section not found.");
        return entity;
    }

    private async Task<AppraisalTemplateItem> GetOwnedItemAsync(Guid sectionId, Guid itemId)
    {
        var entity = await _itemRepository.GetQueryable()
            .Include(i => i.Section)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.AppraisalTemplateSectionId == sectionId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException("Template item not found.");
        return entity;
    }

    // ─── CRUD ────────────────────────────────────────────────────────────────

    public async Task<AppraisalTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _templateRepository.GetQueryable()
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .Include(t => t.Sections.OrderBy(s => s.DisplayOrder))
                .ThenInclude(s => s.TemplateItems.OrderBy(i => i.DisplayOrder))
                    .ThenInclude(i => i.Competency)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(i => i.KpiDefinition)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Appraisal template with ID '{id}' not found.");

        return (await WithLocksAsync(new List<AppraisalTemplateDto> { entity.ToDto() }, cancellationToken))[0];
    }

    public async Task<IEnumerable<AppraisalTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return await WithLocksAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<IEnumerable<AppraisalTemplateSummaryDto>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .Include(t => t.CycleAssignments)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        var locks = await GetLockReasonsAsync(entities.Select(t => t.Id).ToList(), cancellationToken);
        return entities.Select(t => new AppraisalTemplateSummaryDto
        {
            Id                    = t.Id,
            TemplateName          = t.TemplateName,
            Description           = t.Description,
            OrganizationLevelId   = t.OrganizationLevelId,
            OrganizationLevelName = t.OrganizationLevel?.Name,
            OrganizationUnitId    = t.OrganizationUnitId,
            OrganizationUnitName  = t.OrganizationUnit?.Name,
            PositionId            = t.PositionId,
            PositionTitle         = t.Position?.Title,
            IsActive              = t.IsActive,
            ApprovalStatus        = t.ApprovalStatus,
            SectionsCount         = t.Sections.Count,
            TotalItemsCount       = t.Sections.Sum(s => s.TemplateItems.Count),
            HasCycleAssignments   = t.CycleAssignments.Any(),
            IsLocked              = locks.ContainsKey(t.Id),
            LockReason            = locks.GetValueOrDefault(t.Id)
        }).ToList();
    }

    public async Task<PagedResult<AppraisalTemplateDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .OrderBy(t => t.TemplateName);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<AppraisalTemplateDto>
        {
            Items = await WithLocksAsync(items.ToDtoList(), cancellationToken),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AppraisalTemplateDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.PositionId == positionId)
            .Include(t => t.Position)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return await WithLocksAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<IEnumerable<AppraisalTemplateDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.OrganizationUnitId == orgUnitId)
            .Include(t => t.OrganizationUnit)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return await WithLocksAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<IEnumerable<AppraisalTemplateDto>> GetByOrganizationLevelIdAsync(Guid orgLevelId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.OrganizationLevelId == orgLevelId)
            .Include(t => t.OrganizationLevel)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return await WithLocksAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<IEnumerable<AppraisalTemplateDto>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .Include(t => t.OrganizationLevel)
            .Include(t => t.OrganizationUnit)
            .Include(t => t.Position)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return await WithLocksAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<AppraisalTemplateDto> CreateAsync(CreateAppraisalTemplateDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _templateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template created: {Id} '{Name}'", entity.Id, entity.TemplateName);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AppraisalTemplateDto> UpdateAsync(UpdateAppraisalTemplateDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        // Nothing changes while it awaits approval (performance closure E-e, D-66). Its name and description are
        // words, not structure, and stay editable while it is locked; its scope decides who is scored on it, so a
        // scope change is structural — refused while locked, and an approved template goes back to Draft.
        EnsureNotAwaitingApproval(entity);
        var scopeChanged = entity.OrganizationLevelId != updateDto.OrganizationLevelId
                        || entity.OrganizationUnitId != updateDto.OrganizationUnitId
                        || entity.PositionId != updateDto.PositionId;
        if (scopeChanged)
            await EnsureStructureEditableAsync(entity.Id, cancellationToken);

        // Activation checks the weights and bands through this route as through the active-status one; it
        // skipped them here.
        if (updateDto.IsActive && !entity.IsActive)
            await ValidateTemplateWeightsAsync(entity.Id, cancellationToken);

        updateDto.UpdateEntity(entity);
        if (scopeChanged)
            ReturnToDraftIfApproved(entity);

        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await EnsureStructureEditableAsync(id, cancellationToken);
        var tenantId = GetTenantId();

        // A template on a cycle is not deleted (performance closure E-e, D-68): generation silently dropped the
        // link, and the people it covered fell to another template or to none. It comes off its cycles first.
        var cycles = await _cycleTemplateRepository.GetQueryable()
            .Where(l => l.TenantId == tenantId && l.AppraisalTemplateId == id)
            .Select(l => l.AppraisalCycle.CycleName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);
        if (cycles.Count > 0)
            throw new AppraisalConfigurationLockedException(
                $"This template is on {(cycles.Count == 1 ? "the cycle" : "the cycles")} " +
                $"{string.Join(", ", cycles.Select(n => $"'{n}'"))}: remove it from " +
                $"{(cycles.Count == 1 ? "that cycle" : "them")} before deleting it.");

        // Its sections, items and bands go with it — they stayed, pointing at a deleted template.
        var sections = await _sectionRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.AppraisalTemplateId == id)
            .ToListAsync(cancellationToken);
        var sectionIds = sections.Select(s => s.Id).ToList();
        var items = await _itemRepository.GetQueryable()
            .Where(i => i.TenantId == tenantId && sectionIds.Contains(i.AppraisalTemplateSectionId))
            .ToListAsync(cancellationToken);
        var itemIds = items.Select(i => i.Id).ToList();
        var ranges = await _gradeRangeRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && itemIds.Contains(r.AppraisalTemplateItemId))
            .ToListAsync(cancellationToken);

        foreach (var range in ranges)
            await _gradeRangeRepository.DeleteAsync(range);
        foreach (var item in items)
            await _itemRepository.DeleteAsync(item);
        foreach (var section in sections)
            await _sectionRepository.DeleteAsync(section);

        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Appraisal template deleted: {Id}, with {sections} section(s), {items} item(s) and {ranges} grade range(s)",
            id, sections.Count, items.Count, ranges.Count);
        return true;
    }

    public async Task<bool> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // Validate weight integrity before activation
        if (isActive)
            await ValidateTemplateWeightsAsync(id, cancellationToken);

        entity.IsActive = isActive;
        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template {Id} active status set to {Status}", id, isActive);
        return true;
    }

    // ── Approval workflow ──────────────────────────────────────────────────
    // Submit / approve / reject / recall all run through the generic workflow engine, so the
    // routing (who signs a template off, and in how many steps) is configuration rather than
    // code. This service never sets ApprovalStatus itself — the engine reports an outcome and
    // AppraisalTemplateWorkflowStatusAdapter maps it onto the entity.
    //
    // Two ids are in play and they are not interchangeable: the engine resolves approvers by
    // ApplicationUser, while the template's own SubmittedById / ApprovedById columns follow
    // the pre-existing convention of holding an Employee id. Each is written from its own
    // source rather than from whichever one happened to be at hand.

    public async Task<AppraisalTemplateDto> SubmitForApprovalAsync(Guid id, Guid submittedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        if (entity.ApprovalStatus == TemplateApprovalStatus.PendingApproval)
            throw new InvalidOperationException("This template is already awaiting approval.");
        if (entity.ApprovalStatus == TemplateApprovalStatus.Approved)
            throw new InvalidOperationException("This template is already approved.");

        // Ensure the template is structurally valid before it goes to HR.
        await ValidateTemplateWeightsAsync(id, cancellationToken);

        // Submitting must never approve — with no published definition the engine returns Approved
        // and the adapter maps it to an approved template, publishing an appraisal template nobody
        // reviewed. Defence in depth; an APPRAISAL_TEMPLATE definition is seeded. See
        // HrWorkflowFallbackAuthority.
        var (workflowResult, submitOutcome) =
            await HrWorkflowFallbackAuthority.SubmitAsync(_workflowIntegrationService, EntityType, id);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start the template approval workflow.");

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplySubmitOutcome(entity, submitOutcome, _currentUserProvider.UserId);

        entity.SubmittedById = submittedByEmployeeId == Guid.Empty ? null : submittedByEmployeeId;
        entity.SubmittedDate = DateTime.UtcNow;

        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template {Id} submitted for approval by employee {Employee}", id, submittedByEmployeeId);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<AppraisalTemplateDto> ApproveAsync(Guid id, Guid approvedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        // Only what was submitted is decided (performance closure E-e): with no definition published, an approver
        // could approve a Draft nobody submitted — past the weight check only the submission runs.
        if (entity.ApprovalStatus != TemplateApprovalStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only a template awaiting approval can be approved; this one is {entity.ApprovalStatus}.");

        var approvalOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserProvider, EntityType, id, userId,
            "Approve", null, "approve an appraisal template", HrPermissions.ApprovePerformance);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, approvalOutcome, userId);

        // Only stamp the approver once the engine says the whole chain has passed; an
        // intermediate step leaves the template Pending and unattributed. On the no-workflow path
        // there is no chain, so a single approval is the whole of it and this stamps.
        if (approvalOutcome == WorkflowOutcome.Approved)
            entity.ApprovedById = approvedByEmployeeId == Guid.Empty ? null : approvedByEmployeeId;

        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template {Id} approval step processed by employee {Employee}", id, approvedByEmployeeId);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<AppraisalTemplateDto> RejectAsync(Guid id, Guid rejectedByEmployeeId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        // As the approval: an approved template — perhaps already on a cycle — is not rejected after the fact.
        if (entity.ApprovalStatus != TemplateApprovalStatus.PendingApproval)
            throw new InvalidOperationException(
                $"Only a template awaiting approval can be rejected; this one is {entity.ApprovalStatus}.");

        var rejectionText = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();

        var rejectionOutcome = await HrWorkflowFallbackAuthority.ProcessApprovalAsync(
            _workflowIntegrationService, _currentUserProvider, EntityType, id, userId,
            "Reject", rejectionText, "reject an appraisal template", HrPermissions.ApprovePerformance);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, rejectionOutcome, userId, rejectionText);

        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template {Id} rejected by employee {Employee}", id, rejectedByEmployeeId);
        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Pulls a submitted template back to Draft so its author can keep editing it. Allowed
    /// only while it is still awaiting a decision — once HR has ruled, the way back is a new
    /// submission, not a recall.
    /// </summary>
    public async Task<AppraisalTemplateDto> RecallAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        var userId = RequireUserId();

        if (entity.ApprovalStatus != TemplateApprovalStatus.PendingApproval)
            throw new InvalidOperationException("Only a template still awaiting approval can be recalled.");

        // Skipped when nothing is published; the adapter returns the template to Draft either way.
        await HrWorkflowFallbackAuthority.RecallAsync(_workflowIntegrationService, EntityType, id, userId);

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(entity, userId);

        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template {Id} recalled to draft", id);
        return await GetByIdAsync(id, cancellationToken);
    }

    private Guid RequireUserId()
    {
        var userId = _currentUserProvider.UserId;
        if (userId == Guid.Empty)
            throw new UnauthorizedAccessException("User not authenticated.");
        return userId;
    }

    public async Task<AppraisalTemplateDto> CloneAsync(Guid sourceTemplateId, CopyAppraisalTemplateDto dto, CancellationToken cancellationToken = default)
    {
        // A rule, not a missing record — it answered 404.
        if (string.IsNullOrWhiteSpace(dto.NewTemplateName))
            throw new InvalidOperationException("A name is required for the copied template.");

        var tenantId = GetTenantId();
        var source = await _templateRepository.GetQueryable()
            .Where(t => t.Id == sourceTemplateId && t.TenantId == tenantId)
            .Include(t => t.Sections.OrderBy(s => s.DisplayOrder))
                .ThenInclude(s => s.TemplateItems.OrderBy(i => i.DisplayOrder))
                    .ThenInclude(i => i.GradeRanges)
            .FirstOrDefaultAsync(t => t.Id == sourceTemplateId, cancellationToken);

        if (source == null)
            throw new ArgumentException($"Source template with ID '{sourceTemplateId}' not found.");

        // A band on a deleted grade is not copied (performance closure E-g1, D-79): generation and the forms drop it,
        // so the copy would carry a band nobody is scored on.
        var liveGrades = await LiveGradeIdsAsync(
            source.Sections.SelectMany(s => s.TemplateItems).SelectMany(i => i.GradeRanges).Select(r => r.GradeDefinitionId),
            cancellationToken);

        var clone = new AppraisalTemplate
        {
            TemplateName = dto.NewTemplateName,
            Description = source.Description,
            // Scope targets the copy explicitly (not inherited from source).
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            IsActive = false, // New copies are inactive until explicitly activated
            TenantId = tenantId
        };

        foreach (var srcSection in source.Sections)
        {
            var cloneSection = new AppraisalTemplateSection
            {
                SectionName = srcSection.SectionName,
                Description = srcSection.Description,
                DisplayOrder = srcSection.DisplayOrder,
                Weight = srcSection.Weight,
                // A copy of a goals section is a goals section (lane L).
                Kind = srcSection.Kind,
                TenantId = tenantId
            };

            foreach (var srcItem in srcSection.TemplateItems)
            {
                var cloneItem = new AppraisalTemplateItem
                {
                    CompetencyId = srcItem.CompetencyId,
                    KpiDefinitionId = srcItem.KpiDefinitionId,
                    KpiTargetValue = srcItem.KpiTargetValue,
                    KpiMinValue = srcItem.KpiMinValue,
                    KpiMaxValue = srcItem.KpiMaxValue,
                    CustomQuestion = srcItem.CustomQuestion,
                    DisplayOrder = srcItem.DisplayOrder,
                    Weight = srcItem.Weight,
                    TenantId = tenantId
                };

                foreach (var srcRange in srcItem.GradeRanges.Where(r => liveGrades.Contains(r.GradeDefinitionId)))
                {
                    cloneItem.GradeRanges.Add(new TemplateItemGradeRange
                    {
                        GradeDefinitionId = srcRange.GradeDefinitionId,
                        LowScore = srcRange.LowScore,
                        HighScore = srcRange.HighScore,
                        TenantId = tenantId
                    });
                }

                cloneSection.TemplateItems.Add(cloneItem);
            }

            clone.Sections.Add(cloneSection);
        }

        await _templateRepository.AddAsync(clone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template copied from {SourceId} to {CloneId} as '{Name}'", sourceTemplateId, clone.Id, dto.NewTemplateName);

        return await GetByIdAsync(clone.Id, cancellationToken);
    }

    // ─── Section Operations ──────────────────────────────────────────────────

    public async Task<AppraisalTemplateSectionDto> AddSectionAsync(Guid templateId, CreateAppraisalTemplateSectionDto dto, CancellationToken cancellationToken = default)
    {
        var template = await EnsureStructureEditableAsync(templateId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.AppraisalTemplateId = templateId;
        entity.TenantId = tenantId;

        if (entity.Kind == AppraisalSectionKind.EmployeeGoals)
            await EnsureNoOtherGoalsSectionAsync(templateId, null, cancellationToken);

        // Auto-assign display order if not provided
        if (entity.DisplayOrder == 0)
        {
            var maxOrder = await _sectionRepository.GetQueryable()
                .Where(s => s.TenantId == tenantId && s.AppraisalTemplateId == templateId)
                .MaxAsync(s => (int?)s.DisplayOrder, cancellationToken) ?? 0;
            entity.DisplayOrder = maxOrder + 1;
        }

        await _sectionRepository.AddAsync(entity);
        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _sectionRepository.GetQueryable()
            .Include(s => s.TemplateItems)
            .FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Section added to template {TemplateId}: {SectionId}", templateId, entity!.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalTemplateSectionDto>> GetSectionsAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(templateId);
        var tenantId = GetTenantId();

        var entities = await _sectionRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.AppraisalTemplateId == templateId)
            .Include(s => s.TemplateItems.OrderBy(i => i.DisplayOrder))
                .ThenInclude(i => i.Competency)
            .Include(s => s.TemplateItems)
                .ThenInclude(i => i.KpiDefinition)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalTemplateSectionDto> UpdateSectionAsync(Guid templateId, UpdateAppraisalTemplateSectionDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSectionAsync(templateId, dto.Id);

        // A section stays on its template (performance closure E-e): the body's template id moved it, past that
        // template's lock and across tenants. Moving it is copying the template.
        if (dto.AppraisalTemplateId != Guid.Empty && dto.AppraisalTemplateId != templateId)
            throw new InvalidOperationException("A section stays on its template; it cannot be moved to another one.");

        var template = await EnsureStructureEditableAsync(templateId, cancellationToken);

        // Becoming a goals section: the only one on the template, and empty — its rows are each
        // employee's goals, and items kept beside them would count the section twice (lane L).
        if (dto.Kind == AppraisalSectionKind.EmployeeGoals && entity.Kind != AppraisalSectionKind.EmployeeGoals)
        {
            await EnsureNoOtherGoalsSectionAsync(templateId, entity.Id, cancellationToken);
            var tenantId = GetTenantId();
            if (await _itemRepository.GetQueryable().AnyAsync(i => i.TenantId == tenantId && i.AppraisalTemplateSectionId == entity.Id, cancellationToken))
                throw new InvalidOperationException(
                    "A section with items cannot become a goals section: its rows are each employee's goals. Remove its items first.");
        }

        dto.UpdateEntity(entity);
        await _sectionRepository.UpdateAsync(entity);
        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template section updated: {SectionId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSectionAsync(Guid templateId, Guid sectionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSectionAsync(templateId, sectionId);

        var template = await EnsureStructureEditableAsync(templateId, cancellationToken);

        await _sectionRepository.DeleteAsync(entity);
        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template section deleted: {SectionId}", sectionId);
        return true;
    }

    public async Task<bool> ReorderSectionsAsync(Guid templateId, IEnumerable<Guid> orderedSectionIds, CancellationToken cancellationToken = default)
    {
        // The forms lay a template out in its order, live, so the order is part of its structure (E-e, D-66).
        var template = await EnsureStructureEditableAsync(templateId, cancellationToken);
        var tenantId = GetTenantId();

        var sections = await _sectionRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.AppraisalTemplateId == templateId)
            .ToListAsync(cancellationToken);

        var order = orderedSectionIds.ToList();
        foreach (var section in sections)
        {
            var idx = order.IndexOf(section.Id);
            if (idx >= 0)
                section.DisplayOrder = idx + 1;
        }

        foreach (var section in sections)
            await _sectionRepository.UpdateAsync(section);

        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template {TemplateId} sections reordered", templateId);
        return true;
    }

    // ─── Item Operations ─────────────────────────────────────────────────────

    public async Task<AppraisalTemplateItemDto> AddItemAsync(Guid sectionId, CreateAppraisalTemplateItemDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var section = await _sectionRepository.GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.TenantId == tenantId, cancellationToken);
        if (section == null)
            throw new ArgumentException($"Template section with ID '{sectionId}' not found.");

        var template = await EnsureStructureEditableAsync(section.AppraisalTemplateId, cancellationToken);

        // A goals section is filled by each employee's locked goals (lane L); an item beside them
        // would share the section's weight with every goal and count the section twice.
        if (section.Kind == AppraisalSectionKind.EmployeeGoals)
            throw new InvalidOperationException(
                "This is a goals section: it is filled by each employee's locked goals and takes no items. Add the item to a fixed section.");

        await EnsureItemDefinitionsAsync(dto.CompetencyId, dto.KpiDefinitionId, null, null, cancellationToken);

        // Duplicate check: same competency/KPI cannot appear more than once across all sections of the template
        if (dto.CompetencyId.HasValue)
        {
            var duplicate = await _itemRepository.GetQueryable()
                .Include(i => i.Section)
                .AnyAsync(i => i.TenantId == tenantId
                            && i.Section.AppraisalTemplateId == section.AppraisalTemplateId
                            && i.CompetencyId == dto.CompetencyId, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException("This soft skill has already been added to this template.");
        }
        else if (dto.KpiDefinitionId.HasValue)
        {
            var duplicate = await _itemRepository.GetQueryable()
                .Include(i => i.Section)
                .AnyAsync(i => i.TenantId == tenantId
                            && i.Section.AppraisalTemplateId == section.AppraisalTemplateId
                            && i.KpiDefinitionId == dto.KpiDefinitionId, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException("This KPI definition has already been added to this template.");
        }

        var entity = dto.ToEntity();
        entity.AppraisalTemplateSectionId = sectionId;
        entity.TenantId = tenantId;

        if (entity.DisplayOrder == 0)
        {
            var maxOrder = await _itemRepository.GetQueryable()
                .Where(i => i.TenantId == tenantId && i.AppraisalTemplateSectionId == sectionId)
                .MaxAsync(i => (int?)i.DisplayOrder, cancellationToken) ?? 0;
            entity.DisplayOrder = maxOrder + 1;
        }

        await _itemRepository.AddAsync(entity);
        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _itemRepository.GetQueryable()
            .Include(i => i.Competency)
            .Include(i => i.KpiDefinition)
            .Include(i => i.GradeRanges)
            .FirstOrDefaultAsync(i => i.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Item added to section {SectionId}: {ItemId}", sectionId, entity!.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalTemplateItemDto>> GetItemsAsync(Guid sectionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _itemRepository.GetQueryable()
            .Where(i => i.TenantId == tenantId && i.AppraisalTemplateSectionId == sectionId)
            .Include(i => i.Competency)
            .Include(i => i.KpiDefinition)
            .Include(i => i.GradeRanges)
            .OrderBy(i => i.DisplayOrder)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<AppraisalTemplateItemDto> UpdateItemAsync(Guid sectionId, UpdateAppraisalTemplateItemDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _itemRepository.GetQueryable()
            .Include(i => i.Section)
            .Include(i => i.Competency)
            .Include(i => i.KpiDefinition)
            .Include(i => i.GradeRanges)
            .FirstOrDefaultAsync(i => i.Id == dto.Id && i.AppraisalTemplateSectionId == sectionId && i.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Template item not found.");

        // An item stays in its section (performance closure E-e): the body's section id moved it into another
        // template, a goals section or another tenant, past each check — which all ran on where it came from.
        if (dto.AppraisalTemplateSectionId != Guid.Empty && dto.AppraisalTemplateSectionId != sectionId)
            throw new InvalidOperationException("An item stays in its section; it cannot be moved to another one.");

        var template = await EnsureStructureEditableAsync(entity.Section.AppraisalTemplateId, cancellationToken);

        await EnsureItemDefinitionsAsync(dto.CompetencyId, dto.KpiDefinitionId, entity.CompetencyId, entity.KpiDefinitionId, cancellationToken);

        // Duplicate check: same competency/KPI cannot appear more than once across all sections (exclude self)
        if (dto.CompetencyId.HasValue)
        {
            var duplicate = await _itemRepository.GetQueryable()
                .Include(i => i.Section)
                .AnyAsync(i => i.TenantId == tenantId
                            && i.Section.AppraisalTemplateId == entity.Section.AppraisalTemplateId
                            && i.CompetencyId == dto.CompetencyId
                            && i.Id != entity.Id, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException("This soft skill has already been added to this template.");
        }
        else if (dto.KpiDefinitionId.HasValue)
        {
            var duplicate = await _itemRepository.GetQueryable()
                .Include(i => i.Section)
                .AnyAsync(i => i.TenantId == tenantId
                            && i.Section.AppraisalTemplateId == entity.Section.AppraisalTemplateId
                            && i.KpiDefinitionId == dto.KpiDefinitionId
                            && i.Id != entity.Id, cancellationToken);
            if (duplicate)
                throw new InvalidOperationException("This KPI definition has already been added to this template.");
        }

        dto.UpdateEntity(entity);
        await _itemRepository.UpdateAsync(entity);
        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template item updated: {ItemId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid sectionId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(sectionId, itemId);

        var template = await EnsureStructureEditableAsync(entity.Section.AppraisalTemplateId, cancellationToken);

        await _itemRepository.DeleteAsync(entity);
        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template item deleted: {ItemId}", itemId);
        return true;
    }

    public async Task<bool> ReorderItemsAsync(Guid sectionId, IEnumerable<Guid> orderedItemIds, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var section = await _sectionRepository.GetQueryable()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Template section with ID '{sectionId}' not found.");
        var template = await EnsureStructureEditableAsync(section.AppraisalTemplateId, cancellationToken);

        var items = await _itemRepository.GetQueryable()
            .Where(i => i.TenantId == tenantId && i.AppraisalTemplateSectionId == sectionId)
            .ToListAsync(cancellationToken);

        var order = orderedItemIds.ToList();
        foreach (var item in items)
        {
            var idx = order.IndexOf(item.Id);
            if (idx >= 0)
                item.DisplayOrder = idx + 1;
        }

        foreach (var item in items)
            await _itemRepository.UpdateAsync(item);

        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Section {SectionId} items reordered", sectionId);
        return true;
    }

    // ─── Item Grade-Range Operations ─────────────────────────────────────

    public async Task<IEnumerable<TemplateItemGradeRangeDto>> GetItemGradeRangesAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var ranges = await _gradeRangeRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AppraisalTemplateItemId == itemId)
            .Include(r => r.GradeDefinition)
            .OrderBy(r => r.LowScore)
            .ToListAsync(cancellationToken);

        return ranges.Select(r => r.ToDto()).ToList();
    }

    public async Task<IEnumerable<TemplateItemGradeRangeDto>> UpdateItemGradeRangesAsync(Guid itemId, UpsertTemplateItemGradeRangesDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var item = await _itemRepository.GetQueryable()
            .Include(i => i.Section)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId, cancellationToken);

        if (item == null)
            throw new ArgumentException($"Template item with ID '{itemId}' not found.");

        var template = await EnsureStructureEditableAsync(item.Section.AppraisalTemplateId, cancellationToken);

        // ── Validate input ──────────────────────────────────────────────────
        if (dto.Ranges == null || dto.Ranges.Count == 0)
            throw new InvalidOperationException("At least one grade range must be provided.");

        var errors = new List<string>();

        // Check LowScore ≤ HighScore and 0–100 range
        foreach (var r in dto.Ranges)
        {
            if (r.LowScore < 0 || r.HighScore > 100)
                errors.Add($"Grade range scores must be between 0 and 100 (received Low={r.LowScore}, High={r.HighScore}).");
            else if (r.LowScore > r.HighScore)
                errors.Add($"LowScore ({r.LowScore}) cannot exceed HighScore ({r.HighScore}).");
        }

        // Check duplicate GradeDefinitionIds
        var duplicateGrades = dto.Ranges.GroupBy(r => r.GradeDefinitionId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateGrades.Count > 0)
            errors.Add("Each grade definition may only appear once per item.");

        // Check overlapping score ranges
        var sorted = dto.Ranges.OrderBy(r => r.LowScore).ToList();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i].HighScore >= sorted[i + 1].LowScore)
                errors.Add($"Score ranges overlap: [{sorted[i].LowScore}–{sorted[i].HighScore}] and [{sorted[i + 1].LowScore}–{sorted[i + 1].HighScore}].");
        }

        // ── Replace all existing ranges ────────────────────────────────────
        var existing = await _gradeRangeRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AppraisalTemplateItemId == itemId)
            .ToListAsync(cancellationToken);

        // Each grade named is this tenant's and not deleted, and one newly added to the item is active — the picker
        // offers only those (performance closure E-g1, D-79). An unknown id was a 500 from the foreign key; another
        // tenant's or a deleted one was stored, and generation dropped the band.
        var named = dto.Ranges.Select(r => r.GradeDefinitionId).Distinct().ToList();
        var grades = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId && named.Contains(g.Id))
            .Select(g => new { g.Id, g.GradeName, g.IsActive })
            .ToListAsync(cancellationToken);
        if (grades.Count < named.Count)
            errors.Add("A grade named was not found.");
        var onItem = existing.Select(r => r.GradeDefinitionId).ToHashSet();
        foreach (var grade in grades.Where(g => !g.IsActive && !onItem.Contains(g.Id)))
            errors.Add($"The grade \"{grade.GradeName}\" is inactive; a band cannot be added on it.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Grade range validation failed:\n" + string.Join("\n", errors.Select(e => $"  • {e}")));

        foreach (var old in existing)
            await _gradeRangeRepository.DeleteAsync(old);

        var newRanges = dto.Ranges.Select(r => new TemplateItemGradeRange
        {
            AppraisalTemplateItemId = itemId,
            GradeDefinitionId = r.GradeDefinitionId,
            LowScore = r.LowScore,
            HighScore = r.HighScore,
            TenantId = tenantId
        }).ToList();

        foreach (var newRange in newRanges)
            await _gradeRangeRepository.AddAsync(newRange);

        ReturnToDraftIfApproved(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grade ranges updated for template item {ItemId}: {Count} ranges", itemId, newRanges.Count);

        // Return the newly saved ranges with grade names
        return await GetItemGradeRangesAsync(itemId, cancellationToken);
    }

    public async Task<IEnumerable<AppraisalGradeDefinitionDto>> GetActiveGradeDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var defs = await _gradeDefinitionRepository.GetQueryable()
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .OrderBy(d => d.GradeName)
            .ToListAsync(cancellationToken);

        return defs.Select(d => d.ToDto()).ToList();
    }

    // ─── Private Validation Helpers ───────────────────────────────────────

    // ─── The lock (performance closure E-e, D-66) ─────────────────────────
    // An appraisal's form reads its template live — the sections, the rows, the questions, whether a row is rated or
    // measured, which competencies a self-evaluation must score — and its snapshot freezes only the weights, KPI
    // targets and bands. So a template's structure does not change while appraisals are scored on it, nor while an
    // open cycle has it (whoever it generates next is scored on what the others were). The lock covered the open
    // cycle alone — not a Draft cycle with appraisals, nor a Closed one.

    /// <summary>
    /// Why each template is locked; a template with no entry is not. Appraisals scored on it (withdrawn ones too —
    /// they stay on the record, on their form), or an active link to an open cycle.
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetLockReasonsAsync(IReadOnlyCollection<Guid> templateIds, CancellationToken cancellationToken)
    {
        var reasons = new Dictionary<Guid, string>();
        if (templateIds.Count == 0)
            return reasons;

        var tenantId = GetTenantId();
        var scored = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.AppraisalTemplateId != null && templateIds.Contains(a.AppraisalTemplateId.Value))
            .GroupBy(a => a.AppraisalTemplateId!.Value)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var openCycles = await _cycleTemplateRepository.GetQueryable()
            .Where(l => l.TenantId == tenantId && l.IsActive && templateIds.Contains(l.AppraisalTemplateId)
                     && l.AppraisalCycle.Status == AppraisalCycleStatus.Open)
            .Select(l => new { l.AppraisalTemplateId, l.AppraisalCycle.CycleName })
            .ToListAsync(cancellationToken);

        foreach (var id in templateIds)
        {
            var parts = new List<string>();
            var count = scored.FirstOrDefault(s => s.TemplateId == id)?.Count ?? 0;
            if (count > 0)
                parts.Add(count == 1 ? "an appraisal is scored on it" : $"{count} appraisals are scored on it");
            var cycles = openCycles.Where(l => l.AppraisalTemplateId == id).Select(l => l.CycleName)
                .Distinct().OrderBy(n => n).ToList();
            if (cycles.Count > 0)
                parts.Add($"it is assigned to the open cycle{(cycles.Count == 1 ? "" : "s")} " +
                          string.Join(", ", cycles.Select(n => $"'{n}'")));
            if (parts.Count > 0)
                reasons[id] = string.Join(", and ", parts);
        }
        return reasons;
    }

    /// <summary>Sets <c>IsLocked</c> and <c>LockReason</c> on each template read (P-7: the editor reads them).</summary>
    private async Task<List<AppraisalTemplateDto>> WithLocksAsync(List<AppraisalTemplateDto> templates, CancellationToken cancellationToken)
    {
        var reasons = await GetLockReasonsAsync(templates.Select(t => t.Id).ToList(), cancellationToken);
        foreach (var template in templates)
        {
            template.IsLocked = reasons.TryGetValue(template.Id, out var reason);
            template.LockReason = reason;
        }
        return templates;
    }

    /// <summary>
    /// Refuses a change to the template's structure — its sections, items, bands, their order, its scope, its
    /// delete — while it awaits approval or is locked. Returns the template, so the caller can send an approved one
    /// back to Draft once the change is made (<see cref="ReturnToDraftIfApproved"/>).
    /// </summary>
    private async Task<AppraisalTemplate> EnsureStructureEditableAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var template = await GetOwnedAsync(templateId);
        EnsureNotAwaitingApproval(template);

        var reasons = await GetLockReasonsAsync(new[] { templateId }, cancellationToken);
        if (reasons.TryGetValue(templateId, out var reason))
            throw new AppraisalConfigurationLockedException(
                $"This template is locked: {reason}. Its structure cannot change underneath them — copy it and edit the copy.");
        return template;
    }

    /// <summary>The grades among <paramref name="gradeIds"/> that are this tenant's and not deleted.</summary>
    private async Task<HashSet<Guid>> LiveGradeIdsAsync(IEnumerable<Guid> gradeIds, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var ids = gradeIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<Guid>();
        var live = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId && ids.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);
        return live.ToHashSet();
    }

    /// <summary>
    /// The competency or KPI an item names is this tenant's and not deleted, and one newly named is active — the
    /// pickers offer only those, keeping the item's own (performance closure E-g1, D-79). An unknown id was a 500 from
    /// the foreign key; another tenant's was stored, and the form read a criterion it could not show.
    /// </summary>
    private async Task EnsureItemDefinitionsAsync(Guid? competencyId, Guid? kpiId, Guid? currentCompetencyId, Guid? currentKpiId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if (competencyId is Guid cid)
        {
            var competency = await _competencyRepository.GetQueryable()
                .Where(c => c.Id == cid && c.TenantId == tenantId)
                .Select(c => new { c.CriteriaName, c.IsActive })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The competency named was not found.");
            if (!competency.IsActive && cid != currentCompetencyId)
                throw new InvalidOperationException($"The competency \"{competency.CriteriaName}\" is inactive; choose an active one.");
        }
        if (kpiId is Guid kid)
        {
            var kpi = await _kpiRepository.GetQueryable()
                .Where(k => k.Id == kid && k.TenantId == tenantId)
                .Select(k => new { k.KpiName, k.IsActive })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The KPI named was not found.");
            if (!kpi.IsActive && kid != currentKpiId)
                throw new InvalidOperationException($"The KPI \"{kpi.KpiName}\" is inactive; choose an active one.");
        }
    }

    /// <summary>Nothing changes on a template awaiting a decision: what the approver sees is what is decided.</summary>
    private static void EnsureNotAwaitingApproval(AppraisalTemplate template)
    {
        if (template.ApprovalStatus == TemplateApprovalStatus.PendingApproval)
            throw new AppraisalConfigurationLockedException(
                "This template is awaiting approval: recall it before changing it.");
    }

    /// <summary>
    /// An approved template whose structure changed goes back to Draft: what was approved is no longer what it is, so
    /// it is approved again before a cycle takes it (generation refuses an unapproved one). The adapter's recall
    /// outcome clears the submission and approval stamps, as a recall does.
    /// </summary>
    private void ReturnToDraftIfApproved(AppraisalTemplate template)
    {
        if (template.ApprovalStatus != TemplateApprovalStatus.Approved)
            return;

        _workflowStatusAdapterRegistry.GetAdapter(EntityType).ApplyRecallOutcome(template, _currentUserProvider.UserId);
        _logger.LogInformation("Appraisal template {Id} returned to Draft: its structure changed after approval", template.Id);
    }

    /// <summary>
    /// Refuses a second goals section on a template (lane L): the goal rows go to the first by
    /// display order, and the other's weight would score nothing.
    /// </summary>
    private async Task EnsureNoOtherGoalsSectionAsync(Guid templateId, Guid? exceptSectionId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var another = await _sectionRepository.GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && s.AppraisalTemplateId == templateId
                        && s.Kind == AppraisalSectionKind.EmployeeGoals
                        && (exceptSectionId == null || s.Id != exceptSectionId), cancellationToken);
        if (another)
            throw new InvalidOperationException("This template already has a goals section; a template has one.");
    }

    /// <summary>
    /// Validates that section weights sum to 100 and that item weights within each section sum to 100.
    /// Called before activating a template to prevent broken scoring calculations. A goals section
    /// has no items — its rows are each employee's goals — and counts complete as it is (lane L).
    /// </summary>
    private async Task ValidateTemplateWeightsAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var template = await _templateRepository.GetQueryable()
            .Where(t => t.Id == templateId && t.TenantId == tenantId)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(i => i.GradeRanges)
            // The names the refusal quotes; without them a criterion or KPI was named by its id.
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(i => i.Competency)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(i => i.KpiDefinition)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template == null) return;

        // A band on a deleted grade is no band (performance closure E-g1, D-79): generation and the forms drop it, so an
        // item whose only bands are on deleted grades is scored on nothing.
        var liveGrades = await LiveGradeIdsAsync(
            template.Sections.SelectMany(s => s.TemplateItems).SelectMany(i => i.GradeRanges).Select(r => r.GradeDefinitionId),
            cancellationToken);

        var errors = new List<string>();

        if (!template.Sections.Any())
        {
            errors.Add("Template must have at least one section before it can be activated.");
        }
        else
        {
            var sectionWeightSum = template.Sections.Sum(s => s.Weight);
            if (sectionWeightSum != 100)
                errors.Add($"Section weights must sum to 100 (current total: {sectionWeightSum}).");

            var goalsSections = template.Sections.Where(s => s.Kind == AppraisalSectionKind.EmployeeGoals).ToList();
            if (goalsSections.Count > 1)
                errors.Add($"A template has one goals section (this one has {goalsSections.Count}).");
            foreach (var section in goalsSections.Where(s => s.TemplateItems.Any()))
                errors.Add($"Goals section '{section.SectionName}' has items; its rows are each employee's goals, so it takes none.");

            foreach (var section in template.Sections.Where(s => s.TemplateItems.Any() && s.Kind != AppraisalSectionKind.EmployeeGoals))
            {
                var itemSum = section.TemplateItems.Sum(i => i.Weight);
                if (itemSum != 100)
                    errors.Add($"Item weights in section '{section.SectionName}' must sum to 100 (current total: {itemSum}).");

                foreach (var item in section.TemplateItems)
                {
                    // A free-text question that carries no weight is never scored — the criterion
                    // snapshot skips it — so it needs no grade bands (P-8). It used to block
                    // activation and the submission for approval.
                    if (item.CompetencyId == null && item.KpiDefinitionId == null && item.Weight == 0)
                        continue;

                    if (!item.GradeRanges.Any(r => liveGrades.Contains(r.GradeDefinitionId)))
                    {
                        var label = item.Competency?.CriteriaName
                                 ?? item.KpiDefinition?.KpiName
                                 ?? item.CustomQuestion
                                 ?? item.Id.ToString();
                        errors.Add($"Item '{label}' in section '{section.SectionName}' has no grade ranges configured.");
                    }
                }
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Template activation blocked — validation failed:\n" +
                string.Join("\n", errors.Select(e => $"  • {e}")));
    }
}
