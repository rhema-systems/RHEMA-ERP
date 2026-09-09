using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Inventory;

internal sealed class InventoryDisposalService : IInventoryDisposalService, IInventoryDisposalReportSource
{
    private const string EntityType = "InventoryDisposal";
    private const string CommitteeMemberRole = "TDC_DISPOSAL_COMMITTEE_MEMBER";
    private const int CommitteeQuorum = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IProcurementSodGuardService _sod;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IInventoryDisposalStockAdjustmentParticipant _disposalAdjustments;
    private readonly IStockAdjustmentValuationIntentBuilder _valuationIntent;
    private readonly IFinanceProducerIntentService _producerIntents;
    private readonly IFinanceProducerIntentGroupService _producerGroups;
    private readonly IFinanceProducerApprovedExecution _producerExecution;
    private readonly IFinanceProducerIntentGroupApprovedExecution _producerGroupExecution;
    private readonly IInventoryTrackingControlService _trackingControls;
    private readonly IProcurementControlEventService _controlEvents;

    public InventoryDisposalService(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IProcurementSodGuardService sod,
        IWorkflowIntegrationService workflow,
        IInventoryDisposalStockAdjustmentParticipant disposalAdjustments,
        IStockAdjustmentValuationIntentBuilder valuationIntent,
        IFinanceProducerIntentService producerIntents,
        IFinanceProducerIntentGroupService producerGroups,
        IFinanceProducerApprovedExecution producerExecution,
        IFinanceProducerIntentGroupApprovedExecution producerGroupExecution,
        IInventoryTrackingControlService trackingControls,
        IProcurementControlEventService controlEvents)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
        _sod = sod;
        _workflow = workflow;
        _disposalAdjustments = disposalAdjustments;
        _valuationIntent = valuationIntent;
        _producerIntents = producerIntents;
        _producerGroups = producerGroups;
        _producerExecution = producerExecution;
        _producerGroupExecution = producerGroupExecution;
        _trackingControls = trackingControls;
        _controlEvents = controlEvents;
    }

    public Task<IReadOnlyList<InventoryDisposalDto>> GetAsync(
        InventoryDisposalStatus? status,
        Guid? warehouseId,
        int take,
        CancellationToken cancellationToken = default) =>
        GetSourceAsync(status, warehouseId, Math.Clamp(take, 1, 500), cancellationToken);

    public Task<IReadOnlyList<InventoryDisposalDto>> GetReportSourceAsync(
        InventoryDisposalStatus? status,
        Guid? warehouseId,
        CancellationToken cancellationToken = default) =>
        GetSourceAsync(status, warehouseId, null, cancellationToken);

    private async Task<IReadOnlyList<InventoryDisposalDto>> GetSourceAsync(
        InventoryDisposalStatus? status,
        Guid? warehouseId,
        int? take,
        CancellationToken cancellationToken)
    {
        EnsureActor();
        var query = FullQuery().AsNoTracking().Where(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted);
        if (status.HasValue) query = query.Where(value => value.Status == status.Value);
        if (warehouseId.HasValue) query = query.Where(value => value.WarehouseId == warehouseId.Value);
        var allowed = new List<InventoryDisposalDto>();
        var offset = 0;
        var pageSize = Math.Max(take ?? 500, 50);
        while (!take.HasValue || allowed.Count < take.Value)
        {
            var candidates = await query.OrderByDescending(value => value.RequestedAtUtc).ThenByDescending(value => value.Id)
                .Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
            foreach (var item in candidates)
            {
                if (await HasAccessAsync("procurement.inventory.read", item, cancellationToken)) allowed.Add(Map(item));
                if (take.HasValue && allowed.Count == take.Value) break;
            }
            offset += candidates.Count;
            if (candidates.Count < pageSize) break;
        }
        return allowed;
    }

    public async Task<InventoryDisposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var item = await FullQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == _currentUser.TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new InventoryDisposalNotFoundException("The inventory disposal case was not found in the current tenant.");
        await RequireAccessAsync("procurement.inventory.read", item, cancellationToken);
        return Map(item);
    }

    public async Task<InventoryDisposalDto> CreateAsync(
        CreateInventoryDisposalRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        if (request.WarehouseId == Guid.Empty || !Enum.IsDefined(request.Method) || request.Lines.Count == 0)
            throw Error("INV_DISPOSAL_REQUEST_INVALID", "A warehouse, disposal method and at least one positive line are required.");
        var key = Required(request.IdempotencyKey, 100, "Idempotency key");
        var correlation = Correlation(request.CorrelationId);
        var payloadHash = Hash(request);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var replay = await FullQuery().SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                value.IdempotencyKey == key && !value.IsDeleted, cancellationToken);
            if (replay is not null)
            {
                EnsurePayload(replay.PayloadHash, payloadHash);
                await transaction.CommitAsync(cancellationToken);
                return Map(replay);
            }
            var warehouse = await _db.Warehouses.SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                value.Id == request.WarehouseId && value.IsActive && !value.IsDeleted, cancellationToken)
                ?? throw Error("INV_DISPOSAL_WAREHOUSE_INVALID", "The selected active warehouse was not found in the current tenant.");
            var duplicateKeys = request.Lines.GroupBy(value => new
                {
                    value.InventoryItemId,
                    value.LocationId,
                    LotNumber = TrackingKey(value.LotNumber),
                    BatchNumber = TrackingKey(value.BatchNumber),
                    SerialNumber = TrackingKey(value.SerialNumber)
                })
                .Where(value => value.Count() > 1).ToList();
            if (duplicateKeys.Count != 0) throw Error("INV_DISPOSAL_LINE_DUPLICATE", "The same item/location/tracking line cannot be identified twice.");

            var id = Guid.NewGuid();
            var item = new InventoryDisposalCase
            {
                Id = id,
                TenantId = _currentUser.TenantId,
                DisposalNumber = $"IDP-{DateTime.UtcNow:yyyyMMdd}-{id.ToString("N")[..8].ToUpperInvariant()}",
                WarehouseId = warehouse.Id,
                Status = InventoryDisposalStatus.Identified,
                Method = request.Method,
                Reason = Required(request.Reason, 1000, "Reason"),
                IdentificationDetails = Required(request.IdentificationDetails, 2000, "Identification details"),
                RequestedById = _currentUser.UserId,
                RequestedAtUtc = DateTime.UtcNow,
                IdempotencyKey = key,
                PayloadHash = payloadHash,
                CorrelationId = correlation,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedById = _currentUser.UserId
            };

            foreach (var input in request.Lines)
            {
                if (input.InventoryItemId == Guid.Empty || input.LocationId == Guid.Empty || input.Quantity <= 0m)
                    throw Error("INV_DISPOSAL_LINE_INVALID", "Every disposal line requires an item, exact location and positive quantity.");
                var location = await _db.WarehouseLocations.SingleOrDefaultAsync(value => value.TenantId == item.TenantId &&
                    value.Id == input.LocationId && value.IsActive && !value.IsDeleted &&
                    ((value.IsConsignmentBin && value.ConsignmentWarehouseId == item.WarehouseId) ||
                     (!value.IsConsignmentBin && value.WarehouseId == item.WarehouseId)),
                    cancellationToken) ?? throw Error("INV_DISPOSAL_LOCATION_INVALID", "A disposal location is inactive or outside the selected warehouse.");
                await RequireLocationAccessAsync("procurement.inventory.disposal.request", item.WarehouseId,
                    location.Id, item.DisposalNumber, item.CorrelationId, cancellationToken);
                var inventoryWarehouseId = location.InventoryWarehouseId;
                var inventoryItem = await _db.InventoryItems.SingleOrDefaultAsync(value => value.TenantId == item.TenantId &&
                    value.Id == input.InventoryItemId && !value.IsDeleted, cancellationToken)
                    ?? throw Error("INV_DISPOSAL_ITEM_INVALID", "A disposal item was not found in the current tenant.");
                var balance = await _db.InventoryBalances.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == item.TenantId &&
                    value.InventoryItemId == input.InventoryItemId && value.WarehouseId == inventoryWarehouseId &&
                    value.LocationId == input.LocationId && !value.IsDeleted, cancellationToken);
                if (balance is null || balance.QuantityAvailable < input.Quantity)
                    throw Error("INV_DISPOSAL_STOCK_UNAVAILABLE", $"Exact-location available stock is insufficient for {inventoryItem.ItemCode}.");
                var activeLines = _db.InventoryDisposalLines.AsNoTracking().Where(value => value.TenantId == item.TenantId &&
                        value.InventoryItemId == input.InventoryItemId && value.LocationId == input.LocationId && !value.IsDeleted &&
                        value.InventoryDisposalCase.Status != InventoryDisposalStatus.Completed &&
                        value.InventoryDisposalCase.Status != InventoryDisposalStatus.Rejected &&
                        value.InventoryDisposalCase.Status != InventoryDisposalStatus.Cancelled &&
                        !value.InventoryDisposalCase.IsDeleted);
                var activeQuantity = await activeLines.SumAsync(value => (decimal?)value.Quantity, cancellationToken) ?? 0m;
                var lotKey = TrackingKey(input.LotNumber);
                var batchKey = TrackingKey(input.BatchNumber);
                var serialKey = TrackingKey(input.SerialNumber);
                var exactReservedQuantity = lotKey.Length == 0 && batchKey.Length == 0 && serialKey.Length == 0
                    ? 0m
                    : await activeLines.Where(value =>
                            (lotKey.Length == 0 || (value.LotNumber != null && value.LotNumber.Trim().ToUpper() == lotKey)) &&
                            (batchKey.Length == 0 || (value.BatchNumber != null && value.BatchNumber.Trim().ToUpper() == batchKey)) &&
                            (serialKey.Length == 0 || (value.SerialNumber != null && value.SerialNumber.Trim().ToUpper() == serialKey)))
                        .SumAsync(value => (decimal?)value.Quantity, cancellationToken) ?? 0m;
                if (serialKey.Length != 0 && exactReservedQuantity > 0m)
                    throw Error("INV_DISPOSAL_STOCK_RESERVED", $"Serial {serialKey} is already identified by another active disposal case.");
                await _trackingControls.ValidateAvailabilityAsync(input.InventoryItemId, inventoryWarehouseId,
                    input.LocationId, input.Quantity + (serialKey.Length == 0 ? exactReservedQuantity : 0m),
                    input.LotNumber, input.BatchNumber, input.SerialNumber, cancellationToken);
                if (balance.QuantityAvailable - activeQuantity < input.Quantity)
                    throw Error("INV_DISPOSAL_STOCK_RESERVED", $"Stock already identified by another active disposal case leaves insufficient quantity for {inventoryItem.ItemCode}.");
                var unitCost = balance.AverageUnitCost > 0m ? balance.AverageUnitCost :
                    inventoryItem.AverageCost > 0m ? inventoryItem.AverageCost :
                    inventoryItem.StandardCost > 0m ? inventoryItem.StandardCost : inventoryItem.LastPurchaseCost;
                if (unitCost <= 0m) throw Error("INV_DISPOSAL_COST_MISSING", $"A server-derived valuation is required for {inventoryItem.ItemCode}.");
                var line = new InventoryDisposalLine
                {
                    Id = Guid.NewGuid(), TenantId = item.TenantId, InventoryDisposalCaseId = item.Id,
                    InventoryItemId = inventoryItem.Id, LocationId = location.Id, Quantity = input.Quantity,
                    UnitCost = decimal.Round(unitCost, 4), TotalValue = decimal.Round(input.Quantity * unitCost, 2),
                    LotNumber = Normalize(input.LotNumber, 100), BatchNumber = Normalize(input.BatchNumber, 100),
                    SerialNumber = Normalize(input.SerialNumber, 100),
                    ConditionNotes = Normalize(input.ConditionNotes, 1000), CreatedAt = DateTime.UtcNow,
                    CreatedById = _currentUser.UserId
                };
                line.IntegrityHash = Hash(new { line.InventoryItemId, line.LocationId, line.Quantity, line.UnitCost,
                    line.TotalValue, line.LotNumber, line.BatchNumber, line.SerialNumber });
                item.Lines.Add(line);
            }
            item.TotalQuantity = item.Lines.Sum(value => value.Quantity);
            item.TotalValue = item.Lines.Sum(value => value.TotalValue);
            await AddEvidenceAsync(item, request.Evidence, "Identification", required: true, cancellationToken);
            item.IntegrityHash = CaseHash(item);
            _db.InventoryDisposalCases.Add(item);
            AddAction(item, InventoryDisposalActionType.Identified, key, payloadHash, item.Reason);
            AddAudit(item, "Identified", null, Snapshot(item));
            await _db.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(item, "Identify", ProcurementControlEventResult.Allowed, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(await FullQuery().AsNoTracking().SingleAsync(value => value.Id == item.Id, cancellationToken));
        });
    }

    public Task<InventoryDisposalDto> VerifyAsync(Guid id, VerifyInventoryDisposalRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(id, request, async item =>
        {
            if (item.Status != InventoryDisposalStatus.Identified)
                throw State(item, "Only an identified disposal case can receive independent audit verification.");
            if (item.RequestedById == _currentUser.UserId)
                throw Error("INV_DISPOSAL_AUDIT_SOD", "The requester cannot audit-verify the same disposal case.");
            await RequireGlobalPermissionAsync("procurement.audit.read", item.DisposalNumber, cancellationToken);
            await RevalidateEvidenceAsync(item, cancellationToken);
            await AddEvidenceAsync(item, request.Evidence, "Audit", required: false, cancellationToken);
            item.AuditFindings = Required(request.Findings, 2000, "Audit findings");
            item.AuditVerifiedById = _currentUser.UserId;
            item.AuditVerifiedAtUtc = DateTime.UtcNow;
            if (!request.Verified)
            {
                item.Status = InventoryDisposalStatus.Rejected;
                item.RejectedById = _currentUser.UserId;
                item.RejectedAtUtc = DateTime.UtcNow;
                item.RejectionReason = item.AuditFindings;
                return InventoryDisposalActionType.AuditRejected;
            }
            item.Status = InventoryDisposalStatus.AuditVerified;
            return InventoryDisposalActionType.AuditVerified;
        }, cancellationToken);

    public Task<InventoryDisposalDto> ScheduleCommitteeAsync(Guid id, ScheduleInventoryDisposalCommitteeRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(id, request, async item =>
        {
            if (item.Status != InventoryDisposalStatus.AuditVerified)
                throw State(item, "Only an audit-verified disposal case can be scheduled for committee review.");
            await RequireAccessAsync("procurement.inventory.disposal.approve", item, cancellationToken);
            if (request.MeetingAtUtc <= DateTime.UtcNow || request.MemberUserIds.Distinct().Count() < CommitteeQuorum)
                throw Error("INV_DISPOSAL_COMMITTEE_INVALID", "A future meeting and at least three unique committee members are required.");
            var memberIds = request.MemberUserIds.Distinct().ToList();
            if (memberIds.Contains(item.RequestedById) || (item.AuditVerifiedById.HasValue && memberIds.Contains(item.AuditVerifiedById.Value)))
                throw Error("INV_DISPOSAL_COMMITTEE_SOD", "The requester and audit verifier cannot be disposal committee members.");
            var eligibleMembers = await _db.UserRoles
                .Where(value => memberIds.Contains(value.UserId) &&
                    value.User.TenantId == item.TenantId &&
                    value.User.IsActive &&
                    (value.Role.Name == CommitteeMemberRole || value.Role.NormalizedName == CommitteeMemberRole))
                .Select(value => value.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (eligibleMembers.Count != memberIds.Count)
                throw Error("INV_DISPOSAL_COMMITTEE_MEMBER_INVALID",
                    $"Every committee member must be an active user in the current tenant with the {CommitteeMemberRole} role.");
            item.CommitteeMeetingAtUtc = request.MeetingAtUtc.ToUniversalTime();
            item.CommitteeReference = Required(request.CommitteeReference, 100, "Committee reference");
            item.CommitteeScheduledById = _currentUser.UserId;
            foreach (var memberId in memberIds)
            {
                var member = new InventoryDisposalCommitteeMember
                {
                    Id = Guid.NewGuid(), TenantId = item.TenantId, InventoryDisposalCaseId = item.Id,
                    MemberUserId = memberId, CreatedAt = DateTime.UtcNow, CreatedById = _currentUser.UserId
                };
                member.IntegrityHash = MemberHash(member);
                item.CommitteeMembers.Add(member);
                _db.InventoryDisposalCommitteeMembers.Add(member);
            }
            item.Status = InventoryDisposalStatus.CommitteeScheduled;
            return InventoryDisposalActionType.CommitteeScheduled;
        }, cancellationToken);

    public Task<InventoryDisposalDto> VoteAsync(Guid id, VoteInventoryDisposalRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(id, request, async item =>
        {
            if (item.Status != InventoryDisposalStatus.CommitteeScheduled)
                throw State(item, "Committee votes are accepted only for a scheduled disposal case.");
            await RequireAccessAsync("procurement.inventory.disposal.approve", item, cancellationToken);
            if (item.CommitteeMeetingAtUtc > DateTime.UtcNow)
                throw Error("INV_DISPOSAL_COMMITTEE_NOT_OPEN", "Committee voting cannot begin before the scheduled meeting.");
            var member = item.CommitteeMembers.SingleOrDefault(value => value.MemberUserId == _currentUser.UserId)
                ?? throw new InventoryDisposalAuthorizationException("The current actor is not an appointed member of this disposal committee.");
            if (member.VotedAtUtc.HasValue) throw Error("INV_DISPOSAL_COMMITTEE_ALREADY_VOTED", "The appointed member has already recorded a final vote.");
            member.ConflictDeclared = request.ConflictDeclared;
            member.RecommendApproval = request.ConflictDeclared ? null : request.RecommendApproval;
            member.Comment = Normalize(request.Comment, 1000);
            member.VotedAtUtc = DateTime.UtcNow;
            member.IntegrityHash = MemberHash(member);
            var eligibleVotes = item.CommitteeMembers.Where(value => value.VotedAtUtc.HasValue && !value.ConflictDeclared && value.RecommendApproval.HasValue).ToList();
            var approvals = eligibleVotes.Count(value => value.RecommendApproval == true);
            var rejections = eligibleVotes.Count(value => value.RecommendApproval == false);
            if (approvals >= CommitteeQuorum && approvals > rejections)
            {
                item.Status = InventoryDisposalStatus.CommitteeRecommended;
                item.CommitteeRecommendedAtUtc = DateTime.UtcNow;
            }
            else if (rejections >= CommitteeQuorum && rejections >= approvals)
            {
                item.Status = InventoryDisposalStatus.Rejected;
                item.RejectedById = _currentUser.UserId;
                item.RejectedAtUtc = DateTime.UtcNow;
                item.RejectionReason = "The appointed disposal committee rejected the case.";
            }
            return item.Status == InventoryDisposalStatus.CommitteeRecommended
                ? InventoryDisposalActionType.CommitteeRecommended
                : item.Status == InventoryDisposalStatus.Rejected
                    ? InventoryDisposalActionType.CommitteeRejected
                    : InventoryDisposalActionType.CommitteeVoteRecorded;
        }, cancellationToken);

    public Task<InventoryDisposalDto> SubmitAsync(Guid id, SubmitInventoryDisposalRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(id, request, async item =>
        {
            if (item.Status != InventoryDisposalStatus.CommitteeRecommended)
                throw State(item, "Only a committee-recommended disposal case can enter the configured approval route.");
            await RequireAccessAsync("procurement.inventory.disposal.approve", item, cancellationToken);
            await RevalidateEvidenceAsync(item, cancellationToken);
            var workflow = await _workflow.SubmitAsync(EntityType, item.Id);
            if (!workflow.ExecutionResult.Success || workflow.Outcome != WorkflowOutcome.Pending || !workflow.ExecutionResult.WorkflowInstanceId.HasValue)
                throw Error("INV_DISPOSAL_WORKFLOW_INVALID", "The disposal workflow must start with an independent pending approval step.");
            item.WorkflowInstanceId = workflow.ExecutionResult.WorkflowInstanceId;
            item.Status = InventoryDisposalStatus.PendingApproval;
            item.AuthorityRoute = "Configured Disposal Committee / Managing Director / Board workflow";
            return InventoryDisposalActionType.Submitted;
        }, cancellationToken);

    public Task<InventoryDisposalDto> DecideAsync(Guid id, DecideInventoryDisposalRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(id, request, async item =>
        {
            if (item.Status != InventoryDisposalStatus.PendingApproval)
                throw State(item, "Only a pending disposal case can receive a workflow decision.");
            await RequireAccessAsync("procurement.inventory.disposal.approve", item, cancellationToken);
            var prohibited = new[] { item.RequestedById }
                .Concat(item.AuditVerifiedById.HasValue ? [item.AuditVerifiedById.Value] : [])
                .Concat(item.CommitteeMembers.Select(value => value.MemberUserId)).Distinct().ToList();
            await _sod.EnforceAsync(new ProcurementSodGuardRequest
            {
                ControlCode = "SOD-INVENTORY-DISPOSAL-APPROVAL",
                SourceType = EntityType,
                SourceReference = item.DisposalNumber,
                ProhibitedActorUserIds = prohibited
            }, item.CorrelationId, cancellationToken);
            if (!await _workflow.CanUserApproveAsync(EntityType, item.Id, _currentUser.UserId))
                throw new InventoryDisposalAuthorizationException("The current actor is not eligible for the active disposal workflow step.");
            await RevalidateEvidenceAsync(item, cancellationToken);
            var result = await _workflow.ProcessApprovalAsync(EntityType, item.Id, _currentUser.UserId,
                request.Approved ? "Approve" : "Reject", request.Comment);
            if (!result.ExecutionResult.Success)
                throw Error("INV_DISPOSAL_WORKFLOW_FAILED", result.ExecutionResult.Message ?? "The disposal workflow decision failed.");
            if (result.Outcome == WorkflowOutcome.Pending) return InventoryDisposalActionType.Submitted;
            if (request.Approved && result.Outcome != WorkflowOutcome.Approved)
                throw Error("INV_DISPOSAL_WORKFLOW_REJECTED", "The shared workflow did not approve the disposal case.");
            if (!request.Approved && result.Outcome == WorkflowOutcome.Approved)
                throw Error("INV_DISPOSAL_WORKFLOW_CONTRADICTION", "An approved workflow cannot be recorded as rejected.");
            if (request.Approved)
            {
                item.Status = InventoryDisposalStatus.Approved;
                item.ApprovedById = _currentUser.UserId;
                item.ApprovedAtUtc = DateTime.UtcNow;
                return InventoryDisposalActionType.Approved;
            }
            item.Status = InventoryDisposalStatus.Rejected;
            item.RejectedById = _currentUser.UserId;
            item.RejectedAtUtc = DateTime.UtcNow;
            item.RejectionReason = Required(request.Comment, 1000, "Rejection reason");
            return InventoryDisposalActionType.Rejected;
        }, cancellationToken);

    public async Task<InventoryDisposalDto> StageExecutionAsync(
        Guid id,
        StageInventoryDisposalExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var key = Required(request.IdempotencyKey, 100, "Idempotency key");
        var payloadHash = Hash(request);
        var item = await FullQuery().SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
            value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new InventoryDisposalNotFoundException("The inventory disposal case was not found in the current tenant.");
        var replay = item.Actions.SingleOrDefault(value => value.IdempotencyKey == key);
        if (replay is not null)
        {
            EnsurePayload(replay.PayloadHash, payloadHash);
            return Map(item);
        }
        EnsureRowVersion(item.RowVersion, request.RowVersion);
        if (item.Status != InventoryDisposalStatus.Approved && item.Status != InventoryDisposalStatus.AdjustmentPending)
            throw State(item, "Only an approved disposal case can stage controlled stock/Finance execution.");
        if (item.RequestedById == _currentUser.UserId)
            throw Error("INV_DISPOSAL_EXECUTION_SOD", "The disposal requester cannot stage execution of the same case.");
        await RequireAccessAsync("procurement.inventory.disposal.approve", item, cancellationToken);
        ValidateExecution(item.Method, request);
        var executionReference = Required(request.ExecutionReference, 200, "Execution reference");
        var plan = await BuildFinancePlanAsync(item, decimal.Round(request.ProceedsAmount, 2),
            request.ProceedsAccountId, executionReference, request.Evidence, _currentUser.UserId, cancellationToken);
        // Preparation is deliberately first. C7/C8 persist only Finance-owned evidence; no disposal,
        // stock-adjustment, evidence or inventory row is tracked until this succeeds.
        await PreparePlanAsync(plan, cancellationToken);

        await AddEvidenceAsync(item, request.Evidence, "Execution", required: true, cancellationToken);
        await RevalidateEvidenceAsync(item, cancellationToken);
        item.ProceedsAmount = decimal.Round(request.ProceedsAmount, 2);
        item.ProceedsAccountId = request.ProceedsAccountId;
        item.BuyerOrRecipient = Normalize(request.BuyerOrRecipient, 200);
        item.ExecutionReference = executionReference;
        item.StockAdjustmentId = plan.AdjustmentId;
        item.Status = InventoryDisposalStatus.AdjustmentPending;
        item.UpdatedAt = DateTime.UtcNow;
        item.LastModifiedById = _currentUser.UserId;
        item.IntegrityHash = CaseHash(item);
        AddAction(item, InventoryDisposalActionType.AdjustmentStaged, key, payloadHash,
            $"Finance producer intent {plan.ApprovalId:D} is pending an independent checker; controlled stock adjustment {plan.AdjustmentNumber} is not yet persisted.");
        AddAudit(item, "AdjustmentStaged", null, Snapshot(item));
        await _db.SaveChangesAsync(cancellationToken);
        await RecordControlEventAsync(item, "StageExecution", ProcurementControlEventResult.Allowed, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(await FullQuery().AsNoTracking().SingleAsync(value => value.Id == id, cancellationToken));
    }

    public async Task<InventoryDisposalDto> CompleteAsync(
        Guid id,
        CompleteInventoryDisposalRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var key = Required(request.IdempotencyKey, 100, "Idempotency key");
        var payloadHash = Hash(request);
        var item = await FullQuery().SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
            value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new InventoryDisposalNotFoundException("The inventory disposal case was not found in the current tenant.");
        var replay = item.Actions.SingleOrDefault(value => value.IdempotencyKey == key);
        if (replay is not null)
        {
            EnsurePayload(replay.PayloadHash, payloadHash);
            return Map(item);
        }
        EnsureRowVersion(item.RowVersion, request.RowVersion);
        if (item.Status != InventoryDisposalStatus.AdjustmentPending || !item.StockAdjustmentId.HasValue)
            throw State(item, "The approved disposal must first stage its controlled stock adjustment.");
        if (item.RequestedById == _currentUser.UserId || item.CommitteeMembers.Any(value => value.MemberUserId == _currentUser.UserId))
            throw Error("INV_DISPOSAL_COMPLETION_SOD", "The requester and disposal committee members cannot approve/post final stock execution.");
        await RequireAccessAsync("procurement.inventory.adjust.approve", item, cancellationToken);
        await RevalidateEvidenceAsync(item, cancellationToken);
        var plan = await BuildFinancePlanAsync(item, item.ProceedsAmount, item.ProceedsAccountId,
            Required(item.ExecutionReference, 200, "Execution reference"), [], StagingMaker(item), cancellationToken);
        var decision = await GetApprovedDecisionAsync(plan, cancellationToken);
        if (!string.Equals(decision.Status, "Approved", StringComparison.Ordinal) &&
            !string.Equals(decision.Status, "Failed", StringComparison.Ordinal) &&
            !string.Equals(decision.Status, "Posted", StringComparison.Ordinal))
            throw Error("INV_DISPOSAL_FINANCE_APPROVAL_REQUIRED",
                "The prepared neutral Finance intent requires an independent checker decision before completion.");
        if (!decision.DecidedByUserId.HasValue || !decision.DecidedAtUtc.HasValue)
            throw Error("INV_DISPOSAL_FINANCE_APPROVAL_REQUIRED",
                "The prepared neutral Finance intent does not retain complete independent-checker evidence.");

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    item = await FullQuery().SingleAsync(value => value.TenantId == _currentUser.TenantId && value.Id == id && !value.IsDeleted,
                        cancellationToken);
                    var stagedPlan = await BuildFinancePlanAsync(item, item.ProceedsAmount, item.ProceedsAccountId,
                        Required(item.ExecutionReference, 200, "Execution reference"), [], StagingMaker(item), cancellationToken);
                    var participantRequest = stagedPlan.StockAdjustmentRequest with
                    {
                        ApprovedById = decision.DecidedByUserId.Value,
                        ApprovedAtUtc = decision.DecidedAtUtc
                    };
                    var adjustment = await _disposalAdjustments.StageApprovedDisposalAsync(participantRequest, cancellationToken);
                    var receipt = ReceiptFor(stagedPlan);
                    var compatibility = stagedPlan.IsGroup
                        ? await _producerGroupExecution.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
                            stagedPlan.ApprovalId, stagedPlan.Group!, receipt, cancellationToken)
                        : null;
                    var single = stagedPlan.IsGroup
                        ? null
                        : await _producerExecution.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
                            stagedPlan.ApprovalId, stagedPlan.ValuationIntent, receipt, cancellationToken);
                    var valuation = stagedPlan.IsGroup
                        ? compatibility!.Members.Single(member => member.MemberOrder == 2)
                        : new FinanceProducerIntentGroupMemberExecutionResult(1, string.Empty,
                            single!.AccountingEventId, single.AccountingEventRequestFingerprint,
                            single.FinancePostingEventId, single.JournalEntryId);
                    await _disposalAdjustments.StagePostedDisposalAsync(adjustment, _currentUser.UserId,
                        valuation.FinancePostingEventId, valuation.JournalEntryId,
                        NegativeOverrides(item, stagedPlan, request), cancellationToken);
                    if (stagedPlan.IsGroup)
                    {
                        var proceeds = compatibility!.Members.Single(member => member.MemberOrder == 1);
                        item.ProceedsPostingEventId = proceeds.FinancePostingEventId;
                        item.ProceedsJournalEntryId = proceeds.JournalEntryId;
                    }
                    item.Status = InventoryDisposalStatus.Completed;
                    item.CompletedById = _currentUser.UserId;
                    item.CompletedAtUtc = DateTime.UtcNow;
                    item.UpdatedAt = DateTime.UtcNow;
                    item.LastModifiedById = _currentUser.UserId;
                    item.IntegrityHash = CaseHash(item);
                    AddAction(item, InventoryDisposalActionType.Completed, key, payloadHash,
                        $"Stock adjustment {stagedPlan.AdjustmentNumber} and approved neutral Finance intent completed atomically.");
                    AddAudit(item, "Completed", null, Snapshot(item));
                    await RecordControlEventAsync(item, "Complete", ProcurementControlEventResult.Allowed, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return Map(await FullQuery().AsNoTracking().SingleAsync(value => value.Id == id, cancellationToken));
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (Exception failure)
        {
            // C7/C8 explicitly reject durable failure evidence while an owner transaction remains open.
            if (_db.Database.CurrentTransaction is not null)
                throw Error("INV_DISPOSAL_ROLLBACK_REQUIRED", "The owner transaction did not end before Finance failure evidence.");
            _db.ChangeTracker.Clear();
            var receipt = ReceiptFor(plan);
            if (plan.IsGroup)
                await _producerGroupExecution.RecordFailureAfterRollbackAsync(plan.ApprovalId, plan.Group!, receipt, failure, cancellationToken);
            else
                await _producerExecution.RecordFailureAfterRollbackAsync(plan.ApprovalId, plan.ValuationIntent, receipt, failure, cancellationToken);
            throw;
        }
    }

    private async Task<InventoryDisposalDto> MutateAsync<TRequest>(
        Guid id,
        TRequest request,
        Func<InventoryDisposalCase, Task<InventoryDisposalActionType>> mutation,
        CancellationToken cancellationToken) where TRequest : InventoryDisposalMutationRequest
    {
        EnsureActor();
        var key = Required(request.IdempotencyKey, 100, "Idempotency key");
        var payloadHash = Hash(request);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var item = await FullQuery().SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                value.Id == id && !value.IsDeleted, cancellationToken)
                ?? throw new InventoryDisposalNotFoundException("The inventory disposal case was not found in the current tenant.");
            var replay = item.Actions.SingleOrDefault(value => value.IdempotencyKey == key);
            if (replay is not null)
            {
                EnsurePayload(replay.PayloadHash, payloadHash);
                await transaction.CommitAsync(cancellationToken);
                return Map(item);
            }
            EnsureRowVersion(item.RowVersion, request.RowVersion);
            var before = Snapshot(item);
            var action = await mutation(item);
            item.UpdatedAt = DateTime.UtcNow;
            item.LastModifiedById = _currentUser.UserId;
            item.IntegrityHash = CaseHash(item);
            AddAction(item, action, key, payloadHash, request.Comment);
            AddAudit(item, action.ToString(), before, Snapshot(item));
            await _db.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(item, action.ToString(),
                action is InventoryDisposalActionType.AuditRejected or InventoryDisposalActionType.CommitteeRejected or InventoryDisposalActionType.Rejected
                    ? ProcurementControlEventResult.Rejected : ProcurementControlEventResult.Allowed, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(await FullQuery().AsNoTracking().SingleAsync(value => value.Id == id, cancellationToken));
        });
    }

    private async Task<DisposalFinancePlan> BuildFinancePlanAsync(InventoryDisposalCase item, decimal proceeds,
        Guid? proceedsAccountId, string executionReference, IReadOnlyCollection<InventoryControlEvidenceRequest> additionalEvidence,
        Guid makerId, CancellationToken cancellationToken)
    {
        var isSaleOrAuction = item.Method is InventoryDisposalMethod.Sale or InventoryDisposalMethod.Auction;
        if (isSaleOrAuction && (proceeds <= 0m || !proceedsAccountId.HasValue || proceedsAccountId == Guid.Empty))
            throw Error("INV_DISPOSAL_PROCEEDS_ACCOUNT_REQUIRED", "A positive tenant Finance proceeds account is required for auction or sale proceeds.");
        if (!isSaleOrAuction && proceeds != 0m)
            throw Error("INV_DISPOSAL_PROCEEDS_NOT_ALLOWED", "Donation and destruction disposal methods cannot prepare proceeds.");

        var postingDate = StablePostingDate(item);
        var adjustmentId = item.StockAdjustmentId ?? DeterministicGuid(
            $"RHEMA:INV_DISPOSAL:ADJUSTMENT:V1:{item.TenantId:N}:{item.Id:N}");
        var sources = item.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.Id).ToList();
        if (sources.Count == 0 || sources.Select(SourceIdentity).Distinct(StringComparer.Ordinal).Count() != sources.Count)
            throw Error("INV_DISPOSAL_ADJUSTMENT_LINEAGE_INVALID",
                "Every disposal item/location/lot/batch/serial source line must be unique before Finance preparation.");
        var itemIds = sources.Select(line => DeterministicGuid(
                $"RHEMA:INV_DISPOSAL:ADJUSTMENT_LINE:V1:{item.TenantId:N}:{adjustmentId:N}:{line.Id:N}:{SourceIdentity(line)}"))
            .ToList();
        if (itemIds.Distinct().Count() != itemIds.Count)
            throw Error("INV_DISPOSAL_ADJUSTMENT_ID_COLLISION", "The deterministic disposal adjustment line identity collides; preparation is denied.");
        var evidence = item.Evidence.Select(value => new InventoryControlEvidenceRequest
            {
                CentralDocumentVersionId = value.CentralDocumentVersionId,
                EvidenceReference = value.EvidenceReference
            })
            .Concat(additionalEvidence)
            .GroupBy(value => value.CentralDocumentVersionId)
            .Select(group => group.First())
            .OrderBy(value => value.CentralDocumentVersionId)
            .ToList();
        var create = new CreateStockAdjustmentDto
        {
            WarehouseId = item.WarehouseId,
            AdjustmentDate = postingDate,
            ReasonCode = item.Method == InventoryDisposalMethod.Donation
                ? StockAdjustmentReasonCodes.Donation : StockAdjustmentReasonCodes.WriteOff,
            Description = $"Inventory disposal {item.DisposalNumber}: {item.Reason}",
            Reference = item.DisposalNumber,
            IdempotencyKey = $"disposal:{item.Id:N}:adjustment",
            CorrelationId = item.CorrelationId,
            Evidence = evidence,
            Items = sources.Select(source => new CreateStockAdjustmentItemDto
            {
                InventoryItemId = source.InventoryItemId, LocationId = source.LocationId,
                AdjustmentQuantity = -source.Quantity, LotNumber = source.LotNumber, BatchNumber = source.BatchNumber,
                SerialNumber = source.SerialNumber, UnitCost = source.UnitCost, Reason = item.Reason,
                Notes = $"{item.Method}: {executionReference}"
            }).ToList()
        };
        var participantRequest = new InventoryDisposalStockAdjustmentRequest
        {
            AdjustmentId = adjustmentId, ItemIds = itemIds,
            AdjustmentNumber = $"IDP-SA-{item.Id:N}"[..23].ToUpperInvariant(), PostingDateUtc = postingDate,
            RequestedById = makerId, Create = create
        };
        var preview = await _disposalAdjustments.PreviewDisposalAsync(participantRequest, cancellationToken);
        var owner = OwnerEffect(item, adjustmentId, itemIds, proceeds, proceedsAccountId, executionReference, postingDate);
        var valuation = await _valuationIntent.BuildAsync(preview, owner, cancellationToken);
        if (!isSaleOrAuction)
            return new DisposalFinancePlan(item.TenantId, adjustmentId, participantRequest.AdjustmentNumber,
                participantRequest, owner, valuation, null, null);

        var proceedsIntent = await BuildProceedsIntentAsync(item, proceeds, proceedsAccountId!.Value, executionReference,
            postingDate, owner, cancellationToken);
        var group = new ProducerIntentGroupRequestDto
        {
            ProducerIntentGroupId = DeterministicGuid($"RHEMA:INV_DISPOSAL:C8:V1:{item.TenantId:N}:{item.Id:N}"),
            GroupKind = "Original", IdempotencyKey = $"INVENTORY_DISPOSAL:{item.TenantId:N}:{item.Id:N}:COMPLETE",
            ParticipantIdentity = owner.ParticipantCode, ExpectedOwnerEffect = owner,
            Members = [proceedsIntent, valuation]
        };
        return new DisposalFinancePlan(item.TenantId, adjustmentId, participantRequest.AdjustmentNumber,
            participantRequest, owner, valuation, group, proceedsIntent);
    }

    private async Task<ProducerAccountingIntentDto> BuildProceedsIntentAsync(InventoryDisposalCase item, decimal proceeds,
        Guid proceedsAccountId, string executionReference, DateTime postingDate, ProducerOwnerEffectIdentityDto owner,
        CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking().SingleOrDefaultAsync(value =>
                value.TenantId == item.TenantId && !value.IsDeleted, cancellationToken)
            ?? throw Error("INV_DISPOSAL_FINANCE_SETTINGS_MISSING", "Finance settings are not configured for this tenant.");
        var recovery = settings.WriteOffRecoveryAccountId
            ?? throw Error("INV_DISPOSAL_RECOVERY_ACCOUNT_MISSING", "Write-off Recovery Account is not configured in Finance Settings.");
        var accounts = await _db.Accounts.AsNoTracking().Where(value => value.TenantId == item.TenantId && !value.IsDeleted &&
                (value.Id == proceedsAccountId || value.Id == recovery)).Select(value => value.Id).ToListAsync(cancellationToken);
        if (accounts.Count != 2) throw Error("INV_DISPOSAL_ACCOUNT_INVALID", "A configured disposal proceeds/recovery account is outside the current tenant.");
        var currency = string.IsNullOrWhiteSpace(settings.BaseCurrency) ? "GHS" : settings.BaseCurrency.Trim().ToUpperInvariant();
        var description = $"Inventory disposal proceeds {item.DisposalNumber} - {executionReference}";
        var posting = new ProducerFinancePostingRequestDto
        {
            SourceModule = "Inventory", OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = "InventoryDisposal", SourceDocumentId = item.Id, SourceDocumentTenantId = item.TenantId,
            PostingAction = "PostDisposalProceeds", SourceDocumentReference = item.DisposalNumber,
            Description = description, PostingDate = postingDate, JournalType = "System Generated",
            FunctionalCurrencyCode = currency,
            IdempotencyKey = $"InventoryDisposal:{item.TenantId:N}:{item.Id:N}:Proceeds",
            Lines =
            [
                FinanceLine(proceedsAccountId, description, proceeds, 0m, currency, 1, item.DisposalNumber,
                    "INV-DISPOSAL-PROCEEDS", postingDate, item.Id),
                FinanceLine(recovery, description, 0m, proceeds, currency, 2, item.DisposalNumber,
                    "INV-DISPOSAL-RECOVERY", postingDate, item.Id)
            ]
        };
        return new ProducerAccountingIntentDto
        {
            AccountingEventId = DeterministicGuid($"RHEMA:INV_DISPOSAL:PROCEEDS_EVENT:V1:{item.TenantId:N}:{item.Id:N}"),
            EventKind = "Original", IdempotencyKey = $"INVENTORY_DISPOSAL:{item.TenantId:N}:{item.Id:N}:PROCEEDS",
            ParticipantIdentity = owner.ParticipantCode, ExpectedOwnerEffect = owner, PostingRequest = posting
        };
    }

    private static FinancePostingLineDto FinanceLine(Guid accountId, string description, decimal debit, decimal credit,
        string currency, int lineNumber, string reference, string tag, DateTime postingDate, Guid sourceDocumentId) => new()
    {
        AccountId = accountId, SourceDocumentLineId = DeterministicGuid($"RHEMA:INV_DISPOSAL:PROCEEDS_LINE:V1:{sourceDocumentId:N}:{tag}"),
        Description = description, DebitAmount = debit, CreditAmount = credit, TransactionCurrency = currency,
        TransactionDebitAmount = debit, TransactionCreditAmount = credit, ExchangeRate = 1m,
        ExchangeRateSource = "Functional currency", ExchangeRateDate = postingDate,
        SourceReferenceNumber = reference, LineNumber = lineNumber, TransactionTag = tag
    };

    private async Task PreparePlanAsync(DisposalFinancePlan plan, CancellationToken cancellationToken)
    {
        if (plan.IsGroup)
            await _producerGroups.PrepareAsync(plan.Group!, cancellationToken);
        else
            await _producerIntents.PrepareAsync(plan.ValuationIntent, cancellationToken);
    }

    private async Task<DisposalFinanceDecision> GetApprovedDecisionAsync(DisposalFinancePlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.IsGroup)
        {
            var group = await _producerGroups.GetAsync(plan.ApprovalId, cancellationToken);
            return new DisposalFinanceDecision(group.Status, group.DecidedByUserId, group.DecidedAtUtc);
        }
        var intent = await _producerIntents.GetAsync(plan.ApprovalId, cancellationToken);
        return new DisposalFinanceDecision(intent.ProducerDecisionStatus, intent.ProducerDecidedByUserId,
            intent.ProducerDecidedAtUtc);
    }

    private static ProducerOwnerEffectReceiptDto ReceiptFor(DisposalFinancePlan plan) => new()
    {
        TenantId = plan.TenantId, ParticipantCode = plan.Owner.ParticipantCode,
        OwnerEntityType = plan.Owner.OwnerEntityType, OwnerEntityId = plan.Owner.OwnerEntityId,
        OwnerAction = plan.Owner.OwnerAction, EffectFingerprint = plan.Owner.EffectFingerprint
    };

    private static IReadOnlyDictionary<Guid, Guid> NegativeOverrides(InventoryDisposalCase item,
        DisposalFinancePlan plan, CompleteInventoryDisposalRequest request)
    {
        var sources = item.Lines.Where(line => !line.IsDeleted).OrderBy(line => line.Id).ToList();
        if (sources.Count != plan.StockAdjustmentRequest.ItemIds.Count)
            throw Error("INV_DISPOSAL_ADJUSTMENT_LINEAGE_INVALID", "The staged disposal source lines no longer match the prepared adjustment graph.");
        var sourceIds = sources.Select(line => line.Id).ToHashSet();
        if (request.NegativeStockOverrideIds.Keys.Any(key => !sourceIds.Contains(key)))
            throw Error("INV_DISPOSAL_ADJUSTMENT_LINEAGE_INVALID", "A negative-stock override does not belong to the prepared disposal source graph.");
        return sources.Select((source, index) => new { ItemId = plan.StockAdjustmentRequest.ItemIds[index],
                OverrideId = request.NegativeStockOverrideIds.GetValueOrDefault(source.Id) })
            .Where(value => value.OverrideId != Guid.Empty)
            .ToDictionary(value => value.ItemId, value => value.OverrideId);
    }

    private static ProducerOwnerEffectIdentityDto OwnerEffect(InventoryDisposalCase item, Guid adjustmentId,
        IReadOnlyList<Guid> itemIds, decimal proceeds, Guid? proceedsAccountId, string executionReference, DateTime postingDate)
    {
        var fingerprint = Hash(new
        {
            Contract = "RHEMA:INV_DISPOSAL:C11:OWNER_EFFECT:V1", item.TenantId, item.Id, adjustmentId,
            ItemIds = itemIds.Select(id => id.ToString("N")).ToArray(), item.Method, Proceeds = decimal.Round(proceeds, 2),
            ProceedsAccountId = proceedsAccountId, ExecutionReference = executionReference.Trim().ToUpperInvariant(), PostingDate = postingDate
        });
        return new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = "INVENTORY.DISPOSAL.V1", OwnerEntityType = "INVENTORY_DISPOSAL",
            OwnerEntityId = item.Id, OwnerAction = "COMPLETE", EffectFingerprint = fingerprint
        };
    }

    private static DateTime StablePostingDate(InventoryDisposalCase item)
    {
        var durable = item.ApprovedAtUtc ?? item.RequestedAtUtc;
        return DateTime.SpecifyKind(durable.Date, DateTimeKind.Utc);
    }

    private static string SourceIdentity(InventoryDisposalLine line) => string.Join('|',
        line.InventoryItemId.ToString("N"), line.LocationId.ToString("N"), TrackingKey(line.LotNumber),
        TrackingKey(line.BatchNumber), TrackingKey(line.SerialNumber));

    private static Guid StagingMaker(InventoryDisposalCase item) => item.Actions
        .Where(action => action.ActionType == InventoryDisposalActionType.AdjustmentStaged)
        .OrderByDescending(action => action.Sequence).Select(action => action.ActorUserId).FirstOrDefault()
        is var maker && maker != Guid.Empty
            ? maker
            : throw Error("INV_DISPOSAL_FINANCE_MAKER_MISSING",
                "The prepared disposal Finance intent has no durable staging-maker evidence.");

    private static Guid DeterministicGuid(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record DisposalFinancePlan(Guid TenantId, Guid AdjustmentId, string AdjustmentNumber,
        InventoryDisposalStockAdjustmentRequest StockAdjustmentRequest, ProducerOwnerEffectIdentityDto Owner,
        ProducerAccountingIntentDto ValuationIntent, ProducerIntentGroupRequestDto? Group,
        ProducerAccountingIntentDto? ProceedsIntent)
    {
        public bool IsGroup => Group is not null;
        public Guid ApprovalId => Group?.ProducerIntentGroupId ?? ValuationIntent.AccountingEventId
            ?? throw new InvalidOperationException("INV_DISPOSAL_FINANCE_IDENTITY_REQUIRED: prepared Finance identity is required.");
    }

    private sealed record DisposalFinanceDecision(string Status, Guid? DecidedByUserId, DateTime? DecidedAtUtc);

    private async Task AddEvidenceAsync(InventoryDisposalCase item, IEnumerable<InventoryControlEvidenceRequest> requests,
        string stage, bool required, CancellationToken cancellationToken)
    {
        var inputs = requests.ToList();
        if (required && inputs.Count == 0) throw Error("INV_DISPOSAL_EVIDENCE_REQUIRED", $"Current malware-clean central-DMS evidence is required for {stage.ToLowerInvariant()}.");
        if (inputs.GroupBy(value => value.CentralDocumentVersionId).Any(value => value.Count() > 1))
            throw Error("INV_DISPOSAL_EVIDENCE_DUPLICATE", "The same central-DMS version cannot be linked twice in one request.");
        foreach (var input in inputs)
        {
            if (item.Evidence.Any(value => value.CentralDocumentVersionId == input.CentralDocumentVersionId)) continue;
            var version = await _db.CentralDocumentVersions.Include(value => value.DocumentRecord)
                .SingleOrDefaultAsync(value => value.Id == input.CentralDocumentVersionId && value.TenantId == item.TenantId &&
                    !value.IsDeleted && value.DocumentRecord.TenantId == item.TenantId && !value.DocumentRecord.IsDeleted &&
                    value.DocumentRecord.LifecycleStatus == CentralDocumentEvidenceRules.ActiveLifecycleStatus &&
                    value.DocumentRecord.VersionStatus == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    value.DocumentRecord.CurrentVersion == value.VersionNumber &&
                    value.Status == CentralDocumentEvidenceRules.PublishedVersionStatus && value.PublishedAt.HasValue &&
                    value.FileUploadRecordId.HasValue, cancellationToken)
                ?? throw Error("INV_DISPOSAL_EVIDENCE_NOT_CURRENT", "Evidence must reference the current published central-DMS version.");
            var upload = await _db.FileUploadRecords.AsNoTracking().SingleOrDefaultAsync(value => value.Id == version.FileUploadRecordId &&
                value.TenantId == item.TenantId && !value.IsDeleted && value.VirusScanStatus == FileVirusScanStatus.Clean, cancellationToken)
                ?? throw Error("INV_DISPOSAL_EVIDENCE_NOT_CLEAN", "Disposal evidence must have a clean centralized malware-scan result.");
            var evidence = new InventoryDisposalEvidence
            {
                Id = Guid.NewGuid(), TenantId = item.TenantId, InventoryDisposalCaseId = item.Id,
                CentralDocumentVersionId = version.Id, FileUploadRecordId = upload.Id, Stage = stage,
                EvidenceReference = Required(input.EvidenceReference, 500, "Evidence reference"),
                CreatedAt = DateTime.UtcNow, CreatedById = _currentUser.UserId
            };
            evidence.IntegrityHash = Hash(new { evidence.InventoryDisposalCaseId, evidence.CentralDocumentVersionId,
                evidence.FileUploadRecordId, evidence.Stage, evidence.EvidenceReference });
            item.Evidence.Add(evidence);
            _db.InventoryDisposalEvidence.Add(evidence);
        }
    }

    private async Task RevalidateEvidenceAsync(InventoryDisposalCase item, CancellationToken cancellationToken)
    {
        if (item.Evidence.Count == 0) throw Error("INV_DISPOSAL_EVIDENCE_REQUIRED", "The disposal case has no controlled evidence.");
        foreach (var evidence in item.Evidence)
        {
            var current = await _db.CentralDocumentVersions.Include(value => value.DocumentRecord)
                .AnyAsync(value => value.Id == evidence.CentralDocumentVersionId && value.FileUploadRecordId == evidence.FileUploadRecordId &&
                    value.TenantId == item.TenantId && !value.IsDeleted && !value.DocumentRecord.IsDeleted &&
                    value.DocumentRecord.LifecycleStatus == CentralDocumentEvidenceRules.ActiveLifecycleStatus &&
                    value.DocumentRecord.VersionStatus == CentralDocumentEvidenceRules.PublishedVersionStatus &&
                    value.DocumentRecord.CurrentVersion == value.VersionNumber &&
                    value.Status == CentralDocumentEvidenceRules.PublishedVersionStatus && value.PublishedAt.HasValue &&
                    _db.FileUploadRecords.Any(upload => upload.Id == evidence.FileUploadRecordId && upload.TenantId == item.TenantId &&
                        !upload.IsDeleted && upload.VirusScanStatus == FileVirusScanStatus.Clean), cancellationToken);
            if (!current) throw Error("INV_DISPOSAL_EVIDENCE_STALE", "Linked disposal evidence is no longer current, published and malware-clean.");
        }
    }

    private async Task RequireAccessAsync(string permission, InventoryDisposalCase item, CancellationToken cancellationToken)
    {
        foreach (var locationId in item.Lines.Select(value => (Guid?)value.LocationId).Distinct())
        {
            var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission, WarehouseId = item.WarehouseId, LocationId = locationId,
                RequireLocationScope = true, SourceType = EntityType, SourceReference = item.DisposalNumber
            }, item.CorrelationId, cancellationToken);
            if (!decision.Allowed) throw new InventoryDisposalAuthorizationException(decision.Message);
        }
    }

    private async Task<bool> HasAccessAsync(string permission, InventoryDisposalCase item, CancellationToken cancellationToken)
    {
        try { await RequireAccessAsync(permission, item, cancellationToken); return true; }
        catch (InventoryDisposalAuthorizationException) { return false; }
        catch (ProcurementAccessAuthorizationException) { return false; }
        catch (ProcurementAccessValidationException) { return false; }
    }

    private async Task RequireGlobalPermissionAsync(string permission, string reference, CancellationToken cancellationToken)
    {
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission, RequireLocationScope = false, SourceType = EntityType, SourceReference = reference
        }, Correlation(null), cancellationToken);
        if (!decision.Allowed) throw new InventoryDisposalAuthorizationException(decision.Message);
    }

    private static void ValidateExecution(InventoryDisposalMethod method, StageInventoryDisposalExecutionRequest request)
    {
        var proceedsMethod = method is InventoryDisposalMethod.Auction or InventoryDisposalMethod.Sale;
        if (proceedsMethod && (request.ProceedsAmount <= 0m || !request.ProceedsAccountId.HasValue || string.IsNullOrWhiteSpace(request.BuyerOrRecipient)))
            throw Error("INV_DISPOSAL_PROCEEDS_REQUIRED", "Auction and sale require positive proceeds, a Finance proceeds account and buyer details.");
        if (!proceedsMethod && request.ProceedsAmount != 0m)
            throw Error("INV_DISPOSAL_PROCEEDS_NOT_ALLOWED", "Write-off, donation and destruction cannot record sale proceeds.");
        if (method == InventoryDisposalMethod.Donation && string.IsNullOrWhiteSpace(request.BuyerOrRecipient))
            throw Error("INV_DISPOSAL_RECIPIENT_REQUIRED", "Donation requires the recipient name.");
    }

    private IQueryable<InventoryDisposalCase> FullQuery() => _db.InventoryDisposalCases
        .Include(value => value.Warehouse)
        .Include(value => value.RequestedBy)
        .Include(value => value.Lines).ThenInclude(value => value.InventoryItem)
        .Include(value => value.Lines).ThenInclude(value => value.Location)
        .Include(value => value.Evidence).ThenInclude(value => value.CentralDocumentVersion).ThenInclude(value => value.DocumentRecord)
        .Include(value => value.CommitteeMembers).ThenInclude(value => value.MemberUser)
        .Include(value => value.Actions).ThenInclude(value => value.ActorUser)
        .AsSplitQuery();

    private void AddAction(InventoryDisposalCase item, InventoryDisposalActionType type, string key, string payloadHash, string? comment)
    {
        var sequence = item.Actions.Count == 0 ? 1 : item.Actions.Max(value => value.Sequence) + 1;
        var action = new InventoryDisposalAction
        {
            Id = Guid.NewGuid(), TenantId = item.TenantId, InventoryDisposalCaseId = item.Id,
            Sequence = sequence, ActionType = type, ActorUserId = _currentUser.UserId,
            OccurredAtUtc = DateTime.UtcNow, IdempotencyKey = key, PayloadHash = payloadHash,
            CorrelationId = item.CorrelationId, Comment = Normalize(comment, 1000),
            SnapshotJson = JsonSerializer.Serialize(Snapshot(item), JsonOptions), CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUser.UserId
        };
        action.IntegrityHash = Hash(new { action.InventoryDisposalCaseId, action.Sequence, action.ActionType,
            action.ActorUserId, action.OccurredAtUtc, action.IdempotencyKey, action.PayloadHash, action.SnapshotJson });
        item.Actions.Add(action);
        _db.InventoryDisposalActions.Add(action);
    }

    private void AddAudit(InventoryDisposalCase item, string action, object? before, object after) =>
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = item.TenantId, UserId = _currentUser.UserId,
            Username = _currentUser.Username, Action = action, Resource = EntityType, ResourceId = item.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(after, JsonOptions), IpAddress = "system", Timestamp = DateTime.UtcNow
        });

    private Task RecordControlEventAsync(InventoryDisposalCase item, string action, ProcurementControlEventResult result,
        CancellationToken cancellationToken) => _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
    {
        EventKey = $"inventory-disposal:{item.Id:N}:{action.ToLowerInvariant()}:{item.Actions.Count}",
        EventType = "InventoryDisposalControl", Action = action, Result = result, RuleCode = "INV-020",
        DecisionKeys = Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToList(),
        SourceType = EntityType, SourceId = item.Id, SourceReference = item.DisposalNumber,
        Reason = item.Reason, Before = null, After = Snapshot(item), CorrelationId = item.CorrelationId,
        OccurredAtUtc = DateTime.UtcNow,
        Evidence = item.Evidence.Select(value => new ProcurementControlEventEvidenceReference
        {
            ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
            ReferenceId = value.FileUploadRecordId, Reference = value.EvidenceReference,
            Label = $"Central DMS disposal {value.Stage.ToLowerInvariant()} evidence", RequirementKey = "INV-020"
        }).ToList()
    }, cancellationToken);

    private static object Snapshot(InventoryDisposalCase item) => new
    {
        item.Id, item.DisposalNumber, item.WarehouseId, item.Status, item.Method, item.Reason,
        item.RequestedById, item.AuditVerifiedById, item.CommitteeMeetingAtUtc, item.CommitteeReference,
        item.WorkflowInstanceId, item.AuthorityRoute, item.ApprovedById, item.StockAdjustmentId,
        item.ProceedsAmount, item.ProceedsPostingEventId, item.TotalQuantity, item.TotalValue, item.IntegrityHash
    };

    private static string CaseHash(InventoryDisposalCase item) => Hash(new
    {
        item.Id, item.TenantId, item.DisposalNumber, item.WarehouseId, item.Status, item.Method,
        item.RequestedById, item.AuditVerifiedById, item.CommitteeReference, item.WorkflowInstanceId,
        item.ApprovedById, item.RejectedById, item.StockAdjustmentId, item.ProceedsAmount,
        item.ProceedsPostingEventId, item.TotalQuantity, item.TotalValue, item.PayloadHash
    });

    private static string MemberHash(InventoryDisposalCommitteeMember member) => Hash(new
    {
        member.InventoryDisposalCaseId, member.MemberUserId, member.RecommendApproval,
        member.ConflictDeclared, member.VotedAtUtc, member.Comment
    });

    private static InventoryDisposalDto Map(InventoryDisposalCase item) => new()
    {
        Id = item.Id, DisposalNumber = item.DisposalNumber, WarehouseId = item.WarehouseId,
        WarehouseCode = item.Warehouse?.Code ?? string.Empty, WarehouseName = item.Warehouse?.Name ?? string.Empty,
        Status = item.Status, Method = item.Method, Reason = item.Reason, IdentificationDetails = item.IdentificationDetails,
        RequestedById = item.RequestedById, RequestedByName = item.RequestedBy?.FullName ?? string.Empty,
        RequestedAtUtc = item.RequestedAtUtc, AuditVerifiedById = item.AuditVerifiedById,
        AuditVerifiedAtUtc = item.AuditVerifiedAtUtc, AuditFindings = item.AuditFindings,
        CommitteeMeetingAtUtc = item.CommitteeMeetingAtUtc, CommitteeReference = item.CommitteeReference,
        AuthorityRoute = item.AuthorityRoute, WorkflowInstanceId = item.WorkflowInstanceId,
        ApprovedById = item.ApprovedById, ApprovedAtUtc = item.ApprovedAtUtc, StockAdjustmentId = item.StockAdjustmentId,
        ProceedsAmount = item.ProceedsAmount, BuyerOrRecipient = item.BuyerOrRecipient,
        ExecutionReference = item.ExecutionReference, ProceedsPostingEventId = item.ProceedsPostingEventId,
        ProceedsJournalEntryId = item.ProceedsJournalEntryId, CompletedAtUtc = item.CompletedAtUtc,
        FinanceProducerApprovalId = item.StockAdjustmentId.HasValue ? FinanceApprovalId(item) : null,
        FinanceProducerApprovalIsGroup = item.Method is InventoryDisposalMethod.Sale or InventoryDisposalMethod.Auction,
        TotalQuantity = item.TotalQuantity, TotalValue = item.TotalValue, RowVersion = Convert.ToBase64String(item.RowVersion),
        Lines = item.Lines.OrderBy(value => value.CreatedAt).Select(value => new InventoryDisposalLineDto
        {
            Id = value.Id, InventoryItemId = value.InventoryItemId, ItemCode = value.InventoryItem?.ItemCode ?? string.Empty,
            ItemName = value.InventoryItem?.Name ?? string.Empty, LocationId = value.LocationId,
            LocationCode = value.Location?.LocationCode ?? string.Empty, Quantity = value.Quantity,
            UnitCost = value.UnitCost, TotalValue = value.TotalValue, LotNumber = value.LotNumber,
            BatchNumber = value.BatchNumber, SerialNumber = value.SerialNumber, ConditionNotes = value.ConditionNotes
        }).ToList(),
        Evidence = item.Evidence.OrderBy(value => value.CreatedAt).Select(value => new InventoryDisposalEvidenceDto
        {
            Id = value.Id, CentralDocumentVersionId = value.CentralDocumentVersionId,
            FileUploadRecordId = value.FileUploadRecordId, Stage = value.Stage,
            EvidenceReference = value.EvidenceReference,
            DocumentReference = value.CentralDocumentVersion?.DocumentRecord?.DocumentReference ?? string.Empty,
            VersionNumber = value.CentralDocumentVersion?.VersionNumber ?? string.Empty
        }).ToList(),
        CommitteeMembers = item.CommitteeMembers.OrderBy(value => value.CreatedAt).Select(value => new InventoryDisposalCommitteeMemberDto
        {
            MemberUserId = value.MemberUserId, MemberName = value.MemberUser?.FullName ?? string.Empty,
            RecommendApproval = value.RecommendApproval, ConflictDeclared = value.ConflictDeclared,
            VotedAtUtc = value.VotedAtUtc, Comment = value.Comment
        }).ToList(),
        Actions = item.Actions.OrderBy(value => value.Sequence).Select(value => new InventoryDisposalActionDto
        {
            Sequence = value.Sequence, ActionType = value.ActionType, ActorUserId = value.ActorUserId,
            ActorName = value.ActorUser?.FullName ?? string.Empty, OccurredAtUtc = value.OccurredAtUtc,
            Comment = value.Comment
        }).ToList()
    };

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new InventoryDisposalAuthorizationException("An authenticated internal tenant actor is required.");
    }

    private static void EnsureRowVersion(byte[] current, string supplied)
    {
        byte[] requested;
        try { requested = Convert.FromBase64String(supplied); }
        catch { throw Error("INV_DISPOSAL_ROW_VERSION_INVALID", "The row version is invalid. Reload and retry."); }
        if (!current.SequenceEqual(requested))
            throw Error("INV_DISPOSAL_CONCURRENCY", "The disposal case changed. Reload and retry.");
    }

    private static void EnsurePayload(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw Error("INV_DISPOSAL_IDEMPOTENCY_CONFLICT", "The idempotency key already identifies a different disposal request.");
    }

    private static string Correlation(string? value) => Normalize(value, 100) ?? $"inventory-disposal:{Guid.NewGuid():N}";
    private static string Required(string? value, int max, string label) => Normalize(value, max)
        ?? throw Error("INV_DISPOSAL_VALUE_REQUIRED", $"{label} is required.");
    private static string? Normalize(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private async Task RequireLocationAccessAsync(
        string permission,
        Guid warehouseId,
        Guid locationId,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var decision = await _access.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = permission, WarehouseId = warehouseId, LocationId = locationId,
            RequireLocationScope = true, SourceType = EntityType, SourceReference = sourceReference
        }, correlationId, cancellationToken);
        if (!decision.Allowed) throw new InventoryDisposalAuthorizationException(decision.Message);
    }

    private static string TrackingKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static Guid FinanceApprovalId(InventoryDisposalCase item)
    {
        if (item.Method is InventoryDisposalMethod.Sale or InventoryDisposalMethod.Auction)
            return DeterministicGuid($"RHEMA:INV_DISPOSAL:C8:V1:{item.TenantId:N}:{item.Id:N}");
        var adjustmentId = item.StockAdjustmentId ?? throw Error("INV_DISPOSAL_FINANCE_IDENTITY_REQUIRED",
            "A staged stock adjustment identity is required for Finance approval.");
        return DeterministicGuid($"FIN:C9:STOCK_ADJUSTMENT_EVENT:V1:{item.TenantId:N}:{adjustmentId:N}:POST");
    }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static InventoryDisposalException Error(string code, string message) => new(code, message);
    private static InventoryDisposalException State(InventoryDisposalCase item, string message) =>
        Error("INV_DISPOSAL_STATE_CONFLICT", $"{message} Current status is {item.Status}.");
}
