using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Notifications;
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

public sealed class ProcurementSupplierEvidencePackService : IProcurementSupplierEvidencePackService
{
    private const string SourceType = "ProcurementSupplierEvidencePack";
    private const string RegistrationSourceType = "BusinessPartnerRegistration";
    private const string EventType = "ProcurementSupplierEvidencePackControl";
    private const string ManagePermission = "procurement.supplier.manage";
    private const string ReviewPermission = "procurement.supplier.review";
    private const string ApprovePermission = "procurement.supplier.approve";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementSupplierEvidencePackService> _logger;

    public ProcurementSupplierEvidencePackService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementSupplierEvidencePackService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSupplierEvidencePackVersion> Packs =>
        _unitOfWork.Repository<ProcurementSupplierEvidencePackVersion>();
    private IGenericRepository<ProcurementSupplierEvidenceRequirement> Requirements =>
        _unitOfWork.Repository<ProcurementSupplierEvidenceRequirement>();
    private IGenericRepository<ProcurementSupplierRegistrationEvidencePackBinding> Bindings =>
        _unitOfWork.Repository<ProcurementSupplierRegistrationEvidencePackBinding>();
    private IGenericRepository<BusinessPartnerRegistration> Registrations =>
        _unitOfWork.Repository<BusinessPartnerRegistration>();
    private IGenericRepository<ProcurementConfigurationProfile> ConfigurationProfiles =>
        _unitOfWork.Repository<ProcurementConfigurationProfile>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions =>
        _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstanceRows =>
        _unitOfWork.Repository<WorkflowInstance>();

    public async Task<ProcurementSupplierEvidencePackSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var query = PackQuery();
        var effective = query.Where(item =>
            item.Status == ProcurementSupplierEvidencePackStatus.Published &&
            item.EffectiveFromUtc <= now &&
            (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now));
        var byCategory = await effective.GroupBy(item => item.Category)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

