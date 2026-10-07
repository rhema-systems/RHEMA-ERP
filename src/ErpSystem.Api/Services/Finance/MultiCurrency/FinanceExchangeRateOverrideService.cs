using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.MultiCurrency;

public sealed class FinanceExchangeRateOverrideService : IFinanceExchangeRateOverrideService
{
    public const string WorkflowEntityType = "FinanceExchangeRateOverride";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService _workflow;
    private readonly IFinanceAuditService? _audit;

    public FinanceExchangeRateOverrideService(ApplicationDbContext db, ICurrentUserService currentUser,
        IWorkflowService workflow, IFinanceAuditService? audit = null)
    {
        _db = db;
        _currentUser = currentUser;
        _workflow = workflow;
        _audit = audit;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var value) ? value : Guid.Empty;

    public async Task<IReadOnlyList<FinanceExchangeRateOverrideRequestDto>> GetForSourceAsync(
        string sourceDocumentType, Guid sourceDocumentId, CancellationToken cancellationToken = default)
    {
        var canonicalType = CanonicalSourceType(sourceDocumentType);
        return await _db.FinanceExchangeRateOverrideRequests.AsNoTracking()
            .Where(x => x.TenantId == TenantId && !x.IsDeleted
                && x.SourceDocumentType == canonicalType && x.SourceDocumentId == sourceDocumentId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<FinanceExchangeRateOverrideRequestDto> RequestAsync(
        FinanceExchangeRateOverrideCommandDto command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (UserId == Guid.Empty) throw new InvalidOperationException("An authenticated tenant user is required.");
        var reason = command.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new ArgumentException("An exchange-rate override reason of at least 10 characters is required.", nameof(command));
        if (command.RequestedRate <= 0m)
            throw new ArgumentException("The requested exchange rate must be greater than zero.", nameof(command));

        var tenantId = TenantId;
        var sourceType = CanonicalSourceType(command.SourceDocumentType);
        var currency = NormalizeCurrency(command.TransactionCurrencyCode);
        var source = await ResolveSourceSnapshotAsync(tenantId, sourceType, command.SourceDocumentId, currency, cancellationToken);
        if (!source.CanRequest)
            throw new InvalidOperationException(source.IneligibleReason ?? "This transaction is not eligible for an exchange-rate override.");
        if (source.GovernedExchangeRateId != command.GovernedExchangeRateId)
            throw new InvalidOperationException("The governed exchange-rate record changed. Refresh the transaction before requesting an override.");
        var governed = await _db.ExchangeRates.AsNoTracking().FirstOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == command.GovernedExchangeRateId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The governed exchange-rate record was not found.");
        if (governed.ApprovalStatus is not RateApprovalStatus.Approved and not RateApprovalStatus.AutoApproved)
            throw new InvalidOperationException("Only an approved governed exchange rate can be overridden.");
        if (NormalizeCurrency(governed.BaseCurrencyCode) != source.FunctionalCurrencyCode
            || NormalizeCurrency(governed.TargetCurrencyCode) != currency)
            throw new InvalidOperationException("The governed exchange-rate record does not match the transaction currency pair.");
        if (Math.Abs(source.GovernedRate - command.RequestedRate) < 0.000001m)
            throw new InvalidOperationException("The requested rate matches the governed rate; no override is required.");

        var active = await _db.FinanceExchangeRateOverrideRequests.Where(x =>
                x.TenantId == tenantId && !x.IsDeleted && x.SourceDocumentType == sourceType
                && x.SourceDocumentId == command.SourceDocumentId && x.TransactionCurrencyCode == currency
                && (x.Status == FinanceExchangeRateOverrideStatuses.PendingApproval
                    || x.Status == FinanceExchangeRateOverrideStatuses.Approved))
            .OrderByDescending(x => x.RequestedAtUtc).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var staleActive = active.Where(x => x.SourceSnapshotHash != source.Hash
            || x.GovernedExchangeRateId != source.GovernedExchangeRateId
            || x.GovernedRate != source.GovernedRate).ToList();
        foreach (var stale in staleActive)
        {
            if (stale.Status == FinanceExchangeRateOverrideStatuses.PendingApproval)
            {
                var cancelled = await _workflow.CancelWorkflowAsync(WorkflowEntityType, stale.Id,
                    "The source transaction or governed rate changed.");
                if (!cancelled.Success)
                    throw new InvalidOperationException(cancelled.Message ?? "The stale override workflow could not be cancelled.");
            }
            stale.Status = FinanceExchangeRateOverrideStatuses.Superseded;
            stale.SupersededAtUtc = now;
            stale.SupersessionReason = "The source transaction or governed rate changed.";
            stale.UpdatedAt = now;
            stale.LastModifiedById = UserId;
        }
        if (staleActive.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var stale in staleActive)
                await AuditAsync(FinanceAuditEvents.TransactionExchangeRateOverrideSuperseded,
                    stale, stale.SupersessionReason, cancellationToken);
            active = active.Except(staleActive).ToList();
        }
        var duplicate = active.FirstOrDefault(x => x.SourceSnapshotHash == source.Hash
            && x.GovernedExchangeRateId == command.GovernedExchangeRateId
            && x.RequestedRate == command.RequestedRate && x.Reason == reason);
        if (duplicate != null) return Map(duplicate);
        if (active.Count > 0)
            throw new InvalidOperationException("An active exchange-rate override already exists for this transaction and currency. It must be rejected or consumed before another request is submitted.");

        var request = new FinanceExchangeRateOverrideRequest
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SourceDocumentType = sourceType,
            SourceDocumentId = command.SourceDocumentId, SourceDocumentReference = source.Reference,
            TransactionCurrencyCode = currency, FunctionalCurrencyCode = source.FunctionalCurrencyCode,
            GovernedExchangeRateId = governed.Id, GovernedRate = source.GovernedRate,
            GovernedRateSource = governed.RateSource, GovernedRateEffectiveDate = governed.EffectiveDate,
            GovernedRateType = governed.RateType.ToString(), GovernedQuoteSide = governed.QuoteSide.ToString(),
            RequestedRate = decimal.Round(command.RequestedRate, 6, MidpointRounding.AwayFromZero),
            Reason = reason, SourceSnapshotHash = source.Hash,
            Status = FinanceExchangeRateOverrideStatuses.PendingApproval,
            RequestedByUserId = UserId, RequestedAtUtc = now, CreatedAt = now,
            CreatedById = UserId, CreatedBy = _currentUser.UserName
        };
        _db.FinanceExchangeRateOverrideRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            var workflow = await _workflow.StartApprovalWorkflowAsync(WorkflowEntityType, request.Id);
            if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue || workflow.WorkflowInstanceId == Guid.Empty)
                throw new InvalidOperationException(workflow.Message ?? "The transaction exchange-rate override workflow could not be started.");
            request.WorkflowInstanceId = workflow.WorkflowInstanceId.Value;
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(FinanceAuditEvents.TransactionExchangeRateOverrideRequested, request, null, cancellationToken);
            return Map(request);
        }
        catch
        {
            _db.FinanceExchangeRateOverrideRequests.Remove(request);
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task ApplyWorkflowOutcomeAsync(Guid requestId, bool approved, Guid actorUserId,
        string? reason, CancellationToken cancellationToken = default)
    {
        var request = await _db.FinanceExchangeRateOverrideRequests.FirstOrDefaultAsync(x =>
            x.TenantId == TenantId && x.Id == requestId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Transaction exchange-rate override request was not found.");
        if (request.Status != FinanceExchangeRateOverrideStatuses.PendingApproval) return;
        if (actorUserId == request.RequestedByUserId)
            throw new InvalidOperationException("The override requester cannot approve or reject their own request.");
        if (!request.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Transaction exchange-rate override workflow evidence is missing.");
        var workflow = await _db.WorkflowInstances.AsNoTracking().FirstOrDefaultAsync(x =>
            x.TenantId == TenantId && x.Id == request.WorkflowInstanceId.Value && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Transaction exchange-rate override workflow evidence was not found.");
        var now = DateTime.UtcNow;
        if (approved)
        {
            if (workflow.Status != WorkflowInstanceStatus.Completed)
                throw new InvalidOperationException("The transaction exchange-rate override workflow is not complete.");
            var source = await ResolveSourceSnapshotAsync(TenantId, request.SourceDocumentType,
                request.SourceDocumentId, request.TransactionCurrencyCode, cancellationToken);
            if (source.Hash != request.SourceSnapshotHash || source.GovernedExchangeRateId != request.GovernedExchangeRateId
                || source.GovernedRate != request.GovernedRate)
            {
                request.Status = FinanceExchangeRateOverrideStatuses.Superseded;
                request.SupersededAtUtc = now;
                request.SupersessionReason = "The source transaction or governed rate changed before approval completed.";
                request.UpdatedAt = now;
                request.LastModifiedById = actorUserId;
                await _db.SaveChangesAsync(cancellationToken);
                await AuditAsync(FinanceAuditEvents.TransactionExchangeRateOverrideSuperseded, request,
                    request.SupersessionReason, cancellationToken);
                throw new InvalidOperationException("The source transaction changed after the override was requested. Submit a new request.");
            }
            request.Status = FinanceExchangeRateOverrideStatuses.Approved;
            request.ApprovedByUserId = actorUserId;
            request.ApprovedAtUtc = now;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A rejection reason is required.", nameof(reason));
            request.Status = FinanceExchangeRateOverrideStatuses.Rejected;
            request.RejectedByUserId = actorUserId;
            request.RejectedAtUtc = now;
            request.RejectionReason = reason.Trim();
        }
        request.UpdatedAt = now;
        request.LastModifiedById = actorUserId;
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(approved ? FinanceAuditEvents.TransactionExchangeRateOverrideApproved
            : FinanceAuditEvents.TransactionExchangeRateOverrideRejected, request, reason, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ApplyApprovedOverridesForPostingAsync(
        Guid tenantId, FinancePostingCommandDto posting, CancellationToken cancellationToken = default)
    {
        if (!TryCanonicalSourceType(posting.SourceDocumentType, out var sourceType))
            return Array.Empty<Guid>();

        if (posting.ExchangeRateOverrideRequestId.HasValue || posting.ExchangeRateOverrideWorkflowInstanceId.HasValue
            || posting.ExchangeRateOverrideApprovedByUserId.HasValue || posting.ExchangeRateOverrideApprovedAt.HasValue)
            throw new InvalidOperationException("Exchange-rate override approval evidence is resolved by Finance and cannot be supplied by the caller.");
        var functionalCurrency = NormalizeCurrency(posting.FunctionalCurrencyCode);
        var currencies = posting.Lines.Where(x => !string.IsNullOrWhiteSpace(x.TransactionCurrency) && x.ExchangeRateId.HasValue)
            .Select(x => NormalizeCurrency(x.TransactionCurrency!)).Where(x => x != functionalCurrency)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (currencies.Length == 0) return Array.Empty<Guid>();

        var candidates = await _db.FinanceExchangeRateOverrideRequests.Include(x => x.WorkflowInstance)
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.SourceDocumentType == sourceType
                && x.SourceDocumentId == posting.SourceDocumentId && x.Status == FinanceExchangeRateOverrideStatuses.Approved
                && x.ConsumedByPostingEventId == null && currencies.Contains(x.TransactionCurrencyCode))
            .OrderByDescending(x => x.ApprovedAtUtc).ToListAsync(cancellationToken);
        var consumed = new List<Guid>();
        foreach (var currency in currencies)
        {
            var request = candidates.FirstOrDefault(x => x.TransactionCurrencyCode == currency);
            if (request == null) continue;
            if (request.WorkflowInstance?.Status != WorkflowInstanceStatus.Completed
                || !request.ApprovedByUserId.HasValue || !request.ApprovedAtUtc.HasValue
                || request.ApprovedByUserId == request.RequestedByUserId)
                throw new InvalidOperationException("The transaction exchange-rate override lacks valid independent workflow approval evidence.");
            var source = await ResolveSourceSnapshotAsync(tenantId, sourceType, posting.SourceDocumentId, currency, cancellationToken);
            if (source.Hash != request.SourceSnapshotHash || source.GovernedExchangeRateId != request.GovernedExchangeRateId
                || source.GovernedRate != request.GovernedRate)
            {
                request.Status = FinanceExchangeRateOverrideStatuses.Superseded;
                request.SupersededAtUtc = DateTime.UtcNow;
                request.SupersessionReason = "The source transaction or governed rate changed before posting.";
                request.UpdatedAt = request.SupersededAtUtc;
                await _db.SaveChangesAsync(cancellationToken);
                await AuditAsync(FinanceAuditEvents.TransactionExchangeRateOverridePostingBlocked,
                    request, request.SupersessionReason, cancellationToken);
                throw new InvalidOperationException("The approved transaction exchange-rate override is stale because the source transaction or governed rate changed. Submit a new request.");
            }

            var lines = posting.Lines.Where(x => NormalizeCurrency(x.TransactionCurrency ?? functionalCurrency) == currency
                && x.ExchangeRateId == request.GovernedExchangeRateId).ToList();
            if (lines.Count == 0)
                throw new InvalidOperationException("The approved exchange-rate override does not match any posting line.");
            foreach (var line in lines)
            {
                line.ExchangeRate = request.RequestedRate;
                line.ExchangeRateSource = $"Transaction override ({request.Id:N})";
                if (line.TransactionDebitAmount.HasValue)
                    line.DebitAmount = decimal.Round(line.TransactionDebitAmount.Value * request.RequestedRate, 2, MidpointRounding.AwayFromZero);
                if (line.TransactionCreditAmount.HasValue)
                    line.CreditAmount = decimal.Round(line.TransactionCreditAmount.Value * request.RequestedRate, 2, MidpointRounding.AwayFromZero);
            }
            if (posting.ExchangeRateOverrideRequestId.HasValue)
                throw new InvalidOperationException("A posting containing multiple independently overridden currencies is not yet supported.");
            posting.ExchangeRateOverrideRequestId = request.Id;
            posting.ExchangeRateOverrideWorkflowInstanceId = request.WorkflowInstanceId;
            posting.ExchangeRateOverrideReason = request.Reason;
            posting.ExchangeRateOverrideApprovedByUserId = request.ApprovedByUserId;
            posting.ExchangeRateOverrideApprovedAt = request.ApprovedAtUtc;
            consumed.Add(request.Id);
        }
        return consumed;
    }

    public async Task ConsumeAsync(Guid tenantId, IReadOnlyList<Guid> requestIds, Guid postingEventId,
        CancellationToken cancellationToken = default)
    {
        if (requestIds.Count == 0) return;
        var rows = await _db.FinanceExchangeRateOverrideRequests.Where(x =>
            x.TenantId == tenantId && requestIds.Contains(x.Id) && !x.IsDeleted).ToListAsync(cancellationToken);
        if (rows.Count != requestIds.Distinct().Count()
            || rows.Any(x => x.Status != FinanceExchangeRateOverrideStatuses.Approved || x.ConsumedByPostingEventId.HasValue))
            throw new InvalidOperationException("Transaction exchange-rate override evidence is missing, stale, or already consumed.");
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.Status = FinanceExchangeRateOverrideStatuses.Consumed;
            row.ConsumedByPostingEventId = postingEventId;
            row.ConsumedAtUtc = now;
            row.UpdatedAt = now;
            row.LastModifiedById = UserId == Guid.Empty ? null : UserId;
            await AuditAsync(FinanceAuditEvents.TransactionExchangeRateOverrideConsumed, row, null,
                cancellationToken, postingEventId);
        }
    }

    private async Task<SourceSnapshot> ResolveSourceSnapshotAsync(
        Guid tenantId, string sourceType, Guid sourceId, string currency, CancellationToken token)
    {
        var functional = NormalizeCurrency(await _db.FinanceSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted).Select(x => x.BaseCurrency)
            .FirstOrDefaultAsync(token) ?? throw new InvalidOperationException("Finance settings are missing."));
        if (currency == functional)
            throw new InvalidOperationException("A rate override is not applicable to functional-currency transactions.");
        string reference, material, ineligible;
        Guid? rateId;
        decimal rate;
        bool eligible;
        switch (sourceType)
        {
            case "VendorInvoice":
            {
                var x = await _db.VendorInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("AP invoice was not found.");
                EnsureCurrency(x.CurrencyCode, currency); reference = x.InvoiceNumber; rateId = x.ExchangeRateId; rate = x.ExchangeRate;
                eligible = x.Status == VendorInvoiceStatus.Draft; ineligible = "Only a Draft AP invoice is eligible.";
                material = $"{x.InvoiceDate:O}|{x.DueDate:O}|{x.TotalAmount}|{x.BaseCurrencyAmount}|{x.BusinessPartnerId:N}|{x.PurchaseOrderId}";
                break;
            }
            case "VendorPayment":
            {
                var x = await _db.Set<VendorPayment>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("AP payment was not found.");
                EnsureCurrency(x.CurrencyCode, currency); reference = x.PaymentNumber; rateId = x.ExchangeRateId; rate = x.ExchangeRate;
                eligible = x.Status == VendorPaymentStatus.Draft; ineligible = "Only a Draft AP payment is eligible.";
                material = $"{x.PaymentDate:O}|{x.TotalAmount}|{x.BusinessPartnerId:N}|{x.BankAccountId}";
                break;
            }
            case "CustomerInvoice":
            {
                var x = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("AR invoice was not found.");
                EnsureCurrency(x.CurrencyCode, currency); reference = x.InvoiceNumber; rateId = x.ExchangeRateId; rate = x.ExchangeRate;
                eligible = x.Status == InvoiceStatus.Draft; ineligible = "Only a Draft AR invoice is eligible.";
                material = $"{x.InvoiceDate:O}|{x.DueDate:O}|{x.TotalAmount}|{x.BaseCurrencyAmount}|{x.BusinessPartnerId:N}";
                break;
            }
            case "CustomerPayment":
            {
                var x = await _db.Set<CustomerPayment>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("AR receipt was not found.");
                EnsureCurrency(x.CurrencyCode, currency); reference = x.PaymentNumber; rateId = x.ExchangeRateId; rate = x.ExchangeRate;
                eligible = x.JournalEntryId == null && x.Status == "Pending"; ineligible = "Only an unposted Pending AR receipt is eligible.";
                material = $"{x.PaymentDate:O}|{x.TotalAmount}|{x.BusinessPartnerId:N}|{x.BankAccountId}|{x.LiquidityAccountId}";
                break;
            }
            case "CashTransaction":
            {
                var x = await _db.Set<CashTransaction>().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("Cash/bank transaction was not found.");
                EnsureCurrency(x.Currency, currency); reference = x.TransactionNumber; rateId = x.ExchangeRateId; rate = x.ExchangeRate ?? 0m;
                eligible = !x.IsPosted && x.ApprovalStatus == CashTransactionApprovalStatus.Captured;
                ineligible = "Only a captured, unposted cash/bank transaction is eligible.";
                material = $"{x.TransactionDate:O}|{x.Amount}|{x.BaseAmount}|{x.BankAccountId}|{x.ToBankAccountId}|{x.TransactionType}";
                break;
            }
            case "ManualJournalEntry":
            {
                var x = await _db.JournalEntries.AsNoTracking().Include(x => x.Transactions)
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("Manual journal was not found.");
                var lines = x.Transactions.Where(line => NormalizeCurrency(line.TransactionCurrency ?? functional) == currency)
                    .OrderBy(line => line.Id).ToList();
                if (lines.Count == 0) throw new InvalidOperationException("The journal does not contain the requested currency.");
                rateId = RequireSingle(lines.Select(line => line.ExchangeRateId), "The journal currency must use one governed rate record.");
                rate = RequireSingle(lines.Select(line => line.ExchangeRate ?? 0m), "The journal currency must use one governed rate value.");
                reference = x.JournalEntryNumber; eligible = x.PostingStatus == "Draft";
                ineligible = "Only a Draft manual journal is eligible.";
                material = string.Join("|", lines.Select(line =>
                    $"{line.Id:N}:{line.AccountId:N}:{line.TransactionDebitAmount}:{line.TransactionCreditAmount}:{line.DebitAmount}:{line.CreditAmount}"));
                break;
            }
            case "OpeningBalanceBatch":
            {
                var x = await _db.OpeningBalanceBatches.AsNoTracking().Include(x => x.Lines)
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("Opening-balance batch was not found.");
                var lines = x.Lines.Where(line => NormalizeCurrency(line.TransactionCurrencyCode) == currency)
                    .OrderBy(line => line.Id).ToList();
                if (lines.Count == 0) throw new InvalidOperationException("The opening-balance batch does not contain the requested currency.");
                rateId = RequireSingle(lines.Select(line => line.ExchangeRateId), "The opening-balance currency must use one governed rate record.");
                var governed = await _db.ExchangeRates.AsNoTracking().FirstOrDefaultAsync(r =>
                    r.TenantId == tenantId && r.Id == rateId && !r.IsDeleted, token)
                    ?? throw new InvalidOperationException("The opening-balance governed rate was not found.");
                rate = governed.Rate; reference = x.BatchNumber;
                eligible = x.SubmittedAt == null && x.PostedAt == null;
                ineligible = "Only an unsubmitted opening-balance batch is eligible.";
                material = string.Join("|", lines.Select(line =>
                    $"{line.Id:N}:{line.AccountId:N}:{line.TransactionDebitAmount}:{line.TransactionCreditAmount}"));
                break;
            }
            case "AssetDisposal":
            {
                var x = await _db.AssetDisposals.AsNoTracking().FirstOrDefaultAsync(x =>
                    x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted, token)
                    ?? throw new InvalidOperationException("Asset disposal was not found.");
                EnsureCurrency(x.ProceedsCurrencyCode, currency); reference = x.ReferenceNumber ?? x.Id.ToString("N");
                rateId = x.ProceedsExchangeRateId; rate = x.ProceedsExchangeRateValue;
                eligible = x.Status == AssetDisposalStatus.Draft; ineligible = "Only a Draft asset disposal is eligible.";
                material = $"{x.DisposalDate:O}|{x.SaleProceeds}|{x.DisposalCost}|{x.NetProceeds}|{x.FixedAssetId:N}|{x.ProceedsAccountId}";
                break;
            }
            default:
                throw new InvalidOperationException($"Transaction type '{sourceType}' is not eligible for a manual exchange-rate override.");
        }
        if (!rateId.HasValue || rateId == Guid.Empty || rate <= 0m)
            throw new InvalidOperationException("The transaction does not retain a governed exchange-rate snapshot.");
        var canonical = $"{sourceType}|{sourceId:N}|{currency}|{functional}|{rateId:N}|{rate.ToString("0.000000", CultureInfo.InvariantCulture)}|{material}";
        return new SourceSnapshot(reference, functional, rateId.Value, rate, Hash(canonical), eligible,
            eligible ? null : ineligible);
    }

    private async Task AuditAsync(string eventType, FinanceExchangeRateOverrideRequest request,
        string? comment, CancellationToken token, Guid? postingEventId = null)
    {
        if (_audit == null) return;
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType, TenantId = request.TenantId, SourceModule = "FINANCE",
            SourceDocumentType = request.SourceDocumentType, SourceDocumentId = request.SourceDocumentId,
            PostingEventId = postingEventId, WorkflowInstanceId = request.WorkflowInstanceId,
            Resource = "Finance.ExchangeRateOverride", ResourceId = request.Id.ToString(),
            Reason = request.Reason, Comment = comment,
            Context = new
            {
                overrideRequestId = request.Id, request.TransactionCurrencyCode,
                request.FunctionalCurrencyCode, request.GovernedExchangeRateId,
                request.GovernedRate, request.RequestedRate, request.SourceSnapshotHash,
                request.RequestedByUserId, request.ApprovedByUserId, request.ApprovedAtUtc, request.Status
            }
        }, token);
    }

    private static FinanceExchangeRateOverrideRequestDto Map(FinanceExchangeRateOverrideRequest x) => new()
    {
        Id = x.Id, SourceDocumentType = x.SourceDocumentType, SourceDocumentId = x.SourceDocumentId,
        SourceDocumentReference = x.SourceDocumentReference, TransactionCurrencyCode = x.TransactionCurrencyCode,
        FunctionalCurrencyCode = x.FunctionalCurrencyCode, GovernedExchangeRateId = x.GovernedExchangeRateId,
        GovernedRate = x.GovernedRate, GovernedRateSource = x.GovernedRateSource,
        GovernedRateEffectiveDate = x.GovernedRateEffectiveDate, GovernedRateType = x.GovernedRateType,
        GovernedQuoteSide = x.GovernedQuoteSide, RequestedRate = x.RequestedRate, Reason = x.Reason,
        Status = x.Status, WorkflowInstanceId = x.WorkflowInstanceId, RequestedByUserId = x.RequestedByUserId,
        RequestedAtUtc = x.RequestedAtUtc, ApprovedByUserId = x.ApprovedByUserId, ApprovedAtUtc = x.ApprovedAtUtc,
        RejectedByUserId = x.RejectedByUserId, RejectedAtUtc = x.RejectedAtUtc,
        RejectionReason = x.RejectionReason, ConsumedByPostingEventId = x.ConsumedByPostingEventId,
        ConsumedAtUtc = x.ConsumedAtUtc
    };

    private static string CanonicalSourceType(string? value) => NormalizeKey(value) switch
    {
        "VENDORINVOICE" => "VendorInvoice",
        "VENDORPAYMENT" => "VendorPayment",
        "CUSTOMERINVOICE" or "INVOICE" => "CustomerInvoice",
        "CUSTOMERPAYMENT" or "ARPAYMENT" => "CustomerPayment",
        "CASHTRANSACTION" or "BANKTRANSACTION" => "CashTransaction",
        "MANUALJOURNALENTRY" or "JOURNALENTRY" => "ManualJournalEntry",
        "OPENINGBALANCEBATCH" => "OpeningBalanceBatch",
        "ASSETDISPOSAL" or "FIXEDASSETDISPOSAL" => "AssetDisposal",
        _ => throw new InvalidOperationException("This Finance transaction type is not eligible for a manual exchange-rate override.")
    };
    private static bool TryCanonicalSourceType(string? value, out string sourceType)
    {
        try { sourceType = CanonicalSourceType(value); return true; }
        catch (InvalidOperationException) { sourceType = string.Empty; return false; }
    }
    private static string NormalizeKey(string? value) => new((value ?? string.Empty)
        .Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant();
    private static void EnsureCurrency(string actual, string expected)
    {
        if (NormalizeCurrency(actual) != expected)
            throw new InvalidOperationException("The requested currency does not match the source transaction.");
    }
    private static T RequireSingle<T>(IEnumerable<T> values, string message)
    {
        var distinct = values.Distinct().ToArray();
        if (distinct.Length != 1 || distinct[0] is null) throw new InvalidOperationException(message);
        return distinct[0]!;
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record SourceSnapshot(string Reference, string FunctionalCurrencyCode,
        Guid GovernedExchangeRateId, decimal GovernedRate, string Hash, bool CanRequest, string? IneligibleReason);
}
