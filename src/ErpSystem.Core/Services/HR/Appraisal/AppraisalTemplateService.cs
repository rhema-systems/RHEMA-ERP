using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
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

        return entity.ToDto();
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

        return entities.ToDtoList();
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
            HasCycleAssignments   = t.CycleAssignments.Any()
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
            Items = items.ToDtoList(),
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

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalTemplateDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.OrganizationUnitId == orgUnitId)
            .Include(t => t.OrganizationUnit)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<AppraisalTemplateDto>> GetByOrganizationLevelIdAsync(Guid orgLevelId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _templateRepository.GetQueryable()
            .Where(t => t.TenantId == tenantId && t.OrganizationLevelId == orgLevelId)
            .Include(t => t.OrganizationLevel)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
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

        return entities.ToDtoList();
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

        await AssertTemplateNotInActiveCycleAsync(updateDto.Id, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await AssertTemplateNotInActiveCycleAsync(id, cancellationToken);

        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal template deleted: {Id}", id);
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
        if (string.IsNullOrWhiteSpace(dto.NewTemplateName))
            throw new ArgumentException("A name is required for the copied template.");

        var tenantId = GetTenantId();
        var source = await _templateRepository.GetQueryable()
            .Where(t => t.Id == sourceTemplateId && t.TenantId == tenantId)
            .Include(t => t.Sections.OrderBy(s => s.DisplayOrder))
                .ThenInclude(s => s.TemplateItems.OrderBy(i => i.DisplayOrder))
                    .ThenInclude(i => i.GradeRanges)
            .FirstOrDefaultAsync(t => t.Id == sourceTemplateId, cancellationToken);

        if (source == null)
            throw new ArgumentException($"Source template with ID '{sourceTemplateId}' not found.");

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

                foreach (var srcRange in srcItem.GradeRanges)
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
        await GetOwnedAsync(templateId);

        await AssertTemplateNotInActiveCycleAsync(templateId, cancellationToken);

        var tenantId = GetTenantId();
        var entity = dto.ToEntity();
        entity.AppraisalTemplateId = templateId;
        entity.TenantId = tenantId;

        // Auto-assign display order if not provided
        if (entity.DisplayOrder == 0)
        {
            var maxOrder = await _sectionRepository.GetQueryable()
                .Where(s => s.TenantId == tenantId && s.AppraisalTemplateId == templateId)
                .MaxAsync(s => (int?)s.DisplayOrder, cancellationToken) ?? 0;
            entity.DisplayOrder = maxOrder + 1;
        }

        await _sectionRepository.AddAsync(entity);
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

        await AssertTemplateNotInActiveCycleAsync(templateId, cancellationToken);

        dto.UpdateEntity(entity);
        await _sectionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template section updated: {SectionId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSectionAsync(Guid templateId, Guid sectionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSectionAsync(templateId, sectionId);

        await AssertTemplateNotInActiveCycleAsync(templateId, cancellationToken);

        await _sectionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template section deleted: {SectionId}", sectionId);
        return true;
    }

    public async Task<bool> ReorderSectionsAsync(Guid templateId, IEnumerable<Guid> orderedSectionIds, CancellationToken cancellationToken = default)
    {
        await GetOwnedAsync(templateId);
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

        await AssertTemplateNotInActiveCycleAsync(section.AppraisalTemplateId, cancellationToken);

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

        await AssertTemplateNotInActiveCycleAsync(entity.Section.AppraisalTemplateId, cancellationToken);

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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template item updated: {ItemId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid sectionId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItemAsync(sectionId, itemId);

        await AssertTemplateNotInActiveCycleAsync(entity.Section.AppraisalTemplateId, cancellationToken);

        await _itemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template item deleted: {ItemId}", itemId);
        return true;
    }

    public async Task<bool> ReorderItemsAsync(Guid sectionId, IEnumerable<Guid> orderedItemIds, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
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

        await AssertTemplateNotInActiveCycleAsync(item.Section.AppraisalTemplateId, cancellationToken);

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

        if (errors.Count > 0)
            throw new InvalidOperationException("Grade range validation failed:\n" + string.Join("\n", errors.Select(e => $"  • {e}")));

        // ── Replace all existing ranges ────────────────────────────────────
        var existing = await _gradeRangeRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AppraisalTemplateItemId == itemId)
            .ToListAsync(cancellationToken);

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

    /// <summary>
    /// Throws if the template is assigned to any Open or InProgress appraisal cycle.
    /// All structural edits must be made on a clone while the original is in use.
    /// </summary>
    private async Task AssertTemplateNotInActiveCycleAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var template = await _templateRepository.GetQueryable()
            .Where(t => t.Id == templateId && t.TenantId == tenantId)
            .Include(t => t.CycleAssignments)
                .ThenInclude(ct => ct.AppraisalCycle)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template == null) return;

        var inActiveCycle = template.CycleAssignments
            .Any(ct => ct.AppraisalCycle.Status == AppraisalCycleStatus.Open
                    || ct.AppraisalCycle.Status == AppraisalCycleStatus.InProgress);

        if (inActiveCycle)
            throw new InvalidOperationException(
                "This template cannot be modified because it is assigned to an Open or InProgress appraisal cycle. " +
                "Use CloneAsync to create a new version for modifications.");
    }

    /// <summary>
    /// Validates that section weights sum to 100 and that item weights within each section sum to 100.
    /// Called before activating a template to prevent broken scoring calculations.
    /// </summary>
    private async Task ValidateTemplateWeightsAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var template = await _templateRepository.GetQueryable()
            .Where(t => t.Id == templateId && t.TenantId == tenantId)
            .Include(t => t.Sections)
                .ThenInclude(s => s.TemplateItems)
                    .ThenInclude(i => i.GradeRanges)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template == null) return;

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

            foreach (var section in template.Sections.Where(s => s.TemplateItems.Any()))
            {
                var itemSum = section.TemplateItems.Sum(i => i.Weight);
                if (itemSum != 100)
                    errors.Add($"Item weights in section '{section.SectionName}' must sum to 100 (current total: {itemSum}).");

                foreach (var item in section.TemplateItems)
                {
                    if (!item.GradeRanges.Any())
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
