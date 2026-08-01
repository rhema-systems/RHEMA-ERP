using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementMasterDataChangeService : IProcurementMasterDataChangeService
{
    private const string EventType = "MasterDataChange";
    private static readonly string[] DecisionKeys = Enumerable.Range(1, 14)
        .Select(number => $"DEC-{number:000}")
        .ToArray();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IWorkflowInstanceService _workflowInstances;
    private readonly INotificationTopicPublisher _notificationTopics;
    private readonly ILogger<ProcurementMasterDataChangeService> _logger;
    private readonly IInventoryItemIdentifierService? _inventoryIdentifiers;

    public ProcurementMasterDataChangeService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementControlEventService controlEvents,
        IWorkflowInstanceService workflowInstances,
        INotificationTopicPublisher notificationTopics,
        ILogger<ProcurementMasterDataChangeService> logger,
        IInventoryItemIdentifierService? inventoryIdentifiers = null)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _controlEvents = controlEvents;
        _workflowInstances = workflowInstances;
        _notificationTopics = notificationTopics;
        _logger = logger;
        _inventoryIdentifiers = inventoryIdentifiers;
    }

    private IGenericRepository<ProcurementMasterDataControlPolicy> Policies => _unitOfWork.Repository<ProcurementMasterDataControlPolicy>();
    private IGenericRepository<ProcurementMasterDataChangeRequest> Requests => _unitOfWork.Repository<ProcurementMasterDataChangeRequest>();
    private IGenericRepository<ProcurementMasterDataChangeEvidenceLink> EvidenceLinks => _unitOfWork.Repository<ProcurementMasterDataChangeEvidenceLink>();
    private IGenericRepository<WorkflowEvidenceDocument> WorkflowEvidence => _unitOfWork.Repository<WorkflowEvidenceDocument>();
    private IGenericRepository<FileUploadRecord> FileUploads => _unitOfWork.Repository<FileUploadRecord>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions => _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<WorkflowInstance> WorkflowInstanceRows => _unitOfWork.Repository<WorkflowInstance>();

    public Task<IReadOnlyList<ProcurementMasterDataResourceDefinitionDto>> GetRegistryAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return Task.FromResult<IReadOnlyList<ProcurementMasterDataResourceDefinitionDto>>(ProcurementMasterDataResourceRegistry.ToDtos());
    }

    public async Task<ProcurementMasterDataChangeSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var now = DateTime.UtcNow;
        var policyQuery = Policies.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        var requestQuery = Requests.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        return new ProcurementMasterDataChangeSummaryDto
        {
            PolicyCount = await policyQuery.CountAsync(cancellationToken),
            EffectivePolicyCount = await policyQuery.CountAsync(item => item.Status == ProcurementMasterDataPolicyStatus.Active &&
                item.EffectiveFromUtc <= now && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc >= now), cancellationToken),
            DraftCount = await requestQuery.CountAsync(item => item.Status == ProcurementMasterDataChangeStatus.Draft, cancellationToken),
            PendingApprovalCount = await requestQuery.CountAsync(item => item.Status == ProcurementMasterDataChangeStatus.PendingApproval, cancellationToken),
            ApprovedAwaitingEffectiveDateCount = await requestQuery.CountAsync(item => item.Status == ProcurementMasterDataChangeStatus.Approved, cancellationToken),
            RevalidationFailedCount = await requestQuery.CountAsync(item => item.Status == ProcurementMasterDataChangeStatus.RevalidationFailed, cancellationToken),
            AppliedCount = await requestQuery.CountAsync(item => item.Status == ProcurementMasterDataChangeStatus.Applied, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<ProcurementMasterDataPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var rows = await Policies.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.WorkflowDefinition)
            .AsNoTracking()
            .OrderBy(item => item.ResourceType).ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        return rows.Select(MapPolicy).ToList();
    }

    public async Task<ProcurementMasterDataPolicyDto> SavePolicyAsync(
        Guid? id,
        SaveProcurementMasterDataPolicyRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        ValidatePolicyRequest(request);
        await ValidateWorkflowDefinitionAsync(request.WorkflowDefinitionId, cancellationToken);
        var now = DateTime.UtcNow;
        ProcurementMasterDataControlPolicy entity;
        object? before = null;

        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            if (id.HasValue)
            {
                entity = await Policies.GetQueryable(item => item.Id == id.Value && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ProcurementMasterDataChangeNotFoundException("The control policy was not found in the current tenant.");
                if (entity.Status != ProcurementMasterDataPolicyStatus.Draft)
                    throw new ProcurementMasterDataChangeConflictException("Only a Draft control policy can be edited; create the next version instead.");
                EnsureRowVersion(entity.RowVersion, request.RowVersion, "policy");
                if (entity.ResourceType != request.ResourceType)
                    throw new ProcurementMasterDataChangeConflictException("A policy resource type is immutable after creation.");
                before = PolicyAuditShape(entity);
            }
            else
            {
                var existingDraft = await Policies.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                        item.ResourceType == request.ResourceType && item.Status == ProcurementMasterDataPolicyStatus.Draft)
                    .AnyAsync(cancellationToken);
                if (existingDraft)
                    throw new ProcurementMasterDataChangeConflictException("This resource already has a Draft control policy. Edit that version before creating another.");
                var latest = await Policies.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.ResourceType == request.ResourceType)
                    .OrderByDescending(item => item.Version).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
                entity = new ProcurementMasterDataControlPolicy
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUser.TenantId,
                    PolicyKey = latest?.PolicyKey ?? Guid.NewGuid(),
                    Version = (latest?.Version ?? 0) + 1,
                    ResourceType = request.ResourceType,
                    SupersedesPolicyId = latest?.Id,
                    CreatedAt = now,
                    CreatedBy = _currentUser.Username,
                    CreatedById = _currentUser.UserId
                };
                await Policies.AddAsync(entity);
            }

            ApplyPolicy(entity, request);
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            if (id.HasValue) await Policies.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(id.HasValue ? "PolicyUpdated" : "PolicyCreated", ProcurementControlEventResult.Succeeded,
                entity, entity.Id, $"POLICY-{entity.ResourceType}-V{entity.Version}", correlationId, request.Description,
                before, PolicyAuditShape(entity), null, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return await LoadPolicyDtoAsync(entity.Id, cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataPolicyDto> ActivatePolicyAsync(
        Guid id,
        ProcurementMasterDataPolicyLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        EnsureReason(request.Reason);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadPolicyAsync(id, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "policy");
            if (entity.Status != ProcurementMasterDataPolicyStatus.Draft)
                throw new ProcurementMasterDataChangeConflictException("Only a Draft control policy can be activated.");
            ValidateConfiguredRoles(DeserializeRoles(entity.MakerRolesJson), DeserializeRoles(entity.CheckerRolesJson));
            await ValidateWorkflowDefinitionAsync(entity.WorkflowDefinitionId, cancellationToken);
            var previous = await Policies.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                    item.ResourceType == entity.ResourceType && item.Status == ProcurementMasterDataPolicyStatus.Active && item.Id != entity.Id)
                .ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var activatesInFuture = entity.EffectiveFromUtc > now;
            if (activatesInFuture && previous.Any(item => item.EffectiveFromUtc > now))
                throw new ProcurementMasterDataChangeConflictException(
                    "A future replacement is already Active for this master-data policy. Retire it before scheduling another replacement.");
            foreach (var item in previous)
            {
                var isCurrentlyEffective = item.EffectiveFromUtc <= now &&
                    (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= now);
                if (activatesInFuture && isCurrentlyEffective)
                {
                    if (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc.Value >= entity.EffectiveFromUtc)
                        item.EffectiveToUtc = entity.EffectiveFromUtc.AddTicks(-1);
                    item.UpdatedAt = now;
                    item.UpdatedBy = _currentUser.Username;
                    item.LastModifiedById = _currentUser.UserId;
                    await Policies.UpdateAsync(item);
                    continue;
                }

                item.Status = ProcurementMasterDataPolicyStatus.Retired;
                item.EffectiveToUtc = item.EffectiveToUtc.HasValue && item.EffectiveToUtc.Value < now ? item.EffectiveToUtc : now;
                item.RetiredAtUtc = now;
                item.RetiredById = _currentUser.UserId;
                item.UpdatedAt = now;
                item.UpdatedBy = _currentUser.Username;
                item.LastModifiedById = _currentUser.UserId;
                await Policies.UpdateAsync(item);
            }
            if (previous.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);
            var before = PolicyAuditShape(entity);
            entity.Status = ProcurementMasterDataPolicyStatus.Active;
            entity.ActivatedAtUtc = now;
            entity.ActivatedById = _currentUser.UserId;
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Policies.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync("PolicyActivated", ProcurementControlEventResult.Succeeded, entity, entity.Id,
                $"POLICY-{entity.ResourceType}-V{entity.Version}", correlationId, request.Reason, before, PolicyAuditShape(entity), null, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return await LoadPolicyDtoAsync(entity.Id, cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataPolicyDto> RetirePolicyAsync(
        Guid id,
        ProcurementMasterDataPolicyLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureSuperAdministrator();
        EnsureReason(request.Reason);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadPolicyAsync(id, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "policy");
            if (entity.Status != ProcurementMasterDataPolicyStatus.Active)
                throw new ProcurementMasterDataChangeConflictException("Only an Active control policy can be retired.");
            var before = PolicyAuditShape(entity);
            var now = DateTime.UtcNow;
            entity.Status = ProcurementMasterDataPolicyStatus.Retired;
            entity.EffectiveToUtc = entity.EffectiveToUtc.HasValue && entity.EffectiveToUtc.Value < now ? entity.EffectiveToUtc : now;
            entity.RetiredAtUtc = now;
            entity.RetiredById = _currentUser.UserId;
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Policies.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync("PolicyRetired", ProcurementControlEventResult.Succeeded, entity, entity.Id,
                $"POLICY-{entity.ResourceType}-V{entity.Version}", correlationId, request.Reason, before, PolicyAuditShape(entity), null, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return await LoadPolicyDtoAsync(entity.Id, cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataChangePageDto> SearchAsync(
        ProcurementMasterDataChangeSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureReader();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = Requests.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        if (request.ResourceType.HasValue) query = query.Where(item => item.ResourceType == request.ResourceType.Value);
        if (request.Status.HasValue) query = query.Where(item => item.Status == request.Status.Value);
        if (request.TargetId.HasValue) query = query.Where(item => item.TargetId == request.TargetId.Value);
        if (request.MakerUserId.HasValue) query = query.Where(item => item.MakerUserId == request.MakerUserId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.RequestNumber.Contains(search) || item.TargetReference.Contains(search) || item.Reason.Contains(search));
        }
        var total = await query.CountAsync(cancellationToken);
        var rows = await IncludeRequestDetails(query)
            .AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new ProcurementMasterDataChangePageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = rows.Select(MapRequest).ToList()
        };
    }

    public async Task<ProcurementMasterDataChangeDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureReader();
        return MapRequest(await LoadRequestAsync(id, false, cancellationToken));
    }

    public async Task<ProcurementMasterDataChangeDto> SaveDraftAsync(
        Guid? id,
        SaveProcurementMasterDataChangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureReason(request.Reason);
        if (request.EffectiveAtUtc == default)
            throw new ProcurementMasterDataChangeValidationException("EFFECTIVE_DATE_REQUIRED", "EffectiveAtUtc is required.");
        var policy = await GetEffectivePolicyAsync(request.ResourceType, DateTime.UtcNow, cancellationToken)
            ?? throw new ProcurementMasterDataChangeConflictException("No Active and effective maker-checker policy protects this resource.");
        EnsureMaker(policy);
        var target = await LoadTargetAsync(request.ResourceType, request.TargetId, null, false, cancellationToken);
        var beforeJson = Snapshot(target.Entity, request.ResourceType, target.Kind);
        var normalizedPatch = NormalizeAndValidatePatch(target.Entity, request.ResourceType, target.Kind, request.ProposedChangesJson);
        await ValidateTargetRelationsAsync(target.Entity, target.Kind, request.ResourceType, cancellationToken);
        var now = DateTime.UtcNow;
        ProcurementMasterDataChangeRequest entity;
        object? before = null;

        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            if (id.HasValue)
            {
                entity = await LoadRequestAsync(id.Value, true, cancellationToken);
                if (entity.Status != ProcurementMasterDataChangeStatus.Draft)
                    throw new ProcurementMasterDataChangeConflictException("Only a Draft change request can be edited.");
                EnsureRowVersion(entity.RowVersion, request.RowVersion, "change request");
                if (entity.MakerUserId != _currentUser.UserId)
                    throw new ProcurementMasterDataChangeAuthorizationException("Only the original maker can edit this Draft request.");
                if (entity.ResourceType != request.ResourceType || entity.TargetId != target.Entity.Id || entity.TargetKind != target.Kind)
                    throw new ProcurementMasterDataChangeConflictException("Resource type and target are immutable after request creation.");
                before = RequestAuditShape(entity);
                foreach (var link in entity.EvidenceLinks.Where(item => !item.IsDeleted))
                {
                    link.IsDeleted = true;
                    link.DeletedAt = now;
                    link.DeletedBy = _currentUser.Username;
                    await EvidenceLinks.UpdateAsync(link);
                }
                if (entity.EvidenceLinks.Any(item => item.IsDeleted))
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                entity = new ProcurementMasterDataChangeRequest
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUser.TenantId,
                    RequestNumber = $"MDC-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                    PolicyId = policy.Id,
                    PolicyVersion = policy.Version,
                    ResourceType = request.ResourceType,
                    TargetKind = target.Kind,
                    TargetId = target.Entity.Id,
                    MakerUserId = _currentUser.UserId,
                    CorrelationId = NormalizeCorrelation(correlationId),
                    CreatedAt = now,
                    CreatedBy = _currentUser.Username,
                    CreatedById = _currentUser.UserId
                };
                await Requests.AddAsync(entity);
            }

            entity.PolicyId = policy.Id;
            entity.PolicyVersion = policy.Version;
            entity.TargetReference = target.Reference;
            entity.BeforeJson = beforeJson;
            entity.BeforeHash = Hash(beforeJson);
            entity.ProposedChangesJson = normalizedPatch;
            entity.ProposedChangesHash = Hash(normalizedPatch);
            entity.Reason = request.Reason.Trim();
            entity.EffectiveAtUtc = EnsureUtc(request.EffectiveAtUtc);
            entity.WorkflowDefinitionId = policy.WorkflowDefinitionId;
            entity.RevalidationPassed = null;
            entity.RevalidationMessage = null;
            entity.RevalidatedAtUtc = null;
            entity.RevalidatedById = null;
            entity.RevalidatedSnapshotHash = null;
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            if (id.HasValue) await Requests.UpdateAsync(entity);
            var links = await ResolveEvidenceAsync(entity.Id, request.Evidence, cancellationToken);
            foreach (var link in links) await EvidenceLinks.AddAsync(link);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync(id.HasValue ? "DraftUpdated" : "DraftCreated", ProcurementControlEventResult.Succeeded,
                policy, entity.Id, entity.RequestNumber, correlationId, entity.Reason, before, RequestAuditShape(entity), links, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataChangeDto> SubmitAsync(
        Guid id,
        ProcurementMasterDataChangeLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadRequestAsync(id, true, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "change request");
            if (entity.Status != ProcurementMasterDataChangeStatus.Draft)
                throw new ProcurementMasterDataChangeConflictException("Only a Draft change request can be submitted.");
            if (entity.MakerUserId != _currentUser.UserId)
                throw new ProcurementMasterDataChangeAuthorizationException("Only the original maker can submit this request.");
            EnsureMaker(entity.Policy);
            if (entity.Policy.RequireEvidence && !entity.EvidenceLinks.Any(item => !item.IsDeleted))
                throw new ProcurementMasterDataChangeValidationException("EVIDENCE_REQUIRED", "The active policy requires at least one shared evidence reference before submission.");
            var before = RequestAuditShape(entity);
            var revalidation = await RevalidateCoreAsync(entity, cancellationToken);
            if (!revalidation.Passed)
            {
                entity.Status = ProcurementMasterDataChangeStatus.RevalidationFailed;
                await Requests.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync("SubmissionRevalidationFailed", ProcurementControlEventResult.Rejected, entity.Policy, entity.Id,
                    entity.RequestNumber, correlationId, revalidation.Message, before, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }

            var now = DateTime.UtcNow;
            entity.Status = ProcurementMasterDataChangeStatus.PendingApproval;
            entity.SubmittedById = _currentUser.UserId;
            entity.SubmittedAtUtc = now;
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Requests.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (entity.WorkflowDefinitionId.HasValue)
            {
                var definition = await WorkflowDefinitions.GetQueryable(item => item.Id == entity.WorkflowDefinitionId.Value &&
                        item.TenantId == _currentUser.TenantId && !item.IsDeleted)
                    .AsNoTracking().SingleAsync(cancellationToken);
                var instance = await _workflowInstances.StartWorkflowAsync(definition.Id, definition.EntityTypeId,
                    entity.Id.ToString(), _currentUser.UserId,
                    new { entity.RequestNumber, entity.ResourceType, entity.TargetId, entity.EffectiveAtUtc, entity.CorrelationId }, cancellationToken);
                entity.WorkflowInstanceId = instance.Id;
                await Requests.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await RecordEventAsync("Submitted", ProcurementControlEventResult.ReviewRequired, entity.Policy, entity.Id,
                entity.RequestNumber, correlationId, request.Comment ?? entity.Reason, before, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataChangeDto> RevalidateAsync(
        Guid id,
        ProcurementMasterDataChangeLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadRequestAsync(id, true, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "change request");
            if (entity.Status is not (ProcurementMasterDataChangeStatus.PendingApproval or ProcurementMasterDataChangeStatus.Approved))
                throw new ProcurementMasterDataChangeConflictException("Only a Pending approval or Approved request can be revalidated.");
            EnsureMakerOrChecker(entity.Policy);
            var before = RequestAuditShape(entity);
            var result = await RevalidateCoreAsync(entity, cancellationToken);
            if (!result.Passed) entity.Status = ProcurementMasterDataChangeStatus.RevalidationFailed;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Requests.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync("Revalidated", result.Passed ? ProcurementControlEventResult.Succeeded : ProcurementControlEventResult.Rejected,
                entity.Policy, entity.Id, entity.RequestNumber, correlationId, result.Message, before, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataChangeDto> ApproveAsync(
        Guid id,
        ProcurementMasterDataChangeDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureReason(request.Comment);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadRequestAsync(id, true, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "change request");
            if (entity.Status != ProcurementMasterDataChangeStatus.PendingApproval)
                throw new ProcurementMasterDataChangeConflictException("Only a Pending approval request can be approved.");
            EnsureChecker(entity.Policy);
            EnsureIndependentChecker(entity);
            await EnsureWorkflowOutcomeAsync(entity, true, cancellationToken);
            var before = RequestAuditShape(entity);
            var revalidation = await RevalidateCoreAsync(entity, cancellationToken);
            if (!revalidation.Passed)
            {
                entity.Status = ProcurementMasterDataChangeStatus.RevalidationFailed;
                entity.CheckerUserId = _currentUser.UserId;
                entity.CheckedAtUtc = DateTime.UtcNow;
                entity.CheckerComment = request.Comment.Trim();
                await Requests.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync("ApprovalRevalidationFailed", ProcurementControlEventResult.Rejected, entity.Policy, entity.Id,
                    entity.RequestNumber, correlationId, revalidation.Message, before, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            var now = DateTime.UtcNow;
            entity.Status = ProcurementMasterDataChangeStatus.Approved;
            entity.CheckerUserId = _currentUser.UserId;
            entity.CheckedAtUtc = now;
            entity.CheckerComment = request.Comment.Trim();
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Requests.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync("Approved", ProcurementControlEventResult.Allowed, entity.Policy, entity.Id,
                entity.RequestNumber, correlationId, entity.CheckerComment, before, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataChangeDto> RejectAsync(
        Guid id,
        ProcurementMasterDataChangeDecisionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureReason(request.Comment);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadRequestAsync(id, true, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "change request");
            if (entity.Status != ProcurementMasterDataChangeStatus.PendingApproval)
                throw new ProcurementMasterDataChangeConflictException("Only a Pending approval request can be rejected.");
            EnsureChecker(entity.Policy);
            EnsureIndependentChecker(entity);
            await EnsureWorkflowOutcomeAsync(entity, false, cancellationToken);
            var before = RequestAuditShape(entity);
            var now = DateTime.UtcNow;
            entity.Status = ProcurementMasterDataChangeStatus.Rejected;
            entity.CheckerUserId = _currentUser.UserId;
            entity.CheckedAtUtc = now;
            entity.CheckerComment = request.Comment.Trim();
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Requests.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync("Rejected", ProcurementControlEventResult.Rejected, entity.Policy, entity.Id,
                entity.RequestNumber, correlationId, entity.CheckerComment, before, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataChangeDto> ApplyAsync(
        Guid id,
        ProcurementMasterDataChangeLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadRequestAsync(id, true, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "change request");
            if (entity.Status != ProcurementMasterDataChangeStatus.Approved)
                throw new ProcurementMasterDataChangeConflictException("Only an Approved request can be applied.");
            EnsureChecker(entity.Policy);
            EnsureIndependentChecker(entity);
            var now = DateTime.UtcNow;
            if (EnsureUtc(entity.EffectiveAtUtc) > now)
                throw new ProcurementMasterDataChangeConflictException($"This request is approved but cannot be applied before {entity.EffectiveAtUtc:O}.");
            var beforeAudit = RequestAuditShape(entity);
            var revalidation = await RevalidateCoreAsync(entity, cancellationToken);
            if (!revalidation.Passed)
            {
                entity.Status = ProcurementMasterDataChangeStatus.RevalidationFailed;
                await Requests.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordEventAsync("ApplicationRevalidationFailed", ProcurementControlEventResult.Rejected, entity.Policy, entity.Id,
                    entity.RequestNumber, correlationId, revalidation.Message, beforeAudit, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }

            var target = await LoadTargetAsync(entity.ResourceType, entity.TargetId, entity.TargetKind, true, cancellationToken);
            var currentBefore = Snapshot(target.Entity, entity.ResourceType, entity.TargetKind);
            NormalizeAndValidatePatch(target.Entity, entity.ResourceType, entity.TargetKind, entity.ProposedChangesJson);
            await ValidateTargetRelationsAsync(target.Entity, target.Kind, entity.ResourceType, cancellationToken);
            if (target.Entity is BusinessPartner partner &&
                entity.ResourceType == ProcurementMasterDataResourceType.SupplierCategoryAssignments)
                SynchronizeSupplierCategories(partner);
            target.Entity.UpdatedAt = now;
            target.Entity.UpdatedBy = _currentUser.Username;
            target.Entity.LastModifiedById = _currentUser.UserId;
            if (target.IsNewTenantSingleton)
                await _unitOfWork.Repository<ProcurementSettings>().AddAsync((ProcurementSettings)target.Entity);
            else if (entity.ResourceType != ProcurementMasterDataResourceType.SupplierCategoryAssignments)
                await UpdateTargetAsync(target, cancellationToken);
            var afterJson = Snapshot(target.Entity, entity.ResourceType, entity.TargetKind);
            entity.AppliedAfterJson = afterJson;
            entity.AppliedAfterHash = Hash(afterJson);
            entity.Status = ProcurementMasterDataChangeStatus.Applied;
            entity.AppliedById = _currentUser.UserId;
            entity.AppliedAtUtc = now;
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Requests.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync("Applied", ProcurementControlEventResult.Succeeded, entity.Policy, entity.Id,
                entity.RequestNumber, correlationId, request.Comment ?? entity.Reason,
                JsonElementFrom(currentBefore), JsonElementFrom(afterJson), entity.EvidenceLinks, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            _logger.LogInformation("Applied controlled master-data request {RequestNumber} to {ResourceType}/{TargetId}",
                entity.RequestNumber, entity.ResourceType, entity.TargetId);
            return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataChangeDto> CancelAsync(
        Guid id,
        ProcurementMasterDataChangeLifecycleRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        EnsureReason(request.Comment);
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
            var entity = await LoadRequestAsync(id, true, cancellationToken);
            EnsureRowVersion(entity.RowVersion, request.RowVersion, "change request");
            if (entity.Status is not (ProcurementMasterDataChangeStatus.Draft or ProcurementMasterDataChangeStatus.PendingApproval or ProcurementMasterDataChangeStatus.RevalidationFailed))
                throw new ProcurementMasterDataChangeConflictException("Only a Draft, Pending approval, or Revalidation failed request can be cancelled.");
            if (entity.MakerUserId != _currentUser.UserId && !IsAdministrator())
                throw new ProcurementMasterDataChangeAuthorizationException("Only the maker or a tenant administrator can cancel this request.");
            var before = RequestAuditShape(entity);
            if (entity.WorkflowInstanceId.HasValue)
            {
                var instance = await WorkflowInstanceRows.GetQueryable(item => item.Id == entity.WorkflowInstanceId.Value &&
                        item.TenantId == _currentUser.TenantId && !item.IsDeleted).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                if (instance is not null && instance.Status is WorkflowInstanceStatus.Created or WorkflowInstanceStatus.InProgress or WorkflowInstanceStatus.Suspended)
                    await _workflowInstances.CancelWorkflowAsync(instance.Id, _currentUser.UserId, request.Comment, cancellationToken);
            }
            var now = DateTime.UtcNow;
            entity.Status = ProcurementMasterDataChangeStatus.Cancelled;
            entity.CheckerComment = request.Comment?.Trim();
            entity.UpdatedAt = now;
            entity.UpdatedBy = _currentUser.Username;
            entity.LastModifiedById = _currentUser.UserId;
            await Requests.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordEventAsync("Cancelled", ProcurementControlEventResult.Rejected, entity.Policy, entity.Id,
                entity.RequestNumber, correlationId, request.Comment, before, RequestAuditShape(entity), entity.EvidenceLinks, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return MapRequest(await LoadRequestAsync(entity.Id, false, cancellationToken));
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<ProcurementMasterDataDirectMutationDecisionDto> CheckDirectMutationAsync(
        IReadOnlyCollection<ProcurementMasterDataResourceType> resourceTypes,
        Guid? targetId,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (resourceTypes.Count == 0)
            throw new ProcurementMasterDataChangeValidationException("RESOURCE_REQUIRED", "At least one resource type is required for a direct-mutation check.");
        var now = DateTime.UtcNow;
        var policy = await Policies.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                resourceTypes.Contains(item.ResourceType) && item.Status == ProcurementMasterDataPolicyStatus.Active &&
                item.EffectiveFromUtc <= now && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc >= now))
            .AsNoTracking().OrderBy(item => item.ResourceType).ThenByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        if (policy is null)
        {
            return new ProcurementMasterDataDirectMutationDecisionDto
            {
                Allowed = true,
                Code = "DIRECT_MUTATION_NOT_PROTECTED",
                Message = "No Active effective maker-checker policy currently protects this direct mutation.",
                CorrelationId = NormalizeCorrelation(correlationId)
            };
        }

        var result = new ProcurementMasterDataDirectMutationDecisionDto
        {
            Allowed = false,
            Code = "STAGED_CHANGE_REQUIRED",
            Message = $"{ProcurementMasterDataResourceRegistry.Get(policy.ResourceType).Name} is protected by an Active maker-checker policy. Submit a staged master-data change request.",
            PolicyId = policy.Id,
            ResourceType = policy.ResourceType,
            CorrelationId = NormalizeCorrelation(correlationId)
        };
        await RecordEventAsync("DirectMutationDenied", ProcurementControlEventResult.Denied, policy, targetId,
            sourceReference, correlationId, result.Message,
            new { ResourceTypes = resourceTypes, TargetId = targetId }, result, null, cancellationToken);
        return result;
    }

    private async Task<ProcurementMasterDataControlPolicy?> GetEffectivePolicyAsync(
        ProcurementMasterDataResourceType resourceType,
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        var at = EnsureUtc(atUtc);
        return await Policies.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted &&
                item.ResourceType == resourceType && item.Status == ProcurementMasterDataPolicyStatus.Active &&
                item.EffectiveFromUtc <= at && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc >= at))
            .AsNoTracking().OrderByDescending(item => item.Version).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ProcurementMasterDataControlPolicy> LoadPolicyAsync(Guid id, CancellationToken cancellationToken) =>
        await Policies.GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new ProcurementMasterDataChangeNotFoundException("The control policy was not found in the current tenant.");

    private async Task<ProcurementMasterDataPolicyDto> LoadPolicyDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await Policies.GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.WorkflowDefinition).AsNoTracking().SingleAsync(cancellationToken);
        return MapPolicy(entity);
    }

    private async Task<ProcurementMasterDataChangeRequest> LoadRequestAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<ProcurementMasterDataChangeRequest> query = IncludeRequestDetails(Requests.GetQueryable(item =>
            item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted));
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementMasterDataChangeNotFoundException("The change request was not found in the current tenant.");
    }

    private static IQueryable<ProcurementMasterDataChangeRequest> IncludeRequestDetails(IQueryable<ProcurementMasterDataChangeRequest> query) =>
        query.Include(item => item.Policy)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.WorkflowEvidenceDocument)
            .Include(item => item.EvidenceLinks).ThenInclude(item => item.FileUploadRecord);

    private async Task<RevalidationResult> RevalidateCoreAsync(
        ProcurementMasterDataChangeRequest entity,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        entity.RevalidatedById = _currentUser.UserId;
        entity.RevalidatedAtUtc = now;
        try
        {
            var target = await LoadTargetAsync(entity.ResourceType, entity.TargetId, entity.TargetKind, false, cancellationToken);
            var currentJson = Snapshot(target.Entity, entity.ResourceType, entity.TargetKind);
            var currentHash = Hash(currentJson);
            entity.RevalidatedSnapshotHash = currentHash;
            if (!string.Equals(currentHash, entity.BeforeHash, StringComparison.OrdinalIgnoreCase))
            {
                entity.RevalidationPassed = false;
                entity.RevalidationMessage = "The protected master-data record changed after this request captured its immutable before snapshot. Create a fresh request from the current state.";
                return new RevalidationResult(false, entity.RevalidationMessage);
            }
            NormalizeAndValidatePatch(target.Entity, entity.ResourceType, entity.TargetKind, entity.ProposedChangesJson);
            await ValidateTargetRelationsAsync(target.Entity, target.Kind, entity.ResourceType, cancellationToken);
            if (target.Entity is BusinessPartner partner &&
                entity.ResourceType == ProcurementMasterDataResourceType.SupplierCategoryAssignments)
                SynchronizeSupplierCategories(partner);
            var eligibility = ValidateEligibility(target);
            entity.RevalidationPassed = eligibility.Passed;
            entity.RevalidationMessage = eligibility.Message;
            return eligibility;
        }
        catch (ProcurementMasterDataChangeException exception)
        {
            entity.RevalidationPassed = false;
            entity.RevalidationMessage = Truncate(exception.Message, 2000);
            return new RevalidationResult(false, entity.RevalidationMessage);
        }
        catch (ValidationException exception)
        {
            entity.RevalidationPassed = false;
            entity.RevalidationMessage = Truncate(exception.Message, 2000);
            return new RevalidationResult(false, entity.RevalidationMessage);
        }
    }

    private static RevalidationResult ValidateEligibility(LoadedTarget target)
    {
        if (target.Entity is BusinessPartner partner)
        {
            if (partner.IsBlacklisted || string.Equals(partner.RegistrationStatus, "Blacklisted", StringComparison.OrdinalIgnoreCase))
                return new RevalidationResult(false, "Supplier eligibility revalidation failed because the business partner is blacklisted.");
            if (string.Equals(partner.RegistrationStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                return new RevalidationResult(false, "Supplier eligibility revalidation failed because the business partner registration is rejected.");
        }
        if (target.Entity is Supplier supplier && (supplier.IsBlacklisted || string.Equals(supplier.Status, "Blacklisted", StringComparison.OrdinalIgnoreCase)))
            return new RevalidationResult(false, "Supplier eligibility revalidation failed because the supplier is blacklisted.");
        return new RevalidationResult(true, "Target snapshot, tenant ownership, field policy, relationships, and supplier eligibility checks passed.");
    }

    private async Task EnsureWorkflowOutcomeAsync(
        ProcurementMasterDataChangeRequest entity,
        bool approving,
        CancellationToken cancellationToken)
    {
        if (!entity.WorkflowDefinitionId.HasValue) return;
        if (!entity.WorkflowInstanceId.HasValue)
            throw new ProcurementMasterDataChangeConflictException("The configured shared workflow was not started for this request.");
        var instance = await WorkflowInstanceRows.GetQueryable(item => item.Id == entity.WorkflowInstanceId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementMasterDataChangeConflictException("The linked shared workflow instance was not found in the current tenant.");
        if (approving && instance.Status != WorkflowInstanceStatus.Completed)
            throw new ProcurementMasterDataChangeConflictException("Complete the configured shared workflow before synchronizing approval to this change request.");
        if (!approving && instance.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            throw new ProcurementMasterDataChangeConflictException("Reject or cancel the configured shared workflow before synchronizing rejection to this change request.");
    }

    private async Task<List<ProcurementMasterDataChangeEvidenceLink>> ResolveEvidenceAsync(
        Guid requestId,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> references,
        CancellationToken cancellationToken)
    {
        var result = new List<ProcurementMasterDataChangeEvidenceLink>();
        foreach (var reference in references)
        {
            var row = new ProcurementMasterDataChangeEvidenceLink
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                ChangeRequestId = requestId,
                ReferenceKind = reference.ReferenceKind,
                Label = TruncateNullable(reference.Label, 300),
                RequirementKey = TruncateNullable(reference.RequirementKey, 200),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            };
            if (reference.ReferenceKind == ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument)
            {
                if (!reference.ReferenceId.HasValue)
                    throw new ProcurementMasterDataChangeValidationException("EVIDENCE_ID_REQUIRED", "Workflow evidence requires ReferenceId.");
                var item = await WorkflowEvidence.GetQueryable(value => value.Id == reference.ReferenceId.Value &&
                        value.TenantId == _currentUser.TenantId && !value.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ProcurementMasterDataChangeNotFoundException("The workflow evidence reference was not found in the current tenant.");
                row.WorkflowEvidenceDocumentId = item.Id;
                row.Reference = item.AttachmentId;
            }
            else if (reference.ReferenceKind == ProcurementControlEvidenceReferenceKind.FileUploadRecord)
            {
                if (!reference.ReferenceId.HasValue)
                    throw new ProcurementMasterDataChangeValidationException("EVIDENCE_ID_REQUIRED", "Shared file evidence requires ReferenceId.");
                var item = await FileUploads.GetQueryable(value => value.Id == reference.ReferenceId.Value &&
                        value.TenantId == _currentUser.TenantId && !value.IsDeleted)
                    .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                    ?? throw new ProcurementMasterDataChangeNotFoundException("The shared file-upload reference was not found in the current tenant.");
                row.FileUploadRecordId = item.Id;
                row.Reference = item.Id.ToString("N");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(reference.Reference))
                    throw new ProcurementMasterDataChangeValidationException("EVIDENCE_REFERENCE_REQUIRED", "External evidence requires a reference value.");
                row.Reference = Truncate(reference.Reference.Trim(), 200);
            }
            result.Add(row);
        }
        if (result.GroupBy(item => new { item.ReferenceKind, item.Reference }).Any(group => group.Count() > 1))
            throw new ProcurementMasterDataChangeValidationException("EVIDENCE_DUPLICATE", "Evidence references must be unique within a change request.");
        return result;
    }

    private async Task ValidateWorkflowDefinitionAsync(Guid? workflowDefinitionId, CancellationToken cancellationToken)
    {
        if (!workflowDefinitionId.HasValue) return;
        var definition = await WorkflowDefinitions.GetQueryable(item => item.Id == workflowDefinitionId.Value &&
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementMasterDataChangeNotFoundException("The shared workflow definition was not found in the current tenant.");
        if (!definition.IsActive || definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
            throw new ProcurementMasterDataChangeConflictException("A referenced shared workflow definition must be Active and Published.");
    }

    private async Task<LoadedTarget> LoadTargetAsync(
        ProcurementMasterDataResourceType resourceType,
        Guid targetId,
        ProcurementMasterDataTargetKind? requiredKind,
        bool tracked,
        CancellationToken cancellationToken)
    {
        LoadedTarget? target = resourceType switch
        {
            ProcurementMasterDataResourceType.SupplierProfile =>
                await LoadBusinessPartnerOrSupplierAsync(targetId, requiredKind, tracked, cancellationToken),
            ProcurementMasterDataResourceType.SupplierBankDetails =>
                await LoadBusinessPartnerAsync(targetId, requiredKind, tracked, cancellationToken),
            ProcurementMasterDataResourceType.SupplierTaxDetails =>
                await LoadBusinessPartnerOrSupplierAsync(targetId, requiredKind, tracked, cancellationToken),
            ProcurementMasterDataResourceType.SupplierOwnershipDetails =>
                await LoadBusinessPartnerAsync(targetId, requiredKind, tracked, cancellationToken),
            ProcurementMasterDataResourceType.SupplierCategoryAssignments =>
                await LoadBusinessPartnerAsync(targetId, requiredKind, tracked, cancellationToken),
            ProcurementMasterDataResourceType.SupplierComplianceStatus =>
                await LoadBusinessPartnerAsync(targetId, requiredKind, tracked, cancellationToken),
            ProcurementMasterDataResourceType.InventoryItem =>
                await LoadEntityAsync<InventoryItem>(targetId, ProcurementMasterDataTargetKind.InventoryItem, requiredKind, tracked,
                    item => $"{item.ItemCode} - {item.Name}", cancellationToken),
            ProcurementMasterDataResourceType.InventoryCategory =>
                await LoadEntityAsync<InventoryCategory>(targetId, ProcurementMasterDataTargetKind.InventoryCategory, requiredKind, tracked,
                    item => $"{item.Code} - {item.Name}", cancellationToken),
            ProcurementMasterDataResourceType.UnitOfMeasure =>
                await LoadEntityAsync<UnitOfMeasure>(targetId, ProcurementMasterDataTargetKind.UnitOfMeasure, requiredKind, tracked,
                    item => $"{item.Code} - {item.Name}", cancellationToken),
            ProcurementMasterDataResourceType.Warehouse =>
                await LoadEntityAsync<Warehouse>(targetId, ProcurementMasterDataTargetKind.Warehouse, requiredKind, tracked,
                    item => $"{item.Code} - {item.Name}", cancellationToken),
            ProcurementMasterDataResourceType.WarehouseLocation =>
                await LoadEntityAsync<WarehouseLocation>(targetId, ProcurementMasterDataTargetKind.WarehouseLocation, requiredKind, tracked,
                    item => item.LocationCode, cancellationToken),
            ProcurementMasterDataResourceType.ProcurementPolicySensitive =>
                await LoadProcurementSettingsAsync(targetId, requiredKind, tracked, cancellationToken),
            _ => null
        };
        return target ?? throw new ProcurementMasterDataChangeNotFoundException("The protected target was not found in the current tenant.");
    }

    private async Task<LoadedTarget?> LoadBusinessPartnerOrSupplierAsync(
        Guid id,
        ProcurementMasterDataTargetKind? requiredKind,
        bool tracked,
        CancellationToken cancellationToken)
    {
        if (requiredKind is null or ProcurementMasterDataTargetKind.BusinessPartner)
        {
            var partner = await FindBusinessPartnerAsync(id, tracked, cancellationToken);
            if (partner is not null)
                return new LoadedTarget(partner, ProcurementMasterDataTargetKind.BusinessPartner, $"{partner.PartnerCode} - {partner.PartnerName}", false);
        }
        if (requiredKind is null or ProcurementMasterDataTargetKind.LegacySupplier)
        {
            var supplier = await FindTenantEntityAsync<Supplier>(id, tracked, cancellationToken);
            if (supplier is not null)
                return new LoadedTarget(supplier, ProcurementMasterDataTargetKind.LegacySupplier, $"{supplier.SupplierCode} - {supplier.Name}", false);
        }
        return null;
    }

    private async Task<LoadedTarget?> LoadBusinessPartnerAsync(
        Guid id,
        ProcurementMasterDataTargetKind? requiredKind,
        bool tracked,
        CancellationToken cancellationToken)
    {
        if (requiredKind.HasValue && requiredKind != ProcurementMasterDataTargetKind.BusinessPartner) return null;
        var partner = await FindBusinessPartnerAsync(id, tracked, cancellationToken);
        return partner is null ? null : new LoadedTarget(partner, ProcurementMasterDataTargetKind.BusinessPartner,
            $"{partner.PartnerCode} - {partner.PartnerName}", false);
    }

    private async Task<BusinessPartner?> FindBusinessPartnerAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<BusinessPartner> query = _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Categories);
        if (!tracked) query = query.AsNoTracking();
        var partner = await query.SingleOrDefaultAsync(cancellationToken);
        if (partner is not null)
            partner.CategoryIds = partner.Categories.Select(item => item.CategoryId).Distinct().Order().ToList();
        return partner;
    }

    private async Task<LoadedTarget?> LoadEntityAsync<T>(
        Guid id,
        ProcurementMasterDataTargetKind kind,
        ProcurementMasterDataTargetKind? requiredKind,
        bool tracked,
        Func<T, string> reference,
        CancellationToken cancellationToken) where T : TenantEntity
    {
        if (requiredKind.HasValue && requiredKind.Value != kind) return null;
        var entity = await FindTenantEntityAsync<T>(id, tracked, cancellationToken);
        return entity is null ? null : new LoadedTarget(entity, kind, reference(entity), false);
    }

    private async Task<T?> FindTenantEntityAsync<T>(Guid id, bool tracked, CancellationToken cancellationToken) where T : TenantEntity
    {
        IQueryable<T> query = _unitOfWork.Repository<T>().GetQueryable(item => item.Id == id &&
            item.TenantId == _currentUser.TenantId && !item.IsDeleted);
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<LoadedTarget?> LoadProcurementSettingsAsync(
        Guid targetId,
        ProcurementMasterDataTargetKind? requiredKind,
        bool tracked,
        CancellationToken cancellationToken)
    {
        if (requiredKind.HasValue && requiredKind != ProcurementMasterDataTargetKind.ProcurementSettings) return null;
        var query = _unitOfWork.Repository<ProcurementSettings>().GetQueryable(item => item.TenantId == _currentUser.TenantId &&
            !item.IsDeleted && (targetId == Guid.Empty || item.Id == targetId));
        if (!tracked) query = query.AsNoTracking();
        var existing = await query.SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return new LoadedTarget(existing, ProcurementMasterDataTargetKind.ProcurementSettings, "PROCUREMENT-SETTINGS", false);
        var entity = new ProcurementSettings
        {
            Id = targetId == Guid.Empty ? Guid.NewGuid() : targetId,
            TenantId = _currentUser.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        return new LoadedTarget(entity, ProcurementMasterDataTargetKind.ProcurementSettings, "PROCUREMENT-SETTINGS", true);
    }

    private async Task UpdateTargetAsync(LoadedTarget target, CancellationToken cancellationToken)
    {
        switch (target.Entity)
        {
            case BusinessPartner item: await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(item); break;
            case Supplier item: await _unitOfWork.Repository<Supplier>().UpdateAsync(item); break;
            case InventoryItem item: await _unitOfWork.Repository<InventoryItem>().UpdateAsync(item); break;
            case InventoryCategory item: await _unitOfWork.Repository<InventoryCategory>().UpdateAsync(item); break;
            case UnitOfMeasure item: await _unitOfWork.Repository<UnitOfMeasure>().UpdateAsync(item); break;
            case Warehouse item: await _unitOfWork.Repository<Warehouse>().UpdateAsync(item); break;
            case WarehouseLocation item: await _unitOfWork.Repository<WarehouseLocation>().UpdateAsync(item); break;
            case ProcurementSettings item: await _unitOfWork.Repository<ProcurementSettings>().UpdateAsync(item); break;
            default: throw new ProcurementMasterDataChangeConflictException("The protected target adapter is not configured.");
        }
    }

    private static string NormalizeAndValidatePatch(
        BaseEntity entity,
        ProcurementMasterDataResourceType resourceType,
        ProcurementMasterDataTargetKind targetKind,
        string patchJson)
    {
        if (string.IsNullOrWhiteSpace(patchJson))
            throw new ProcurementMasterDataChangeValidationException("PATCH_REQUIRED", "ProposedChangesJson is required.");
        JsonDocument document;
        try { document = JsonDocument.Parse(patchJson); }
        catch (JsonException exception) { throw new ProcurementMasterDataChangeValidationException("PATCH_JSON_INVALID", exception.Message); }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ProcurementMasterDataChangeValidationException("PATCH_OBJECT_REQUIRED", "ProposedChangesJson must be a JSON object.");
            var properties = document.RootElement.EnumerateObject().ToList();
            if (properties.Count == 0)
                throw new ProcurementMasterDataChangeValidationException("PATCH_EMPTY", "At least one protected field change is required.");
            if (properties.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                throw new ProcurementMasterDataChangeValidationException("PATCH_DUPLICATE_FIELD", "Patch field names must be unique ignoring case.");
            var allowed = ProcurementMasterDataResourceRegistry.GetAllowedFields(resourceType, targetKind);
            var normalized = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var item in properties)
            {
                var property = entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .SingleOrDefault(value => string.Equals(value.Name, item.Name, StringComparison.OrdinalIgnoreCase));
                if (property is null || !property.CanWrite || !allowed.Contains(property.Name))
                    throw new ProcurementMasterDataChangeValidationException("FIELD_NOT_ALLOWED",
                        $"'{item.Name}' is not an allowed {resourceType} field. Use the resource registry for the explicit whitelist.");
                object? value;
                try
                {
                    if (item.Value.ValueKind == JsonValueKind.Null)
                    {
                        if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
                            throw new JsonException($"{property.Name} cannot be null.");
                        value = null;
                    }
                    else
                    {
                        value = JsonSerializer.Deserialize(item.Value.GetRawText(), property.PropertyType, JsonOptions);
                        if (resourceType == ProcurementMasterDataResourceType.SupplierOwnershipDetails &&
                            string.Equals(property.Name, nameof(BusinessPartner.BeneficialOwnershipJson), StringComparison.Ordinal) &&
                            value is string ownershipJson)
                            value = NormalizeBeneficialOwnershipJson(ownershipJson);
                    }
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException)
                {
                    throw new ProcurementMasterDataChangeValidationException("FIELD_VALUE_INVALID",
                        $"'{property.Name}' has an invalid value: {exception.Message}");
                }
                property.SetValue(entity, value);
                normalized[property.Name] = value;
            }
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(entity, new ValidationContext(entity), validationResults, true))
                throw new ProcurementMasterDataChangeValidationException("TARGET_VALIDATION_FAILED",
                    string.Join(" ", validationResults.Select(item => item.ErrorMessage).Where(item => !string.IsNullOrWhiteSpace(item))));
            return JsonSerializer.Serialize(normalized, JsonOptions);
        }
    }

    private static string Snapshot(
        BaseEntity entity,
        ProcurementMasterDataResourceType resourceType,
        ProcurementMasterDataTargetKind targetKind)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var name in ProcurementMasterDataResourceRegistry.GetAllowedFields(resourceType, targetKind).OrderBy(item => item))
        {
            var property = entity.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is not null && property.CanRead) values[property.Name] = property.GetValue(entity);
        }
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private async Task ValidateTargetRelationsAsync(
        BaseEntity entity,
        ProcurementMasterDataTargetKind kind,
        ProcurementMasterDataResourceType resourceType,
        CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case BusinessPartner partner:
                if (partner.ParentId == partner.Id)
                    throw new ProcurementMasterDataChangeValidationException("SUPPLIER_PARENT_SELF", "A supplier cannot be its own parent.");
                if (partner.ParentId.HasValue && !await ExistsTenantAsync<BusinessPartner>(partner.ParentId.Value, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("SUPPLIER_PARENT_NOT_FOUND", "ParentId must identify a current-tenant business partner.");
                if (resourceType == ProcurementMasterDataResourceType.SupplierCategoryAssignments)
                {
                    partner.CategoryIds = partner.CategoryIds.Distinct().Order().ToList();
                    if (partner.CategoryIds.Count == 0)
                        throw new ProcurementMasterDataChangeValidationException("SUPPLIER_CATEGORY_REQUIRED", "At least one supplier category is required.");
                    var validCategories = await _unitOfWork.Repository<PartnerCategory>()
                        .GetQueryable(item => partner.CategoryIds.Contains(item.Id) &&
                            item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive)
                        .CountAsync(cancellationToken);
                    if (validCategories != partner.CategoryIds.Count)
                        throw new ProcurementMasterDataChangeValidationException("SUPPLIER_CATEGORY_INVALID", "Every CategoryIds value must identify an active current-tenant partner category.");
                }
                if (resourceType == ProcurementMasterDataResourceType.SupplierOwnershipDetails)
                {
                    partner.BeneficialOwnershipJson = NormalizeBeneficialOwnershipJson(partner.BeneficialOwnershipJson);
                    if (partner.OwnershipVerifiedAtUtc.HasValue &&
                        EnsureUtc(partner.OwnershipVerifiedAtUtc.Value) > DateTime.UtcNow.AddMinutes(1))
                        throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_VERIFIED_DATE_INVALID", "OwnershipVerifiedAtUtc cannot be in the future.");
                }
                if (resourceType == ProcurementMasterDataResourceType.SupplierComplianceStatus)
                    ValidateSupplierCompliance(partner);
                break;
            case InventoryItem item:
                item.Barcode = NormalizeInventoryIdentifier(item.Barcode);
                item.AlternateBarcode = NormalizeInventoryIdentifier(item.AlternateBarcode);
                item.QRCode = NormalizeInventoryIdentifier(item.QRCode);
                if (_inventoryIdentifiers is not null)
                {
                    await _inventoryIdentifiers.ValidateItemIdentifiersAsync(
                        item.TenantId,
                        item.Id,
                        item.Barcode,
                        item.AlternateBarcode,
                        item.QRCode,
                        cancellationToken);
                }
                if (!await ExistsTenantAsync<InventoryCategory>(item.CategoryId, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("CATEGORY_NOT_FOUND", "Inventory item CategoryId must identify a current-tenant category.");
                if (item.UnitOfMeasureScheduleId.HasValue && !await ExistsTenantAsync<UnitOfMeasureSchedule>(item.UnitOfMeasureScheduleId.Value, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("UOM_SCHEDULE_NOT_FOUND", "UnitOfMeasureScheduleId must identify a current-tenant schedule.");
                if (await _unitOfWork.Repository<InventoryItem>().GetQueryable(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                        value.Id != item.Id && value.ItemCode == item.ItemCode).AnyAsync(cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("ITEM_CODE_DUPLICATE", "Inventory item code must be unique in the current tenant.");
                break;
            case InventoryCategory category:
                if (category.ParentCategoryId == category.Id)
                    throw new ProcurementMasterDataChangeValidationException("CATEGORY_PARENT_SELF", "An inventory category cannot be its own parent.");
                if (category.ParentCategoryId.HasValue && !await ExistsTenantAsync<InventoryCategory>(category.ParentCategoryId.Value, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("CATEGORY_PARENT_NOT_FOUND", "ParentCategoryId must identify a current-tenant category.");
                if (await _unitOfWork.Repository<InventoryCategory>().GetQueryable(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                        value.Id != category.Id && value.Code == category.Code).AnyAsync(cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("CATEGORY_CODE_DUPLICATE", "Inventory category code must be unique in the current tenant.");
                break;
            case UnitOfMeasure unit:
                if (await _unitOfWork.Repository<UnitOfMeasure>().GetQueryable(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                        value.Id != unit.Id && value.Code == unit.Code).AnyAsync(cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("UOM_CODE_DUPLICATE", "Unit-of-measure code must be unique in the current tenant.");
                break;
            case Warehouse warehouse:
                if (await _unitOfWork.Repository<Warehouse>().GetQueryable(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                        value.Id != warehouse.Id && value.Code == warehouse.Code).AnyAsync(cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("WAREHOUSE_CODE_DUPLICATE", "Warehouse code must be unique in the current tenant.");
                if (warehouse.IsDefault && await _unitOfWork.Repository<Warehouse>().GetQueryable(value => value.TenantId == _currentUser.TenantId &&
                        !value.IsDeleted && value.Id != warehouse.Id && value.IsDefault).AnyAsync(cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("WAREHOUSE_DEFAULT_DUPLICATE", "Only one current-tenant warehouse can be the default.");
                break;
            case WarehouseLocation location:
                if (!await ExistsTenantAsync<Warehouse>(location.WarehouseId, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("WAREHOUSE_NOT_FOUND", "WarehouseId must identify a current-tenant warehouse.");
                if (location.ParentLocationId == location.Id)
                    throw new ProcurementMasterDataChangeValidationException("LOCATION_PARENT_SELF", "A warehouse location cannot be its own parent.");
                if (location.ParentLocationId.HasValue)
                {
                    var parent = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value => value.Id == location.ParentLocationId.Value &&
                            value.TenantId == _currentUser.TenantId && !value.IsDeleted).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                    if (parent is null || parent.WarehouseId != location.WarehouseId)
                        throw new ProcurementMasterDataChangeValidationException("LOCATION_PARENT_INVALID", "ParentLocationId must identify a location in the same current-tenant warehouse.");
                }
                if (location.IsConsignmentBin && !location.ConsignmentWarehouseId.HasValue)
                    throw new ProcurementMasterDataChangeValidationException("CONSIGNMENT_WAREHOUSE_REQUIRED", "A consignment bin requires ConsignmentWarehouseId.");
                if (location.ConsignmentWarehouseId.HasValue && !await ExistsTenantAsync<Warehouse>(location.ConsignmentWarehouseId.Value, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("CONSIGNMENT_WAREHOUSE_NOT_FOUND", "ConsignmentWarehouseId must identify a current-tenant warehouse.");
                if (location.DedicatedItemId.HasValue && !await ExistsTenantAsync<InventoryItem>(location.DedicatedItemId.Value, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("DEDICATED_ITEM_NOT_FOUND", "DedicatedItemId must identify a current-tenant inventory item.");
                if (await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                        value.Id != location.Id && value.WarehouseId == location.WarehouseId && value.LocationCode == location.LocationCode).AnyAsync(cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("LOCATION_CODE_DUPLICATE", "Location code must be unique within the selected warehouse.");
                break;
            case ProcurementSettings settings:
                if (settings.DefaultItemCategoryId.HasValue && !await ExistsTenantAsync<InventoryCategory>(settings.DefaultItemCategoryId.Value, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("DEFAULT_CATEGORY_NOT_FOUND", "DefaultItemCategoryId must identify a current-tenant category.");
                if (settings.DefaultUnitOfMeasureId.HasValue && !await ExistsTenantAsync<UnitOfMeasure>(settings.DefaultUnitOfMeasureId.Value, cancellationToken))
                    throw new ProcurementMasterDataChangeValidationException("DEFAULT_UOM_NOT_FOUND", "DefaultUnitOfMeasureId must identify a current-tenant unit of measure.");
                if (settings.AutoCreateSupplierItems && !settings.AutoCreateInventoryItems)
                    throw new ProcurementMasterDataChangeValidationException("AUTO_CREATE_DEPENDENCY", "AutoCreateSupplierItems requires AutoCreateInventoryItems.");
                break;
        }
    }

    private static void SynchronizeSupplierCategories(BusinessPartner partner)
    {
        var desired = partner.CategoryIds.Distinct().Order().ToList();
        partner.Categories.Clear();
        for (var index = 0; index < desired.Count; index++)
        {
            partner.Categories.Add(new BusinessPartnerCategory
            {
                BusinessPartnerId = partner.Id,
                CategoryId = desired[index],
                IsPrimary = index == 0
            });
        }
    }

    private Task<bool> ExistsTenantAsync<T>(Guid id, CancellationToken cancellationToken) where T : TenantEntity =>
        _unitOfWork.Repository<T>().GetQueryable(item => item.Id == id && item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AnyAsync(cancellationToken);

    private static string? NormalizeInventoryIdentifier(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToUpperInvariant();
    }

    private static string NormalizeBeneficialOwnershipJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ProcurementMasterDataChangeValidationException(
                "OWNERSHIP_REQUIRED",
                "BeneficialOwnershipJson must contain at least one beneficial owner.");

        JsonDocument document;
        try { document = JsonDocument.Parse(value); }
        catch (JsonException exception)
        {
            throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_JSON_INVALID", exception.Message);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_ARRAY_REQUIRED", "BeneficialOwnershipJson must be a JSON array.");
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name", "ownershipPercent", "nationality", "registrationNumber", "politicallyExposed"
            };
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    element.EnumerateObject().Any(property => !allowed.Contains(property.Name)))
                    throw new ProcurementMasterDataChangeValidationException(
                        "OWNERSHIP_FIELD_INVALID",
                        "Each beneficial owner may contain only name, ownershipPercent, nationality, registrationNumber, and politicallyExposed.");
            }
        }

        List<BeneficialOwnerInput> owners;
        try
        {
            owners = JsonSerializer.Deserialize<List<BeneficialOwnerInput>>(value, JsonOptions) ?? new();
        }
        catch (JsonException exception)
        {
            throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_JSON_INVALID", exception.Message);
        }

        if (owners.Count == 0 || owners.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_NAME_REQUIRED", "Every beneficial owner requires a name.");
        if (owners.Any(item => item.OwnershipPercent <= 0m || item.OwnershipPercent > 100m))
            throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_PERCENT_INVALID", "Every ownershipPercent must be greater than zero and no more than 100.");
        if (owners.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_DUPLICATE", "Beneficial-owner names must be unique.");
        if (Math.Abs(owners.Sum(item => item.OwnershipPercent) - 100m) > 0.01m)
            throw new ProcurementMasterDataChangeValidationException("OWNERSHIP_TOTAL_INVALID", "Beneficial ownership percentages must total 100.");

        var normalized = owners
            .Select(item => new BeneficialOwnerInput
            {
                Name = item.Name.Trim(),
                OwnershipPercent = decimal.Round(item.OwnershipPercent, 2),
                Nationality = TruncateNullable(item.Nationality, 100),
                RegistrationNumber = TruncateNullable(item.RegistrationNumber, 100),
                PoliticallyExposed = item.PoliticallyExposed
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    private static void ValidateSupplierCompliance(BusinessPartner partner)
    {
        var registrationStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pending", "PendingApproval", "UnderReview", "Approved", "Rejected", "Suspended", "Blacklisted", "Active", "Inactive"
        };
        var approvalStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pending", "Approved", "Rejected"
        };
        var complianceStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PendingReview", "Compliant", "Conditional", "NonCompliant", "Suspended"
        };
        var riskLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Low", "Medium", "High", "Critical"
        };

        if (string.IsNullOrWhiteSpace(partner.RegistrationStatus) || !registrationStatuses.Contains(partner.RegistrationStatus))
            throw new ProcurementMasterDataChangeValidationException("REGISTRATION_STATUS_INVALID", "RegistrationStatus is not an allowed controlled supplier status.");
        if (!string.IsNullOrWhiteSpace(partner.ApprovalStatus) && !approvalStatuses.Contains(partner.ApprovalStatus))
            throw new ProcurementMasterDataChangeValidationException("APPROVAL_STATUS_INVALID", "ApprovalStatus is not an allowed controlled supplier approval status.");
        if (!string.IsNullOrWhiteSpace(partner.ComplianceStatus) && !complianceStatuses.Contains(partner.ComplianceStatus))
            throw new ProcurementMasterDataChangeValidationException("COMPLIANCE_STATUS_INVALID", "ComplianceStatus must be PendingReview, Compliant, Conditional, NonCompliant, or Suspended.");
        if (!string.IsNullOrWhiteSpace(partner.RiskLevel) && !riskLevels.Contains(partner.RiskLevel))
            throw new ProcurementMasterDataChangeValidationException("RISK_LEVEL_INVALID", "RiskLevel must be Low, Medium, High, or Critical.");
        if (partner.IsBlacklisted && (string.IsNullOrWhiteSpace(partner.BlacklistReason) || !partner.BlacklistDate.HasValue))
            throw new ProcurementMasterDataChangeValidationException("BLACKLIST_EVIDENCE_REQUIRED", "Blacklisted suppliers require BlacklistReason and BlacklistDate.");
        if (partner.ComplianceReviewDateUtc.HasValue && partner.ComplianceValidUntilUtc.HasValue &&
            EnsureUtc(partner.ComplianceValidUntilUtc.Value) < EnsureUtc(partner.ComplianceReviewDateUtc.Value))
            throw new ProcurementMasterDataChangeValidationException("COMPLIANCE_PERIOD_INVALID", "ComplianceValidUntilUtc cannot precede ComplianceReviewDateUtc.");
    }

    private async Task RecordEventAsync(
        string action,
        ProcurementControlEventResult result,
        ProcurementMasterDataControlPolicy policy,
        Guid? sourceId,
        string sourceReference,
        string correlationId,
        string? reason,
        object? before,
        object? after,
        IEnumerable<ProcurementMasterDataChangeEvidenceLink>? evidence,
        CancellationToken cancellationToken)
    {
        var evidenceReferences = evidence?.Where(item => !item.IsDeleted).Select(item => new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = item.ReferenceKind,
            ReferenceId = item.WorkflowEvidenceDocumentId ?? item.FileUploadRecordId,
            Reference = item.Reference,
            Label = item.Label,
            RequirementKey = item.RequirementKey
        }).ToList() ?? new List<ProcurementControlEventEvidenceReference>();
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("master-data-change", action, sourceId, NormalizeCorrelation(correlationId)),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = ProcurementMasterDataResourceRegistry.Get(policy.ResourceType).Code,
            RuleId = policy.Id,
            RuleVersion = policy.Version.ToString(),
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = sourceId == policy.Id ? "ProcurementMasterDataControlPolicy" : "ProcurementMasterDataChangeRequest",
            SourceId = sourceId,
            SourceReference = Truncate(sourceReference, 500),
            Reason = TruncateNullable(reason, 1000),
            Before = before,
            After = after,
            ResultValues = new { Status = result.ToString(), ResourceType = policy.ResourceType.ToString() },
            CorrelationId = NormalizeCorrelation(correlationId),
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidenceReferences
        }, cancellationToken);

        if (action is "Submitted" or "Approved" or "Rejected" or "Applied" or "Cancelled" or
            "SubmissionRevalidationFailed" or "ApprovalRevalidationFailed" or "ApplicationRevalidationFailed")
            await PublishNotificationAsync(action, result, policy, sourceId, sourceReference, reason, cancellationToken);
    }

    private async Task PublishNotificationAsync(
        string action,
        ProcurementControlEventResult result,
        ProcurementMasterDataControlPolicy policy,
        Guid? sourceId,
        string sourceReference,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var supplierResource = policy.ResourceType is
                ProcurementMasterDataResourceType.SupplierProfile or
                ProcurementMasterDataResourceType.SupplierBankDetails or
                ProcurementMasterDataResourceType.SupplierTaxDetails or
                ProcurementMasterDataResourceType.SupplierOwnershipDetails or
                ProcurementMasterDataResourceType.SupplierCategoryAssignments or
                ProcurementMasterDataResourceType.SupplierComplianceStatus;
            var topicScope = supplierResource ? "supplier-master-change" : "master-data-change";
            await _notificationTopics.PublishAsync(new NotificationTopicEvent
            {
                TenantId = policy.TenantId,
                TopicKey = $"procurement.{topicScope}.{action.ToLowerInvariant()}",
                NotificationType = supplierResource
                    ? "ProcurementSupplierMasterChangeControl"
                    : "ProcurementMasterDataChangeControl",
                EntityType = sourceId == policy.Id
                    ? "ProcurementMasterDataControlPolicy"
                    : "ProcurementMasterDataChangeRequest",
                EntityId = sourceId,
                TriggeredByUserId = _currentUser.UserId,
                Data = new Dictionary<string, object>
                {
                    ["sourceReference"] = sourceReference,
                    ["resourceType"] = policy.ResourceType.ToString(),
                    ["policyId"] = policy.Id,
                    ["policyVersion"] = policy.Version,
                    ["action"] = action,
                    ["result"] = result.ToString(),
                    ["reason"] = reason ?? string.Empty
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Failed to publish master-data change notification for {SourceId}/{Action}",
                sourceId, action);
        }
    }

    private static ProcurementMasterDataPolicyDto MapPolicy(ProcurementMasterDataControlPolicy item)
    {
        var now = DateTime.UtcNow;
        return new ProcurementMasterDataPolicyDto
        {
            Id = item.Id,
            PolicyKey = item.PolicyKey,
            ResourceType = item.ResourceType,
            Version = item.Version,
            Status = item.Status,
            Name = item.Name,
            Description = item.Description,
            MakerRoles = DeserializeRoles(item.MakerRolesJson),
            CheckerRoles = DeserializeRoles(item.CheckerRolesJson),
            RequireIndependentApproval = item.RequireIndependentApproval,
            RequireRevalidation = item.RequireRevalidation,
            RequireEvidence = item.RequireEvidence,
            WorkflowDefinitionId = item.WorkflowDefinitionId,
            WorkflowDefinitionName = item.WorkflowDefinition?.Name,
            WorkflowDefinitionVersion = item.WorkflowDefinition?.Version,
            EffectiveFromUtc = item.EffectiveFromUtc,
            EffectiveToUtc = item.EffectiveToUtc,
            SupersedesPolicyId = item.SupersedesPolicyId,
            ActivatedAtUtc = item.ActivatedAtUtc,
            RetiredAtUtc = item.RetiredAtUtc,
            IsEffective = item.Status == ProcurementMasterDataPolicyStatus.Active && item.EffectiveFromUtc <= now &&
                          (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc >= now),
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private static ProcurementMasterDataChangeDto MapRequest(ProcurementMasterDataChangeRequest item) => new()
    {
        Id = item.Id,
        RequestNumber = item.RequestNumber,
        PolicyId = item.PolicyId,
        PolicyVersion = item.PolicyVersion,
        ResourceType = item.ResourceType,
        TargetKind = item.TargetKind,
        TargetId = item.TargetId,
        TargetReference = item.TargetReference,
        Status = item.Status,
        BeforeJson = item.BeforeJson,
        BeforeHash = item.BeforeHash,
        ProposedChangesJson = item.ProposedChangesJson,
        ProposedChangesHash = item.ProposedChangesHash,
        AppliedAfterJson = item.AppliedAfterJson,
        AppliedAfterHash = item.AppliedAfterHash,
        Reason = item.Reason,
        EffectiveAtUtc = item.EffectiveAtUtc,
        MakerUserId = item.MakerUserId,
        SubmittedById = item.SubmittedById,
        SubmittedAtUtc = item.SubmittedAtUtc,
        CheckerUserId = item.CheckerUserId,
        CheckedAtUtc = item.CheckedAtUtc,
        CheckerComment = item.CheckerComment,
        RevalidationPassed = item.RevalidationPassed,
        RevalidatedAtUtc = item.RevalidatedAtUtc,
        RevalidationMessage = item.RevalidationMessage,
        RevalidatedSnapshotHash = item.RevalidatedSnapshotHash,
        AppliedById = item.AppliedById,
        AppliedAtUtc = item.AppliedAtUtc,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        CorrelationId = item.CorrelationId,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        Evidence = item.EvidenceLinks.Where(link => !link.IsDeleted).OrderBy(link => link.ReferenceKind).ThenBy(link => link.Reference)
            .Select(link => new ProcurementMasterDataChangeEvidenceDto
            {
                Id = link.Id,
                ReferenceKind = link.ReferenceKind,
                ReferenceId = link.WorkflowEvidenceDocumentId ?? link.FileUploadRecordId,
                Reference = link.Reference,
                Label = link.Label,
                RequirementKey = link.RequirementKey,
                FileName = link.WorkflowEvidenceDocument?.FileName ?? link.FileUploadRecord?.OriginalFileName,
                Sha256 = link.WorkflowEvidenceDocument?.Sha256,
                VerificationStatus = link.WorkflowEvidenceDocument?.VerificationStatus.ToString() ?? link.FileUploadRecord?.VirusScanStatus.ToString(),
                ReferenceAvailable = link.ReferenceKind == ProcurementControlEvidenceReferenceKind.ExternalReference ||
                                     link.WorkflowEvidenceDocument is not null || link.FileUploadRecord is not null
            }).ToList()
    };

    private static object PolicyAuditShape(ProcurementMasterDataControlPolicy item) => new
    {
        item.Id,
        item.PolicyKey,
        item.ResourceType,
        item.Version,
        item.Status,
        item.Name,
        MakerRoles = DeserializeRoles(item.MakerRolesJson),
        CheckerRoles = DeserializeRoles(item.CheckerRolesJson),
        item.RequireIndependentApproval,
        item.RequireRevalidation,
        item.RequireEvidence,
        item.WorkflowDefinitionId,
        item.EffectiveFromUtc,
        item.EffectiveToUtc,
        item.SupersedesPolicyId
    };

    private static object RequestAuditShape(ProcurementMasterDataChangeRequest item) => new
    {
        item.Id,
        item.RequestNumber,
        item.PolicyId,
        item.PolicyVersion,
        item.ResourceType,
        item.TargetKind,
        item.TargetId,
        item.TargetReference,
        item.Status,
        item.BeforeJson,
        item.BeforeHash,
        item.ProposedChangesJson,
        item.ProposedChangesHash,
        item.AppliedAfterJson,
        item.AppliedAfterHash,
        item.Reason,
        item.EffectiveAtUtc,
        item.MakerUserId,
        item.SubmittedById,
        item.SubmittedAtUtc,
        item.CheckerUserId,
        item.CheckedAtUtc,
        item.CheckerComment,
        item.RevalidationPassed,
        item.RevalidatedAtUtc,
        item.RevalidationMessage,
        item.RevalidatedSnapshotHash,
        item.AppliedById,
        item.AppliedAtUtc,
        item.WorkflowDefinitionId,
        item.WorkflowInstanceId,
        item.CorrelationId
    };

    private static void ApplyPolicy(ProcurementMasterDataControlPolicy entity, SaveProcurementMasterDataPolicyRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Description = TruncateNullable(request.Description, 1000);
        entity.MakerRolesJson = JsonSerializer.Serialize(NormalizeRoles(request.MakerRoles), JsonOptions);
        entity.CheckerRolesJson = JsonSerializer.Serialize(NormalizeRoles(request.CheckerRoles), JsonOptions);
        entity.RequireIndependentApproval = request.RequireIndependentApproval;
        entity.RequireRevalidation = request.RequireRevalidation;
        entity.RequireEvidence = request.RequireEvidence;
        entity.WorkflowDefinitionId = request.WorkflowDefinitionId;
        entity.EffectiveFromUtc = EnsureUtc(request.EffectiveFromUtc);
        entity.EffectiveToUtc = request.EffectiveToUtc.HasValue ? EnsureUtc(request.EffectiveToUtc.Value) : null;
    }

    private static void ValidatePolicyRequest(SaveProcurementMasterDataPolicyRequest request)
    {
        ProcurementMasterDataResourceRegistry.Get(request.ResourceType);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ProcurementMasterDataChangeValidationException("POLICY_NAME_REQUIRED", "Policy name is required.");
        if (request.EffectiveFromUtc == default)
            throw new ProcurementMasterDataChangeValidationException("EFFECTIVE_DATE_REQUIRED", "EffectiveFromUtc is required.");
        if (request.EffectiveToUtc.HasValue && EnsureUtc(request.EffectiveToUtc.Value) < EnsureUtc(request.EffectiveFromUtc))
            throw new ProcurementMasterDataChangeValidationException("EFFECTIVE_PERIOD_INVALID", "EffectiveToUtc cannot precede EffectiveFromUtc.");
        if (!request.RequireIndependentApproval)
            throw new ProcurementMasterDataChangeValidationException("INDEPENDENT_APPROVAL_REQUIRED", "TDC-0007 policies must require independent maker-checker approval.");
        if (!request.RequireRevalidation)
            throw new ProcurementMasterDataChangeValidationException("REVALIDATION_REQUIRED", "TDC-0007 policies must require revalidation before approval and application.");
        ValidateConfiguredRoles(request.MakerRoles, request.CheckerRoles);
    }

    private static void ValidateConfiguredRoles(IEnumerable<string> makers, IEnumerable<string> checkers)
    {
        var makerRoles = NormalizeRoles(makers);
        var checkerRoles = NormalizeRoles(checkers);
        if (makerRoles.Count == 0)
            throw new ProcurementMasterDataChangeValidationException("MAKER_ROLE_REQUIRED", "At least one maker role is required.");
        if (checkerRoles.Count == 0)
            throw new ProcurementMasterDataChangeValidationException("CHECKER_ROLE_REQUIRED", "At least one checker role is required.");
        if (makerRoles.Intersect(checkerRoles, StringComparer.OrdinalIgnoreCase).Any())
            throw new ProcurementMasterDataChangeValidationException("ROLE_SEPARATION_REQUIRED", "Maker and checker role sets must be disjoint.");
        foreach (var role in makerRoles.Concat(checkerRoles))
        {
            if (role is "SuperAdmin" or "TenantAdmin") continue;
            var definition = ProcurementAccessControlRegistry.FindRole(role)
                ?? throw new ProcurementMasterDataChangeValidationException("ROLE_UNKNOWN", $"'{role}' is not a registered TDC procurement or tenant-administration role.");
            if (definition.IsReadOnly)
                throw new ProcurementMasterDataChangeValidationException("READ_ONLY_ROLE", $"'{role}' is read-only and cannot be configured as maker or checker.");
        }
    }

    private void EnsureMaker(ProcurementMasterDataControlPolicy policy)
    {
        if (!HasAnyRole(DeserializeRoles(policy.MakerRolesJson)))
            throw new ProcurementMasterDataChangeAuthorizationException("The current user does not hold a configured maker role for this resource.");
    }

    private void EnsureChecker(ProcurementMasterDataControlPolicy policy)
    {
        if (!HasAnyRole(DeserializeRoles(policy.CheckerRolesJson)))
            throw new ProcurementMasterDataChangeAuthorizationException("The current user does not hold a configured checker role for this resource.");
    }

    private void EnsureMakerOrChecker(ProcurementMasterDataControlPolicy policy)
    {
        if (!HasAnyRole(DeserializeRoles(policy.MakerRolesJson)) && !HasAnyRole(DeserializeRoles(policy.CheckerRolesJson)) && !IsAdministrator())
            throw new ProcurementMasterDataChangeAuthorizationException("The current user is not authorized to revalidate this request.");
    }

    private void EnsureIndependentChecker(ProcurementMasterDataChangeRequest entity)
    {
        if (entity.Policy.RequireIndependentApproval &&
            (entity.MakerUserId == _currentUser.UserId || entity.SubmittedById == _currentUser.UserId))
            throw new ProcurementMasterDataChangeAuthorizationException("The maker or submitter cannot approve, reject, or apply the same change request.");
    }

    private bool HasAnyRole(IEnumerable<string> roles) => roles.Any(_currentUser.HasRole);

    private void EnsureReader()
    {
        EnsureAuthenticatedTenant();
        if (IsAdministrator() || _currentUser.HasRole(ProcurementAccessControlRegistry.InternalAuditRole) ||
            _currentUser.Roles.Any(role => ProcurementAccessControlRegistry.FindRole(role) is not null)) return;
        throw new ProcurementMasterDataChangeAuthorizationException("A TDC procurement role or tenant-administration role is required.");
    }

    private void EnsureAdministrator()
    {
        EnsureAuthenticatedTenant();
        if (!IsAdministrator())
            throw new ProcurementMasterDataChangeAuthorizationException("SuperAdmin or TenantAdmin is required to configure maker-checker policies.");
    }

    private void EnsureSuperAdministrator()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.HasRole("SuperAdmin"))
            throw new ProcurementMasterDataChangeAuthorizationException("SuperAdmin is required to activate or retire a maker-checker policy.");
    }

    private bool IsAdministrator() => _currentUser.HasRole("SuperAdmin") || _currentUser.HasRole("TenantAdmin");

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            throw new ProcurementMasterDataChangeAuthorizationException("An authenticated user is required.");
        if (_currentUser.TenantId == Guid.Empty)
            throw new ProcurementMasterDataChangeAuthorizationException("An authenticated tenant context is required.");
    }

    private static void EnsureRowVersion(byte[] current, string? supplied, string target)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw new ProcurementMasterDataChangeConflictException($"The {target} row version is required. Reload before saving.");
        byte[] parsed;
        try { parsed = Convert.FromBase64String(supplied); }
        catch { throw new ProcurementMasterDataChangeConflictException($"The {target} row version is invalid. Reload before saving."); }
        if (!current.SequenceEqual(parsed))
            throw new ProcurementMasterDataChangeConflictException($"The {target} changed by another user. Reload before saving.");
    }

    private static void EnsureReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ProcurementMasterDataChangeValidationException("REASON_REQUIRED", "A reason or comment is required.");
    }

    private static List<string> NormalizeRoles(IEnumerable<string> roles) => roles
        .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();

    private static List<string> DeserializeRoles(string json)
    {
        try { return NormalizeRoles(JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>()); }
        catch { return new List<string>(); }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string NormalizeCorrelation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : Truncate(value.Trim(), 100);
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string? TruncateNullable(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), length);
    private static JsonElement JsonElementFrom(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record LoadedTarget(BaseEntity Entity, ProcurementMasterDataTargetKind Kind, string Reference, bool IsNewTenantSingleton);
    private sealed record RevalidationResult(bool Passed, string Message);
    private sealed class BeneficialOwnerInput
    {
        public string Name { get; set; } = string.Empty;
        public decimal OwnershipPercent { get; set; }
        public string? Nationality { get; set; }
        public string? RegistrationNumber { get; set; }
        public bool PoliticallyExposed { get; set; }
    }
}
