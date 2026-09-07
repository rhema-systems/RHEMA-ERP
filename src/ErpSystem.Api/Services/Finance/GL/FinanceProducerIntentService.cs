using System.Text.RegularExpressions;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
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
public sealed partial class FinanceProducerIntentService : IFinanceProducerIntentService, IFinanceProducerApprovedExecution
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

    private async Task<CreateAccountingEventDto> BuildRequestAsync(ProducerAccountingIntentDto intent, CancellationToken ct)
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

    private async Task<CreateAccountingEventDto> BuildPreparedRequestAsync(Guid accountingEventId,
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
        if (normalized.Length is 0 or > 100 || !ParticipantIdentityPattern().IsMatch(normalized))
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
        if (entityType.Length is 0 or > 100 || action.Length is 0 or > 60 || effect.OwnerEntityId == Guid.Empty
            || fingerprint.Length != 64 || fingerprint.All(ch => ch == '0') || fingerprint.Any(ch => !Uri.IsHexDigit(ch)))
            throw new InvalidOperationException("PRODUCER_OWNER_EFFECT_INVALID: a stable entity, action and 64-character fingerprint are required.");
        return new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = participantCode, OwnerEntityType = entityType,
            OwnerEntityId = effect.OwnerEntityId, OwnerAction = action, EffectFingerprint = fingerprint
        };
    }

    private static void RequireReason(DecideProducerAccountingIntentDto decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (string.IsNullOrWhiteSpace(decision.Reason))
            throw new InvalidOperationException("A governed approval or rejection reason is required.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ParticipantIdentityPattern();
}
