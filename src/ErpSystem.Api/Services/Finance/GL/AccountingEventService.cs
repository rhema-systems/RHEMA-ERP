using System.Data;
using System.Globalization;
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
        var key = CanonicalKey(request.SelectionIdempotencyKey);
        var fingerprint = Fingerprint(request, key);
        var existing = await FindByKeyAsync(tenantId, key, cancellationToken);
        if (existing is not null) { RequireRetryMatch(existing, fingerprint); return Map(existing); }
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
                if (raced is not null) { RequireRetryMatch(raced, fingerprint); prepared = raced; await tx.CommitAsync(cancellationToken); return; }
                var lineage = await ResolveLineageAsync(tenantId, request, cancellationToken);
                if (lineage.Target is not null
                    && !string.Equals(request.ExpectedSelectionFingerprint, lineage.Target.SelectionFingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("ACCOUNTING_EVENT_LINEAGE_SELECTION_CONFLICT: corrections and reversals must bind the target's frozen selection fingerprint.");
                var posting = request.PostingRequest;
                var now = DateTime.UtcNow;
                prepared = new AccountingEvent
                {
                    Id = eventId, TenantId = tenantId, RootAccountingEventId = lineage.RootId == Guid.Empty ? eventId : lineage.RootId,
                    EventKind = lineage.Kind, Version = lineage.Version, SupersedesAccountingEventId = lineage.SupersedesId,
                    CorrectsAccountingEventId = lineage.CorrectsId, ReversesAccountingEventId = lineage.ReversesId,
                    OriginatingModuleCode = (posting.OriginModuleCode ?? posting.SourceModule).Trim().ToUpperInvariant(),
                    SourceDocumentType = posting.SourceDocumentType.Trim().ToUpperInvariant(), SourceDocumentId = posting.SourceDocumentId,
                    PostingAction = posting.PostingAction.Trim().ToUpperInvariant(), IdempotencyKey = key,
                    SelectionFingerprint = request.ExpectedSelectionFingerprint, RequestFingerprint = fingerprint,
                    Status = AccountingEventStatuses.PendingApproval, EventDate = posting.PostingDate.Date,
                    RequestedAtUtc = now, RequestedByUserId = actor, PreparedByUserId = actor, PreparedAtUtc = now,
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
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var prepared = await Query().SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == accountingEventId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("AccountingEvent was not found.");
        var checker = RequireActor();
        if (prepared.PreparedByUserId == checker) throw new InvalidOperationException("The AccountingEvent checker must differ from its preparer.");
        var normalizedReason = release.Reason.Trim();
        if (prepared.ReleasedByUserId.HasValue && prepared.ReleasedByUserId != checker)
            throw new InvalidOperationException("ACCOUNTING_EVENT_RELEASE_CHECKER_CONFLICT: a retry must retain the original checker.");
        if (prepared.ReleaseReason is not null && !string.Equals(prepared.ReleaseReason, normalizedReason, StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_EVENT_RELEASE_REASON_CONFLICT: a retry must retain the original normalized reason.");
        release.Request.AccountingEventId = accountingEventId;
        RequireRetryMatch(prepared, Fingerprint(release.Request, CanonicalKey(release.Request.SelectionIdempotencyKey)));
        return await OrchestrateAsync(release.Request, normalizedReason, cancellationToken);
    }

    private async Task<AccountingEventDto> OrchestrateAsync(
        CreateAccountingEventDto request,
        string releaseReason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.Enabled)
            throw new InvalidOperationException("ACCOUNTING_EVENT_ORCHESTRATION_DISABLED: C6 multi-book release is not enabled.");
        if (!_db.Database.IsRelational())
            throw new InvalidOperationException("ACCOUNTING_EVENT_RELATIONAL_TRANSACTION_REQUIRED: orchestration requires rollback-capable relational transactions.");

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var actorId = RequireActor();
        var eventId = request.AccountingEventId.GetValueOrDefault(Guid.NewGuid());
        if (eventId == Guid.Empty) eventId = Guid.NewGuid();
        var key = CanonicalKey(request.SelectionIdempotencyKey);
        var requestFingerprint = Fingerprint(request, key);

        var existing = await FindByKeyAsync(tenantId, key, cancellationToken);
        if (existing is not null && existing.Status == AccountingEventStatuses.Posted)
        {
            RequireRetryMatch(existing, requestFingerprint);
            return Map(existing);
        }
        if (existing is not null)
        {
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
                        RequireRetryMatch(raced, requestFingerprint);
                        attempted = raced;
                        await transaction.CommitAsync(cancellationToken);
                        return;
                    }

                    if (raced is not null) RequireRetryMatch(raced, requestFingerprint);
                    var lineage = raced is null
                        ? await ResolveLineageAsync(tenantId, request, cancellationToken)
                        : new Lineage(raced.EventKind, raced.Version, raced.RootAccountingEventId,
                            raced.SupersedesAccountingEventId, raced.CorrectsAccountingEventId,
                            raced.ReversesAccountingEventId, null);
                    var posting = request.PostingRequest ?? throw new InvalidOperationException("A Finance posting request is required.");
                    var now = DateTime.UtcNow;
                    releaseAttemptStartedAt = now;
                    attempted = raced ?? new AccountingEvent
                    {
                        Id = eventId, TenantId = tenantId,
                        OriginatingModuleCode = (posting.OriginModuleCode ?? posting.SourceModule).Trim().ToUpperInvariant(),
                        SourceDocumentType = posting.SourceDocumentType.Trim().ToUpperInvariant(),
                        SourceDocumentId = posting.SourceDocumentId, PostingAction = posting.PostingAction.Trim().ToUpperInvariant(),
                        IdempotencyKey = key, EventKind = lineage.Kind, Version = lineage.Version,
                        RootAccountingEventId = lineage.RootId == Guid.Empty ? eventId : lineage.RootId,
                        SupersedesAccountingEventId = lineage.SupersedesId, CorrectsAccountingEventId = lineage.CorrectsId,
                        ReversesAccountingEventId = lineage.ReversesId, SelectionFingerprint = request.ExpectedSelectionFingerprint,
                        RequestFingerprint = requestFingerprint, EventDate = posting.PostingDate.Date,
                        RequestedAtUtc = now, RequestedByUserId = actorId, CreatedAt = now, CreatedBy = ActorName()
                    };
                    if (raced is null) _db.AccountingEvents.Add(attempted);
                    else
                    {
                        attempted.Status = AccountingEventStatuses.Pending; attempted.CompletedAtUtc = null;
                        attempted.FailureMessage = null; attempted.UpdatedAt = now; attempted.UpdatedBy = ActorName();
                    }
                    attempted.ReleasedByUserId ??= actorId;
                    attempted.ReleasedAtUtc ??= now;
                    attempted.ReleaseReason ??= releaseReason;
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

                    var selectedBookIds = attempted.Postings.Select(item => item.AccountingBookId).ToList();
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
                                new AccountingEventPostingAuthority(eventId, evidenceId), cancellationToken)
                            : await _leaf.PostAsync(CopyForBook(posting, representation.AccountingBookCodeSnapshot, representationKey,
                                    includeBudgetReservations: representation.AccountingBookId == reservationConsumerBookId,
                                    isCorrection: lineage.Kind == AccountingEventKinds.Correction, eventId),
                                new AccountingEventPostingAuthority(eventId, evidenceId), cancellationToken);
                        representation.FinancePostingEventId = result.PostingEventId;
                        representation.JournalEntryId = result.JournalEntryId;
                        representation.PostedAtUtc = DateTime.UtcNow;
                        representation.Status = AccountingEventStatuses.Posted;
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
            await PersistFailureAsync(tenantId, actorId, eventId, key, requestFingerprint, request, attempted,
                releaseAttemptStartedAt ?? DateTime.UtcNow, ex, cancellationToken);
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
        string fingerprint, CreateAccountingEventDto request, AccountingEvent? attempted, DateTime attemptStartedAt,
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
                failure.ReleasedByUserId ??= attempted.ReleasedByUserId;
                failure.ReleasedAtUtc ??= attempted.ReleasedAtUtc;
                failure.ReleaseReason ??= attempted.ReleaseReason;
                failure.Status = AccountingEventStatuses.Failed; failure.CompletedAtUtc = now;
                failure.FailureMessage = Truncate(exception.Message, 1000);
                failure.Attempts.Add(new AccountingEventAttempt
                {
                    TenantId = tenantId, AccountingEventId = failure.Id,
                    AttemptNumber = failure.Attempts.Count == 0 ? 1 : failure.Attempts.Max(item => item.AttemptNumber) + 1,
                    RequestFingerprint = fingerprint, Status = AccountingEventStatuses.Failed,
                    StartedAtUtc = attemptStartedAt, CompletedAtUtc = now,
                    FailureMessage = failure.FailureMessage, CreatedAt = now, CreatedBy = ActorName()
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
        bool includeBudgetReservations, bool isCorrection, Guid eventId) => new()
    {
        SourceModule = isCorrection ? "GL" : source.SourceModule,
        OriginModuleCode = isCorrection ? FinanceModuleLockCatalog.Finance : source.OriginModuleCode,
        SourceDocumentType = isCorrection ? "AccountingEventCorrection" : source.SourceDocumentType,
        SourceDocumentId = isCorrection ? eventId : source.SourceDocumentId,
        SourceDocumentTenantId = source.SourceDocumentTenantId, ExistingJournalEntryId = source.ExistingJournalEntryId,
        ReversalOfJournalEntryId = source.ReversalOfJournalEntryId, ReversalReason = source.ReversalReason,
        ReversalType = source.ReversalType, PostingAction = source.PostingAction,
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

    private static string Fingerprint(CreateAccountingEventDto request, string key)
    {
        var posting = request.PostingRequest ?? throw new InvalidOperationException("A Finance posting request is required.");
        var text = new StringBuilder("RHEMA-FINANCE-ACCOUNTING-EVENT-V1");
        static string S(string? value) => value?.Trim() ?? "~";
        static string D(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture) ?? "~";
        static string T(DateTime? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "~";
        void Add(object? value) => text.Append('|').Append(value ?? "~");
        Add(key); Add(S(request.EventKind).ToUpperInvariant()); Add(request.SupersedesAccountingEventId);
        Add(request.CorrectsAccountingEventId); Add(request.ReversesAccountingEventId);
        Add(S(request.ExpectedCalculationInputHash).ToUpperInvariant()); Add(S(request.ExpectedSelectionFingerprint).ToUpperInvariant());
        Add(S(posting.SourceModule).ToUpperInvariant()); Add(S(posting.OriginModuleCode).ToUpperInvariant());
        Add(S(posting.SourceDocumentType).ToUpperInvariant()); Add(posting.SourceDocumentId); Add(posting.SourceDocumentTenantId);
        Add(posting.ExistingJournalEntryId); Add(posting.ReversalOfJournalEntryId); Add(S(posting.ReversalReason));
        Add(S(posting.ReversalType).ToUpperInvariant()); Add(S(posting.PostingAction).ToUpperInvariant());
        Add(S(posting.SourceDocumentReference)); Add(S(posting.Description)); Add(T(posting.PostingDate));
        Add(posting.FiscalPeriodId); Add(S(posting.JournalType).ToUpperInvariant()); Add(S(posting.FunctionalCurrencyCode).ToUpperInvariant());
        Add(posting.ReturnExistingOnDuplicate); Add(S(posting.ExchangeRateTypeOverride).ToUpperInvariant());
        Add(S(posting.ExchangeRateQuoteSideOverride).ToUpperInvariant()); Add(S(posting.ExchangeRateOverrideReason));
        Add(posting.ExchangeRateOverrideApprovedByUserId); Add(T(posting.ExchangeRateOverrideApprovedAt));
        Add(posting.PreserveHistoricalExchangeRateSnapshot); Add(posting.AllowPostingToClosedPeriod);
        Add(S(posting.BudgetReservationSourceDocumentType).ToUpperInvariant());
        foreach (var reservation in posting.BudgetReservationIds.OrderBy(item => item)) Add($"B:{reservation:D}");
        // Collection position is evidence: equal-valued lines in a different order are not silently conflated.
        foreach (var pair in posting.Lines.Select((line, index) => (line, index)))
        {
            var line = pair.line;
            Add($"L:{pair.index}:{line.LineNumber}:{line.AccountId:D}:{line.SourceDocumentLineId}:{S(line.Description)}:{D(line.DebitAmount)}:{D(line.CreditAmount)}:{S(line.TransactionCurrency).ToUpperInvariant()}:{D(line.TransactionDebitAmount)}:{D(line.TransactionCreditAmount)}:{D(line.ForeignCurrencyAmount)}:{line.ExchangeRateId}:{D(line.ExchangeRate)}:{S(line.ExchangeRateSource)}:{T(line.ExchangeRateDate)}:{S(line.SourceReferenceNumber)}:{line.FinanceDimensionSetId}:{S(line.SegmentString)}:{S(line.Notes)}:{S(line.TransactionTag)}");
            foreach (var dimension in line.Dimensions.OrderBy(item => item.DimensionCode, StringComparer.Ordinal)
                         .ThenBy(item => item.ValueCode, StringComparer.Ordinal).ThenBy(item => item.SourceEntityType, StringComparer.Ordinal)
                         .ThenBy(item => item.SourceEntityId))
                Add($"DIM:{S(dimension.DimensionCode).ToUpperInvariant()}:{S(dimension.ValueCode).ToUpperInvariant()}:{S(dimension.SourceEntityType).ToUpperInvariant()}:{dimension.SourceEntityId}");
        }
        foreach (var tax in posting.TaxCalculationSnapshots.OrderBy(item => item.CalculationOrder)
                     .ThenBy(item => item.DocumentType, StringComparer.Ordinal).ThenBy(item => item.DocumentId).ThenBy(item => item.DocumentLineId).ThenBy(item => item.TaxId))
            Add($"TAX:{S(tax.DocumentType).ToUpperInvariant()}:{tax.DocumentId:D}:{tax.DocumentLineId}:{tax.TaxId:D}:{tax.TaxGroupId}:{tax.PostingAccountId}:{D(tax.BaseAmount)}:{D(tax.TaxableAmount)}:{D(tax.TaxRate)}:{D(tax.TaxAmount)}:{tax.CompoundBasis}:{tax.CalculationOrder}:{T(tax.CalculationDate)}:{tax.IsManualOverride}:{S(tax.OverrideReason)}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
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
