using System.Text.RegularExpressions;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Producer-facing Finance adapter. It converts one neutral intent into governed C5 preview evidence and
/// C6 maker/checker state; book choice and per-book execution remain entirely inside Finance.
/// </summary>
public sealed partial class FinanceProducerIntentService : IFinanceProducerIntentService, IFinanceProducerApprovedExecution,
    IFinanceProducerApprovedExecutionService, IFinanceProducerReversalPreparationService
{
    private readonly IAccountingBookApplicabilityService _applicability;
    private readonly IAccountingEventService _events;
    private readonly ITrustedAccountingEventExecutor _executor;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly FinanceProducerIntentOptions _options;

    public FinanceProducerIntentService(IAccountingBookApplicabilityService applicability,
        IAccountingEventService events, AccountingEventService executor,
        ApplicationDbContext db, ICurrentUserService currentUser,
        IOptions<FinanceProducerIntentOptions> options)
        : this(applicability, events, (ITrustedAccountingEventExecutor)executor, db, currentUser, options)
    {
    }

    internal FinanceProducerIntentService(IAccountingBookApplicabilityService applicability,
        IAccountingEventService events, ITrustedAccountingEventExecutor executor,
        ApplicationDbContext db, ICurrentUserService currentUser,
        IOptions<FinanceProducerIntentOptions> options)
    {
        _applicability = applicability;
        _events = events;
        _executor = executor;
        _db = db;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<AccountingEventDto> PrepareAsync(ProducerAccountingIntentDto intent, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        ValidateIntent(intent);
        var request = await BuildRequestAsync(intent, cancellationToken);
        return await _events.CreateAsync(request, cancellationToken);
    }

    public async Task<ProducerAccountingReversalPreparationResultDto> PrepareReversalAsync(
        PrepareProducerAccountingReversalDto request, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        ArgumentNullException.ThrowIfNull(request);
        if (request.OriginalAccountingEventId == Guid.Empty || request.ReversalAccountingEventId == Guid.Empty
            || request.OriginalAccountingEventId == request.ReversalAccountingEventId)
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_IDENTITY_INVALID: distinct original and deterministic reversal identities are required.");
        if (request.ReversalDate == default)
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_DATE_INVALID: an exact reversal date is required.");
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 500)
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_REASON_INVALID: a reason of 1 to 500 characters is required.");

        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var originalRequest = await ReconstructPreparedRequestAsync(
            request.OriginalAccountingEventId, cancellationToken);
        var original = await _db.AccountingEvents.AsNoTracking()
            .Include(item => item.AccountingBookSelectionEvidence)!
                .ThenInclude(item => item!.Books)
            .Include(item => item.Postings)
            .Include(item => item.ProducerReceipt)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == request.OriginalAccountingEventId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Original AccountingEvent was not found for this tenant.");
        RequireExactReversalOriginal(original, originalRequest, tenantId);

        var participant = CanonicalParticipant(request.ParticipantIdentity);
        if (!string.Equals(participant, original.ProducerParticipantIdentity, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_PARTICIPANT_CONFLICT: reversal preparation must retain the original participant authority.");
        var ownerEffect = CanonicalOwnerEffect(request.ExpectedOwnerEffect, participant);
        RequireReversalOwnerAuthority(originalRequest.ExpectedOwnerEffect, ownerEffect);

        var key = request.IdempotencyKey?.Trim().ToUpperInvariant() ?? string.Empty;
        var existingById = await _db.AccountingEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == request.ReversalAccountingEventId && !item.IsDeleted,
            cancellationToken);
        var existingByKey = await _db.AccountingEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.IdempotencyKey == key && !item.IsDeleted,
            cancellationToken);
        if ((existingById is not null && !string.Equals(existingById.IdempotencyKey, key, StringComparison.Ordinal))
            || (existingByKey is not null && existingByKey.Id != request.ReversalAccountingEventId))
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_IDENTITY_CONFLICT: reversal ID and idempotency identity must remain paired.");

        var prepared = await PrepareAsync(new ProducerAccountingIntentDto
        {
            AccountingEventId = request.ReversalAccountingEventId,
            EventKind = AccountingEventKinds.Reversal,
            SupersedesAccountingEventId = original.Id,
            ReversesAccountingEventId = original.Id,
            IdempotencyKey = key,
            ParticipantIdentity = participant,
            ExpectedOwnerEffect = ownerEffect,
            PostingRequest = BuildExactReversalPosting(originalRequest.PostingRequest,
                tenantId, request.ReversalDate.Date, reason)
        }, cancellationToken);

        return new ProducerAccountingReversalPreparationResultDto(
            prepared.Id, original.Id, prepared.RequestFingerprint, prepared.Status,
            prepared.ProducerDecisionStatus);
    }

    public async Task<AccountingEventDto> GetAsync(Guid accountingEventId, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        _ = await ReconstructPreparedRequestAsync(accountingEventId, cancellationToken);
        return await _events.GetAsync(accountingEventId, cancellationToken);
    }

    public Task<AccountingEventDto> ApprovePreparedAsync(Guid accountingEventId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default) =>
        DecidePreparedAsync(accountingEventId, decision, approve: true, cancellationToken);

    public Task<AccountingEventDto> RejectPreparedAsync(Guid accountingEventId,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default) =>
        DecidePreparedAsync(accountingEventId, decision, approve: false, cancellationToken);

    private async Task<AccountingEventDto> DecidePreparedAsync(Guid accountingEventId,
        DecideProducerAccountingIntentDto decision, bool approve, CancellationToken cancellationToken)
    {
        RequireEnabled();
        RequireReason(decision);
        var request = await ReconstructPreparedRequestAsync(accountingEventId, cancellationToken);
        var release = new ReleaseAccountingEventDto { Request = request, Reason = decision.Reason };
        return approve
            ? await _events.ApproveAsync(accountingEventId, release, cancellationToken)
            : await _events.RejectAsync(accountingEventId, release, cancellationToken);
    }

    public async Task<AccountingEventDto> ApproveAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        RequireReason(decision);
        return await _events.ApproveAsync(accountingEventId,
            new ReleaseAccountingEventDto { Request = await BuildPreparedRequestAsync(accountingEventId, intent, cancellationToken), Reason = decision.Reason }, cancellationToken);
    }

    public async Task<AccountingEventDto> RejectAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        RequireReason(decision);
        return await _events.RejectAsync(accountingEventId,
            new ReleaseAccountingEventDto { Request = await BuildPreparedRequestAsync(accountingEventId, intent, cancellationToken), Reason = decision.Reason }, cancellationToken);
    }

    async Task<AccountingEventDto> IFinanceProducerApprovedExecution.ExecuteInAmbientTransactionAsync(Guid accountingEventId,
        ProducerAccountingIntentDto intent, ProducerOwnerEffectReceiptDto receipt, CancellationToken cancellationToken)
    {
        RequireEnabled();
        ValidateIntent(intent);
        var approved = await _events.GetAsync(accountingEventId, cancellationToken);
        if (approved.ProducerDecisionStatus != "Approved"
            || approved.Status is not ("PendingApproval" or "Failed" or "Posted"))
            throw new InvalidOperationException("ACCOUNTING_EVENT_APPROVAL_REQUIRED: execution requires durable independent approval.");
        var request = await BuildPreparedRequestAsync(accountingEventId, intent, cancellationToken);
        return await _executor.ExecuteApprovedInAmbientTransactionAsync(accountingEventId,
            new ReleaseAccountingEventDto { Request = request, Reason = approved.ProducerDecisionReason ?? string.Empty }, receipt, cancellationToken);
    }

    async Task<FinanceProducerApprovedExecutionResult> IFinanceProducerApprovedExecution.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
        Guid accountingEventId, ProducerAccountingIntentDto intent, ProducerOwnerEffectReceiptDto receipt,
        CancellationToken cancellationToken)
    {
        var accountingEvent = await ((IFinanceProducerApprovedExecution)this)
            .ExecuteInAmbientTransactionAsync(accountingEventId, intent, receipt, cancellationToken);
        return await FinanceProducerCompatibilityAuthority.ResolveAsync(
            _db, _currentUser.GetRequiredFinanceTenantId(), accountingEvent, cancellationToken);
    }

    async Task IFinanceProducerApprovedExecution.RecordFailureAfterRollbackAsync(Guid accountingEventId,
        ProducerAccountingIntentDto intent, ProducerOwnerEffectReceiptDto receipt, Exception failure,
        CancellationToken cancellationToken)
    {
        RequireEnabled();
        ArgumentNullException.ThrowIfNull(failure);
        var prepared = await _events.GetAsync(accountingEventId, cancellationToken);
        var request = await BuildPreparedRequestAsync(accountingEventId, intent, cancellationToken);
        await _executor.RecordApprovedFailureAfterRollbackAsync(accountingEventId,
            new ReleaseAccountingEventDto { Request = request, Reason = prepared.ProducerDecisionReason ?? string.Empty },
            receipt, failure, cancellationToken);
    }

    async Task<FinanceProducerApprovedExecutionResultDto>
        IFinanceProducerApprovedExecutionService.ExecuteInAmbientTransactionAsync(
            Guid accountingEventId, ProducerAccountingIntentDto preparedIntent,
            ProducerOwnerEffectReceiptDto receipt, CancellationToken cancellationToken)
    {
        var result = await ((IFinanceProducerApprovedExecution)this)
            .ExecuteWithCompatibilityResultInAmbientTransactionAsync(
                accountingEventId, preparedIntent, receipt, cancellationToken);
        return new FinanceProducerApprovedExecutionResultDto(
            result.AccountingEventId,
            result.AccountingEventRequestFingerprint,
            result.Status,
            result.FinancePostingEventId,
            result.JournalEntryId);
    }

    Task IFinanceProducerApprovedExecutionService.RecordFailureAfterRollbackAsync(
        Guid accountingEventId, ProducerAccountingIntentDto preparedIntent,
        ProducerOwnerEffectReceiptDto receipt, Exception failure,
        CancellationToken cancellationToken) =>
        ((IFinanceProducerApprovedExecution)this).RecordFailureAfterRollbackAsync(
            accountingEventId, preparedIntent, receipt, failure, cancellationToken);

    internal async Task<CreateAccountingEventDto> BuildRequestAsync(ProducerAccountingIntentDto intent, CancellationToken ct)
    {
        ValidateIntent(intent);
        var posting = intent.PostingRequest;
        string calculationInputHash;
        string selectionFingerprint;
        if (string.Equals(intent.EventKind?.Trim(), AccountingEventKinds.Original, StringComparison.OrdinalIgnoreCase))
        {
            var preview = await _applicability.ResolveAsync(new ResolveAccountingBookApplicabilityDto
            {
                EffectiveDate = posting.PostingDate,
                OriginatingModuleCode = posting.OriginModuleCode ?? posting.SourceModule,
                SourceDocumentType = posting.SourceDocumentType,
                PostingAction = posting.PostingAction
            }, ct);
            if (preview.Blockers.Count != 0)
                throw new InvalidOperationException($"ACCOUNTING_BOOK_APPLICABILITY_BLOCKED: {string.Join("; ", preview.Blockers.Select(item => $"{item.Code}: {item.Message}"))}");
            if (preview.Books.Count == 0)
                throw new InvalidOperationException("ACCOUNTING_BOOK_APPLICABILITY_EMPTY_SELECTION: Finance preview selected no full accounting book.");
            calculationInputHash = preview.CalculationInputHash;
            selectionFingerprint = preview.SelectionFingerprint;
        }
        else
        {
            var targetId = intent.CorrectsAccountingEventId ?? intent.ReversesAccountingEventId
                ?? throw new InvalidOperationException("Producer correction or reversal requires exact target lineage.");
            var tenantId = _currentUser.GetRequiredFinanceTenantId();
            var target = await _db.AccountingEvents.AsNoTracking().Include(item => item.AccountingBookSelectionEvidence)
                .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == targetId && !item.IsDeleted, ct)
                ?? throw new InvalidOperationException("Producer correction or reversal target was not found for this tenant.");
            if (target.Status != AccountingEventStatuses.Posted || target.AccountingBookSelectionEvidence is null)
                throw new InvalidOperationException("Producer correction or reversal requires immutable posted C5 evidence.");
            calculationInputHash = target.AccountingBookSelectionEvidence.CalculationInputHash;
            selectionFingerprint = target.SelectionFingerprint;
        }
        return new CreateAccountingEventDto
        {
            AccountingEventId = intent.AccountingEventId,
            EventKind = intent.EventKind ?? AccountingEventKinds.Original,
            SupersedesAccountingEventId = intent.SupersedesAccountingEventId,
            CorrectsAccountingEventId = intent.CorrectsAccountingEventId,
            ReversesAccountingEventId = intent.ReversesAccountingEventId,
            SelectionIdempotencyKey = intent.IdempotencyKey,
            ExpectedCalculationInputHash = calculationInputHash,
            ExpectedSelectionFingerprint = selectionFingerprint,
            ProducerParticipantIdentity = CanonicalParticipant(intent.ParticipantIdentity),
            ExpectedOwnerEffect = CanonicalOwnerEffect(intent.ExpectedOwnerEffect, intent.ParticipantIdentity),
            PostingRequest = ToFinancePosting(posting)
        };
    }

    internal async Task<CreateAccountingEventDto> BuildPreparedRequestAsync(Guid accountingEventId,
        ProducerAccountingIntentDto intent, CancellationToken ct)
    {
        ValidateIntent(intent);
        var prepared = await _events.GetAsync(accountingEventId, ct);
        if (string.IsNullOrWhiteSpace(prepared.ProducerIntentSnapshotJson))
            throw new InvalidOperationException("ACCOUNTING_EVENT_SNAPSHOT_INVALID: prepared producer evidence is unavailable.");
        var frozen = JsonSerializer.Deserialize<CreateAccountingEventDto>(prepared.ProducerIntentSnapshotJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("ACCOUNTING_EVENT_SNAPSHOT_INVALID: prepared producer evidence cannot be reconstructed.");
        return new CreateAccountingEventDto
        {
            AccountingEventId = accountingEventId, EventKind = intent.EventKind ?? AccountingEventKinds.Original,
            SupersedesAccountingEventId = intent.SupersedesAccountingEventId,
            CorrectsAccountingEventId = intent.CorrectsAccountingEventId, ReversesAccountingEventId = intent.ReversesAccountingEventId,
            SelectionIdempotencyKey = intent.IdempotencyKey,
            ExpectedCalculationInputHash = frozen.ExpectedCalculationInputHash,
            ExpectedSelectionFingerprint = frozen.ExpectedSelectionFingerprint,
            ProducerParticipantIdentity = CanonicalParticipant(intent.ParticipantIdentity),
            ExpectedOwnerEffect = CanonicalOwnerEffect(intent.ExpectedOwnerEffect, intent.ParticipantIdentity),
            PostingRequest = ToFinancePosting(intent.PostingRequest)
        };
    }

    internal async Task<CreateAccountingEventDto> ReconstructPreparedRequestAsync(Guid accountingEventId,
        CancellationToken cancellationToken)
    {
        // The checker deliberately supplies no economic payload. Finance reconstructs and rebinds the
        // complete immutable maker snapshot so approval cannot substitute lines, source or lineage.
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        var prepared = await _db.AccountingEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == accountingEventId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("AccountingEvent was not found.");
        if (string.IsNullOrWhiteSpace(prepared.ProducerParticipantIdentity)
            || prepared.ProducerDecisionStatus == ProducerIntentDecisionStatuses.NotRequired
            || prepared.PreparedByUserId == Guid.Empty || prepared.RequestedByUserId != prepared.PreparedByUserId)
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_PREPARED_AUTHORITY_INVALID: durable producer maker or participant authority is incomplete.");
        if (string.IsNullOrWhiteSpace(prepared.ProducerIntentSnapshotJson)
            || prepared.ProducerIntentSnapshotHash?.Length != 64
            || !string.Equals(prepared.ProducerIntentSnapshotHash,
                Sha256(prepared.ProducerIntentSnapshotJson), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_SNAPSHOT_INVALID: immutable producer intent evidence is missing or corrupted.");

        CreateAccountingEventDto frozen;
        try
        {
            frozen = JsonSerializer.Deserialize<CreateAccountingEventDto>(prepared.ProducerIntentSnapshotJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new JsonException("Snapshot deserialized to null.");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_SNAPSHOT_INVALID: prepared producer evidence cannot be reconstructed.", error);
        }

        var posting = frozen.PostingRequest
            ?? throw new InvalidOperationException("ACCOUNTING_EVENT_SNAPSHOT_INVALID: Finance posting evidence is missing.");
        var identity = FinancePreparedIdentityNormalizer.Normalize(
            posting.OriginModuleCode ?? posting.SourceModule, posting.SourceDocumentType, posting.PostingAction);
        var key = frozen.SelectionIdempotencyKey?.Trim().ToUpperInvariant() ?? string.Empty;
        var participant = CanonicalParticipant(frozen.ProducerParticipantIdentity);
        _ = CanonicalOwnerEffect(frozen.ExpectedOwnerEffect, participant);
        var fingerprint = AccountingEventService.Fingerprint(
            frozen, key, prepared.Version, prepared.RootAccountingEventId);
        if (frozen.AccountingEventId != prepared.Id
            || !string.Equals(frozen.EventKind?.Trim(), prepared.EventKind, StringComparison.OrdinalIgnoreCase)
            || frozen.SupersedesAccountingEventId != prepared.SupersedesAccountingEventId
            || frozen.CorrectsAccountingEventId != prepared.CorrectsAccountingEventId
            || frozen.ReversesAccountingEventId != prepared.ReversesAccountingEventId
            || !string.Equals(key, prepared.IdempotencyKey, StringComparison.Ordinal)
            || !string.Equals(participant, prepared.ProducerParticipantIdentity, StringComparison.Ordinal)
            || !string.Equals(identity.OriginatingModuleCode, prepared.OriginatingModuleCode, StringComparison.Ordinal)
            || !string.Equals(identity.SourceDocumentType, prepared.SourceDocumentType, StringComparison.Ordinal)
            || posting.SourceDocumentId != prepared.SourceDocumentId
            || !string.Equals(identity.PostingAction, prepared.PostingAction, StringComparison.Ordinal)
            || posting.PostingDate.Date != prepared.EventDate.Date
            || !string.Equals(frozen.ExpectedSelectionFingerprint, prepared.SelectionFingerprint, StringComparison.Ordinal)
            || !string.Equals(fingerprint, prepared.RequestFingerprint, StringComparison.Ordinal)
            || (posting.SourceDocumentTenantId.HasValue && posting.SourceDocumentTenantId != tenantId))
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_SNAPSHOT_AUTHORITY_CONFLICT: durable source, tenant, lineage or fingerprint differs from the canonical snapshot.");
        return frozen;
    }

    internal static ProducerAccountingIntentDto ToProducerIntent(CreateAccountingEventDto source) => new()
    {
        AccountingEventId = source.AccountingEventId, EventKind = source.EventKind,
        SupersedesAccountingEventId = source.SupersedesAccountingEventId,
        CorrectsAccountingEventId = source.CorrectsAccountingEventId,
        ReversesAccountingEventId = source.ReversesAccountingEventId,
        IdempotencyKey = source.SelectionIdempotencyKey,
        ParticipantIdentity = source.ProducerParticipantIdentity,
        ExpectedOwnerEffect = source.ExpectedOwnerEffect
            ?? throw new InvalidOperationException("ACCOUNTING_EVENT_SNAPSHOT_INVALID: owner-effect authority is missing."),
        PostingRequest = ToProducerPosting(source.PostingRequest)
    };

    private static void RequireExactReversalOriginal(AccountingEvent original,
        CreateAccountingEventDto snapshot, Guid tenantId)
    {
        if (original.EventKind != AccountingEventKinds.Original || original.Version != 1
            || original.RootAccountingEventId != original.Id
            || original.SupersedesAccountingEventId.HasValue || original.CorrectsAccountingEventId.HasValue
            || original.ReversesAccountingEventId.HasValue)
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_ORIGINAL_KIND_INVALID: only a canonical original C7 event may be reversed.");
        if (original.Status != AccountingEventStatuses.Posted
            || original.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Approved
            || original.AccountingBookSelectionEvidence is null)
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_ORIGINAL_NOT_POSTED: exact reversal requires approved posted C7 evidence.");

        var evidence = original.AccountingBookSelectionEvidence;
        if (evidence.TenantId != tenantId || evidence.Id != original.AccountingBookSelectionEvidenceId
            || !string.Equals(evidence.CalculationInputHash, snapshot.ExpectedCalculationInputHash, StringComparison.Ordinal)
            || !string.Equals(evidence.SelectionFingerprint, snapshot.ExpectedSelectionFingerprint, StringComparison.Ordinal)
            || !string.Equals(evidence.SelectionFingerprint, original.SelectionFingerprint, StringComparison.Ordinal)
            || evidence.EffectiveDate.Date != original.EventDate.Date
            || !string.Equals(evidence.OriginatingModuleCode, original.OriginatingModuleCode, StringComparison.Ordinal)
            || !string.Equals(evidence.SourceDocumentType, original.SourceDocumentType, StringComparison.Ordinal)
            || !string.Equals(evidence.PostingAction, original.PostingAction, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_FROZEN_AUTHORITY_INVALID: original C5 evidence conflicts with the immutable C7 snapshot.");

        var selected = evidence.Books.OrderBy(item => item.SelectionOrder).ToList();
        var posted = original.Postings.OrderBy(item => item.SelectionOrder).ToList();
        if (selected.Count == 0 || selected.Count != posted.Count
            || selected.Any(item => item.TenantId != tenantId
                || item.AccountingBookSelectionEvidenceId != evidence.Id)
            || posted.Any(item => item.TenantId != tenantId || item.AccountingEventId != original.Id
                || item.EventVersion != original.Version || item.Status != AccountingEventStatuses.Posted
                || !item.FinancePostingEventId.HasValue || !item.JournalEntryId.HasValue)
            || selected.Zip(posted).Any(pair => pair.First.AccountingBookId != pair.Second.AccountingBookId
                || pair.First.SelectionOrder != pair.Second.SelectionOrder
                || !string.Equals(pair.First.AccountingBookCodeSnapshot,
                    pair.Second.AccountingBookCodeSnapshot, StringComparison.Ordinal)
                || !string.Equals(pair.First.AuthorityFingerprint,
                    pair.Second.AuthorityFingerprint, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_FROZEN_SET_INVALID: original selected-book and posted representation evidence disagree.");

        var expected = snapshot.ExpectedOwnerEffect
            ?? throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_OWNER_AUTHORITY_INVALID: original owner authority is missing.");
        var receipt = original.ProducerReceipt;
        if (receipt is null || receipt.TenantId != tenantId || receipt.AccountingEventId != original.Id
            || !string.Equals(receipt.ParticipantCode, original.ProducerParticipantIdentity, StringComparison.Ordinal)
            || !string.Equals(receipt.ParticipantCode, expected.ParticipantCode, StringComparison.Ordinal)
            || !string.Equals(receipt.OwnerEntityType, expected.OwnerEntityType, StringComparison.Ordinal)
            || receipt.OwnerEntityId != expected.OwnerEntityId
            || !string.Equals(receipt.OwnerAction, expected.OwnerAction, StringComparison.Ordinal)
            || !string.Equals(receipt.EffectFingerprint, expected.EffectFingerprint, StringComparison.Ordinal)
            || !string.Equals(receipt.RequestFingerprint, original.RequestFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_OWNER_AUTHORITY_INVALID: original participant receipt conflicts with the immutable snapshot.");
    }

    private static void RequireReversalOwnerAuthority(ProducerOwnerEffectIdentityDto? original,
        ProducerOwnerEffectIdentityDto reversal)
    {
        if (original is null
            || !string.Equals(original.ParticipantCode, reversal.ParticipantCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(original.OwnerEntityType, reversal.OwnerEntityType, StringComparison.OrdinalIgnoreCase)
            || original.OwnerEntityId != reversal.OwnerEntityId)
            throw new InvalidOperationException(
                "ACCOUNTING_EVENT_REVERSAL_OWNER_AUTHORITY_CONFLICT: reversal owner lineage must retain the original participant, entity type and entity ID.");
    }

    private static ProducerFinancePostingRequestDto BuildExactReversalPosting(
        FinancePostingRequestV2Dto original, Guid tenantId, DateTime reversalDate, string reason) => new()
    {
        SourceModule = original.SourceModule,
        OriginModuleCode = original.OriginModuleCode,
        SourceDocumentType = original.SourceDocumentType,
        SourceDocumentId = original.SourceDocumentId,
        SourceDocumentTenantId = original.SourceDocumentTenantId ?? tenantId,
        ReversalReason = reason,
        ReversalType = "Exact producer AccountingEvent reversal",
        PostingAction = original.PostingAction,
        SourceDocumentReference = original.SourceDocumentReference,
        Description = $"Reversal: {original.Description}",
        PostingDate = reversalDate,
        JournalType = original.JournalType,
        FunctionalCurrencyCode = original.FunctionalCurrencyCode,
        ReturnExistingOnDuplicate = true,
        PreserveHistoricalExchangeRateSnapshot = true,
        Lines = original.Lines.Select(line => new FinancePostingLineDto
        {
            AccountId = line.AccountId,
            SourceDocumentLineId = line.SourceDocumentLineId,
            Description = $"Reversal: {line.Description}",
            DebitAmount = line.CreditAmount,
            CreditAmount = line.DebitAmount,
            TransactionCurrency = line.TransactionCurrency,
            TransactionDebitAmount = line.TransactionCreditAmount,
            TransactionCreditAmount = line.TransactionDebitAmount,
            ForeignCurrencyAmount = line.ForeignCurrencyAmount,
            ExchangeRateId = line.ExchangeRateId,
            ExchangeRate = line.ExchangeRate,
            ExchangeRateSource = line.ExchangeRateSource,
            ExchangeRateDate = line.ExchangeRateDate,
            SourceReferenceNumber = line.SourceReferenceNumber,
            LineNumber = line.LineNumber,
            Dimensions = line.Dimensions.Select(dimension => new FinancePostingDimensionValueDto
            {
                DimensionCode = dimension.DimensionCode,
                ValueCode = dimension.ValueCode,
                SourceEntityType = dimension.SourceEntityType,
                SourceEntityId = dimension.SourceEntityId
            }).ToList(),
            FinanceDimensionSetId = line.FinanceDimensionSetId,
            SegmentString = line.SegmentString,
            Notes = reason,
            TransactionTag = "Reversal"
        }).ToList()
    };

    private static ProducerFinancePostingRequestDto ToProducerPosting(FinancePostingRequestV2Dto source) => new()
    {
        SourceModule = source.SourceModule, OriginModuleCode = source.OriginModuleCode,
        SourceDocumentType = source.SourceDocumentType, SourceDocumentId = source.SourceDocumentId,
        SourceDocumentTenantId = source.SourceDocumentTenantId, ExistingJournalEntryId = source.ExistingJournalEntryId,
        ReversalOfJournalEntryId = source.ReversalOfJournalEntryId, ReversalReason = source.ReversalReason,
        ReversalType = source.ReversalType, PostingAction = source.PostingAction,
        SourceDocumentReference = source.SourceDocumentReference, Description = source.Description,
        PostingDate = source.PostingDate, FiscalPeriodId = source.FiscalPeriodId, JournalType = source.JournalType,
        FunctionalCurrencyCode = source.FunctionalCurrencyCode, IdempotencyKey = source.IdempotencyKey,
        ReturnExistingOnDuplicate = source.ReturnExistingOnDuplicate,
        ExchangeRateTypeOverride = source.ExchangeRateTypeOverride,
        ExchangeRateQuoteSideOverride = source.ExchangeRateQuoteSideOverride,
        ExchangeRateOverrideReason = source.ExchangeRateOverrideReason,
        ExchangeRateOverrideApprovedByUserId = source.ExchangeRateOverrideApprovedByUserId,
        ExchangeRateOverrideApprovedAt = source.ExchangeRateOverrideApprovedAt,
        PreserveHistoricalExchangeRateSnapshot = source.PreserveHistoricalExchangeRateSnapshot,
        AllowPostingToClosedPeriod = source.AllowPostingToClosedPeriod,
        BudgetReservationIds = source.BudgetReservationIds,
        BudgetReservationSourceDocumentType = source.BudgetReservationSourceDocumentType,
        Lines = source.Lines, TaxCalculationSnapshots = source.TaxCalculationSnapshots
    };

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static FinancePostingRequestV2Dto ToFinancePosting(ProducerFinancePostingRequestDto source) => new()
    {
        SourceModule = source.SourceModule, OriginModuleCode = source.OriginModuleCode,
        SourceDocumentType = source.SourceDocumentType, SourceDocumentId = source.SourceDocumentId,
        SourceDocumentTenantId = source.SourceDocumentTenantId, ExistingJournalEntryId = source.ExistingJournalEntryId,
        ReversalOfJournalEntryId = source.ReversalOfJournalEntryId, ReversalReason = source.ReversalReason,
        ReversalType = source.ReversalType, PostingAction = source.PostingAction,
        SourceDocumentReference = source.SourceDocumentReference, Description = source.Description,
        PostingDate = source.PostingDate, FiscalPeriodId = source.FiscalPeriodId, JournalType = source.JournalType,
        FunctionalCurrencyCode = source.FunctionalCurrencyCode, IdempotencyKey = source.IdempotencyKey,
        ReturnExistingOnDuplicate = source.ReturnExistingOnDuplicate,
        ExchangeRateTypeOverride = source.ExchangeRateTypeOverride,
        ExchangeRateQuoteSideOverride = source.ExchangeRateQuoteSideOverride,
        ExchangeRateOverrideReason = source.ExchangeRateOverrideReason,
        ExchangeRateOverrideApprovedByUserId = source.ExchangeRateOverrideApprovedByUserId,
        ExchangeRateOverrideApprovedAt = source.ExchangeRateOverrideApprovedAt,
        PreserveHistoricalExchangeRateSnapshot = source.PreserveHistoricalExchangeRateSnapshot,
        AllowPostingToClosedPeriod = source.AllowPostingToClosedPeriod,
        BudgetReservationIds = source.BudgetReservationIds,
        BudgetReservationSourceDocumentType = source.BudgetReservationSourceDocumentType,
        Lines = source.Lines, TaxCalculationSnapshots = source.TaxCalculationSnapshots,
        // Empty is intentional. C5 frozen evidence is the only book-selection authority.
        AccountingBookCode = string.Empty
    };

    private void RequireEnabled()
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("FINANCE_PRODUCER_INTENT_DISABLED: staged producer intents are not enabled.");
    }

    private static void ValidateIntent(ProducerAccountingIntentDto intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(intent.PostingRequest);
        _ = CanonicalParticipant(intent.ParticipantIdentity);
        _ = CanonicalOwnerEffect(intent.ExpectedOwnerEffect, intent.ParticipantIdentity);
        if (intent.PostingRequest.Lines.Count == 0)
            throw new InvalidOperationException("PRODUCER_INTENT_LINES_REQUIRED: at least one economic line is required.");
        var debit = intent.PostingRequest.Lines.Sum(line => line.DebitAmount);
        var credit = intent.PostingRequest.Lines.Sum(line => line.CreditAmount);
        if (debit <= 0 || debit != credit)
            throw new InvalidOperationException("PRODUCER_INTENT_UNBALANCED: neutral economic lines must be positive and exactly balanced.");
    }

    private static string CanonicalParticipant(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 100 || !ParticipantIdentityPattern().IsMatch(normalized)
            || IsPseudoIdentity(normalized))
            throw new InvalidOperationException("Producer participant identity must be 1 to 100 canonical characters.");
        return normalized;
    }

    private static ProducerOwnerEffectIdentityDto CanonicalOwnerEffect(ProducerOwnerEffectIdentityDto? effect, string participant)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var participantCode = CanonicalParticipant(effect.ParticipantCode);
        if (!string.Equals(participantCode, CanonicalParticipant(participant), StringComparison.Ordinal))
            throw new InvalidOperationException("PRODUCER_OWNER_EFFECT_PARTICIPANT_CONFLICT: expected effect must retain the producer participant.");
        var entityType = effect.OwnerEntityType?.Trim().ToUpperInvariant() ?? string.Empty;
        var action = effect.OwnerAction?.Trim().ToUpperInvariant() ?? string.Empty;
        var fingerprint = effect.EffectFingerprint?.Trim().ToUpperInvariant() ?? string.Empty;
        if (entityType.Length is 0 or > 100 || action.Length is 0 or > 60
            || !ParticipantIdentityPattern().IsMatch(entityType) || !ParticipantIdentityPattern().IsMatch(action)
            || IsPseudoIdentity(entityType) || IsPseudoIdentity(action)
            || effect.OwnerEntityId == Guid.Empty
            || fingerprint.Length != 64 || fingerprint.All(ch => ch == '0') || fingerprint.Any(ch => !Uri.IsHexDigit(ch)))
            throw new InvalidOperationException("PRODUCER_OWNER_EFFECT_INVALID: a stable entity, action and 64-character fingerprint are required.");
        return new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = participantCode, OwnerEntityType = entityType,
            OwnerEntityId = effect.OwnerEntityId, OwnerAction = action, EffectFingerprint = fingerprint
        };
    }

    private static bool IsPseudoIdentity(string value) => value is "ALL" or "ALL_ACTIVE_BOOKS"
        or "ALL_CLASSIFIED_BOOKS" or "ALLCLASSIFIEDBOOKS";

    private static void RequireReason(DecideProducerAccountingIntentDto decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (string.IsNullOrWhiteSpace(decision.Reason))
            throw new InvalidOperationException("A governed approval or rejection reason is required.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ParticipantIdentityPattern();
}
