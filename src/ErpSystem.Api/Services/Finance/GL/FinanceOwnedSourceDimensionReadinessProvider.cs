using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Tenant-scoped readiness evidence for the three Finance-owned PR 2 routes. It deliberately
/// reads only persisted source provenance and current Finance rules; external producers that use
/// the same lifecycle services do not acquire the manual Finance route identity.
/// </summary>
public sealed class FinanceOwnedSourceDimensionReadinessProvider : IFinanceDimensionReadinessProvider
{
    private readonly ApplicationDbContext _db;

    public FinanceOwnedSourceDimensionReadinessProvider(
        ApplicationDbContext db,
        FinanceDimensionRouteId routeId)
    {
        _db = db;
        RouteId = routeId;
        if (routeId is not FinanceDimensionRouteId.FinanceApVendorInvoice
            and not FinanceDimensionRouteId.FinanceApSupplierDebitNote
            and not FinanceDimensionRouteId.FinanceArCustomerInvoice)
            throw new ArgumentOutOfRangeException(nameof(routeId), routeId, "The route is not owned by the Finance PR 2 adapter.");
    }

    public FinanceDimensionRouteId RouteId { get; }

    public async Task<FinanceDimensionReadinessContribution> EvaluateAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken = default)
    {
        if (route.Id != RouteId)
            throw new InvalidOperationException("The readiness provider was invoked for a different compiled route.");

        var documents = RouteId switch
        {
            FinanceDimensionRouteId.FinanceApVendorInvoice =>
                await LoadVendorInvoicesAsync(tenantId, cancellationToken),
            FinanceDimensionRouteId.FinanceApSupplierDebitNote =>
                await LoadSupplierDebitNotesAsync(tenantId, cancellationToken),
            FinanceDimensionRouteId.FinanceArCustomerInvoice =>
                await LoadCustomerInvoicesAsync(tenantId, cancellationToken),
            _ => []
        };
        var documentIds = documents.Select(item => item.Id).ToArray();
        var assignments = documentIds.Length == 0
            ? []
            : await _db.FinanceSourceDimensionAssignments.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.RouteId == RouteId
                    && documentIds.Contains(item.SourceDocumentId) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var assignmentGroups = assignments.GroupBy(item => item.SourceDocumentId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var setIds = assignments.Where(item => item.FinanceDimensionSetId.HasValue)
            .Select(item => item.FinanceDimensionSetId!.Value).Distinct().ToArray();
        var sets = setIds.Length == 0
            ? new Dictionary<Guid, FinanceDimensionSet>()
            : await _db.FinanceDimensionSets.AsNoTracking().Include(item => item.Items)
                .Where(item => item.TenantId == tenantId && setIds.Contains(item.Id) && !item.IsDeleted)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var activeReservations = new Dictionary<Guid, string>();
        if (RouteId == FinanceDimensionRouteId.FinanceApVendorInvoice)
        {
            var reservationDocumentIds = await _db.FinanceBudgetReservations.AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted
                    && item.SourceDocumentType == "VendorInvoice"
                    && documentIds.Contains(item.SourceDocumentId)
                    && item.Status == "Reserved")
                .Select(item => item.SourceDocumentId)
                .ToListAsync(cancellationToken);
            activeReservations = reservationDocumentIds.GroupBy(id => id)
                .ToDictionary(group => group.Key, group => $"Reserved ({group.Count()})");
        }

        var blockers = new List<FinanceDimensionReadinessBlockerDto>();
        foreach (var document in documents)
        {
            assignmentGroups.TryGetValue(document.Id, out var documentAssignments);
            documentAssignments ??= [];
            var header = documentAssignments.SingleOrDefault(item => !item.SourceLineId.HasValue);
            if (header is null)
            {
                blockers.Add(Blocker(
                    document,
                    "UNCERTIFIED_LEGACY_DOCUMENT",
                    "The document has no trusted Finance source-route provenance.",
                    "Recall or reopen the document through the manual Finance route and capture its line evidence.",
                    "Trusted origin is missing."));
                continue;
            }

            var lineAssignments = documentAssignments.Where(item => item.SourceLineId.HasValue)
                .ToDictionary(item => item.SourceLineId!.Value);
            foreach (var line in document.Lines)
            {
                if (!line.AccountId.HasValue)
                {
                    blockers.Add(Blocker(
                        document,
                        "SOURCE_ACCOUNT_UNRESOLVED",
                        $"Source line {line.Id} has no server-resolved economic account.",
                        "Correct the draft line/account configuration.",
                        "Account resolution failed."));
                    continue;
                }
                if (!lineAssignments.TryGetValue(line.Id, out var assignment))
                {
                    blockers.Add(Blocker(
                        document,
                        "SOURCE_LINE_NOT_ADAPTED",
                        $"Source line {line.Id} has no persisted Finance dimension capture evidence.",
                        "Open and save the document through the certified Finance route.",
                        "Line-level provenance is missing."));
                    continue;
                }
                if (document.RequiresFrozenEvidence && !assignment.EvidenceFrozenAt.HasValue)
                {
                    blockers.Add(Blocker(
                        document,
                        "SUBMISSION_EVIDENCE_NOT_FROZEN",
                        $"Source line {line.Id} is in {document.LifecycleState} without frozen submission evidence.",
                        "Recall the document, revalidate dimensions and begin a new approval workflow.",
                        "Submission snapshot is missing."));
                }

                sets.TryGetValue(assignment.FinanceDimensionSetId ?? Guid.Empty, out var set);
                var issues = await ValidateAccountRulesAsync(
                    tenantId, route, document.DocumentDate, line.AccountId.Value, set, cancellationToken);
                foreach (var issue in issues)
                    blockers.Add(Blocker(
                        document,
                        issue.Code,
                        $"Source line {line.Id}: {issue.Message}",
                        "Correct the draft line dimensions and revalidate the document.",
                        issue.Message,
                        issue.FixedRuleDrift));
            }

            if (document.BudgetRelevant
                && !string.Equals(header.BudgetEvidenceStatus, "Current", StringComparison.Ordinal))
                blockers.Add(Blocker(
                    document,
                    "STALE_BUDGET_EVIDENCE",
                    "The document has budget-bearing lines without current dimension-aware budget evidence.",
                    "Run the explicit Finance budget refresh after correcting dimensions.",
                    "Budget evidence is stale or not evaluated.",
                    staleBudgetEvidence: true,
                    activeReservationState: activeReservations.GetValueOrDefault(document.Id, "None")));
        }

        var documentWatermark = documents.Count == 0 ? 0L : documents.Max(item => item.VersionTicks);
        var assignmentWatermark = assignments.Count == 0
            ? 0L
            : assignments.Max(item => (item.UpdatedAt ?? item.CreatedAt).Ticks);
        var watermark = $"{route.ContractVersion}:{documents.Count}:{assignments.Count}:{documentWatermark}:{assignmentWatermark}:{blockers.Count}";
        return new FinanceDimensionReadinessContribution(watermark, blockers);
    }

    private async Task<IReadOnlyList<ReadinessDocument>> LoadVendorInvoicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var invoices = await _db.VendorInvoices.AsNoTracking()
            .Include(item => item.Supplier)
            .Include(item => item.LineItems.Where(line => !line.IsDeleted))
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && (item.Status == VendorInvoiceStatus.Draft
                    || item.Status == VendorInvoiceStatus.Rejected
                    || item.Status == VendorInvoiceStatus.PendingApproval
                    || item.Status == VendorInvoiceStatus.Approved))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(item => item.Id).ToArray();
        var grvInvoices = await _db.FinancePurchaseOrderReceipts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.VendorInvoiceId.HasValue && invoiceIds.Contains(item.VendorInvoiceId.Value))
            .Select(item => item.VendorInvoiceId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var fixedAssetIds = invoices.SelectMany(item => item.LineItems)
            .Where(line => line.FixedAssetId.HasValue).Select(line => line.FixedAssetId!.Value)
            .Distinct().ToArray();
        var assetAccounts = fixedAssetIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await _db.FixedAssets.AsNoTracking()
                .Where(item => item.TenantId == tenantId && fixedAssetIds.Contains(item.Id) && !item.IsDeleted)
                .Select(item => new { item.Id, item.Category.AssetAccountId })
                .ToDictionaryAsync(item => item.Id, item => item.AssetAccountId, cancellationToken);

        return invoices.Select(invoice =>
        {
            var clearsGrv = grvInvoices.Contains(invoice.Id)
                || (invoice.PurchaseOrderId.HasValue
                    && invoice.AcceptedSupplyKind == ProcurementAcceptedSupplyKind.GoodsReceiptInspection
                    && invoice.AcceptedSupplySourceId == invoice.PurchaseOrderId);
            var lines = invoice.LineItems.OrderBy(line => line.CreatedAt).ThenBy(line => line.Id)
                .Select(line => new ReadinessLine(line.Id, ResolveVendorAccount(
                    invoice, line, settings, clearsGrv, assetAccounts)))
                .ToArray();
            return new ReadinessDocument(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.Status.ToString(),
                invoice.InvoiceDate,
                $"/finance/ap/invoices/{invoice.Id}",
                invoice.Status is VendorInvoiceStatus.PendingApproval or VendorInvoiceStatus.Approved,
                invoice.LineItems.Any(line => line.BudgetEntryId.HasValue),
                (invoice.UpdatedAt ?? invoice.CreatedAt).Ticks,
                lines);
        }).ToArray();
    }

    private static Guid? ResolveVendorAccount(
        VendorInvoice invoice,
        VendorInvoiceLineItem line,
        FinanceSettings? settings,
        bool clearsGrv,
        IReadOnlyDictionary<Guid, Guid> assetAccounts)
    {
        if (invoice.IsOpeningBalance) return settings?.MigrationClearingAccountId;
        if (clearsGrv) return settings?.ControlAccountGRVAccrualId;
        if (line.FixedAssetId.HasValue && assetAccounts.TryGetValue(line.FixedAssetId.Value, out var assetAccount))
            return assetAccount;
        var isInventory = string.Equals(line.LineItemType, "Inventory", StringComparison.OrdinalIgnoreCase)
            || string.Equals(line.LineItemType, "Product", StringComparison.OrdinalIgnoreCase);
        return isInventory
            ? line.GLAccountId ?? settings?.ControlAccountInventoryId
            : line.GLAccountId ?? invoice.ExpenseAccountId ?? invoice.Supplier?.DefaultExpenseAccountId;
    }

    private async Task<IReadOnlyList<ReadinessDocument>> LoadSupplierDebitNotesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var notes = await _db.SupplierDebitNotes.AsNoTracking()
            .Include(item => item.LineItems.Where(line => !line.IsDeleted))
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && (item.Status == SupplierDebitNoteStatus.Draft
                    || item.Status == SupplierDebitNoteStatus.Rejected
                    || item.Status == SupplierDebitNoteStatus.PendingApproval
                    || item.Status == SupplierDebitNoteStatus.Approved))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return notes.Select(note => new ReadinessDocument(
            note.Id,
            note.DebitNoteNumber,
            note.Status.ToString(),
            note.DebitNoteDate,
            $"/finance/ap/supplier-debit-notes/{note.Id}",
            note.Status is SupplierDebitNoteStatus.PendingApproval or SupplierDebitNoteStatus.Approved,
            false,
            (note.UpdatedAt ?? note.CreatedAt).Ticks,
            note.LineItems.OrderBy(line => line.CreatedAt).ThenBy(line => line.Id)
                .Select(line => new ReadinessLine(line.Id, line.ResolvedCreditAccountId)).ToArray()))
            .ToArray();
    }

    private async Task<IReadOnlyList<ReadinessDocument>> LoadCustomerInvoicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var invoices = await _db.Invoices.AsNoTracking()
            .Include(item => item.LineItems.Where(line => !line.IsDeleted))
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && (item.Status == InvoiceStatus.Draft
                    || item.Status == InvoiceStatus.Rejected
                    || item.Status == InvoiceStatus.PendingApproval
                    || item.Status == InvoiceStatus.Approved))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return invoices.Select(invoice => new ReadinessDocument(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status.ToString(),
            invoice.InvoiceDate,
            $"/finance/ar/invoices/{invoice.Id}",
            invoice.Status is InvoiceStatus.PendingApproval or InvoiceStatus.Approved,
            false,
            (invoice.UpdatedAt ?? invoice.CreatedAt).Ticks,
            invoice.LineItems.OrderBy(line => line.CreatedAt).ThenBy(line => line.Id)
                .Select(line => new ReadinessLine(
                    line.Id,
                    invoice.IsOpeningBalance ? settings?.MigrationClearingAccountId : line.GLAccountId))
                .ToArray()))
            .ToArray();
    }

    private async Task<IReadOnlyList<RuleIssue>> ValidateAccountRulesAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        DateTime documentDate,
        Guid accountId,
        FinanceDimensionSet? set,
        CancellationToken cancellationToken)
    {
        var accountExists = await _db.Accounts.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.Id == accountId && !item.IsDeleted
            && item.Status == AccountStatus.Active,
            cancellationToken);
        if (!accountExists)
            return [new RuleIssue("SOURCE_ACCOUNT_INVALID", "The source account is inactive, deleted or cross-tenant.", false)];

        var candidates = await _db.FinanceDimensionAccountRules.AsNoTracking()
            .Include(item => item.FinanceDimensionDefinition)
            .Include(item => item.DefaultDimensionValue)
            .Where(item => item.TenantId == tenantId && item.AccountId == accountId
                && !item.IsDeleted && item.IsActive
                && item.EffectiveDate.Date <= documentDate.Date
                && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= documentDate.Date)
                && (!item.RouteId.HasValue || item.RouteId == route.Id)
                && (item.SourceModule == null || item.SourceModule == route.PostingSourceModule)
                && (item.SourceDocumentType == null || item.SourceDocumentType == route.DocumentType)
                && (item.PostingAction == null || item.PostingAction == "Post"))
            .ToListAsync(cancellationToken);
        var selected = new List<FinanceDimensionAccountRule>();
        var issues = new List<RuleIssue>();
        foreach (var group in candidates.GroupBy(item => item.FinanceDimensionDefinitionId))
        {
            var ordered = group.OrderByDescending(item => Specificity(item, route.Id))
                .ThenByDescending(item => item.RuleVersion).ToList();
            if (ordered.Count > 1 && Specificity(ordered[0], route.Id) == Specificity(ordered[1], route.Id))
            {
                issues.Add(new RuleIssue(
                    "AMBIGUOUS_DIMENSION_RULE",
                    $"Dimension {ordered[0].FinanceDimensionDefinition.Code} has ambiguous effective account rules.",
                    true));
                continue;
            }
            selected.Add(ordered[0]);
        }

        var values = set?.Items.ToDictionary(
            item => item.FinanceDimensionDefinitionId,
            item => item.FinanceDimensionValueId) ?? new Dictionary<Guid, Guid>();
        foreach (var rule in selected)
        {
            var hasValue = values.TryGetValue(rule.FinanceDimensionDefinitionId, out var valueId);
            switch (rule.RuleType)
            {
                case "Prohibited" when hasValue:
                    issues.Add(new RuleIssue(
                        "PROHIBITED_DIMENSION",
                        $"Dimension {rule.FinanceDimensionDefinition.Code} is prohibited for the source account.",
                        false));
                    break;
                case "Fixed" when !rule.DefaultDimensionValueId.HasValue:
                    issues.Add(new RuleIssue(
                        "INVALID_FIXED_RULE",
                        $"Fixed dimension {rule.FinanceDimensionDefinition.Code} has no effective value.",
                        true));
                    break;
                case "Fixed" when !hasValue || valueId != rule.DefaultDimensionValueId:
                    issues.Add(new RuleIssue(
                        "FIXED_RULE_DRIFT",
                        $"Dimension {rule.FinanceDimensionDefinition.Code} does not match the current fixed value {rule.DefaultDimensionValue?.Code}.",
                        true));
                    break;
                case "Required" when !hasValue && !rule.DefaultDimensionValueId.HasValue:
                    issues.Add(new RuleIssue(
                        "REQUIRED_DIMENSION_MISSING",
                        $"Dimension {rule.FinanceDimensionDefinition.Code} is required for the source account.",
                        false));
                    break;
            }
        }

        if (set is not null)
        {
            var definitionIds = set.Items.Select(item => item.FinanceDimensionDefinitionId).ToArray();
            var valueIds = set.Items.Select(item => item.FinanceDimensionValueId).ToArray();
            var validDefinitions = await _db.FinanceDimensionDefinitions.AsNoTracking().CountAsync(item =>
                item.TenantId == tenantId && definitionIds.Contains(item.Id)
                && !item.IsDeleted && item.IsActive,
                cancellationToken);
            var validValues = await _db.FinanceDimensionValues.AsNoTracking().CountAsync(item =>
                item.TenantId == tenantId && valueIds.Contains(item.Id)
                && !item.IsDeleted && item.IsActive
                && item.EffectiveDate.Date <= documentDate.Date
                && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= documentDate.Date),
                cancellationToken);
            if (validDefinitions != definitionIds.Distinct().Count()
                || validValues != valueIds.Distinct().Count())
                issues.Add(new RuleIssue(
                    "DIMENSION_MASTER_INVALID",
                    "One or more captured dimensions are inactive, expired, missing or cross-tenant.",
                    false));
        }
        return issues;
    }

    private static int Specificity(FinanceDimensionAccountRule rule, FinanceDimensionRouteId routeId)
        => (rule.RouteId.HasValue && rule.RouteId == routeId ? 8 : 0)
           + (rule.SourceModule is null ? 0 : 4)
           + (rule.SourceDocumentType is null ? 0 : 2)
           + (rule.PostingAction is null ? 0 : 1);

    private FinanceDimensionReadinessBlockerDto Blocker(
        ReadinessDocument document,
        string code,
        string message,
        string remediation,
        string dimensionIssue,
        bool fixedRuleDrift = false,
        bool staleBudgetEvidence = false,
        string? activeReservationState = null) => new()
    {
        Code = code,
        Message = message,
        LifecycleState = document.LifecycleState,
        DocumentId = document.Id,
        DocumentReference = document.Reference,
        DocumentLink = document.Link,
        RemediationStatus = remediation,
        DimensionIssue = dimensionIssue,
        FixedRuleDrift = fixedRuleDrift,
        StaleBudgetEvidence = staleBudgetEvidence,
        ActiveReservationState = activeReservationState,
        Details = $"Route={RouteId}; Date={document.DocumentDate:yyyy-MM-dd}"
    };

    private sealed record ReadinessLine(Guid Id, Guid? AccountId);
    private sealed record ReadinessDocument(
        Guid Id,
        string Reference,
        string LifecycleState,
        DateTime DocumentDate,
        string Link,
        bool RequiresFrozenEvidence,
        bool BudgetRelevant,
        long VersionTicks,
        IReadOnlyList<ReadinessLine> Lines);
    private sealed record RuleIssue(string Code, string Message, bool FixedRuleDrift);
}
