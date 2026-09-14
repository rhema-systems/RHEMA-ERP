using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Promotion evidence for the Finance-owned reconciliation-adjustment route. Historical direct
/// cash rows are deliberately outside this route; only documents carrying the compiled route's
/// persisted provenance participate in its readiness decision.
/// </summary>
public sealed class FinanceReconciliationAdjustmentDimensionReadinessProvider
    : IFinanceDimensionReadinessProvider
{
    private readonly ApplicationDbContext _db;

    public FinanceReconciliationAdjustmentDimensionReadinessProvider(ApplicationDbContext db)
    {
        _db = db;
    }

    public FinanceDimensionRouteId RouteId =>
        FinanceDimensionRouteId.FinanceBankReconciliationAdjustment;

    public async Task<FinanceDimensionReadinessContribution> EvaluateAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken = default)
    {
        if (route.Id != RouteId)
            throw new InvalidOperationException(
                "The reconciliation-adjustment readiness provider was invoked for another route.");

        var assignments = await _db.FinanceSourceDimensionAssignments.AsNoTracking()
            .Include(item => item.FinanceDimensionSnapshot)!
                .ThenInclude(snapshot => snapshot!.Items)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.RouteId == RouteId)
            .ToListAsync(cancellationToken);
        var headers = assignments.Where(item => !item.SourceLineId.HasValue).ToArray();
        var sourceIds = headers.Select(item => item.SourceDocumentId).Distinct().ToArray();
        var transactions = sourceIds.Length == 0
            ? new Dictionary<Guid, CashTransaction>()
            : await _db.Set<CashTransaction>().AsNoTracking()
                .Include(item => item.BankAccount)
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && sourceIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var postingLines = sourceIds.Length == 0
            ? []
            : await _db.AccountTransactions.AsNoTracking()
                .Include(item => item.FinanceDimensionSnapshot)!
                    .ThenInclude(snapshot => snapshot!.Items)
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && item.SourceDocumentType == route.DocumentType
                    && item.SourceDocumentId.HasValue
                    && sourceIds.Contains(item.SourceDocumentId.Value))
                .ToListAsync(cancellationToken);
        var baseCurrency = await _db.FinanceSettings.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken) ?? "GHS";

        var blockers = new List<FinanceDimensionReadinessBlockerDto>();
        foreach (var header in headers)
        {
            if (!transactions.TryGetValue(header.SourceDocumentId, out var transaction))
            {
                blockers.Add(Blocker(
                    header.SourceDocumentId,
                    null,
                    "SOURCE_DOCUMENT_MISSING",
                    "The trusted reconciliation-adjustment provenance has no tenant-scoped cash transaction.",
                    "Investigate the orphan source assignment before promotion."));
                continue;
            }

            var reference = string.IsNullOrWhiteSpace(transaction.ReferenceNumber)
                ? transaction.TransactionNumber
                : transaction.ReferenceNumber;
            if (!transaction.ReconciliationId.HasValue)
                blockers.Add(Blocker(
                    transaction.Id, reference, "RECONCILIATION_LINEAGE_MISSING",
                    "The adjustment is not linked to its originating bank reconciliation.",
                    "Restore the governed reconciliation lineage."));
            if (!transaction.IsPosted
                || transaction.ApprovalStatus != CashTransactionApprovalStatus.Posted
                || !transaction.JournalEntryId.HasValue)
                blockers.Add(Blocker(
                    transaction.Id, reference, "ADJUSTMENT_NOT_POSTED",
                    "The reconciliation adjustment did not reach a complete posted state.",
                    "Resolve or cancel the incomplete adjustment through Finance governance."));
            if (!string.Equals(transaction.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase)
                && (!transaction.ExchangeRateId.HasValue
                    || string.IsNullOrWhiteSpace(transaction.ExchangeRateSource)
                    || !transaction.ExchangeRateDate.HasValue))
                blockers.Add(Blocker(
                    transaction.Id, reference, "EXCHANGE_RATE_EVIDENCE_MISSING",
                    "A foreign-currency reconciliation adjustment lacks approved rate evidence.",
                    "Correct the adjustment through a governed Finance correction."));

            var expectedLines = new[]
            {
                new ExpectedLine(
                    FinanceReconciliationDimensionIdentity.BankLine(transaction.Id),
                    transaction.BankAccount.GLAccountId,
                    "bank"),
                new ExpectedLine(
                    FinanceReconciliationDimensionIdentity.OffsetLine(transaction.Id),
                    transaction.GLAccountId,
                    "offset")
            };
            var sourceLines = assignments.Where(item =>
                    item.SourceDocumentId == transaction.Id && item.SourceLineId.HasValue)
                .ToDictionary(item => item.SourceLineId!.Value);
            var journalLines = postingLines.Where(item =>
                    item.SourceDocumentId == transaction.Id && item.SourceDocumentLineId.HasValue)
                .ToDictionary(item => item.SourceDocumentLineId!.Value);
            foreach (var expected in expectedLines)
            {
                if (!expected.AccountId.HasValue)
                {
                    blockers.Add(Blocker(
                        transaction.Id, reference, "SOURCE_ACCOUNT_UNRESOLVED",
                        $"The reconciliation adjustment {expected.Label} line has no trusted GL account.",
                        "Correct the Finance bank/account configuration."));
                    continue;
                }
                if (!sourceLines.TryGetValue(expected.SourceLineId, out var sourceLine))
                {
                    blockers.Add(Blocker(
                        transaction.Id, reference, "SOURCE_LINE_NOT_ADAPTED",
                        $"The reconciliation adjustment {expected.Label} line has no persisted dimension assignment.",
                        "Recreate the adjustment through the dimension-aware Finance route."));
                    continue;
                }
                if (!sourceLine.EvidenceFrozenAt.HasValue
                    || (sourceLine.FinanceDimensionSetId.HasValue
                        && !sourceLine.FinanceDimensionSnapshotId.HasValue))
                    blockers.Add(Blocker(
                        transaction.Id, reference, "SOURCE_EVIDENCE_NOT_FROZEN",
                        $"The reconciliation adjustment {expected.Label} line lacks immutable source evidence.",
                        "Investigate the incomplete posting evidence before promotion."));
                journalLines.TryGetValue(expected.SourceLineId, out var journalLine);
                var mismatch = PostingEvidenceMismatch(
                    expected.AccountId.Value,
                    sourceLine,
                    journalLine);
                if (mismatch is not null)
                    blockers.Add(Blocker(
                        transaction.Id, reference, "POSTING_EVIDENCE_MISMATCH",
                        $"The posted {expected.Label} line does not match its frozen source dimension evidence: {mismatch}",
                        "Run Finance posting-evidence diagnostics and correct through governance."));
            }
        }

        var assignmentWatermark = assignments.Count == 0
            ? 0L
            : assignments.Max(item => (item.UpdatedAt ?? item.CreatedAt).Ticks);
        var transactionWatermark = transactions.Count == 0
            ? 0L
            : transactions.Values.Max(item => (item.UpdatedAt ?? item.CreatedAt).Ticks);
        var watermark =
            $"{route.ContractVersion}:{headers.Length}:{assignments.Count}:{postingLines.Count}:{assignmentWatermark}:{transactionWatermark}:{blockers.Count}";
        return new FinanceDimensionReadinessContribution(watermark, blockers);
    }

    private static FinanceDimensionReadinessBlockerDto Blocker(
        Guid documentId,
        string? reference,
        string code,
        string message,
        string remediation) => new()
    {
        Code = code,
        Message = message,
        LifecycleState = "Posted",
        DocumentId = documentId,
        DocumentReference = reference,
        DocumentLink = $"/finance/cash/transactions/{documentId}",
        RemediationStatus = remediation,
        DimensionIssue = message,
        Details = $"Route={FinanceDimensionRouteId.FinanceBankReconciliationAdjustment}"
    };

    private static bool SnapshotsMatch(
        FinanceDimensionSnapshot? source,
        FinanceDimensionSnapshot? posting)
    {
        if (source is null || posting is null)
            return source is null && posting is null;
        if (source.FinanceDimensionSetId != posting.FinanceDimensionSetId
            || !string.Equals(
                source.CombinationHashSnapshot,
                posting.CombinationHashSnapshot,
                StringComparison.Ordinal))
            return false;

        var sourceItems = source.Items.OrderBy(item => item.DimensionCodeSnapshot, StringComparer.Ordinal)
            .Select(SnapshotItemEvidence)
            .ToArray();
        var postingItems = posting.Items.OrderBy(item => item.DimensionCodeSnapshot, StringComparer.Ordinal)
            .Select(SnapshotItemEvidence)
            .ToArray();
        return sourceItems.SequenceEqual(postingItems, StringComparer.Ordinal);
    }

    private static string? PostingEvidenceMismatch(
        Guid expectedAccountId,
        FinanceSourceDimensionAssignment source,
        AccountTransaction? posting)
    {
        if (posting is null)
            return "the exact source-line posting is missing";
        if (posting.AccountId != expectedAccountId)
            return "the posting account differs from the trusted source account";
        if (posting.FinanceDimensionSetId != source.FinanceDimensionSetId)
            return "the canonical dimension set differs";
        if (!SnapshotsMatch(source.FinanceDimensionSnapshot, posting.FinanceDimensionSnapshot))
            return "the immutable dimension or rule snapshot differs";
        return null;
    }

    private static string SnapshotItemEvidence(FinanceDimensionSnapshotItem item) => string.Join(
        "|",
        item.FinanceDimensionDefinitionId,
        item.FinanceDimensionValueId,
        item.DimensionCodeSnapshot,
        item.DimensionNameSnapshot,
        item.DimensionValueCodeSnapshot,
        item.DimensionValueNameSnapshot,
        item.FinanceDimensionAccountRuleId,
        item.RuleFamilyIdSnapshot,
        item.RuleVersionSnapshot,
        item.RuleTypeSnapshot,
        item.RuleEffectiveDateSnapshot,
        item.RuleExpiryDateSnapshot);

    private sealed record ExpectedLine(Guid SourceLineId, Guid? AccountId, string Label);
}
