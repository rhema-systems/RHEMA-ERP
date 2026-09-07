using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Producer-facing Finance adapter. It converts one neutral intent into governed C5 preview evidence and
/// C6 maker/checker state; book choice and per-book execution remain entirely inside Finance.
/// </summary>
public sealed partial class FinanceProducerIntentService : IFinanceProducerIntentService
{
    private readonly IAccountingBookApplicabilityService _applicability;
    private readonly IAccountingEventService _events;
    private readonly FinanceProducerIntentOptions _options;

    public FinanceProducerIntentService(IAccountingBookApplicabilityService applicability,
        IAccountingEventService events, IOptions<FinanceProducerIntentOptions> options)
    {
        _applicability = applicability;
        _events = events;
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
            new ReleaseAccountingEventDto { Request = await BuildRequestAsync(intent, cancellationToken), Reason = decision.Reason }, cancellationToken);
    }

    public async Task<AccountingEventDto> RejectAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        DecideProducerAccountingIntentDto decision, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        RequireReason(decision);
        return await _events.RejectAsync(accountingEventId,
            new ReleaseAccountingEventDto { Request = await BuildRequestAsync(intent, cancellationToken), Reason = decision.Reason }, cancellationToken);
    }

    public async Task<AccountingEventDto> ExecuteApprovedAsync(Guid accountingEventId, ProducerAccountingIntentDto intent,
        IFinanceProducerExecutionParticipant participant, CancellationToken cancellationToken = default)
    {
        RequireEnabled();
        ArgumentNullException.ThrowIfNull(participant);
        ValidateIntent(intent);
        if (!string.Equals(CanonicalParticipant(intent.ParticipantIdentity), CanonicalParticipant(participant.ParticipantIdentity), StringComparison.Ordinal))
            throw new InvalidOperationException("ACCOUNTING_EVENT_PARTICIPANT_CONFLICT: execution participant differs from prepared intent evidence.");
        var approved = await _events.GetAsync(accountingEventId, cancellationToken);
        if (approved.ProducerDecisionStatus != "Approved"
            || approved.Status is not ("PendingApproval" or "Failed" or "Posted"))
            throw new InvalidOperationException("ACCOUNTING_EVENT_APPROVAL_REQUIRED: execution requires durable independent approval.");
        var request = await BuildRequestAsync(intent, cancellationToken);
        return await _events.ExecuteApprovedAsync(accountingEventId,
            new ReleaseAccountingEventDto { Request = request, Reason = approved.ProducerDecisionReason ?? string.Empty }, participant, cancellationToken);
    }

    private async Task<CreateAccountingEventDto> BuildRequestAsync(ProducerAccountingIntentDto intent, CancellationToken ct)
    {
        ValidateIntent(intent);
        var posting = intent.PostingRequest;
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
        return new CreateAccountingEventDto
        {
            AccountingEventId = intent.AccountingEventId,
            EventKind = intent.EventKind,
            SupersedesAccountingEventId = intent.SupersedesAccountingEventId,
            CorrectsAccountingEventId = intent.CorrectsAccountingEventId,
            ReversesAccountingEventId = intent.ReversesAccountingEventId,
            SelectionIdempotencyKey = intent.IdempotencyKey,
            ExpectedCalculationInputHash = preview.CalculationInputHash,
            ExpectedSelectionFingerprint = preview.SelectionFingerprint,
            ProducerParticipantIdentity = CanonicalParticipant(intent.ParticipantIdentity),
            PostingRequest = ToFinancePosting(posting)
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

    private static void RequireReason(DecideProducerAccountingIntentDto decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (string.IsNullOrWhiteSpace(decision.Reason))
            throw new InvalidOperationException("A governed approval or rejection reason is required.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ParticipantIdentityPattern();
}
