using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private static readonly FinancePostingProducerContext LeaseApProducer =
        new(FinanceDimensionRouteId.FinanceApVendorInvoice);

    public Task<VendorInvoiceDto> CreateLeaseInstallmentDraftAsync(
        Guid leaseId, Guid scheduleLineId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || CurrentUserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated Finance invoice maker is required.");
        if (leaseId == Guid.Empty || scheduleLineId == Guid.Empty)
            throw new ArgumentException("Select a lease schedule period.");
        if (_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Start lease payable preparation outside another transaction.");

        return _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"lease-ap:{TenantId:N}:{scheduleLineId:N}", cancellationToken);

                var lease = await _unitOfWork.Repository<LeaseContract>().GetQueryable(item =>
                        item.Id == leaseId && item.TenantId == TenantId && !item.IsDeleted)
                    .Include(item => item.ScheduleLines)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException("Lease contract not found in this tenant.");
                var source = lease.ScheduleLines.SingleOrDefault(item =>
                    item.Id == scheduleLineId && item.TenantId == TenantId && !item.IsDeleted)
                    ?? throw new KeyNotFoundException("Lease schedule line not found in this tenant.");
                var retained = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(item =>
                        item.TenantId == TenantId && item.LeaseScheduleLineId == source.Id && !item.IsDeleted)
                    .Include(item => item.LineItems)
                    .Include(item => item.PaymentAllocations)
                    .ToListAsync(cancellationToken);
                var active = retained.Where(item => item.Status != VendorInvoiceStatus.Voided).ToArray();
                if (active.Length > 1)
                    throw new InvalidOperationException(
                        "LEASE_AP_SOURCE_AMBIGUOUS: multiple active supplier invoices claim this lease period.");
                VendorInvoice? terminal = null;
                if (retained.Count > 0)
                {
                    var byId = retained.ToDictionary(item => item.Id);
                    if (retained.Any(item => item.ReplacesLeaseVendorInvoiceId.HasValue &&
                            !byId.ContainsKey(item.ReplacesLeaseVendorInvoiceId.Value)) ||
                        retained.Where(item => item.ReplacesLeaseVendorInvoiceId.HasValue)
                            .GroupBy(item => item.ReplacesLeaseVendorInvoiceId!.Value)
                            .Any(group => group.Count() != 1))
                        throw new InvalidOperationException(
                            "LEASE_AP_REPLACEMENT_AMBIGUOUS: retained invoice lineage is incomplete or forked.");
                    var roots = retained.Where(item => !item.ReplacesLeaseVendorInvoiceId.HasValue).ToArray();
                    if (roots.Length != 1)
                        throw new InvalidOperationException(
                            "LEASE_AP_REPLACEMENT_AMBIGUOUS: one retained root invoice is required.");
                    var visited = new HashSet<Guid>();
                    terminal = roots[0];
                    while (true)
                    {
                        if (!visited.Add(terminal.Id))
                            throw new InvalidOperationException(
                                "LEASE_AP_REPLACEMENT_AMBIGUOUS: retained invoice lineage contains a cycle.");
                        var successor = retained.SingleOrDefault(item =>
                            item.ReplacesLeaseVendorInvoiceId == terminal.Id);
                        if (successor == null) break;
                        terminal = successor;
                    }
                    if (visited.Count != retained.Count)
                        throw new InvalidOperationException(
                            "LEASE_AP_REPLACEMENT_AMBIGUOUS: retained invoice lineage is disconnected or cyclic.");
                }
                if (active.Length == 1)
                {
                    var existing = active[0];
                    if (terminal?.Id != existing.Id)
                        throw new InvalidOperationException(
                            "LEASE_AP_REPLACEMENT_AMBIGUOUS: the sole active invoice must be the unique terminal successor.");
                    await ValidateLeaseSourceAsync(existing, cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return await GetByIdAsync(existing.Id, LeaseApProducer, cancellationToken)
                        ?? throw new InvalidOperationException("The linked supplier invoice is unavailable.");
                }
                VendorInvoice? replaces = null;
                if (retained.Count > 0)
                {
                    if (terminal == null || terminal.Status != VendorInvoiceStatus.Voided)
                        throw new InvalidOperationException(
                            "LEASE_AP_REPLACEMENT_AMBIGUOUS: the unique terminal invoice must be voided.");
                    var activeSettlement = RoundMoney(terminal.PaymentAllocations.Where(item => !item.IsDeleted).Sum(
                        item => item.AllocatedAmount + item.DiscountAmount + item.WithholdingTaxAmount));
                    if (Math.Abs(activeSettlement) > 0.01m || Math.Abs(RoundMoney(terminal.PaidAmount)) > 0.01m)
                        throw new InvalidOperationException(
                            "LEASE_AP_REPLACEMENT_SETTLEMENT_EXISTS: reverse or reconcile all settlement evidence before replacing the voided instalment.");
                    await ValidateLeaseSourceAsync(terminal, cancellationToken);
                    replaces = terminal;
                }
                if (lease.Status != LeaseStatus.Active)
                    throw new InvalidOperationException("Only an active lease can prepare a supplier payable.");
                if (source.IsPosted)
                    throw new InvalidOperationException(
                        "This period is already accounting-posted but its canonical AP link is missing. Finance must reconcile the historical source before continuing.");
                if (lease.ScheduleLines.Any(item => item.PeriodNumber < source.PeriodNumber && !item.IsPosted))
                    throw new InvalidOperationException(
                        "The prior lease period must be accounting-posted through AP before the next payable is prepared.");

                await EnsureLeaseAuthorityAsync(lease, source, cancellationToken);
                var stalePeriodPosting = await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                        item.SourceDocumentType == "LeasePeriodPosting" && item.SourceDocumentId == source.Id &&
                        item.PostingStatus == "Posted")
                    .AnyAsync(cancellationToken);
                if (stalePeriodPosting)
                    throw new InvalidOperationException(
                        "A standalone lease-period journal already exists for this schedule line. Reconcile that historical journal before creating an AP payable; Finance will not post the period twice.");

                var lines = new List<VendorInvoiceLineItemCreateDto>();
                if (source.PrincipalReduction > 0m)
                    lines.Add(LeaseLine(LeaseInvoiceComponent.Principal,
                        lease.LeaseLiabilityAccountId!.Value, source.PrincipalReduction,
                        $"Lease principal — {lease.ContractNumber} P{source.PeriodNumber}"));
                if (source.InterestExpense > 0m)
                    lines.Add(LeaseLine(LeaseInvoiceComponent.Interest,
                        lease.InterestExpenseAccountId!.Value, source.InterestExpense,
                        $"Lease interest — {lease.ContractNumber} P{source.PeriodNumber}"));
                if (lines.Count == 0 || RoundMoney(lines.Sum(item => item.UnitPrice)) != RoundMoney(source.PaymentAmount))
                    throw new InvalidOperationException("Lease schedule principal and interest do not reconcile to the period payment.");

                var draft = await CreateCoreAsync(new VendorInvoiceCreateDto
                {
                    LeaseScheduleLineId = source.Id,
                    ReplacesLeaseVendorInvoiceId = replaces?.Id,
                    LeaseAccountingBookId = lease.AccountingBookId,
                    LeaseAccountingBookCode = lease.AccountingBookCode,
                    LeaseFunctionalCurrencyCode = lease.FunctionalCurrencyCode,
                    BusinessPartnerId = lease.LessorId,
                    InvoiceDate = source.PeriodDate.Date,
                    DueDate = source.PeriodDate.Date,
                    CurrencyCode = lease.FunctionalCurrencyCode!,
                    ExchangeRate = 1m,
                    MatchingType = InvoiceMatchingType.None,
                    Reference = $"{lease.ContractNumber}-P{source.PeriodNumber}",
                    Notes = "Generated from the governed lease schedule. Review tax and withholding, then use the normal AP maker/checker workflow.",
                    LineItems = lines
                }, LeaseApProducer, cancellationToken, deferSupplierWithholdingDecision: true);
                var persistedDraft = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(item =>
                        item.Id == draft.Id && item.TenantId == TenantId && !item.IsDeleted)
                    .SingleAsync(cancellationToken);
                // Lease tax/WHT classification is Finance-owned. A partner profile that is not
                // ordinarily subject to WHT is not authority to classify this instalment.
                persistedDraft.WithholdingDecisionPending = true;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                draft.WithholdingDecisionPending = true;

                await _unitOfWork.CommitAsync(cancellationToken);
                return draft;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
    }

    private static VendorInvoiceLineItemCreateDto LeaseLine(
        LeaseInvoiceComponent component, Guid accountId, decimal amount, string description) => new()
    {
        LeaseComponent = component,
        LineItemType = "Expense",
        GLAccountId = accountId,
        Description = description,
        Quantity = 1m,
        UnitPrice = amount,
        Unit = "Instalment",
        TaxTreatment = TaxTreatment.PendingReview
    };

    private static bool HasLeaseSource(VendorInvoiceCreateDto dto) =>
        dto.LeaseScheduleLineId.HasValue || dto.ReplacesLeaseVendorInvoiceId.HasValue ||
        dto.LeaseAccountingBookId.HasValue ||
        !string.IsNullOrWhiteSpace(dto.LeaseAccountingBookCode) ||
        !string.IsNullOrWhiteSpace(dto.LeaseFunctionalCurrencyCode) ||
        dto.LineItems.Any(item => item.LeaseComponent.HasValue);

    private static void ValidateLeaseCreate(VendorInvoiceCreateDto dto)
    {
        if (!HasLeaseSource(dto)) return;
        if (!dto.LeaseScheduleLineId.HasValue || !dto.LeaseAccountingBookId.HasValue ||
            string.IsNullOrWhiteSpace(dto.LeaseAccountingBookCode) ||
            string.IsNullOrWhiteSpace(dto.LeaseFunctionalCurrencyCode) ||
            dto.IsOpeningBalance || dto.PurchaseOrderId.HasValue || dto.AcceptedSupplyKind.HasValue ||
            dto.AcceptedSupplySourceId.HasValue || dto.AutoInvoiceRequestId.HasValue ||
            dto.EstateAcquisitionId.HasValue || dto.LineItems.Any(item => item.LandedCostItemId.HasValue ||
                !item.LeaseComponent.HasValue || item.FixedAssetId.HasValue || item.PurchaseOrderItemId.HasValue ||
                item.BudgetEntryId.HasValue))
            throw new InvalidOperationException("Invalid lease supplier-invoice source.");
        var components = dto.LineItems.Select(item => item.LeaseComponent!.Value).ToArray();
        if (components.Distinct().Count() != components.Length ||
            components.Any(item => !Enum.IsDefined(item)))
            throw new InvalidOperationException("Lease invoice components are missing or duplicated.");
    }

    private static void ValidateLeaseInvoiceUpdate(VendorInvoice invoice, VendorInvoiceUpdateDto dto)
    {
        if (!invoice.LeaseScheduleLineId.HasValue) return;
        var lines = invoice.LineItems.Where(item => !item.IsDeleted).ToDictionary(item => item.Id);
        if (dto.IsOpeningBalance || dto.PurchaseOrderId.HasValue || dto.AcceptedSupplyKind.HasValue ||
            dto.AcceptedSupplySourceId.HasValue || dto.InvoiceDate.Date != invoice.InvoiceDate.Date ||
            !string.Equals(dto.CurrencyCode, invoice.CurrencyCode, StringComparison.Ordinal) ||
            dto.ExchangeRate != 1m || dto.ExchangeRateId.HasValue || dto.MatchingType != InvoiceMatchingType.None ||
            dto.Reference != invoice.Reference || dto.ExpenseAccountId.HasValue || dto.ApAccountId.HasValue ||
            dto.LineItems.Count != lines.Count || dto.LineItems.Select(item => item.Id).Distinct().Count() != lines.Count ||
            dto.LineItems.Any(item => !item.Id.HasValue || !lines.TryGetValue(item.Id.Value, out var original) ||
                !original.LeaseComponent.HasValue || item.LineItemType != original.LineItemType ||
                item.Description != original.Description || item.Quantity != original.Quantity ||
                item.UnitPrice != original.UnitPrice || item.DiscountPercentage != 0m ||
                item.GLAccountId != original.GLAccountId || item.FixedAssetId.HasValue ||
                item.PurchaseOrderItemId.HasValue || item.BudgetEntryId.HasValue || item.LandedCostItemId.HasValue ||
                item.Unit != original.Unit))
            throw new InvalidOperationException(
                "Lease schedule amounts, principal/interest accounts, date, currency, book and source lineage cannot be changed in AP. Review only supplier reference, payment terms, tax and withholding.");
    }

    private async Task ValidateLeaseSourceAsync(
        VendorInvoice invoice,
        CancellationToken ct,
        bool requireOpenPostingPeriod = false)
    {
        if (!invoice.LeaseScheduleLineId.HasValue) return;
        var source = await _unitOfWork.Repository<LeaseScheduleLine>().GetQueryable(item =>
                item.Id == invoice.LeaseScheduleLineId && item.TenantId == TenantId && !item.IsDeleted)
            .Include(item => item.LeaseContract)
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The lease schedule source is unavailable in this tenant.");
        var lease = source.LeaseContract;
        await ValidateLeaseAuthorityAsync(lease, source.PeriodDate, ct, requireOpenPostingPeriod);
        if (invoice.ReplacesLeaseVendorInvoiceId.HasValue)
        {
            var predecessor = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(item =>
                    item.Id == invoice.ReplacesLeaseVendorInvoiceId && item.TenantId == TenantId && !item.IsDeleted)
                .SingleOrDefaultAsync(ct);
            if (predecessor == null || predecessor.Id == invoice.Id ||
                predecessor.LeaseScheduleLineId != invoice.LeaseScheduleLineId ||
                predecessor.Status != VendorInvoiceStatus.Voided)
                throw new InvalidOperationException(
                    "The lease AP replacement predecessor is unavailable, cross-source or not voided.");
            var successorCount = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(item =>
                    item.TenantId == TenantId && !item.IsDeleted &&
                    item.ReplacesLeaseVendorInvoiceId == predecessor.Id)
                .CountAsync(ct);
            if (successorCount > 1)
                throw new InvalidOperationException("The lease AP replacement lineage is forked.");
        }
        var lines = invoice.LineItems.Where(item => !item.IsDeleted).ToList();
        var principal = lines.SingleOrDefault(item => item.LeaseComponent == LeaseInvoiceComponent.Principal);
        var interest = lines.SingleOrDefault(item => item.LeaseComponent == LeaseInvoiceComponent.Interest);
        if (lines.Any(item => !item.LeaseComponent.HasValue) ||
            lines.Count(item => item.LeaseComponent == LeaseInvoiceComponent.Principal) != (source.PrincipalReduction > 0m ? 1 : 0) ||
            lines.Count(item => item.LeaseComponent == LeaseInvoiceComponent.Interest) != (source.InterestExpense > 0m ? 1 : 0) ||
            invoice.BusinessPartnerId != lease.LessorId || invoice.PurchaseOrderId.HasValue || invoice.IsOpeningBalance ||
            invoice.AcceptedSupplyKind.HasValue || invoice.AutoInvoiceRequestId.HasValue || invoice.EstateAcquisitionId.HasValue ||
            invoice.LeaseAccountingBookId != lease.AccountingBookId ||
            invoice.LeaseAccountingBookCode != lease.AccountingBookCode ||
            invoice.LeaseFunctionalCurrencyCode != lease.FunctionalCurrencyCode ||
            invoice.CurrencyCode != lease.FunctionalCurrencyCode || invoice.ExchangeRate != 1m || invoice.ExchangeRateId.HasValue ||
            invoice.InvoiceDate.Date != source.PeriodDate.Date || invoice.MatchingType != InvoiceMatchingType.None ||
            principal != null && !LeaseLineMatches(principal, lease.LeaseLiabilityAccountId, source.PrincipalReduction) ||
            interest != null && !LeaseLineMatches(interest, lease.InterestExpenseAccountId, source.InterestExpense) ||
            RoundMoney(invoice.SubTotal) != RoundMoney(source.PaymentAmount) || invoice.TaxAmount != 0m ||
            RoundMoney(invoice.TotalAmount) != RoundMoney(source.PaymentAmount))
            throw new InvalidOperationException(
                "The lease AP draft no longer matches its immutable schedule, account, book or currency authority. Reconcile the source before continuing.");
    }

    private static bool LeaseLineMatches(VendorInvoiceLineItem line, Guid? accountId, decimal amount) =>
        accountId.HasValue && line.GLAccountId == accountId && line.LineItemType == "Expense" &&
        line.Quantity == 1m && line.UnitPrice == amount && line.DiscountAmount == 0m &&
        line.DiscountPercentage == 0m && !line.FixedAssetId.HasValue && !line.PurchaseOrderItemId.HasValue &&
        !line.BudgetEntryId.HasValue && !line.LandedCostItemId.HasValue;

    private static void EnsureLeaseTaxReviewed(VendorInvoice invoice)
    {
        if (!invoice.LeaseScheduleLineId.HasValue) return;
        if (invoice.WithholdingDecisionPending)
            throw new InvalidOperationException(
                "AP_WHT_CONFIRMATION_REQUIRED: Finance must record the lease withholding decision before submission, approval or posting.");
        if (invoice.LineItems.Any(item => !item.IsDeleted &&
            (!Enum.IsDefined(item.TaxTreatment) || item.TaxTreatment == TaxTreatment.PendingReview ||
             item.TaxTreatment == TaxTreatment.Standard && !item.TaxGroupId.HasValue)))
            throw new InvalidOperationException(
                "Complete the tax treatment for every lease instalment line in AP before submission, approval or posting.");
        if (invoice.TaxAmount != 0m)
            throw new InvalidOperationException(
                "The governed lease schedule contains no separate tax amount. Reconcile a taxable lease instalment at the lease source before posting AP.");
    }

    private async Task<Dictionary<Guid, (Guid AccountId, string Tag)>> ResolveLeasePostingAccountsAsync(
        VendorInvoice invoice, CancellationToken ct)
    {
        if (!invoice.LeaseScheduleLineId.HasValue) return [];
        await ValidateLeaseSourceAsync(invoice, ct, requireOpenPostingPeriod: true);
        EnsureLeaseTaxReviewed(invoice);
        return invoice.LineItems.Where(item => !item.IsDeleted).ToDictionary(item => item.Id, item =>
            item.LeaseComponent == LeaseInvoiceComponent.Principal
                ? (item.GLAccountId!.Value, "AP-LEASE-PRINCIPAL")
                : (item.GLAccountId!.Value, "AP-LEASE-INTEREST"));
    }

    private async Task<bool> DeleteLeaseInvoiceDraftAsync(VendorInvoice invoice, CancellationToken ct)
    {
        if (!invoice.LeaseScheduleLineId.HasValue) return false;
        var id = invoice.Id;
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var owns = !_unitOfWork.HasActiveTransaction;
            if (owns) await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"lease-ap:{TenantId:N}:{invoice.LeaseScheduleLineId:N}", ct);
                var current = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(item =>
                    item.Id == id && item.TenantId == TenantId && !item.IsDeleted).SingleAsync(ct);
                if (current.JournalEntryId.HasValue || current.Status != VendorInvoiceStatus.Draft)
                    throw new InvalidOperationException("Only an unposted lease invoice draft can be deleted.");
                await _unitOfWork.Repository<VendorInvoice>().DeleteAsync(current);
                await _unitOfWork.SaveChangesAsync(ct);
                if (owns) await _unitOfWork.CommitAsync(ct);
            }
            catch
            {
                if (owns && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(ct);
                if (owns) _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, ct);
        return true;
    }

    private async Task ApplyLeaseAccountingStatusAsync(VendorInvoice invoice, bool posted, CancellationToken ct)
    {
        if (!invoice.LeaseScheduleLineId.HasValue) return;
        var line = await _unitOfWork.Repository<LeaseScheduleLine>().GetQueryable(item =>
                item.Id == invoice.LeaseScheduleLineId && item.TenantId == TenantId && !item.IsDeleted)
            .Include(item => item.LeaseContract).ThenInclude(item => item.ScheduleLines)
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The lease schedule source is unavailable in this tenant.");
        line.IsPosted = posted;
        line.UpdatedAt = DateTime.UtcNow;
        line.UpdatedBy = UserName;
        line.LeaseContract.Status = line.LeaseContract.ScheduleLines.Where(item => !item.IsDeleted)
            .All(item => item.Id == line.Id ? posted : item.IsPosted)
            ? LeaseStatus.Completed : LeaseStatus.Active;
        line.LeaseContract.UpdatedAt = DateTime.UtcNow;
        line.LeaseContract.UpdatedBy = UserName;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ValidateLeaseAuthorityAsync(
        LeaseContract lease,
        DateTime postingDate,
        CancellationToken ct,
        bool requireOpenPostingPeriod = false)
    {
        if (!lease.AccountingBookId.HasValue || string.IsNullOrWhiteSpace(lease.AccountingBookCode) ||
            string.IsNullOrWhiteSpace(lease.FunctionalCurrencyCode) || !lease.RecognitionPostingEventId.HasValue ||
            !lease.RecognitionJournalEntryId.HasValue || !lease.RouAssetAccountId.HasValue ||
            !lease.LeaseLiabilityAccountId.HasValue || !lease.InterestExpenseAccountId.HasValue)
            throw new InvalidOperationException(
                "LEASE_AUTHORITY_REMEDIATION_REQUIRED: immutable recognition book, currency and account evidence is incomplete.");
        var book = await _unitOfWork.Repository<AccountingBook>().GetQueryable(item => item.Id == lease.AccountingBookId &&
                item.TenantId == TenantId && !item.IsDeleted).SingleOrDefaultAsync(ct);
        if (book == null || book.Code != lease.AccountingBookCode || book.BookType != AccountingBookType.PrimaryFull ||
            book.LifecycleStatus != AccountingBookLifecycleStatus.Active || !book.IsActive || !book.AllowsPosting ||
            book.FunctionalCurrencyCode != lease.FunctionalCurrencyCode)
            throw new InvalidOperationException("The lease recognition accounting book is no longer the active governed primary posting book.");
        var recognition = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(item =>
                item.Id == lease.RecognitionPostingEventId && item.TenantId == TenantId && !item.IsDeleted &&
                item.SourceDocumentType == "LeaseRecognition" && item.SourceDocumentId == lease.Id &&
                item.PostingAction == "Post" && item.PostingStatus == "Posted" &&
                item.JournalEntryId == lease.RecognitionJournalEntryId &&
                item.AccountingBookId == lease.AccountingBookId &&
                item.BookClassification == lease.AccountingBookCode &&
                item.FunctionalCurrencyCode == lease.FunctionalCurrencyCode)
            .Include(item => item.JournalEntry)
            .SingleOrDefaultAsync(ct);
        if (recognition?.JournalEntry == null || recognition.JournalEntry.TenantId != TenantId ||
            recognition.JournalEntry.PostingStatus != "Posted" || recognition.JournalEntry.IsReversed ||
            recognition.JournalEntry.ReversalJournalEntryId.HasValue ||
            recognition.JournalEntry.ReplicatedFromJournalEntryId.HasValue ||
            recognition.JournalEntry.SourceDocumentType != "LeaseRecognition" ||
            recognition.JournalEntry.SourceDocumentId != lease.Id ||
            recognition.JournalEntry.AccountingBookId != lease.AccountingBookId ||
            recognition.JournalEntry.BookClassification != lease.AccountingBookCode)
            throw new InvalidOperationException(
                "LEASE_AUTHORITY_REMEDIATION_REQUIRED: the original posted recognition journal is unavailable, reversed or inconsistent.");
        var rouSource = FinanceSourceLineIdentity.Create(lease.Id, "ROU-ASSET", lease.Id);
        var liabilitySource = FinanceSourceLineIdentity.Create(lease.Id, "LEASE-LIABILITY", lease.Id);
        var recognitionLines = await _unitOfWork.Repository<AccountTransaction>().GetQueryable(item =>
                item.TenantId == TenantId && !item.IsDeleted &&
                item.JournalEntryId == lease.RecognitionJournalEntryId &&
                item.SourceDocumentType == "LeaseRecognition" && item.SourceDocumentId == lease.Id &&
                item.PostingStatus == "Posted" &&
                (item.SourceDocumentLineId == rouSource || item.SourceDocumentLineId == liabilitySource))
            .ToListAsync(ct);
        if (recognitionLines.Count != 2 ||
            !recognitionLines.Any(item => item.SourceDocumentLineId == rouSource &&
                item.AccountId == lease.RouAssetAccountId && item.DebitAmount == lease.PresentValue &&
                item.CreditAmount == 0m && item.AccountingBookId == lease.AccountingBookId &&
                item.BookClassification == lease.AccountingBookCode &&
                item.FunctionalCurrencyCode == lease.FunctionalCurrencyCode) ||
            !recognitionLines.Any(item => item.SourceDocumentLineId == liabilitySource &&
                item.AccountId == lease.LeaseLiabilityAccountId && item.CreditAmount == lease.PresentValue &&
                item.DebitAmount == 0m && item.AccountingBookId == lease.AccountingBookId &&
                item.BookClassification == lease.AccountingBookCode &&
                item.FunctionalCurrencyCode == lease.FunctionalCurrencyCode))
            throw new InvalidOperationException(
                "LEASE_AUTHORITY_REMEDIATION_REQUIRED: the original recognition account evidence no longer matches the frozen lease authority.");
        var periodReady = await _unitOfWork.Repository<FiscalPeriod>().GetQueryable(item =>
                item.TenantId == TenantId && !item.IsDeleted &&
                item.StartDate <= postingDate.Date && item.EndDate >= postingDate.Date &&
                (item.PeriodStatus == "Open" || !requireOpenPostingPeriod && item.PeriodStatus == "Future") &&
                (!requireOpenPostingPeriod || item.IsOpen && !item.IsLocked))
            .AnyAsync(ct);
        if (!periodReady)
            throw new InvalidOperationException(requireOpenPostingPeriod
                ? "The lease instalment date has no governed open tenant fiscal period."
                : "The lease instalment date has no governed open or future tenant fiscal period.");
    }

    private async Task EnsureLeaseAuthorityAsync(LeaseContract lease, LeaseScheduleLine source, CancellationToken ct)
    {
        var any = lease.RecognitionPostingEventId.HasValue || lease.RecognitionJournalEntryId.HasValue ||
            lease.AccountingBookId.HasValue || lease.RouAssetAccountId.HasValue || lease.LeaseLiabilityAccountId.HasValue ||
            lease.InterestExpenseAccountId.HasValue || !string.IsNullOrWhiteSpace(lease.AccountingBookCode) ||
            !string.IsNullOrWhiteSpace(lease.FunctionalCurrencyCode);
        var complete = lease.RecognitionPostingEventId.HasValue && lease.RecognitionJournalEntryId.HasValue &&
            lease.AccountingBookId.HasValue && lease.RouAssetAccountId.HasValue && lease.LeaseLiabilityAccountId.HasValue &&
            lease.InterestExpenseAccountId.HasValue && !string.IsNullOrWhiteSpace(lease.AccountingBookCode) &&
            !string.IsNullOrWhiteSpace(lease.FunctionalCurrencyCode);
        if (any && !complete)
            throw new InvalidOperationException("LEASE_AUTHORITY_REMEDIATION_REQUIRED: partially frozen lease authority cannot be inferred.");
        if (complete)
        {
            await ValidateLeaseAuthorityAsync(lease, source.PeriodDate, ct);
            return;
        }

        var events = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(item =>
                item.TenantId == TenantId && !item.IsDeleted && item.SourceDocumentType == "LeaseRecognition" &&
                item.SourceDocumentId == lease.Id && item.PostingAction == "Post" &&
                item.PostingStatus == "Posted" && item.JournalEntryId.HasValue &&
                item.JournalEntry != null && item.JournalEntry.TenantId == TenantId &&
                item.JournalEntry.SourceDocumentType == "LeaseRecognition" &&
                item.JournalEntry.SourceDocumentId == lease.Id &&
                item.JournalEntry.PostingStatus == "Posted" &&
                item.JournalEntry.ReplicatedFromJournalEntryId == null &&
                !item.JournalEntry.IsReversed && !item.JournalEntry.ReversalJournalEntryId.HasValue)
            .Include(item => item.JournalEntry).Take(2).ToListAsync(ct);
        if (events.Count != 1 || events[0].JournalEntry == null)
            throw new InvalidOperationException("LEASE_AUTHORITY_REMEDIATION_REQUIRED: one unreversed recognition journal is required.");
        var recognition = events[0];
        var rouSource = FinanceSourceLineIdentity.Create(lease.Id, "ROU-ASSET", lease.Id);
        var liabilitySource = FinanceSourceLineIdentity.Create(lease.Id, "LEASE-LIABILITY", lease.Id);
        var recognitionLines = await _unitOfWork.Repository<AccountTransaction>().GetQueryable(item =>
                item.TenantId == TenantId && !item.IsDeleted && item.JournalEntryId == recognition.JournalEntryId &&
                item.SourceDocumentType == "LeaseRecognition" && item.SourceDocumentId == lease.Id &&
                item.PostingStatus == "Posted" && item.AccountingBookId == recognition.AccountingBookId &&
                item.BookClassification == recognition.BookClassification &&
                item.FunctionalCurrencyCode == recognition.FunctionalCurrencyCode &&
                (item.SourceDocumentLineId == rouSource || item.SourceDocumentLineId == liabilitySource))
            .ToListAsync(ct);
        var rou = recognitionLines.SingleOrDefault(item => item.SourceDocumentLineId == rouSource &&
            item.DebitAmount == lease.PresentValue && item.CreditAmount == 0m);
        var liability = recognitionLines.SingleOrDefault(item => item.SourceDocumentLineId == liabilitySource &&
            item.CreditAmount == lease.PresentValue && item.DebitAmount == 0m);
        if (rou == null || liability == null)
            throw new InvalidOperationException("LEASE_AUTHORITY_REMEDIATION_REQUIRED: recognition account evidence is incomplete.");

        Guid? interestAccount = null;
        if (lease.ScheduleLines.Any(item => item.InterestExpense > 0m))
        {
            var postedPeriods = lease.ScheduleLines.Where(item => item.IsPosted && item.InterestExpense > 0m)
                .ToArray();
            if (postedPeriods.Length > 0)
            {
                var periodIds = postedPeriods.Select(item => item.Id).ToArray();
                var periodEvents = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(item =>
                        item.TenantId == TenantId && !item.IsDeleted &&
                        item.SourceDocumentType == "LeasePeriodPosting" && periodIds.Contains(item.SourceDocumentId) &&
                        item.PostingAction == "Post" && item.PostingStatus == "Posted" && item.JournalEntryId.HasValue &&
                        item.AccountingBookId == recognition.AccountingBookId &&
                        item.BookClassification == recognition.BookClassification &&
                        item.FunctionalCurrencyCode == recognition.FunctionalCurrencyCode &&
                        item.JournalEntry != null && item.JournalEntry.TenantId == TenantId &&
                        item.JournalEntry.SourceDocumentType == "LeasePeriodPosting" &&
                        item.JournalEntry.SourceDocumentId == item.SourceDocumentId &&
                        item.JournalEntry.PostingStatus == "Posted" &&
                        item.JournalEntry.ReplicatedFromJournalEntryId == null &&
                        !item.JournalEntry.IsReversed && !item.JournalEntry.ReversalJournalEntryId.HasValue)
                    .ToListAsync(ct);
                if (periodEvents.Count == postedPeriods.Length &&
                    periodEvents.GroupBy(item => item.SourceDocumentId).All(group => group.Count() == 1))
                {
                    var periodJournalIds = periodEvents.Select(item => item.JournalEntryId!.Value).ToArray();
                    var interestSources = postedPeriods.Select(item =>
                        FinanceSourceLineIdentity.Create(item.Id, "INTEREST", lease.Id, item.Id)).ToArray();
                    var transactions = await _unitOfWork.Repository<AccountTransaction>().GetQueryable(item =>
                            item.TenantId == TenantId && !item.IsDeleted &&
                            periodJournalIds.Contains(item.JournalEntryId) &&
                            item.SourceDocumentType == "LeasePeriodPosting" &&
                            item.SourceDocumentId.HasValue && periodIds.Contains(item.SourceDocumentId.Value) &&
                            item.SourceDocumentLineId.HasValue && interestSources.Contains(item.SourceDocumentLineId.Value) &&
                            item.PostingStatus == "Posted" && item.DebitAmount > 0m && item.CreditAmount == 0m &&
                            item.AccountingBookId == recognition.AccountingBookId &&
                            item.BookClassification == recognition.BookClassification &&
                            item.FunctionalCurrencyCode == recognition.FunctionalCurrencyCode)
                        .ToListAsync(ct);
                    var accounts = new HashSet<Guid>();
                    var exact = true;
                    foreach (var period in postedPeriods)
                    {
                        var postingEvent = periodEvents.Single(item => item.SourceDocumentId == period.Id);
                        var sourceId = FinanceSourceLineIdentity.Create(period.Id, "INTEREST", lease.Id, period.Id);
                        var matches = transactions.Where(item =>
                            item.JournalEntryId == postingEvent.JournalEntryId &&
                            item.SourceDocumentId == period.Id &&
                            item.SourceDocumentLineId == sourceId &&
                            item.DebitAmount == period.InterestExpense).ToArray();
                        if (matches.Length != 1) { exact = false; break; }
                        accounts.Add(matches[0].AccountId);
                    }
                    if (exact && accounts.Count == 1) interestAccount = accounts.Single();
                }
            }
            if (!interestAccount.HasValue)
                throw new InvalidOperationException(
                    "LEASE_AUTHORITY_REMEDIATION_REQUIRED: this legacy lease has no immutable posted interest-account evidence. Finance must govern the migration; today's settings will not be treated as historical authority.");
        }
        else interestAccount = liability.AccountId;

        lease.RecognitionPostingEventId = recognition.Id;
        lease.RecognitionJournalEntryId = recognition.JournalEntryId;
        lease.AccountingBookId = recognition.AccountingBookId;
        lease.AccountingBookCode = recognition.BookClassification;
        lease.FunctionalCurrencyCode = recognition.FunctionalCurrencyCode;
        lease.RouAssetAccountId = rou.AccountId;
        lease.LeaseLiabilityAccountId = liability.AccountId;
        lease.InterestExpenseAccountId = interestAccount;
        lease.UpdatedAt = DateTime.UtcNow;
        lease.UpdatedBy = UserName;
        await ValidateLeaseAuthorityAsync(lease, source.PeriodDate, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
