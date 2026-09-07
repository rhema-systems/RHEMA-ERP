using System.Data;
using System.Globalization;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class AccountingEventService : IAccountingEventService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountingBookApplicabilityService _applicability;
    private readonly IAccountingEventPostingLeaf _leaf;
    private readonly IFinanceAuditService _audit;
    private readonly AccountingEventOptions _options;

    public AccountingEventService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAccountingBookApplicabilityService applicability,
        FinancePostingEngine leaf,
        IFinanceAuditService audit,
        IOptions<AccountingEventOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _applicability = applicability;
        _leaf = (IAccountingEventPostingLeaf)leaf;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<AccountingEventDto> CreateAsync(
        CreateAccountingEventDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.Enabled) throw new InvalidOperationException("ACCOUNTING_EVENT_ORCHESTRATION_DISABLED: C6 multi-book release is not enabled.");
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var actor = RequireActor();
        var posting = request.PostingRequest ?? throw new InvalidOperationException("A Finance posting request is required.");
        var preparedIdentity = FinancePreparedIdentityNormalizer.Normalize(
            posting.OriginModuleCode ?? posting.SourceModule, posting.SourceDocumentType, posting.PostingAction);
        var key = CanonicalKey(request.SelectionIdempotencyKey);
        var existing = await FindByKeyAsync(tenantId, key, cancellationToken);
        if (existing is not null)
        {
            RequireRetryMatch(existing, Fingerprint(request, key, existing.Version, existing.RootAccountingEventId));
            RequireProducerMakerMatch(existing, actor);
            return Map(existing);
        }
        var eventId = request.AccountingEventId.GetValueOrDefault(Guid.NewGuid());
        if (eventId == Guid.Empty) eventId = Guid.NewGuid();
        var strategy = _db.Database.CreateExecutionStrategy();
        AccountingEvent? prepared = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await AcquireEventLockAsync(tenantId, key, cancellationToken);
                var raced = await FindByKeyAsync(tenantId, key, cancellationToken);
                if (raced is not null)
                {
                    RequireRetryMatch(raced, Fingerprint(request, key, raced.Version, raced.RootAccountingEventId));
                    RequireProducerMakerMatch(raced, actor);
                    prepared = raced; await tx.CommitAsync(cancellationToken); return;
                }
                var lineage = await ResolveLineageAsync(tenantId, request, cancellationToken);
                var rootId = lineage.RootId == Guid.Empty ? eventId : lineage.RootId;
                var fingerprint = Fingerprint(request, key, lineage.Version, rootId);
                if (lineage.Target is not null
                    && !string.Equals(request.ExpectedSelectionFingerprint, lineage.Target.SelectionFingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("ACCOUNTING_EVENT_LINEAGE_SELECTION_CONFLICT: corrections and reversals must bind the target's frozen selection fingerprint.");
                var now = DateTime.UtcNow;
                prepared = new AccountingEvent
                {
                    Id = eventId, TenantId = tenantId, RootAccountingEventId = rootId,
                    EventKind = lineage.Kind, Version = lineage.Version, SupersedesAccountingEventId = lineage.SupersedesId,
                    CorrectsAccountingEventId = lineage.CorrectsId, ReversesAccountingEventId = lineage.ReversesId,
                    OriginatingModuleCode = preparedIdentity.OriginatingModuleCode,
                    SourceDocumentType = preparedIdentity.SourceDocumentType, SourceDocumentId = posting.SourceDocumentId,
                    PostingAction = preparedIdentity.PostingAction, IdempotencyKey = key,
                    SelectionFingerprint = request.ExpectedSelectionFingerprint, RequestFingerprint = fingerprint,
                    Status = AccountingEventStatuses.PendingApproval, EventDate = posting.PostingDate.Date,
                    RequestedAtUtc = now, RequestedByUserId = actor, PreparedByUserId = actor, PreparedAtUtc = now,
                    ProducerDecisionStatus = string.IsNullOrWhiteSpace(request.ProducerParticipantIdentity)
                        ? ProducerIntentDecisionStatuses.NotRequired : ProducerIntentDecisionStatuses.Pending,
                    ProducerParticipantIdentity = string.IsNullOrWhiteSpace(request.ProducerParticipantIdentity)
                        ? null : request.ProducerParticipantIdentity.Trim().ToUpperInvariant(),
                    CreatedAt = now, CreatedBy = ActorName()
                };
                _db.AccountingEvents.Add(prepared);
                await _db.SaveChangesAsync(cancellationToken);
                await AuditAsync(FinanceAuditEvents.AccountingEventPrepared, prepared, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch { await tx.RollbackAsync(cancellationToken); _db.ChangeTracker.Clear(); throw; }
        });
        return Map(prepared!);
    }

    public async Task<AccountingEventDto> ReleaseAsync(Guid accountingEventId, ReleaseAccountingEventDto release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (string.IsNullOrWhiteSpace(release.Reason)) throw new InvalidOperationException("A governed release reason is required.");
        release.Request.AccountingEventId = accountingEventId;
        return await OrchestrateAsync(release.Request, release.Reason.Trim(), null, cancellationToken);
    }

    public Task<AccountingEventDto> ApproveAsync(Guid accountingEventId, ReleaseAccountingEventDto release, CancellationToken cancellationToken = default)
        => DecideAsync(accountingEventId, release, approve: true, cancellationToken);

    public Task<AccountingEventDto> RejectAsync(Guid accountingEventId, ReleaseAccountingEventDto release, CancellationToken cancellationToken = default)
        => DecideAsync(accountingEventId, release, approve: false, cancellationToken);

    private async Task<AccountingEventDto> DecideAsync(Guid accountingEventId, ReleaseAccountingEventDto release,
        bool approve, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (string.IsNullOrWhiteSpace(release.Reason)) throw new InvalidOperationException("A governed release reason is required.");
        if (!_options.Enabled) throw new InvalidOperationException("ACCOUNTING_EVENT_ORCHESTRATION_DISABLED: C6 multi-book release is not enabled.");
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var checker = RequireActor();
        var normalizedReason = release.Reason.Trim();
        release.Request.AccountingEventId = accountingEventId;
        var strategy = _db.Database.CreateExecutionStrategy();
        AccountingEvent? prepared = null;
        await strategy.ExecuteAsync(async () =>
        {
            // Approval and its audit are one durable maker/checker boundary. SQL Server also takes the
            // same event-key application lock used by prepare and execution so concurrent decisions fail closed.
            await using var tx = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                if (tx is not null)
                    await AcquireEventLockAsync(tenantId, CanonicalKey(release.Request.SelectionIdempotencyKey), cancellationToken);
                prepared = await _db.AccountingEvents.SingleOrDefaultAsync(item => item.TenantId == tenantId
                    && item.Id == accountingEventId && !item.IsDeleted, cancellationToken)
                    ?? throw new KeyNotFoundException("AccountingEvent was not found.");
                RequireRetryMatch(prepared, Fingerprint(release.Request, CanonicalKey(release.Request.SelectionIdempotencyKey),
                    prepared.Version, prepared.RootAccountingEventId));
                var decidedStatus = approve ? ProducerIntentDecisionStatuses.Approved : ProducerIntentDecisionStatuses.Rejected;
                if (prepared.ProducerDecisionStatus == decidedStatus)
                {
                    RequireProducerDecisionMatch(prepared, checker, normalizedReason);
                    if (tx is not null) await tx.CommitAsync(cancellationToken);
                    return;
                }
                if (prepared.Status != AccountingEventStatuses.PendingApproval
                    || prepared.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Pending)
                    throw new InvalidOperationException("ACCOUNTING_EVENT_DECISION_CONFLICT: only pending maker evidence can be approved or rejected.");
                if (prepared.PreparedByUserId == checker)
                    throw new InvalidOperationException("The producer AccountingEvent checker must differ from its preparer.");
                prepared.ProducerDecidedByUserId = checker;
                prepared.ProducerDecidedAtUtc = DateTime.UtcNow;
                prepared.ProducerDecisionReason = normalizedReason;
                prepared.ProducerDecisionStatus = decidedStatus;
                await _db.SaveChangesAsync(cancellationToken);
                await AuditAsync(approve ? FinanceAuditEvents.ProducerAccountingIntentApproved
                    : FinanceAuditEvents.ProducerAccountingIntentRejected, prepared, cancellationToken);
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

    public async Task<AccountingEventDto> ExecuteApprovedAsync(Guid accountingEventId, ReleaseAccountingEventDto release,
        IFinanceProducerExecutionParticipant? participant = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var prepared = await Query().SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == accountingEventId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("AccountingEvent was not found.");
        if (prepared.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Approved
            || prepared.Status is not (AccountingEventStatuses.PendingApproval or AccountingEventStatuses.Failed or AccountingEventStatuses.Posted))
            throw new InvalidOperationException("ACCOUNTING_EVENT_APPROVAL_REQUIRED: execution requires durable independent approval.");
        release.Request.AccountingEventId = accountingEventId;
        RequireRetryMatch(prepared, Fingerprint(release.Request, CanonicalKey(release.Request.SelectionIdempotencyKey),
            prepared.Version, prepared.RootAccountingEventId));
        RequireParticipantMatch(release.Request, participant);
        return await OrchestrateAsync(release.Request, prepared.ProducerDecisionReason!, participant, cancellationToken);
    }

    private async Task<AccountingEventDto> OrchestrateAsync(
        CreateAccountingEventDto request,
        string releaseReason,
        IFinanceProducerExecutionParticipant? participant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireParticipantMatch(request, participant);
        if (!_options.Enabled)
            throw new InvalidOperationException("ACCOUNTING_EVENT_ORCHESTRATION_DISABLED: C6 multi-book release is not enabled.");
        if (!_db.Database.IsRelational())
            throw new InvalidOperationException("ACCOUNTING_EVENT_RELATIONAL_TRANSACTION_REQUIRED: orchestration requires rollback-capable relational transactions.");

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var actorId = RequireActor();
        var eventId = request.AccountingEventId.GetValueOrDefault(Guid.NewGuid());
        if (eventId == Guid.Empty) eventId = Guid.NewGuid();
        var key = CanonicalKey(request.SelectionIdempotencyKey);
        string? requestFingerprint = null;

        var existing = await FindByKeyAsync(tenantId, key, cancellationToken);
        if (existing is not null)
        {
            requestFingerprint = Fingerprint(request, key, existing.Version, existing.RootAccountingEventId);
            RequireRetryMatch(existing, requestFingerprint);
            eventId = existing.Id;
        }

        AccountingEvent? attempted = null;
        DateTime? releaseAttemptStartedAt = null;
        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await AcquireEventLockAsync(tenantId, key, cancellationToken);
                    var raced = await _db.AccountingEvents.Include(item => item.Postings).Include(item => item.Attempts)
                        .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == key && !item.IsDeleted, cancellationToken);
                    if (raced is not null && raced.Status == AccountingEventStatuses.Posted)
                    {
                        requestFingerprint = Fingerprint(request, key, raced.Version, raced.RootAccountingEventId);
                        RequireRetryMatch(raced, requestFingerprint);
                        RequireReleaseMatch(raced, raced.ReleasedByUserId
                            ?? throw new InvalidOperationException("Posted AccountingEvent approval evidence is incomplete."), releaseReason);
                        attempted = raced;
                        await transaction.CommitAsync(cancellationToken);
                        return;
                    }

                    if (raced is null || raced.Id != eventId)
                        throw new InvalidOperationException("ACCOUNTING_EVENT_RELEASE_IDENTITY_CONFLICT: the prepared event no longer matches its canonical release identity.");
                    requestFingerprint = Fingerprint(request, key, raced.Version, raced.RootAccountingEventId);
                    RequireRetryMatch(raced, requestFingerprint);
                    var approvalCheckerId = participant is null ? actorId : raced.ProducerDecidedByUserId
                        ?? throw new InvalidOperationException("ACCOUNTING_EVENT_APPROVAL_REQUIRED: approved checker evidence is missing.");
                    RequireReleaseMatch(raced, approvalCheckerId, releaseReason);
                    RequireParticipantMatch(request, participant);
                    if (participant is not null && raced.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Approved)
                        throw new InvalidOperationException("ACCOUNTING_EVENT_APPROVAL_REQUIRED: producer execution requires durable independent approval.");
                    var lineage = new Lineage(raced.EventKind, raced.Version, raced.RootAccountingEventId,
                        raced.SupersedesAccountingEventId, raced.CorrectsAccountingEventId,
                        raced.ReversesAccountingEventId, null);
                    var posting = request.PostingRequest ?? throw new InvalidOperationException("A Finance posting request is required.");
                    var now = DateTime.UtcNow;
                    releaseAttemptStartedAt = now;
                    attempted = raced;
                    var attempt = new AccountingEventAttempt
                    {
                        TenantId = tenantId, AccountingEventId = eventId,
                        AttemptNumber = attempted.Attempts.Count == 0 ? 1 : attempted.Attempts.Max(item => item.AttemptNumber) + 1,
                        RequestFingerprint = requestFingerprint, Status = AccountingEventStatuses.Pending,
                        StartedAtUtc = now, CreatedAt = now, CreatedBy = ActorName()
                    };
                    // Attempts are immutable outcome evidence. Keep the pending shape untracked so leaf
                    // SaveChanges calls cannot persist a row that would later require an update.

                    var lineageTargetId = lineage.CorrectsId ?? lineage.ReversesId;
                    var lineageTarget = lineageTargetId.HasValue
                        ? lineage.Target ?? await LoadLineageTargetAsync(tenantId, lineageTargetId.Value, cancellationToken)
                        : null;
                    if (lineageTarget is not null
                        && !string.Equals(request.ExpectedSelectionFingerprint, lineageTarget.SelectionFingerprint, StringComparison.Ordinal))
                        throw new InvalidOperationException("ACCOUNTING_EVENT_LINEAGE_SELECTION_CONFLICT: corrections and reversals must bind the target's frozen selection fingerprint.");
                    // A correction posts its compensating payload, while an exact reversal derives opposite
                    // lines. Both retain the target version's frozen ordered book set despite later C5 changes.
                    var selection = lineageTarget is not null
                        ? SelectionFromTarget(lineageTarget)
                        : await _applicability.FreezeAsync(new FreezeAccountingBookSelectionDto
                        {
                            EffectiveDate = posting.PostingDate,
                            OriginatingModuleCode = posting.OriginModuleCode ?? posting.SourceModule,
                            SourceDocumentType = posting.SourceDocumentType,
                            PostingAction = posting.PostingAction,
                            IdempotencyKey = key,
                            ExpectedCalculationInputHash = request.ExpectedCalculationInputHash,
                            ExpectedSelectionFingerprint = request.ExpectedSelectionFingerprint
                        }, cancellationToken);
                    var evidenceId = selection.SelectionEvidenceId
                        ?? throw new InvalidOperationException("Frozen accounting-book selection evidence did not return its stable identity.");
                    if (selection.Books.Count == 0)
                        throw new InvalidOperationException("ACCOUNTING_EVENT_EMPTY_SELECTION: frozen evidence selected no accounting book.");

                    // C5 persists frozen evidence and its audit with SaveChanges. Keep the tracked event in
                    // its valid PendingApproval/Failed database shape until FreezeAsync has completed.
                    attempted.Status = AccountingEventStatuses.Pending;
                    attempted.CompletedAtUtc = null;
                    attempted.FailureMessage = null;
                    var releaseActorId = participant is null ? actorId : attempted.ProducerDecidedByUserId
                        ?? throw new InvalidOperationException("Approved producer checker evidence is incomplete.");
                    attempted.ReleasedByUserId ??= releaseActorId;
                    attempted.ReleasedAtUtc ??= now;
                    attempted.ReleaseReason ??= releaseReason;
                    attempted.AccountingBookSelectionEvidenceId = evidenceId;
                    attempted.SelectionFingerprint = selection.SelectionFingerprint;
                    attempted.Postings = selection.Books.OrderBy(item => item.SelectionOrder).Select(item => new AccountingEventPosting
                        {
                            TenantId = tenantId,
                            EventVersion = attempted.Version,
                            AccountingBookId = item.AccountingBookId,
                            SelectionOrder = item.SelectionOrder,
                            AccountingBookCodeSnapshot = item.AccountingBookCode,
                            AuthorityFingerprint = item.AuthorityFingerprint,
                            Status = AccountingEventStatuses.Pending,
                            CreatedAt = now,
                            CreatedBy = ActorName()
                        }).ToList();
                    // The internal leaf verifies database-backed event/evidence/book authority. Persist this
                    // valid Pending aggregate shape before invoking the first representation.
                    await _db.SaveChangesAsync(cancellationToken);

                    // The owner participant runs under this same serializable database transaction. Any owner
                    // or Finance failure therefore rolls every economic write back before failure evidence is appended.
                    if (participant is not null)
                        await participant.ExecuteAsync(cancellationToken);

                    var selectedBookIds = attempted.Postings.Select(item => item.AccountingBookId).ToList();
                    var orderedSelectedBookIds = attempted.Postings.OrderBy(item => item.SelectionOrder)
                        .Select(item => item.AccountingBookId).ToImmutableArray();
                    var reservationConsumerBookId = await _db.AccountingBooks.AsNoTracking()
                        .Where(item => item.TenantId == tenantId && selectedBookIds.Contains(item.Id)
                            && item.BookType == AccountingBookType.PrimaryFull && !item.IsDeleted)
                        .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken)
                        ?? attempted.Postings.OrderBy(item => item.SelectionOrder).First().AccountingBookId;
                    foreach (var representation in attempted.Postings.OrderBy(item => item.SelectionOrder))
                    {
                        var representationKey = $"{key}:{representation.AccountingBookId:N}";
                        var result = lineage.Kind == AccountingEventKinds.Reversal
                            ? await _leaf.ReverseAsync(
                                lineageTarget!.Postings.Single(item => item.AccountingBookId == representation.AccountingBookId).FinancePostingEventId
                                    ?? throw new InvalidOperationException("Original exact-book posting evidence is incomplete."),
                                posting.PostingDate, posting.ReversalReason ?? "Exact AccountingEvent reversal", representationKey,
                                new AccountingEventPostingAuthority(eventId, evidenceId, representation.AccountingBookId,
                                    representation.AuthorityFingerprint, orderedSelectedBookIds), cancellationToken)
                            : await _leaf.PostAsync(CopyForBook(posting, representation.AccountingBookCodeSnapshot, representationKey,
                                    includeBudgetReservations: representation.AccountingBookId == reservationConsumerBookId,
                                    isCorrection: lineage.Kind == AccountingEventKinds.Correction, attempted),
                                new AccountingEventPostingAuthority(eventId, evidenceId, representation.AccountingBookId,
                                    representation.AuthorityFingerprint, orderedSelectedBookIds), cancellationToken);
                        representation.FinancePostingEventId = result.PostingEventId;
                        representation.JournalEntryId = result.JournalEntryId;
                        representation.PostedAtUtc = DateTime.UtcNow;
                        representation.Status = AccountingEventStatuses.Posted;
                        // The next sibling's duplicate scan must be able to prove that every earlier cross-book
                        // match is linked to this exact event/evidence/book context inside the outer transaction.
                        await _db.SaveChangesAsync(cancellationToken);
                    }

                    // SQL authority validates that every child is Posted before the group can be released.
                    // Flush children first, still inside the same outer transaction, to avoid provider ordering ambiguity.
                    await _db.SaveChangesAsync(cancellationToken);
                    attempted.Status = AccountingEventStatuses.Posted;
                    attempted.CompletedAtUtc = DateTime.UtcNow;
                    attempt.Status = AccountingEventStatuses.Posted;
                    attempt.CompletedAtUtc = attempted.CompletedAtUtc;
                    _db.AccountingEventAttempts.Add(attempt);
                    await _db.SaveChangesAsync(cancellationToken);
                    await AuditAsync(FinanceAuditEvents.AccountingEventPosted, attempted, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            var approvalCheckerId = attempted?.ReleasedByUserId
                ?? attempted?.ProducerDecidedByUserId
                ?? actorId;
            await PersistFailureAsync(tenantId, approvalCheckerId, eventId, key,
                requestFingerprint ?? throw new InvalidOperationException("AccountingEvent release fingerprint was not resolved."), attempted,
                releaseReason, releaseAttemptStartedAt ?? DateTime.UtcNow, ex, cancellationToken);
            throw;
        }

        return attempted is not null
            ? Map(attempted)
            : throw new InvalidOperationException("AccountingEvent orchestration completed without durable evidence.");
    }

    public async Task<AccountingEventDto> GetAsync(Guid accountingEventId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var item = await Query().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == accountingEventId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("AccountingEvent was not found.");
        return Map(item);
    }

    public async Task<AccountingEventPostingDto> GetBookAsync(Guid accountingEventId, Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        var group = await GetAsync(accountingEventId, cancellationToken);
        return group.Postings.SingleOrDefault(item => item.AccountingBookId == accountingBookId)
            ?? throw new KeyNotFoundException("The AccountingEvent has no representation for that exact accounting book.");
    }

    private IQueryable<AccountingEvent> Query() => _db.AccountingEvents.AsNoTracking()
        .Include(item => item.Postings).Include(item => item.Attempts);

    private async Task<AccountingEvent?> FindByKeyAsync(Guid tenantId, string key, CancellationToken ct) =>
        await Query().SingleOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == key && !item.IsDeleted, ct);

    private async Task<Lineage> ResolveLineageAsync(Guid tenantId, CreateAccountingEventDto request, CancellationToken ct)
    {
        var kind = request.EventKind?.Trim() ?? string.Empty;
        if (kind.Equals(AccountingEventKinds.Original, StringComparison.OrdinalIgnoreCase))
        {
            if (request.SupersedesAccountingEventId.HasValue || request.CorrectsAccountingEventId.HasValue || request.ReversesAccountingEventId.HasValue)
                throw new InvalidOperationException("Original AccountingEvents cannot carry correction or reversal lineage.");
            return new(AccountingEventKinds.Original, 1, Guid.Empty, null, null, null, null);
        }

        var targetId = kind.Equals(AccountingEventKinds.Correction, StringComparison.OrdinalIgnoreCase)
            ? request.CorrectsAccountingEventId
            : kind.Equals(AccountingEventKinds.Reversal, StringComparison.OrdinalIgnoreCase)
                ? request.ReversesAccountingEventId
                : throw new InvalidOperationException("AccountingEvent kind must be Original, Correction, or Reversal.");
        if (!targetId.HasValue || targetId == Guid.Empty)
            throw new InvalidOperationException($"{kind} AccountingEvents require exact target lineage.");
        if (request.SupersedesAccountingEventId.HasValue && request.SupersedesAccountingEventId != targetId)
            throw new InvalidOperationException("The canonical predecessor must be the exact correction or reversal target.");

        var target = await LoadLineageTargetAsync(tenantId, targetId.Value, ct);
        if (target.Status != AccountingEventStatuses.Posted)
            throw new InvalidOperationException("Only a posted AccountingEvent can be corrected or reversed.");
        RequireCanonicalTargetIdentity(target, request.PostingRequest
            ?? throw new InvalidOperationException("A Finance posting request is required."));
        var hasSuccessor = await _db.AccountingEvents.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.SupersedesAccountingEventId == target.Id && !item.IsDeleted, ct);
        if (hasSuccessor)
            throw new InvalidOperationException("ACCOUNTING_EVENT_LINEAGE_CONFLICT: the target already has a canonical successor.");
        return new(kind.Equals(AccountingEventKinds.Correction, StringComparison.OrdinalIgnoreCase)
                ? AccountingEventKinds.Correction : AccountingEventKinds.Reversal,
            checked(target.Version + 1), target.RootAccountingEventId, target.Id,
            kind.Equals(AccountingEventKinds.Correction, StringComparison.OrdinalIgnoreCase) ? target.Id : null,
            kind.Equals(AccountingEventKinds.Reversal, StringComparison.OrdinalIgnoreCase) ? target.Id : null, target);
    }

    private async Task<AccountingEvent> LoadLineageTargetAsync(Guid tenantId, Guid targetId, CancellationToken ct) =>
        await _db.AccountingEvents.AsNoTracking().Include(item => item.Postings).SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == targetId && !item.IsDeleted, ct)
        ?? throw new InvalidOperationException("AccountingEvent lineage target was not found for this tenant.");

    private static AccountingBookSelectionDto SelectionFromTarget(AccountingEvent target)
    {
        if (!target.AccountingBookSelectionEvidenceId.HasValue || target.Postings.Count == 0)
            throw new InvalidOperationException("Exact reversal requires the original frozen book-selection evidence.");
        // Reversal never asks today's policy which books apply; it reuses the original immutable ordered set.
        return new AccountingBookSelectionDto
        {
            SelectionEvidenceId = target.AccountingBookSelectionEvidenceId,
            EffectiveDate = target.EventDate, OriginatingModuleCode = target.OriginatingModuleCode,
            SourceDocumentType = target.SourceDocumentType, PostingAction = target.PostingAction,
            SelectionFingerprint = target.SelectionFingerprint,
            Books = target.Postings.OrderBy(item => item.SelectionOrder).Select(item => new AccountingBookSelectionBookDto
            {
                AccountingBookId = item.AccountingBookId, AccountingBookCode = item.AccountingBookCodeSnapshot,
                SelectionOrder = item.SelectionOrder, AuthorityFingerprint = item.AuthorityFingerprint
            }).ToList()
        };
    }

    private async Task PersistFailureAsync(Guid tenantId, Guid actorId, Guid eventId, string key,
        string fingerprint, AccountingEvent? attempted, string releaseReason,
        DateTime attemptStartedAt,
        Exception exception, CancellationToken ct)
    {
        // Failure evidence is written only after the economic transaction rolled back. Reusing the
        // preallocated event ID makes logs and a later inquiry correlate to the failed attempt.
        if (attempted is null) return; // structurally invalid commands do not become canonical accounting evidence.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await AcquireEventLockAsync(tenantId, key, ct);
                var failure = await _db.AccountingEvents.Include(item => item.Attempts).SingleOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.IdempotencyKey == key && !item.IsDeleted, ct);
                if (failure is not null && failure.Status == AccountingEventStatuses.Posted)
                {
                    RequireRetryMatch(failure, fingerprint);
                    RequireReleaseMatch(failure, actorId, releaseReason);
                    await tx.CommitAsync(ct);
                    return;
                }
                if (failure is null)
                {
                    failure = attempted;
                    failure.Postings = [];
                    failure.Attempts = [];
                    failure.AccountingBookSelectionEvidenceId = null;
                    _db.AccountingEvents.Add(failure);
                }
                RequireRetryMatch(failure, fingerprint);
                var now = DateTime.UtcNow;
                RequireReleaseMatch(failure, actorId, releaseReason);
                failure.ReleasedByUserId ??= actorId;
                failure.ReleasedAtUtc ??= attemptStartedAt;
                failure.ReleaseReason ??= releaseReason;
                // The first failure fixes the event-level terminal summary. Later retries append attempts
                // without rewriting the prior outcome evidence after their economic transaction rolls back.
                failure.Status = AccountingEventStatuses.Failed;
                failure.CompletedAtUtc ??= now;
                failure.FailureMessage ??= Truncate(exception.Message, 1000);
                failure.Attempts.Add(new AccountingEventAttempt
                {
                    TenantId = tenantId, AccountingEventId = failure.Id,
                    AttemptNumber = failure.Attempts.Count == 0 ? 1 : failure.Attempts.Max(item => item.AttemptNumber) + 1,
                    RequestFingerprint = fingerprint, Status = AccountingEventStatuses.Failed,
                    StartedAtUtc = attemptStartedAt, CompletedAtUtc = now,
                    FailureMessage = Truncate(exception.Message, 1000), CreatedAt = now, CreatedBy = ActorName()
                });
                await _db.SaveChangesAsync(ct);
                await AuditAsync(FinanceAuditEvents.AccountingEventFailed, failure, ct);
                await tx.CommitAsync(ct);
            }
            catch { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); throw; }
        });
    }

    private async Task AcquireEventLockAsync(Guid tenantId, string key, CancellationToken ct)
    {
        if (!_db.Database.IsSqlServer()) return;
        var resource = $"FIN:C6:EVENT:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:D}|{key}")))}";
        await _db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000;