        return new ProcurementSupplierEvidencePackSummaryDto
        {
            FamilyCount = await query.Select(item => item.PackKey).Distinct().CountAsync(cancellationToken),
            DraftCount = await query.CountAsync(item => item.Status == ProcurementSupplierEvidencePackStatus.Draft, cancellationToken),
            PendingApprovalCount = await query.CountAsync(item => item.Status == ProcurementSupplierEvidencePackStatus.PendingApproval, cancellationToken),
            PublishedCount = await query.CountAsync(item => item.Status == ProcurementSupplierEvidencePackStatus.Published, cancellationToken),
            EffectiveCount = await effective.CountAsync(cancellationToken),
            RetiredCount = await query.CountAsync(item => item.Status == ProcurementSupplierEvidencePackStatus.Retired, cancellationToken),
            EffectiveByCategory = Enum.GetValues<ProcurementSupplierRegistrationCategory>()
                .ToDictionary(category => category, category => byCategory.GetValueOrDefault(category))
        };
    }

    public async Task<ProcurementSupplierEvidencePackPageDto> SearchAsync(
        ProcurementSupplierEvidencePackSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = PackQuery().Include(item => item.Requirements).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.PackCode.Contains(search) || item.Name.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        }
        if (request.Category.HasValue)
            query = query.Where(item => item.Category == request.Category.Value);
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        if (request.EffectiveAtUtc.HasValue)
        {
            var at = EnsureUtc(request.EffectiveAtUtc.Value);
            query = query.Where(item =>
                item.Status == ProcurementSupplierEvidencePackStatus.Published &&
                item.EffectiveFromUtc <= at &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= at));
        }
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.Category).ThenBy(item => item.PackCode)
            .ThenByDescending(item => item.Version)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync(cancellationToken);
        return new ProcurementSupplierEvidencePackPageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = rows.Select(MapList).ToList()
        };
    }

    public async Task<ProcurementSupplierEvidencePackDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return Map(await LoadPackAsync(id, tracked: false, cancellationToken));
    }

    public async Task<IReadOnlyList<ProcurementSupplierEvidencePackOptionDto>> GetWorkflowOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .Include(item => item.Steps)
            .AsNoTracking().OrderBy(item => item.Name).ThenByDescending(item => item.Version)
            .Select(item => new ProcurementSupplierEvidencePackOptionDto
            {
                Id = item.Id,
                Code = item.DefinitionKey.ToString(),
                Name = item.Name,
                Version = item.Version,
                Steps = item.Steps.Where(step => !step.IsDeleted)
                    .OrderBy(step => step.Order)
                    .Select(step => new ProcurementSupplierEvidencePackWorkflowStepOptionDto
                    {
                        Order = step.Order,
                        Name = step.Name
                    }).ToList()
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementSupplierEvidencePackOptionDto>> GetConfigurationProfileOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        return await ConfigurationProfiles.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.EffectiveFrom <= now &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .AsNoTracking().OrderBy(item => item.ProfileCode).ThenByDescending(item => item.Version)
            .Select(item => new ProcurementSupplierEvidencePackOptionDto
            {
                Id = item.Id,
                Code = item.ProfileCode,
                Name = item.Name,
                Version = item.Version
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementSupplierEvidencePackDto> CreateAsync(
        SaveProcurementSupplierEvidencePackRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, request.PackCode, correlation, cancellationToken);
        var replay = await Packs.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.CreationCorrelationId == correlation)
            .Include(item => item.Requirements).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (replay is not null) return Map(replay);

        ValidateRequest(request, isReplacement: false);
        var profile = await ValidateProfileAsync(request.SourceConfigurationProfileId,
            request.EffectiveFromUtc, cancellationToken);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, request.Requirements,
            cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProcurementSupplierEvidencePackVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            PackKey = Guid.NewGuid(),
            Version = 1,
            Status = ProcurementSupplierEvidencePackStatus.Draft,
            CreationCorrelationId = correlation,
            LastOperation = "Created",
            LastOperationCorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Apply(entity, request, profile, workflow);
        ApplyRequirements(entity, request.Requirements, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await EnsureUniqueFamilyAsync(entity, cancellationToken);
            await Packs.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Created", ProcurementControlEventResult.Succeeded,
                null, Snapshot(entity), request.ChangeSummary, [], correlation, now, cancellationToken);
            return Map(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidencePackDto> UpdateAsync(
        Guid id,
        SaveProcurementSupplierEvidencePackRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadPackAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.PackCode, correlation, cancellationToken);
        if (IsReplay(entity, "Updated", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierEvidencePackStatus.Draft,
            "Only a Draft supplier evidence pack can be edited.");
        ValidateRequest(request, entity.Version > 1);
        var profile = await ValidateProfileAsync(request.SourceConfigurationProfileId,
            request.EffectiveFromUtc, cancellationToken);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, request.Requirements,
            cancellationToken);
        var before = Snapshot(entity);
        Apply(entity, request, profile, workflow);
        await SynchronizeRequirementsAsync(entity, request.Requirements, cancellationToken);
        var now = DateTime.UtcNow;
        Touch(entity, "Updated", correlation, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await EnsureUniqueFamilyAsync(entity, cancellationToken);
            await Packs.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Updated", ProcurementControlEventResult.Succeeded,
                before, Snapshot(entity), request.ChangeSummary, [], correlation, now, cancellationToken);
            return Map(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidencePackDto> SubmitAsync(
        Guid id,
        ProcurementSupplierEvidencePackLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadPackAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.PackCode, correlation, cancellationToken);
        if (IsReplay(entity, "Submitted", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierEvidencePackStatus.Draft,
            "Only a Draft supplier evidence pack can be submitted.");
        EnsureEvidence(request.Evidence);
        await ValidateForPublicationAsync(entity, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSupplierEvidencePackStatus.PendingApproval;
        entity.SubmittedAtUtc = now;
        entity.SubmittedById = _currentUser.UserId;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Submitted", correlation, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await Packs.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var workflow = await WorkflowDefinitions.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.Id == entity.WorkflowDefinitionId &&
                    !item.IsDeleted)
                .SingleAsync(cancellationToken);
            var instance = await _workflowInstances.StartWorkflowAsync(
                workflow.Id, workflow.EntityTypeId, entity.Id.ToString(), _currentUser.UserId,
                new
                {
                    entity.PackKey,
                    entity.PackCode,
                    entity.Category,
                    entity.Version,
                    entity.SourceConfigurationProfileId,
                    entity.EffectiveFromUtc
                }, cancellationToken);
            entity.WorkflowInstanceId = instance.Id;
            Touch(entity, "Submitted", correlation, now);
            Capture(entity);
            await Packs.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Submitted", ProcurementControlEventResult.Succeeded,
                before, Snapshot(entity), request.Comment, request.Evidence, correlation, now, cancellationToken);
            await PublishNotificationAsync("procurement.supplier-evidence-pack.submitted", entity, cancellationToken);
            return Map(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidencePackDto> PublishAsync(
        Guid id,
        ProcurementSupplierEvidencePackLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadPackAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.PackCode, correlation, cancellationToken);
        if (IsReplay(entity, "Published", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierEvidencePackStatus.PendingApproval,
            "Only a PendingApproval supplier evidence pack can be published.");
        EnsureEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, entity.PackCode, correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, approving: true, cancellationToken);
        await ValidateForPublicationAsync(entity, cancellationToken);
        await EnsureNoCompetingFamilyOverlapAsync(entity, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSupplierEvidencePackStatus.Published;
        entity.PublishedAtUtc = now;
        entity.PublishedById = _currentUser.UserId;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Published", correlation, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await Packs.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Published", ProcurementControlEventResult.Succeeded,
                before, Snapshot(entity), request.Comment, request.Evidence, correlation, now, cancellationToken);
            await PublishNotificationAsync("procurement.supplier-evidence-pack.published", entity, cancellationToken);
            return Map(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidencePackDto> RejectAsync(
        Guid id,
        ProcurementSupplierEvidencePackLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadPackAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ReviewPermission, entity.PackCode, correlation, cancellationToken);
        if (IsReplay(entity, "Rejected", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierEvidencePackStatus.PendingApproval,
            "Only a PendingApproval supplier evidence pack can be rejected.");
        EnsureEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, entity.PackCode, correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, approving: false, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSupplierEvidencePackStatus.Draft;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Rejected", correlation, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await Packs.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Rejected", ProcurementControlEventResult.Rejected,
                before, Snapshot(entity), request.Comment, request.Evidence, correlation, now, cancellationToken);
            await PublishNotificationAsync("procurement.supplier-evidence-pack.rejected", entity, cancellationToken);
            return Map(entity);
        }, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidencePackDto> CloneAsync(
        Guid id,
        CloneProcurementSupplierEvidencePackRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var source = await LoadPackAsync(id, tracked: false, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.PackCode, correlation, cancellationToken);
        var replay = await Packs.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.CreationCorrelationId == correlation)
            .Include(item => item.Requirements).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (replay is not null) return Map(replay);
        EnsureRowVersion(source.RowVersion, request.RowVersion);
        if (source.Status is ProcurementSupplierEvidencePackStatus.Draft or
            ProcurementSupplierEvidencePackStatus.PendingApproval)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_CLONE_STATUS_INVALID",
                "Only a Published or Retired supplier evidence pack can be cloned.");
        if (string.IsNullOrWhiteSpace(request.ChangeSummary))
            throw Validation("SUPPLIER_EVIDENCE_PACK_CHANGE_SUMMARY_REQUIRED",
                "A replacement version requires a change summary.");
        ValidateDates(request.EffectiveFromUtc, request.EffectiveToUtc);
        var nextVersion = await Packs.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.PackKey == source.PackKey)
            .MaxAsync(item => (int?)item.Version, cancellationToken) + 1 ?? 1;
        var now = DateTime.UtcNow;
        var clone = new ProcurementSupplierEvidencePackVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            PackKey = source.PackKey,
            PackCode = source.PackCode,
            Name = source.Name,
            Description = source.Description,
            Category = source.Category,
            Version = nextVersion,
            Status = ProcurementSupplierEvidencePackStatus.Draft,
            EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc),
            EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null,
            SourceConfigurationProfileId = source.SourceConfigurationProfileId,
            SourceConfigurationProfileCode = source.SourceConfigurationProfileCode,
            SourceConfigurationProfileVersion = source.SourceConfigurationProfileVersion,
            CreationCorrelationId = correlation,
            LastOperation = "Cloned",
            LastOperationCorrelationId = correlation,
            WorkflowDefinitionId = source.WorkflowDefinitionId,
            SupersedesVersionId = source.Id,
            ChangeSummary = request.ChangeSummary.Trim(),
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        ApplyRequirements(clone, source.Requirements.Where(item => !item.IsDeleted).Select(ToRequest).ToList(), now);
        Capture(clone);

        return await ExecuteAsync(async () =>
        {
            await EnsureUniqueFamilyAsync(clone, cancellationToken);
            await Packs.AddAsync(clone);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(clone, "Cloned", ProcurementControlEventResult.Succeeded,
                Snapshot(source), Snapshot(clone), request.ChangeSummary, [], correlation, now, cancellationToken);
            return Map(clone);
        }, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidencePackDto> RetireAsync(
        Guid id,
        ProcurementSupplierEvidencePackLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadPackAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.PackCode, correlation, cancellationToken);
        if (IsReplay(entity, "Retired", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierEvidencePackStatus.Published,
            "Only a Published supplier evidence pack can be retired.");
        EnsureEvidence(request.Evidence);
        var now = DateTime.UtcNow;
        if (entity.EffectiveFromUtc <= now &&
            (!entity.EffectiveToUtc.HasValue || entity.EffectiveToUtc.Value >= now))
        {
            var replacement = await PackQuery().AnyAsync(item =>
                item.Id != entity.Id && item.Category == entity.Category &&
                item.Status == ProcurementSupplierEvidencePackStatus.Published &&
                item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now),
                cancellationToken);
            if (!replacement)
                throw Conflict("SUPPLIER_EVIDENCE_PACK_LAST_EFFECTIVE",
                    "The current effective pack cannot be retired until another Published pack is effective.");
        }
        var before = Snapshot(entity);
        entity.Status = ProcurementSupplierEvidencePackStatus.Retired;
        entity.RetiredAtUtc = now;
        entity.RetiredById = _currentUser.UserId;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Retired", correlation, now);
        Capture(entity);

        return await ExecuteAsync(async () =>
        {
            await Packs.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Retired", ProcurementControlEventResult.Succeeded,
                before, Snapshot(entity), request.Comment, request.Evidence, correlation, now, cancellationToken);
            await PublishNotificationAsync("procurement.supplier-evidence-pack.retired", entity, cancellationToken);
            return Map(entity);
        }, cancellationToken);
    }

    public async Task DeleteDraftAsync(
        Guid id,
        ProcurementSupplierEvidencePackLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadPackAsync(id, tracked: true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.PackCode, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RowVersion);
        EnsureStatus(entity, ProcurementSupplierEvidencePackStatus.Draft,
            "Only a Draft supplier evidence pack can be deleted.");
        var now = DateTime.UtcNow;
        var before = Snapshot(entity);
        foreach (var requirement in entity.Requirements.Where(item => !item.IsDeleted))
            SoftDelete(requirement, now);
        SoftDelete(entity, now);
        Touch(entity, "Deleted", correlation, now);
        Capture(entity);
        await ExecuteAsync(async () =>
        {
            await Packs.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(entity, "Deleted", ProcurementControlEventResult.Succeeded,
                before, null, request.Comment, request.Evidence, correlation, now, cancellationToken);
        }, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidenceReadinessDto> GetRegistrationReadinessAsync(
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        var registration = await LoadRegistrationAsync(registrationId, cancellationToken);
        await EnsureRegistrationReaderAsync(registration, cancellationToken);
        return await EvaluateRegistrationAsync(registration, cancellationToken);
    }

    public async Task<ProcurementSupplierEvidenceReadinessDto> BindAndValidateRegistrationAsync(
        Guid registrationId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (actorUserId == Guid.Empty || actorUserId != _currentUser.UserId)
            throw new ProcurementSupplierEvidencePackAuthorizationException(
                "The authenticated actor must submit the supplier registration.");
        var correlation = NormalizeCorrelation(correlationId);
        var registration = await LoadRegistrationAsync(registrationId, cancellationToken);
        await EnsureRegistrationReaderAsync(registration, cancellationToken);
        var readiness = await EvaluateRegistrationAsync(registration, cancellationToken);
        if (!readiness.IsReady)
            throw Validation("SUPPLIER_REGISTRATION_EVIDENCE_INCOMPLETE",
                $"Supplier evidence is incomplete: {string.Join("; ", readiness.BlockingReasons)}");
        if (registration.EvidencePackBinding is not null) return readiness;

        var pack = await ResolveEffectivePackAsync(registration.RegistrationCategory!.Value,
            DateTime.UtcNow, cancellationToken);
        var now = DateTime.UtcNow;
        var snapshot = Snapshot(pack);
        var snapshotJson = Serialize(snapshot);
        var binding = new ProcurementSupplierRegistrationEvidencePackBinding
        {
            Id = Guid.NewGuid(),
            TenantId = registration.TenantId,
            RegistrationId = registration.Id,
            PackVersionId = pack.Id,
            RegistrationCategory = registration.RegistrationCategory.Value,
            PackCode = pack.PackCode,
            PackVersion = pack.Version,
            BoundAtUtc = now,
            BoundById = actorUserId,
            PackSnapshotJson = snapshotJson,
            PackSnapshotHash = Hash(snapshotJson),
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = actorUserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        binding.IntegrityHash = Hash(Serialize(new
        {
            binding.TenantId,
            binding.RegistrationId,
            binding.PackVersionId,
            binding.RegistrationCategory,
            binding.PackCode,
            binding.PackVersion,
            binding.BoundAtUtc,
            binding.BoundById,
            binding.PackSnapshotHash
        }));

        await ExecuteAsync(async () =>
        {
            var existing = await Bindings.GetQueryable(item =>
                    item.TenantId == registration.TenantId && item.RegistrationId == registration.Id &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                await Bindings.AddAsync(binding);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                var controlEvent = new ProcurementControlEventWriteRequest
                {
                    EventKey = ProcurementControlEventKey.Create(
                        "supplier-registration-pack", registration.TenantId, registration.Id,
                        $"bound-{correlation}"),
                    EventType = EventType,
                    Action = "RegistrationPackBound",
                    Result = ProcurementControlEventResult.Succeeded,
                    RuleCode = pack.PackCode,
                    RuleId = pack.Id,
                    RuleVersion = pack.Version.ToString(),
                    DecisionKeys = DecisionKeys.ToList(),
                    SourceType = RegistrationSourceType,
                    SourceId = registration.Id,
                    SourceReference = registration.RegistrationNumber,
                    ResultValues = new
                    {
                        pack.Id,
                        pack.PackCode,
                        pack.Version,
                        pack.Category,
                        binding.PackSnapshotHash
                    },
                    CorrelationId = correlation,
                    CausationId = correlation,
                    OccurredAtUtc = now
                };
                if (_currentUser.IsExternalUser)
                {
                    await _controlEvents.RecordSystemAsync(
                        registration.TenantId,
                        ActorName,
                        controlEvent,
                        cancellationToken);
                }
                else
                {
                    await _controlEvents.RecordAsync(controlEvent, cancellationToken);
                }
            }
        }, cancellationToken);
        return await GetRegistrationReadinessAsync(registrationId, cancellationToken);
    }

    private IQueryable<ProcurementSupplierEvidencePackVersion> PackQuery() =>
        Packs.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private async Task<ProcurementSupplierEvidencePackVersion> LoadPackAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ProcurementSupplierEvidencePackVersion> query =
            PackQuery().Include(item => item.Requirements);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ??
            throw NotFound("SUPPLIER_EVIDENCE_PACK_NOT_FOUND",
                $"Supplier evidence pack {id} was not found in the current tenant.");
    }

    private async Task<BusinessPartnerRegistration> LoadRegistrationAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await Registrations.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == id && !item.IsDeleted)
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .Include(item => item.EvidencePackBinding)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken) ??
        throw NotFound("SUPPLIER_REGISTRATION_NOT_FOUND",
            $"Supplier registration {id} was not found in the current tenant.");

    private async Task<ProcurementSupplierEvidencePackVersion> ResolveEffectivePackAsync(
        ProcurementSupplierRegistrationCategory category,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        var candidates = await PackQuery().Where(item =>
                item.Category == category &&
                item.Status == ProcurementSupplierEvidencePackStatus.Published &&
                item.EffectiveFromUtc <= atUtc &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= atUtc))
            .Include(item => item.Requirements)
            .OrderByDescending(item => item.EffectiveFromUtc)
            .ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_NOT_EFFECTIVE",
                $"No Published and effective {category} supplier evidence pack is available.");
        var selected = candidates[0];
        if (candidates.Skip(1).Any(item =>
                item.PackKey != selected.PackKey &&
                item.EffectiveFromUtc == selected.EffectiveFromUtc))
            throw Conflict("SUPPLIER_EVIDENCE_PACK_AMBIGUOUS",
                $"More than one {category} supplier evidence pack is equally effective.");
        return selected;
    }

    private async Task<ProcurementSupplierEvidenceReadinessDto> EvaluateRegistrationAsync(
        BusinessPartnerRegistration registration,
        CancellationToken cancellationToken)
    {
        var result = new ProcurementSupplierEvidenceReadinessDto
        {
            RegistrationId = registration.Id,
            Category = registration.RegistrationCategory,
            IsBound = registration.EvidencePackBinding is not null,
            EvaluatedAtUtc = DateTime.UtcNow
        };
        if (!registration.RegistrationCategory.HasValue)
        {
            result.BlockingReasons.Add("Select Goods, Works, or Services.");
            return result;
        }

        ProcurementSupplierEvidencePackVersion pack;
        if (registration.EvidencePackBinding is not null)
        {
            pack = await Packs.GetQueryable(item =>
                    item.TenantId == registration.TenantId &&
                    item.Id == registration.EvidencePackBinding.PackVersionId)
                .Include(item => item.Requirements)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken) ??
                throw Conflict("SUPPLIER_EVIDENCE_PACK_BINDING_BROKEN",
                    "The registration's immutable evidence-pack binding is unavailable.");
            if (pack.Category != registration.RegistrationCategory.Value)
                throw Conflict("SUPPLIER_EVIDENCE_PACK_BINDING_CATEGORY_MISMATCH",
                    "The bound evidence pack does not match the registration category.");
        }
        else
        {
            try
            {
                pack = await ResolveEffectivePackAsync(registration.RegistrationCategory.Value,
                    result.EvaluatedAtUtc, cancellationToken);
            }
            catch (ProcurementSupplierEvidencePackConflictException exception)
            {
                result.BlockingReasons.Add(exception.Message);
                return result;
            }
        }

        result.PackVersionId = pack.Id;
        result.PackCode = pack.PackCode;
        result.PackVersion = pack.Version;
        foreach (var requirement in pack.Requirements.Where(item => !item.IsDeleted)
                     .OrderBy(item => item.ApprovalStepOrder).ThenBy(item => item.RequirementCode))
        {
            var item = EvaluateRequirement(requirement, registration.Documents, result.EvaluatedAtUtc);
            result.Requirements.Add(item);
            if (requirement.IsMandatory && !item.IsSatisfied)
                result.BlockingReasons.Add($"{requirement.RequirementCode}: {string.Join(", ", item.Issues)}");
        }
        result.IsReady = result.BlockingReasons.Count == 0;
        return result;
    }

    private static ProcurementSupplierEvidenceRequirementReadinessDto EvaluateRequirement(
        ProcurementSupplierEvidenceRequirement requirement,
        IEnumerable<BusinessPartnerRegistrationDocument> documents,
        DateTime atUtc)
    {
        var result = new ProcurementSupplierEvidenceRequirementReadinessDto
        {
            RequirementCode = requirement.RequirementCode,
            Name = requirement.Name,
            IsMandatory = requirement.IsMandatory,
            Kind = requirement.Kind,
            DocumentType = requirement.DocumentType,
            ClassificationScheme = requirement.ClassificationScheme,
            AllowedClassifications = DeserializeList(requirement.AllowedClassificationsJson),
            ValidityMode = requirement.ValidityMode,
            MinimumRemainingDays = requirement.MinimumRemainingDays,
            MaxFileSizeBytes = requirement.MaxFileSizeBytes,
            AllowedMimeTypes = DeserializeList(requirement.AllowedMimeTypesJson),
            ApprovalStepName = requirement.ApprovalStepName,
            ApprovalStepOrder = requirement.ApprovalStepOrder
        };
        var matches = documents.Where(item =>
                !item.IsDeleted && !item.IsRejected &&
                string.Equals(item.EvidenceRequirementCode, requirement.RequirementCode,
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAt).ToList();
        var document = matches.FirstOrDefault();
        if (document is null)
        {
            result.Issues.Add("required evidence has not been uploaded");
            result.IsSatisfied = !requirement.IsMandatory;
            return result;
        }
        result.MatchedDocumentName = document.DocumentName;
        result.ClassificationCode = document.ClassificationCode;
        result.ExpiresAtUtc = document.ExpiresAtUtc;
        if (requirement.Kind is ProcurementSupplierEvidenceRequirementKind.Document or
            ProcurementSupplierEvidenceRequirementKind.DocumentAndClassification)
        {
            if (!string.Equals(document.DocumentType, requirement.DocumentType,
                    StringComparison.OrdinalIgnoreCase))
                result.Issues.Add($"document type must be {requirement.DocumentType}");
            if (document.FileSize.GetValueOrDefault() <= 0 ||
                document.FileSize.GetValueOrDefault() > requirement.MaxFileSizeBytes)
                result.Issues.Add($"file size must be between 1 and {requirement.MaxFileSizeBytes} bytes");
            var mimeTypes = DeserializeList(requirement.AllowedMimeTypesJson);
            if (mimeTypes.Count > 0 && !mimeTypes.Contains(document.MimeType ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase))
                result.Issues.Add("file type is not permitted");
        }
        if (requirement.Kind is ProcurementSupplierEvidenceRequirementKind.Classification or
            ProcurementSupplierEvidenceRequirementKind.DocumentAndClassification)
        {
            var allowed = DeserializeList(requirement.AllowedClassificationsJson);
            if (string.IsNullOrWhiteSpace(document.ClassificationCode))
                result.Issues.Add($"{requirement.ClassificationScheme} classification is required");
            else if (allowed.Count > 0 && !allowed.Contains(document.ClassificationCode,
                         StringComparer.OrdinalIgnoreCase))
                result.Issues.Add("classification is not allowed");
        }
        if (requirement.ValidityMode == ProcurementSupplierEvidenceValidityMode.CurrentOnSubmission &&
            (!document.ExpiresAtUtc.HasValue || document.ExpiresAtUtc.Value <= atUtc))
            result.Issues.Add("evidence must be current on submission");
        if (requirement.ValidityMode == ProcurementSupplierEvidenceValidityMode.MinimumRemainingDays)
        {
            var minimum = atUtc.AddDays(requirement.MinimumRemainingDays.GetValueOrDefault());
            if (!document.ExpiresAtUtc.HasValue || document.ExpiresAtUtc.Value < minimum)
                result.Issues.Add(
                    $"evidence must remain valid for at least {requirement.MinimumRemainingDays} days");
        }
        result.IsSatisfied = result.Issues.Count == 0;
        return result;
    }

    private async Task<ProcurementConfigurationProfile> ValidateProfileAsync(
        Guid id,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw Validation("SUPPLIER_EVIDENCE_PACK_PROFILE_REQUIRED",
                "A Published configuration profile is required.");
        var at = EnsureUtc(effectiveAtUtc);
        var profile = await ConfigurationProfiles.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == id && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken) ??
            throw Validation("SUPPLIER_EVIDENCE_PACK_PROFILE_NOT_FOUND",
                "The configuration profile was not found in the current tenant.");
        if (profile.LifecycleStatus != ProcurementConfigurationProfileStatus.Published ||
            profile.EffectiveFrom > at ||
            (profile.EffectiveTo.HasValue && profile.EffectiveTo.Value < at))
            throw Validation("SUPPLIER_EVIDENCE_PACK_PROFILE_NOT_EFFECTIVE",
                "The configuration profile must be Published and effective when the pack becomes effective.");
        return profile;
    }

    private async Task<WorkflowDefinition> ValidateWorkflowAsync(
        Guid id,
        IReadOnlyCollection<SaveProcurementSupplierEvidenceRequirementRequest> requirements,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw Validation("SUPPLIER_EVIDENCE_PACK_WORKFLOW_REQUIRED",
                "A Published shared workflow definition is required.");
        var workflow = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == id && !item.IsDeleted)
            .Include(item => item.Steps.Where(step => !step.IsDeleted))
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken) ??
            throw Validation("SUPPLIER_EVIDENCE_PACK_WORKFLOW_NOT_FOUND",
                "The shared workflow definition was not found in the current tenant.");
        if (!workflow.IsActive ||
            workflow.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
            throw Validation("SUPPLIER_EVIDENCE_PACK_WORKFLOW_NOT_PUBLISHED",
                "The shared workflow definition must be active and Published.");
        foreach (var requirement in requirements)
        {
            var step = workflow.Steps.SingleOrDefault(item =>
                item.Order == requirement.ApprovalStepOrder &&
                string.Equals(item.Name.Trim(), requirement.ApprovalStepName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            if (step is null)
                throw Validation("SUPPLIER_EVIDENCE_PACK_APPROVAL_STEP_INVALID",
                    $"Requirement {requirement.RequirementCode} must reference an exact order/name step in the selected shared workflow.");
        }
        return workflow;
    }

    private async Task ValidateForPublicationAsync(
        ProcurementSupplierEvidencePackVersion entity,
        CancellationToken cancellationToken)
    {
        var requests = entity.Requirements.Where(item => !item.IsDeleted).Select(ToRequest).ToList();
        ValidateRequest(new SaveProcurementSupplierEvidencePackRequest
        {
            PackCode = entity.PackCode,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            EffectiveFromUtc = entity.EffectiveFromUtc,
            EffectiveToUtc = entity.EffectiveToUtc,
            SourceConfigurationProfileId = entity.SourceConfigurationProfileId,
            WorkflowDefinitionId = entity.WorkflowDefinitionId,
            ChangeSummary = entity.ChangeSummary,
            Requirements = requests
        }, entity.Version > 1);
        await ValidateProfileAsync(entity.SourceConfigurationProfileId,
            entity.EffectiveFromUtc, cancellationToken);
        await ValidateWorkflowAsync(entity.WorkflowDefinitionId, requests, cancellationToken);
    }

    private static void ValidateRequest(
        SaveProcurementSupplierEvidencePackRequest request,
        bool isReplacement)
    {
        request.PackCode = NormalizeCode(request.PackCode, 50, "SUPPLIER_EVIDENCE_PACK_CODE_REQUIRED");
        request.Name = Require(request.Name, 200, "SUPPLIER_EVIDENCE_PACK_NAME_REQUIRED",
            "Pack name is required.");
        request.Description = Trim(request.Description, 1000);
        request.ChangeSummary = Trim(request.ChangeSummary, 1000);
        if (!Enum.IsDefined(request.Category))
            throw Validation("SUPPLIER_EVIDENCE_PACK_CATEGORY_INVALID",
                "Category must be Goods, Works, or Services.");
        ValidateDates(request.EffectiveFromUtc, request.EffectiveToUtc);
        if (isReplacement && string.IsNullOrWhiteSpace(request.ChangeSummary))
            throw Validation("SUPPLIER_EVIDENCE_PACK_CHANGE_SUMMARY_REQUIRED",
                "A replacement version requires a change summary.");
        if (request.Requirements.Count == 0)
            throw Validation("SUPPLIER_EVIDENCE_PACK_REQUIREMENTS_REQUIRED",
                "At least one evidence requirement is required.");
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in request.Requirements)
        {
            requirement.RequirementCode = NormalizeCode(requirement.RequirementCode, 50,
                "SUPPLIER_EVIDENCE_REQUIREMENT_CODE_REQUIRED");
            requirement.Name = Require(requirement.Name, 200,
                "SUPPLIER_EVIDENCE_REQUIREMENT_NAME_REQUIRED", "Requirement name is required.");
            requirement.Description = Trim(requirement.Description, 1000);
            requirement.DocumentType = Trim(requirement.DocumentType, 100);
            requirement.ClassificationScheme = Trim(requirement.ClassificationScheme, 100);
            requirement.ApprovalStepName = Require(requirement.ApprovalStepName, 100,
                "SUPPLIER_EVIDENCE_APPROVAL_STEP_REQUIRED", "Approval step name is required.");
            if (!codes.Add(requirement.RequirementCode))
                throw Validation("SUPPLIER_EVIDENCE_REQUIREMENT_DUPLICATE",
                    $"Requirement code {requirement.RequirementCode} is duplicated.");
            if (!Enum.IsDefined(requirement.Kind) || !Enum.IsDefined(requirement.ValidityMode))
                throw Validation("SUPPLIER_EVIDENCE_REQUIREMENT_ENUM_INVALID",
                    $"Requirement {requirement.RequirementCode} has an invalid kind or validity mode.");
            if (requirement.ApprovalStepOrder < 1)
                throw Validation("SUPPLIER_EVIDENCE_APPROVAL_STEP_ORDER_INVALID",
                    $"Requirement {requirement.RequirementCode} needs a positive approval step order.");
            if (requirement.Kind is ProcurementSupplierEvidenceRequirementKind.Document or
                    ProcurementSupplierEvidenceRequirementKind.DocumentAndClassification &&
                string.IsNullOrWhiteSpace(requirement.DocumentType))
                throw Validation("SUPPLIER_EVIDENCE_DOCUMENT_TYPE_REQUIRED",
                    $"Requirement {requirement.RequirementCode} needs a document type.");
            if (requirement.Kind is ProcurementSupplierEvidenceRequirementKind.Classification or
                    ProcurementSupplierEvidenceRequirementKind.DocumentAndClassification &&
                (string.IsNullOrWhiteSpace(requirement.ClassificationScheme) ||
                 requirement.AllowedClassifications.Count == 0))
                throw Validation("SUPPLIER_EVIDENCE_CLASSIFICATION_REQUIRED",
                    $"Requirement {requirement.RequirementCode} needs a classification scheme and allowed values.");
            if (requirement.ValidityMode == ProcurementSupplierEvidenceValidityMode.MinimumRemainingDays &&
                requirement.MinimumRemainingDays.GetValueOrDefault() <= 0)
                throw Validation("SUPPLIER_EVIDENCE_MINIMUM_VALIDITY_INVALID",
                    $"Requirement {requirement.RequirementCode} needs positive minimum remaining days.");
            if (requirement.ValidityMode != ProcurementSupplierEvidenceValidityMode.MinimumRemainingDays)
                requirement.MinimumRemainingDays = null;
            requirement.AllowedClassifications = NormalizeValues(requirement.AllowedClassifications, 100);
            requirement.AllowedMimeTypes = NormalizeValues(requirement.AllowedMimeTypes, 100);
            if (requirement.Kind is ProcurementSupplierEvidenceRequirementKind.Document or
                    ProcurementSupplierEvidenceRequirementKind.DocumentAndClassification &&
                requirement.AllowedMimeTypes.Count == 0)
                throw Validation("SUPPLIER_EVIDENCE_MIME_TYPES_REQUIRED",
                    $"Requirement {requirement.RequirementCode} needs at least one allowed MIME type.");
        }
        if (!request.Requirements.Any(item => item.IsMandatory))
            throw Validation("SUPPLIER_EVIDENCE_MANDATORY_REQUIREMENT_REQUIRED",
                "At least one requirement must be mandatory.");
        if (request.Category == ProcurementSupplierRegistrationCategory.Works &&
            !request.Requirements.Any(item => item.IsMandatory &&
                item.Kind is ProcurementSupplierEvidenceRequirementKind.Classification or
                    ProcurementSupplierEvidenceRequirementKind.DocumentAndClassification))
            throw Validation("SUPPLIER_EVIDENCE_WORKS_CLASSIFICATION_REQUIRED",
                "A Works pack needs at least one mandatory classification requirement.");
    }

    private static void ValidateDates(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        var from = EnsureUtc(effectiveFromUtc);
        if (effectiveFromUtc == default)
            throw Validation("SUPPLIER_EVIDENCE_PACK_EFFECTIVE_FROM_REQUIRED",
                "EffectiveFromUtc is required.");
        if (effectiveToUtc.HasValue && EnsureUtc(effectiveToUtc.Value) <= from)
            throw Validation("SUPPLIER_EVIDENCE_PACK_EFFECTIVE_RANGE_INVALID",
                "EffectiveToUtc must be later than EffectiveFromUtc.");
    }

    private void Apply(
        ProcurementSupplierEvidencePackVersion entity,
        SaveProcurementSupplierEvidencePackRequest request,
        ProcurementConfigurationProfile profile,
        WorkflowDefinition workflow)
    {
        entity.PackCode = request.PackCode;
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Category = request.Category;
        entity.EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc);
        entity.EffectiveToUtc = request.EffectiveToUtc.HasValue
            ? EnsureUtc(request.EffectiveToUtc.Value)
            : null;
        entity.SourceConfigurationProfileId = profile.Id;
        entity.SourceConfigurationProfileCode = profile.ProfileCode;
        entity.SourceConfigurationProfileVersion = profile.Version;
        entity.WorkflowDefinitionId = workflow.Id;
        entity.ChangeSummary = request.ChangeSummary;
    }

    private void ApplyRequirements(
        ProcurementSupplierEvidencePackVersion entity,
        IEnumerable<SaveProcurementSupplierEvidenceRequirementRequest> requests,
        DateTime now)
    {
        foreach (var request in requests)
        {
            var requirement = new ProcurementSupplierEvidenceRequirement
            {
                Id = Guid.NewGuid(),
                TenantId = entity.TenantId,
                PackVersionId = entity.Id,
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            ApplyRequirement(requirement, request);
            entity.Requirements.Add(requirement);
        }
    }

    private async Task SynchronizeRequirementsAsync(
        ProcurementSupplierEvidencePackVersion entity,
        IReadOnlyCollection<SaveProcurementSupplierEvidenceRequirementRequest> requests,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var all = await Requirements.GetQueryableIncludingDeleted(item =>
                item.TenantId == entity.TenantId && item.PackVersionId == entity.Id)
            .ToListAsync(cancellationToken);
        var desired = requests.ToDictionary(item => item.RequirementCode,
            StringComparer.OrdinalIgnoreCase);
        foreach (var existing in all)
        {
            if (!desired.TryGetValue(existing.RequirementCode, out var request))
            {
                if (!existing.IsDeleted) SoftDelete(existing, now);
                continue;
            }
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            ApplyRequirement(existing, request);
            existing.UpdatedAt = now;
            existing.UpdatedBy = ActorName;
            existing.LastModifiedById = _currentUser.UserId;
            desired.Remove(existing.RequirementCode);
        }
        ApplyRequirements(entity, desired.Values, now);
    }

    private static void ApplyRequirement(
        ProcurementSupplierEvidenceRequirement entity,
        SaveProcurementSupplierEvidenceRequirementRequest request)
    {
        entity.RequirementCode = request.RequirementCode;
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Kind = request.Kind;
        entity.DocumentType = request.DocumentType;
        entity.IsMandatory = request.IsMandatory;
        entity.ClassificationScheme = request.ClassificationScheme;
        entity.AllowedClassificationsJson = request.AllowedClassifications.Count == 0
            ? null
            : Serialize(request.AllowedClassifications);
        entity.ValidityMode = request.ValidityMode;
        entity.MinimumRemainingDays = request.MinimumRemainingDays;
        entity.ApprovalStepOrder = request.ApprovalStepOrder;
        entity.ApprovalStepName = request.ApprovalStepName;
        entity.MaxFileSizeBytes = request.MaxFileSizeBytes;
        entity.AllowedMimeTypesJson = Serialize(request.AllowedMimeTypes);
        entity.IntegrityHash = Hash(Serialize(new
        {
            entity.RequirementCode,
            entity.Name,
            entity.Description,
            entity.Kind,
            entity.DocumentType,
            entity.IsMandatory,
            entity.ClassificationScheme,
            entity.AllowedClassificationsJson,
            entity.ValidityMode,
            entity.MinimumRemainingDays,
            entity.ApprovalStepOrder,
            entity.ApprovalStepName,
            entity.MaxFileSizeBytes,
            entity.AllowedMimeTypesJson
        }));
    }

    private static SaveProcurementSupplierEvidenceRequirementRequest ToRequest(
        ProcurementSupplierEvidenceRequirement entity) => new()
    {
        RequirementCode = entity.RequirementCode,
        Name = entity.Name,
        Description = entity.Description,
        Kind = entity.Kind,
        DocumentType = entity.DocumentType,
        IsMandatory = entity.IsMandatory,
        ClassificationScheme = entity.ClassificationScheme,
        AllowedClassifications = DeserializeList(entity.AllowedClassificationsJson),
        ValidityMode = entity.ValidityMode,
        MinimumRemainingDays = entity.MinimumRemainingDays,
        ApprovalStepOrder = entity.ApprovalStepOrder,
        ApprovalStepName = entity.ApprovalStepName,
        MaxFileSizeBytes = entity.MaxFileSizeBytes,
        AllowedMimeTypes = DeserializeList(entity.AllowedMimeTypesJson)
    };

    private async Task EnsureUniqueFamilyAsync(
        ProcurementSupplierEvidencePackVersion entity,
        CancellationToken cancellationToken)
    {
        if (await Packs.GetQueryableIncludingDeleted(item =>
                item.TenantId == entity.TenantId && item.Id != entity.Id &&
                item.PackKey == entity.PackKey && item.Version == entity.Version)
            .AnyAsync(cancellationToken))
            throw Conflict("SUPPLIER_EVIDENCE_PACK_VERSION_DUPLICATE",
                "The pack family already contains this version.");
        if (await Packs.GetQueryable(item =>
                item.TenantId == entity.TenantId && item.Id != entity.Id &&
                item.PackCode == entity.PackCode && item.Version == entity.Version &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken))
            throw Conflict("SUPPLIER_EVIDENCE_PACK_CODE_VERSION_DUPLICATE",
                "The pack code and version already exist.");
        if (await Packs.GetQueryable(item =>
                item.TenantId == entity.TenantId && item.Id != entity.Id &&
                item.PackKey == entity.PackKey && !item.IsDeleted &&
                (item.Status == ProcurementSupplierEvidencePackStatus.Draft ||
                 item.Status == ProcurementSupplierEvidencePackStatus.PendingApproval))
            .AnyAsync(cancellationToken))
            throw Conflict("SUPPLIER_EVIDENCE_PACK_OPEN_VERSION_EXISTS",
                "Only one Draft or PendingApproval version may exist in a pack family.");
    }

    private async Task EnsureNoCompetingFamilyOverlapAsync(
        ProcurementSupplierEvidencePackVersion entity,
        CancellationToken cancellationToken)
    {
        var from = entity.EffectiveFromUtc;
        var to = entity.EffectiveToUtc;
        var overlap = await PackQuery().AnyAsync(item =>
            item.Id != entity.Id && item.PackKey != entity.PackKey &&
            item.Category == entity.Category &&
            item.Status == ProcurementSupplierEvidencePackStatus.Published &&
            (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= from) &&
            (!to.HasValue || item.EffectiveFromUtc <= to.Value), cancellationToken);
        if (overlap)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_EFFECTIVE_OVERLAP",
                "Another pack family is Published for an overlapping category/effective period.");
    }

    private async Task EnsureWorkflowOutcomeAsync(
        ProcurementSupplierEvidencePackVersion entity,
        bool approving,
        CancellationToken cancellationToken)
    {
        if (!entity.WorkflowInstanceId.HasValue)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_WORKFLOW_INSTANCE_REQUIRED",
                "The shared workflow instance was not recorded.");
        var instance = await WorkflowInstanceRows.GetQueryable(item =>
                item.TenantId == entity.TenantId && item.Id == entity.WorkflowInstanceId.Value &&
                !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken) ??
            throw Conflict("SUPPLIER_EVIDENCE_PACK_WORKFLOW_INSTANCE_NOT_FOUND",
                "The shared workflow instance is unavailable.");
        if (instance.WorkflowDefinitionId != entity.WorkflowDefinitionId ||
            instance.EntityId != entity.Id)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_WORKFLOW_BINDING_INVALID",
                "The shared workflow instance does not belong to this pack/version.");
        if (approving && instance.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_WORKFLOW_NOT_APPROVED",
                "Only a Completed shared workflow permits publication.");
        if (!approving &&
            instance.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw Conflict("SUPPLIER_EVIDENCE_PACK_WORKFLOW_NOT_REJECTED",
                "Only a Cancelled or Failed shared workflow permits rejection.");
    }

    private async Task EnsureIndependentActorAsync(
        Guid? prohibitedActor,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!prohibitedActor.HasValue || prohibitedActor.Value == Guid.Empty)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_INITIATOR_NOT_RECORDED",
                "The submitting actor was not recorded.");
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = [prohibitedActor.Value]
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierEvidencePackAuthorizationException(decision.Message);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierEvidencePackAuthorizationException(
                "Supplier portal users cannot administer evidence-pack configuration.");
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission,
            SourceType = SourceType,
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? "NEW" : sourceReference.Trim()
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierEvidencePackAuthorizationException(decision.Message);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierEvidencePackAuthorizationException(
                "Supplier portal users cannot access evidence-pack administration.");
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read"))
            return;
        throw new ProcurementSupplierEvidencePackAuthorizationException(
            "The procurement records read permission is required.");
    }

    private async Task EnsureRegistrationReaderAsync(
        BusinessPartnerRegistration registration,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsExternalUser) return;
        if (registration.CreatedById == _currentUser.UserId) return;

        // Approval replaces the temporary applicant identity with the supplier account.
        // The approved supplier owner and its active delegated users must therefore be
        // able to read the evidence lineage that now governs their eligibility.
        if (registration.BusinessPartnerId.HasValue)
        {
            var businessPartnerId = registration.BusinessPartnerId.Value;
            var ownsPartner = await _unitOfWork.Repository<BusinessPartner>()
                .ExistsAsync(item => item.Id == businessPartnerId &&
                    item.TenantId == _currentUser.TenantId &&
                    item.UserId == _currentUser.UserId &&
                    !item.IsDeleted);
            if (ownsPartner) return;

            var isActiveDelegate = await _unitOfWork.Repository<BusinessPartnerUser>()
                .ExistsAsync(item => item.BusinessPartnerId == businessPartnerId &&
                    item.TenantId == _currentUser.TenantId &&
                    item.UserId == _currentUser.UserId &&
                    item.IsActive &&
                    !item.IsDeleted);
            if (isActiveDelegate) return;
        }

        throw new ProcurementSupplierEvidencePackAuthorizationException(
            "Supplier applicants can access only their own or linked approved supplier registration evidence status.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSupplierEvidencePackAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private async Task RecordEventAsync(
        ProcurementSupplierEvidencePackVersion entity,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create(
                "supplier-evidence-pack", entity.TenantId, entity.Id,
                $"{action}-{correlationId}"),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = entity.SourceConfigurationProfileCode,
            RuleId = entity.SourceConfigurationProfileId,
            RuleVersion = entity.SourceConfigurationProfileVersion.ToString(),
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = SourceType,
            SourceId = entity.Id,
            SourceReference = $"{entity.PackCode}/v{entity.Version}",
            Reason = Trim(reason, 1000),
            Before = before,
            After = after,
            CorrelationId = correlationId,
            CausationId = correlationId,
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

    private async Task PublishNotificationAsync(
        string topic,
        ProcurementSupplierEvidencePackVersion entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = entity.TenantId,
                TopicKey = topic,
                NotificationType = "ProcurementSupplierEvidencePackControl",
                EntityType = SourceType,
                EntityId = entity.Id,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["packCode"] = entity.PackCode,
                    ["version"] = entity.Version,
                    ["category"] = entity.Category.ToString(),
                    ["status"] = entity.Status.ToString(),
                    ["effectiveFromUtc"] = entity.EffectiveFromUtc
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish supplier evidence-pack notification {Topic} for {PackId}",
                topic, entity.Id);
        }
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
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
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await action();
            return true;
        }, cancellationToken);

    private static ProcurementSupplierEvidencePackListItemDto MapList(
        ProcurementSupplierEvidencePackVersion item)
    {
        var now = DateTime.UtcNow;
        var requirements = item.Requirements.Where(requirement => !requirement.IsDeleted).ToList();
        return new ProcurementSupplierEvidencePackListItemDto
        {
            Id = item.Id,
            PackKey = item.PackKey,
            PackCode = item.PackCode,
            Name = item.Name,
            Category = item.Category,
            Version = item.Version,
            Status = item.Status,
            EffectiveFromUtc = item.EffectiveFromUtc,
            EffectiveToUtc = item.EffectiveToUtc,
            IsEffective = item.Status == ProcurementSupplierEvidencePackStatus.Published &&
                item.EffectiveFromUtc <= now &&
                (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now),
            RequirementCount = requirements.Count,
            MandatoryRequirementCount = requirements.Count(requirement => requirement.IsMandatory),
            SourceConfigurationProfileCode = item.SourceConfigurationProfileCode,
            SourceConfigurationProfileVersion = item.SourceConfigurationProfileVersion,
            AllowedActions = Actions(item.Status),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static ProcurementSupplierEvidencePackDto Map(
        ProcurementSupplierEvidencePackVersion item)
    {
        var list = MapList(item);
        var blocked = new List<string>();
        if (list.RequirementCount == 0) blocked.Add("At least one requirement is required.");
        if (list.MandatoryRequirementCount == 0) blocked.Add("At least one mandatory requirement is required.");
        return new ProcurementSupplierEvidencePackDto
        {
            Id = list.Id,
            PackKey = list.PackKey,
            PackCode = list.PackCode,
            Name = list.Name,
            Category = list.Category,
            Version = list.Version,
            Status = list.Status,
            EffectiveFromUtc = list.EffectiveFromUtc,
            EffectiveToUtc = list.EffectiveToUtc,
            IsEffective = list.IsEffective,
            RequirementCount = list.RequirementCount,
            MandatoryRequirementCount = list.MandatoryRequirementCount,
            SourceConfigurationProfileCode = list.SourceConfigurationProfileCode,
            SourceConfigurationProfileVersion = list.SourceConfigurationProfileVersion,
            AllowedActions = list.AllowedActions,
            RowVersion = list.RowVersion,
            Description = item.Description,
            SourceConfigurationProfileId = item.SourceConfigurationProfileId,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            SupersedesVersionId = item.SupersedesVersionId,
            ChangeSummary = item.ChangeSummary,
            ReviewComment = item.ReviewComment,
            SubmittedById = item.SubmittedById,
            SubmittedAtUtc = item.SubmittedAtUtc,
            PublishedById = item.PublishedById,
            PublishedAtUtc = item.PublishedAtUtc,
            RetiredById = item.RetiredById,
            RetiredAtUtc = item.RetiredAtUtc,
            IntegrityHash = item.IntegrityHash,
            Requirements = item.Requirements.Where(requirement => !requirement.IsDeleted)
                .OrderBy(requirement => requirement.ApprovalStepOrder)
                .ThenBy(requirement => requirement.RequirementCode)
                .Select(requirement => new ProcurementSupplierEvidenceRequirementDto
                {
                    Id = requirement.Id,
                    RequirementCode = requirement.RequirementCode,
                    Name = requirement.Name,
                    Description = requirement.Description,
                    Kind = requirement.Kind,
                    DocumentType = requirement.DocumentType,
                    IsMandatory = requirement.IsMandatory,
                    ClassificationScheme = requirement.ClassificationScheme,
                    AllowedClassifications = DeserializeList(requirement.AllowedClassificationsJson),
                    ValidityMode = requirement.ValidityMode,
                    MinimumRemainingDays = requirement.MinimumRemainingDays,
                    ApprovalStepOrder = requirement.ApprovalStepOrder,
                    ApprovalStepName = requirement.ApprovalStepName,
                    MaxFileSizeBytes = requirement.MaxFileSizeBytes,
                    AllowedMimeTypes = DeserializeList(requirement.AllowedMimeTypesJson),
                    IntegrityHash = requirement.IntegrityHash
                }).ToList(),
            BlockedReasons = blocked
        };
    }

    private static List<string> Actions(ProcurementSupplierEvidencePackStatus status) => status switch
    {
        ProcurementSupplierEvidencePackStatus.Draft => ["Edit", "Submit", "Delete"],
        ProcurementSupplierEvidencePackStatus.PendingApproval => ["Publish", "Reject"],
        ProcurementSupplierEvidencePackStatus.Published => ["Clone", "Retire"],
        ProcurementSupplierEvidencePackStatus.Retired => ["Clone"],
        _ => []
    };

    private static object Snapshot(ProcurementSupplierEvidencePackVersion item) => new
    {
        item.Id,
        item.TenantId,
        item.PackKey,
        item.PackCode,
        item.Name,
        item.Description,
        item.Category,
        item.Version,
        item.Status,
        item.EffectiveFromUtc,
        item.EffectiveToUtc,
        item.SourceConfigurationProfileId,
        item.SourceConfigurationProfileCode,
        item.SourceConfigurationProfileVersion,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.SupersedesVersionId,
        item.ChangeSummary,
        item.ReviewComment,
        Requirements = item.Requirements.Where(requirement => !requirement.IsDeleted)
            .OrderBy(requirement => requirement.RequirementCode)
            .Select(requirement => new
            {
                requirement.RequirementCode,
                requirement.Name,
                requirement.Description,
                requirement.Kind,
                requirement.DocumentType,
                requirement.IsMandatory,
                requirement.ClassificationScheme,
                requirement.AllowedClassificationsJson,
                requirement.ValidityMode,
                requirement.MinimumRemainingDays,
                requirement.ApprovalStepOrder,
                requirement.ApprovalStepName,
                requirement.MaxFileSizeBytes,
                requirement.AllowedMimeTypesJson,
                requirement.IntegrityHash
            }).ToList()
    };

    private static void Capture(ProcurementSupplierEvidencePackVersion entity)
    {
        entity.LifecycleSnapshotJson = Serialize(Snapshot(entity));
        entity.IntegrityHash = Hash(entity.LifecycleSnapshotJson);
    }

    private void Touch(
        ProcurementSupplierEvidencePackVersion entity,
        string operation,
        string correlationId,
        DateTime now)
    {
        entity.LastOperation = operation;
        entity.LastOperationCorrelationId = correlationId;
        entity.UpdatedAt = now;
        entity.UpdatedBy = ActorName;
        entity.LastModifiedById = _currentUser.UserId;
        entity.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void SoftDelete(ErpSystem.Core.Entities.BaseEntity entity, DateTime now)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.DeletedBy = ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = ActorName;
        entity.LastModifiedById = _currentUser.UserId;
    }

    private static bool IsReplay(
        ProcurementSupplierEvidencePackVersion entity,
        string operation,
        string correlationId) =>
        string.Equals(entity.LastOperation, operation, StringComparison.Ordinal) &&
        string.Equals(entity.LastOperationCorrelationId, correlationId, StringComparison.Ordinal);

    private static void EnsureStatus(
        ProcurementSupplierEvidencePackVersion entity,
        ProcurementSupplierEvidencePackStatus expected,
        string message)
    {
        if (entity.Status != expected)
            throw Conflict("SUPPLIER_EVIDENCE_PACK_STATUS_INVALID", message);
    }

    private static void EnsureRowVersion(byte[] current, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw Validation("SUPPLIER_EVIDENCE_PACK_ROW_VERSION_REQUIRED",
                "RowVersion is required.");
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw Validation("SUPPLIER_EVIDENCE_PACK_ROW_VERSION_INVALID",
                "RowVersion must be valid Base64.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict("SUPPLIER_EVIDENCE_PACK_VERSION_CONFLICT",
                "The evidence pack changed by another user. Reload before continuing.");
    }

    private static void EnsureEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0 || evidence.Any(item =>
                !item.ReferenceId.HasValue && string.IsNullOrWhiteSpace(item.Reference)))
            throw Validation("SUPPLIER_EVIDENCE_PACK_LIFECYCLE_EVIDENCE_REQUIRED",
                "Lifecycle submission and decisions require a shared evidence reference.");
    }

    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.Username)
        ? _currentUser.UserId.ToString()
        : _currentUser.Username.Trim();

    private static string NormalizeCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(value.Trim().Length, 100)];

    private static string NormalizeCode(string? value, int max, string code)
    {
        var result = Require(value, max, code, "Code is required.")
            .ToUpperInvariant().Replace(' ', '-');
        if (result.Any(character => !char.IsLetterOrDigit(character) &&
                                    character is not '-' and not '_' and not '.'))
            throw Validation(code, "Code contains unsupported characters.");
        return result;
    }

    private static string Require(string? value, int max, string code, string message)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw Validation(code, message);
        return result[..Math.Min(result.Length, max)];
    }

    private static string? Trim(string? value, int max)
    {
        var result = value?.Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result[..Math.Min(result.Length, max)];
    }

    private static List<string> NormalizeValues(IEnumerable<string> values, int max) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()[..Math.Min(value.Trim().Length, max)])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static ProcurementSupplierEvidencePackNotFoundException NotFound(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierEvidencePackConflictException Conflict(
        string code,
        string message) => new(code, message);
    private static ProcurementSupplierEvidencePackValidationException Validation(
        string code,
        string message) => new(code, message);
}
