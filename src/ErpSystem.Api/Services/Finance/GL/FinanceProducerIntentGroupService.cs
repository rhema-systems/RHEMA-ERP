using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Finance-owned maker/checker and zero-or-all boundary for a complete ordered set of neutral producer intents.
/// Members retain independent C5/C6 identity; callers never receive book or transaction-control capability.
/// </summary>
public sealed partial class FinanceProducerIntentGroupService : IFinanceProducerIntentGroupService,
    IFinanceProducerIntentGroupApprovedExecution
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly FinanceProducerIntentService _producer;
    private readonly ITrustedAccountingEventExecutor _events;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService _audit;
    private readonly FinanceProducerIntentGroupOptions _options;
    private readonly FinanceProducerIntentOptions _producerOptions;

    public FinanceProducerIntentGroupService(ApplicationDbContext db, FinanceProducerIntentService producer,
        AccountingEventService events, ICurrentUserService currentUser, IFinanceAuditService audit,
        IOptions<FinanceProducerIntentGroupOptions> options, IOptions<FinanceProducerIntentOptions> producerOptions)
        : this(db, producer, (ITrustedAccountingEventExecutor)events, currentUser, audit, options, producerOptions)
    {
    }

    internal FinanceProducerIntentGroupService(ApplicationDbContext db, FinanceProducerIntentService producer,
        ITrustedAccountingEventExecutor events, ICurrentUserService currentUser, IFinanceAuditService audit,
        IOptions<FinanceProducerIntentGroupOptions> options, IOptions<FinanceProducerIntentOptions> producerOptions)
    {
        _db = db;
        _producer = producer;
        _events = events;
        _currentUser = currentUser;
        _audit = audit;
        _options = options.Value;
        _producerOptions = producerOptions.Value;
    }

    public async Task<ProducerIntentGroupDto> PrepareAsync(ProducerIntentGroupRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        ValidateRequest(request, tenantId);
        var actor = RequireActor();
        var strategy = _db.Database.CreateExecutionStrategy();
        ProducerIntentGroup? prepared = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await BeginTransactionAsync(cancellationToken);
            try
            {
                var key = CanonicalKey(request.IdempotencyKey, "group idempotency key");
                await AcquireGroupLockAsync(tenantId, key, cancellationToken);
                var existing = await Query(tracking: true).SingleOrDefaultAsync(item => item.TenantId == tenantId
                    && item.IdempotencyKey == key && !item.IsDeleted, cancellationToken);
                if (existing is not null)
                {
                    if (existing.PreparedByUserId != actor)
                        throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MAKER_CONFLICT: exact retries require the original maker.");
                    BindDurableIds(request, existing);
                    var validatedEvents = await ValidateMembersAsync(request, existing.Members.OrderBy(item => item.MemberOrder).ToList(), cancellationToken);
                    await RequireDurableMemberLineageAsync(existing, cancellationToken);
                    RequireGroupMatch(existing, request, validatedEvents);
                    prepared = existing;
                    if (tx is not null) await tx.CommitAsync(cancellationToken);
                    return;
                }

                var lineage = await ResolveLineageAsync(tenantId, request, cancellationToken);
                var groupId = request.ProducerIntentGroupId.GetValueOrDefault(Guid.NewGuid());
                if (groupId == Guid.Empty) groupId = Guid.NewGuid();
                request.ProducerIntentGroupId = groupId;
                BindNewMemberIds(request);
                ValidateMemberLineage(request, lineage.Target);
                var memberEvents = new List<AccountingEventDto>(request.Members.Count);
                foreach (var intent in request.Members)
                {
                    var eventRequest = await _producer.BuildRequestAsync(intent, cancellationToken);
                    memberEvents.Add(await _events.PrepareGroupMemberInAmbientTransactionAsync(eventRequest, cancellationToken));
                }
                var evidence = CanonicalEvidence(request, memberEvents);
                var snapshot = JsonSerializer.Serialize(evidence, SnapshotOptions);
                var fingerprint = Sha256($"RHEMA.FIN.C8.GROUP.V1\n{snapshot}");
                var owner = CanonicalOwnerEffect(request.ExpectedOwnerEffect, request.ParticipantIdentity);
                var now = DateTime.UtcNow;
                prepared = new ProducerIntentGroup
                {
                    Id = groupId, TenantId = tenantId, IdempotencyKey = CanonicalKey(request.IdempotencyKey, "group idempotency key"),
                    GroupKind = lineage.Kind, Version = lineage.Version,
                    RootProducerIntentGroupId = lineage.RootId == Guid.Empty ? groupId : lineage.RootId,
                    SupersedesProducerIntentGroupId = lineage.SupersedesId,
                    CorrectsProducerIntentGroupId = lineage.CorrectsId,
                    ReversesProducerIntentGroupId = lineage.ReversesId,
                    Status = ProducerIntentGroupStatuses.PendingApproval,
                    MemberCount = memberEvents.Count,
                    ParticipantCode = owner.ParticipantCode, OwnerEntityType = owner.OwnerEntityType,
                    OwnerEntityId = owner.OwnerEntityId, OwnerAction = owner.OwnerAction,
                    ExpectedOwnerEffectFingerprint = owner.EffectFingerprint,
                    RequestSnapshotJson = snapshot, RequestSnapshotHash = Sha256(snapshot), GroupFingerprint = fingerprint,
                    PreparedByUserId = actor, PreparedAtUtc = now, CreatedAt = now, CreatedBy = ActorName(),
                    Members = memberEvents.Select((item, index) => new ProducerIntentGroupMember
                    {
                        TenantId = tenantId, AccountingEventId = item.Id, MemberOrder = index + 1,
                        MemberFingerprint = item.RequestFingerprint, CreatedAt = now, CreatedBy = ActorName()
                    }).ToList()
                };
                RequireMemberLineage(prepared, memberEvents, lineage.Target);
                _db.ProducerIntentGroups.Add(prepared);
                await _db.SaveChangesAsync(cancellationToken);
                await AuditAsync(FinanceAuditEvents.ProducerIntentGroupPrepared, prepared, cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
        return Map(prepared!);
    }

    public Task<ProducerIntentGroupDto> ApproveAsync(Guid groupId, DecideProducerIntentGroupRequestDto request,
        CancellationToken cancellationToken = default) => DecideAsync(groupId, request, approve: true, cancellationToken);

    public Task<ProducerIntentGroupDto> RejectAsync(Guid groupId, DecideProducerIntentGroupRequestDto request,
        CancellationToken cancellationToken = default) => DecideAsync(groupId, request, approve: false, cancellationToken);

    public Task<ProducerIntentGroupDto> ApprovePreparedAsync(Guid groupId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default) =>
        DecidePreparedAsync(groupId, decision, approve: true, cancellationToken);

    public Task<ProducerIntentGroupDto> RejectPreparedAsync(Guid groupId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default) =>
        DecidePreparedAsync(groupId, decision, approve: false, cancellationToken);

    private async Task<ProducerIntentGroupDto> DecidePreparedAsync(Guid groupId,
        DecideProducerAccountingIntentDto decision, bool approve, CancellationToken cancellationToken)
    {
        RequireEnabled();
        ArgumentNullException.ThrowIfNull(decision);
        if (string.IsNullOrWhiteSpace(decision.Reason))
            throw new InvalidOperationException("A governed group approval or rejection reason is required.");
        var request = await ReconstructPreparedGroupRequestAsync(groupId, cancellationToken);
        var governed = new DecideProducerIntentGroupRequestDto { Group = request, Reason = decision.Reason };
        return await DecideAsync(groupId, governed, approve, cancellationToken);
    }

    private async Task<ProducerIntentGroupDto> DecideAsync(Guid groupId, DecideProducerIntentGroupRequestDto decision,
        bool approve, CancellationToken cancellationToken)
    {
        RequireEnabled();
        ArgumentNullException.ThrowIfNull(decision);
        if (string.IsNullOrWhiteSpace(decision.Reason))
            throw new InvalidOperationException("A governed group approval or rejection reason is required.");
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        ValidateRequest(decision.Group, tenantId);
        var checker = RequireActor();
        var reason = decision.Reason.Trim();
        ProducerIntentGroup? group = null;
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await BeginTransactionAsync(cancellationToken);
            try
            {
                var key = CanonicalKey(decision.Group.IdempotencyKey, "group idempotency key");
                await AcquireGroupLockAsync(tenantId, key, cancellationToken);
                group = await Query(tracking: true).SingleOrDefaultAsync(item => item.TenantId == tenantId
                    && item.Id == groupId && !item.IsDeleted, cancellationToken)
                    ?? throw new KeyNotFoundException("Producer intent group was not found.");
                BindDurableIds(decision.Group, group);
                var members = await ValidateMembersAsync(decision.Group, group.Members.OrderBy(item => item.MemberOrder).ToList(), cancellationToken);
                await RequireDurableMemberLineageAsync(group, cancellationToken);
                RequireGroupMatch(group, decision.Group, members);
                var targetStatus = approve ? ProducerIntentGroupStatuses.Approved : ProducerIntentGroupStatuses.Rejected;
                if (group.Status == targetStatus)
                {
                    RequireDecisionMatch(group, checker, reason);
                    if (tx is not null) await tx.CommitAsync(cancellationToken);
                    return;
                }
                if (group.Status != ProducerIntentGroupStatuses.PendingApproval)
                    throw new InvalidOperationException("PRODUCER_INTENT_GROUP_DECISION_CONFLICT: only a pending complete group may be decided.");
                if (group.PreparedByUserId == checker)
                    throw new InvalidOperationException("The producer intent group checker must differ from its maker.");
                group.Status = targetStatus; group.DecidedByUserId = checker; group.DecidedAtUtc = DateTime.UtcNow;
                group.DecisionReason = reason;
                await _db.SaveChangesAsync(cancellationToken);
                await AuditAsync(approve ? FinanceAuditEvents.ProducerIntentGroupApproved
                    : FinanceAuditEvents.ProducerIntentGroupRejected, group, cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                throw;
            }
        });
        return Map(group!);
    }

    public async Task<ProducerIntentGroupDto> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var group = await Query().SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == groupId
            && !item.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("Producer intent group was not found.");
        RequireSnapshotIntegrity(group);
        if (group.MemberCount != group.Members.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_COUNT_CONFLICT: durable member cardinality changed.");
        await RequireDurableMemberLineageAsync(group, cancellationToken);
        return Map(group);
    }

    async Task<ProducerIntentGroupDto> IFinanceProducerIntentGroupApprovedExecution.ExecuteInAmbientTransactionAsync(
        Guid groupId, ProducerIntentGroupRequestDto request, ProducerOwnerEffectReceiptDto receipt,
        CancellationToken cancellationToken)
    {
        RequireEnabled();
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        ValidateRequest(request, tenantId);
        var transaction = RequireAmbientTransaction();
        await AcquireGroupLockAsync(tenantId, CanonicalKey(request.IdempotencyKey, "group idempotency key"), cancellationToken);
        var group = await Query(tracking: true).SingleOrDefaultAsync(item => item.TenantId == tenantId
            && item.Id == groupId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Producer intent group was not found.");
        BindDurableIds(request, group);
        var preparedRequests = await BuildAndValidateMembersAsync(request, group, cancellationToken);
        await RequireDurableMemberLineageAsync(group, cancellationToken);
        RequireGroupMatch(group, request, preparedRequests.Select(item => item.Event).ToList());
        RequireReceiptMatch(group, receipt, tenantId);
        await FinanceProducerOwnerEffectAuthority.AcquireAsync(_db, tenantId,
            receipt.ParticipantCode, receipt.EffectFingerprint, cancellationToken);
        if (await FinanceProducerOwnerEffectAuthority.IsUsedByAnotherAsync(_db, tenantId,
                receipt.ParticipantCode, receipt.EffectFingerprint, null, group.Id, cancellationToken))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_OWNER_EFFECT_REUSED: receipt effect already belongs to another Finance execution.");
        if (group.Status == ProducerIntentGroupStatuses.Posted)
        {
            RequirePersistedReceiptMatch(group.Receipt, receipt, group.GroupFingerprint);
            RequireTransactionIdentity(transaction.TransactionId);
            return Map(group);
        }
        if (group.Status is not (ProducerIntentGroupStatuses.Approved or ProducerIntentGroupStatuses.Failed)
            || !group.DecidedByUserId.HasValue || string.IsNullOrWhiteSpace(group.DecisionReason))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_APPROVAL_REQUIRED: execution requires complete independent approval.");
        if (group.Receipt is null)
        {
            group.Receipt = NewReceipt(group, receipt, RequireActor());
            _db.ProducerIntentGroupReceipts.Add(group.Receipt);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else RequirePersistedReceiptMatch(group.Receipt, receipt, group.GroupFingerprint);

        var now = DateTime.UtcNow;
        var attempt = new ProducerIntentGroupAttempt
        {
            TenantId = tenantId, ProducerIntentGroupId = group.Id,
            AttemptNumber = group.Attempts.Count == 0 ? 1 : group.Attempts.Max(item => item.AttemptNumber) + 1,
            GroupFingerprint = group.GroupFingerprint, Status = AccountingEventStatuses.Posted,
            StartedAtUtc = now, CreatedAt = now, CreatedBy = ActorName()
        };
        foreach (var member in preparedRequests.OrderBy(item => item.Member.MemberOrder))
        {
            try
            {
                await _events.ExecuteApprovedGroupMemberInAmbientTransactionAsync(member.Member.AccountingEventId,
                    new ReleaseAccountingEventDto { Request = member.Request, Reason = group.DecisionReason },
                    group.Id, group.DecidedByUserId.Value, transaction.TransactionId, cancellationToken);
                RequireTransactionIdentity(transaction.TransactionId);
            }
            catch (Exception error)
            {
                throw new ProducerIntentGroupMemberExecutionException(member.Member.MemberOrder,
                    member.Member.AccountingEventId, error);
            }
        }
        attempt.CompletedAtUtc = DateTime.UtcNow;
        _db.ProducerIntentGroupAttempts.Add(attempt);
        if (!_db.Database.IsSqlServer())
        {
            // SQL Server's C8 attempt trigger owns the paired terminal transition. Lightweight
            // relational test providers emulate that authority here without weakening production.
            group.Status = ProducerIntentGroupStatuses.Posted;
            group.CompletedAtUtc = attempt.CompletedAtUtc;
            group.FailureMessage = null;
        }
        await _db.SaveChangesAsync(cancellationToken);
        if (_db.Database.IsSqlServer()) await _db.Entry(group).ReloadAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.ProducerIntentGroupPosted, group, cancellationToken);
        RequireTransactionIdentity(transaction.TransactionId);
        return Map(group);
    }

    async Task<FinanceProducerIntentGroupApprovedExecutionResult>
        IFinanceProducerIntentGroupApprovedExecution.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
            Guid groupId, ProducerIntentGroupRequestDto request, ProducerOwnerEffectReceiptDto receipt,
            CancellationToken cancellationToken)
    {
        var group = await ((IFinanceProducerIntentGroupApprovedExecution)this)
            .ExecuteInAmbientTransactionAsync(groupId, request, receipt, cancellationToken);
        return await FinanceProducerCompatibilityAuthority.ResolveGroupAsync(
            _db, _currentUser.GetRequiredFinanceTenantId(), group, cancellationToken);
    }

    async Task IFinanceProducerIntentGroupApprovedExecution.RecordFailureAfterRollbackAsync(Guid groupId,
        ProducerIntentGroupRequestDto request, ProducerOwnerEffectReceiptDto receipt, Exception failure,
        CancellationToken cancellationToken)
    {
        RequireEnabled();
        ArgumentNullException.ThrowIfNull(failure);
        if (_db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_FAILURE_REQUIRES_ROLLBACK: owner transaction must end before durable failure evidence.");
        // Rollback never resets EF states; Finance clears before any fresh group/member query or write.
        _db.ChangeTracker.Clear();
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        ValidateRequest(request, tenantId);
        var group = await Query().SingleOrDefaultAsync(item => item.TenantId == tenantId
            && item.Id == groupId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Producer intent group was not found.");
        BindDurableIds(request, group);
        var members = await ValidateMembersAsync(request, group.Members.OrderBy(item => item.MemberOrder).ToList(), cancellationToken);
        await RequireDurableMemberLineageAsync(group, cancellationToken);
        RequireGroupMatch(group, request, members);
        RequireReceiptMatch(group, receipt, tenantId);
        if (group.Status == ProducerIntentGroupStatuses.Posted) return;
        if (group.Status == ProducerIntentGroupStatuses.Failed)
        {
            RequireExactFailureRetry(group, failure);
            return;
        }
        if (group.Status != ProducerIntentGroupStatuses.Approved)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_APPROVAL_REQUIRED: only approved group execution can record failure.");
        var failedMember = failure as ProducerIntentGroupMemberExecutionException;
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await AcquireGroupLockAsync(tenantId, group.IdempotencyKey, cancellationToken);
                // Validation above is read-only. Re-clear after acquiring durable authority so only the
                // freshly loaded failure aggregate can participate in this separate transaction.
                _db.ChangeTracker.Clear();
                var durable = await Query(tracking: true).SingleAsync(item => item.TenantId == tenantId && item.Id == groupId, cancellationToken);
                await RequireDurableMemberLineageAsync(durable, cancellationToken);
                RequireGroupMatch(durable, request, members);
                if (durable.Status == ProducerIntentGroupStatuses.Posted)
                {
                    await tx.CommitAsync(cancellationToken);
                    return;
                }
                if (durable.Status == ProducerIntentGroupStatuses.Failed)
                {
                    RequireExactFailureRetry(durable, failure);
                    await tx.CommitAsync(cancellationToken);
                    return;
                }
                if (durable.Status != ProducerIntentGroupStatuses.Approved)
                    throw new InvalidOperationException("PRODUCER_INTENT_GROUP_APPROVAL_REQUIRED: durable failure requires approved group authority.");
                var now = DateTime.UtcNow;
                var attempt = new ProducerIntentGroupAttempt
                {
                    TenantId = tenantId, ProducerIntentGroupId = durable.Id,
                    AttemptNumber = durable.Attempts.Count == 0 ? 1 : durable.Attempts.Max(item => item.AttemptNumber) + 1,
                    GroupFingerprint = durable.GroupFingerprint, Status = AccountingEventStatuses.Failed,
                    StartedAtUtc = now, CompletedAtUtc = now, FailedMemberOrder = failedMember?.MemberOrder,
                    FailedAccountingEventId = failedMember?.AccountingEventId, FailureMessage = Truncate(failure.Message, 1000),
                    CreatedAt = now, CreatedBy = ActorName()
                };
                _db.ProducerIntentGroupAttempts.Add(attempt);
                if (!_db.Database.IsSqlServer())
                {
                    durable.Status = ProducerIntentGroupStatuses.Failed;
                    durable.CompletedAtUtc = now;
                    durable.FailureMessage = attempt.FailureMessage;
                }
                await _db.SaveChangesAsync(cancellationToken);
                if (_db.Database.IsSqlServer()) await _db.Entry(durable).ReloadAsync(cancellationToken);
                await AuditAsync(FinanceAuditEvents.ProducerIntentGroupFailed, durable, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch { await tx.RollbackAsync(cancellationToken); _db.ChangeTracker.Clear(); throw; }
        });
    }

    private async Task<List<(ProducerIntentGroupMember Member, CreateAccountingEventDto Request, AccountingEventDto Event)>>
        BuildAndValidateMembersAsync(ProducerIntentGroupRequestDto request, ProducerIntentGroup group, CancellationToken ct)
    {
        var durable = group.Members.OrderBy(item => item.MemberOrder).ToList();
        var result = new List<(ProducerIntentGroupMember, CreateAccountingEventDto, AccountingEventDto)>(durable.Count);
        for (var index = 0; index < durable.Count; index++)
        {
            var prepared = await _producer.BuildPreparedRequestAsync(durable[index].AccountingEventId, request.Members[index], ct);
            var validated = await _events.ValidatePreparedGroupMemberAsync(durable[index].AccountingEventId, prepared, ct);
            result.Add((durable[index], prepared, validated));
        }
        return result;
    }

    private async Task<ProducerIntentGroupRequestDto> ReconstructPreparedGroupRequestAsync(
        Guid groupId, CancellationToken cancellationToken)
    {
        // Group decisions reload every ordered C7 member from Finance evidence; the checker cannot
        // resubmit, reorder or silently omit economics while deciding the complete maker package.
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var group = await Query().SingleOrDefaultAsync(item => item.TenantId == tenantId
            && item.Id == groupId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Producer intent group was not found.");
        RequireSnapshotIntegrity(group);
        if (group.PreparedByUserId == Guid.Empty || group.MemberCount != group.Members.Count
            || string.IsNullOrWhiteSpace(group.ParticipantCode))
            throw new InvalidOperationException(
                "PRODUCER_INTENT_GROUP_PREPARED_AUTHORITY_INVALID: durable maker, participant or member authority is incomplete.");
        await RequireDurableMemberLineageAsync(group, cancellationToken);

        var orderedMembers = group.Members.OrderBy(item => item.MemberOrder).ToList();
        var intents = new List<ProducerAccountingIntentDto>(orderedMembers.Count);
        var memberEvents = new List<AccountingEventDto>(orderedMembers.Count);
        for (var index = 0; index < orderedMembers.Count; index++)
        {
            var member = orderedMembers[index];
            if (member.MemberOrder != index + 1
                || member.AccountingEvent is null
                || member.AccountingEvent.TenantId != tenantId
                || member.AccountingEvent.PreparedByUserId != group.PreparedByUserId
                || !string.Equals(member.MemberFingerprint,
                    member.AccountingEvent.RequestFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "PRODUCER_INTENT_GROUP_MEMBER_AUTHORITY: durable member order, tenant, maker or fingerprint is invalid.");
            var frozen = await _producer.ReconstructPreparedRequestAsync(
                member.AccountingEventId, cancellationToken);
            if (!string.Equals(member.MemberFingerprint,
                    AccountingEventService.Fingerprint(frozen,
                        frozen.SelectionIdempotencyKey.Trim().ToUpperInvariant(),
                        member.AccountingEvent.Version, member.AccountingEvent.RootAccountingEventId),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "PRODUCER_INTENT_GROUP_MEMBER_AUTHORITY: member snapshot is not bound to the group fingerprint.");
            intents.Add(FinanceProducerIntentService.ToProducerIntent(frozen));
            memberEvents.Add(AccountingEventService.Map(member.AccountingEvent));
        }

        var request = new ProducerIntentGroupRequestDto
        {
            ProducerIntentGroupId = group.Id, GroupKind = group.GroupKind,
            SupersedesProducerIntentGroupId = group.SupersedesProducerIntentGroupId,
            CorrectsProducerIntentGroupId = group.CorrectsProducerIntentGroupId,
            ReversesProducerIntentGroupId = group.ReversesProducerIntentGroupId,
            IdempotencyKey = group.IdempotencyKey, ParticipantIdentity = group.ParticipantCode,
            ExpectedOwnerEffect = new ProducerOwnerEffectIdentityDto
            {
                ParticipantCode = group.ParticipantCode, OwnerEntityType = group.OwnerEntityType,
                OwnerEntityId = group.OwnerEntityId, OwnerAction = group.OwnerAction,
                EffectFingerprint = group.ExpectedOwnerEffectFingerprint
            },
            Members = intents
        };
        ValidateRequest(request, tenantId);
        RequireGroupMatch(group, request, memberEvents);
        return request;
    }

    private async Task<List<AccountingEventDto>> ValidateMembersAsync(ProducerIntentGroupRequestDto request,
        IReadOnlyList<ProducerIntentGroupMember> durable, CancellationToken ct)
    {
        if (durable.Count != request.Members.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_COUNT_CONFLICT: exact retries cannot add or drop members.");
        var result = new List<AccountingEventDto>(durable.Count);
        for (var index = 0; index < durable.Count; index++)
        {
            if (durable[index].MemberOrder != index + 1 || request.Members[index].AccountingEventId != durable[index].AccountingEventId)
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_ORDER_CONFLICT: immutable member order or identity changed.");
            var prepared = await _producer.BuildPreparedRequestAsync(durable[index].AccountingEventId, request.Members[index], ct);
            result.Add(await _events.ValidatePreparedGroupMemberAsync(durable[index].AccountingEventId, prepared, ct));
        }
        return result;
    }

    private static object CanonicalEvidence(ProducerIntentGroupRequestDto request, IReadOnlyList<AccountingEventDto> members) => new
    {
        groupId = request.ProducerIntentGroupId,
        kind = CanonicalKind(request.GroupKind),
        supersedes = request.SupersedesProducerIntentGroupId,
        corrects = request.CorrectsProducerIntentGroupId,
        reverses = request.ReversesProducerIntentGroupId,
        key = CanonicalKey(request.IdempotencyKey, "group idempotency key"),
        participant = CanonicalIdentity(request.ParticipantIdentity, 100, "participant identity"),
        owner = CanonicalOwnerEffect(request.ExpectedOwnerEffect, request.ParticipantIdentity),
        members = members.Select((item, index) => new { order = index + 1, eventId = item.Id, fingerprint = item.RequestFingerprint }).ToArray()
    };

    private static void RequireGroupMatch(ProducerIntentGroup group, ProducerIntentGroupRequestDto request,
        IReadOnlyList<AccountingEventDto> members)
    {
        RequireSnapshotIntegrity(group);
        if (group.MemberCount != members.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_COUNT_CONFLICT: durable member cardinality changed.");
        var evidence = CanonicalEvidence(request, members);
        var snapshot = JsonSerializer.Serialize(evidence, SnapshotOptions);
        var fingerprint = Sha256($"RHEMA.FIN.C8.GROUP.V1\n{snapshot}");
        if (!string.Equals(group.RequestSnapshotHash, Sha256(snapshot), StringComparison.Ordinal)
            || !string.Equals(group.GroupFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_RETRY_CONFLICT: group payload, order, identity or lineage changed.");
    }

    private static void RequireSnapshotIntegrity(ProducerIntentGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.RequestSnapshotJson)
            || !string.Equals(Sha256(group.RequestSnapshotJson), group.RequestSnapshotHash, StringComparison.Ordinal)
            || !string.Equals(Sha256($"RHEMA.FIN.C8.GROUP.V1\n{group.RequestSnapshotJson}"),
                group.GroupFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_SNAPSHOT_INVALID: immutable group evidence was altered.");
    }

    private static void BindNewMemberIds(ProducerIntentGroupRequestDto request)
    {
        foreach (var member in request.Members)
        {
            var id = member.AccountingEventId.GetValueOrDefault(Guid.NewGuid());
            member.AccountingEventId = id == Guid.Empty ? Guid.NewGuid() : id;
        }
    }

    private static void BindDurableIds(ProducerIntentGroupRequestDto request, ProducerIntentGroup group)
    {
        if (request.ProducerIntentGroupId.HasValue && request.ProducerIntentGroupId != group.Id)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_IDENTITY_CONFLICT: group identity changed.");
        request.ProducerIntentGroupId = group.Id;
        var durable = group.Members.OrderBy(item => item.MemberOrder).ToList();
        if (request.Members.Count != durable.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_COUNT_CONFLICT: exact retries cannot add or drop members.");
        for (var index = 0; index < durable.Count; index++)
        {
            if (request.Members[index].AccountingEventId.HasValue
                && request.Members[index].AccountingEventId != durable[index].AccountingEventId)
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_ORDER_CONFLICT: member identity changed.");
            request.Members[index].AccountingEventId = durable[index].AccountingEventId;
        }
    }

    private async Task<GroupLineage> ResolveLineageAsync(Guid tenantId, ProducerIntentGroupRequestDto request, CancellationToken ct)
    {
        var kind = CanonicalKind(request.GroupKind);
        if (kind == AccountingEventKinds.Original)
        {
            if (request.SupersedesProducerIntentGroupId.HasValue || request.CorrectsProducerIntentGroupId.HasValue
                || request.ReversesProducerIntentGroupId.HasValue)
                throw new InvalidOperationException("Original producer intent groups cannot carry correction or reversal lineage.");
            return new(kind, 1, Guid.Empty, null, null, null, null);
        }
        var targetId = kind == AccountingEventKinds.Correction ? request.CorrectsProducerIntentGroupId : request.ReversesProducerIntentGroupId;
        if (!targetId.HasValue || targetId == Guid.Empty || request.SupersedesProducerIntentGroupId != targetId)
            throw new InvalidOperationException("Producer intent group correction/reversal requires its exact canonical predecessor.");
        var target = await Query().SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == targetId
            && !item.IsDeleted, ct) ?? throw new InvalidOperationException("Producer intent group lineage target was not found for this tenant.");
        if (target.Status != ProducerIntentGroupStatuses.Posted)
            throw new InvalidOperationException("Only a posted producer intent group can be corrected or reversed.");
        if (await _db.ProducerIntentGroups.AsNoTracking().AnyAsync(item => item.TenantId == tenantId
            && item.SupersedesProducerIntentGroupId == target.Id && !item.IsDeleted, ct))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_CONFLICT: target already has a successor.");
        return new(kind, checked(target.Version + 1), target.RootProducerIntentGroupId, target.Id,
            kind == AccountingEventKinds.Correction ? target.Id : null,
            kind == AccountingEventKinds.Reversal ? target.Id : null, target);
    }

    private static void ValidateMemberLineage(ProducerIntentGroupRequestDto request, ProducerIntentGroup? target)
    {
        if (target is null) return;
        var prior = target.Members.OrderBy(item => item.MemberOrder).ToList();
        if (prior.Count != request.Members.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_MEMBER_COUNT: successors preserve every original member.");
        for (var index = 0; index < prior.Count; index++)
        {
            var expected = prior[index].AccountingEventId;
            var member = request.Members[index];
            var actual = CanonicalKind(request.GroupKind) == AccountingEventKinds.Correction
                ? member.CorrectsAccountingEventId : member.ReversesAccountingEventId;
            if (actual != expected || member.SupersedesAccountingEventId != expected)
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_MEMBER_ORDER: each successor member must target the same ordered original member.");
        }
    }

    private async Task RequireDurableMemberLineageAsync(ProducerIntentGroup group, CancellationToken ct)
    {
        ProducerIntentGroup? target = null;
        if (group.GroupKind != AccountingEventKinds.Original)
        {
            var targetId = group.SupersedesProducerIntentGroupId
                ?? throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_CONFLICT: successor predecessor is missing.");
            target = await Query().SingleOrDefaultAsync(item => item.TenantId == group.TenantId
                && item.Id == targetId && !item.IsDeleted, ct)
                ?? throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_CONFLICT: successor predecessor was not found.");
        }
        RequireMemberLineage(group, group.Members.OrderBy(item => item.MemberOrder)
            .Select(item => AccountingEventService.Map(item.AccountingEvent)).ToList(), target);
    }

    private static void RequireMemberLineage(ProducerIntentGroup group, IReadOnlyList<AccountingEventDto> events,
        ProducerIntentGroup? target)
    {
        var kind = CanonicalKind(group.GroupKind);
        if (events.Count != group.MemberCount || events.Any(item => item.EventKind != kind || item.Version != group.Version))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_KIND_VERSION: every member must match the group kind and version.");
        if (kind == AccountingEventKinds.Original)
        {
            if (target is not null || group.Version != 1 || group.RootProducerIntentGroupId != group.Id
                || events.Any(item => item.RootAccountingEventId != item.Id || item.SupersedesAccountingEventId.HasValue
                    || item.CorrectsAccountingEventId.HasValue || item.ReversesAccountingEventId.HasValue))
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_CONFLICT: original group/member lineage is invalid.");
            return;
        }
        if (target is null || target.Status != ProducerIntentGroupStatuses.Posted
            || group.Version != target.Version + 1 || group.RootProducerIntentGroupId != target.RootProducerIntentGroupId
            || group.SupersedesProducerIntentGroupId != target.Id
            || (kind == AccountingEventKinds.Correction && group.CorrectsProducerIntentGroupId != target.Id)
            || (kind == AccountingEventKinds.Reversal && group.ReversesProducerIntentGroupId != target.Id))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_CONFLICT: successor must bind the exact posted predecessor version and root.");
        var prior = target.Members.OrderBy(item => item.MemberOrder).ToList();
        if (prior.Count != events.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_MEMBER_COUNT: successors preserve every predecessor member.");
        for (var index = 0; index < events.Count; index++)
        {
            var before = prior[index].AccountingEvent;
            var after = events[index];
            if (prior[index].MemberOrder != index + 1 || before.Status != AccountingEventStatuses.Posted
                || after.RootAccountingEventId != before.RootAccountingEventId || after.Version != before.Version + 1
                || after.SupersedesAccountingEventId != before.Id
                || after.OriginatingModuleCode != before.OriginatingModuleCode
                || after.SourceDocumentType != before.SourceDocumentType
                || after.SourceDocumentId != before.SourceDocumentId || after.PostingAction != before.PostingAction
                || (kind == AccountingEventKinds.Correction
                    && (after.CorrectsAccountingEventId != before.Id || after.ReversesAccountingEventId.HasValue))
                || (kind == AccountingEventKinds.Reversal
                    && (after.ReversesAccountingEventId != before.Id || after.CorrectsAccountingEventId.HasValue)))
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_LINEAGE_MEMBER_ORDER: successor members must preserve exact ordered posted roots.");
        }
    }

    private static void ValidateRequest(ProducerIntentGroupRequestDto request, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Members is null || request.Members.Count < 2 || request.Members.Count > 20)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_CARDINALITY: a complete group requires 2 to 20 members.");
        if (request.Members.Any(member => member is null || member.PostingRequest is null))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_INVALID: every ordered member requires a neutral posting request.");
        _ = CanonicalKey(request.IdempotencyKey, "group idempotency key");
        var groupKind = CanonicalKind(request.GroupKind);
        var groupOwner = CanonicalOwnerEffect(request.ExpectedOwnerEffect, request.ParticipantIdentity);
        foreach (var member in request.Members)
        {
            if (CanonicalKind(member.EventKind) != groupKind)
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_MEMBER_KIND_VERSION: every member kind must match the group kind.");
            if (!string.Equals(CanonicalIdentity(member.ParticipantIdentity, 100, "member participant identity"),
                    CanonicalIdentity(request.ParticipantIdentity, 100, "participant identity"), StringComparison.Ordinal))
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_PARTICIPANT_CONFLICT: every member must bind the group participant.");
            var memberOwner = CanonicalOwnerEffect(member.ExpectedOwnerEffect, member.ParticipantIdentity);
            if (memberOwner.ParticipantCode != groupOwner.ParticipantCode
                || memberOwner.OwnerEntityType != groupOwner.OwnerEntityType
                || memberOwner.OwnerEntityId != groupOwner.OwnerEntityId
                || memberOwner.OwnerAction != groupOwner.OwnerAction
                || memberOwner.EffectFingerprint != groupOwner.EffectFingerprint)
                throw new InvalidOperationException("PRODUCER_INTENT_GROUP_OWNER_EFFECT_CONFLICT: every member must bind the one full-group owner effect.");
        }
        if (request.Members.Any(member => member.PostingRequest.SourceDocumentTenantId.HasValue
            && member.PostingRequest.SourceDocumentTenantId != tenantId))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_TENANT_CONFLICT: every member source must belong to the current tenant.");
        if (request.Members.Select(member => CanonicalKey(member.IdempotencyKey, "member idempotency key"))
            .Distinct(StringComparer.Ordinal).Count() != request.Members.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_DUPLICATE_MEMBER: member idempotency keys must be unique.");
        if (request.Members.Select(member => $"{member.PostingRequest.SourceModule}|{member.PostingRequest.SourceDocumentType}|{member.PostingRequest.SourceDocumentId:D}|{member.PostingRequest.PostingAction}")
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Members.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_DUPLICATE_SOURCE: each member retains a distinct source identity/action.");
        var ids = request.Members.Where(member => member.AccountingEventId.HasValue).Select(member => member.AccountingEventId!.Value).ToList();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_DUPLICATE_MEMBER: member event identities must be nonempty and unique.");
    }

    private static ProducerOwnerEffectIdentityDto CanonicalOwnerEffect(ProducerOwnerEffectIdentityDto effect, string participant)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var participantCode = CanonicalIdentity(effect.ParticipantCode, 100, "owner participant");
        if (!string.Equals(participantCode, CanonicalIdentity(participant, 100, "participant identity"), StringComparison.Ordinal))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_OWNER_PARTICIPANT_CONFLICT: receipt authority must bind the group participant.");
        var entity = CanonicalIdentity(effect.OwnerEntityType, 100, "owner entity type");
        var action = CanonicalIdentity(effect.OwnerAction, 60, "owner action");
        var fingerprint = effect.EffectFingerprint?.Trim().ToUpperInvariant() ?? string.Empty;
        if (effect.OwnerEntityId == Guid.Empty || fingerprint.Length != 64 || fingerprint.All(character => character == '0')
            || fingerprint.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_OWNER_EFFECT_INVALID: stable nonempty owner-effect evidence is required.");
        return new() { ParticipantCode = participantCode, OwnerEntityType = entity, OwnerEntityId = effect.OwnerEntityId,
            OwnerAction = action, EffectFingerprint = fingerprint };
    }

    private static void RequireReceiptMatch(ProducerIntentGroup group, ProducerOwnerEffectReceiptDto receipt, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var expected = new ProducerOwnerEffectIdentityDto { ParticipantCode = group.ParticipantCode,
            OwnerEntityType = group.OwnerEntityType, OwnerEntityId = group.OwnerEntityId,
            OwnerAction = group.OwnerAction, EffectFingerprint = group.ExpectedOwnerEffectFingerprint };
        var actual = CanonicalOwnerEffect(receipt, receipt.ParticipantCode);
        if (receipt.TenantId != tenantId || actual.ParticipantCode != expected.ParticipantCode
            || actual.OwnerEntityType != expected.OwnerEntityType || actual.OwnerEntityId != expected.OwnerEntityId
            || actual.OwnerAction != expected.OwnerAction || actual.EffectFingerprint != expected.EffectFingerprint)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_OWNER_EFFECT_CONFLICT: receipt does not match immutable group authority.");
    }

    private static ProducerIntentGroupReceipt NewReceipt(ProducerIntentGroup group, ProducerOwnerEffectReceiptDto receipt, Guid actor) => new()
    {
        TenantId = group.TenantId, ProducerIntentGroupId = group.Id,
        ParticipantCode = receipt.ParticipantCode.Trim().ToUpperInvariant(),
        OwnerEntityType = receipt.OwnerEntityType.Trim().ToUpperInvariant(), OwnerEntityId = receipt.OwnerEntityId,
        OwnerAction = receipt.OwnerAction.Trim().ToUpperInvariant(), EffectFingerprint = receipt.EffectFingerprint.Trim().ToUpperInvariant(),
        GroupFingerprint = group.GroupFingerprint, RecordedAtUtc = DateTime.UtcNow, RecordedByUserId = actor,
        CreatedAt = DateTime.UtcNow
    };

    private static void RequirePersistedReceiptMatch(ProducerIntentGroupReceipt? persisted,
        ProducerOwnerEffectReceiptDto receipt, string fingerprint)
    {
        if (persisted is null || persisted.ParticipantCode != receipt.ParticipantCode.Trim().ToUpperInvariant()
            || persisted.OwnerEntityType != receipt.OwnerEntityType.Trim().ToUpperInvariant()
            || persisted.OwnerEntityId != receipt.OwnerEntityId || persisted.OwnerAction != receipt.OwnerAction.Trim().ToUpperInvariant()
            || persisted.EffectFingerprint != receipt.EffectFingerprint.Trim().ToUpperInvariant()
            || persisted.GroupFingerprint != fingerprint)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_RECEIPT_CONFLICT: exact retry receipt evidence changed.");
    }

    private static void RequireDecisionMatch(ProducerIntentGroup group, Guid checker, string reason)
    {
        if (group.DecidedByUserId != checker || !string.Equals(group.DecisionReason, reason, StringComparison.Ordinal))
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_DECISION_CONFLICT: checker or reason changed.");
    }

    private static void RequireExactFailureRetry(ProducerIntentGroup group, Exception failure)
    {
        var expected = Truncate(failure.Message, 1000);
        var failedMember = failure as ProducerIntentGroupMemberExecutionException;
        var durable = group.Attempts.OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefault(item => item.Status == AccountingEventStatuses.Failed);
        if (durable is null || !string.Equals(group.FailureMessage, expected, StringComparison.Ordinal)
            || !string.Equals(durable.FailureMessage, expected, StringComparison.Ordinal)
            || durable.FailedMemberOrder != failedMember?.MemberOrder
            || durable.FailedAccountingEventId != failedMember?.AccountingEventId)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_FAILURE_CONFLICT: failed retry evidence changed.");
    }

    private IQueryable<ProducerIntentGroup> Query(bool tracking = false)
    {
        var query = _db.ProducerIntentGroups.Include(item => item.Members).ThenInclude(item => item.AccountingEvent)
            .ThenInclude(item => item.Postings).Include(item => item.Members).ThenInclude(item => item.AccountingEvent)
            .ThenInclude(item => item.Attempts).Include(item => item.Receipt).Include(item => item.Attempts).AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private static ProducerIntentGroupDto Map(ProducerIntentGroup group) => new()
    {
        Id = group.Id, RootProducerIntentGroupId = group.RootProducerIntentGroupId, Version = group.Version,
        SupersedesProducerIntentGroupId = group.SupersedesProducerIntentGroupId,
        CorrectsProducerIntentGroupId = group.CorrectsProducerIntentGroupId,
        ReversesProducerIntentGroupId = group.ReversesProducerIntentGroupId,
        GroupKind = group.GroupKind, IdempotencyKey = group.IdempotencyKey, Status = group.Status,
        ParticipantIdentity = group.ParticipantCode, GroupFingerprint = group.GroupFingerprint,
        RequestSnapshotJson = group.RequestSnapshotJson, RequestSnapshotHash = group.RequestSnapshotHash,
        ExpectedOwnerEffect = new() { ParticipantCode = group.ParticipantCode, OwnerEntityType = group.OwnerEntityType,
            OwnerEntityId = group.OwnerEntityId, OwnerAction = group.OwnerAction, EffectFingerprint = group.ExpectedOwnerEffectFingerprint },
        PreparedByUserId = group.PreparedByUserId, PreparedAtUtc = group.PreparedAtUtc,
        DecidedByUserId = group.DecidedByUserId, DecidedAtUtc = group.DecidedAtUtc, DecisionReason = group.DecisionReason,
        CompletedAtUtc = group.CompletedAtUtc, FailureMessage = group.FailureMessage,
        OwnerEffectReceipt = group.Receipt is null ? null : new() { TenantId = group.TenantId,
            ParticipantCode = group.Receipt.ParticipantCode, OwnerEntityType = group.Receipt.OwnerEntityType,
            OwnerEntityId = group.Receipt.OwnerEntityId, OwnerAction = group.Receipt.OwnerAction,
            EffectFingerprint = group.Receipt.EffectFingerprint },
        Members = group.Members.OrderBy(item => item.MemberOrder).Select(item => new ProducerIntentGroupMemberDto
        {
            MemberOrder = item.MemberOrder, MemberFingerprint = item.MemberFingerprint,
            AccountingEvent = AccountingEventService.Map(item.AccountingEvent)
        }).ToList(),
        Attempts = group.Attempts.OrderBy(item => item.AttemptNumber).Select(item => new ProducerIntentGroupAttemptDto
        {
            AttemptNumber = item.AttemptNumber, Status = item.Status, StartedAtUtc = item.StartedAtUtc,
            CompletedAtUtc = item.CompletedAtUtc, FailedMemberOrder = item.FailedMemberOrder,
            FailedAccountingEventId = item.FailedAccountingEventId, FailureMessage = item.FailureMessage
        }).ToList()
    };

    private async Task AuditAsync(string eventType, ProducerIntentGroup group, CancellationToken ct) =>
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            TenantId = group.TenantId, EventType = eventType, SourceModule = "FIN",
            SourceDocumentType = "ProducerIntentGroup", SourceDocumentId = group.Id,
            Resource = "Finance.ProducerIntentGroup", ResourceId = group.Id.ToString(),
            IdempotencyKey = $"C8:{eventType}:{group.Id:N}:{(eventType == FinanceAuditEvents.ProducerIntentGroupFailed ? group.Attempts.Count : 0)}",
            AfterValues = Map(group), Reason = group.DecisionReason ?? group.FailureMessage
        }, ct);

    private async Task AcquireGroupLockAsync(Guid tenantId, string key, CancellationToken ct)
    {
        if (!_db.Database.IsSqlServer()) return;
        var resource = $"FIN:C8:GROUP:{Sha256($"{tenantId:D}|{key}")}";
        await _db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000;
IF @result < 0 THROW 51000, 'PRODUCER_INTENT_GROUP_LOCK_FAILED: group identity could not be serialized.', 1;", ct);
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct) => _db.Database.IsRelational()
        ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;

    private IDbContextTransaction RequireAmbientTransaction()
    {
        var tx = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("PRODUCER_INTENT_GROUP_AMBIENT_TRANSACTION_REQUIRED: execution must join the owner's transaction.");
        if (tx.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_AMBIENT_TRANSACTION_ISOLATION: Serializable isolation is required.");
        return tx;
    }

    private void RequireTransactionIdentity(Guid expected)
    {
        if (_db.Database.CurrentTransaction?.TransactionId != expected)
            throw new InvalidOperationException("PRODUCER_INTENT_GROUP_AMBIENT_TRANSACTION_CHANGED: owner transaction identity changed during execution.");
    }

    private void RequireEnabled()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("FINANCE_PRODUCER_INTENT_GROUP_DISABLED: atomic producer groups are not enabled.");
        if (!_producerOptions.Enabled)
            throw new InvalidOperationException("FINANCE_PRODUCER_INTENT_DISABLED: staged producer intents are not enabled.");
    }

    private Guid RequireActor()
    {
        if (!Guid.TryParse(_currentUser.UserId, out var actor) || actor == Guid.Empty)
            throw new InvalidOperationException("An authenticated Finance actor is required.");
        return actor;
    }

    private string ActorName() => string.IsNullOrWhiteSpace(_currentUser.UserName) ? "Finance.C8" : _currentUser.UserName!;
    private static string CanonicalKind(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "ORIGINAL" => AccountingEventKinds.Original,
        "CORRECTION" => AccountingEventKinds.Correction,
        "REVERSAL" => AccountingEventKinds.Reversal,
        _ => throw new InvalidOperationException("Producer intent group kind must be Original, Correction, or Reversal.")
    };

    private static string CanonicalIdentity(string? value, int max, string label)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > max || !CanonicalIdentityPattern().IsMatch(normalized)
            || IsPseudoIdentity(normalized))
            throw new InvalidOperationException($"A canonical {label} is required.");
        return normalized;
    }

    private static string CanonicalKey(string? value, string label)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 100 || !CanonicalKeyPattern().IsMatch(normalized)
            || IsPseudoIdentity(normalized))
            throw new InvalidOperationException($"A canonical {label} is required.");
        return normalized;
    }

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    private static bool IsPseudoIdentity(string value) => value is "ALL" or "ALL_ACTIVE_BOOKS"
        or "ALL_CLASSIFIED_BOOKS" or "ALLCLASSIFIEDBOOKS";

    [GeneratedRegex("^[A-Z][A-Z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentityPattern();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_.:-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalKeyPattern();

    private sealed record GroupLineage(string Kind, int Version, Guid RootId, Guid? SupersedesId,
        Guid? CorrectsId, Guid? ReversesId, ProducerIntentGroup? Target);
}