IF @result < 0 THROW 51000, 'ACCOUNTING_EVENT_LOCK_FAILED: event identity could not be serialized.', 1;", ct);
    }

    private async Task AuditAsync(string eventType, AccountingEvent item, CancellationToken ct) =>
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            TenantId = item.TenantId, EventType = eventType, SourceModule = item.OriginatingModuleCode,
            SourceDocumentType = item.SourceDocumentType, SourceDocumentId = item.SourceDocumentId,
            Resource = "Finance.AccountingEvent", ResourceId = item.Id.ToString(),
            IdempotencyKey = $"C6:{eventType}:{item.Id:N}:{(eventType == FinanceAuditEvents.AccountingEventFailed ? item.Attempts.Count : 0)}",
            AfterValues = Map(item), Reason = item.FailureMessage
        }, ct);

    private static FinancePostingRequestV2Dto CopyForBook(FinancePostingRequestV2Dto source, string bookCode, string key,
        bool includeBudgetReservations, bool isCorrection, AccountingEvent accountingEvent) => new()
    {
        SourceModule = isCorrection ? "GL" : source.SourceModule.Trim().ToUpperInvariant(),
        OriginModuleCode = isCorrection ? FinanceModuleLockCatalog.Finance : accountingEvent.OriginatingModuleCode,
        SourceDocumentType = isCorrection ? "AccountingEventCorrection" : accountingEvent.SourceDocumentType,
        SourceDocumentId = isCorrection ? accountingEvent.Id : accountingEvent.SourceDocumentId,
        SourceDocumentTenantId = source.SourceDocumentTenantId, ExistingJournalEntryId = source.ExistingJournalEntryId,
        ReversalOfJournalEntryId = source.ReversalOfJournalEntryId, ReversalReason = source.ReversalReason,
        ReversalType = source.ReversalType, PostingAction = accountingEvent.PostingAction,
        SourceDocumentReference = source.SourceDocumentReference, Description = source.Description,
        PostingDate = source.PostingDate, FiscalPeriodId = source.FiscalPeriodId, JournalType = source.JournalType,
        FunctionalCurrencyCode = source.FunctionalCurrencyCode, IdempotencyKey = key, ReturnExistingOnDuplicate = source.ReturnExistingOnDuplicate,
        ExchangeRateTypeOverride = source.ExchangeRateTypeOverride, ExchangeRateQuoteSideOverride = source.ExchangeRateQuoteSideOverride,
        ExchangeRateOverrideReason = source.ExchangeRateOverrideReason, ExchangeRateOverrideApprovedByUserId = source.ExchangeRateOverrideApprovedByUserId,
        ExchangeRateOverrideApprovedAt = source.ExchangeRateOverrideApprovedAt,
        PreserveHistoricalExchangeRateSnapshot = source.PreserveHistoricalExchangeRateSnapshot,
        AllowPostingToClosedPeriod = source.AllowPostingToClosedPeriod,
        // Reservations represent the economic source, not each accounting representation. The
        // first frozen representation consumes them inside the shared transaction; siblings do not double-consume.
        BudgetReservationIds = includeBudgetReservations ? source.BudgetReservationIds : [],
        BudgetReservationSourceDocumentType = includeBudgetReservations ? source.BudgetReservationSourceDocumentType : null,
        Lines = source.Lines, TaxCalculationSnapshots = source.TaxCalculationSnapshots, AccountingBookCode = bookCode
    };

    private static void RequireCanonicalTargetIdentity(AccountingEvent target, FinancePostingRequestV2Dto posting)
    {
        static string N(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (N(posting.OriginModuleCode ?? posting.SourceModule) != target.OriginatingModuleCode
            || N(posting.SourceDocumentType) != target.SourceDocumentType
            || posting.SourceDocumentId != target.SourceDocumentId
            || N(posting.PostingAction) != target.PostingAction)
            throw new InvalidOperationException("ACCOUNTING_EVENT_LINEAGE_IDENTITY_CONFLICT: successors must retain the target's canonical economic source identity.");
    }

    private static string Fingerprint(CreateAccountingEventDto request, string key, int eventVersion, Guid rootAccountingEventId)
    {
        var posting = request.PostingRequest ?? throw new InvalidOperationException("A Finance posting request is required.");
        static string? S(string? value) => value?.Trim();
        static string? U(string? value) => value?.Trim().ToUpperInvariant();
        static string? D(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);
        static string? T(DateTime? value) => CanonicalDateTime(value);
        static string? G(Guid? value) => value?.ToString("D");
        var canonical = new CanonicalFingerprintWriter("RHEMA-FINANCE-ACCOUNTING-EVENT-V2");
        void Add(string name, object? value) => canonical.Add(name, value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        });
        Add("event.key", key); Add("event.kind", U(request.EventKind));
        Add("event.version", eventVersion); Add("event.rootId", rootAccountingEventId.ToString("D"));
        Add("event.supersedesId", G(request.SupersedesAccountingEventId));
        Add("event.correctsId", G(request.CorrectsAccountingEventId));
        Add("event.reversesId", G(request.ReversesAccountingEventId));
        Add("selection.calculationInputHash", U(request.ExpectedCalculationInputHash));
        Add("selection.fingerprint", U(request.ExpectedSelectionFingerprint));
        Add("producer.participantIdentity", U(request.ProducerParticipantIdentity));
        Add("posting.sourceModule", U(posting.SourceModule));
        Add("posting.originModule", U(posting.OriginModuleCode));
        Add("posting.documentType", U(posting.SourceDocumentType));
        Add("posting.documentId", posting.SourceDocumentId.ToString("D"));
        Add("posting.documentTenantId", G(posting.SourceDocumentTenantId));
        Add("posting.existingJournalId", G(posting.ExistingJournalEntryId));
        Add("posting.reversalJournalId", G(posting.ReversalOfJournalEntryId));
        Add("posting.reversalReason", S(posting.ReversalReason));
        Add("posting.reversalType", U(posting.ReversalType));
        Add("posting.action", U(posting.PostingAction));
        Add("posting.reference", S(posting.SourceDocumentReference));
        Add("posting.description", S(posting.Description)); Add("posting.date", T(posting.PostingDate));
        Add("posting.fiscalPeriodId", G(posting.FiscalPeriodId));
        Add("posting.journalType", U(posting.JournalType));
        Add("posting.functionalCurrency", U(posting.FunctionalCurrencyCode));
        Add("posting.returnExisting", posting.ReturnExistingOnDuplicate);
        Add("posting.rateTypeOverride", U(posting.ExchangeRateTypeOverride));
        Add("posting.rateQuoteSideOverride", U(posting.ExchangeRateQuoteSideOverride));
        Add("posting.rateOverrideReason", S(posting.ExchangeRateOverrideReason));
        Add("posting.rateOverrideApprover", G(posting.ExchangeRateOverrideApprovedByUserId));
        Add("posting.rateOverrideApprovedAt", T(posting.ExchangeRateOverrideApprovedAt));
        Add("posting.preserveHistoricalRate", posting.PreserveHistoricalExchangeRateSnapshot);
        Add("posting.allowClosedPeriod", posting.AllowPostingToClosedPeriod);
        Add("budget.sourceDocumentType", U(posting.BudgetReservationSourceDocumentType));
        var reservations = posting.BudgetReservationIds.OrderBy(item => item).ToList();
        Add("budget.count", reservations.Count);
        for (var index = 0; index < reservations.Count; index++)
            Add($"budget[{index}].id", reservations[index].ToString("D"));
        // Collection position is evidence: equal-valued lines in a different order are not silently conflated.
        Add("lines.count", posting.Lines.Count);
        foreach (var pair in posting.Lines.Select((line, index) => (line, index)))
        {
            var line = pair.line;
            var prefix = $"lines[{pair.index}]";
            Add($"{prefix}.lineNumber", line.LineNumber); Add($"{prefix}.accountId", line.AccountId.ToString("D"));
            Add($"{prefix}.sourceLineId", G(line.SourceDocumentLineId)); Add($"{prefix}.description", S(line.Description));
            Add($"{prefix}.debit", D(line.DebitAmount)); Add($"{prefix}.credit", D(line.CreditAmount));
            Add($"{prefix}.transactionCurrency", U(line.TransactionCurrency));
            Add($"{prefix}.transactionDebit", D(line.TransactionDebitAmount)); Add($"{prefix}.transactionCredit", D(line.TransactionCreditAmount));
            Add($"{prefix}.foreignAmount", D(line.ForeignCurrencyAmount)); Add($"{prefix}.rateId", G(line.ExchangeRateId));
            Add($"{prefix}.rate", D(line.ExchangeRate)); Add($"{prefix}.rateSource", S(line.ExchangeRateSource));
            Add($"{prefix}.rateDate", T(line.ExchangeRateDate)); Add($"{prefix}.sourceReference", S(line.SourceReferenceNumber));
            Add($"{prefix}.dimensionSetId", G(line.FinanceDimensionSetId)); Add($"{prefix}.segment", S(line.SegmentString));
            Add($"{prefix}.notes", S(line.Notes)); Add($"{prefix}.tag", S(line.TransactionTag));
            var dimensions = line.Dimensions.OrderBy(item => U(item.DimensionCode), StringComparer.Ordinal)
                         .ThenBy(item => U(item.ValueCode), StringComparer.Ordinal).ThenBy(item => U(item.SourceEntityType), StringComparer.Ordinal)
                         .ThenBy(item => item.SourceEntityId).ToList();
            Add($"{prefix}.dimensions.count", dimensions.Count);
            for (var dimensionIndex = 0; dimensionIndex < dimensions.Count; dimensionIndex++)
            {
                var dimension = dimensions[dimensionIndex]; var dimensionPrefix = $"{prefix}.dimensions[{dimensionIndex}]";
                Add($"{dimensionPrefix}.code", U(dimension.DimensionCode));
                Add($"{dimensionPrefix}.value", U(dimension.ValueCode));
                Add($"{dimensionPrefix}.sourceType", U(dimension.SourceEntityType));
                Add($"{dimensionPrefix}.sourceId", G(dimension.SourceEntityId));
            }
        }
        // Tax DTO list order is transport-incidental; its governed CalculationOrder and stable identities
        // define canonical order. Lines remain request-ordered because journal line position is economic evidence.
        var taxes = posting.TaxCalculationSnapshots.OrderBy(item => item.CalculationOrder)
            .ThenBy(item => U(item.DocumentType), StringComparer.Ordinal).ThenBy(item => item.DocumentId)
            .ThenBy(item => item.DocumentLineId).ThenBy(item => item.TaxId).ThenBy(item => item.TaxGroupId)
            .ThenBy(item => item.PostingAccountId).ThenBy(item => item.BaseAmount).ThenBy(item => item.TaxableAmount)
            .ThenBy(item => item.TaxRate).ThenBy(item => item.TaxAmount).ThenBy(item => item.CompoundBasis)
            .ThenBy(item => CanonicalDateTime(item.CalculationDate), StringComparer.Ordinal).ThenBy(item => item.IsManualOverride)
            .ThenBy(item => S(item.OverrideReason), StringComparer.Ordinal).ToList();
        Add("taxes.count", taxes.Count);
        for (var index = 0; index < taxes.Count; index++)
        {
            var tax = taxes[index]; var prefix = $"taxes[{index}]";
            Add($"{prefix}.documentType", U(tax.DocumentType)); Add($"{prefix}.documentId", tax.DocumentId.ToString("D"));
            Add($"{prefix}.documentLineId", G(tax.DocumentLineId)); Add($"{prefix}.taxId", tax.TaxId.ToString("D"));
            Add($"{prefix}.taxGroupId", G(tax.TaxGroupId)); Add($"{prefix}.postingAccountId", G(tax.PostingAccountId));
            Add($"{prefix}.baseAmount", D(tax.BaseAmount)); Add($"{prefix}.taxableAmount", D(tax.TaxableAmount));
            Add($"{prefix}.rate", D(tax.TaxRate)); Add($"{prefix}.amount", D(tax.TaxAmount));
            Add($"{prefix}.compoundBasis", Convert.ToInt32(tax.CompoundBasis, CultureInfo.InvariantCulture));
            Add($"{prefix}.calculationOrder", tax.CalculationOrder); Add($"{prefix}.calculationDate", T(tax.CalculationDate));
            Add($"{prefix}.manualOverride", tax.IsManualOverride); Add($"{prefix}.overrideReason", S(tax.OverrideReason));
        }
        return canonical.Hash();
    }

    private static string? CanonicalDateTime(DateTime? value)
    {
        if (!value.HasValue) return null;
        var utc = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
        // Unspecified Finance inputs are deliberately UTC wall-clock values; Local inputs represent
        // local instants and normalize to their equivalent UTC instant before hashing and ordering.
        return utc.ToString("O", CultureInfo.InvariantCulture);
    }

    private sealed class CanonicalFingerprintWriter(string version)
    {
        private readonly StringBuilder _text = new();

        public void Add(string name, string? value)
        {
            // Names and UTF-8 byte lengths make adjacent values and embedded delimiters unambiguous.
            _text.Append(Encoding.UTF8.GetByteCount(name)).Append(':').Append(name).Append('=');
            if (value is null) _text.Append("-1:;");
            else _text.Append(Encoding.UTF8.GetByteCount(value)).Append(':').Append(value).Append(';');
        }

        public string Hash()
        {
            var payload = $"{Encoding.UTF8.GetByteCount(version)}:{version};{_text}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }
    }

    private static string CanonicalKey(string value)
    {
        var key = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (key.Length is 0 or > 100) throw new InvalidOperationException("AccountingEvent idempotency key must contain 1 to 100 characters.");
        return key;
    }

    private static void RequireRetryMatch(AccountingEvent item, string fingerprint)
    {
        if (!string.Equals(item.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_EVENT_IDEMPOTENCY_CONFLICT: the key already identifies different immutable evidence.");
    }

    private static void RequireProducerMakerMatch(AccountingEvent item, Guid maker)
    {
        if (item.ProducerDecisionStatus != ProducerIntentDecisionStatuses.NotRequired
            && item.PreparedByUserId != maker)
            throw new InvalidOperationException("ACCOUNTING_EVENT_MAKER_CONFLICT: a producer-intent retry must retain the original maker.");
    }

    private static void RequireReleaseMatch(AccountingEvent item, Guid checker, string normalizedReason)
    {
        if (item.PreparedByUserId == checker)
            throw new InvalidOperationException("The AccountingEvent checker must differ from its preparer.");
        if (item.ReleasedByUserId.HasValue && item.ReleasedByUserId != checker)
            throw new InvalidOperationException("ACCOUNTING_EVENT_RELEASE_CHECKER_CONFLICT: a retry must retain the original checker.");
        if (item.ReleaseReason is not null && !string.Equals(item.ReleaseReason, normalizedReason, StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_EVENT_RELEASE_REASON_CONFLICT: a retry must retain the original normalized reason.");
    }

    private static void RequireParticipantMatch(CreateAccountingEventDto request, IFinanceProducerExecutionParticipant? participant)
    {
        var expected = request.ProducerParticipantIdentity?.Trim().ToUpperInvariant() ?? string.Empty;
        var actual = participant?.ParticipantIdentity?.Trim().ToUpperInvariant() ?? string.Empty;
        if (expected.Length == 0 && participant is null) return; // Existing Finance-only C6 callers have no owner participant.
        if (expected.Length == 0 || participant is null || !string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_EVENT_PARTICIPANT_CONFLICT: execution must retain the prepared owner participant identity.");
    }

    private static void RequireProducerDecisionMatch(AccountingEvent item, Guid checker, string normalizedReason)
    {
        if (item.ProducerDecidedByUserId != checker)
            throw new InvalidOperationException("ACCOUNTING_EVENT_DECISION_CHECKER_CONFLICT: a retry must retain the original checker.");
        if (!string.Equals(item.ProducerDecisionReason, normalizedReason, StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_EVENT_DECISION_REASON_CONFLICT: a retry must retain the original normalized reason.");
    }

    private Guid RequireActor() => Guid.TryParse(_currentUser.UserId, out var actor) && actor != Guid.Empty
        ? actor : throw new UnauthorizedAccessException("An authenticated Finance user is required.");
    private string ActorName() => _currentUser.UserName ?? "system";
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static AccountingEventDto Map(AccountingEvent item) => new()
    {
        Id = item.Id, RootAccountingEventId = item.RootAccountingEventId, Version = item.Version,
        EventKind = item.EventKind, OriginatingModuleCode = item.OriginatingModuleCode,
        SourceDocumentType = item.SourceDocumentType, SourceDocumentId = item.SourceDocumentId,
        PostingAction = item.PostingAction, IdempotencyKey = item.IdempotencyKey,
        SupersedesAccountingEventId = item.SupersedesAccountingEventId,
        CorrectsAccountingEventId = item.CorrectsAccountingEventId, ReversesAccountingEventId = item.ReversesAccountingEventId,
        AccountingBookSelectionEvidenceId = item.AccountingBookSelectionEvidenceId,
        SelectionFingerprint = item.SelectionFingerprint, RequestFingerprint = item.RequestFingerprint,
        Status = item.Status, EventDate = item.EventDate, RequestedAtUtc = item.RequestedAtUtc,
        CompletedAtUtc = item.CompletedAtUtc, FailureMessage = item.FailureMessage,
        PreparedByUserId = item.PreparedByUserId, PreparedAtUtc = item.PreparedAtUtc,
        ReleasedByUserId = item.ReleasedByUserId, ReleasedAtUtc = item.ReleasedAtUtc,
        ReleaseReason = item.ReleaseReason,
        ProducerDecisionStatus = item.ProducerDecisionStatus,
        ProducerParticipantIdentity = item.ProducerParticipantIdentity,
        ProducerDecidedByUserId = item.ProducerDecidedByUserId,
        ProducerDecidedAtUtc = item.ProducerDecidedAtUtc,
        ProducerDecisionReason = item.ProducerDecisionReason,
        Postings = item.Postings.OrderBy(x => x.SelectionOrder).Select(x => new AccountingEventPostingDto
        {
            EventVersion = x.EventVersion, AccountingBookId = x.AccountingBookId, AccountingBookCode = x.AccountingBookCodeSnapshot,
            SelectionOrder = x.SelectionOrder, AuthorityFingerprint = x.AuthorityFingerprint,
            Status = x.Status, FinancePostingEventId = x.FinancePostingEventId, JournalEntryId = x.JournalEntryId,
            PostedAtUtc = x.PostedAtUtc, FailureMessage = x.FailureMessage
        }).ToList(),
        Attempts = item.Attempts.OrderBy(x => x.AttemptNumber).Select(x => new AccountingEventAttemptDto
        {
            Id = x.Id, AttemptNumber = x.AttemptNumber, Status = x.Status,
            StartedAtUtc = x.StartedAtUtc, CompletedAtUtc = x.CompletedAtUtc, FailureMessage = x.FailureMessage
        }).ToList()
    };

    private sealed record Lineage(string Kind, int Version, Guid RootId, Guid? SupersedesId, Guid? CorrectsId, Guid? ReversesId, AccountingEvent? Target);
}
