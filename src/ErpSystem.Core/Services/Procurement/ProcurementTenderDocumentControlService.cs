using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementTenderDocumentControlService : IProcurementTenderDocumentControlService
{
    private const string TemplateSourceType = "ProcurementTenderDocumentTemplate";
    private const string RegisterSourceType = "ProcurementTenderDocumentRegister";
    private const string EventType = "ProcurementTenderDocumentControl";
    private const string ManagePermission = "procurement.tender.administer";
    private const string ApprovePermission = "procurement.tender.approve";
    private const string ObservePermission = "procurement.tender.observe";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions WorkflowConfigJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementTenderDocumentControlService> _logger;

    public ProcurementTenderDocumentControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        ISupplierValidationService supplierValidation,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementTenderDocumentControlService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _supplierValidation = supplierValidation;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementTenderDocumentTemplateVersion> Templates =>
        _unitOfWork.Repository<ProcurementTenderDocumentTemplateVersion>();
    private IGenericRepository<ProcurementTenderDocumentTemplateMethod> TemplateMethods =>
        _unitOfWork.Repository<ProcurementTenderDocumentTemplateMethod>();
    private IGenericRepository<ProcurementTenderDocumentRegister> Registers =>
        _unitOfWork.Repository<ProcurementTenderDocumentRegister>();
    private IGenericRepository<ProcurementTenderDocumentIssuance> Issuances =>
        _unitOfWork.Repository<ProcurementTenderDocumentIssuance>();
    private IGenericRepository<ProcurementTenderDocumentChange> Changes =>
        _unitOfWork.Repository<ProcurementTenderDocumentChange>();
    private IGenericRepository<ProcurementTenderDocumentChangeRecipient> ChangeRecipients =>
        _unitOfWork.Repository<ProcurementTenderDocumentChangeRecipient>();
    private IGenericRepository<ProcurementTenderDocumentAcknowledgement> Acknowledgements =>
        _unitOfWork.Repository<ProcurementTenderDocumentAcknowledgement>();
    private IGenericRepository<ProcurementPolicySet> PolicySets => _unitOfWork.Repository<ProcurementPolicySet>();
    private IGenericRepository<ProcurementConfigurationProfile> ConfigurationProfiles =>
        _unitOfWork.Repository<ProcurementConfigurationProfile>();
    private IGenericRepository<ProcurementPolicyMethodRule> MethodRules =>
        _unitOfWork.Repository<ProcurementPolicyMethodRule>();
    private IGenericRepository<ProcurementSourcingCase> SourcingCases =>
        _unitOfWork.Repository<ProcurementSourcingCase>();
    private IGenericRepository<Tender> Tenders => _unitOfWork.Repository<Tender>();
    private IGenericRepository<RequestForQuotation> Rfqs => _unitOfWork.Repository<RequestForQuotation>();
    private IGenericRepository<TenderBid> TenderBids => _unitOfWork.Repository<TenderBid>();
    private IGenericRepository<RequestForQuotationQuote> RfqQuotes =>
        _unitOfWork.Repository<RequestForQuotationQuote>();
    private IGenericRepository<BusinessPartner> BusinessPartners => _unitOfWork.Repository<BusinessPartner>();
    private IGenericRepository<BusinessPartnerUser> BusinessPartnerUsers =>
        _unitOfWork.Repository<BusinessPartnerUser>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions =>
        _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstanceRows =>
        _unitOfWork.Repository<WorkflowInstance>();
    private IGenericRepository<WorkflowStepInstance> WorkflowStepInstanceRows =>
        _unitOfWork.Repository<WorkflowStepInstance>();
    private IGenericRepository<ProcurementTenderControl> LegacyTenderControls =>
        _unitOfWork.Repository<ProcurementTenderControl>();

    public async Task<ProcurementTenderDocumentTemplateSummaryDto> GetTemplateSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var query = TemplateQuery();
        return new ProcurementTenderDocumentTemplateSummaryDto
        {
            TemplateFamilyCount = await query.Select(item => item.TemplateKey).Distinct().CountAsync(cancellationToken),
            DraftCount = await query.CountAsync(item => item.Status == ProcurementTenderDocumentTemplateStatus.Draft, cancellationToken),
            PendingApprovalCount = await query.CountAsync(item => item.Status == ProcurementTenderDocumentTemplateStatus.PendingApproval, cancellationToken),
            PublishedCount = await query.CountAsync(item => item.Status == ProcurementTenderDocumentTemplateStatus.Published, cancellationToken),
            EffectiveCount = await query.CountAsync(item =>
                item.Status == ProcurementTenderDocumentTemplateStatus.Published &&
                item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now), cancellationToken),
            RetiredCount = await query.CountAsync(item => item.Status == ProcurementTenderDocumentTemplateStatus.Retired, cancellationToken)
        };
    }

    public async Task<ProcurementTenderDocumentTemplatePageDto> SearchTemplatesAsync(
        ProcurementTenderDocumentTemplateSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = TemplateQuery().Include(item => item.ApplicableMethods).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.TemplateCode.Contains(search) || item.Name.Contains(search) ||
                item.DocumentTypeCode.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        }
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        if (request.Method.HasValue)
            query = query.Where(item => item.ApplicableMethods.Any(method =>
                !method.IsDeleted && method.Method == request.Method.Value));
        if (request.EffectiveAtUtc.HasValue)
        {
            var at = EnsureUtc(request.EffectiveAtUtc.Value);
            query = query.Where(item => item.Status == ProcurementTenderDocumentTemplateStatus.Published &&
                item.EffectiveFromUtc <= at && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= at));
        }
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.TemplateCode).ThenByDescending(item => item.Version)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new ProcurementTenderDocumentTemplatePageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = rows.Select(MapTemplateList).ToList()
        };
    }

    public async Task<ProcurementTenderDocumentTemplateDto> GetTemplateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var template = await LoadTemplateAsync(id, tracked: false, cancellationToken);
        var detail = MapTemplate(template);
        if (template.Status != ProcurementTenderDocumentTemplateStatus.PendingApproval ||
            !template.WorkflowInstanceId.HasValue)
            return detail;

        var stepIds = await WorkflowStepInstanceRows.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.WorkflowInstanceId == template.WorkflowInstanceId.Value)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var candidates = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                stepIds.Contains(item.StepInstanceId) && item.IsCurrent &&
                item.MalwareScanStatus == WorkflowMalwareScanStatus.Clean &&
                item.VerificationStatus != WorkflowEvidenceVerificationStatus.Rejected)
            .AsNoTracking().ToListAsync(cancellationToken);
        foreach (var artifact in candidates)
        {
            if ((!artifact.ExpiryDate.HasValue || artifact.ExpiryDate.Value >= DateTime.UtcNow) &&
                (artifact.VerificationStatus == WorkflowEvidenceVerificationStatus.Verified ||
                 await HasConfiguredContentReviewAsync(template, artifact, cancellationToken)))
                detail.EligibleContentEvidenceDocumentIds.Add(artifact.Id);
        }
        return detail;
    }

    public async Task<IReadOnlyList<ProcurementTenderDocumentWorkflowOptionDto>> GetTemplateWorkflowOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await WorkflowDefinitions.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .Include(item => item.EntityType)
            .Where(item => item.EntityType.Code == "PROCUREMENT_SOURCING" ||
                           item.EntityType.Name == "Procurement Sourcing" ||
                           item.EntityType.Name == "ProcurementSourcing")
            .AsNoTracking().OrderBy(item => item.Name).ThenByDescending(item => item.Version)
            .Select(item => new ProcurementTenderDocumentWorkflowOptionDto
            {
                Id = item.Id,
                Name = item.Name,
                Version = item.Version,
                LifecycleStatus = item.LifecycleStatus.ToString()
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementTenderDocumentPolicyOptionDto>> GetTemplatePolicyOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        return await PolicySets.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                item.EffectiveFrom <= now && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .Include(item => item.SourceConfigurationProfile)
            .Where(item => !item.SourceConfigurationProfile.IsDeleted &&
                item.SourceConfigurationProfile.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.SourceConfigurationProfile.EffectiveFrom <= now &&
                (!item.SourceConfigurationProfile.EffectiveTo.HasValue ||
                 item.SourceConfigurationProfile.EffectiveTo.Value >= now))
            .AsNoTracking()
            .OrderBy(item => item.Code).ThenByDescending(item => item.Version)
            .Select(item => new ProcurementTenderDocumentPolicyOptionDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Version = item.Version,
                SourceConfigurationProfileId = item.SourceConfigurationProfileId,
                SourceConfigurationProfileCode = item.SourceConfigurationProfile.ProfileCode
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> CreateTemplateAsync(
        SaveProcurementTenderDocumentTemplateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, request.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        ValidateTemplateRequest(request, requireContent: false);
        if (HasTemplateContent(request))
            throw Validation("TENDER_DOCUMENT_CONTENT_WORKFLOW_REQUIRED",
                "Create the Draft first, then attach content from its exact sourcing approval workflow.");
        var lineage = await ValidateTemplateLineageAsync(request, cancellationToken);
        await ValidateWorkflowDefinitionAsync(request.WorkflowDefinitionId, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProcurementTenderDocumentTemplateVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            TemplateKey = Guid.NewGuid(),
            Version = 1,
            Status = ProcurementTenderDocumentTemplateStatus.Draft,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        ApplyTemplateRequest(entity, request, lineage.Policy);
        AddTemplateMethods(entity, request.ApplicableMethods, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await EnsureTemplateVersionUniqueAsync(entity, cancellationToken);
            await Templates.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "Created", ProcurementControlEventResult.Succeeded,
                null, TemplateSnapshot(entity), request.ChangeSummary, ContentEvidence(entity),
                correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> UpdateTemplateAsync(
        Guid id,
        SaveProcurementTenderDocumentTemplateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        ValidateTemplateRequest(request, requireContent: false);
        var entity = await LoadTemplateAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        EnsureTemplateStatus(entity, ProcurementTenderDocumentTemplateStatus.Draft,
            "Only a Draft tender-document template can be edited.");
        if (HasTemplateContent(request))
            throw Validation("TENDER_DOCUMENT_CONTENT_WORKFLOW_REQUIRED",
                "Submit the Draft, then attach content from its exact sourcing approval workflow.");
        var lineage = await ValidateTemplateLineageAsync(request, cancellationToken);
        await ValidateWorkflowDefinitionAsync(request.WorkflowDefinitionId, cancellationToken);
        var before = TemplateSnapshot(entity);
        ApplyTemplateRequest(entity, request, lineage.Policy);
        var now = DateTime.UtcNow;
        var desiredMethods = request.ApplicableMethods.Distinct().ToHashSet();
        var existingMethods = await TemplateMethods.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.TemplateVersionId == entity.Id)
            .ToListAsync(cancellationToken);
        foreach (var method in existingMethods)
        {
            if (desiredMethods.Contains(method.Method))
            {
                if (method.IsDeleted)
                {
                    method.IsDeleted = false;
                    method.DeletedAt = null;
                    method.DeletedBy = null;
                    method.UpdatedAt = now;
                    method.UpdatedBy = _currentUser.Username;
                    method.LastModifiedById = _currentUser.UserId;
                }
                continue;
            }
            if (!method.IsDeleted)
            {
                method.IsDeleted = true;
                method.DeletedAt = now;
                method.DeletedBy = _currentUser.Username;
                method.UpdatedAt = now;
                method.UpdatedBy = _currentUser.Username;
                method.LastModifiedById = _currentUser.UserId;
            }
        }
        AddTemplateMethods(entity, desiredMethods.Except(existingMethods.Select(item => item.Method)), now);
        Touch(entity, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await EnsureTemplateVersionUniqueAsync(entity, cancellationToken);
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "Updated", ProcurementControlEventResult.Succeeded,
                before, TemplateSnapshot(entity), request.ChangeSummary, ContentEvidence(entity),
                correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> AttachTemplateContentAsync(
        Guid id,
        AttachProcurementTenderDocumentTemplateContentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadTemplateAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        EnsureTemplateStatus(entity, ProcurementTenderDocumentTemplateStatus.PendingApproval,
            "Controlled content can be attached only after the exact approval workflow starts.");
        if (!entity.WorkflowInstanceId.HasValue)
            throw Conflict("TENDER_DOCUMENT_WORKFLOW_NOT_STARTED",
                "Submit the Draft to start its exact sourcing approval workflow before attaching content.");
        if (request.ContentWorkflowEvidenceDocumentId == Guid.Empty)
            throw Validation("TENDER_DOCUMENT_CONTENT_ARTIFACT_REQUIRED",
                "Select one workflow evidence document from this template's approval workflow.");

        var artifact = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
            .GetQueryable(item => item.Id == request.ContentWorkflowEvidenceDocumentId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_WORKFLOW_EVIDENCE_NOT_FOUND",
                "The workflow evidence document was not found in the current tenant.");
        var belongsToExactWorkflow = await WorkflowStepInstanceRows.GetQueryable(item =>
                item.Id == artifact.StepInstanceId && item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.WorkflowInstanceId == entity.WorkflowInstanceId.Value)
            .AnyAsync(cancellationToken);
        if (!belongsToExactWorkflow)
            throw Conflict("TENDER_DOCUMENT_CONTENT_WORKFLOW_MISMATCH",
                "Controlled content must come from this template's exact sourcing approval workflow instance.");

        await ValidateTemplateContentArtifactAsync(
            artifact.Id, null, artifact.FilePath, artifact.Sha256,
            entity, requireApproved: true, cancellationToken);
        var before = TemplateSnapshot(entity);
        entity.ContentWorkflowEvidenceDocumentId = artifact.Id;
        entity.ContentFileUploadRecordId = null;
        entity.ContentReference = artifact.FilePath;
        entity.ContentChecksumSha256 = artifact.Sha256.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        Touch(entity, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "ContentAttached", ProcurementControlEventResult.Succeeded,
                before, TemplateSnapshot(entity),
                "Scan-clean content satisfying the configured review policy attached from the exact sourcing approval workflow.",
                ContentEvidence(entity), correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> SubmitTemplateAsync(
        Guid id,
        ProcurementTenderDocumentTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadTemplateAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        EnsureTemplateStatus(entity, ProcurementTenderDocumentTemplateStatus.Draft,
            "Only a Draft tender-document template can be submitted.");
        EnsureEventEvidence(request.Evidence);
        await ValidateTemplateForSubmissionAsync(entity, cancellationToken);
        var before = TemplateSnapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementTenderDocumentTemplateStatus.PendingApproval;
        entity.SubmittedAtUtc = now;
        entity.SubmittedById = _currentUser.UserId;
        entity.SubmittedByName = ActorName;
        entity.ReviewComment = TrimOrNull(request.Comment, 1000);
        Touch(entity, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var definition = await LoadWorkflowDefinitionAsync(entity.WorkflowDefinitionId, cancellationToken);
            var instance = await _workflowInstances.StartWorkflowAsync(
                definition.Id, definition.EntityTypeId, entity.Id.ToString(), _currentUser.UserId,
                new
                {
                    entity.TemplateKey,
                    entity.TemplateCode,
                    entity.DocumentTypeCode,
                    entity.Version,
                    entity.PolicySetId,
                    entity.EffectiveFromUtc
                }, cancellationToken);
            entity.WorkflowInstanceId = instance.Id;
            Touch(entity);
            Capture(entity);
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "Submitted", ProcurementControlEventResult.ReviewRequired,
                before, TemplateSnapshot(entity), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> PublishTemplateAsync(
        Guid id,
        ProcurementTenderDocumentTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadTemplateAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        EnsureTemplateStatus(entity, ProcurementTenderDocumentTemplateStatus.PendingApproval,
            "Only a Pending approval tender-document template can be published.");
        EnsureEventEvidence(request.Evidence);
        await ValidateTemplateForPublicationAsync(entity, cancellationToken);
        await EnsureIndependentActorAsync(entity.SubmittedById, $"{entity.TemplateCode}/v{entity.Version}",
            TemplateSourceType, correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity.WorkflowInstanceId, entity.WorkflowDefinitionId,
            entity.Id, approving: true, cancellationToken);
        var before = TemplateSnapshot(entity);
        var now = DateTime.UtcNow;
        var publishesInFuture = entity.EffectiveFromUtc > now;
        var previous = await TemplateQuery().Where(item => item.TemplateKey == entity.TemplateKey &&
                item.Id != entity.Id && item.Status == ProcurementTenderDocumentTemplateStatus.Published)
            .ToListAsync(cancellationToken);
        if (publishesInFuture && previous.Any(item => item.EffectiveFromUtc > now))
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_FUTURE_REPLACEMENT_EXISTS",
                "A future replacement is already Published for this template family.");

        return await ExecuteAsync(async () =>
        {
            foreach (var prior in previous)
            {
                var priorBefore = TemplateSnapshot(prior);
                var currentlyEffective = prior.EffectiveFromUtc <= now &&
                    (!prior.EffectiveToUtc.HasValue || prior.EffectiveToUtc.Value >= now);
                if (publishesInFuture && currentlyEffective)
                {
                    if (!prior.EffectiveToUtc.HasValue || prior.EffectiveToUtc.Value >= entity.EffectiveFromUtc)
                        prior.EffectiveToUtc = entity.EffectiveFromUtc.AddTicks(-1);
                    Touch(prior, now);
                    Capture(prior);
                    await Templates.UpdateAsync(prior);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await RecordTemplateEventAsync(prior, "SupersessionScheduled",
                        ProcurementControlEventResult.Succeeded, priorBefore, TemplateSnapshot(prior),
                        $"Version {entity.Version} becomes effective at {entity.EffectiveFromUtc:O}.",
                        request.Evidence, correlation, now, cancellationToken);
                    continue;
                }
                prior.Status = ProcurementTenderDocumentTemplateStatus.Retired;
                prior.RetiredAtUtc = now;
                prior.RetiredById = _currentUser.UserId;
                prior.RetiredByName = ActorName;
                Touch(prior, now);
                Capture(prior);
                await Templates.UpdateAsync(prior);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordTemplateEventAsync(prior, "RetiredOnSupersession",
                    ProcurementControlEventResult.Succeeded, priorBefore, TemplateSnapshot(prior),
                    $"Superseded by version {entity.Version}.", request.Evidence,
                    correlation, now, cancellationToken);
            }
            entity.Status = ProcurementTenderDocumentTemplateStatus.Published;
            entity.PublishedAtUtc = now;
            entity.PublishedById = _currentUser.UserId;
            entity.PublishedByName = ActorName;
            entity.ApprovalEvidenceReference = EvidenceLabel(request.Evidence);
            entity.ReviewComment = TrimOrNull(request.Comment, 1000);
            Touch(entity, now);
            Capture(entity);
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "Published", ProcurementControlEventResult.Succeeded,
                before, TemplateSnapshot(entity), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> RejectTemplateAsync(
        Guid id,
        ProcurementTenderDocumentTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadTemplateAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        EnsureTemplateStatus(entity, ProcurementTenderDocumentTemplateStatus.PendingApproval,
            "Only a Pending approval tender-document template can be rejected.");
        EnsureEventEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, $"{entity.TemplateCode}/v{entity.Version}",
            TemplateSourceType, correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity.WorkflowInstanceId, entity.WorkflowDefinitionId,
            entity.Id, approving: false, cancellationToken);
        var before = TemplateSnapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementTenderDocumentTemplateStatus.Draft;
        entity.WorkflowInstanceId = null;
        entity.ReviewComment = TrimOrNull(request.Comment, 1000);
        Touch(entity, now);
        Capture(entity);
        return await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "Rejected", ProcurementControlEventResult.Rejected,
                before, TemplateSnapshot(entity), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> CloneTemplateAsync(
        Guid id,
        CloneProcurementTenderDocumentTemplateRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var source = await LoadTemplateAsync(id, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(source.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        if (source.Status is ProcurementTenderDocumentTemplateStatus.Draft or ProcurementTenderDocumentTemplateStatus.PendingApproval)
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_CLONE_STATUS",
                "Only a Published or Retired tender-document template can be cloned.");
        ValidatePeriod(request.EffectiveFromUtc, request.EffectiveToUtc);
        Require(request.ChangeSummary, "TENDER_DOCUMENT_TEMPLATE_CHANGE_SUMMARY_REQUIRED",
            "A change summary is required for a replacement version.");
        var maxVersion = await Templates.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.TemplateKey == source.TemplateKey)
            .MaxAsync(item => (int?)item.Version, cancellationToken) ?? source.Version;
        var now = DateTime.UtcNow;
        var entity = new ProcurementTenderDocumentTemplateVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            TemplateKey = source.TemplateKey,
            TemplateCode = source.TemplateCode,
            Name = source.Name,
            Description = source.Description,
            DocumentTypeCode = source.DocumentTypeCode,
            Version = maxVersion + 1,
            Status = ProcurementTenderDocumentTemplateStatus.Draft,
            EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc),
            EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null,
            PolicySetId = source.PolicySetId,
            PolicySetCode = source.PolicySetCode,
            PolicySetVersion = source.PolicySetVersion,
            SourceConfigurationProfileId = source.SourceConfigurationProfileId,
            ContentReference = string.Empty,
            ContentWorkflowEvidenceDocumentId = null,
            ContentFileUploadRecordId = null,
            ContentChecksumSha256 = string.Empty,
            WorkflowDefinitionId = source.WorkflowDefinitionId,
            SupersedesVersionId = source.Id,
            ChangeSummary = request.ChangeSummary.Trim(),
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        AddTemplateMethods(entity, source.ApplicableMethods.Where(item => !item.IsDeleted).Select(item => item.Method), now);
        Capture(entity);
        return await ExecuteAsync(async () =>
        {
            await EnsureTemplateVersionUniqueAsync(entity, cancellationToken);
            await Templates.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "Cloned", ProcurementControlEventResult.Succeeded,
                TemplateSnapshot(source), TemplateSnapshot(entity), request.ChangeSummary, ContentEvidence(entity),
                correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentTemplateDto> RetireTemplateAsync(
        Guid id,
        ProcurementTenderDocumentTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadTemplateAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        EnsureTemplateStatus(entity, ProcurementTenderDocumentTemplateStatus.Published,
            "Only a Published tender-document template can be retired.");
        EnsureEventEvidence(request.Evidence);
        var before = TemplateSnapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementTenderDocumentTemplateStatus.Retired;
        entity.RetiredAtUtc = now;
        entity.RetiredById = _currentUser.UserId;
        entity.RetiredByName = ActorName;
        entity.ReviewComment = TrimOrNull(request.Comment, 1000);
        Touch(entity, now);
        Capture(entity);
        return await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "Retired", ProcurementControlEventResult.Succeeded,
                before, TemplateSnapshot(entity), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            return MapTemplate(entity);
        }, cancellationToken);
    }

    public async Task DeleteDraftTemplateAsync(
        Guid id,
        ProcurementTenderDocumentTemplateLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadTemplateAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.TemplateCode, TemplateSourceType, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "TENDER_DOCUMENT_TEMPLATE");
        EnsureTemplateStatus(entity, ProcurementTenderDocumentTemplateStatus.Draft,
            "Only a Draft tender-document template can be deleted.");
        EnsureEventEvidence(request.Evidence);
        var before = TemplateSnapshot(entity);
        var now = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.DeletedBy = _currentUser.Username;
        Touch(entity, now);
        Capture(entity);
        await ExecuteAsync(async () =>
        {
            await Templates.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordTemplateEventAsync(entity, "DraftDeleted", ProcurementControlEventResult.Succeeded,
                before, TemplateSnapshot(entity), request.Comment, request.Evidence,
                correlation, now, cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentRegisterReadinessDto> GetRegisterReadinessAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var source = await LoadSourceAsync(sourceType, sourceId, tracked: false, cancellationToken);
        await EnsureSourceReaderAsync(source, cancellationToken);
        var register = await LoadRegisterOrNullAsync(sourceType, sourceId, tracked: false, cancellationToken);
        if (register is not null)
            return MapReadiness(register, source, await GetExternalPartnerIdsAsync(cancellationToken));

        var now = DateTime.UtcNow;
        var effectiveTemplate = await TemplateQuery()
            .Include(item => item.ApplicableMethods)
            .Where(item => item.Status == ProcurementTenderDocumentTemplateStatus.Published &&
                item.PolicySetId == source.SourcingCase.PolicySetId &&
                item.SourceConfigurationProfileId == source.SourcingCase.PolicySet.SourceConfigurationProfileId &&
                item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now) &&
                item.ApplicableMethods.Any(method => !method.IsDeleted && method.Method == source.SourcingCase.SelectedMethod))
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var blocked = new List<string>();
        if (effectiveTemplate is null)
            blocked.Add("No Published tender-document template is effective for the locked policy, profile, and procurement method.");
        if (!source.SubmissionDeadlineUtc.HasValue)
            blocked.Add("The source submission deadline is required before binding its tender-document register.");
        if (source.SourcingCase.MethodRule.IsDeleted || !source.SourcingCase.MethodRule.IsEnabled ||
            !source.SourcingCase.MethodRule.IsAllowed ||
            source.SourcingCase.MethodRule.Method != source.SourcingCase.SelectedMethod)
            blocked.Add("The locked sourcing-case method rule is no longer valid.");
        return new ProcurementTenderDocumentRegisterReadinessDto
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceReference = source.Reference,
            SourcingCaseId = source.SourcingCase.Id,
            Method = source.SourcingCase.SelectedMethod,
            MethodRuleId = source.SourcingCase.MethodRuleId,
            MethodRuleCode = source.SourcingCase.MethodRuleCode,
            HasRegister = false,
            EffectiveTemplateVersionId = effectiveTemplate?.Id,
            EffectiveTemplateReference = effectiveTemplate is null
                ? null
                : $"{effectiveTemplate.TemplateCode}/v{effectiveTemplate.Version}",
            EffectiveSubmissionDeadlineUtc = source.SubmissionDeadlineUtc,
            CurrencyCode = source.CurrencyCode,
            Ready = blocked.Count == 0,
            BlockedReasons = blocked,
            AllowedActions = !_currentUser.IsExternalUser && blocked.Count == 0 ? ["Bind"] : []
        };
    }

    public async Task<ProcurementTenderDocumentRegisterDto> GetRegisterAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var source = await LoadSourceAsync(sourceType, sourceId, tracked: false, cancellationToken);
        await EnsureSourceReaderAsync(source, cancellationToken);
        var register = await LoadRegisterAsync(sourceType, sourceId, tracked: false, cancellationToken);
        return MapRegister(register, source, await GetExternalPartnerIdsAsync(cancellationToken));
    }

    public async Task<ProcurementTenderDocumentRegisterDto> BindAsync(
        BindProcurementTenderDocumentRegisterRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        ValidateSourceKey(request.SourceType, request.SourceId);
        var source = await LoadSourceAsync(request.SourceType, request.SourceId, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.Reference, RegisterSourceType, correlation, cancellationToken);
        var existingRegister = await LoadRegisterOrNullAsync(
            request.SourceType, request.SourceId, tracked: false, cancellationToken);
        if (existingRegister is not null &&
            string.Equals(existingRegister.CorrelationId, correlation, StringComparison.Ordinal))
            return MapRegister(existingRegister, source, null);
        if (existingRegister is not null)
            throw Conflict("TENDER_DOCUMENT_REGISTER_EXISTS",
                "A tender-document register is already bound to this procurement source.");
        if (!source.SubmissionDeadlineUtc.HasValue)
            throw Validation("TENDER_DOCUMENT_SOURCE_DEADLINE_REQUIRED",
                "Set the source submission deadline before binding the tender-document register.");
        var deadline = EnsureUtc(request.SubmissionDeadlineUtc);
        var validity = EnsureUtc(request.BidValidityUntilUtc);
        var opening = request.OpeningScheduledAtUtc.HasValue
            ? EnsureUtc(request.OpeningScheduledAtUtc.Value)
            : (DateTime?)null;
        if (deadline != EnsureUtc(source.SubmissionDeadlineUtc.Value))
            throw Validation("TENDER_DOCUMENT_DEADLINE_MISMATCH",
                "The register submission deadline must exactly match the source deadline.");
        if (deadline <= DateTime.UtcNow)
            throw Validation("TENDER_DOCUMENT_DEADLINE_ELAPSED",
                "The register submission deadline must be in the future.");
        if (opening.HasValue && opening.Value < deadline)
            throw Validation("TENDER_DOCUMENT_OPENING_BEFORE_DEADLINE",
                "The scheduled opening cannot precede the submission deadline.");
        if (source.OpeningScheduledAtUtc.HasValue && opening != EnsureUtc(source.OpeningScheduledAtUtc.Value))
            throw Validation("TENDER_DOCUMENT_OPENING_MISMATCH",
                "The register opening schedule must exactly match the source opening schedule.");
        if (validity <= deadline)
            throw Validation("TENDER_DOCUMENT_VALIDITY_INVALID",
                "Bid validity must extend beyond the submission deadline.");
        ValidateFee(request.FeeMode, request.FeeAmount);
        var currency = NormalizeCurrency(request.CurrencyCode);
        if (!string.Equals(currency, source.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw Validation("TENDER_DOCUMENT_CURRENCY_MISMATCH",
                "The register currency must match the locked sourcing-case currency.");
        var template = await LoadTemplateAsync(request.TemplateVersionId, tracked: false, cancellationToken);
        await EnsureTemplateCompatibleAsync(template, source, DateTime.UtcNow, cancellationToken);
        var now = DateTime.UtcNow;
        var register = new ProcurementTenderDocumentRegister
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            SourceType = request.SourceType,
            TenderId = request.SourceType == ProcurementTenderDocumentSourceType.Tender ? request.SourceId : null,
            RequestForQuotationId = request.SourceType == ProcurementTenderDocumentSourceType.RequestForQuotation ? request.SourceId : null,
            SourcingCaseId = source.SourcingCase.Id,
            MethodRuleId = source.SourcingCase.MethodRuleId,
            Method = source.SourcingCase.SelectedMethod,
            MethodRuleCode = source.SourcingCase.MethodRuleCode,
            PolicySetId = source.SourcingCase.PolicySetId,
            PolicySetCode = source.SourcingCase.PolicyCode,
            PolicySetVersion = source.SourcingCase.PolicyVersion,
            SourceConfigurationProfileId = source.SourcingCase.PolicySet.SourceConfigurationProfileId,
            InitialTemplateVersionId = template.Id,
            OriginalSubmissionDeadlineUtc = deadline,
            OpeningScheduledAtUtc = opening,
            OriginalBidValidityUntilUtc = validity,
            FeeMode = request.FeeMode,
            FeeAmount = request.FeeAmount,
            CurrencyCode = currency,
            BoundAtUtc = now,
            BoundByUserId = _currentUser.UserId,
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(register);
        return await ExecuteAsync(async () =>
        {
            await Registers.AddAsync(register);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordRegisterEventAsync(register, source, "RegisterBound",
                ProcurementControlEventResult.Succeeded, null, RegisterSnapshot(register),
                "Exact published template, sourcing lineage, deadlines, validity, and fee mode were bound.",
                [], correlation, now, cancellationToken);
            return MapRegister(
                await LoadRegisterAsync(request.SourceType, request.SourceId, tracked: false, cancellationToken),
                source, null);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentIssuanceDto> IssueAsync(
        IssueProcurementTenderDocumentControlRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        ValidateSourceKey(request.SourceType, request.SourceId);
        var source = await LoadSourceAsync(request.SourceType, request.SourceId, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.Reference, RegisterSourceType, correlation, cancellationToken);
        var register = await LoadRegisterAsync(request.SourceType, request.SourceId, tracked: true, cancellationToken);
        var replay = register.Issuances.FirstOrDefault(item =>
            !item.IsDeleted && string.Equals(item.CorrelationId, correlation, StringComparison.Ordinal));
        if (replay is not null)
            return MapIssuance(replay);
        EnsureRowVersion(register.RowVersion, request.RegisterRowVersion, "TENDER_DOCUMENT_REGISTER");
        if (DateTime.UtcNow > EffectiveSubmissionDeadline(register))
            throw Conflict("TENDER_DOCUMENT_ISSUE_WINDOW_CLOSED",
                "Tender documents cannot be issued after the effective submission deadline.");
        Require(request.RecipientName, "TENDER_DOCUMENT_RECIPIENT_REQUIRED", "Document recipient is required.");
        Require(request.ReceiptNumber, "TENDER_DOCUMENT_RECEIPT_REQUIRED", "An issue or sale receipt number is required.");
        Require(request.IssueChannel, "TENDER_DOCUMENT_CHANNEL_REQUIRED", "An issue channel is required.");
        Require(request.EvidenceReference, "TENDER_DOCUMENT_EVIDENCE_REQUIRED", "Document issue evidence is required.");
        await ValidateEvidenceReferenceAsync(
            request.EvidenceWorkflowDocumentId, request.EvidenceFileUploadRecordId, cancellationToken);
        BusinessPartner? partner = null;
        if (request.BusinessPartnerId.HasValue)
        {
            partner = await BusinessPartners.GetQueryable(item => item.Id == request.BusinessPartnerId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("TENDER_DOCUMENT_RECIPIENT_NOT_FOUND",
                    "The supplier was not found in the current tenant.");
            if (!partner.IsActive || partner.IsBlacklisted ||
                partner.PartnerType is not ("Supplier" or "Contractor" or "Both"))
                throw Validation("TENDER_DOCUMENT_RECIPIENT_INELIGIBLE",
                    "The selected business partner is not an active eligible supplier.");
            var validation = source.Tender is not null
                ? await _supplierValidation.ValidateForTenderAsync(
                    partner.Id,
                    source.Tender.RequiresPrequalification,
                    source.Tender.MinimumPerformanceRating)
                : await _supplierValidation.ValidateForRfqAsync(partner.Id);
            if (!validation.IsValid)
                throw Validation("TENDER_DOCUMENT_RECIPIENT_INELIGIBLE",
                    string.Join("; ", validation.Errors));
        }
        else if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            throw Validation("TENDER_DOCUMENT_RECIPIENT_CONTACT_REQUIRED",
                "An external recipient must have an email address.");
        }
        if (register.FeeMode == ProcurementTenderDocumentFeeMode.Paid)
        {
            if (request.AmountPaid != register.FeeAmount)
                throw Validation("TENDER_DOCUMENT_FEE_MISMATCH",
                    $"The exact document fee is {register.FeeAmount:0.00} {register.CurrencyCode}.");
            Require(request.PaymentReference, "TENDER_DOCUMENT_PAYMENT_REQUIRED",
                "Paid tender documents require a payment reference.");
        }
        else if (request.AmountPaid != 0m)
        {
            throw Validation("TENDER_DOCUMENT_FREE_AMOUNT_INVALID",
                "A Free tender document cannot record a payment amount.");
        }
        else if (!string.IsNullOrWhiteSpace(request.PaymentReference))
        {
            throw Validation("TENDER_DOCUMENT_FREE_PAYMENT_REFERENCE_INVALID",
                "A Free tender document cannot record a payment reference.");
        }
        var recipientKey = RecipientKey(request.BusinessPartnerId, request.RecipientEmail);
        var existingRecipient = register.Issuances.FirstOrDefault(item =>
            !item.IsDeleted && item.RecipientKey == recipientKey);
        if (existingRecipient is not null)
        {
            if (string.Equals(existingRecipient.ReceiptNumber, request.ReceiptNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                return MapIssuance(existingRecipient);
            throw Conflict("TENDER_DOCUMENT_ALREADY_ISSUED",
                "The effective tender document has already been issued to this recipient.");
        }
        if (register.Issuances.Any(item => !item.IsDeleted &&
            string.Equals(item.ReceiptNumber, request.ReceiptNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw Conflict("TENDER_DOCUMENT_RECEIPT_DUPLICATE",
                "This issue or sale receipt number is already registered.");
        var effectiveTemplate = EffectiveTemplate(register);
        var now = DateTime.UtcNow;
        var issuance = new ProcurementTenderDocumentIssuance
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            RegisterId = register.Id,
            TemplateVersionId = effectiveTemplate.Id,
            BusinessPartnerId = request.BusinessPartnerId,
            RecipientKey = recipientKey,
            RecipientName = request.RecipientName.Trim(),
            RecipientEmail = NormalizeEmail(request.RecipientEmail),
            RecipientPhone = TrimOrNull(request.RecipientPhone, 30),
            FeeMode = register.FeeMode,
            FeeAmount = register.FeeAmount,
            CurrencyCode = register.CurrencyCode,
            AmountPaid = request.AmountPaid,
            PaymentReference = TrimOrNull(request.PaymentReference, 200),
            ReceiptNumber = request.ReceiptNumber.Trim(),
            IssueChannel = request.IssueChannel.Trim(),
            IssuedAtUtc = now,
            IssuedByUserId = _currentUser.UserId,
            EvidenceReference = request.EvidenceReference.Trim(),
            EvidenceWorkflowDocumentId = request.EvidenceWorkflowDocumentId,
            EvidenceFileUploadRecordId = request.EvidenceFileUploadRecordId,
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            TemplateVersion = effectiveTemplate
        };
        Capture(issuance, register, effectiveTemplate);
        return await ExecuteAsync(async () =>
        {
            await Issuances.AddAsync(issuance);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordRegisterEventAsync(register, source,
                register.FeeMode == ProcurementTenderDocumentFeeMode.Paid
                    ? "TenderDocumentSold"
                    : "TenderDocumentIssued",
                ProcurementControlEventResult.Succeeded,
                new { issuance.BusinessPartnerId, issuance.RecipientKey, issuance.AmountPaid, issuance.PaymentReference },
                IssuanceSnapshot(issuance), issuance.EvidenceReference,
                Evidence(issuance.EvidenceReference, issuance.EvidenceWorkflowDocumentId,
                    issuance.EvidenceFileUploadRecordId, "Tender document issue or sale", "DEC-003"),
                correlation, now, cancellationToken);
            await PublishRecipientNotificationAsync("procurement.tender-document.issued", source, issuance.BusinessPartnerId,
                issuance.RecipientEmail, new
                {
                    source.Reference,
                    issuance.RecipientName,
                    issuance.ReceiptNumber,
                    templateReference = $"{effectiveTemplate.TemplateCode}/v{effectiveTemplate.Version}"
                }, cancellationToken);
            return MapIssuance(issuance);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentChangeDto> CreateChangeAsync(
        CreateProcurementTenderDocumentChangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        ValidateSourceKey(request.SourceType, request.SourceId);
        var source = await LoadSourceAsync(request.SourceType, request.SourceId, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.Reference, RegisterSourceType, correlation, cancellationToken);
        var register = await LoadRegisterAsync(request.SourceType, request.SourceId, tracked: true, cancellationToken);
        var replay = register.Changes.FirstOrDefault(item =>
            !item.IsDeleted && string.Equals(item.CorrelationId, correlation, StringComparison.Ordinal));
        if (replay is not null)
            return MapChange(replay);
        EnsureRowVersion(register.RowVersion, request.RegisterRowVersion, "TENDER_DOCUMENT_REGISTER");
        await EnsureChangeWindowOpenAsync(register, source, request.ChangeType, cancellationToken);
        if (register.Changes.Any(item =>
            !item.IsDeleted && item.Status == ProcurementTenderDocumentChangeStatus.PendingApproval))
            throw Conflict("TENDER_DOCUMENT_CHANGE_PENDING",
                "Complete or reject the current pending tender-document change before creating another.");
        Require(request.Reason, "TENDER_DOCUMENT_CHANGE_REASON_REQUIRED", "A change reason is required.");
        Require(request.EvidenceReference, "TENDER_DOCUMENT_CHANGE_EVIDENCE_REQUIRED",
            "Shared or external evidence is required for the change.");
        await ValidateEvidenceReferenceAsync(
            request.EvidenceWorkflowDocumentId, request.EvidenceFileUploadRecordId, cancellationToken);
        var definition = await LoadWorkflowDefinitionAsync(request.WorkflowDefinitionId, cancellationToken);
        var effectiveTemplate = EffectiveTemplate(register);
        DateTime? previousValue = null;
        DateTime? newValue = null;
        ProcurementTenderDocumentTemplateVersion? newTemplate = null;
        Guid? previousTemplateId = null;
        switch (request.ChangeType)
        {
            case ProcurementTenderDocumentChangeType.Addendum:
                if (!request.NewTemplateVersionId.HasValue)
                    throw Validation("TENDER_DOCUMENT_ADDENDUM_TEMPLATE_REQUIRED",
                        "An addendum requires a new Published template version.");
                newTemplate = await LoadTemplateAsync(request.NewTemplateVersionId.Value, tracked: false, cancellationToken);
                await EnsureTemplateCompatibleAsync(newTemplate, source, DateTime.UtcNow, cancellationToken);
                if (newTemplate.TemplateKey != effectiveTemplate.TemplateKey ||
                    newTemplate.Version <= effectiveTemplate.Version)
                    throw Validation("TENDER_DOCUMENT_ADDENDUM_VERSION_INVALID",
                        "An addendum must use a later Published version from the same template family.");
                previousTemplateId = effectiveTemplate.Id;
                break;
            case ProcurementTenderDocumentChangeType.SubmissionDeadlineExtension:
                if (!request.NewValueUtc.HasValue)
                    throw Validation("TENDER_DOCUMENT_EXTENSION_VALUE_REQUIRED",
                        "A submission-deadline extension requires NewValueUtc.");
                previousValue = EffectiveSubmissionDeadline(register);
                newValue = EnsureUtc(request.NewValueUtc.Value);
                if (newValue <= previousValue)
                    throw Validation("TENDER_DOCUMENT_DEADLINE_EXTENSION_NOT_FORWARD",
                        "A submission deadline can only be extended forward.");
                if (newValue <= DateTime.UtcNow)
                    throw Validation("TENDER_DOCUMENT_DEADLINE_EXTENSION_ELAPSED",
                        "The extended submission deadline must be in the future.");
                if (register.OpeningScheduledAtUtc.HasValue &&
                    newValue > register.OpeningScheduledAtUtc.Value)
                    throw Validation("TENDER_DOCUMENT_DEADLINE_AFTER_OPENING",
                        "The submission deadline cannot be extended beyond the immutable scheduled opening.");
                break;
            case ProcurementTenderDocumentChangeType.BidValidityExtension:
                if (!request.NewValueUtc.HasValue)
                    throw Validation("TENDER_DOCUMENT_EXTENSION_VALUE_REQUIRED",
                        "A bid-validity extension requires NewValueUtc.");
                previousValue = EffectiveBidValidity(register);
                newValue = EnsureUtc(request.NewValueUtc.Value);
                if (newValue <= previousValue)
                    throw Validation("TENDER_DOCUMENT_VALIDITY_EXTENSION_NOT_FORWARD",
                        "Bid validity can only be extended forward.");
                if (newValue <= EffectiveSubmissionDeadline(register))
                    throw Validation("TENDER_DOCUMENT_VALIDITY_EXTENSION_INVALID",
                        "Extended bid validity must remain later than the submission deadline.");
                break;
            default:
                throw Validation("TENDER_DOCUMENT_CHANGE_TYPE_INVALID",
                    "The tender-document change type is invalid.");
        }
        var nextSequence = (await Changes.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.RegisterId == register.Id)
            .MaxAsync(item => (int?)item.Sequence, cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var change = new ProcurementTenderDocumentChange
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            RegisterId = register.Id,
            Sequence = nextSequence,
            ChangeType = request.ChangeType,
            Status = ProcurementTenderDocumentChangeStatus.PendingApproval,
            PreviousTemplateVersionId = previousTemplateId,
            NewTemplateVersionId = newTemplate?.Id,
            PreviousValueUtc = previousValue,
            NewValueUtc = newValue,
            RequiresAcknowledgement = true,
            Reason = request.Reason.Trim(),
            WorkflowDefinitionId = definition.Id,
            EvidenceReference = request.EvidenceReference.Trim(),
            EvidenceWorkflowDocumentId = request.EvidenceWorkflowDocumentId,
            EvidenceFileUploadRecordId = request.EvidenceFileUploadRecordId,
            RequestedAtUtc = now,
            RequestedByUserId = _currentUser.UserId,
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(change);
        return await ExecuteAsync(async () =>
        {
            await Changes.AddAsync(change);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var instance = await _workflowInstances.StartWorkflowAsync(
                definition.Id, definition.EntityTypeId, change.Id.ToString(), _currentUser.UserId,
                new
                {
                    register.Id,
                    source.Reference,
                    change.Sequence,
                    change.ChangeType,
                    change.PreviousTemplateVersionId,
                    change.NewTemplateVersionId,
                    change.PreviousValueUtc,
                    change.NewValueUtc
                }, cancellationToken);
            change.WorkflowInstanceId = instance.Id;
            Touch(change);
            Capture(change);
            await Changes.UpdateAsync(change);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordRegisterEventAsync(register, source, "ChangeRequested",
                ProcurementControlEventResult.ReviewRequired, null, ChangeSnapshot(change), change.Reason,
                Evidence(change.EvidenceReference, change.EvidenceWorkflowDocumentId,
                    change.EvidenceFileUploadRecordId, "Tender document change", "DEC-003"),
                correlation, now, cancellationToken);
            return MapChange(change);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentChangeDto> DecideChangeAsync(
        Guid changeId,
        DecideProcurementTenderDocumentChangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var change = await LoadChangeAsync(changeId, tracked: true, cancellationToken);
        var source = await LoadSourceAsync(change.Register.SourceType,
            change.Register.TenderId ?? change.Register.RequestForQuotationId!.Value,
            tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, source.Reference, RegisterSourceType, correlation, cancellationToken);
        EnsureRowVersion(change.RowVersion, request.RowVersion, "TENDER_DOCUMENT_CHANGE");
        var action = request.Action.Trim();
        var approving = string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase);
        var rejecting = string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase);
        if (!approving && !rejecting)
            throw Validation("TENDER_DOCUMENT_CHANGE_ACTION_INVALID", "Action must be Approve or Reject.");
        if (change.Status != ProcurementTenderDocumentChangeStatus.PendingApproval)
        {
            if ((approving && change.Status == ProcurementTenderDocumentChangeStatus.Approved ||
                 rejecting && change.Status == ProcurementTenderDocumentChangeStatus.Rejected) &&
                string.Equals(change.ApprovalReference, request.ApprovalReference.Trim(), StringComparison.OrdinalIgnoreCase))
                return MapChange(change);
            throw Conflict("TENDER_DOCUMENT_CHANGE_ALREADY_DECIDED",
                "The tender-document change has already been decided.");
        }
        Require(request.ApprovalReference, "TENDER_DOCUMENT_CHANGE_APPROVAL_REFERENCE_REQUIRED",
            "An approval or rejection reference is required.");
        await EnsureIndependentActorAsync(change.RequestedByUserId, $"{source.Reference}/change-{change.Sequence}",
            RegisterSourceType, correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(change.WorkflowInstanceId, change.WorkflowDefinitionId,
            change.Id, approving, cancellationToken);
        if (approving)
        {
            RevalidateRegisterLineage(change.Register, source);
            await EnsureChangeWindowOpenAsync(
                change.Register, source, change.ChangeType, cancellationToken);
            await RevalidatePendingChangeAsync(change, source, cancellationToken);
        }
        var now = DateTime.UtcNow;
        var before = ChangeSnapshot(change);
        change.Status = approving
            ? ProcurementTenderDocumentChangeStatus.Approved
            : ProcurementTenderDocumentChangeStatus.Rejected;
        change.WorkflowOutcome = approving ? "Approved" : "Rejected";
        change.ApprovalReference = request.ApprovalReference.Trim();
        change.DecidedAtUtc = now;
        change.DecidedByUserId = _currentUser.UserId;
        if (approving)
        {
            if (change.ChangeType == ProcurementTenderDocumentChangeType.SubmissionDeadlineExtension)
            {
                var extended = change.NewValueUtc!.Value;
                if (source.Tender is not null)
                {
                    source.Tender.SubmissionDeadline = extended;
                    Touch(source.Tender, now);
                    var legacy = await LegacyTenderControls.GetQueryable(item =>
                            item.TenantId == _currentUser.TenantId && item.TenderId == source.Tender.Id && !item.IsDeleted)
                        .SingleOrDefaultAsync(cancellationToken);
                    if (legacy is not null)
                    {
                        legacy.SubmissionDeadlineUtc = extended;
                        legacy.UpdatedAt = now;
                        await LegacyTenderControls.UpdateAsync(legacy);
                    }
                    await Tenders.UpdateAsync(source.Tender);
                }
                else
                {
                    source.Rfq!.SubmissionDeadline = extended;
                    Touch(source.Rfq, now);
                    await Rfqs.UpdateAsync(source.Rfq);
                }
            }
            var recipients = await BuildChangeRecipientsAsync(change, source, request, now, cancellationToken);
            foreach (var recipient in recipients)
                change.Recipients.Add(recipient);
            if (recipients.Count > 0)
            {
                Require(request.DispatchChannel, "TENDER_DOCUMENT_CHANGE_DISPATCH_CHANNEL_REQUIRED",
                    "A dispatch channel is required when approving a change for existing recipients.");
                Require(request.DispatchReference, "TENDER_DOCUMENT_CHANGE_DISPATCH_REFERENCE_REQUIRED",
                    "A dispatch reference is required when approving a change for existing recipients.");
                Require(request.DispatchEvidenceReference, "TENDER_DOCUMENT_CHANGE_DISPATCH_EVIDENCE_REQUIRED",
                    "Dispatch evidence is required when approving a change for existing recipients.");
                change.DispatchedAtUtc = now;
                change.DispatchedByUserId = _currentUser.UserId;
                change.DispatchEvidenceReference = request.DispatchEvidenceReference!.Trim();
            }
        }
        Touch(change, now);
        Capture(change);
        return await ExecuteAsync(async () =>
        {
            if (approving && change.Recipients.Count > 0)
                await ChangeRecipients.AddRangeAsync(change.Recipients);
            await Changes.UpdateAsync(change);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordRegisterEventAsync(change.Register, source,
                approving ? "ChangeApprovedAndDispatched" : "ChangeRejected",
                approving ? ProcurementControlEventResult.Succeeded : ProcurementControlEventResult.Rejected,
                before, ChangeSnapshot(change), request.Comments ?? change.Reason,
                Evidence(change.EvidenceReference, change.EvidenceWorkflowDocumentId,
                    change.EvidenceFileUploadRecordId, "Tender document change decision", "DEC-003"),
                correlation, now, cancellationToken);
            if (approving)
            {
                foreach (var recipient in change.Recipients)
                {
                    await PublishRecipientNotificationAsync("procurement.tender-document.changed", source,
                        recipient.BusinessPartnerId, recipient.RecipientEmail,
                        new
                        {
                            source.Reference,
                            change.Sequence,
                            change.ChangeType,
                            change.NewTemplateVersionId,
                            change.NewValueUtc,
                            recipient.DispatchReference,
                            change.RequiresAcknowledgement
                        }, cancellationToken);
                }
            }
            return MapChange(change);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentAcknowledgementDto> AcknowledgeAsync(
        AcknowledgeProcurementTenderDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        if (request.IssuanceId.HasValue == request.ChangeRecipientId.HasValue)
            throw Validation("TENDER_DOCUMENT_ACK_TARGET_INVALID",
                "Specify exactly one issuance or change recipient to acknowledge.");
        Require(request.AcknowledgementChannel, "TENDER_DOCUMENT_ACK_CHANNEL_REQUIRED",
            "An acknowledgement channel is required.");
        Require(request.AcknowledgementReference, "TENDER_DOCUMENT_ACK_REFERENCE_REQUIRED",
            "An acknowledgement reference is required.");
        Require(request.EvidenceReference, "TENDER_DOCUMENT_ACK_EVIDENCE_REQUIRED",
            "Acknowledgement evidence is required.");
        var replay = await Acknowledgements.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.CorrelationId == correlation)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (replay is not null)
            return MapAcknowledgement(replay);

        ProcurementTenderDocumentIssuance? issuance = null;
        ProcurementTenderDocumentChangeRecipient? recipient = null;
        ProcurementTenderDocumentRegister register;
        Guid? partnerId;
        Guid prohibitedActor;
        if (request.IssuanceId.HasValue)
        {
            issuance = await Issuances.GetQueryable(item => item.Id == request.IssuanceId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .Include(item => item.Register).ThenInclude(item => item.InitialTemplateVersion)
                .Include(item => item.Acknowledgements)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("TENDER_DOCUMENT_ISSUANCE_NOT_FOUND",
                    "The tender-document issuance was not found in the current tenant.");
            register = issuance.Register;
            partnerId = issuance.BusinessPartnerId;
            prohibitedActor = issuance.IssuedByUserId;
            if (issuance.Acknowledgements.Any(item => !item.IsDeleted))
                return MapAcknowledgement(issuance.Acknowledgements.First(item => !item.IsDeleted));
        }
        else
        {
            recipient = await ChangeRecipients.GetQueryable(item => item.Id == request.ChangeRecipientId!.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .Include(item => item.Change).ThenInclude(item => item.Register)
                .Include(item => item.Acknowledgements)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("TENDER_DOCUMENT_CHANGE_RECIPIENT_NOT_FOUND",
                    "The tender-document change recipient was not found in the current tenant.");
            register = recipient.Change.Register;
            partnerId = recipient.BusinessPartnerId;
            prohibitedActor = recipient.Change.RequestedByUserId;
            if (recipient.Acknowledgements.Any(item => !item.IsDeleted))
                return MapAcknowledgement(recipient.Acknowledgements.First(item => !item.IsDeleted));
        }
        var sourceId = register.TenderId ?? register.RequestForQuotationId!.Value;
        var source = await LoadSourceAsync(register.SourceType, sourceId, tracked: false, cancellationToken);
        if (_currentUser.IsExternalUser)
        {
            if (!partnerId.HasValue || !await OwnsBusinessPartnerAsync(partnerId.Value, cancellationToken))
                throw new ProcurementTenderDocumentControlAuthorizationException(
                    "The acknowledgement target does not belong to the current supplier account.");
        }
        else
        {
            await EnsureCapabilityAsync(ManagePermission, source.Reference, RegisterSourceType, correlation, cancellationToken);
            await EnsureIndependentActorAsync(prohibitedActor, $"{source.Reference}/acknowledgement",
                RegisterSourceType, correlation, cancellationToken);
        }
        var now = DateTime.UtcNow;
        var acknowledgement = new ProcurementTenderDocumentAcknowledgement
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            IssuanceId = request.IssuanceId,
            ChangeRecipientId = request.ChangeRecipientId,
            BusinessPartnerId = partnerId,
            Outcome = request.Outcome,
            AcknowledgedAtUtc = now,
            AcknowledgedByUserId = _currentUser.UserId,
            AcknowledgementChannel = request.AcknowledgementChannel.Trim(),
            AcknowledgementReference = request.AcknowledgementReference.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            Issuance = issuance,
            ChangeRecipient = recipient
        };
        Capture(acknowledgement);
        return await ExecuteAsync(async () =>
        {
            await Acknowledgements.AddAsync(acknowledgement);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordRegisterEventAsync(register, source,
                _currentUser.IsExternalUser ? "RecipientAcknowledged" : "ProxyAcknowledgementRecorded",
                request.Outcome == ProcurementTenderDocumentAcknowledgementOutcome.Acknowledged
                    ? ProcurementControlEventResult.Succeeded
                    : ProcurementControlEventResult.Rejected,
                null, AcknowledgementSnapshot(acknowledgement), request.EvidenceReference,
                Evidence(request.EvidenceReference, null, null, "Tender document acknowledgement", "DEC-003"),
                correlation, now, cancellationToken);
            return MapAcknowledgement(acknowledgement);
        }, cancellationToken);
    }

    public async Task<ProcurementTenderDocumentEffectiveStateDto> EnsurePublicationReadyAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        DateTime expectedDeadlineUtc,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var source = await LoadSourceAsync(sourceType, sourceId, tracked: false, cancellationToken);
        var register = await LoadRegisterAsync(sourceType, sourceId, tracked: false, cancellationToken);
        var state = BuildEffectiveState(register);
        RevalidateRegisterLineage(register, source);
        if (EnsureUtc(expectedDeadlineUtc) != state.EffectiveSubmissionDeadlineUtc)
            throw Conflict("TENDER_DOCUMENT_PUBLICATION_DEADLINE_MISMATCH",
                "The source deadline does not match the effective controlled-document deadline.");
        if (!state.Ready)
            throw Conflict("TENDER_DOCUMENT_PUBLICATION_BLOCKED", string.Join("; ", state.BlockedReasons));
        await RecordRegisterEventAsync(register, source, "PublicationGuardPassed",
            ProcurementControlEventResult.Allowed,
            new { expectedDeadlineUtc = EnsureUtc(expectedDeadlineUtc) },
            state, "Controlled document register is ready for publication.",
            [], correlation, DateTime.UtcNow, cancellationToken);
        return state;
    }

    public async Task<ProcurementTenderDocumentEffectiveStateDto> EnsureDispatchReadyAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        IReadOnlyCollection<Guid> businessPartnerIds,
        IReadOnlyCollection<string> externalEmails,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var source = await LoadSourceAsync(sourceType, sourceId, tracked: false, cancellationToken);
        var register = await LoadRegisterAsync(sourceType, sourceId, tracked: false, cancellationToken);
        RevalidateRegisterLineage(register, source);
        var state = BuildEffectiveState(register);
        var missingPartners = businessPartnerIds.Distinct().Where(id =>
            !HasEffectiveDocumentAccess(register, id, null)).ToList();
        var normalizedEmails = (externalEmails ?? Array.Empty<string>()).Select(NormalizeEmail).Where(item => item is not null)
            .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missingEmails = normalizedEmails.Where(email =>
            !HasEffectiveDocumentAccess(register, null, email)).ToList();
        if (missingPartners.Count > 0 || missingEmails.Count > 0)
            throw Conflict("TENDER_DOCUMENT_DISPATCH_RECIPIENTS_NOT_ISSUED",
                "Issue the effective document, or dispatch every later approved addendum, to each selected recipient before dispatch.");
        if (!state.Ready)
            throw Conflict("TENDER_DOCUMENT_DISPATCH_BLOCKED", string.Join("; ", state.BlockedReasons));
        await RecordRegisterEventAsync(register, source, "DispatchGuardPassed",
            ProcurementControlEventResult.Allowed,
            new { businessPartnerIds, externalEmails = normalizedEmails },
            state, "Every selected recipient has an effective issuance or the complete later addendum chain.",
            [], correlation, DateTime.UtcNow, cancellationToken);
        return state;
    }

    public async Task<ProcurementTenderDocumentEffectiveStateDto> EnsureSubmissionReadyAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        Guid businessPartnerId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        if (_currentUser.IsExternalUser && !await OwnsBusinessPartnerAsync(businessPartnerId, cancellationToken))
            throw new ProcurementTenderDocumentControlAuthorizationException(
                "The procurement submission does not belong to the current supplier account.");
        var source = await LoadSourceAsync(sourceType, sourceId, tracked: false, cancellationToken);
        var register = await LoadRegisterAsync(sourceType, sourceId, tracked: false, cancellationToken);
        RevalidateRegisterLineage(register, source);
        var state = BuildEffectiveState(register, businessPartnerId);
        if (!HasEffectiveDocumentAccess(register, businessPartnerId, null))
            throw Conflict("TENDER_DOCUMENT_SUBMISSION_NOT_ISSUED",
                "The effective document was not issued, or a later approved addendum was not dispatched, to this supplier.");
        if (DateTime.UtcNow > state.EffectiveSubmissionDeadlineUtc)
            throw Conflict("TENDER_DOCUMENT_SUBMISSION_WINDOW_CLOSED",
                "The effective controlled-document submission deadline has passed.");
        if (!state.Ready)
            throw Conflict("TENDER_DOCUMENT_SUBMISSION_BLOCKED", string.Join("; ", state.BlockedReasons));
        await RecordRegisterEventAsync(register, source, "SubmissionGuardPassed",
            ProcurementControlEventResult.Allowed, new { businessPartnerId }, state,
            "Supplier issue and mandatory acknowledgement controls passed.",
            [], correlation, DateTime.UtcNow, cancellationToken);
        return state;
    }

    public Task<ProcurementTenderDocumentEffectiveStateDto> EnsureOpeningReadyAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EnsurePostSubmissionReadyAsync(
            sourceType,
            sourceId,
            businessPartnerId: null,
            action: "OpeningGuardPassed",
            blockedCode: "TENDER_DOCUMENT_OPENING_BLOCKED",
            successReason: "The controlled submission window is closed and the effective document lineage is ready for opening.",
            correlationId,
            cancellationToken);

    public Task<ProcurementTenderDocumentEffectiveStateDto> EnsureEvaluationReadyAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EnsurePostSubmissionReadyAsync(
            sourceType,
            sourceId,
            businessPartnerId: null,
            action: "EvaluationGuardPassed",
            blockedCode: "TENDER_DOCUMENT_EVALUATION_BLOCKED",
            successReason: "The effective controlled-document lineage and acknowledgement history are ready for evaluation.",
            correlationId,
            cancellationToken);

    public Task<ProcurementTenderDocumentEffectiveStateDto> EnsureAwardReadyAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        Guid? businessPartnerId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EnsurePostSubmissionReadyAsync(
            sourceType,
            sourceId,
            businessPartnerId,
            action: "AwardGuardPassed",
            blockedCode: "TENDER_DOCUMENT_AWARD_BLOCKED",
            successReason: "The effective controlled-document lineage, recipient access, acknowledgements, and bid validity are ready for award.",
            correlationId,
            cancellationToken);

    public async Task<ProcurementTenderDocumentIssuanceDto> IssueTenderCompatibilityAsync(
        Guid tenderId,
        IssueProcurementTenderDocumentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var register = await LoadRegisterAsync(
            ProcurementTenderDocumentSourceType.Tender, tenderId, tracked: false, cancellationToken);
        return await IssueAsync(new IssueProcurementTenderDocumentControlRequest
        {
            SourceType = ProcurementTenderDocumentSourceType.Tender,
            SourceId = tenderId,
            BusinessPartnerId = request.BusinessPartnerId,
            RecipientName = request.RecipientName,
            RecipientEmail = request.RecipientEmail,
            RecipientPhone = request.RecipientPhone,
            AmountPaid = request.AmountPaid,
            PaymentReference = request.PaymentReference,
            ReceiptNumber = request.IssueReceiptNumber,
            IssueChannel = "LegacyTenderControl",
            EvidenceReference = request.EvidenceReference,
            RegisterRowVersion = Convert.ToBase64String(register.RowVersion)
        }, correlationId, cancellationToken);
    }

    private async Task<ProcurementTenderDocumentEffectiveStateDto>
        EnsurePostSubmissionReadyAsync(
            ProcurementTenderDocumentSourceType sourceType,
            Guid sourceId,
            Guid? businessPartnerId,
            string action,
            string blockedCode,
            string successReason,
            string correlationId,
            CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var source = await LoadSourceAsync(
            sourceType, sourceId, tracked: false, cancellationToken);
        var register = await LoadRegisterAsync(
            sourceType, sourceId, tracked: false, cancellationToken);
        RevalidateRegisterLineage(register, source);
        var state = BuildEffectiveState(register, businessPartnerId);

        state.BlockedReasons.Remove(
            "The effective controlled-document submission deadline has passed.");
        if (DateTime.UtcNow <= state.EffectiveSubmissionDeadlineUtc)
            state.BlockedReasons.Add(
                "The controlled submission window is still open.");
        if (DateTime.UtcNow > state.EffectiveBidValidityUntilUtc)
            state.BlockedReasons.Add(
                "The effective controlled bid-validity period has expired.");
        if (businessPartnerId.HasValue &&
            !HasEffectiveDocumentAccess(register, businessPartnerId.Value, null))
            state.BlockedReasons.Add(
                "The effective controlled document or a later approved addendum was not issued to the selected supplier.");

        state.BlockedReasons = state.BlockedReasons.Distinct().ToList();
        state.Ready = state.BlockedReasons.Count == 0;
        if (!state.Ready)
            throw Conflict(blockedCode, string.Join("; ", state.BlockedReasons));

        await RecordRegisterEventAsync(
            register,
            source,
            action,
            ProcurementControlEventResult.Allowed,
            new { businessPartnerId },
            state,
            successReason,
            [],
            correlation,
            DateTime.UtcNow,
            cancellationToken);
        return state;
    }

    private IQueryable<ProcurementTenderDocumentTemplateVersion> TemplateQuery() =>
        Templates.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private IQueryable<ProcurementTenderDocumentRegister> RegisterQuery() =>
        Registers.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private async Task<ProcurementTenderDocumentTemplateVersion> LoadTemplateAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ProcurementTenderDocumentTemplateVersion> query = TemplateQuery()
            .Where(item => item.Id == id)
            .Include(item => item.ApplicableMethods)
            .Include(item => item.PolicySet)
            .Include(item => item.SourceConfigurationProfile)
            .Include(item => item.WorkflowDefinition);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_TEMPLATE_NOT_FOUND",
                "The tender-document template was not found in the current tenant.");
    }

    private async Task<ProcurementTenderDocumentRegister?> LoadRegisterOrNullAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ProcurementTenderDocumentRegister> query = RegisterQuery()
            .Include(item => item.InitialTemplateVersion)
            .Include(item => item.Issuances).ThenInclude(item => item.TemplateVersion)
            .Include(item => item.Issuances).ThenInclude(item => item.Acknowledgements)
            .Include(item => item.Changes).ThenInclude(item => item.PreviousTemplateVersion)
            .Include(item => item.Changes).ThenInclude(item => item.NewTemplateVersion)
            .Include(item => item.Changes).ThenInclude(item => item.Recipients)
                .ThenInclude(item => item.Acknowledgements);
        query = sourceType == ProcurementTenderDocumentSourceType.Tender
            ? query.Where(item => item.TenderId == sourceId && item.RequestForQuotationId == null)
            : query.Where(item => item.RequestForQuotationId == sourceId && item.TenderId == null);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ProcurementTenderDocumentRegister> LoadRegisterAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        bool tracked,
        CancellationToken cancellationToken) =>
        await LoadRegisterOrNullAsync(sourceType, sourceId, tracked, cancellationToken)
        ?? throw NotFound("TENDER_DOCUMENT_REGISTER_NOT_FOUND",
            "Bind the controlled tender-document register before continuing.");

    private async Task<ProcurementTenderDocumentChange> LoadChangeAsync(
        Guid changeId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ProcurementTenderDocumentChange> query = Changes.GetQueryable(item =>
                item.Id == changeId && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Register).ThenInclude(item => item.InitialTemplateVersion)
            .Include(item => item.Register).ThenInclude(item => item.Issuances)
                .ThenInclude(item => item.TemplateVersion)
            .Include(item => item.Register).ThenInclude(item => item.Issuances)
                .ThenInclude(item => item.Acknowledgements)
            .Include(item => item.Register).ThenInclude(item => item.Changes)
                .ThenInclude(item => item.NewTemplateVersion)
            .Include(item => item.Register).ThenInclude(item => item.Changes)
                .ThenInclude(item => item.Recipients)
                    .ThenInclude(item => item.Acknowledgements)
            .Include(item => item.PreviousTemplateVersion)
            .Include(item => item.NewTemplateVersion)
            .Include(item => item.Recipients).ThenInclude(item => item.Acknowledgements);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_CHANGE_NOT_FOUND",
                "The tender-document change was not found in the current tenant.");
    }

    private async Task<SourceContext> LoadSourceAsync(
        ProcurementTenderDocumentSourceType sourceType,
        Guid sourceId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        ValidateSourceKey(sourceType, sourceId);
        Tender? tender = null;
        RequestForQuotation? rfq = null;
        Guid? sourcingCaseId;
        string reference;
        DateTime? deadline;
        DateTime? opening;
        string currency;
        if (sourceType == ProcurementTenderDocumentSourceType.Tender)
        {
            IQueryable<Tender> query = Tenders.GetQueryable(item =>
                item.Id == sourceId && item.TenantId == _currentUser.TenantId && !item.IsDeleted);
            if (!tracked) query = query.AsNoTracking();
            tender = await query.SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("TENDER_DOCUMENT_SOURCE_NOT_FOUND",
                    "The tender was not found in the current tenant.");
            sourcingCaseId = tender.SourcingCaseId;
            reference = tender.TenderNumber;
            deadline = tender.SubmissionDeadline;
            opening = tender.OpeningDate;
            currency = NormalizeCurrency(tender.Currency ?? string.Empty);
        }
        else
        {
            IQueryable<RequestForQuotation> query = Rfqs.GetQueryable(item =>
                item.Id == sourceId && item.TenantId == _currentUser.TenantId && !item.IsDeleted);
            if (!tracked) query = query.AsNoTracking();
            rfq = await query.SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("TENDER_DOCUMENT_SOURCE_NOT_FOUND",
                    "The RFQ was not found in the current tenant.");
            sourcingCaseId = rfq.SourcingCaseId;
            reference = rfq.RfqNumber;
            deadline = rfq.SubmissionDeadline;
            opening = null;
            currency = NormalizeCurrency(rfq.Currency);
        }
        if (!sourcingCaseId.HasValue)
            throw Validation("TENDER_DOCUMENT_SOURCING_CASE_REQUIRED",
                "The procurement source must be linked to its locked sourcing case.");
        var sourcingCase = await SourcingCases.GetQueryable(item => item.Id == sourcingCaseId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.PolicySet)
            .Include(item => item.MethodRule)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_SOURCING_CASE_NOT_FOUND",
                "The locked sourcing case was not found in the current tenant.");
        if (sourcingCase.Status is ProcurementSourcingCaseStatus.Cancelled or ProcurementSourcingCaseStatus.Closed)
            throw Conflict("TENDER_DOCUMENT_SOURCING_CASE_CLOSED",
                "The locked sourcing case is closed or cancelled.");
        if (sourcingCase.MethodRuleId != sourcingCase.MethodRule.Id ||
            sourcingCase.PolicySetId != sourcingCase.PolicySet.Id ||
            sourcingCase.SelectedMethod != sourcingCase.MethodRule.Method ||
            sourcingCase.MethodRule.PolicySetId != sourcingCase.PolicySetId ||
            sourcingCase.MethodRule.IsDeleted || !sourcingCase.MethodRule.IsEnabled ||
            !sourcingCase.MethodRule.IsAllowed)
            throw Conflict("TENDER_DOCUMENT_SOURCE_LINEAGE_STALE",
                "The source no longer matches its locked policy and method-rule lineage.");
        if (!string.Equals(currency, NormalizeCurrency(sourcingCase.CurrencyCode), StringComparison.OrdinalIgnoreCase))
            throw Conflict("TENDER_DOCUMENT_SOURCE_CURRENCY_STALE",
                "The source currency no longer matches its locked sourcing case.");
        return new SourceContext(
            sourceType, sourceId, reference, deadline.HasValue ? EnsureUtc(deadline.Value) : null,
            opening.HasValue ? EnsureUtc(opening.Value) : null, currency, sourcingCase, tender, rfq);
    }

    private async Task EnsureSourceReaderAsync(SourceContext source, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsExternalUser)
        {
            EnsureInternalReader();
            return;
        }
        IReadOnlySet<Guid> partnerIds =
            await GetExternalPartnerIdsAsync(cancellationToken) ?? new HashSet<Guid>();
        if (partnerIds.Count == 0)
            throw new ProcurementTenderDocumentControlAuthorizationException(
                "The current user is not linked to an active supplier account.");
        var hasControlledDocumentAccess = source.SourceType == ProcurementTenderDocumentSourceType.Tender
            ? await RegisterQuery().Where(item => item.TenderId == source.SourceId)
                .AnyAsync(item =>
                    item.Issuances.Any(issue => !issue.IsDeleted &&
                        issue.BusinessPartnerId.HasValue &&
                        partnerIds.Contains(issue.BusinessPartnerId.Value)) ||
                    item.Changes.Any(change => !change.IsDeleted &&
                        change.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                        change.Recipients.Any(recipient => !recipient.IsDeleted &&
                            recipient.BusinessPartnerId.HasValue &&
                            partnerIds.Contains(recipient.BusinessPartnerId.Value))), cancellationToken)
            : await RegisterQuery().Where(item => item.RequestForQuotationId == source.SourceId)
                .AnyAsync(item =>
                    item.Issuances.Any(issue => !issue.IsDeleted &&
                        issue.BusinessPartnerId.HasValue &&
                        partnerIds.Contains(issue.BusinessPartnerId.Value)) ||
                    item.Changes.Any(change => !change.IsDeleted &&
                        change.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                        change.Recipients.Any(recipient => !recipient.IsDeleted &&
                            recipient.BusinessPartnerId.HasValue &&
                            partnerIds.Contains(recipient.BusinessPartnerId.Value))), cancellationToken);
        if (hasControlledDocumentAccess) return;
        bool allowed;
        if (source.SourceType == ProcurementTenderDocumentSourceType.Tender)
        {
            allowed = await _unitOfWork.Repository<TenderInvitation>().ExistsAsync(item =>
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                    item.TenderId == source.SourceId && partnerIds.Contains(item.BusinessPartnerId)) ||
                await TenderBids.ExistsAsync(item => item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted && item.TenderId == source.SourceId &&
                    partnerIds.Contains(item.BusinessPartnerId));
        }
        else
        {
            allowed = await _unitOfWork.Repository<RequestForQuotationInvitation>().ExistsAsync(item =>
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                    item.RfqId == source.SourceId && partnerIds.Contains(item.BusinessPartnerId)) ||
                await RfqQuotes.ExistsAsync(item => item.TenantId == _currentUser.TenantId &&
                    !item.IsDeleted && item.RfqId == source.SourceId &&
                    partnerIds.Contains(item.BusinessPartnerId));
        }
        if (!allowed)
            throw new ProcurementTenderDocumentControlAuthorizationException(
                "The procurement source is not assigned to the current supplier account.");
    }

    private async Task<IReadOnlySet<Guid>?> GetExternalPartnerIdsAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsExternalUser) return null;
        return (await BusinessPartnerUsers.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.UserId == _currentUser.UserId && item.IsActive && !item.IsDeleted &&
                item.BusinessPartner.TenantId == _currentUser.TenantId &&
                !item.BusinessPartner.IsDeleted && item.BusinessPartner.IsActive &&
                !item.BusinessPartner.IsBlacklisted &&
                item.BusinessPartner.ApprovalStatus ==
                    BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus &&
                (item.BusinessPartner.RegistrationStatus ==
                     BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
                 item.BusinessPartner.RegistrationStatus ==
                     BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus) &&
                (item.BusinessPartner.PartnerType == "Supplier" ||
                 item.BusinessPartner.PartnerType == "Contractor" ||
                 item.BusinessPartner.PartnerType == "Both"))
            .Select(item => item.BusinessPartnerId).ToListAsync(cancellationToken)).ToHashSet();
    }

    private Task<bool> OwnsBusinessPartnerAsync(Guid partnerId, CancellationToken cancellationToken) =>
        BusinessPartnerUsers.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.UserId == _currentUser.UserId && item.BusinessPartnerId == partnerId &&
                item.IsActive && !item.IsDeleted &&
                item.BusinessPartner.TenantId == _currentUser.TenantId &&
                !item.BusinessPartner.IsDeleted && item.BusinessPartner.IsActive &&
                !item.BusinessPartner.IsBlacklisted &&
                item.BusinessPartner.ApprovalStatus ==
                    BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus &&
                (item.BusinessPartner.RegistrationStatus ==
                     BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
                 item.BusinessPartner.RegistrationStatus ==
                     BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus) &&
                (item.BusinessPartner.PartnerType == "Supplier" ||
                 item.BusinessPartner.PartnerType == "Contractor" ||
                 item.BusinessPartner.PartnerType == "Both"))
            .AnyAsync(cancellationToken);

    private async Task<(ProcurementPolicySet Policy, ProcurementConfigurationProfile Profile)>
        ValidateTemplateLineageAsync(
            SaveProcurementTenderDocumentTemplateRequest request,
            CancellationToken cancellationToken)
    {
        if (request.PolicySetId == Guid.Empty || request.SourceConfigurationProfileId == Guid.Empty)
            throw Validation("TENDER_DOCUMENT_TEMPLATE_LINEAGE_REQUIRED",
                "PolicySetId and SourceConfigurationProfileId are required.");
        var policy = await PolicySets.GetQueryable(item => item.Id == request.PolicySetId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_POLICY_NOT_FOUND",
                "The exact policy set was not found in the current tenant.");
        var now = DateTime.UtcNow;
        if (policy.LifecycleStatus != ProcurementPolicyLifecycleStatus.Published ||
            policy.EffectiveFrom > now ||
            policy.EffectiveTo.HasValue && policy.EffectiveTo.Value < now)
            throw Conflict("TENDER_DOCUMENT_POLICY_NOT_EFFECTIVE",
                "A tender-document template must use a current immutable Published policy set.");
        if (!string.Equals(policy.Code, NormalizeCode(request.PolicySetCode), StringComparison.Ordinal) ||
            policy.Version != request.PolicySetVersion)
            throw Validation("TENDER_DOCUMENT_POLICY_SNAPSHOT_MISMATCH",
                "PolicySetCode and PolicySetVersion must exactly match PolicySetId.");
        if (policy.SourceConfigurationProfileId != request.SourceConfigurationProfileId)
            throw Validation("TENDER_DOCUMENT_PROFILE_LINEAGE_MISMATCH",
                "The policy set is not derived from the supplied configuration profile.");
        var profile = await ConfigurationProfiles.GetQueryable(item =>
                item.Id == request.SourceConfigurationProfileId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_PROFILE_NOT_FOUND",
                "The exact source configuration profile was not found in the current tenant.");
        if (profile.LifecycleStatus != ProcurementConfigurationProfileStatus.Published ||
            profile.EffectiveFrom > now ||
            profile.EffectiveTo.HasValue && profile.EffectiveTo.Value < now)
            throw Conflict("TENDER_DOCUMENT_PROFILE_NOT_EFFECTIVE",
                "A tender-document template must use a current immutable Published configuration profile.");
        var methods = request.ApplicableMethods.Distinct().ToList();
        var validMethods = await MethodRules.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.PolicySetId == policy.Id && !item.IsDeleted && item.IsEnabled && item.IsAllowed &&
                methods.Contains(item.Method))
            .Select(item => item.Method).Distinct().ToListAsync(cancellationToken);
        if (methods.Any(method => !validMethods.Contains(method)))
            throw Validation("TENDER_DOCUMENT_TEMPLATE_METHOD_NOT_ALLOWED",
                "Every applicable method must be allowed by the selected Published policy set.");
        return (policy, profile);
    }

    private async Task ValidateTemplateForSubmissionAsync(
        ProcurementTenderDocumentTemplateVersion template,
        CancellationToken cancellationToken)
    {
        if (template.ApplicableMethods.Count(item => !item.IsDeleted) == 0)
            throw Validation("TENDER_DOCUMENT_TEMPLATE_METHOD_REQUIRED",
                "At least one applicable procurement method is required.");
        if (template.Version > 1 && string.IsNullOrWhiteSpace(template.ChangeSummary))
            throw Validation("TENDER_DOCUMENT_TEMPLATE_CHANGE_SUMMARY_REQUIRED",
                "A change summary is required for a replacement version.");
        await ValidateWorkflowDefinitionAsync(template.WorkflowDefinitionId, cancellationToken);
        var request = new SaveProcurementTenderDocumentTemplateRequest
        {
            PolicySetId = template.PolicySetId,
            PolicySetCode = template.PolicySetCode,
            PolicySetVersion = template.PolicySetVersion,
            SourceConfigurationProfileId = template.SourceConfigurationProfileId,
            ApplicableMethods = template.ApplicableMethods.Where(item => !item.IsDeleted).Select(item => item.Method).ToList()
        };
        await ValidateTemplateLineageAsync(request, cancellationToken);
        if (ComputeHash(template.LifecycleSnapshotJson) != template.IntegrityHash)
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_INTEGRITY_FAILED",
                "The tender-document template failed integrity verification.");
    }

    private async Task ValidateTemplateForPublicationAsync(
        ProcurementTenderDocumentTemplateVersion template,
        CancellationToken cancellationToken)
    {
        await ValidateTemplateForSubmissionAsync(template, cancellationToken);
        await ValidateTemplateContentArtifactAsync(
            template.ContentWorkflowEvidenceDocumentId, template.ContentFileUploadRecordId,
            template.ContentReference, template.ContentChecksumSha256, template, requireApproved: true, cancellationToken);
        if (!template.WorkflowInstanceId.HasValue)
            throw Conflict("TENDER_DOCUMENT_WORKFLOW_NOT_STARTED",
                "The configured shared workflow was not started.");
        var contentStepInstanceId = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
            .GetQueryable(item => item.Id == template.ContentWorkflowEvidenceDocumentId!.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Select(item => item.StepInstanceId)
            .SingleAsync(cancellationToken);
        var exactWorkflowContent = await WorkflowStepInstanceRows.GetQueryable(item =>
                item.Id == contentStepInstanceId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.WorkflowInstanceId == template.WorkflowInstanceId.Value)
            .AnyAsync(cancellationToken);
        if (!exactWorkflowContent)
            throw Conflict("TENDER_DOCUMENT_CONTENT_WORKFLOW_MISMATCH",
                "Controlled content must come from this template's exact sourcing approval workflow instance.");
    }

    private async Task EnsureTemplateCompatibleAsync(
        ProcurementTenderDocumentTemplateVersion template,
        SourceContext source,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        var at = EnsureUtc(atUtc);
        if (template.Status != ProcurementTenderDocumentTemplateStatus.Published ||
            template.EffectiveFromUtc > at ||
            template.EffectiveToUtc.HasValue && template.EffectiveToUtc.Value < at)
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_NOT_EFFECTIVE",
                "The selected tender-document template is not Published and effective.");
        if (template.PolicySetId != source.SourcingCase.PolicySetId ||
            template.PolicySetCode != source.SourcingCase.PolicyCode ||
            template.PolicySetVersion != source.SourcingCase.PolicyVersion ||
            template.SourceConfigurationProfileId != source.SourcingCase.PolicySet.SourceConfigurationProfileId)
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_LINEAGE_MISMATCH",
                "The selected template does not match the source's exact policy/profile lineage.");
        if (!template.ApplicableMethods.Any(item =>
            !item.IsDeleted && item.Method == source.SourcingCase.SelectedMethod))
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_METHOD_MISMATCH",
                "The selected template does not allow the source's locked procurement method.");
        var ruleValid = await MethodRules.GetQueryable(item => item.Id == source.SourcingCase.MethodRuleId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsEnabled &&
                item.IsAllowed && item.PolicySetId == template.PolicySetId &&
                item.Method == source.SourcingCase.SelectedMethod)
            .AnyAsync(cancellationToken);
        if (!ruleValid)
            throw Conflict("TENDER_DOCUMENT_METHOD_RULE_STALE",
                "The source's exact method rule is no longer valid.");
    }

    private async Task ValidateEvidenceReferenceAsync(
        Guid? workflowEvidenceDocumentId,
        Guid? fileUploadRecordId,
        CancellationToken cancellationToken)
    {
        if (workflowEvidenceDocumentId.HasValue)
        {
            var exists = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                .GetQueryable(item => item.Id == workflowEvidenceDocumentId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!exists)
                throw NotFound("TENDER_DOCUMENT_WORKFLOW_EVIDENCE_NOT_FOUND",
                    "The workflow evidence document was not found in the current tenant.");
        }
        if (fileUploadRecordId.HasValue)
        {
            var exists = await _unitOfWork.Repository<FileUploadRecord>()
                .GetQueryable(item => item.Id == fileUploadRecordId.Value &&
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!exists)
                throw NotFound("TENDER_DOCUMENT_FILE_EVIDENCE_NOT_FOUND",
                    "The uploaded evidence file was not found in the current tenant.");
        }
    }

    private async Task ValidateTemplateContentArtifactAsync(
        Guid? workflowEvidenceDocumentId,
        Guid? fileUploadRecordId,
        string contentReference,
        string checksumSha256,
        ProcurementTenderDocumentTemplateVersion template,
        bool requireApproved,
        CancellationToken cancellationToken)
    {
        if (!workflowEvidenceDocumentId.HasValue || fileUploadRecordId.HasValue)
            throw Validation("TENDER_DOCUMENT_CONTENT_ARTIFACT_REQUIRED",
                "A controlled template must reference exactly one workflow evidence document.");

        var artifact = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
            .GetQueryable(item => item.Id == workflowEvidenceDocumentId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_WORKFLOW_EVIDENCE_NOT_FOUND",
                "The workflow evidence document was not found in the current tenant.");
        if (!artifact.IsCurrent)
            throw Conflict("TENDER_DOCUMENT_CONTENT_ARTIFACT_SUPERSEDED",
                "The workflow evidence document is no longer the current content artifact.");
        if (artifact.ExpiryDate.HasValue && artifact.ExpiryDate.Value < DateTime.UtcNow)
            throw Conflict("TENDER_DOCUMENT_CONTENT_ARTIFACT_EXPIRED",
                "The controlled content artifact has expired.");
        if (!string.Equals(artifact.FilePath, contentReference.Trim(), StringComparison.Ordinal))
            throw Validation("TENDER_DOCUMENT_CONTENT_REFERENCE_MISMATCH",
                "ContentReference must exactly match the controlled evidence document path.");
        if (!string.Equals(artifact.Sha256, checksumSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            throw Validation("TENDER_DOCUMENT_CHECKSUM_MISMATCH",
                "ContentChecksumSha256 must exactly match the controlled evidence document.");
        if (artifact.MalwareScanStatus is WorkflowMalwareScanStatus.Infected or WorkflowMalwareScanStatus.Failed)
            throw Conflict("TENDER_DOCUMENT_CONTENT_SCAN_FAILED",
                "The controlled content artifact failed malware scanning.");
        if (artifact.VerificationStatus == WorkflowEvidenceVerificationStatus.Rejected)
            throw Conflict("TENDER_DOCUMENT_CONTENT_VERIFICATION_REJECTED",
                "The controlled content artifact was rejected during verification.");
        if (requireApproved && (artifact.MalwareScanStatus != WorkflowMalwareScanStatus.Clean ||
            (artifact.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified &&
             !await HasConfiguredContentReviewAsync(template, artifact, cancellationToken))))
            throw Conflict("TENDER_DOCUMENT_CONTENT_NOT_APPROVED",
                "The controlled content artifact must be malware-scan clean and independently verified, " +
                "unless its recorded approval policy explicitly permits completed independent workflow review instead.");
    }

    private async Task<bool> HasConfiguredContentReviewAsync(
        ProcurementTenderDocumentTemplateVersion template,
        WorkflowEvidenceDocument artifact,
        CancellationToken cancellationToken)
    {
        // Absence of a recorded, explicit policy is deliberately strict for existing workflows.
        // A live/default policy must never retroactively weaken an already-started approval route.
        if (!template.WorkflowInstanceId.HasValue || string.IsNullOrWhiteSpace(artifact.RequirementKey) ||
            artifact.VerificationStatus != WorkflowEvidenceVerificationStatus.Pending)
            return false;
        var workflow = await WorkflowInstanceRows.GetQueryable(item =>
                item.Id == template.WorkflowInstanceId.Value && item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.EntityId == template.Id &&
                item.WorkflowDefinitionId == template.WorkflowDefinitionId &&
                item.Status == WorkflowInstanceStatus.Completed)
            .Include(item => item.EntityType).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (workflow == null || !await WorkflowDefinitions.GetQueryable(item =>
                item.Id == workflow.WorkflowDefinitionId && item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AnyAsync(cancellationToken))
            return false;
        var steps = await WorkflowStepInstanceRows.GetQueryable(item =>
                item.WorkflowInstanceId == workflow.Id && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.WorkflowStep).Include(item => item.Approvals)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (!steps.Any(item => item.Id == artifact.StepInstanceId))
            return false;
        var approvalSteps = steps.Where(item => item.WorkflowStep != null &&
            item.WorkflowStep.StepType == WorkflowStepType.Approval).ToList();
        if (approvalSteps.Count == 0)
            return false;
        var policyActivities = await _unitOfWork.Repository<WorkflowActivityLog>().GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.WorkflowInstanceId == workflow.Id && item.PerformedById == null &&
                item.ActivityType == WorkflowActivityType.DataUpdated && item.Title == "Approval policy applied")
            .AsNoTracking().ToListAsync(cancellationToken);
        var matchedWaiver = false;
        foreach (var step in approvalSteps)
        {
            if (step.WorkflowStep.TenantId != _currentUser.TenantId || step.WorkflowStep.IsDeleted ||
                step.WorkflowStep.WorkflowDefinitionId != template.WorkflowDefinitionId ||
                step.Status != WorkflowStepInstanceStatus.Completed ||
                !step.Approvals.Any(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                    item.Status == WorkflowApprovalStatus.Approved && item.ProcessedById.HasValue &&
                    item.ProcessedById.Value != artifact.UploadedById &&
                    item.ProcessedDate.HasValue && item.ProcessedDate.Value >= artifact.UploadedAt))
                return false;
            try
            {
                using var result = JsonDocument.Parse(step.ResultData ?? "{}");
                if (result.RootElement.ValueKind != JsonValueKind.Object || HasAmbiguousJsonProperties(result.RootElement) ||
                    !result.RootElement.TryGetProperty("appliedApprovalPolicySetId", out var appliedId) ||
                    appliedId.ValueKind != JsonValueKind.String || !appliedId.TryGetGuid(out var policyId))
                    return false;
                var startedAt = workflow.StartedDate ?? workflow.CreatedDate;
                // ResultData can include caller input. Only the separate, server-written event
                // corroborates which policy the engine actually applied to this exact step.
                var appliedEvents = policyActivities.Where(item => item.StepInstanceId == step.Id).ToList();
                if (appliedEvents.Count != 1 || appliedEvents[0].ActivityDate < startedAt ||
                    appliedEvents[0].ActivityDate < step.CreatedDate ||
                    !step.Approvals.Any(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                        item.Status == WorkflowApprovalStatus.Approved && item.ProcessedById.HasValue &&
                        item.ProcessedById.Value != artifact.UploadedById &&
                        item.ProcessedDate.HasValue && item.ProcessedDate.Value >= artifact.UploadedAt &&
                        item.ProcessedDate.Value >= appliedEvents[0].ActivityDate))
                    return false;
                using var provenance = JsonDocument.Parse(appliedEvents[0].Data ?? "{}");
                if (provenance.RootElement.ValueKind != JsonValueKind.Object ||
                    HasAmbiguousJsonProperties(provenance.RootElement) ||
                    !provenance.RootElement.TryGetProperty("PolicySetId", out var recordedId) ||
                    recordedId.ValueKind != JsonValueKind.String || !recordedId.TryGetGuid(out var recordedPolicyId) ||
                    recordedPolicyId != policyId ||
                    !provenance.RootElement.TryGetProperty("PolicyCode", out var recordedCode) ||
                    recordedCode.ValueKind != JsonValueKind.String)
                    return false;
                var policy = await _unitOfWork.Repository<WorkflowApprovalPolicySet>().GetQueryable(item =>
                        item.Id == policyId && item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                        item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                        item.EffectiveFrom <= startedAt && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= startedAt))
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                if (policy == null || !string.Equals(policy.Code, recordedCode.GetString(), StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(policy.EntityType) &&
                    !string.Equals(policy.EntityType.Trim(), workflow.EntityType?.Name, StringComparison.OrdinalIgnoreCase)))
                    return false;
                using var configuration = JsonDocument.Parse(policy.ApprovalConfiguration);
                if (HasAmbiguousJsonProperties(configuration.RootElement))
                    return false;
                var config = JsonSerializer.Deserialize<WorkflowApprovalConfigDto>(
                    policy.ApprovalConfiguration, WorkflowConfigJsonOptions);
                if (config?.EvidenceRequirements == null)
                    return false;
                var requirements = config.EvidenceRequirements.Where(item => item != null &&
                    string.Equals(item.RequirementKey?.Trim(), artifact.RequirementKey.Trim(),
                        StringComparison.OrdinalIgnoreCase)).ToList();
                if (requirements.Count == 0)
                    continue;
                if (requirements.Count != 1 || requirements[0].RequireVerification ||
                    requirements[0].MinimumDocuments != 1 ||
                    (!string.IsNullOrWhiteSpace(requirements[0].DocumentType) &&
                     !string.Equals(requirements[0].DocumentType.Trim(), artifact.DocumentType?.Trim(),
                         StringComparison.OrdinalIgnoreCase)))
                    return false;
                matchedWaiver = true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
        return matchedWaiver;
    }

    private static bool HasAmbiguousJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Any(HasAmbiguousJsonProperties);
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return element.EnumerateObject().Any(item =>
            !names.Add(item.Name) || HasAmbiguousJsonProperties(item.Value));
    }

    private async Task<List<ProcurementTenderDocumentChangeRecipient>> BuildChangeRecipientsAsync(
        ProcurementTenderDocumentChange change,
        SourceContext source,
        DecideProcurementTenderDocumentChangeRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var candidates = new List<RecipientCandidate>();
        candidates.AddRange(change.Register.Issuances.Where(item => !item.IsDeleted).Select(item =>
            new RecipientCandidate(
                ProcurementTenderDocumentRecipientSourceType.Issuance,
                item.Id, null, null, item.BusinessPartnerId, item.RecipientKey, item.RecipientName,
                item.RecipientEmail, item.RecipientPhone)));
        if (source.SourceType == ProcurementTenderDocumentSourceType.Tender)
        {
            var bids = await TenderBids.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.TenderId == source.SourceId && !item.IsDeleted && item.Status != "Draft")
                .Include(item => item.BusinessPartner).AsNoTracking().ToListAsync(cancellationToken);
            candidates.AddRange(bids.Select(item => new RecipientCandidate(
                ProcurementTenderDocumentRecipientSourceType.TenderBid,
                null, item.Id, null, item.BusinessPartnerId, RecipientKey(item.BusinessPartnerId, null),
                item.BusinessPartner.PartnerName, item.BusinessPartner.PrimaryEmail, item.BusinessPartner.PrimaryPhone)));
        }
        else
        {
            var quotes = await RfqQuotes.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.RfqId == source.SourceId && !item.IsDeleted && item.Status == "Submitted")
                .Include(item => item.BusinessPartner).AsNoTracking().ToListAsync(cancellationToken);
            candidates.AddRange(quotes.Select(item => new RecipientCandidate(
                ProcurementTenderDocumentRecipientSourceType.RequestForQuotationQuote,
                null, null, item.Id, item.BusinessPartnerId, RecipientKey(item.BusinessPartnerId, null),
                item.BusinessPartner.PartnerName, item.BusinessPartner.PrimaryEmail, item.BusinessPartner.PrimaryPhone)));
        }
        var result = new List<ProcurementTenderDocumentChangeRecipient>();
        foreach (var candidate in candidates
            .GroupBy(item => item.RecipientKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => item.SourceType).First()))
        {
            var recipient = new ProcurementTenderDocumentChangeRecipient
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                ChangeId = change.Id,
                SourceType = candidate.SourceType,
                IssuanceId = candidate.IssuanceId,
                TenderBidId = candidate.TenderBidId,
                RequestForQuotationQuoteId = candidate.RfqQuoteId,
                BusinessPartnerId = candidate.BusinessPartnerId,
                RecipientKey = candidate.RecipientKey,
                RecipientName = candidate.Name,
                RecipientEmail = NormalizeEmail(candidate.Email),
                RecipientPhone = TrimOrNull(candidate.Phone, 30),
                DispatchChannel = request.DispatchChannel?.Trim() ?? string.Empty,
                DispatchReference = request.DispatchReference?.Trim() ?? string.Empty,
                DispatchedAtUtc = now,
                DispatchedByUserId = _currentUser.UserId,
                DispatchEvidenceReference = request.DispatchEvidenceReference?.Trim() ?? string.Empty,
                CreatedAt = now,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId,
                Change = change
            };
            Capture(recipient);
            result.Add(recipient);
        }
        return result;
    }

    private async Task EnsureChangeWindowOpenAsync(
        ProcurementTenderDocumentRegister register,
        SourceContext source,
        ProcurementTenderDocumentChangeType changeType,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var validityExtension = changeType == ProcurementTenderDocumentChangeType.BidValidityExtension;
        if (validityExtension)
        {
            if (EffectiveBidValidity(register) <= now)
                throw Conflict("TENDER_DOCUMENT_VALIDITY_EXTENSION_RETROACTIVE",
                    "Bid validity cannot be extended after the current effective validity has expired.");
        }
        else if (EffectiveSubmissionDeadline(register) <= now)
        {
            throw Conflict("TENDER_DOCUMENT_CHANGE_RETROACTIVE",
                "An addendum or submission-deadline extension cannot be created after the effective submission deadline.");
        }
        if (source.Tender is not null)
        {
            if (source.Tender.Status is "Awarded" or "Cancelled" ||
                !validityExtension && source.Tender.Status == "Closed")
                throw Conflict("TENDER_DOCUMENT_CHANGE_SOURCE_TERMINAL",
                    validityExtension
                        ? "A bid-validity extension cannot be created for an awarded or cancelled tender."
                        : "An addendum or submission-deadline extension cannot be created for a closed, awarded, or cancelled tender.");
            if (validityExtension) return;
            var opened = await LegacyTenderControls.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.TenderId == source.Tender.Id &&
                    !item.IsDeleted && item.OpenedAtUtc.HasValue)
                .AnyAsync(cancellationToken) ||
                await TenderBids.GetQueryable(item =>
                        item.TenantId == _currentUser.TenantId && item.TenderId == source.Tender.Id &&
                        !item.IsDeleted &&
                        (item.OpenedDate.HasValue || item.Status == "Opened"))
                    .AnyAsync(cancellationToken);
            if (opened)
                throw Conflict("TENDER_DOCUMENT_CHANGE_AFTER_OPENING",
                    "A controlled change cannot be created after the sealed tender opening has started.");
        }
        else
        {
            if (source.Rfq!.Status is "Awarded" or "Cancelled" ||
                !validityExtension && source.Rfq.Status == "Closed")
                throw Conflict("TENDER_DOCUMENT_CHANGE_SOURCE_TERMINAL",
                    validityExtension
                        ? "A bid-validity extension cannot be created for an awarded or cancelled RFQ."
                        : "An addendum or submission-deadline extension cannot be created for a closed, awarded, or cancelled RFQ.");
            if (validityExtension) return;
            var opened = await _unitOfWork.Repository<ProcurementRfqOpeningRegister>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.RfqId == source.Rfq.Id && !item.IsDeleted)
                .AnyAsync(cancellationToken);
            if (opened)
                throw Conflict("TENDER_DOCUMENT_CHANGE_AFTER_OPENING",
                    "A controlled change cannot be created after the sealed RFQ opening has started.");
        }
    }

    private async Task RevalidatePendingChangeAsync(
        ProcurementTenderDocumentChange change,
        SourceContext source,
        CancellationToken cancellationToken)
    {
        if (ComputeHash(change.LifecycleSnapshotJson) != change.IntegrityHash)
            throw Conflict("TENDER_DOCUMENT_CHANGE_INTEGRITY_FAILED",
                "The pending controlled change failed integrity verification.");

        switch (change.ChangeType)
        {
            case ProcurementTenderDocumentChangeType.Addendum:
            {
                var current = EffectiveTemplate(change.Register);
                if (change.PreviousTemplateVersionId != current.Id ||
                    !change.NewTemplateVersionId.HasValue)
                    throw Conflict("TENDER_DOCUMENT_ADDENDUM_BASE_STALE",
                        "The addendum no longer follows the current effective template version.");
                var replacement = await LoadTemplateAsync(
                    change.NewTemplateVersionId.Value, tracked: false, cancellationToken);
                await EnsureTemplateCompatibleAsync(replacement, source, DateTime.UtcNow, cancellationToken);
                if (replacement.TemplateKey != current.TemplateKey ||
                    replacement.Version <= current.Version)
                    throw Conflict("TENDER_DOCUMENT_ADDENDUM_VERSION_STALE",
                        "The addendum replacement is no longer a later version in the same template family.");
                break;
            }
            case ProcurementTenderDocumentChangeType.SubmissionDeadlineExtension:
            {
                var current = EffectiveSubmissionDeadline(change.Register);
                if (!change.PreviousValueUtc.HasValue ||
                    EnsureUtc(change.PreviousValueUtc.Value) != current ||
                    !change.NewValueUtc.HasValue ||
                    EnsureUtc(change.NewValueUtc.Value) <= current)
                    throw Conflict("TENDER_DOCUMENT_DEADLINE_BASE_STALE",
                        "The deadline extension no longer follows the current effective deadline.");
                if (!source.SubmissionDeadlineUtc.HasValue ||
                    EnsureUtc(source.SubmissionDeadlineUtc.Value) != current)
                    throw Conflict("TENDER_DOCUMENT_SOURCE_DEADLINE_STALE",
                        "The procurement source deadline no longer matches the controlled register.");
                if (change.Register.OpeningScheduledAtUtc.HasValue &&
                    EnsureUtc(change.NewValueUtc.Value) > change.Register.OpeningScheduledAtUtc.Value)
                    throw Conflict("TENDER_DOCUMENT_DEADLINE_AFTER_OPENING",
                        "The submission deadline cannot extend beyond the immutable scheduled opening.");
                break;
            }
            case ProcurementTenderDocumentChangeType.BidValidityExtension:
            {
                var current = EffectiveBidValidity(change.Register);
                if (!change.PreviousValueUtc.HasValue ||
                    EnsureUtc(change.PreviousValueUtc.Value) != current ||
                    !change.NewValueUtc.HasValue ||
                    EnsureUtc(change.NewValueUtc.Value) <= current ||
                    EnsureUtc(change.NewValueUtc.Value) <= EffectiveSubmissionDeadline(change.Register))
                    throw Conflict("TENDER_DOCUMENT_VALIDITY_BASE_STALE",
                        "The bid-validity extension no longer follows the current controlled validity period.");
                break;
            }
            default:
                throw Validation("TENDER_DOCUMENT_CHANGE_TYPE_INVALID",
                    "The tender-document change type is invalid.");
        }
    }

    private ProcurementTenderDocumentEffectiveStateDto BuildEffectiveState(
        ProcurementTenderDocumentRegister register,
        Guid? businessPartnerId = null)
    {
        var template = EffectiveTemplate(register);
        var deadline = EffectiveSubmissionDeadline(register);
        var validity = EffectiveBidValidity(register);
        var blocked = new List<string>();
        if (ComputeHash(register.LifecycleSnapshotJson) != register.IntegrityHash)
            blocked.Add("The controlled-document register failed integrity verification.");
        if (ComputeHash(template.LifecycleSnapshotJson) != template.IntegrityHash)
            blocked.Add("The effective tender-document template failed integrity verification.");
        if (register.Changes.Any(item =>
            !item.IsDeleted && item.Status == ProcurementTenderDocumentChangeStatus.PendingApproval))
            blocked.Add("A tender-document change is still pending approval.");
        var mandatoryRecipients = register.Changes
            .Where(item => !item.IsDeleted &&
                item.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                item.RequiresAcknowledgement)
            .SelectMany(item => item.Recipients)
            .Where(item => !item.IsDeleted &&
                (!businessPartnerId.HasValue || item.BusinessPartnerId == businessPartnerId.Value))
            .ToList();
        if (mandatoryRecipients.Any(item => !item.Acknowledgements.Any(ack =>
            !ack.IsDeleted && ack.Outcome == ProcurementTenderDocumentAcknowledgementOutcome.Acknowledged)))
            blocked.Add("One or more mandatory controlled-document changes have not been acknowledged.");
        if (deadline <= DateTime.UtcNow)
            blocked.Add("The effective controlled-document submission deadline has passed.");
        if (validity <= deadline)
            blocked.Add("The effective bid-validity date does not extend beyond the submission deadline.");
        return new ProcurementTenderDocumentEffectiveStateDto
        {
            RegisterId = register.Id,
            SourceType = register.SourceType,
            SourceId = register.TenderId ?? register.RequestForQuotationId!.Value,
            EffectiveTemplateVersionId = template.Id,
            EffectiveTemplateReference = $"{template.TemplateCode}/v{template.Version}",
            EffectiveSubmissionDeadlineUtc = deadline,
            EffectiveBidValidityUntilUtc = validity,
            Ready = blocked.Count == 0,
            BlockedReasons = blocked
        };
    }

    private static ProcurementTenderDocumentTemplateVersion EffectiveTemplate(
        ProcurementTenderDocumentRegister register) =>
        register.Changes.Where(item => !item.IsDeleted &&
                item.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                item.ChangeType == ProcurementTenderDocumentChangeType.Addendum &&
                item.NewTemplateVersion is not null)
            .OrderByDescending(item => item.Sequence)
            .Select(item => item.NewTemplateVersion!)
            .FirstOrDefault() ?? register.InitialTemplateVersion;

    private static bool HasEffectiveDocumentAccess(
        ProcurementTenderDocumentRegister register,
        Guid? businessPartnerId,
        string? recipientEmail)
    {
        bool Matches(Guid? partnerId, string? email) =>
            businessPartnerId.HasValue
                ? partnerId == businessPartnerId
                : !string.IsNullOrWhiteSpace(recipientEmail) &&
                  string.Equals(email, recipientEmail, StringComparison.OrdinalIgnoreCase);

        var effectiveTemplateId = EffectiveTemplate(register).Id;
        var approvedAddenda = register.Changes
            .Where(item => !item.IsDeleted &&
                item.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                item.ChangeType == ProcurementTenderDocumentChangeType.Addendum &&
                item.NewTemplateVersionId.HasValue)
            .OrderBy(item => item.Sequence)
            .ToList();

        foreach (var issuance in register.Issuances.Where(item =>
                     !item.IsDeleted && Matches(item.BusinessPartnerId, item.RecipientEmail)))
        {
            if (issuance.TemplateVersionId == effectiveTemplateId)
                return true;

            var issuanceSequence = issuance.TemplateVersionId == register.InitialTemplateVersionId
                ? 0
                : approvedAddenda
                    .Where(item => item.NewTemplateVersionId == issuance.TemplateVersionId)
                    .Select(item => item.Sequence)
                    .DefaultIfEmpty(-1)
                    .Max();
            if (issuanceSequence < 0)
                continue;

            if (approvedAddenda
                .Where(item => item.Sequence > issuanceSequence)
                .All(change => change.Recipients.Any(recipient =>
                    !recipient.IsDeleted &&
                    Matches(recipient.BusinessPartnerId, recipient.RecipientEmail))))
                return true;
        }

        return false;
    }

    private static DateTime EffectiveSubmissionDeadline(ProcurementTenderDocumentRegister register) =>
        register.Changes.Where(item => !item.IsDeleted &&
                item.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                item.ChangeType == ProcurementTenderDocumentChangeType.SubmissionDeadlineExtension &&
                item.NewValueUtc.HasValue)
            .OrderByDescending(item => item.Sequence)
            .Select(item => item.NewValueUtc!.Value)
            .FirstOrDefault() is var value && value != default
                ? value
                : register.OriginalSubmissionDeadlineUtc;

    private static DateTime EffectiveBidValidity(ProcurementTenderDocumentRegister register) =>
        register.Changes.Where(item => !item.IsDeleted &&
                item.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                item.ChangeType == ProcurementTenderDocumentChangeType.BidValidityExtension &&
                item.NewValueUtc.HasValue)
            .OrderByDescending(item => item.Sequence)
            .Select(item => item.NewValueUtc!.Value)
            .FirstOrDefault() is var value && value != default
                ? value
                : register.OriginalBidValidityUntilUtc;

    private static void RevalidateRegisterLineage(
        ProcurementTenderDocumentRegister register,
        SourceContext source)
    {
        if (register.SourceType != source.SourceType ||
            (register.TenderId ?? register.RequestForQuotationId) != source.SourceId ||
            register.SourcingCaseId != source.SourcingCase.Id ||
            register.MethodRuleId != source.SourcingCase.MethodRuleId ||
            register.Method != source.SourcingCase.SelectedMethod ||
            register.MethodRuleCode != source.SourcingCase.MethodRuleCode ||
            register.PolicySetId != source.SourcingCase.PolicySetId ||
            register.PolicySetCode != source.SourcingCase.PolicyCode ||
            register.PolicySetVersion != source.SourcingCase.PolicyVersion ||
            register.SourceConfigurationProfileId != source.SourcingCase.PolicySet.SourceConfigurationProfileId)
            throw Conflict("TENDER_DOCUMENT_REGISTER_LINEAGE_STALE",
                "The controlled-document register no longer matches the source's locked sourcing lineage.");
        if (!string.Equals(register.CurrencyCode, source.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw Conflict("TENDER_DOCUMENT_REGISTER_CURRENCY_STALE",
                "The controlled-document register currency no longer matches the source.");
    }

    private async Task<WorkflowDefinition> LoadWorkflowDefinitionAsync(
        Guid workflowDefinitionId,
        CancellationToken cancellationToken)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw Validation("TENDER_DOCUMENT_WORKFLOW_REQUIRED",
                "A shared workflow definition is required.");
        var definition = await WorkflowDefinitions.GetQueryable(item => item.Id == workflowDefinitionId &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.EntityType)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_DOCUMENT_WORKFLOW_NOT_FOUND",
                "The shared workflow definition was not found in the current tenant.");
        if (!definition.IsActive ||
            definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published ||
            !(definition.EntityType.Code.Equals("PROCUREMENT_SOURCING", StringComparison.OrdinalIgnoreCase) ||
              definition.EntityType.Name.Replace(" ", string.Empty)
                  .Equals("ProcurementSourcing", StringComparison.OrdinalIgnoreCase)))
            throw Conflict("TENDER_DOCUMENT_WORKFLOW_INVALID",
                "The shared workflow definition must be an active Published Procurement Sourcing workflow.");
        return definition;
    }

    private async Task ValidateWorkflowDefinitionAsync(
        Guid workflowDefinitionId,
        CancellationToken cancellationToken) =>
        _ = await LoadWorkflowDefinitionAsync(workflowDefinitionId, cancellationToken);

    private async Task EnsureWorkflowOutcomeAsync(
        Guid? workflowInstanceId,
        Guid expectedWorkflowDefinitionId,
        Guid expectedEntityId,
        bool approving,
        CancellationToken cancellationToken)
    {
        if (!workflowInstanceId.HasValue)
            throw Conflict("TENDER_DOCUMENT_WORKFLOW_NOT_STARTED",
                "The configured shared workflow was not started.");
        var instance = await WorkflowInstanceRows.GetQueryable(item => item.Id == workflowInstanceId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("TENDER_DOCUMENT_WORKFLOW_INSTANCE_NOT_FOUND",
                "The linked shared workflow instance was not found in the current tenant.");
        if (instance.WorkflowDefinitionId != expectedWorkflowDefinitionId ||
            instance.EntityId != expectedEntityId)
            throw Conflict("TENDER_DOCUMENT_WORKFLOW_INSTANCE_MISMATCH",
                "The shared workflow instance does not belong to this exact controlled record and workflow definition.");
        if (approving && instance.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("TENDER_DOCUMENT_WORKFLOW_NOT_APPROVED",
                "Complete the configured shared workflow before approval.");
        if (!approving && instance.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw Conflict("TENDER_DOCUMENT_WORKFLOW_NOT_REJECTED",
                "Only a Cancelled or Failed shared workflow permits rejection.");
    }

    private async Task EnsureIndependentActorAsync(
        Guid? prohibitedActor,
        string sourceReference,
        string sourceType,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!prohibitedActor.HasValue || prohibitedActor.Value == Guid.Empty)
            throw Conflict("TENDER_DOCUMENT_INITIATOR_NOT_RECORDED",
                "The initiating actor was not recorded.");
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = sourceType,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = [prohibitedActor.Value]
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementTenderDocumentControlAuthorizationException(decision.Message);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string sourceType,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementTenderDocumentControlAuthorizationException(
                "Supplier portal users cannot perform internal tender-document control actions.");
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = sourceType,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? "NEW" : sourceReference.Trim()
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementTenderDocumentControlAuthorizationException(decision.Message);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementTenderDocumentControlAuthorizationException(
                "Supplier portal users cannot access internal tender-document control administration.");
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read"))
            return;
        throw new ProcurementTenderDocumentControlAuthorizationException(
            "The procurement records read permission is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementTenderDocumentControlAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action();
                await _unitOfWork.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw Conflict("TENDER_DOCUMENT_VERSION_CONFLICT",
                    "The controlled record changed by another user. Reload before continuing.");
            }
            catch (DbUpdateException exception) when (
                exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
                exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw Conflict("TENDER_DOCUMENT_DUPLICATE",
                    "This controlled operation was already recorded.");
            }
            catch (ProcurementControlEventNotFoundException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw NotFound("TENDER_DOCUMENT_EVIDENCE_NOT_FOUND", exception.Message);
            }
            catch (ProcurementControlEventValidationException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw Validation(exception.Code, exception.Message);
            }
            catch (ProcurementControlEventConflictException exception)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw Conflict("TENDER_DOCUMENT_EVENT_CONFLICT", exception.Message);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private Task RecordTemplateEventAsync(
        ProcurementTenderDocumentTemplateVersion template,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "tender-document-template", template.TenantId, template.Id, $"{action}-{correlationId}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = template.PolicySetCode,
            RuleId = template.PolicySetId,
            RuleVersion = template.PolicySetVersion.ToString(),
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = TemplateSourceType,
            SourceId = template.Id,
            SourceReference = $"{template.TemplateCode}/v{template.Version}",
            Reason = TrimOrNull(reason, 1000),
            Before = before,
            After = after,
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = occurredAtUtc,
            Evidence = CopyEvidence(evidence)
        }, cancellationToken);

    private Task RecordRegisterEventAsync(
        ProcurementTenderDocumentRegister register,
        SourceContext source,
        string action,
        ProcurementControlEventResult result,
        object? input,
        object? output,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "tender-document-register", register.TenantId, register.Id, $"{action}-{correlationId}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = register.MethodRuleCode,
            RuleId = register.MethodRuleId,
            RuleVersion = register.PolicySetVersion.ToString(),
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = source.SourceType.ToString(),
            SourceId = source.SourceId,
            SourceReference = source.Reference,
            Reason = TrimOrNull(reason, 1000),
            InputValues = input,
            ResultValues = output,
            CorrelationId = correlationId,
            CausationId = correlationId,
            OccurredAtUtc = occurredAtUtc,
            Evidence = CopyEvidence(evidence)
        }, cancellationToken);

    private async Task PublishRecipientNotificationAsync(
        string topic,
        SourceContext source,
        Guid? businessPartnerId,
        string? recipientEmail,
        object values,
        CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(values, JsonOptions), JsonOptions) ?? new Dictionary<string, object>();
        if (businessPartnerId.HasValue) data["businessPartnerId"] = businessPartnerId.Value;
        if (!string.IsNullOrWhiteSpace(recipientEmail)) data["recipientEmail"] = recipientEmail;
        data["sourceType"] = source.SourceType.ToString();
        data["sourceId"] = source.SourceId;
        data["sourceReference"] = source.Reference;
        await _notificationTopics.PublishAsync(new NotificationTopicEvent
        {
            TenantId = _currentUser.TenantId,
            TopicKey = topic,
            NotificationType = "ProcurementTenderDocumentControl",
            EntityType = source.SourceType.ToString(),
            EntityId = source.SourceId,
            TriggeredByUserId = _currentUser.UserId,
            Data = data
        }, cancellationToken);
    }

    private static List<ProcurementControlEventEvidenceReference> CopyEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence) =>
        evidence.Select(item => new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = item.ReferenceKind,
            ReferenceId = item.ReferenceId,
            Reference = item.Reference,
            Label = item.Label,
            RequirementKey = item.RequirementKey
        }).ToList();

    private static List<ProcurementControlEventEvidenceReference> Evidence(
        string externalReference,
        Guid? workflowDocumentId,
        Guid? fileUploadRecordId,
        string label,
        string requirement)
    {
        var result = new List<ProcurementControlEventEvidenceReference>();
        if (!string.IsNullOrWhiteSpace(externalReference))
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = externalReference.Trim(),
                Label = label,
                RequirementKey = requirement
            });
        if (workflowDocumentId.HasValue)
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument,
                ReferenceId = workflowDocumentId,
                Label = label,
                RequirementKey = requirement
            });
        if (fileUploadRecordId.HasValue)
            result.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                ReferenceId = fileUploadRecordId,
                Label = label,
                RequirementKey = requirement
            });
        return result;
    }

    private static List<ProcurementControlEventEvidenceReference> ContentEvidence(
        ProcurementTenderDocumentTemplateVersion template) =>
        Evidence(template.ContentReference, template.ContentWorkflowEvidenceDocumentId,
            template.ContentFileUploadRecordId, "Controlled tender-document content", "DEC-003");

    private static string? EvidenceLabel(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence) =>
        evidence.Select(item => item.Reference ?? item.Label ?? item.ReferenceId?.ToString())
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));

    private static void EnsureEventEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0)
            throw Validation("TENDER_DOCUMENT_EVIDENCE_REQUIRED",
                "At least one shared evidence or external reference is required.");
    }

    private async Task EnsureTemplateVersionUniqueAsync(
        ProcurementTenderDocumentTemplateVersion template,
        CancellationToken cancellationToken)
    {
        var exists = await Templates.GetQueryableIncludingDeleted(item =>
            item.TenantId == template.TenantId && item.Id != template.Id &&
            (item.TemplateKey == template.TemplateKey && item.Version == template.Version ||
             item.TemplateCode == template.TemplateCode && item.Version == template.Version))
            .AnyAsync(cancellationToken);
        if (exists)
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_VERSION_DUPLICATE",
                "Template code and family version must be unique, including deleted Draft revisions.");
    }

    private static void ValidateTemplateRequest(
        SaveProcurementTenderDocumentTemplateRequest request,
        bool requireContent)
    {
        Require(request.TemplateCode, "TENDER_DOCUMENT_TEMPLATE_CODE_REQUIRED", "TemplateCode is required.");
        Require(request.Name, "TENDER_DOCUMENT_TEMPLATE_NAME_REQUIRED", "Name is required.");
        Require(request.DocumentTypeCode, "TENDER_DOCUMENT_TYPE_REQUIRED", "DocumentTypeCode is required.");
        var hasContent = HasTemplateContent(request);
        if (requireContent && !hasContent)
            throw Validation("TENDER_DOCUMENT_CONTENT_ARTIFACT_REQUIRED",
                "A controlled template must reference exactly one workflow evidence document.");
        if (hasContent)
        {
            Require(request.ContentReference, "TENDER_DOCUMENT_CONTENT_REQUIRED", "ContentReference is required.");
            Require(request.ContentChecksumSha256, "TENDER_DOCUMENT_CHECKSUM_REQUIRED",
                "ContentChecksumSha256 is required.");
            if (request.ContentChecksumSha256.Length != 64 ||
                request.ContentChecksumSha256.Any(character => !Uri.IsHexDigit(character)))
                throw Validation("TENDER_DOCUMENT_CHECKSUM_INVALID",
                    "ContentChecksumSha256 must be exactly 64 hexadecimal characters.");
            if (!request.ContentWorkflowEvidenceDocumentId.HasValue || request.ContentFileUploadRecordId.HasValue)
                throw Validation("TENDER_DOCUMENT_CONTENT_ARTIFACT_REQUIRED",
                    "A controlled template must reference exactly one workflow evidence document.");
        }
        if (request.ApplicableMethods.Count == 0 || request.ApplicableMethods.Any(method => !Enum.IsDefined(method)))
            throw Validation("TENDER_DOCUMENT_TEMPLATE_METHOD_REQUIRED",
                "At least one valid applicable procurement method is required.");
        ValidatePeriod(request.EffectiveFromUtc, request.EffectiveToUtc);
    }

    private static bool HasTemplateContent(SaveProcurementTenderDocumentTemplateRequest request)
    {
        var any = request.ContentWorkflowEvidenceDocumentId.HasValue ||
            request.ContentFileUploadRecordId.HasValue ||
            !string.IsNullOrWhiteSpace(request.ContentReference) ||
            !string.IsNullOrWhiteSpace(request.ContentChecksumSha256);
        var none = !request.ContentWorkflowEvidenceDocumentId.HasValue &&
            !request.ContentFileUploadRecordId.HasValue &&
            string.IsNullOrWhiteSpace(request.ContentReference) &&
            string.IsNullOrWhiteSpace(request.ContentChecksumSha256);
        if (any && !none &&
            (!request.ContentWorkflowEvidenceDocumentId.HasValue ||
             request.ContentFileUploadRecordId.HasValue ||
             string.IsNullOrWhiteSpace(request.ContentReference) ||
             string.IsNullOrWhiteSpace(request.ContentChecksumSha256)))
            throw Validation("TENDER_DOCUMENT_CONTENT_ARTIFACT_INCOMPLETE",
                "Content ID, controlled path, and SHA-256 must be supplied together.");
        return any;
    }

    private static void ValidatePeriod(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        if (effectiveFromUtc == default)
            throw Validation("TENDER_DOCUMENT_EFFECTIVE_FROM_REQUIRED", "EffectiveFromUtc is required.");
        if (effectiveToUtc.HasValue && EnsureUtc(effectiveToUtc.Value) <= EnsureUtc(effectiveFromUtc))
            throw Validation("TENDER_DOCUMENT_EFFECTIVE_PERIOD_INVALID",
                "EffectiveToUtc must be later than EffectiveFromUtc.");
    }

    private static void ValidateFee(ProcurementTenderDocumentFeeMode feeMode, decimal feeAmount)
    {
        if (!Enum.IsDefined(feeMode))
            throw Validation("TENDER_DOCUMENT_FEE_MODE_INVALID", "FeeMode must be Free or Paid.");
        if (feeMode == ProcurementTenderDocumentFeeMode.Free && feeAmount != 0m)
            throw Validation("TENDER_DOCUMENT_FREE_FEE_INVALID", "A Free tender document must have a zero fee.");
        if (feeMode == ProcurementTenderDocumentFeeMode.Paid && feeAmount <= 0m)
            throw Validation("TENDER_DOCUMENT_PAID_FEE_INVALID", "A Paid tender document must have a positive fee.");
    }

    private static void ValidateSourceKey(ProcurementTenderDocumentSourceType sourceType, Guid sourceId)
    {
        if (!Enum.IsDefined(sourceType))
            throw Validation("TENDER_DOCUMENT_SOURCE_TYPE_INVALID", "SourceType must be Tender or RequestForQuotation.");
        if (sourceId == Guid.Empty)
            throw Validation("TENDER_DOCUMENT_SOURCE_ID_REQUIRED", "SourceId is required.");
    }

    private static void EnsureTemplateStatus(
        ProcurementTenderDocumentTemplateVersion template,
        ProcurementTenderDocumentTemplateStatus expected,
        string message)
    {
        if (template.Status != expected)
            throw Conflict("TENDER_DOCUMENT_TEMPLATE_STATUS_INVALID", message);
    }

    private static void EnsureRowVersion(byte[] current, string? supplied, string prefix)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw Validation($"{prefix}_ROW_VERSION_REQUIRED", "RowVersion is required.");
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw Validation($"{prefix}_ROW_VERSION_INVALID", "RowVersion must be valid Base64.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict($"{prefix}_VERSION_CONFLICT",
                "The controlled record changed by another user. Reload before continuing.");
    }

    private static ProcurementTenderDocumentTemplateListItemDto MapTemplateList(
        ProcurementTenderDocumentTemplateVersion item)
    {
        var now = DateTime.UtcNow;
        return new ProcurementTenderDocumentTemplateListItemDto
        {
            Id = item.Id,
            TemplateKey = item.TemplateKey,
            TemplateCode = item.TemplateCode,
            Name = item.Name,
            DocumentTypeCode = item.DocumentTypeCode,
            Version = item.Version,
            Status = item.Status,
            EffectiveFromUtc = item.EffectiveFromUtc,
            EffectiveToUtc = item.EffectiveToUtc,
            IsEffective = item.Status == ProcurementTenderDocumentTemplateStatus.Published &&
                item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now),
            PolicySetCode = item.PolicySetCode,
            PolicySetVersion = item.PolicySetVersion,
            ApplicableMethods = item.ApplicableMethods.Where(method => !method.IsDeleted)
                .Select(method => method.Method).Distinct().OrderBy(method => method).ToList(),
            AllowedActions = TemplateActions(item.Status),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static ProcurementTenderDocumentTemplateDto MapTemplate(
        ProcurementTenderDocumentTemplateVersion item)
    {
        var list = MapTemplateList(item);
        var blocked = new List<string>();
        if (item.ApplicableMethods.All(method => method.IsDeleted))
            blocked.Add("At least one applicable procurement method is required.");
        if (item.Version > 1 && string.IsNullOrWhiteSpace(item.ChangeSummary))
            blocked.Add("A replacement version requires a change summary.");
        if (!item.ContentWorkflowEvidenceDocumentId.HasValue)
            blocked.Add(item.Status == ProcurementTenderDocumentTemplateStatus.PendingApproval
                ? "Upload and attach one scan-clean content document from this exact approval workflow; the configured policy determines whether separate verification is required."
                : "Controlled content must be attached before publication.");
        return new ProcurementTenderDocumentTemplateDto
        {
            Id = list.Id,
            TemplateKey = list.TemplateKey,
            TemplateCode = list.TemplateCode,
            Name = list.Name,
            Description = item.Description,
            DocumentTypeCode = list.DocumentTypeCode,
            Version = list.Version,
            Status = list.Status,
            EffectiveFromUtc = list.EffectiveFromUtc,
            EffectiveToUtc = list.EffectiveToUtc,
            IsEffective = list.IsEffective,
            PolicySetId = item.PolicySetId,
            PolicySetCode = list.PolicySetCode,
            PolicySetVersion = list.PolicySetVersion,
            SourceConfigurationProfileId = item.SourceConfigurationProfileId,
            ContentReference = item.ContentReference,
            ContentWorkflowEvidenceDocumentId = item.ContentWorkflowEvidenceDocumentId,
            ContentFileUploadRecordId = item.ContentFileUploadRecordId,
            ContentChecksumSha256 = item.ContentChecksumSha256,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SupersedesVersionId = item.SupersedesVersionId,
            ChangeSummary = item.ChangeSummary,
            ApprovalEvidenceReference = item.ApprovalEvidenceReference,
            ReviewComment = item.ReviewComment,
            SubmittedAtUtc = item.SubmittedAtUtc,
            SubmittedById = item.SubmittedById,
            SubmittedByName = item.SubmittedByName,
            PublishedAtUtc = item.PublishedAtUtc,
            PublishedById = item.PublishedById,
            PublishedByName = item.PublishedByName,
            RetiredAtUtc = item.RetiredAtUtc,
            RetiredById = item.RetiredById,
            RetiredByName = item.RetiredByName,
            IntegrityHash = item.IntegrityHash,
            ApplicableMethods = list.ApplicableMethods,
            AllowedActions = list.AllowedActions,
            BlockedReasons = blocked,
            RowVersion = list.RowVersion
        };
    }

    private ProcurementTenderDocumentRegisterReadinessDto MapReadiness(
        ProcurementTenderDocumentRegister register,
        SourceContext source,
        IReadOnlySet<Guid>? externalPartnerIds)
    {
        var state = BuildEffectiveState(register,
            externalPartnerIds is { Count: 1 } ? externalPartnerIds.First() : null);
        var issues = register.Issuances.Where(item => !item.IsDeleted &&
            (externalPartnerIds is null ||
             item.BusinessPartnerId.HasValue && externalPartnerIds.Contains(item.BusinessPartnerId.Value))).ToList();
        var recipients = register.Changes.Where(item => !item.IsDeleted &&
                item.Status == ProcurementTenderDocumentChangeStatus.Approved &&
                item.RequiresAcknowledgement)
            .SelectMany(item => item.Recipients)
            .Where(item => !item.IsDeleted &&
                (externalPartnerIds is null ||
                 item.BusinessPartnerId.HasValue && externalPartnerIds.Contains(item.BusinessPartnerId.Value)))
            .ToList();
        return new ProcurementTenderDocumentRegisterReadinessDto
        {
            SourceType = register.SourceType,
            SourceId = source.SourceId,
            SourceReference = source.Reference,
            SourcingCaseId = register.SourcingCaseId,
            Method = register.Method,
            MethodRuleId = register.MethodRuleId,
            MethodRuleCode = register.MethodRuleCode,
            HasRegister = true,
            RegisterId = register.Id,
            EffectiveTemplateVersionId = state.EffectiveTemplateVersionId,
            EffectiveTemplateReference = state.EffectiveTemplateReference,
            EffectiveSubmissionDeadlineUtc = state.EffectiveSubmissionDeadlineUtc,
            EffectiveBidValidityUntilUtc = state.EffectiveBidValidityUntilUtc,
            FeeMode = register.FeeMode,
            FeeAmount = register.FeeAmount,
            CurrencyCode = register.CurrencyCode,
            IssuanceCount = issues.Count,
            PendingAcknowledgementCount = recipients.Count(item =>
                !item.Acknowledgements.Any(ack => !ack.IsDeleted &&
                    ack.Outcome == ProcurementTenderDocumentAcknowledgementOutcome.Acknowledged)),
            PendingChangeCount = register.Changes.Count(item => !item.IsDeleted &&
                item.Status == ProcurementTenderDocumentChangeStatus.PendingApproval),
            Ready = state.Ready,
            BlockedReasons = state.BlockedReasons,
            AllowedActions = externalPartnerIds is null
                ? ["View", "Issue", "CreateChange"]
                : recipients.Any(item => !item.Acknowledgements.Any(ack => !ack.IsDeleted &&
                    ack.Outcome == ProcurementTenderDocumentAcknowledgementOutcome.Acknowledged))
                    ? ["View", "Acknowledge"]
                    : ["View"],
            RowVersion = Convert.ToBase64String(register.RowVersion)
        };
    }

    private ProcurementTenderDocumentRegisterDto MapRegister(
        ProcurementTenderDocumentRegister register,
        SourceContext source,
        IReadOnlySet<Guid>? externalPartnerIds)
    {
        var state = BuildEffectiveState(register,
            externalPartnerIds is { Count: 1 } ? externalPartnerIds.First() : null);
        var visibleChanges = register.Changes.Where(item => !item.IsDeleted &&
            (externalPartnerIds is null ||
             item.Status == ProcurementTenderDocumentChangeStatus.Approved &&
             item.Recipients.Any(recipient => !recipient.IsDeleted &&
                 recipient.BusinessPartnerId.HasValue &&
                 externalPartnerIds.Contains(recipient.BusinessPartnerId.Value)))).ToList();
        var outstandingAcknowledgement = externalPartnerIds is not null && visibleChanges
            .Where(item => item.RequiresAcknowledgement)
            .SelectMany(item => item.Recipients)
            .Where(item => !item.IsDeleted && item.BusinessPartnerId.HasValue &&
                externalPartnerIds.Contains(item.BusinessPartnerId.Value))
            .Any(item => !item.Acknowledgements.Any(ack => !ack.IsDeleted &&
                ack.Outcome == ProcurementTenderDocumentAcknowledgementOutcome.Acknowledged));
        return new ProcurementTenderDocumentRegisterDto
        {
            Id = register.Id,
            SourceType = register.SourceType,
            SourceId = source.SourceId,
            TenderId = register.TenderId,
            RequestForQuotationId = register.RequestForQuotationId,
            SourceReference = source.Reference,
            SourcingCaseId = register.SourcingCaseId,
            MethodRuleId = register.MethodRuleId,
            Method = register.Method,
            MethodRuleCode = register.MethodRuleCode,
            PolicySetId = register.PolicySetId,
            PolicySetCode = register.PolicySetCode,
            PolicySetVersion = register.PolicySetVersion,
            SourceConfigurationProfileId = register.SourceConfigurationProfileId,
            InitialTemplateVersionId = register.InitialTemplateVersionId,
            EffectiveTemplateVersionId = state.EffectiveTemplateVersionId,
            EffectiveTemplateReference = state.EffectiveTemplateReference,
            OriginalSubmissionDeadlineUtc = register.OriginalSubmissionDeadlineUtc,
            EffectiveSubmissionDeadlineUtc = state.EffectiveSubmissionDeadlineUtc,
            OpeningScheduledAtUtc = register.OpeningScheduledAtUtc,
            OriginalBidValidityUntilUtc = register.OriginalBidValidityUntilUtc,
            EffectiveBidValidityUntilUtc = state.EffectiveBidValidityUntilUtc,
            FeeMode = register.FeeMode,
            FeeAmount = register.FeeAmount,
            CurrencyCode = register.CurrencyCode,
            BoundAtUtc = register.BoundAtUtc,
            BoundByUserId = register.BoundByUserId,
            CorrelationId = register.CorrelationId,
            IntegrityHash = register.IntegrityHash,
            Issuances = register.Issuances.Where(item => !item.IsDeleted &&
                    (externalPartnerIds is null ||
                     item.BusinessPartnerId.HasValue && externalPartnerIds.Contains(item.BusinessPartnerId.Value)))
                .OrderBy(item => item.IssuedAtUtc).Select(MapIssuance).ToList(),
            Changes = visibleChanges.OrderBy(item => item.Sequence)
                .Select(item => MapChange(item, externalPartnerIds)).ToList(),
            BlockedReasons = state.BlockedReasons,
            AllowedActions = externalPartnerIds is null
                ? ["Issue", "CreateChange"]
                : outstandingAcknowledgement ? ["Acknowledge"] : [],
            RowVersion = Convert.ToBase64String(register.RowVersion)
        };
    }

    private static ProcurementTenderDocumentIssuanceDto MapIssuance(
        ProcurementTenderDocumentIssuance item) => new()
    {
        Id = item.Id,
        RegisterId = item.RegisterId,
        TemplateVersionId = item.TemplateVersionId,
        TemplateReference = item.TemplateVersion is null
            ? item.TemplateVersionId.ToString()
            : $"{item.TemplateVersion.TemplateCode}/v{item.TemplateVersion.Version}",
        BusinessPartnerId = item.BusinessPartnerId,
        RecipientKey = item.RecipientKey,
        RecipientName = item.RecipientName,
        RecipientEmail = item.RecipientEmail,
        RecipientPhone = item.RecipientPhone,
        FeeMode = item.FeeMode,
        FeeAmount = item.FeeAmount,
        CurrencyCode = item.CurrencyCode,
        AmountPaid = item.AmountPaid,
        PaymentReference = item.PaymentReference,
        ReceiptNumber = item.ReceiptNumber,
        IssueChannel = item.IssueChannel,
        IssuedAtUtc = item.IssuedAtUtc,
        IssuedByUserId = item.IssuedByUserId,
        EvidenceReference = item.EvidenceReference,
        CorrelationId = item.CorrelationId,
        IntegrityHash = item.IntegrityHash,
        Acknowledgement = item.Acknowledgements.Where(ack => !ack.IsDeleted)
            .Select(MapAcknowledgement).FirstOrDefault()
    };

    private static ProcurementTenderDocumentChangeDto MapChange(
        ProcurementTenderDocumentChange item,
        IReadOnlySet<Guid>? externalPartnerIds = null)
    {
        var recipients = item.Recipients.Where(recipient => !recipient.IsDeleted &&
            (externalPartnerIds is null ||
             recipient.BusinessPartnerId.HasValue &&
             externalPartnerIds.Contains(recipient.BusinessPartnerId.Value))).ToList();
        return new ProcurementTenderDocumentChangeDto
        {
            Id = item.Id,
            RegisterId = item.RegisterId,
            Sequence = item.Sequence,
            ChangeType = item.ChangeType,
            Status = item.Status,
            PreviousTemplateVersionId = item.PreviousTemplateVersionId,
            NewTemplateVersionId = item.NewTemplateVersionId,
            PreviousValueUtc = item.PreviousValueUtc,
            NewValueUtc = item.NewValueUtc,
            RequiresAcknowledgement = item.RequiresAcknowledgement,
            Reason = item.Reason,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            WorkflowOutcome = item.WorkflowOutcome,
            ApprovalReference = item.ApprovalReference,
            EvidenceReference = item.EvidenceReference,
            RequestedAtUtc = item.RequestedAtUtc,
            RequestedByUserId = item.RequestedByUserId,
            DecidedAtUtc = item.DecidedAtUtc,
            DecidedByUserId = item.DecidedByUserId,
            DispatchedAtUtc = item.DispatchedAtUtc,
            DispatchedByUserId = item.DispatchedByUserId,
            DispatchEvidenceReference = item.DispatchEvidenceReference,
            CorrelationId = item.CorrelationId,
            IntegrityHash = item.IntegrityHash,
            Recipients = recipients.Select(MapRecipient).ToList(),
            AllowedActions = item.Status == ProcurementTenderDocumentChangeStatus.PendingApproval
                ? ["Approve", "Reject"]
                : [],
            BlockedReasons = item.Status == ProcurementTenderDocumentChangeStatus.PendingApproval
                ? ["The configured shared workflow must reach its exact approval or rejection outcome."]
                : [],
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static ProcurementTenderDocumentChangeRecipientDto MapRecipient(
        ProcurementTenderDocumentChangeRecipient item) => new()
    {
        Id = item.Id,
        SourceType = item.SourceType,
        IssuanceId = item.IssuanceId,
        TenderBidId = item.TenderBidId,
        RequestForQuotationQuoteId = item.RequestForQuotationQuoteId,
        BusinessPartnerId = item.BusinessPartnerId,
        RecipientKey = item.RecipientKey,
        RecipientName = item.RecipientName,
        RecipientEmail = item.RecipientEmail,
        RecipientPhone = item.RecipientPhone,
        DispatchChannel = item.DispatchChannel,
        DispatchReference = item.DispatchReference,
        DispatchedAtUtc = item.DispatchedAtUtc,
        DispatchEvidenceReference = item.DispatchEvidenceReference,
        IntegrityHash = item.IntegrityHash,
        Acknowledgement = item.Acknowledgements.Where(ack => !ack.IsDeleted)
            .Select(MapAcknowledgement).FirstOrDefault()
    };

    private static ProcurementTenderDocumentAcknowledgementDto MapAcknowledgement(
        ProcurementTenderDocumentAcknowledgement item) => new()
    {
        Id = item.Id,
        IssuanceId = item.IssuanceId,
        ChangeRecipientId = item.ChangeRecipientId,
        BusinessPartnerId = item.BusinessPartnerId,
        Outcome = item.Outcome,
        AcknowledgedAtUtc = item.AcknowledgedAtUtc,
        AcknowledgedByUserId = item.AcknowledgedByUserId,
        AcknowledgementChannel = item.AcknowledgementChannel,
        AcknowledgementReference = item.AcknowledgementReference,
        EvidenceReference = item.EvidenceReference,
        CorrelationId = item.CorrelationId,
        IntegrityHash = item.IntegrityHash
    };

    private static List<string> TemplateActions(ProcurementTenderDocumentTemplateStatus status) =>
        status switch
        {
            ProcurementTenderDocumentTemplateStatus.Draft => ["Edit", "Submit", "DeleteDraft"],
            ProcurementTenderDocumentTemplateStatus.PendingApproval => ["AttachContent", "Publish", "Reject"],
            ProcurementTenderDocumentTemplateStatus.Published => ["Clone", "Retire"],
            ProcurementTenderDocumentTemplateStatus.Retired => ["Clone"],
            _ => []
        };

    private static void ApplyTemplateRequest(
        ProcurementTenderDocumentTemplateVersion entity,
        SaveProcurementTenderDocumentTemplateRequest request,
        ProcurementPolicySet policy)
    {
        entity.TemplateCode = NormalizeCode(request.TemplateCode);
        entity.Name = request.Name.Trim();
        entity.Description = TrimOrNull(request.Description, 1000);
        entity.DocumentTypeCode = NormalizeCode(request.DocumentTypeCode);
        entity.EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc);
        entity.EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null;
        entity.PolicySetId = policy.Id;
        entity.PolicySetCode = policy.Code;
        entity.PolicySetVersion = policy.Version;
        entity.SourceConfigurationProfileId = policy.SourceConfigurationProfileId;
        entity.ContentReference = request.ContentReference?.Trim() ?? string.Empty;
        entity.ContentWorkflowEvidenceDocumentId = request.ContentWorkflowEvidenceDocumentId;
        entity.ContentFileUploadRecordId = request.ContentFileUploadRecordId;
        entity.ContentChecksumSha256 = request.ContentChecksumSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        entity.WorkflowDefinitionId = request.WorkflowDefinitionId;
        entity.ChangeSummary = TrimOrNull(request.ChangeSummary, 1000);
    }

    private void AddTemplateMethods(
        ProcurementTenderDocumentTemplateVersion template,
        IEnumerable<ProcurementMethodType> methods,
        DateTime now)
    {
        foreach (var method in methods.Distinct())
        {
            var row = new ProcurementTenderDocumentTemplateMethod
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                TemplateVersionId = template.Id,
                Method = method,
                CreatedAt = now,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId,
                TemplateVersion = template
            };
            row.IntegrityHash = ComputeHash(JsonSerializer.Serialize(new
            {
                row.TemplateVersionId,
                row.Method,
                row.TenantId
            }, JsonOptions));
            template.ApplicableMethods.Add(row);
        }
    }

    private static void Capture(ProcurementTenderDocumentTemplateVersion entity)
    {
        entity.LifecycleSnapshotJson = JsonSerializer.Serialize(TemplateSnapshot(entity), JsonOptions);
        entity.IntegrityHash = ComputeHash(entity.LifecycleSnapshotJson);
    }

    private static void Capture(ProcurementTenderDocumentRegister entity)
    {
        entity.LifecycleSnapshotJson = JsonSerializer.Serialize(RegisterSnapshot(entity), JsonOptions);
        entity.IntegrityHash = ComputeHash(entity.LifecycleSnapshotJson);
    }

    private static void Capture(
        ProcurementTenderDocumentIssuance entity,
        ProcurementTenderDocumentRegister register,
        ProcurementTenderDocumentTemplateVersion template)
    {
        entity.IssuanceSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.tender-document-issuance.v1",
            entity.Id,
            entity.RegisterId,
            entity.TemplateVersionId,
            templateReference = $"{template.TemplateCode}/v{template.Version}",
            entity.BusinessPartnerId,
            entity.RecipientKey,
            entity.RecipientName,
            entity.RecipientEmail,
            entity.RecipientPhone,
            entity.FeeMode,
            entity.FeeAmount,
            entity.CurrencyCode,
            entity.AmountPaid,
            entity.PaymentReference,
            entity.ReceiptNumber,
            entity.IssueChannel,
            entity.IssuedAtUtc,
            entity.IssuedByUserId,
            entity.EvidenceReference,
            entity.EvidenceWorkflowDocumentId,
            entity.EvidenceFileUploadRecordId,
            entity.CorrelationId,
            register.PolicySetId,
            register.PolicySetVersion,
            register.MethodRuleId
        }, JsonOptions);
        entity.IntegrityHash = ComputeHash(entity.IssuanceSnapshotJson);
    }

    private static void Capture(ProcurementTenderDocumentChange entity)
    {
        entity.LifecycleSnapshotJson = JsonSerializer.Serialize(ChangeSnapshot(entity), JsonOptions);
        entity.IntegrityHash = ComputeHash(entity.LifecycleSnapshotJson);
    }

    private static void Capture(ProcurementTenderDocumentChangeRecipient entity)
    {
        entity.DispatchSnapshotJson = JsonSerializer.Serialize(new
        {
            schemaVersion = "tdc.tender-document-change-recipient.v1",
            entity.Id,
            entity.ChangeId,
            entity.SourceType,
            entity.IssuanceId,
            entity.TenderBidId,
            entity.RequestForQuotationQuoteId,
            entity.BusinessPartnerId,
            entity.RecipientKey,
            entity.RecipientName,
            entity.RecipientEmail,
            entity.RecipientPhone,
            entity.DispatchChannel,
            entity.DispatchReference,
            entity.DispatchedAtUtc,
            entity.DispatchedByUserId,
            entity.DispatchEvidenceReference
        }, JsonOptions);
        entity.IntegrityHash = ComputeHash(entity.DispatchSnapshotJson);
    }

    private static void Capture(ProcurementTenderDocumentAcknowledgement entity)
    {
        entity.AcknowledgementSnapshotJson = JsonSerializer.Serialize(AcknowledgementSnapshot(entity), JsonOptions);
        entity.IntegrityHash = ComputeHash(entity.AcknowledgementSnapshotJson);
    }

    private static object TemplateSnapshot(ProcurementTenderDocumentTemplateVersion entity) => new
    {
        schemaVersion = "tdc.tender-document-template.v1",
        entity.Id,
        entity.TemplateKey,
        entity.TemplateCode,
        entity.Name,
        entity.Description,
        entity.DocumentTypeCode,
        entity.Version,
        entity.Status,
        entity.EffectiveFromUtc,
        entity.EffectiveToUtc,
        entity.PolicySetId,
        entity.PolicySetCode,
        entity.PolicySetVersion,
        entity.SourceConfigurationProfileId,
        entity.ContentReference,
        entity.ContentWorkflowEvidenceDocumentId,
        entity.ContentFileUploadRecordId,
        entity.ContentChecksumSha256,
        entity.WorkflowDefinitionId,
        entity.WorkflowInstanceId,
        entity.SupersedesVersionId,
        entity.ChangeSummary,
        entity.ApprovalEvidenceReference,
        entity.ReviewComment,
        entity.SubmittedAtUtc,
        entity.SubmittedById,
        entity.PublishedAtUtc,
        entity.PublishedById,
        entity.RetiredAtUtc,
        entity.RetiredById,
        methods = entity.ApplicableMethods.Where(item => !item.IsDeleted)
            .Select(item => item.Method).Distinct().OrderBy(item => item).ToArray(),
        entity.IsDeleted
    };

    private static object RegisterSnapshot(ProcurementTenderDocumentRegister entity) => new
    {
        schemaVersion = "tdc.tender-document-register.v1",
        entity.Id,
        entity.SourceType,
        entity.TenderId,
        entity.RequestForQuotationId,
        entity.SourcingCaseId,
        entity.MethodRuleId,
        entity.Method,
        entity.MethodRuleCode,
        entity.PolicySetId,
        entity.PolicySetCode,
        entity.PolicySetVersion,
        entity.SourceConfigurationProfileId,
        entity.InitialTemplateVersionId,
        entity.OriginalSubmissionDeadlineUtc,
        entity.OpeningScheduledAtUtc,
        entity.OriginalBidValidityUntilUtc,
        entity.FeeMode,
        entity.FeeAmount,
        entity.CurrencyCode,
        entity.BoundAtUtc,
        entity.BoundByUserId,
        entity.CorrelationId
    };

    private static object IssuanceSnapshot(ProcurementTenderDocumentIssuance entity) => new
    {
        entity.Id,
        entity.RegisterId,
        entity.TemplateVersionId,
        entity.BusinessPartnerId,
        entity.RecipientKey,
        entity.RecipientName,
        entity.AmountPaid,
        entity.PaymentReference,
        entity.ReceiptNumber,
        entity.IssuedAtUtc,
        entity.EvidenceReference,
        entity.CorrelationId,
        entity.IntegrityHash
    };

    private static object ChangeSnapshot(ProcurementTenderDocumentChange entity) => new
    {
        schemaVersion = "tdc.tender-document-change.v1",
        entity.Id,
        entity.RegisterId,
        entity.Sequence,
        entity.ChangeType,
        entity.Status,
        entity.PreviousTemplateVersionId,
        entity.NewTemplateVersionId,
        entity.PreviousValueUtc,
        entity.NewValueUtc,
        entity.RequiresAcknowledgement,
        entity.Reason,
        entity.WorkflowDefinitionId,
        entity.WorkflowInstanceId,
        entity.WorkflowOutcome,
        entity.ApprovalReference,
        entity.EvidenceReference,
        entity.EvidenceWorkflowDocumentId,
        entity.EvidenceFileUploadRecordId,
        entity.RequestedAtUtc,
        entity.RequestedByUserId,
        entity.DecidedAtUtc,
        entity.DecidedByUserId,
        entity.DispatchedAtUtc,
        entity.DispatchedByUserId,
        entity.DispatchEvidenceReference,
        entity.CorrelationId
    };

    private static object AcknowledgementSnapshot(ProcurementTenderDocumentAcknowledgement entity) => new
    {
        schemaVersion = "tdc.tender-document-acknowledgement.v1",
        entity.Id,
        entity.IssuanceId,
        entity.ChangeRecipientId,
        entity.BusinessPartnerId,
        entity.Outcome,
        entity.AcknowledgedAtUtc,
        entity.AcknowledgedByUserId,
        entity.AcknowledgementChannel,
        entity.AcknowledgementReference,
        entity.EvidenceReference,
        entity.CorrelationId
    };

    private void Touch(ProcurementTenderDocumentTemplateVersion entity, DateTime? now = null)
    {
        entity.UpdatedAt = now ?? DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.Username;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void Touch(ProcurementTenderDocumentChange entity, DateTime? now = null)
    {
        entity.UpdatedAt = now ?? DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.Username;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void Touch(BaseEntity entity, DateTime? now = null)
    {
        entity.UpdatedAt = now ?? DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.Username;
        entity.LastModifiedById = _currentUser.UserId;
    }

    private static string RecipientKey(Guid? businessPartnerId, string? email)
    {
        var raw = businessPartnerId.HasValue
            ? $"BP:{businessPartnerId.Value:N}"
            : $"EMAIL:{NormalizeEmail(email) ?? string.Empty}";
        return ComputeHash(raw);
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private string ActorName =>
        Truncate(string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName, 300);

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string NormalizeCurrency(string value)
    {
        var result = value.Trim().ToUpperInvariant();
        if (result.Length != 3)
            throw Validation("TENDER_DOCUMENT_CURRENCY_INVALID",
                "CurrencyCode must contain exactly three characters.");
        return result;
    }
    private static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static string NormalizeCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string? TrimOrNull(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), length);
    private static void Require(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message);
    }
    private static ProcurementTenderDocumentControlNotFoundException NotFound(string code, string message) =>
        new(code, message);
    private static ProcurementTenderDocumentControlConflictException Conflict(string code, string message) =>
        new(code, message);
    private static ProcurementTenderDocumentControlValidationException Validation(string code, string message) =>
        new(code, message);

    private sealed record SourceContext(
        ProcurementTenderDocumentSourceType SourceType,
        Guid SourceId,
        string Reference,
        DateTime? SubmissionDeadlineUtc,
        DateTime? OpeningScheduledAtUtc,
        string CurrencyCode,
        ProcurementSourcingCase SourcingCase,
        Tender? Tender,
        RequestForQuotation? Rfq);

    private sealed record RecipientCandidate(
        ProcurementTenderDocumentRecipientSourceType SourceType,
        Guid? IssuanceId,
        Guid? TenderBidId,
        Guid? RfqQuoteId,
        Guid? BusinessPartnerId,
        string RecipientKey,
        string Name,
        string? Email,
        string? Phone);
}
