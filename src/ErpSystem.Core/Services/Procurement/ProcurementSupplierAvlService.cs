using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed class ProcurementSupplierAvlService : IProcurementSupplierAvlService
{
    private const string SourceType = "ProcurementSupplierAvl";
    private const string EventType = "ProcurementSupplierAvlControl";
    private const string ManagePermission = "procurement.supplier.manage";
    private const string ApprovePermission = "procurement.supplier.approve";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementSupplierAvlService> _logger;

    public ProcurementSupplierAvlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        ISupplierValidationService supplierValidation,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementSupplierAvlService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _supplierValidation = supplierValidation;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _notificationTopics = notificationTopics;
        _logger = logger;
    }

    private IGenericRepository<ProcurementSupplierAvlRegister> Registers =>
        _unitOfWork.Repository<ProcurementSupplierAvlRegister>();
    private IGenericRepository<ProcurementSupplierAvlEntry> Entries =>
        _unitOfWork.Repository<ProcurementSupplierAvlEntry>();
    private IGenericRepository<ProcurementSupplierAvlEntryStatusHistory> Histories =>
        _unitOfWork.Repository<ProcurementSupplierAvlEntryStatusHistory>();
    private IGenericRepository<ProcurementSupplierAvlPublicationSnapshot> Snapshots =>
        _unitOfWork.Repository<ProcurementSupplierAvlPublicationSnapshot>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions =>
        _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstances =>
        _unitOfWork.Repository<WorkflowInstance>();

    public async Task<ProcurementSupplierAvlSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var now = DateTime.UtcNow;
        var policy = await TryResolvePolicyAsync(now, cancellationToken);
        var query = Registers.GetQueryable(item =>
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        var currentRegisters = query.Where(item =>
            item.Status == ProcurementSupplierAvlRegisterStatus.Published &&
            item.EffectiveFromUtc <= now && item.ExpiresAtUtc > now &&
            (!item.ScheduledRetirementAtUtc.HasValue ||
             item.ScheduledRetirementAtUtc.Value > now));
        return new ProcurementSupplierAvlSummaryDto
        {
            TotalRegisters = await query.CountAsync(cancellationToken),
            DraftCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierAvlRegisterStatus.Draft, cancellationToken),
            PendingApprovalCount = await query.CountAsync(item =>
                item.Status == ProcurementSupplierAvlRegisterStatus.PendingApproval, cancellationToken),
            PublishedCount = await currentRegisters.CountAsync(cancellationToken),
            CurrentEntryCount = await currentRegisters.SelectMany(item => item.Entries)
                .CountAsync(item => !item.IsDeleted &&
                    item.Status == ProcurementSupplierAvlEntryStatus.Active, cancellationToken),
            SuspendedEntryCount = await currentRegisters.SelectMany(item => item.Entries)
                .CountAsync(item => !item.IsDeleted &&
                    item.Status == ProcurementSupplierAvlEntryStatus.Suspended, cancellationToken),
            PolicyAvailable = policy is not null,
            PolicyProfileCode = policy?.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy?.Decision.Profile.Version,
            ReviewFrequencyMonths = policy?.Value.ReviewFrequencyMonths,
            PolicyReleaseGate = policy is null
                ? "No unique Published/effective/approved/evidenced DEC-011 policy is available."
                : null
        };
    }

    public async Task<ProcurementSupplierAvlPageDto> SearchAsync(
        ProcurementSupplierAvlSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        request.Page = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = RegisterQuery().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.RegisterCode.Contains(search) ||
                item.Entries.Any(entry => !entry.IsDeleted &&
                    (entry.BusinessPartner.PartnerCode.Contains(search) ||
                     entry.BusinessPartner.PartnerName.Contains(search))));
        }
        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.ReviewYear)
            .ThenByDescending(item => item.Version)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new ProcurementSupplierAvlPageDto
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = rows.Select(MapList).ToList()
        };
    }

    public async Task<ProcurementSupplierAvlDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return Map(await LoadAsync(id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlCurrentStateDto> GetCurrentStateAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        if (!await _unitOfWork.Repository<BusinessPartner>().GetQueryable(item =>
                item.Id == businessPartnerId && item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted).AnyAsync(cancellationToken))
            throw NotFound("SUPPLIER_AVL_SUPPLIER_NOT_FOUND",
                "The supplier was not found in the current tenant.");
        return await BuildCurrentStateAsync(_currentUser.TenantId, businessPartnerId,
            DateTime.UtcNow, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementSupplierAvlWorkflowOptionDto>>
        GetWorkflowOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking().OrderBy(item => item.Name).ThenByDescending(item => item.Version)
            .Select(item => new ProcurementSupplierAvlWorkflowOptionDto
            {
                Id = item.Id,
                Name = item.Name,
                Version = item.Version
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementSupplierAvlSupplierOptionDto>>
        GetSupplierOptionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        return await _unitOfWork.Repository<BusinessPartner>().GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive &&
                (item.PartnerType.Contains("Supplier") ||
                 item.PartnerType.Contains("Contractor") ||
                 item.PartnerType.Contains("Both")))
            .AsNoTracking().OrderBy(item => item.PartnerCode)
            .Select(item => new ProcurementSupplierAvlSupplierOptionDto
            {
                Id = item.Id,
                Code = item.PartnerCode,
                Name = item.PartnerName
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProcurementSupplierAvlDto> CreateAsync(
        CreateProcurementSupplierAvlRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        await EnsureCapabilityAsync(ManagePermission, "NEW", correlation, cancellationToken);
        var now = DateTime.UtcNow;
        var effectiveFrom = EnsureUtc(request.EffectiveFromUtc);
        if (request.ReviewYear != effectiveFrom.Year)
            throw Validation("SUPPLIER_AVL_REVIEW_YEAR_INVALID",
                "ReviewYear must match the effective-date year.");
        var policy = await ResolvePolicyAsync(now, cancellationToken);
        var workflow = await ValidateWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);
        var existing = await Registers.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.ReviewYear == request.ReviewYear &&
                (item.Status == ProcurementSupplierAvlRegisterStatus.Draft ||
                    item.Status == ProcurementSupplierAvlRegisterStatus.PendingApproval ||
                    item.Status == ProcurementSupplierAvlRegisterStatus.Approved))
            .AnyAsync(cancellationToken);
        if (existing)
            throw Conflict("SUPPLIER_AVL_OPEN_REVIEW_EXISTS",
                "An open AVL review already exists for this review year.");
        var code = $"AVL-{request.ReviewYear}";
        var maxVersion = await Registers.GetQueryableIncludingDeleted(item =>
                item.TenantId == _currentUser.TenantId && item.RegisterCode == code)
            .Select(item => (int?)item.Version).MaxAsync(cancellationToken) ?? 0;
        var entity = new ProcurementSupplierAvlRegister
        {
            TenantId = _currentUser.TenantId,
            RegisterCode = code,
            Version = maxVersion + 1,
            ReviewYear = request.ReviewYear,
            EffectiveFromUtc = effectiveFrom,
            ExpiresAtUtc = effectiveFrom.AddMonths(policy.Value.ReviewFrequencyMonths),
            PolicyDecisionId = policy.Decision.Id,
            PolicyProfileId = policy.Decision.ProfileId,
            PolicyProfileCode = policy.Decision.Profile.ProfileCode,
            PolicyProfileVersion = policy.Decision.Profile.Version,
            ReviewFrequencyMonths = policy.Value.ReviewFrequencyMonths,
            PolicySnapshotJson = policy.Decision.ValueJson,
            PolicyValueHash = Hash(policy.Decision.ValueJson),
            WorkflowDefinitionId = workflow.Id,
            Notes = Trim(request.Notes, 1000),
            CreationCorrelationId = correlation,
            LastOperationCorrelationId = correlation,
            LastOperation = "Created",
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(entity);
        await Registers.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Created", ProcurementControlEventResult.Succeeded,
            null, Snapshot(entity), request.Notes, [], correlation, now, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlDto> UpdateAsync(
        Guid id,
        UpdateProcurementSupplierAvlRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.RegisterCode, correlation, cancellationToken);
        if (IsReplay(entity, "Updated", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "register");
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.Draft,
            "Only a Draft AVL register can be edited.");
        var effectiveFrom = EnsureUtc(request.EffectiveFromUtc);
        if (effectiveFrom.Year != entity.ReviewYear)
            throw Validation("SUPPLIER_AVL_REVIEW_YEAR_INVALID",
                "The effective date must remain in the register review year.");
        var before = Snapshot(entity);
        entity.EffectiveFromUtc = effectiveFrom;
        entity.ExpiresAtUtc = effectiveFrom.AddMonths(entity.ReviewFrequencyMonths);
        entity.Notes = Trim(request.Notes, 1000);
        Touch(entity, "Updated", correlation, DateTime.UtcNow);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Updated", ProcurementControlEventResult.Succeeded,
            before, Snapshot(entity), request.Notes, [], correlation, DateTime.UtcNow, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlDto> AddEntryAsync(
        Guid id,
        AddProcurementSupplierAvlEntryRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.RegisterCode, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, request.RegisterRowVersion, "register");
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.Draft,
            "Entries can be added only to a Draft AVL register.");
        if (request.BusinessPartnerId == Guid.Empty)
            throw Validation("SUPPLIER_AVL_SUPPLIER_REQUIRED", "A supplier is required.");
        if (entity.Entries.Any(item => !item.IsDeleted &&
                item.BusinessPartnerId == request.BusinessPartnerId))
            throw Conflict("SUPPLIER_AVL_ENTRY_EXISTS",
                "The supplier is already included in this AVL register.");
        var eligibility = await EvaluateCandidateAsync(request.BusinessPartnerId, cancellationToken);
        var now = DateTime.UtcNow;
        var entry = new ProcurementSupplierAvlEntry
        {
            TenantId = entity.TenantId,
            RegisterId = entity.Id,
            BusinessPartnerId = request.BusinessPartnerId,
            DueDiligenceReviewId = eligibility.DueDiligenceReviewId!.Value,
            RegistrationId = eligibility.RegistrationId,
            EvidencePackVersionId = eligibility.EvidencePackVersionId,
            QualifiedListEntryId = eligibility.QualifiedListEntries
                .Where(item => item.IsCurrent).Select(item => (Guid?)item.EntryId).FirstOrDefault(),
            AddedAtUtc = now,
            AddedById = _currentUser.UserId,
            EligibilitySnapshotJson = Serialize(eligibility),
            EligibilityDecisionHash = eligibility.DecisionHash,
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        Capture(entry);
        entity.Entries.Add(entry);
        await Entries.AddAsync(entry);
        Touch(entity, "EntryAdded", correlation, now);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "EntryAdded", ProcurementControlEventResult.Succeeded,
            null, SnapshotEntry(entry), "Supplier passed the pre-AVL eligibility review.",
            [], correlation, now, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlDto> RemoveEntryAsync(
        Guid id,
        Guid entryId,
        string rowVersion,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.RegisterCode, correlation, cancellationToken);
        EnsureRowVersion(entity.RowVersion, rowVersion, "register");
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.Draft,
            "Entries can be removed only from a Draft AVL register.");
        var entry = entity.Entries.SingleOrDefault(item => item.Id == entryId && !item.IsDeleted)
            ?? throw NotFound("SUPPLIER_AVL_ENTRY_NOT_FOUND",
                "The AVL entry was not found in the current tenant register.");
        var before = SnapshotEntry(entry);
        await Entries.DeleteAsync(entry);
        entry.IsDeleted = true;
        Touch(entity, "EntryRemoved", correlation, DateTime.UtcNow);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "EntryRemoved", ProcurementControlEventResult.Succeeded,
            before, null, "Draft AVL entry removed.", [], correlation,
            DateTime.UtcNow, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlDto> SubmitAsync(
        Guid id,
        ProcurementSupplierAvlLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, entity.RegisterCode, correlation, cancellationToken);
        if (IsReplay(entity, "Submitted", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "register");
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.Draft,
            "Only a Draft AVL register can be submitted.");
        EnsureLifecycleEvidence(request.Evidence);
        await ValidateForDecisionAsync(entity, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSupplierAvlRegisterStatus.PendingApproval;
        entity.SubmittedById = _currentUser.UserId;
        entity.SubmittedAtUtc = now;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Submitted", correlation, now);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var definition = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == entity.TenantId &&
                item.Id == entity.WorkflowDefinitionId && !item.IsDeleted)
            .SingleAsync(cancellationToken);
        var instance = await _workflowInstances.StartWorkflowAsync(
            definition.Id, definition.EntityTypeId, entity.Id.ToString(),
            _currentUser.UserId, new
            {
                entity.RegisterCode,
                entity.Version,
                entity.ReviewYear,
                entity.EffectiveFromUtc,
                entity.ExpiresAtUtc,
                entity.PolicyDecisionId,
                EntryCount = entity.Entries.Count(item => !item.IsDeleted)
            }, cancellationToken);
        entity.WorkflowInstanceId = instance.Id;
        Touch(entity, "Submitted", correlation, now);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Submitted", ProcurementControlEventResult.Succeeded,
            before, Snapshot(entity), request.Comment, request.Evidence,
            correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-avl.submitted", entity, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlDto> ApproveAsync(
        Guid id,
        ProcurementSupplierAvlLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.RegisterCode, correlation, cancellationToken);
        if (IsReplay(entity, "Approved", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "register");
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.PendingApproval,
            "Only a PendingApproval AVL register can be approved.");
        EnsureLifecycleEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, entity.RegisterCode,
            correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, true, cancellationToken);
        await ValidateForDecisionAsync(entity, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSupplierAvlRegisterStatus.Approved;
        entity.ApprovedById = _currentUser.UserId;
        entity.ApprovedAtUtc = now;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Approved", correlation, now);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Approved", ProcurementControlEventResult.Succeeded,
            before, Snapshot(entity), request.Comment, request.Evidence,
            correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-avl.approved", entity, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlDto> RejectAsync(
        Guid id,
        ProcurementSupplierAvlLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.RegisterCode, correlation, cancellationToken);
        if (IsReplay(entity, "Rejected", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "register");
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.PendingApproval,
            "Only a PendingApproval AVL register can be rejected.");
        EnsureLifecycleEvidence(request.Evidence);
        await EnsureIndependentActorAsync(entity.SubmittedById, entity.RegisterCode,
            correlation, cancellationToken);
        await EnsureWorkflowOutcomeAsync(entity, false, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.Status = ProcurementSupplierAvlRegisterStatus.Rejected;
        entity.RejectedById = _currentUser.UserId;
        entity.RejectedAtUtc = now;
        entity.ReviewComment = Trim(request.Comment, 1000);
        Touch(entity, "Rejected", correlation, now);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, "Rejected", ProcurementControlEventResult.Rejected,
            before, Snapshot(entity), request.Comment, request.Evidence,
            correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-avl.rejected", entity, cancellationToken);
        return Map(await LoadAsync(entity.Id, false, cancellationToken));
    }

    public async Task<ProcurementSupplierAvlDto> PublishAsync(
        Guid id,
        ProcurementSupplierAvlLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(id, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.RegisterCode, correlation, cancellationToken);
        if (IsReplay(entity, "Published", correlation)) return Map(entity);
        EnsureRowVersion(entity.RowVersion, request.RowVersion, "register");
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.Approved,
            "Only an Approved AVL register can be published.");
        EnsureLifecycleEvidence(request.Evidence);
        await ValidateForDecisionAsync(entity, cancellationToken);
        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        await ExecuteAsync(async () =>
        {
            var overlaps = await Registers.GetQueryable(item =>
                    item.TenantId == entity.TenantId && item.Id != entity.Id &&
                    !item.IsDeleted &&
                    item.Status == ProcurementSupplierAvlRegisterStatus.Published &&
                    item.EffectiveFromUtc < entity.ExpiresAtUtc &&
                    item.ExpiresAtUtc > entity.EffectiveFromUtc &&
                    (!item.ScheduledRetirementAtUtc.HasValue ||
                     item.ScheduledRetirementAtUtc.Value > entity.EffectiveFromUtc))
                .Include(item => item.Entries.Where(entry => !entry.IsDeleted))
                .ToListAsync(cancellationToken);
            foreach (var prior in overlaps)
            {
                prior.SupersededByRegisterId = entity.Id;
                if (entity.EffectiveFromUtc <= now)
                    await RetireRegisterAsync(prior, now, correlation, false, cancellationToken);
                else
                {
                    prior.ScheduledRetirementAtUtc = entity.EffectiveFromUtc;
                    Touch(prior, "RetirementScheduled", correlation, now);
                    Capture(prior);
                    await Registers.UpdateAsync(prior);
                }
            }

            entity.Status = ProcurementSupplierAvlRegisterStatus.Published;
            entity.PublishedById = _currentUser.UserId;
            entity.PublishedAtUtc = now;
            entity.ReviewComment = Trim(request.Comment, 1000);
            Touch(entity, "Published", correlation, now);
            Capture(entity);
            await Registers.UpdateAsync(entity);
            var snapshotJson = Serialize(Snapshot(entity));
            var snapshot = new ProcurementSupplierAvlPublicationSnapshot
            {
                TenantId = entity.TenantId,
                RegisterId = entity.Id,
                Sequence = 1,
                PublishedAtUtc = now,
                PublishedById = _currentUser.UserId,
                SnapshotJson = snapshotJson,
                IntegrityHash = Hash(snapshotJson),
                CreatedAt = now,
                CreatedBy = ActorName,
                CreatedById = _currentUser.UserId
            };
            entity.PublicationSnapshots.Add(snapshot);
            await Snapshots.AddAsync(snapshot);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        await RecordEventAsync(entity, "Published", ProcurementControlEventResult.Succeeded,
            before, Snapshot(entity), request.Comment, request.Evidence,
            correlation, now, cancellationToken);
        await PublishNotificationAsync("procurement.supplier-avl.published", entity, cancellationToken);
        return Map(entity);
    }

    public async Task<ProcurementSupplierAvlDto> SuspendEntryAsync(
        Guid id,
        Guid entryId,
        ProcurementSupplierAvlEntryLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        await ChangeEntryStatusAsync(id, entryId, request, correlationId,
            ProcurementSupplierAvlEntryStatus.Active,
            ProcurementSupplierAvlEntryStatus.Suspended,
            ProcurementSupplierAvlEntryAction.Suspended,
            "EntrySuspended", cancellationToken);

    public async Task<ProcurementSupplierAvlDto> ReinstateEntryAsync(
        Guid id,
        Guid entryId,
        ProcurementSupplierAvlEntryLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        await ChangeEntryStatusAsync(id, entryId, request, correlationId,
            ProcurementSupplierAvlEntryStatus.Suspended,
            ProcurementSupplierAvlEntryStatus.Active,
            ProcurementSupplierAvlEntryAction.Reinstated,
            "EntryReinstated", cancellationToken);

    public async Task<int> ProcessExpiryAsync(
        Guid? tenantId = null,
        DateTime? atUtc = null,
        CancellationToken cancellationToken = default)
    {
        var system = tenantId.HasValue;
        Guid targetTenant;
        if (system)
        {
            if (tenantId == Guid.Empty)
                throw Validation("SUPPLIER_AVL_TENANT_REQUIRED", "A tenant is required.");
            targetTenant = tenantId!.Value;
        }
        else
        {
            await EnsureCapabilityAsync(ManagePermission, "EXPIRY",
                $"supplier-avl-expiry-{DateTime.UtcNow:yyyyMMddHH}", cancellationToken);
            targetTenant = _currentUser.TenantId;
        }
        var now = atUtc.HasValue ? EnsureUtc(atUtc.Value) : DateTime.UtcNow;
        var rows = await Registers.GetQueryable(item =>
                item.TenantId == targetTenant && !item.IsDeleted &&
                item.Status == ProcurementSupplierAvlRegisterStatus.Published &&
                (item.ExpiresAtUtc <= now ||
                 (item.ScheduledRetirementAtUtc.HasValue &&
                  item.ScheduledRetirementAtUtc.Value <= now)))
            .Include(item => item.Entries.Where(entry => !entry.IsDeleted))
            .ToListAsync(cancellationToken);
        foreach (var entity in rows)
        {
            var correlation = $"avl-expiry-{entity.Id:N}-{now:yyyyMMddHH}";
            await RetireRegisterAsync(entity, now, correlation, system, cancellationToken);
        }
        if (rows.Count == 0) return 0;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var entity in rows)
        {
            var request = BuildEvent(entity, "Retired", ProcurementControlEventResult.Warning,
                null, Snapshot(entity), "AVL publication expired or reached scheduled replacement.",
                [], entity.LastOperationCorrelationId, now);
            if (system)
                await _controlEvents.RecordSystemAsync(targetTenant, "Supplier AVL scheduler",
                    request, cancellationToken);
            else
                await _controlEvents.RecordAsync(request, cancellationToken);
            await PublishNotificationAsync("procurement.supplier-avl.retired", entity,
                cancellationToken, entity.CreatedById);
        }
        return rows.Count;
    }

    private async Task<ProcurementSupplierAvlDto> ChangeEntryStatusAsync(
        Guid registerId,
        Guid entryId,
        ProcurementSupplierAvlEntryLifecycleRequest request,
        string correlationId,
        ProcurementSupplierAvlEntryStatus expected,
        ProcurementSupplierAvlEntryStatus target,
        ProcurementSupplierAvlEntryAction action,
        string operation,
        CancellationToken cancellationToken)
    {
        var correlation = NormalizeCorrelation(correlationId);
        var entity = await LoadAsync(registerId, true, cancellationToken);
        await EnsureCapabilityAsync(ApprovePermission, entity.RegisterCode, correlation, cancellationToken);
        EnsureStatus(entity, ProcurementSupplierAvlRegisterStatus.Published,
            "Supplier status can change only in a Published AVL register.");
        EnsureLifecycleEvidence(request.Evidence);
        var now = DateTime.UtcNow;
        if (entity.EffectiveFromUtc > now || entity.ExpiresAtUtc <= now ||
            entity.ScheduledRetirementAtUtc <= now)
            throw Conflict("SUPPLIER_AVL_REGISTER_NOT_CURRENT",
                "Supplier status can change only in the current effective AVL register.");
        var entry = entity.Entries.SingleOrDefault(item => item.Id == entryId && !item.IsDeleted)
            ?? throw NotFound("SUPPLIER_AVL_ENTRY_NOT_FOUND",
                "The AVL entry was not found in the current tenant register.");
        EnsureRowVersion(entry.RowVersion, request.RowVersion, "entry");
        if (entry.Status != expected)
            throw Conflict("SUPPLIER_AVL_ENTRY_STATUS_INVALID",
                $"Only a {expected} AVL entry can transition to {target}.");
        SupplierValidationResult? eligibility = null;
        if (target == ProcurementSupplierAvlEntryStatus.Active)
            eligibility = await EvaluateCandidateAsync(entry.BusinessPartnerId, cancellationToken);
        var before = SnapshotEntry(entry);
        entry.Status = target;
        entry.UpdatedAt = now;
        entry.UpdatedBy = ActorName;
        entry.LastModifiedById = _currentUser.UserId;
        if (target == ProcurementSupplierAvlEntryStatus.Suspended)
        {
            entry.SuspendedAtUtc = now;
            entry.SuspendedById = _currentUser.UserId;
            entry.SuspensionReason = Trim(request.Reason, 1000);
        }
        else
        {
            entry.ReinstatedAtUtc = now;
            entry.ReinstatedById = _currentUser.UserId;
            entry.SuspendedAtUtc = null;
            entry.SuspendedById = null;
            entry.SuspensionReason = null;
            entry.EligibilitySnapshotJson = Serialize(eligibility!);
            entry.EligibilityDecisionHash = eligibility!.DecisionHash;
            entry.DueDiligenceReviewId = eligibility.DueDiligenceReviewId!.Value;
            entry.RegistrationId = eligibility.RegistrationId;
            entry.EvidencePackVersionId = eligibility.EvidencePackVersionId;
            entry.QualifiedListEntryId = eligibility.QualifiedListEntries
                .Where(item => item.IsCurrent).Select(item => (Guid?)item.EntryId).FirstOrDefault();
        }
        Capture(entry);
        var after = SnapshotEntry(entry);
        var history = CreateHistory(entity, entry, action, expected, target,
            request.Reason, request.Evidence, correlation, now, after);
        await Histories.AddAsync(history);
        await Entries.UpdateAsync(entry);
        Touch(entity, operation, correlation, now);
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(entity, operation,
            target == ProcurementSupplierAvlEntryStatus.Suspended
                ? ProcurementControlEventResult.ReviewRequired
                : ProcurementControlEventResult.Succeeded,
            before, after, request.Reason, request.Evidence,
            correlation, now, cancellationToken);
        await PublishNotificationAsync(
            target == ProcurementSupplierAvlEntryStatus.Suspended
                ? "procurement.supplier-avl.entry-suspended"
                : "procurement.supplier-avl.entry-reinstated",
            entity, cancellationToken);
        return Map(entity);
    }

    private async Task<SupplierValidationResult> EvaluateCandidateAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken)
    {
        var result = await _supplierValidation.EvaluateEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = businessPartnerId,
                Boundary = SupplierEligibilityBoundary.StatusReview,
                IncludeFinancialWarnings = true,
                SkipFormalAvlMembership = true
            }, cancellationToken);
        if (!result.IsValid)
            throw Validation("SUPPLIER_AVL_CANDIDATE_INELIGIBLE",
                $"Supplier failed the current pre-AVL controls: {string.Join("; ", result.Errors)}");
        if (!result.DueDiligenceCurrent || !result.DueDiligenceReviewId.HasValue)
            throw Validation("SUPPLIER_AVL_DUE_DILIGENCE_REQUIRED",
                "A current approved clear due-diligence review is required.");
        return result;
    }

    private async Task ValidateForDecisionAsync(
        ProcurementSupplierAvlRegister entity,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var policy = await ResolvePolicyAsync(now, cancellationToken);
        if (entity.PolicyDecisionId != policy.Decision.Id ||
            !string.Equals(entity.PolicyValueHash, Hash(policy.Decision.ValueJson),
                StringComparison.OrdinalIgnoreCase))
            throw Validation("SUPPLIER_AVL_POLICY_STALE",
                "The AVL register is not bound to the exact current DEC-011 policy.");
        var entries = entity.Entries.Where(item => !item.IsDeleted).ToList();
        if (entries.Count == 0)
            throw Validation("SUPPLIER_AVL_ENTRIES_REQUIRED",
                "An AVL review requires at least one eligible supplier.");
        if (entries.Any(item => item.Status != ProcurementSupplierAvlEntryStatus.Active))
            throw Validation("SUPPLIER_AVL_ENTRY_NOT_ACTIVE",
                "Only Active candidate entries can be submitted, approved, or published.");
        foreach (var entry in entries)
        {
            var eligibility = await EvaluateCandidateAsync(entry.BusinessPartnerId, cancellationToken);
            entry.DueDiligenceReviewId = eligibility.DueDiligenceReviewId!.Value;
            entry.RegistrationId = eligibility.RegistrationId;
            entry.EvidencePackVersionId = eligibility.EvidencePackVersionId;
            entry.QualifiedListEntryId = eligibility.QualifiedListEntries
                .Where(item => item.IsCurrent).Select(item => (Guid?)item.EntryId).FirstOrDefault();
            entry.EligibilitySnapshotJson = Serialize(eligibility);
            entry.EligibilityDecisionHash = eligibility.DecisionHash;
            Capture(entry);
            await Entries.UpdateAsync(entry);
        }
        Capture(entity);
        await Registers.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProcurementSupplierAvlCurrentStateDto> BuildCurrentStateAsync(
        Guid tenantId,
        Guid businessPartnerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var policy = await TryResolvePolicyAsync(now, cancellationToken, tenantId);
        if (policy is null)
            return new ProcurementSupplierAvlCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                Code = "SUPPLIER_AVL_POLICY_UNAVAILABLE",
                Message = "No unique Published/effective/approved/evidenced DEC-011 policy is available."
            };
        var registers = await Registers.GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted &&
                item.Status == ProcurementSupplierAvlRegisterStatus.Published &&
                item.EffectiveFromUtc <= now && item.ExpiresAtUtc > now &&
                (!item.ScheduledRetirementAtUtc.HasValue ||
                 item.ScheduledRetirementAtUtc.Value > now) &&
                item.PolicyDecisionId == policy.Decision.Id &&
                item.PolicyValueHash == Hash(policy.Decision.ValueJson))
            .Include(item => item.Entries.Where(entry =>
                !entry.IsDeleted && entry.BusinessPartnerId == businessPartnerId))
            .AsNoTracking().ToListAsync(cancellationToken);
        if (registers.Count == 0)
            return new ProcurementSupplierAvlCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                PolicyAvailable = true,
                Code = "SUPPLIER_AVL_REGISTER_REQUIRED",
                Message = "No current Published AVL register exists for the effective DEC-011 policy."
            };
        if (registers.Count > 1)
            return new ProcurementSupplierAvlCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                PolicyAvailable = true,
                RegisterAvailable = true,
                Code = "SUPPLIER_AVL_REGISTER_AMBIGUOUS",
                Message = "More than one current Published AVL register applies."
            };
        var register = registers[0];
        var entries = register.Entries.Where(item => !item.IsDeleted).ToList();
        if (entries.Count == 0)
            return new ProcurementSupplierAvlCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                PolicyAvailable = true,
                RegisterAvailable = true,
                RegisterId = register.Id,
                RegisterCode = register.RegisterCode,
                RegisterVersion = register.Version,
                EffectiveFromUtc = register.EffectiveFromUtc,
                ExpiresAtUtc = register.ExpiresAtUtc,
                Code = "SUPPLIER_NOT_ON_AVL",
                Message = "The supplier is not included in the current Published AVL register."
            };
        if (entries.Count > 1)
            return new ProcurementSupplierAvlCurrentStateDto
            {
                BusinessPartnerId = businessPartnerId,
                PolicyAvailable = true,
                RegisterAvailable = true,
                Code = "SUPPLIER_AVL_ENTRY_AMBIGUOUS",
                Message = "More than one current AVL entry exists for the supplier."
            };
        var entry = entries[0];
        var current = entry.Status == ProcurementSupplierAvlEntryStatus.Active;
        return new ProcurementSupplierAvlCurrentStateDto
        {
            BusinessPartnerId = businessPartnerId,
            PolicyAvailable = true,
            RegisterAvailable = true,
            IsCurrent = current,
            RegisterId = register.Id,
            RegisterCode = register.RegisterCode,
            RegisterVersion = register.Version,
            EntryId = entry.Id,
            EntryStatus = entry.Status,
            EffectiveFromUtc = register.EffectiveFromUtc,
            ExpiresAtUtc = register.ExpiresAtUtc,
            IntegrityHash = entry.IntegrityHash,
            Code = current ? "SUPPLIER_AVL_CURRENT" :
                entry.Status == ProcurementSupplierAvlEntryStatus.Suspended
                    ? "SUPPLIER_AVL_SUSPENDED"
                    : "SUPPLIER_AVL_EXPIRED",
            Message = current
                ? "The supplier is active in the current Published AVL register."
                : "The supplier AVL entry is suspended or expired."
        };
    }

    private async Task RetireRegisterAsync(
        ProcurementSupplierAvlRegister entity,
        DateTime now,
        string correlation,
        bool system,
        CancellationToken cancellationToken)
    {
        entity.Status = ProcurementSupplierAvlRegisterStatus.Retired;
        entity.RetiredAtUtc = now;
        entity.RetiredById = system ? null : _currentUser.UserId;
        foreach (var entry in entity.Entries.Where(item =>
                     !item.IsDeleted && item.Status != ProcurementSupplierAvlEntryStatus.Expired))
        {
            var before = entry.Status;
            entry.Status = ProcurementSupplierAvlEntryStatus.Expired;
            entry.ExpiredAtUtc = now;
            entry.UpdatedAt = now;
            entry.UpdatedBy = system ? "Supplier AVL scheduler" : ActorName;
            entry.LastModifiedById = system ? null : _currentUser.UserId;
            Capture(entry);
            var historyCorrelation = NormalizeDerivedCorrelation(correlation, entry.Id);
            var history = CreateHistory(entity, entry,
                ProcurementSupplierAvlEntryAction.Expired, before,
                ProcurementSupplierAvlEntryStatus.Expired,
                "AVL register expired or was replaced.", [], historyCorrelation,
                now, SnapshotEntry(entry), system ? entity.CreatedById : _currentUser.UserId);
            await Histories.AddAsync(history);
            await Entries.UpdateAsync(entry);
        }
        Touch(entity, "Retired", correlation, now, system);
        Capture(entity);
        await Registers.UpdateAsync(entity);
    }

    private ProcurementSupplierAvlEntryStatusHistory CreateHistory(
        ProcurementSupplierAvlRegister register,
        ProcurementSupplierAvlEntry entry,
        ProcurementSupplierAvlEntryAction action,
        ProcurementSupplierAvlEntryStatus before,
        ProcurementSupplierAvlEntryStatus after,
        string reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlation,
        DateTime now,
        object snapshot,
        Guid? actorId = null)
    {
        var actor = actorId.GetValueOrDefault(_currentUser.UserId);
        var row = new ProcurementSupplierAvlEntryStatusHistory
        {
            TenantId = register.TenantId,
            RegisterId = register.Id,
            EntryId = entry.Id,
            Action = action,
            BeforeStatus = before,
            AfterStatus = after,
            Reason = Trim(reason, 1000) ?? action.ToString(),
            ActorUserId = actor,
            OccurredAtUtc = now,
            CorrelationId = correlation,
            EvidenceJson = Serialize(evidence),
            SnapshotJson = Serialize(snapshot),
            CreatedAt = now,
            CreatedBy = ActorName,
            CreatedById = actor
        };
        row.IntegrityHash = Hash(Serialize(new
        {
            row.TenantId,
            row.RegisterId,
            row.EntryId,
            row.Action,
            row.BeforeStatus,
            row.AfterStatus,
            row.Reason,
            row.ActorUserId,
            row.OccurredAtUtc,
            row.CorrelationId,
            row.EvidenceJson,
            row.SnapshotJson
        }));
        return row;
    }

    private async Task<ProcurementSupplierAvlRegister> LoadAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = RegisterQuery();
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound("SUPPLIER_AVL_REGISTER_NOT_FOUND",
                "The AVL register was not found in the current tenant.");
    }

    private IQueryable<ProcurementSupplierAvlRegister> RegisterQuery() =>
        Registers.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Entries.Where(entry => !entry.IsDeleted))
                .ThenInclude(entry => entry.BusinessPartner)
            .Include(item => item.Entries.Where(entry => !entry.IsDeleted))
                .ThenInclude(entry => entry.DueDiligenceReview)
            .Include(item => item.Entries.Where(entry => !entry.IsDeleted))
                .ThenInclude(entry => entry.StatusHistory.Where(history => !history.IsDeleted))
            .Include(item => item.PublicationSnapshots.Where(snapshot => !snapshot.IsDeleted));

    private async Task<PolicyResolution?> TryResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken,
        Guid? tenantId = null)
    {
        var targetTenant = tenantId ?? _currentUser.TenantId;
        var matches = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
            .GetQueryable(item => item.TenantId == targetTenant &&
                item.DecisionKey == "DEC-011" && !item.IsDeleted &&
                item.Profile.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.Profile.EffectiveFrom <= now &&
                (!item.Profile.EffectiveTo.HasValue || item.Profile.EffectiveTo.Value >= now) &&
                item.Status == ProcurementConfigurationDecisionStatus.Approved &&
                item.ApprovalStatus == ProcurementConfigurationApprovalStatus.Approved &&
                item.EvidenceStatus == ProcurementConfigurationEvidenceStatus.Verified &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom.Value <= now) &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .Include(item => item.Profile).AsNoTracking().ToListAsync(cancellationToken);
        if (matches.Count != 1) return null;
        try
        {
            var value = JsonSerializer.Deserialize<ProcurementSupplierRiskDecisionValueDto>(
                matches[0].ValueJson, JsonOptions);
            return value is null || value.ReviewFrequencyMonths < 1
                ? null
                : new PolicyResolution(matches[0], value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<PolicyResolution> ResolvePolicyAsync(
        DateTime now,
        CancellationToken cancellationToken) =>
        await TryResolvePolicyAsync(now, cancellationToken)
        ?? throw Validation("SUPPLIER_AVL_POLICY_UNAVAILABLE",
            "A unique Published/effective/approved/evidenced DEC-011 supplier-risk policy is required.");

    private async Task<WorkflowDefinition> ValidateWorkflowAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw Validation("SUPPLIER_AVL_WORKFLOW_REQUIRED",
                "A Published shared workflow definition is required.");
        return await WorkflowDefinitions.GetQueryable(item =>
                item.Id == id && item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted && item.IsActive &&
                item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Validation("SUPPLIER_AVL_WORKFLOW_INVALID",
                "The selected shared workflow is unavailable, inactive, foreign, or unpublished.");
    }

    private async Task EnsureWorkflowOutcomeAsync(
        ProcurementSupplierAvlRegister entity,
        bool approving,
        CancellationToken cancellationToken)
    {
        if (!entity.WorkflowInstanceId.HasValue)
            throw Conflict("SUPPLIER_AVL_WORKFLOW_INSTANCE_REQUIRED",
                "The shared workflow instance was not recorded.");
        var instance = await WorkflowInstances.GetQueryable(item =>
                item.TenantId == entity.TenantId &&
                item.Id == entity.WorkflowInstanceId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw Conflict("SUPPLIER_AVL_WORKFLOW_INSTANCE_NOT_FOUND",
                "The shared workflow instance is unavailable.");
        if (instance.WorkflowDefinitionId != entity.WorkflowDefinitionId ||
            instance.EntityId != entity.Id)
            throw Conflict("SUPPLIER_AVL_WORKFLOW_BINDING_INVALID",
                "The shared workflow instance does not belong to this AVL register.");
        if (approving && instance.Status != WorkflowInstanceStatus.Completed)
            throw Conflict("SUPPLIER_AVL_WORKFLOW_NOT_APPROVED",
                "Only a Completed shared workflow permits approval.");
        if (!approving &&
            instance.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw Conflict("SUPPLIER_AVL_WORKFLOW_NOT_REJECTED",
                "Only a Cancelled or Failed shared workflow permits rejection.");
    }

    private async Task EnsureIndependentActorAsync(
        Guid? prohibitedActor,
        string sourceReference,
        string correlation,
        CancellationToken cancellationToken)
    {
        if (!prohibitedActor.HasValue || prohibitedActor == Guid.Empty)
            throw Conflict("SUPPLIER_AVL_INITIATOR_NOT_RECORDED",
                "The submitting actor was not recorded.");
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = "SOD-INITIATOR-APPROVER",
            SourceType = SourceType,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = [prohibitedActor.Value]
        }, correlation, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierAvlAuthorizationException(decision.Message);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlation,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierAvlAuthorizationException(
                "Supplier portal users cannot administer the AVL.");
        if (IsAdministrator()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = SourceType,
                SourceReference = sourceReference
            }, correlation, cancellationToken);
        if (!decision.Allowed)
            throw new ProcurementSupplierAvlAuthorizationException(decision.Message);
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw new ProcurementSupplierAvlAuthorizationException(
                "Supplier portal users cannot access AVL administration.");
        if (IsAdministrator() ||
            _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role =>
                ProcurementAccessControlRegistry.FindRole(role) is not null))
            return;
        throw new ProcurementSupplierAvlAuthorizationException(
            "A supplier-review or TDC procurement role is required.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw new ProcurementSupplierAvlAuthorizationException(
                "An authenticated tenant context is required.");
    }

    private bool IsAdministrator() =>
        _currentUser.HasRole("Admin") || _currentUser.HasRole("Administrator") ||
        _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");

    private async Task RecordEventAsync(
        ProcurementSupplierAvlRegister entity,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlation,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(BuildEvent(entity, action, result, before, after,
            reason, evidence, correlation, occurredAtUtc), cancellationToken);

    private static ProcurementControlEventWriteRequest BuildEvent(
        ProcurementSupplierAvlRegister entity,
        string action,
        ProcurementControlEventResult result,
        object? before,
        object? after,
        string? reason,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        string correlation,
        DateTime occurredAtUtc) => new()
    {
        EventKey = ProcurementControlEventKey.Create(
            "supplier-avl", entity.TenantId, entity.Id, $"{action}-{correlation}"),
        EventType = EventType,
        Action = action,
        Result = result,
        RuleCode = entity.Status.ToString(),
        RuleId = entity.PolicyDecisionId,
        RuleVersion = $"{entity.PolicyProfileCode}/v{entity.PolicyProfileVersion}",
        DecisionKeys = DecisionKeys.ToList(),
        SourceType = SourceType,
        SourceId = entity.Id,
        SourceReference = $"{entity.RegisterCode}/v{entity.Version}",
        Reason = Trim(reason, 1000),
        InputValues = new
        {
            entity.ReviewYear,
            entity.PolicyDecisionId,
            entity.WorkflowDefinitionId,
            entity.EffectiveFromUtc,
            entity.ExpiresAtUtc
        },
        ResultValues = new
        {
            entity.Status,
            entity.ScheduledRetirementAtUtc,
            entity.IntegrityHash,
            Entries = entity.Entries.Where(item => !item.IsDeleted)
                .OrderBy(item => item.BusinessPartnerId)
                .Select(item => new
                {
                    item.Id,
                    item.BusinessPartnerId,
                    item.Status,
                    item.DueDiligenceReviewId,
                    item.EligibilityDecisionHash,
                    item.IntegrityHash
                })
        },
        Before = before,
        After = after,
        CorrelationId = correlation,
        CausationId = correlation,
        OccurredAtUtc = occurredAtUtc,
        Evidence = evidence.Select(item => new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = item.ReferenceKind,
            ReferenceId = item.ReferenceId,
            Reference = item.Reference,
            Label = item.Label,
            RequirementKey = item.RequirementKey
        }).ToList()
    };

    private async Task PublishNotificationAsync(
        string topic,
        ProcurementSupplierAvlRegister entity,
        CancellationToken cancellationToken,
        Guid? actorId = null)
    {
        try
        {
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = entity.TenantId,
                TopicKey = topic,
                NotificationType = "ProcurementSupplierAvlControl",
                EntityType = SourceType,
                EntityId = entity.Id,
                TriggeredByUserId = actorId ?? (_currentUser.UserId == Guid.Empty
                    ? entity.CreatedById ?? Guid.Empty
                    : _currentUser.UserId),
                Data = new Dictionary<string, object>
                {
                    ["registerCode"] = entity.RegisterCode,
                    ["version"] = entity.Version,
                    ["reviewYear"] = entity.ReviewYear,
                    ["status"] = entity.Status.ToString(),
                    ["effectiveFromUtc"] = entity.EffectiveFromUtc,
                    ["expiresAtUtc"] = entity.ExpiresAtUtc,
                    ["entryCount"] = entity.Entries.Count(item => !item.IsDeleted)
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish AVL notification {Topic} for {RegisterId}",
                topic, entity.Id);
        }
    }

    private static ProcurementSupplierAvlListItemDto MapList(
        ProcurementSupplierAvlRegister item)
    {
        var entries = item.Entries.Where(entry => !entry.IsDeleted).ToList();
        return new ProcurementSupplierAvlListItemDto
        {
            Id = item.Id,
            RegisterCode = item.RegisterCode,
            Version = item.Version,
            ReviewYear = item.ReviewYear,
            Status = item.Status,
            EffectiveFromUtc = item.EffectiveFromUtc,
            ExpiresAtUtc = item.ExpiresAtUtc,
            ScheduledRetirementAtUtc = item.ScheduledRetirementAtUtc,
            EntryCount = entries.Count,
            ActiveEntryCount = entries.Count(entry =>
                entry.Status == ProcurementSupplierAvlEntryStatus.Active),
            SuspendedEntryCount = entries.Count(entry =>
                entry.Status == ProcurementSupplierAvlEntryStatus.Suspended),
            PolicyProfileCode = item.PolicyProfileCode,
            PolicyProfileVersion = item.PolicyProfileVersion,
            AllowedActions = AllowedActions(item.Status),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static ProcurementSupplierAvlDto Map(ProcurementSupplierAvlRegister item)
    {
        var list = MapList(item);
        return new ProcurementSupplierAvlDto
        {
            Id = list.Id,
            RegisterCode = list.RegisterCode,
            Version = list.Version,
            ReviewYear = list.ReviewYear,
            Status = list.Status,
            EffectiveFromUtc = list.EffectiveFromUtc,
            ExpiresAtUtc = list.ExpiresAtUtc,
            ScheduledRetirementAtUtc = list.ScheduledRetirementAtUtc,
            EntryCount = list.EntryCount,
            ActiveEntryCount = list.ActiveEntryCount,
            SuspendedEntryCount = list.SuspendedEntryCount,
            PolicyProfileCode = list.PolicyProfileCode,
            PolicyProfileVersion = list.PolicyProfileVersion,
            AllowedActions = list.AllowedActions,
            RowVersion = list.RowVersion,
            PolicyDecisionId = item.PolicyDecisionId,
            PolicyProfileId = item.PolicyProfileId,
            PolicyValueHash = item.PolicyValueHash,
            PolicySnapshotJson = item.PolicySnapshotJson,
            ReviewFrequencyMonths = item.ReviewFrequencyMonths,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowInstanceId = item.WorkflowInstanceId,
            Notes = item.Notes,
            ReviewComment = item.ReviewComment,
            SubmittedById = item.SubmittedById,
            SubmittedAtUtc = item.SubmittedAtUtc,
            ApprovedById = item.ApprovedById,
            ApprovedAtUtc = item.ApprovedAtUtc,
            PublishedById = item.PublishedById,
            PublishedAtUtc = item.PublishedAtUtc,
            IntegrityHash = item.IntegrityHash,
            Entries = item.Entries.Where(entry => !entry.IsDeleted)
                .OrderBy(entry => entry.BusinessPartner.PartnerCode)
                .Select(MapEntry).ToList(),
            PublicationSnapshots = item.PublicationSnapshots
                .Where(snapshot => !snapshot.IsDeleted)
                .OrderBy(snapshot => snapshot.Sequence)
                .Select(snapshot => new ProcurementSupplierAvlPublicationSnapshotDto
                {
                    Id = snapshot.Id,
                    Sequence = snapshot.Sequence,
                    PublishedAtUtc = snapshot.PublishedAtUtc,
                    PublishedById = snapshot.PublishedById,
                    IntegrityHash = snapshot.IntegrityHash
                }).ToList()
        };
    }

    private static ProcurementSupplierAvlEntryDto MapEntry(ProcurementSupplierAvlEntry item) => new()
    {
        Id = item.Id,
        BusinessPartnerId = item.BusinessPartnerId,
        PartnerCode = item.BusinessPartner?.PartnerCode ?? string.Empty,
        PartnerName = item.BusinessPartner?.PartnerName ?? string.Empty,
        Status = item.Status,
        DueDiligenceReviewId = item.DueDiligenceReviewId,
        DueDiligenceReviewReference = item.DueDiligenceReview?.ReviewReference,
        RegistrationId = item.RegistrationId,
        EvidencePackVersionId = item.EvidencePackVersionId,
        QualifiedListEntryId = item.QualifiedListEntryId,
        AddedAtUtc = item.AddedAtUtc,
        SuspendedAtUtc = item.SuspendedAtUtc,
        SuspensionReason = item.SuspensionReason,
        ReinstatedAtUtc = item.ReinstatedAtUtc,
        ExpiredAtUtc = item.ExpiredAtUtc,
        EligibilityDecisionHash = item.EligibilityDecisionHash,
        IntegrityHash = item.IntegrityHash,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        StatusHistory = item.StatusHistory.Where(history => !history.IsDeleted)
            .OrderByDescending(history => history.OccurredAtUtc)
            .Select(history => new ProcurementSupplierAvlEntryStatusHistoryDto
            {
                Id = history.Id,
                Action = history.Action,
                BeforeStatus = history.BeforeStatus,
                AfterStatus = history.AfterStatus,
                Reason = history.Reason,
                ActorUserId = history.ActorUserId,
                OccurredAtUtc = history.OccurredAtUtc,
                CorrelationId = history.CorrelationId,
                IntegrityHash = history.IntegrityHash
            }).ToList()
    };

    private static IReadOnlyList<string> AllowedActions(
        ProcurementSupplierAvlRegisterStatus status) => status switch
    {
        ProcurementSupplierAvlRegisterStatus.Draft => ["Edit", "AddEntry", "RemoveEntry", "Submit"],
        ProcurementSupplierAvlRegisterStatus.PendingApproval => ["Approve", "Reject"],
        ProcurementSupplierAvlRegisterStatus.Approved => ["Publish"],
        ProcurementSupplierAvlRegisterStatus.Published => ["SuspendEntry", "ReinstateEntry"],
        _ => ["View"]
    };

    private static object Snapshot(ProcurementSupplierAvlRegister item) => new
    {
        item.Id,
        item.TenantId,
        item.RegisterCode,
        item.Version,
        item.ReviewYear,
        item.Status,
        item.EffectiveFromUtc,
        item.ExpiresAtUtc,
        item.ScheduledRetirementAtUtc,
        item.SupersededByRegisterId,
        item.PolicyDecisionId,
        item.PolicyProfileId,
        item.PolicyProfileCode,
        item.PolicyProfileVersion,
        item.ReviewFrequencyMonths,
        item.PolicyValueHash,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.SubmittedById,
        item.SubmittedAtUtc,
        item.ApprovedById,
        item.ApprovedAtUtc,
        item.RejectedById,
        item.RejectedAtUtc,
        item.PublishedById,
        item.PublishedAtUtc,
        item.RetiredById,
        item.RetiredAtUtc,
        Entries = item.Entries.Where(entry => !entry.IsDeleted)
            .OrderBy(entry => entry.BusinessPartnerId)
            .Select(SnapshotEntry)
    };

    private static object SnapshotEntry(ProcurementSupplierAvlEntry item) => new
    {
        item.Id,
        item.TenantId,
        item.RegisterId,
        item.BusinessPartnerId,
        item.Status,
        item.DueDiligenceReviewId,
        item.RegistrationId,
        item.EvidencePackVersionId,
        item.QualifiedListEntryId,
        item.AddedAtUtc,
        item.AddedById,
        item.SuspendedAtUtc,
        item.SuspendedById,
        item.SuspensionReason,
        item.ReinstatedAtUtc,
        item.ReinstatedById,
        item.ExpiredAtUtc,
        item.EligibilityDecisionHash,
        item.IntegrityHash
    };

    private static void Capture(ProcurementSupplierAvlRegister item)
    {
        item.SnapshotJson = Serialize(Snapshot(item));
        item.IntegrityHash = Hash(item.SnapshotJson);
    }

    private static void Capture(ProcurementSupplierAvlEntry item)
    {
        item.IntegrityHash = Hash(Serialize(new
        {
            item.Id,
            item.TenantId,
            item.RegisterId,
            item.BusinessPartnerId,
            item.Status,
            item.DueDiligenceReviewId,
            item.RegistrationId,
            item.EvidencePackVersionId,
            item.QualifiedListEntryId,
            item.AddedAtUtc,
            item.AddedById,
            item.SuspendedAtUtc,
            item.SuspendedById,
            item.SuspensionReason,
            item.ReinstatedAtUtc,
            item.ReinstatedById,
            item.ExpiredAtUtc,
            item.EligibilityDecisionHash
        }));
    }

    private void Touch(
        ProcurementSupplierAvlRegister item,
        string operation,
        string correlation,
        DateTime now,
        bool system = false)
    {
        item.LastOperation = operation;
        item.LastOperationCorrelationId = correlation;
        item.UpdatedAt = now;
        item.UpdatedBy = system ? "Supplier AVL scheduler" : ActorName;
        item.LastModifiedById = system ? null : _currentUser.UserId;
    }

    private static bool IsReplay(
        ProcurementSupplierAvlRegister item,
        string operation,
        string correlation) =>
        item.LastOperation == operation &&
        item.LastOperationCorrelationId == correlation;

    private static void EnsureStatus(
        ProcurementSupplierAvlRegister item,
        ProcurementSupplierAvlRegisterStatus expected,
        string message)
    {
        if (item.Status != expected)
            throw Conflict("SUPPLIER_AVL_STATUS_INVALID", message);
    }

    private static void EnsureRowVersion(byte[] current, string supplied, string target)
    {
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch (FormatException)
        {
            throw Conflict("SUPPLIER_AVL_VERSION_INVALID",
                $"{target} RowVersion must be valid Base64.");
        }
        if (!current.SequenceEqual(parsed))
            throw Conflict("SUPPLIER_AVL_VERSION_CONFLICT",
                $"The {target} changed. Reload before continuing.");
    }

    private static void EnsureLifecycleEvidence(
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence)
    {
        if (evidence.Count == 0 || evidence.Any(item =>
                !item.ReferenceId.HasValue && string.IsNullOrWhiteSpace(item.Reference)))
            throw Validation("SUPPLIER_AVL_LIFECYCLE_EVIDENCE_REQUIRED",
                "Submission, approval, publication, suspension, and reinstatement require shared evidence.");
    }

    private async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private string ActorName => string.IsNullOrWhiteSpace(_currentUser.FullName)
        ? _currentUser.Username
        : _currentUser.FullName;

    private static string NormalizeCorrelation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Validation("SUPPLIER_AVL_CORRELATION_REQUIRED",
                "X-Correlation-ID is required.");
        return value.Trim().Length <= 100 ? value.Trim() : Hash(value);
    }

    private static string NormalizeDerivedCorrelation(string correlation, Guid id)
    {
        var value = $"{correlation}-{id:N}";
        return value.Length <= 100 ? value : Hash(value);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? Trim(string? value, int length) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= length ? value.Trim() : value.Trim()[..length];
    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ProcurementSupplierAvlNotFoundException NotFound(string code, string message) =>
        new(code, message);
    private static ProcurementSupplierAvlValidationException Validation(string code, string message) =>
        new(code, message);
    private static ProcurementSupplierAvlConflictException Conflict(string code, string message) =>
        new(code, message);

    private sealed record PolicyResolution(
        ProcurementConfigurationDecision Decision,
        ProcurementSupplierRiskDecisionValueDto Value);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
