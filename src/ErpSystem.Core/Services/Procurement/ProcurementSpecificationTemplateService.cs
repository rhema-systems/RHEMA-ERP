using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementSpecificationTemplateService : IProcurementSpecificationTemplateService
{
    private const string SourceType = "ProcurementSpecificationTemplate";
    private const string EventType = "SpecificationTemplateLifecycle";
    private const string ManagePermission = "procurement.plan.manage";
    private const string ApprovePermission = "procurement.plan.approve";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly ILogger<ProcurementSpecificationTemplateService> _logger;

    public ProcurementSpecificationTemplateService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        ILogger<ProcurementSpecificationTemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSpecificationTemplate> Templates => _unitOfWork.Repository<ProcurementSpecificationTemplate>();
    private IGenericRepository<ProcurementControlEvent> Events => _unitOfWork.Repository<ProcurementControlEvent>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions => _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstanceRows => _unitOfWork.Repository<WorkflowInstance>();

    public async Task<ProcurementSpecificationTemplateSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var now = DateTime.UtcNow;
        var query = TenantQuery();
        return new ProcurementSpecificationTemplateSummaryDto
        {
            TemplateFamilyCount = await query.Select(item => item.TemplateKey).Distinct().CountAsync(cancellationToken),
            DraftCount = await query.CountAsync(item => item.Status == ProcurementSpecificationTemplateStatus.Draft, cancellationToken),
            PendingApprovalCount = await query.CountAsync(item => item.Status == ProcurementSpecificationTemplateStatus.PendingApproval, cancellationToken),
            PublishedCount = await query.CountAsync(item => item.Status == ProcurementSpecificationTemplateStatus.Published, cancellationToken),
            EffectiveCount = await query.CountAsync(item => item.Status == ProcurementSpecificationTemplateStatus.Published &&
                item.EffectiveFromUtc <= now && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now), cancellationToken),
            ByKind = await query.GroupBy(item => item.Kind)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key.ToString(), item => item.Count, cancellationToken)
        };
    }

    public async Task<ProcurementSpecificationTemplatePageDto> SearchAsync(
        ProcurementSpecificationTemplateSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = TenantQuery().Include(item => item.WorkflowDefinition).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.TemplateCode.Contains(search) || item.Name.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        }
        if (request.Kind.HasValue) query = query.Where(item => item.Kind == request.Kind.Value);
        if (request.Status.HasValue) query = query.Where(item => item.Status == request.Status.Value);
        if (request.EffectiveOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(item => item.Status == ProcurementSpecificationTemplateStatus.Published &&
                item.EffectiveFromUtc <= now && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now));
        }
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.TemplateCode).ThenByDescending(item => item.Version)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new ProcurementSpecificationTemplatePageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = rows.Select(MapList).ToList()
        };
    }

    public async Task<ProcurementSpecificationTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var entity = await LoadAsync(id, false, cancellationToken);
        return Map(entity, await LoadTimelineAsync(entity.Id, cancellationToken));
    }

    public async Task<ProcurementSpecificationTemplateDto?> GetEffectiveAsync(
        string templateCode,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var code = NormalizeCode(templateCode);
        var moment = EnsureUtc(atUtc);
        var entity = await TenantQuery().Include(item => item.WorkflowDefinition)
            .AsNoTracking().Where(item => item.TemplateCode == code &&
                item.Status == ProcurementSpecificationTemplateStatus.Published && item.EffectiveFromUtc <= moment &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= moment))
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity, await LoadTimelineAsync(entity.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<ProcurementSpecificationWorkflowOptionDto>> GetWorkflowOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return await WorkflowDefinitions.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.IsActive && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .Include(item => item.EntityType).AsNoTracking().OrderBy(item => item.Name).ThenByDescending(item => item.Version)
            .Select(item => new ProcurementSpecificationWorkflowOptionDto
            {
                Id = item.Id,
                Name = item.Name,
                Version = item.Version,
                EntityTypeName = item.EntityType.Name
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementSpecificationTemplateDto> CreateAsync(
        SaveProcurementSpecificationTemplateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        await EnsureCapabilityAsync(ManagePermission, request.TemplateCode, correlationId, cancellationToken);
        ValidateDraftRequest(request);
        await ValidateWorkflowDefinitionAsync(request.WorkflowDefinitionId, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProcurementSpecificationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            TemplateKey = Guid.NewGuid(),
            Version = 1,
            Status = ProcurementSpecificationTemplateStatus.Draft,
            RevisionNumber = 1,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        ApplyRequest(entity, request);

        return await ExecuteAsync(async () =>
        {
            await EnsureVersionUniqueAsync(entity, cancellationToken);
            await Templates.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Created", ProcurementControlEventResult.Succeeded, null, Snapshot(entity),
                request.ChangeSummary, [], correlationId, now, cancellationToken);
            return entity.Id;
        }, cancellationToken);
    }

    public async Task<ProcurementSpecificationTemplateDto> UpdateAsync(
        Guid id,
        SaveProcurementSpecificationTemplateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        ValidateDraftRequest(request);
        await ValidateWorkflowDefinitionAsync(request.WorkflowDefinitionId, cancellationToken);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.TemplateCode, correlationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSpecificationTemplateStatus.Draft, "Only a Draft specification template can be edited.");
        var before = Snapshot(entity);
        ApplyRequest(entity, request);
        Touch(entity);

        return await ExecuteAsync(async () =>
        {
            await EnsureVersionUniqueAsync(entity, cancellationToken);
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Updated", ProcurementControlEventResult.Succeeded, before, Snapshot(entity),
                request.ChangeSummary, [], correlationId, DateTime.UtcNow, cancellationToken);
            return entity.Id;
        }, cancellationToken);
    }

    public async Task<ProcurementSpecificationTemplateValidationDto> ValidateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var entity = await LoadAsync(id, false, cancellationToken);
        var issues = ValidateForPublication(entity);
        if (entity.WorkflowDefinitionId.HasValue)
        {
            try { await ValidateWorkflowDefinitionAsync(entity.WorkflowDefinitionId, cancellationToken); }
            catch (Exception exception) when (exception is ProcurementSpecificationTemplateNotFoundException or ProcurementSpecificationTemplateConflictException)
            {
                issues.Add(Issue("WORKFLOW_INVALID", nameof(entity.WorkflowDefinitionId), exception.Message));
            }
        }
        return new ProcurementSpecificationTemplateValidationDto { TemplateId = entity.Id, IsValid = issues.Count == 0, Issues = issues };
    }

    public async Task<ProcurementSpecificationTemplateDto> SubmitAsync(
        Guid id,
        ProcurementSpecificationTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.TemplateCode, correlationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSpecificationTemplateStatus.Draft, "Only a Draft specification template can be submitted.");
        EnsurePublicationReady(entity);
        await ValidateWorkflowDefinitionAsync(entity.WorkflowDefinitionId, cancellationToken);
        EnsureEvidence(request.Evidence);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSpecificationTemplateStatus.PendingApproval;
        entity.SubmittedAtUtc = now;
        entity.SubmittedById = _currentUser.UserId;
        entity.SubmittedByName = ActorName;
        entity.ReviewComment = TrimOrNull(request.Comment, 1000);
        Touch(entity, now);

        return await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (entity.WorkflowDefinitionId.HasValue)
            {
                var definition = await WorkflowDefinitions.GetQueryable(item => item.Id == entity.WorkflowDefinitionId.Value &&
                        item.TenantId == _currentUser.TenantId && !item.IsDeleted).AsNoTracking().SingleAsync(cancellationToken);
                var instance = await _workflowInstances.StartWorkflowAsync(definition.Id, definition.EntityTypeId,
                    entity.Id.ToString(), _currentUser.UserId,
                    new { entity.TemplateCode, entity.Name, entity.Kind, entity.Version, entity.EffectiveFromUtc }, cancellationToken);
                entity.WorkflowInstanceId = instance.Id;
                await Templates.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await RecordEventAsync(entity, "Submitted", ProcurementControlEventResult.ReviewRequired, before, Snapshot(entity),
                request.Comment, request.Evidence, correlationId, now, cancellationToken);
            return entity.Id;
        }, cancellationToken);
    }

    public async Task<ProcurementSpecificationTemplateDto> PublishAsync(
        Guid id,
        ProcurementSpecificationTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.TemplateCode, correlationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSpecificationTemplateStatus.PendingApproval, "Only a Pending approval specification template can be published.");
        EnsurePublicationReady(entity);
        await EnsureIndependentApproverAsync(entity, correlationId, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, true, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        var publishesInFuture = entity.EffectiveFromUtc > now;
        var previous = await TenantQuery().Where(item => item.TemplateKey == entity.TemplateKey && item.Id != entity.Id &&
                item.Status == ProcurementSpecificationTemplateStatus.Published).ToListAsync(cancellationToken);
        if (publishesInFuture && previous.Any(item => item.EffectiveFromUtc > now))
            throw new ProcurementSpecificationTemplateConflictException(
                "A future replacement is already Published for this specification-template family. Retire it before scheduling another replacement.");

        return await ExecuteAsync(async () =>
        {
            foreach (var prior in previous)
            {
                var priorBefore = Snapshot(prior);
                var currentlyEffective = prior.EffectiveFromUtc <= now &&
                    (!prior.EffectiveToUtc.HasValue || prior.EffectiveToUtc.Value >= now);
                if (publishesInFuture && currentlyEffective)
                {
                    if (!prior.EffectiveToUtc.HasValue || prior.EffectiveToUtc.Value >= entity.EffectiveFromUtc)
                        prior.EffectiveToUtc = entity.EffectiveFromUtc.AddTicks(-1);
                    Touch(prior, now);
                    await Templates.UpdateAsync(prior);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await RecordEventAsync(prior, "SupersessionScheduled", ProcurementControlEventResult.Succeeded,
                        priorBefore, Snapshot(prior), $"Version {entity.Version} becomes effective at {entity.EffectiveFromUtc:O}.",
                        [], correlationId, now, cancellationToken);
                    continue;
                }

                prior.Status = ProcurementSpecificationTemplateStatus.Retired;
                prior.RetiredAtUtc = now;
                prior.RetiredById = _currentUser.UserId;
                prior.RetiredByName = ActorName;
                if (!prior.EffectiveToUtc.HasValue || prior.EffectiveToUtc.Value > now) prior.EffectiveToUtc = now;
                Touch(prior, now);
                await Templates.UpdateAsync(prior);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync(prior, "RetiredOnSupersession", ProcurementControlEventResult.Succeeded,
                    priorBefore, Snapshot(prior), $"Superseded by version {entity.Version}.", [], correlationId, now, cancellationToken);
            }

            entity.Status = ProcurementSpecificationTemplateStatus.Published;
            entity.PublishedAtUtc = now;
            entity.PublishedById = _currentUser.UserId;
            entity.PublishedByName = ActorName;
            entity.ReviewComment = TrimOrNull(request.Comment, 1000);
            Touch(entity, now);
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Published", ProcurementControlEventResult.Succeeded, before, Snapshot(entity),
                request.Comment, request.Evidence, correlationId, now, cancellationToken);
            return entity.Id;
        }, cancellationToken);
    }

    public async Task<ProcurementSpecificationTemplateDto> RejectAsync(
        Guid id,
        ProcurementSpecificationTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureRequired(request.Comment, "REJECTION_COMMENT_REQUIRED", "A rejection comment is required.");
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.TemplateCode, correlationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSpecificationTemplateStatus.PendingApproval, "Only a Pending approval specification template can be rejected.");
        await EnsureIndependentApproverAsync(entity, correlationId, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, false, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSpecificationTemplateStatus.Draft;
        entity.ReviewComment = request.Comment!.Trim();
        entity.SubmittedAtUtc = null;
        entity.SubmittedById = null;
        entity.SubmittedByName = null;
        entity.WorkflowInstanceId = null;
        Touch(entity, now);

        return await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Rejected", ProcurementControlEventResult.Rejected, before, Snapshot(entity),
                request.Comment, request.Evidence, correlationId, now, cancellationToken);
            return entity.Id;
        }, cancellationToken);
    }

    public async Task<ProcurementSpecificationTemplateDto> CloneAsync(
        Guid id,
        CloneProcurementSpecificationTemplateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureRequired(request.ChangeSummary, "CHANGE_SUMMARY_REQUIRED", "A change summary is required when cloning a template.");
        ValidatePeriod(request.EffectiveFromUtc, request.EffectiveToUtc);
        var source = await LoadAsync(id, false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.TemplateCode, correlationId, cancellationToken);
        EnsureRowVersion(source.RowVersion, request.RowVersion);
        if (source.Status is not (ProcurementSpecificationTemplateStatus.Published or ProcurementSpecificationTemplateStatus.Retired))
            throw new ProcurementSpecificationTemplateConflictException("Only a Published or Retired specification template can be cloned.");

        var version = await Templates.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.TemplateKey == source.TemplateKey)
            .Select(item => (int?)item.Version).MaxAsync(cancellationToken) ?? 0;
        var now = DateTime.UtcNow;
        var entity = new ProcurementSpecificationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            TemplateKey = source.TemplateKey,
            TemplateCode = source.TemplateCode,
            Name = source.Name,
            Description = source.Description,
            Kind = source.Kind,
            Version = version + 1,
            Status = ProcurementSpecificationTemplateStatus.Draft,
            IsDefault = source.IsDefault,
            EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc),
            EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null,
            ChangeSummary = request.ChangeSummary.Trim(),
            Purpose = source.Purpose,
            FunctionalAndPerformanceRequirements = source.FunctionalAndPerformanceRequirements,
            ProcessAndMaterialsRequirements = source.ProcessAndMaterialsRequirements,
            DimensionsAndMarkingRequirements = source.DimensionsAndMarkingRequirements,
            TestingAndInspectionRequirements = source.TestingAndInspectionRequirements,
            ApplicableStandards = source.ApplicableStandards,
            Deliverables = source.Deliverables,
            AcceptanceCriteria = source.AcceptanceCriteria,
            WorkflowDefinitionId = source.WorkflowDefinitionId,
            SupersedesTemplateId = source.Id,
            RevisionNumber = 1,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        return await ExecuteAsync(async () =>
        {
            await Templates.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Cloned", ProcurementControlEventResult.Succeeded, Snapshot(source), Snapshot(entity),
                request.ChangeSummary, [], correlationId, now, cancellationToken);
            return entity.Id;
        }, cancellationToken);
    }

    public async Task<ProcurementSpecificationTemplateDto> RetireAsync(
        Guid id,
        ProcurementSpecificationTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureRequired(request.Comment, "RETIREMENT_COMMENT_REQUIRED", "A retirement comment is required.");
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.TemplateCode, correlationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSpecificationTemplateStatus.Published, "Only a Published specification template can be retired.");
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSpecificationTemplateStatus.Retired;
        entity.RetiredAtUtc = now;
        entity.RetiredById = _currentUser.UserId;
        entity.RetiredByName = ActorName;
        entity.ReviewComment = request.Comment!.Trim();
        if (entity.EffectiveFromUtc <= now && (!entity.EffectiveToUtc.HasValue || entity.EffectiveToUtc.Value > now))
            entity.EffectiveToUtc = now;
        Touch(entity, now);

        return await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Retired", ProcurementControlEventResult.Succeeded, before, Snapshot(entity),
                request.Comment, request.Evidence, correlationId, now, cancellationToken);
            return entity.Id;
        }, cancellationToken);
    }

    public async Task DeleteDraftAsync(
        Guid id,
        ProcurementSpecificationTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.TemplateCode, correlationId, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSpecificationTemplateStatus.Draft, "Only a Draft specification template can be deleted.");
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.DeletedBy = _currentUser.Username;
        Touch(entity, now);
        await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "DraftDeleted", ProcurementControlEventResult.Succeeded, before, Snapshot(entity),
                request.Comment, request.Evidence, correlationId, now, cancellationToken);
            return entity.Id;
        }, cancellationToken, loadResult: false);
    }

    private async Task<ProcurementSpecificationTemplateDto> ExecuteAsync(
        Func<Task<Guid>> action,
        CancellationToken cancellationToken,
        bool loadResult = true)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            Guid id;
            try
            {
                id = await action();
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementSpecificationTemplateConflictException(
                    "The specification template changed by another user. Reload before continuing.");
            }
            catch (ProcurementControlEventNotFoundException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementSpecificationTemplateNotFoundException(exception.Message);
            }
            catch (ProcurementControlEventValidationException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementSpecificationTemplateValidationException(exception.Code, exception.Message);
            }
            catch (ProcurementControlEventConflictException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw new ProcurementSpecificationTemplateConflictException(exception.Message);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }

            _logger.LogInformation("Recorded specification-template mutation {TemplateId} for tenant {TenantId}", id, _currentUser.TenantId);
            if (!loadResult) return new ProcurementSpecificationTemplateDto { Id = id };
            return await GetMappedAsync(id, cancellationToken);
        }, cancellationToken);
    }

    private async Task RecordEventAsync(
        ProcurementSpecificationTemplate entity,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var normalizedCorrelationId = NormalizeCorrelation(correlationId);
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("specification-template", entity.TenantId, entity.Id,
                $"{entity.RevisionNumber:D4}-{action}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = "PLN-003",
            SourceType = SourceType,
            SourceId = entity.Id,
            SourceReference = $"{entity.TemplateCode}/v{entity.Version}",
            Reason = TrimOrNull(reason, 1000),
            Before = before,
            After = after,
            ResultValues = new { entity.Status, entity.Kind, entity.Version, entity.EffectiveFromUtc, entity.EffectiveToUtc },
            CorrelationId = normalizedCorrelationId,
            CausationId = normalizedCorrelationId,
            OccurredAtUtc = occurredAtUtc,
            Evidence = evidence.Select(item => new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = item.ReferenceKind,
                ReferenceId = item.ReferenceId,
                Reference = item.Reference,
                Label = item.Label,
                RequirementKey = item.RequirementKey
            }).ToList()
        }, cancellationToken);
    }

    private async Task<ProcurementSpecificationTemplateDto> GetMappedAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, false, cancellationToken);
        return Map(entity, await LoadTimelineAsync(id, cancellationToken));
    }

    private async Task<ProcurementSpecificationTemplate> LoadAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<ProcurementSpecificationTemplate> query = TenantQuery().Where(item => item.Id == id)
            .Include(item => item.WorkflowDefinition).ThenInclude(item => item!.EntityType);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementSpecificationTemplateNotFoundException(
                "The specification template was not found in the current tenant.");
    }

    private async Task<List<ProcurementSpecificationTemplateTimelineEventDto>> LoadTimelineAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var rows = await Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.EventType == EventType && item.SourceId == templateId)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord)
            .AsNoTracking().OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(item => new ProcurementSpecificationTemplateTimelineEventDto
        {
            Id = item.Id,
            Action = item.Action,
            Result = item.Result,
            ActorName = item.ActorName,
            Reason = item.Reason,
            OccurredAtUtc = item.OccurredAtUtc,
            IntegrityHash = item.IntegrityHash,
            Evidence = item.EvidenceLinks.OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference)
                .Select(MapEvidence).ToList()
        }).ToList();
    }

    private static ProcurementControlEventEvidenceDto MapEvidence(ProcurementControlEventEvidenceLink link)
    {
        var workflow = link.WorkflowEvidenceDocument;
        var upload = link.FileUploadRecord;
        return new ProcurementControlEventEvidenceDto
        {
            Id = link.Id,
            ReferenceKind = link.ReferenceKind,
            ReferenceId = link.WorkflowEvidenceDocumentId ?? link.FileUploadRecordId,
            Reference = link.Reference,
            Label = link.Label,
            RequirementKey = link.RequirementKey,
            FileName = workflow?.FileName ?? upload?.OriginalFileName,
            Sha256 = workflow?.Sha256,
            VerificationStatus = workflow?.VerificationStatus.ToString() ?? upload?.VirusScanStatus.ToString(),
            ReferenceAvailable = link.ReferenceKind == ProcurementControlEvidenceReferenceKind.ExternalReference || workflow is not null || upload is not null
        };
    }

    private IQueryable<ProcurementSpecificationTemplate> TenantQuery() =>
        Templates.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private async Task EnsureVersionUniqueAsync(ProcurementSpecificationTemplate entity, CancellationToken cancellationToken)
    {
        var exists = await Templates.GetQueryableIncludingDeleted(item =>
            item.TenantId == entity.TenantId && item.Id != entity.Id &&
            (item.TemplateKey == entity.TemplateKey && item.Version == entity.Version ||
             item.TemplateCode == entity.TemplateCode && item.Version == entity.Version)).AnyAsync(cancellationToken);
        if (exists) throw new ProcurementSpecificationTemplateConflictException(
            "Template code and family version must be unique within the tenant, including deleted Draft revisions.");
    }

    private async Task ValidateWorkflowDefinitionAsync(Guid? workflowDefinitionId, CancellationToken cancellationToken)
    {
        if (!workflowDefinitionId.HasValue) return;
        var definition = await WorkflowDefinitions.GetQueryable(item => item.Id == workflowDefinitionId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementSpecificationTemplateNotFoundException(
                "The shared workflow definition was not found in the current tenant.");
        if (!definition.IsActive || definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
            throw new ProcurementSpecificationTemplateConflictException(
                "A referenced shared workflow definition must be Active and Published.");
    }

    private async Task EnsureWorkflowOutcomeAsync(
        ProcurementSpecificationTemplate entity,
        bool approving,
        CancellationToken cancellationToken)
    {
        if (!entity.WorkflowDefinitionId.HasValue) return;
        if (!entity.WorkflowInstanceId.HasValue)
            throw new ProcurementSpecificationTemplateConflictException(
                "The configured shared workflow was not started for this specification template.");
        var instance = await WorkflowInstanceRows.GetQueryable(item => item.Id == entity.WorkflowInstanceId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementSpecificationTemplateConflictException(
                "The linked shared workflow instance was not found in the current tenant.");
        if (approving && instance.Status != WorkflowInstanceStatus.Completed)
            throw new ProcurementSpecificationTemplateConflictException(
                "Complete the configured shared workflow before publishing this specification template.");
        if (!approving && instance.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw new ProcurementSpecificationTemplateConflictException(
                "Reject or cancel the configured shared workflow before rejecting this specification template.");
    }

    private async Task EnsureIndependentApproverAsync(
        ProcurementSpecificationTemplate entity,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!entity.SubmittedById.HasValue)
            throw new ProcurementSpecificationTemplateConflictException("The template has no recorded submitter.");
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = $"{entity.TemplateCode}/v{entity.Version}",
            ProhibitedActorUserIds = [entity.SubmittedById.Value]
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSpecificationTemplateAuthorizationException(decision.Message);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = SourceType,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? "NEW" : sourceReference.Trim()
        }, correlationId, cancellationToken);
        if (!decision.Allowed) throw new ProcurementSpecificationTemplateAuthorizationException(decision.Message);
    }

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementSpecificationTemplateAuthorizationException(
            "A TDC procurement role or tenant-administration role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSpecificationTemplateAuthorizationException("An authenticated tenant context is required.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");
    private string ActorName => Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName) ? _currentUser.Username : _currentUser.FullName, 300);

    private static void ValidateDraftRequest(SaveProcurementSpecificationTemplateRequest request)
    {
        EnsureRequired(request.TemplateCode, "TEMPLATE_CODE_REQUIRED", "TemplateCode is required.");
        EnsureRequired(request.Name, "TEMPLATE_NAME_REQUIRED", "Name is required.");
        if (!Enum.IsDefined(request.Kind))
            throw new ProcurementSpecificationTemplateValidationException("TEMPLATE_KIND_INVALID", "Kind must be Goods, Works, or Services.");
        ValidatePeriod(request.EffectiveFromUtc, request.EffectiveToUtc);
    }

    private static void ValidatePeriod(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        if (effectiveFromUtc == default)
            throw new ProcurementSpecificationTemplateValidationException("EFFECTIVE_FROM_REQUIRED", "EffectiveFromUtc is required.");
        if (effectiveToUtc.HasValue && EnsureUtc(effectiveToUtc.Value) < EnsureUtc(effectiveFromUtc))
            throw new ProcurementSpecificationTemplateValidationException("EFFECTIVE_PERIOD_INVALID", "EffectiveToUtc cannot precede EffectiveFromUtc.");
    }

    private static List<ProcurementSpecificationTemplateValidationIssueDto> ValidateForPublication(
        ProcurementSpecificationTemplate entity)
    {
        var issues = new List<ProcurementSpecificationTemplateValidationIssueDto>();
        AddRequired(issues, entity.Purpose, nameof(entity.Purpose), "PURPOSE_REQUIRED", "Purpose is required.");
        AddRequired(issues, entity.FunctionalAndPerformanceRequirements, nameof(entity.FunctionalAndPerformanceRequirements),
            "FUNCTIONAL_PERFORMANCE_REQUIRED", "Functional and performance requirements are required.");
        AddRequired(issues, entity.ProcessAndMaterialsRequirements, nameof(entity.ProcessAndMaterialsRequirements),
            "PROCESS_MATERIALS_REQUIRED", "Process and materials requirements are required; record a justified not-applicable statement where appropriate.");
        AddRequired(issues, entity.DimensionsAndMarkingRequirements, nameof(entity.DimensionsAndMarkingRequirements),
            "DIMENSIONS_MARKING_REQUIRED", "Dimensions and marking requirements are required; record a justified not-applicable statement where appropriate.");
        AddRequired(issues, entity.TestingAndInspectionRequirements, nameof(entity.TestingAndInspectionRequirements),
            "TESTING_INSPECTION_REQUIRED", "Testing and inspection requirements are required.");
        AddRequired(issues, entity.ApplicableStandards, nameof(entity.ApplicableStandards),
            "STANDARDS_REQUIRED", "Applicable standards are required.");
        AddRequired(issues, entity.Deliverables, nameof(entity.Deliverables),
            "DELIVERABLES_REQUIRED", "Deliverables are required.");
        AddRequired(issues, entity.AcceptanceCriteria, nameof(entity.AcceptanceCriteria),
            "ACCEPTANCE_CRITERIA_REQUIRED", "Acceptance criteria are required.");
        if (entity.Version > 1 && string.IsNullOrWhiteSpace(entity.ChangeSummary))
            issues.Add(Issue("CHANGE_SUMMARY_REQUIRED", nameof(entity.ChangeSummary), "A change summary is required for replacement versions."));
        return issues;
    }

    private static void EnsurePublicationReady(ProcurementSpecificationTemplate entity)
    {
        var issues = ValidateForPublication(entity);
        if (issues.Count > 0)
            throw new ProcurementSpecificationTemplateValidationException(issues[0].Code, issues[0].Message);
    }

    private static void EnsureEvidence(IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0)
            throw new ProcurementSpecificationTemplateValidationException(
                "EVIDENCE_REQUIRED", "At least one shared evidence or external reference is required when submitting a template.");
    }

    private static void EnsureStatus(
        ProcurementSpecificationTemplate entity,
        ProcurementSpecificationTemplateStatus required,
        string message)
    {
        if (entity.Status != required) throw new ProcurementSpecificationTemplateConflictException(message);
    }

    private static void EnsureRowVersion(byte[] current, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw new ProcurementSpecificationTemplateValidationException("ROW_VERSION_REQUIRED", "RowVersion is required.");
        try
        {
            if (!current.SequenceEqual(Convert.FromBase64String(supplied)))
                throw new ProcurementSpecificationTemplateConflictException(
                    "The specification template changed by another user. Reload before continuing.");
        }
        catch (FormatException)
        {
            throw new ProcurementSpecificationTemplateValidationException("ROW_VERSION_INVALID", "RowVersion must be valid Base64.");
        }
    }

    private static void EnsureRequired(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ProcurementSpecificationTemplateValidationException(code, message);
    }

    private static void AddRequired(
        List<ProcurementSpecificationTemplateValidationIssueDto> issues,
        string? value,
        string field,
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value)) issues.Add(Issue(code, field, message));
    }

    private static ProcurementSpecificationTemplateValidationIssueDto Issue(string code, string field, string message) =>
        new() { Code = code, Field = field, Message = message };

    private static void ApplyRequest(
        ProcurementSpecificationTemplate entity,
        SaveProcurementSpecificationTemplateRequest request)
    {
        entity.TemplateCode = NormalizeCode(request.TemplateCode);
        entity.Name = request.Name.Trim();
        entity.Description = TrimOrNull(request.Description, 1000);
        entity.Kind = request.Kind;
        entity.IsDefault = request.IsDefault;
        entity.EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc);
        entity.EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null;
        entity.ChangeSummary = TrimOrNull(request.ChangeSummary, 1000);
        entity.Purpose = NormalizeContent(request.Purpose);
        entity.FunctionalAndPerformanceRequirements = NormalizeContent(request.FunctionalAndPerformanceRequirements);
        entity.ProcessAndMaterialsRequirements = NormalizeContent(request.ProcessAndMaterialsRequirements);
        entity.DimensionsAndMarkingRequirements = NormalizeContent(request.DimensionsAndMarkingRequirements);
        entity.TestingAndInspectionRequirements = NormalizeContent(request.TestingAndInspectionRequirements);
        entity.ApplicableStandards = NormalizeContent(request.ApplicableStandards);
        entity.Deliverables = NormalizeContent(request.Deliverables);
        entity.AcceptanceCriteria = NormalizeContent(request.AcceptanceCriteria);
        entity.WorkflowDefinitionId = request.WorkflowDefinitionId;
    }

    private void Touch(ProcurementSpecificationTemplate entity, DateTime? atUtc = null)
    {
        entity.RevisionNumber++;
        entity.UpdatedAt = atUtc ?? DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.Username;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private static object Snapshot(ProcurementSpecificationTemplate item) => new
    {
        item.Id,
        item.TemplateKey,
        item.TemplateCode,
        item.Name,
        item.Kind,
        item.Version,
        item.Status,
        item.IsDefault,
        item.EffectiveFromUtc,
        item.EffectiveToUtc,
        item.ChangeSummary,
        item.Purpose,
        item.FunctionalAndPerformanceRequirements,
        item.ProcessAndMaterialsRequirements,
        item.DimensionsAndMarkingRequirements,
        item.TestingAndInspectionRequirements,
        item.ApplicableStandards,
        item.Deliverables,
        item.AcceptanceCriteria,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.SupersedesTemplateId,
        item.SubmittedAtUtc,
        item.SubmittedById,
        item.PublishedAtUtc,
        item.PublishedById,
        item.RetiredAtUtc,
        item.RetiredById,
        item.ReviewComment,
        item.RevisionNumber,
        item.IsDeleted
    };

    private static ProcurementSpecificationTemplateListItemDto MapList(ProcurementSpecificationTemplate item)
    {
        var now = DateTime.UtcNow;
        return new ProcurementSpecificationTemplateListItemDto
        {
            Id = item.Id,
            TemplateKey = item.TemplateKey,
            TemplateCode = item.TemplateCode,
            Name = item.Name,
            Kind = item.Kind,
            Version = item.Version,
            Status = item.Status,
            IsDefault = item.IsDefault,
            EffectiveFromUtc = item.EffectiveFromUtc,
            EffectiveToUtc = item.EffectiveToUtc,
            IsEffective = item.Status == ProcurementSpecificationTemplateStatus.Published && item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now),
            IsPublicationReady = ValidateForPublication(item).Count == 0,
            WorkflowDefinitionName = item.WorkflowDefinition?.Name,
            CreatedAtUtc = item.CreatedAt,
            UpdatedAtUtc = item.UpdatedAt,
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static ProcurementSpecificationTemplateDto Map(
        ProcurementSpecificationTemplate item,
        List<ProcurementSpecificationTemplateTimelineEventDto> timeline)
    {
        var list = MapList(item);
        return new ProcurementSpecificationTemplateDto
        {
            Id = list.Id,
            TemplateKey = list.TemplateKey,
            TemplateCode = list.TemplateCode,
            Name = list.Name,
            Kind = list.Kind,
            Version = list.Version,
            Status = list.Status,
            IsDefault = list.IsDefault,
            EffectiveFromUtc = list.EffectiveFromUtc,
            EffectiveToUtc = list.EffectiveToUtc,
            IsEffective = list.IsEffective,
            IsPublicationReady = list.IsPublicationReady,
            WorkflowDefinitionName = list.WorkflowDefinitionName,
            CreatedAtUtc = list.CreatedAtUtc,
            UpdatedAtUtc = list.UpdatedAtUtc,
            RowVersion = list.RowVersion,
            Description = item.Description,
            ChangeSummary = item.ChangeSummary,
            Purpose = item.Purpose,
            FunctionalAndPerformanceRequirements = item.FunctionalAndPerformanceRequirements,
            ProcessAndMaterialsRequirements = item.ProcessAndMaterialsRequirements,
            DimensionsAndMarkingRequirements = item.DimensionsAndMarkingRequirements,
            TestingAndInspectionRequirements = item.TestingAndInspectionRequirements,
            ApplicableStandards = item.ApplicableStandards,
            Deliverables = item.Deliverables,
            AcceptanceCriteria = item.AcceptanceCriteria,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowDefinitionVersion = item.WorkflowDefinition?.Version,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SupersedesTemplateId = item.SupersedesTemplateId,
            SubmittedAtUtc = item.SubmittedAtUtc,
            SubmittedById = item.SubmittedById,
            SubmittedByName = item.SubmittedByName,
            PublishedAtUtc = item.PublishedAtUtc,
            PublishedById = item.PublishedById,
            PublishedByName = item.PublishedByName,
            RetiredAtUtc = item.RetiredAtUtc,
            RetiredById = item.RetiredById,
            RetiredByName = item.RetiredByName,
            ReviewComment = item.ReviewComment,
            RevisionNumber = item.RevisionNumber,
            ValidationIssues = ValidateForPublication(item),
            Timeline = timeline
        };
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string NormalizeContent(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string? TrimOrNull(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), length);
}
