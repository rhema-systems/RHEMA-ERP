using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed class SubledgerSettlementReadModelService : ISubledgerSettlementReadModelService
{
    private const string PostAction = "Post";
    private const string PostedStatus = "Posted";
    private const string VendorInvoiceType = "VendorInvoice";
    private const string VendorPaymentType = "VendorPayment";
    private const string CustomerInvoiceType = "CustomerInvoice";
    private const string CustomerPaymentType = "CustomerPayment";
    private const string SalesCreditNoteType = "SalesCreditNote";
    private const string CustomerCreditNoteType = "CustomerCreditNote";
    private const string OpeningBalanceBatchType = "OpeningBalanceBatch";
    private const string OpeningBalancePostingAction = "PostOpeningBalance";
    private const string MigrationModule = "MIGRATION";
    private const string SupplierAdvanceOpeningType = "SupplierAdvanceOpening";
    private const string CustomerAdvanceOpeningType = "CustomerAdvanceOpening";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly ILogger<SubledgerSettlementReadModelService> _logger;

    public SubledgerSettlementReadModelService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<SubledgerSettlementReadModelService> logger,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<SubledgerSettlementRebuildResultDto> RebuildAsync(
        SubledgerSettlementRebuildRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = TenantId;
        var module = NormalizeModule(request.SourceModule);
        var asOfDate = request.AsOfDate ?? DateTime.UtcNow;
        var rebuildBatchId = Guid.NewGuid();
        var rebuiltAt = DateTime.UtcNow;
        var result = new SubledgerSettlementRebuildResultDto
        {
            RebuildBatchId = rebuildBatchId,
            AsOfDate = asOfDate,
            RebuiltAt = rebuiltAt
        };

        try
        {
            if (module is "Both" or SubledgerSettlementModules.AccountsPayable)
            {
                await ClearModuleAsync(SubledgerSettlementModules.AccountsPayable, cancellationToken);
                var apResult = await RebuildAccountsPayableAsync(tenantId, asOfDate, rebuildBatchId, rebuiltAt, cancellationToken);
                result.ApDocumentCount = apResult.DocumentCount;
                result.ApUnappliedSettlementCount = apResult.UnappliedSettlementCount;
                result.ApplicationCount += apResult.ApplicationCount;
                result.Diagnostics.AddRange(apResult.Diagnostics);
            }

            if (module is "Both" or SubledgerSettlementModules.AccountsReceivable)
            {
                await ClearModuleAsync(SubledgerSettlementModules.AccountsReceivable, cancellationToken);
                var arResult = await RebuildAccountsReceivableAsync(tenantId, asOfDate, rebuildBatchId, rebuiltAt, cancellationToken);
                result.ArDocumentCount = arResult.DocumentCount;
                result.ArUnappliedSettlementCount = arResult.UnappliedSettlementCount;
                result.ApplicationCount += arResult.ApplicationCount;
                result.Diagnostics.AddRange(arResult.Diagnostics);
            }

            result.DiagnosticCount = result.Diagnostics.Count;
            await _context.SaveChangesAsync(cancellationToken);

            if (request.RecordAudit)
            {
                if (result.ApDocumentCount > 0 || module == SubledgerSettlementModules.AccountsPayable || module == "Both")
                {
                    await RecordAuditAsync(FinanceAuditEvents.ApSettlementReadModelRebuilt, result, cancellationToken);
                }

                if (result.ArDocumentCount > 0 || module == SubledgerSettlementModules.AccountsReceivable || module == "Both")
                {
                    await RecordAuditAsync(FinanceAuditEvents.ArSettlementReadModelRebuilt, result, cancellationToken);
                }

                if (result.DiagnosticCount > 0)
                {
                    await RecordAuditAsync(FinanceAuditEvents.SettlementRebuildVarianceDetected, result, cancellationToken);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subledger settlement read-model rebuild failed for module {Module}", module);
            await RecordAuditAsync(FinanceAuditEvents.SettlementRebuildFailed, new
            {
                module,
                asOfDate,
                error = ex.Message
            }, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<SubledgerSettlementBalance>> GetBalancesAsync(
        string sourceModule,
        DateTime asOfDate,
        Guid? counterpartyId = null,
        CancellationToken cancellationToken = default)
    {
        var module = NormalizeModule(sourceModule);
        if (module == "Both")
        {
            throw new ArgumentException("A single source module is required when reading settlement balances.", nameof(sourceModule));
        }

        var query = _context.SubledgerSettlementBalances
            .AsNoTracking()
            .Include(b => b.Applications)
            .Where(b =>
                b.TenantId == TenantId &&
                b.SourceModule == module &&
                b.TransactionDate.Date <= asOfDate.Date);

        if (counterpartyId.HasValue)
        {
            query = query.Where(b => b.CounterpartyId == counterpartyId.Value);
        }

        return await query
            .OrderBy(b => b.DueDate ?? b.TransactionDate)
            .ThenBy(b => b.SourceDocumentNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubledgerUnappliedSettlementBalance>> GetUnappliedBalancesAsync(
        string sourceModule,
        DateTime asOfDate,
        Guid? counterpartyId = null,
        CancellationToken cancellationToken = default)
    {
        var module = NormalizeModule(sourceModule);
        if (module == "Both")
        {
            throw new ArgumentException("A single source module is required when reading unapplied settlement balances.", nameof(sourceModule));
        }

        var query = _context.SubledgerUnappliedSettlementBalances
            .AsNoTracking()
            .Where(b =>
                b.TenantId == TenantId &&
                b.SourceModule == module &&
                b.SettlementDate.Date <= asOfDate.Date);

        if (counterpartyId.HasValue)
        {
            query = query.Where(b => b.CounterpartyId == counterpartyId.Value);
        }

        return await query
            .OrderBy(b => b.SettlementDate)
            .ThenBy(b => b.SettlementSourceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<SubledgerControlReconciliationDto> GetControlReconciliationAsync(
        string sourceModule,
        DateTime? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var module = NormalizeModule(sourceModule);
        if (module == "Both")
        {
            throw new ArgumentException("A single source module is required for control reconciliation.", nameof(sourceModule));
        }

        var date = asOfDate ?? DateTime.UtcNow;
        await RebuildAsync(new SubledgerSettlementRebuildRequestDto
        {
            SourceModule = module,
            AsOfDate = date,
            RecordAudit = false
        }, cancellationToken);

        var tenantId = TenantId;
        var balances = await _context.SubledgerSettlementBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.SourceModule == module)
            .ToListAsync(cancellationToken);

        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new
            {
                s.ControlAccountApId,
                s.ControlAccountArId
            })
            .FirstOrDefaultAsync(cancellationToken);

        var controlAccountId = module == SubledgerSettlementModules.AccountsPayable
            ? settings?.ControlAccountApId
            : settings?.ControlAccountArId;

        Account? account = null;
        decimal postedGlBalance = 0m;
        var primaryBook = await ResolveEffectivePrimaryBookAsync(tenantId, date, cancellationToken);
        var diagnostics = balances
            .Where(b => b.HasDiagnostics)
            .Select(b => new SubledgerSettlementDiagnosticDto
            {
                SourceModule = b.SourceModule,
                Code = "ReadModelDiagnostic",
                Message = b.DiagnosticFlags ?? "Settlement read-model diagnostic.",
                SourceDocumentId = b.SourceDocumentId,
                PostingEventId = b.SourcePostingEventId,
                VarianceAmount = b.OperationalVariance
            })
            .ToList();

        if (controlAccountId.HasValue)
        {
            account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == controlAccountId.Value, cancellationToken);

            if (account == null)
            {
                diagnostics.Add(BuildDiagnostic(module, "ControlAccountMissing", "Configured control account does not belong to the current tenant.", null));
            }
            else
            {
                var linesQuery = _context.AccountTransactions
                    .AsNoTracking()
                    .Where(t =>
                        t.TenantId == tenantId &&
                        t.AccountId == account.Id &&
                        t.PostingStatus == PostedStatus &&
                        !t.IsDeleted &&
                        t.TransactionDate.Date <= date.Date);

                if (primaryBook.Book != null)
                {
                    var primaryBookId = primaryBook.Book.Id;
                    var primaryBookCode = primaryBook.Book.Code;
                    linesQuery = linesQuery.Where(t =>
                        t.AccountingBookId == primaryBookId ||
                        (t.AccountingBookId == Guid.Empty && t.BookClassification == primaryBookCode));
                }
                else if (primaryBook.HasConfiguredBooks)
                {
                    diagnostics.Add(BuildDiagnostic(
                        module,
                        "PrimaryAccountingBookAmbiguous",
                        "Exactly one effective primary accounting book is required for control-account reconciliation.",
                        null));
                    linesQuery = linesQuery.Where(_ => false);
                }

                var lines = await linesQuery.ToListAsync(cancellationToken);

                postedGlBalance = module == SubledgerSettlementModules.AccountsPayable
                    ? lines.Sum(t => t.CreditAmount - t.DebitAmount)
                    : lines.Sum(t => t.DebitAmount - t.CreditAmount);
            }
        }
        else
        {
            diagnostics.Add(BuildDiagnostic(module, "ControlAccountNotConfigured", "AP/AR control account is not configured for reconciliation.", null));
        }

        var readModelOutstanding = balances.Sum(b => b.OutstandingAmount);
        var report = new SubledgerControlReconciliationDto
        {
            SourceModule = module,
            AsOfDate = date,
            AccountingBookId = primaryBook.Book?.Id,
            AccountingBookCode = primaryBook.Book?.Code,
            AccountingBookName = primaryBook.Book?.Name,
            ControlAccountId = account?.Id,
            ControlAccountNumber = account?.AccountNumber ?? account?.AccountCode,
            ControlAccountName = account?.AccountName,
            ReadModelOutstanding = readModelOutstanding,
            PostedGlControlBalance = postedGlBalance,
            Variance = postedGlBalance - readModelOutstanding,
            DocumentCount = balances.Count,
            DiagnosticCount = diagnostics.Count,
            Diagnostics = diagnostics
        };

        await RecordAuditAsync(
            module == SubledgerSettlementModules.AccountsPayable
                ? FinanceAuditEvents.ApControlReconciliationGenerated
                : FinanceAuditEvents.ArControlReconciliationGenerated,
            report,
            cancellationToken);

        return report;
    }

    private async Task<PrimaryBookResolution> ResolveEffectivePrimaryBookAsync(
        Guid tenantId,
        DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        var books = await _context.AccountingBooks
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (books.Count == 0)
        {
            // Compatibility for historical tenants created before governed accounting books.
            return new PrimaryBookResolution(null, false);
        }

        var date = asOfDate.Date;
        var designation = await _context.AccountingBookPrimaryDesignations
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                item.ReversedAtUtc == null && item.EffectiveFrom <= date)
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenByDescending(item => item.ApprovedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        Guid? primaryBookId = designation?.NewPrimaryBookId;
        if (!primaryBookId.HasValue)
        {
            primaryBookId = await _context.AccountingBookPrimaryDesignations
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                    item.ReversedAtUtc == null && item.EffectiveFrom > date)
                .OrderBy(item => item.EffectiveFrom)
                .ThenBy(item => item.ApprovedAtUtc)
                .Select(item => (Guid?)item.PreviousPrimaryBookId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var matches = primaryBookId.HasValue
            ? books.Where(item => item.Id == primaryBookId.Value).ToList()
            : books.Where(item => item.IsDefault && item.BookType == AccountingBookType.PrimaryFull).ToList();
        return new PrimaryBookResolution(matches.Count == 1 ? matches[0] : null, true);
    }

    private async Task ClearModuleAsync(string module, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var applications = await _context.SubledgerSettlementApplications
            .Where(a => a.TenantId == tenantId && a.SourceModule == module)
            .ToListAsync(cancellationToken);
        _context.SubledgerSettlementApplications.RemoveRange(applications);

        var balances = await _context.SubledgerSettlementBalances
            .Where(b => b.TenantId == tenantId && b.SourceModule == module)
            .ToListAsync(cancellationToken);
        _context.SubledgerSettlementBalances.RemoveRange(balances);

        var unappliedBalances = await _context.SubledgerUnappliedSettlementBalances
            .Where(b => b.TenantId == tenantId && b.SourceModule == module)
            .ToListAsync(cancellationToken);
        _context.SubledgerUnappliedSettlementBalances.RemoveRange(unappliedBalances);
    }

    private sealed record PrimaryBookResolution(AccountingBook? Book, bool HasConfiguredBooks);

    private async Task<ModuleRebuildResult> RebuildAccountsPayableAsync(
        Guid tenantId,
        DateTime asOfDate,
        Guid rebuildBatchId,
        DateTime rebuiltAt,
        CancellationToken cancellationToken)
    {
        var postedInvoiceEvents = await GetPostingEventsAsync(tenantId, "AP", VendorInvoiceType, asOfDate, cancellationToken);
        var postedPaymentEvents = await GetPostingEventsAsync(tenantId, "AP", VendorPaymentType, asOfDate, cancellationToken);
        await AddOpeningAdvancePostingEventsAsync(
            tenantId, asOfDate, postedPaymentEvents, isSupplierAdvance: true, cancellationToken);
        var postedAdvanceApplicationEvents = await GetPostingEventsAsync(tenantId, "AP", "VendorPaymentAdvanceApplication", asOfDate, cancellationToken);
        var invoiceIds = postedInvoiceEvents.Keys.ToList();
        var invoices = await _context.VendorInvoices
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId &&
                invoiceIds.Contains(i.Id) &&
                i.InvoiceDate.Date <= asOfDate.Date &&
                i.Status != VendorInvoiceStatus.Voided &&
                i.Status != VendorInvoiceStatus.Draft)
            .ToListAsync(cancellationToken);

        var allocations = await _context.Set<VendorPaymentAllocation>()
            .AsNoTracking()
            .Include(a => a.VendorPayment)
            .Where(a =>
                a.TenantId == tenantId &&
                invoiceIds.Contains(a.VendorInvoiceId) &&
                !a.IsDeleted &&
                !a.IsReversal &&
                a.VendorPayment.Status != VendorPaymentStatus.Voided &&
                a.VendorPayment.Status != VendorPaymentStatus.Failed &&
                a.AllocationDate.Date <= asOfDate.Date)
            .ToListAsync(cancellationToken);

        var crossTenantAllocationCount = await _context.Set<VendorPaymentAllocation>()
            .AsNoTracking()
            .CountAsync(a => invoiceIds.Contains(a.VendorInvoiceId) && a.TenantId != tenantId, cancellationToken);

        var fxByAllocation = await GetFxSettlementsAsync(tenantId, "AP", cancellationToken);
        var tenantJournalIds = await GetTenantJournalIdsAsync(
            tenantId,
            postedInvoiceEvents.Values.Select(e => e.JournalEntryId).Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);
        var result = new ModuleRebuildResult();
        if (crossTenantAllocationCount > 0)
        {
            result.Diagnostics.Add(BuildDiagnostic(
                SubledgerSettlementModules.AccountsPayable,
                "CrossTenantAllocation",
                $"{crossTenantAllocationCount} AP allocations reference current-tenant invoices from another tenant.",
                null));
        }

        foreach (var invoice in invoices)
        {
            var sourceEvent = postedInvoiceEvents[invoice.Id];
            var diagnostics = ValidatePostingReference(
                SubledgerSettlementModules.AccountsPayable,
                invoice.Id,
                sourceEvent,
                tenantJournalIds,
                result.Diagnostics);

            var invoiceAllocations = allocations
                .Where(a =>
                    a.VendorInvoiceId == invoice.Id &&
                    a.VendorPayment != null &&
                    a.VendorPayment.TenantId == tenantId &&
                    postedPaymentEvents.ContainsKey(a.VendorPaymentId) &&
                    a.VendorPayment.PaymentDate.Date <= asOfDate.Date)
                .ToList();

            // An advance does not settle AP when cash is first paid. It settles AP only when
            // the later advance-application posting exists and is linked to this allocation.
            invoiceAllocations = invoiceAllocations
                .Where(allocation =>
                {
                    var payment = allocation.VendorPayment!;
                    if (!payment.IsSupplierAdvance)
                    {
                        return true;
                    }

                    var hasApplicationEvent = allocation.ApplicationPostingEventId.HasValue &&
                                              postedAdvanceApplicationEvents.TryGetValue(allocation.Id, out var applicationEvent) &&
                                              applicationEvent.Id == allocation.ApplicationPostingEventId.Value;
                    if (!hasApplicationEvent)
                    {
                        diagnostics.Add("MissingAdvanceApplicationPostingEvent");
                        result.Diagnostics.Add(BuildDiagnostic(
                            SubledgerSettlementModules.AccountsPayable,
                            "MissingAdvanceApplicationPostingEvent",
                            "Supplier advance allocation is missing its posted advance-application event and is excluded from AP settlement.",
                            allocation.Id,
                            allocation.ApplicationPostingEventId));
                    }

                    return hasApplicationEvent;
                })
                .ToList();

            var settledAmount = invoiceAllocations.Sum(a => a.AllocatedAmount + a.DiscountAmount);
            var withheldAmount = invoiceAllocations.Sum(a => a.WithholdingTaxAmount);
            var creditedAmount = 0m;
            var outstanding = Round(invoice.TotalAmount - settledAmount - withheldAmount - creditedAmount);
            var operationalOutstanding = Round(invoice.TotalAmount - invoice.PaidAmount);
            var operationalVariance = Round(outstanding - operationalOutstanding);
            if (operationalVariance != 0)
            {
                diagnostics.Add("OperationalSnapshotVariance");
                result.Diagnostics.Add(BuildDiagnostic(
                    SubledgerSettlementModules.AccountsPayable,
                    "OperationalSnapshotVariance",
                    "AP operational paid field differs from posted settlement read-model outstanding amount.",
                    invoice.Id,
                    sourceEvent.Id,
                    operationalVariance));
            }

            var balance = BuildBalance(
                tenantId,
                SubledgerSettlementModules.AccountsPayable,
                invoice.BusinessPartnerId,
                VendorInvoiceType,
                invoice.Id,
                invoice.InvoiceNumber,
                sourceEvent,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.CurrencyCode,
                invoice.TotalAmount,
                invoice.BaseCurrencyAmount,
                invoice.ExchangeRate,
                settledAmount,
                creditedAmount,
                withheldAmount,
                outstanding,
                invoice.PaidAmount,
                0m,
                operationalOutstanding,
                operationalVariance,
                rebuildBatchId,
                rebuiltAt,
                diagnostics);

            foreach (var allocation in invoiceAllocations)
            {
                var payment = allocation.VendorPayment!;
                var paymentEvent = postedPaymentEvents[allocation.VendorPaymentId];
                var settlementEvent = payment.IsSupplierAdvance
                    ? postedAdvanceApplicationEvents[allocation.Id]
                    : paymentEvent;
                var application = BuildApplication(
                    tenantId,
                    SubledgerSettlementModules.AccountsPayable,
                    invoice.BusinessPartnerId,
                    VendorInvoiceType,
                    invoice.Id,
                    VendorPaymentType,
                    payment.Id,
                    allocation.Id,
                    settlementEvent,
                    payment.PaymentDate,
                    invoice.CurrencyCode,
                    sourceEvent.FunctionalCurrencyCode,
                    allocation.AllocatedAmount + allocation.DiscountAmount,
                    0m,
                    allocation.WithholdingTaxAmount,
                    fxByAllocation.GetValueOrDefault(allocation.Id)?.Id,
                    rebuildBatchId,
                    allocation.WithholdingTaxAmount > 0 ? "Includes AP WHT settlement component." : null);
                balance.Applications.Add(application);
                result.ApplicationCount++;
            }

            _context.SubledgerSettlementBalances.Add(balance);
            result.DocumentCount++;
        }

        result.UnappliedSettlementCount = await RebuildApUnappliedSettlementsAsync(
            tenantId,
            asOfDate,
            rebuildBatchId,
            rebuiltAt,
            postedPaymentEvents,
            result.Diagnostics,
            cancellationToken);

        return result;
    }

    private async Task<ModuleRebuildResult> RebuildAccountsReceivableAsync(
        Guid tenantId,
        DateTime asOfDate,
        Guid rebuildBatchId,
        DateTime rebuiltAt,
        CancellationToken cancellationToken)
    {
        var postedInvoiceEvents = await GetPostingEventsAsync(tenantId, "AR", CustomerInvoiceType, asOfDate, cancellationToken);
        var postedReceiptEvents = await GetPostingEventsAsync(tenantId, "AR", CustomerPaymentType, asOfDate, cancellationToken);
        await AddOpeningAdvancePostingEventsAsync(
            tenantId, asOfDate, postedReceiptEvents, isSupplierAdvance: false, cancellationToken);
        var postedAdvanceApplicationEvents = await GetPostingEventsAsync(tenantId, "AR", "CustomerPaymentAdvanceApplication", asOfDate, cancellationToken);
        var postedSalesCreditNoteEvents = await GetPostingEventsAsync(tenantId, "AR", SalesCreditNoteType, asOfDate, cancellationToken);
        var postedCompatibilityCreditNoteEvents = await GetPostingEventsAsync(tenantId, "AR", CustomerCreditNoteType, asOfDate, cancellationToken);
        var invoiceIds = postedInvoiceEvents.Keys.ToList();

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId &&
                invoiceIds.Contains(i.Id) &&
                i.InvoiceDate.Date <= asOfDate.Date &&
                i.Status != InvoiceStatus.Cancelled &&
                i.Status != InvoiceStatus.Draft)
            .ToListAsync(cancellationToken);

        var allocations = await _context.Set<PaymentAllocation>()
            .AsNoTracking()
            .Include(a => a.CustomerPayment)
            .Where(a =>
                a.TenantId == tenantId &&
                invoiceIds.Contains(a.InvoiceId) &&
                !a.IsDeleted &&
                !a.IsReversal &&
                a.AllocationDate.Date <= asOfDate.Date)
            .ToListAsync(cancellationToken);

        var creditNotes = await _context.CreditNotes
            .AsNoTracking()
            .Where(c =>
                c.TenantId == tenantId &&
                !c.IsDeleted &&
                c.CreditNoteStatus == CreditNoteStatus.Applied &&
                c.AppliedDate.HasValue &&
                c.AppliedDate.Value.Date <= asOfDate.Date &&
                // AppliedToInvoiceId is the settlement target. OriginalInvoiceId is used only
                // for historical rows that predate an explicit application target.
                ((c.AppliedToInvoiceId.HasValue && invoiceIds.Contains(c.AppliedToInvoiceId.Value)) ||
                 (!c.AppliedToInvoiceId.HasValue && c.OriginalInvoiceId.HasValue && invoiceIds.Contains(c.OriginalInvoiceId.Value))))
            .ToListAsync(cancellationToken);

        var crossTenantAllocationCount = await _context.Set<PaymentAllocation>()
            .AsNoTracking()
            .CountAsync(a => invoiceIds.Contains(a.InvoiceId) && a.TenantId != tenantId, cancellationToken);

        var fxByAllocation = await GetFxSettlementsAsync(tenantId, "AR", cancellationToken);
        var tenantJournalIds = await GetTenantJournalIdsAsync(
            tenantId,
            postedInvoiceEvents.Values.Select(e => e.JournalEntryId).Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);
        var result = new ModuleRebuildResult();
        foreach (var creditNote in creditNotes.Where(c =>
                     c.OriginalInvoiceId.HasValue &&
                     c.AppliedToInvoiceId.HasValue &&
                     c.OriginalInvoiceId != c.AppliedToInvoiceId))
        {
            result.Diagnostics.Add(BuildDiagnostic(
                SubledgerSettlementModules.AccountsReceivable,
                "SalesCreditNoteTargetConflict",
                "Posted Sales credit note has different original and applied invoice references; the read model uses the explicit applied invoice only.",
                creditNote.Id,
                postedSalesCreditNoteEvents.GetValueOrDefault(creditNote.Id)?.Id));
        }

        if (crossTenantAllocationCount > 0)
        {
            result.Diagnostics.Add(BuildDiagnostic(
                SubledgerSettlementModules.AccountsReceivable,
                "CrossTenantAllocation",
                $"{crossTenantAllocationCount} AR allocations reference current-tenant invoices from another tenant.",
                null));
        }

        foreach (var invoice in invoices)
        {
            var sourceEvent = postedInvoiceEvents[invoice.Id];
            var diagnostics = ValidatePostingReference(
                SubledgerSettlementModules.AccountsReceivable,
                invoice.Id,
                sourceEvent,
                tenantJournalIds,
                result.Diagnostics);

            var invoiceAllocations = allocations
                .Where(a =>
                    a.InvoiceId == invoice.Id &&
                    a.CustomerPayment != null &&
                    a.CustomerPayment.TenantId == tenantId &&
                    a.CustomerPayment.PaymentDate.Date <= asOfDate.Date)
                .ToList();

            var receiptAllocations = invoiceAllocations
                .Where(a =>
                    a.CustomerPayment != null &&
                    !a.CustomerPayment.IsCreditNote &&
                    postedReceiptEvents.ContainsKey(a.CustomerPaymentId))
                .ToList();

            // A customer advance reaches the AR control account only in its later application
            // journal. Do not let a missing application event reduce invoice aging silently.
            receiptAllocations = receiptAllocations
                .Where(allocation =>
                {
                    var payment = allocation.CustomerPayment!;
                    if (!payment.IsCustomerAdvance)
                    {
                        return true;
                    }

                    var hasApplicationEvent = allocation.ApplicationPostingEventId.HasValue &&
                                              postedAdvanceApplicationEvents.TryGetValue(allocation.Id, out var applicationEvent) &&
                                              applicationEvent.Id == allocation.ApplicationPostingEventId.Value;
                    if (!hasApplicationEvent)
                    {
                        diagnostics.Add("MissingAdvanceApplicationPostingEvent");
                        result.Diagnostics.Add(BuildDiagnostic(
                            SubledgerSettlementModules.AccountsReceivable,
                            "MissingAdvanceApplicationPostingEvent",
                            "Customer advance allocation is missing its posted advance-application event and is excluded from AR settlement.",
                            allocation.Id,
                            allocation.ApplicationPostingEventId));
                    }

                    return hasApplicationEvent;
                })
                .ToList();

            var compatibilityCreditNoteAllocations = invoiceAllocations
                .Where(a =>
                    a.CustomerPayment != null &&
                    a.CustomerPayment.IsCreditNote &&
                    postedCompatibilityCreditNoteEvents.ContainsKey(a.CustomerPaymentId))
                .ToList();

            var salesCreditNotes = creditNotes
                .Where(c =>
                    c.AppliedToInvoiceId == invoice.Id ||
                    (!c.AppliedToInvoiceId.HasValue && c.OriginalInvoiceId == invoice.Id))
                // The original posting event remains immutable after correction, but its credit
                // effect must leave the settlement projection once the inverse journal is posted.
                .Where(c => c.CreditNoteStatus != CreditNoteStatus.Reversed)
                .Where(c => postedSalesCreditNoteEvents.ContainsKey(c.Id))
                .ToList();

            var settledAmount = 0m;
            var withheldAmount = 0m;
            var creditedAmount = compatibilityCreditNoteAllocations.Sum(a => a.AllocatedAmount + a.DiscountAmount)
                + salesCreditNotes.Sum(c => c.TotalAmount);

            var balance = BuildBalance(
                tenantId,
                SubledgerSettlementModules.AccountsReceivable,
                invoice.CustomerId,
                CustomerInvoiceType,
                invoice.Id,
                invoice.InvoiceNumber,
                sourceEvent,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.CurrencyCode,
                invoice.TotalAmount,
                invoice.BaseCurrencyAmount,
                invoice.ExchangeRate,
                0m,
                creditedAmount,
                0m,
                0m,
                invoice.PaidAmount,
                invoice.CreditedAmount,
                Round(invoice.TotalAmount - invoice.PaidAmount - invoice.CreditedAmount),
                0m,
                rebuildBatchId,
                rebuiltAt,
                diagnostics);

            foreach (var allocation in receiptAllocations)
            {
                var payment = allocation.CustomerPayment!;
                var paymentEvent = postedReceiptEvents[allocation.CustomerPaymentId];
                var settlementEvent = payment.IsCustomerAdvance
                    ? postedAdvanceApplicationEvents[allocation.Id]
                    : paymentEvent;
                var allocationWithholding = CalculateArAllocationWithholding(allocation, receiptAllocations);
                var applicationSettledAmount = Math.Max(0m, allocation.AllocatedAmount + allocation.DiscountAmount - allocationWithholding);

                settledAmount += applicationSettledAmount;
                withheldAmount += allocationWithholding;

                balance.Applications.Add(BuildApplication(
                    tenantId,
                    SubledgerSettlementModules.AccountsReceivable,
                    invoice.CustomerId,
                    CustomerInvoiceType,
                    invoice.Id,
                    CustomerPaymentType,
                    payment.Id,
                    allocation.Id,
                    settlementEvent,
                    payment.PaymentDate,
                    invoice.CurrencyCode,
                    sourceEvent.FunctionalCurrencyCode,
                    applicationSettledAmount,
                    0m,
                    allocationWithholding,
                    fxByAllocation.GetValueOrDefault(allocation.Id)?.Id,
                    rebuildBatchId,
                    allocationWithholding > 0 ? "Includes AR withholding/VAT withholding settlement component." : null));
                result.ApplicationCount++;
            }

            foreach (var allocation in compatibilityCreditNoteAllocations)
            {
                var payment = allocation.CustomerPayment!;
                var creditNoteEvent = postedCompatibilityCreditNoteEvents[allocation.CustomerPaymentId];
                balance.Applications.Add(BuildApplication(
                    tenantId,
                    SubledgerSettlementModules.AccountsReceivable,
                    invoice.CustomerId,
                    CustomerInvoiceType,
                    invoice.Id,
                    CustomerCreditNoteType,
                    payment.Id,
                    allocation.Id,
                    creditNoteEvent,
                    payment.PaymentDate,
                    invoice.CurrencyCode,
                    sourceEvent.FunctionalCurrencyCode,
                    0m,
                    allocation.AllocatedAmount + allocation.DiscountAmount,
                    0m,
                    null,
                    rebuildBatchId,
                    "Compatibility customer credit-note allocation."));
                result.ApplicationCount++;
            }

            foreach (var creditNote in salesCreditNotes)
            {
                var creditNoteEvent = postedSalesCreditNoteEvents[creditNote.Id];
                balance.Applications.Add(BuildApplication(
                    tenantId,
                    SubledgerSettlementModules.AccountsReceivable,
                    invoice.CustomerId,
                    CustomerInvoiceType,
                    invoice.Id,
                    SalesCreditNoteType,
                    creditNote.Id,
                    null,
                    creditNoteEvent,
                    creditNote.AppliedDate ?? creditNote.DocumentDate,
                    invoice.CurrencyCode,
                    sourceEvent.FunctionalCurrencyCode,
                    0m,
                    creditNote.TotalAmount,
                    0m,
                    null,
                    rebuildBatchId,
                    "Sales credit note applied to invoice."));
                result.ApplicationCount++;
            }

            balance.SettledAmount = Round(settledAmount);
            balance.WithheldAmount = Round(withheldAmount);
            balance.CreditedAmount = Round(creditedAmount);
            balance.OutstandingAmount = Round(invoice.TotalAmount - balance.SettledAmount - balance.WithheldAmount - balance.CreditedAmount);
            balance.SettlementStatus = ResolveStatus(balance.OutstandingAmount);
            balance.OperationalVariance = Round(balance.OutstandingAmount - balance.OperationalOutstandingSnapshot);
            if (balance.OperationalVariance != 0)
            {
                diagnostics.Add("OperationalSnapshotVariance");
                result.Diagnostics.Add(BuildDiagnostic(
                    SubledgerSettlementModules.AccountsReceivable,
                    "OperationalSnapshotVariance",
                    "AR operational paid/credited fields differ from posted settlement read-model outstanding amount.",
                    invoice.Id,
                    sourceEvent.Id,
                    balance.OperationalVariance));
            }

            balance.HasDiagnostics = diagnostics.Count > 0;
            balance.DiagnosticFlags = diagnostics.Count == 0 ? null : string.Join(";", diagnostics.Distinct());

            _context.SubledgerSettlementBalances.Add(balance);
            result.DocumentCount++;
        }

        result.UnappliedSettlementCount = await RebuildArUnappliedSettlementsAsync(
            tenantId,
            asOfDate,
            rebuildBatchId,
            rebuiltAt,
            postedReceiptEvents,
            result.Diagnostics,
            cancellationToken);

        return result;
    }

    private async Task<int> RebuildApUnappliedSettlementsAsync(
        Guid tenantId,
        DateTime asOfDate,
        Guid rebuildBatchId,
        DateTime rebuiltAt,
        IReadOnlyDictionary<Guid, FinancePostingEvent> postedPaymentEvents,
        ICollection<SubledgerSettlementDiagnosticDto> diagnostics,
        CancellationToken cancellationToken)
    {
        if (postedPaymentEvents.Count == 0)
        {
            return 0;
        }

        var paymentIds = postedPaymentEvents.Keys.ToList();
        var payments = await _context.Set<VendorPayment>()
            .AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId &&
                paymentIds.Contains(p.Id) &&
                p.PaymentDate.Date <= asOfDate.Date &&
                p.Status != VendorPaymentStatus.Voided &&
                p.Status != VendorPaymentStatus.Failed &&
                !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var allocations = await _context.Set<VendorPaymentAllocation>()
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                paymentIds.Contains(a.VendorPaymentId) &&
                !a.IsDeleted &&
                !a.IsReversal &&
                a.AllocationDate.Date <= asOfDate.Date)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var payment in payments)
        {
            var applied = Round(allocations
                .Where(a => a.VendorPaymentId == payment.Id)
                .Sum(a => a.AllocatedAmount));
            var unapplied = Round(payment.TotalAmount - applied);

            var flags = new List<string>();
            if (applied > Round(payment.TotalAmount))
            {
                flags.Add("OverAppliedPayment");
                diagnostics.Add(BuildDiagnostic(
                    SubledgerSettlementModules.AccountsPayable,
                    "OverAppliedPayment",
                    "Posted vendor payment allocations exceed the payment cash amount.",
                    payment.Id,
                    postedPaymentEvents[payment.Id].Id,
                    Round(applied - payment.TotalAmount)));
            }

            if (unapplied <= 0m)
            {
                continue;
            }

            _context.SubledgerUnappliedSettlementBalances.Add(BuildUnappliedBalance(
                tenantId,
                SubledgerSettlementModules.AccountsPayable,
                payment.BusinessPartnerId,
                VendorPaymentType,
                payment.Id,
                payment.PaymentNumber,
                payment.IsSupplierAdvance
                    ? SubledgerUnappliedSettlementClassifications.SupplierAdvance
                    : SubledgerUnappliedSettlementClassifications.UnappliedVendorPayment,
                postedPaymentEvents[payment.Id],
                payment.PaymentDate,
                payment.CurrencyCode,
                payment.TotalAmount,
                applied,
                unapplied,
                rebuildBatchId,
                rebuiltAt,
                flags));
            count++;
        }

        return count;
    }

    private async Task<int> RebuildArUnappliedSettlementsAsync(
        Guid tenantId,
        DateTime asOfDate,
        Guid rebuildBatchId,
        DateTime rebuiltAt,
        IReadOnlyDictionary<Guid, FinancePostingEvent> postedReceiptEvents,
        ICollection<SubledgerSettlementDiagnosticDto> diagnostics,
        CancellationToken cancellationToken)
    {
        if (postedReceiptEvents.Count == 0)
        {
            return 0;
        }

        var receiptIds = postedReceiptEvents.Keys.ToList();
        var receipts = await _context.Set<CustomerPayment>()
            .AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId &&
                receiptIds.Contains(p.Id) &&
                !p.IsCreditNote &&
                p.PaymentDate.Date <= asOfDate.Date &&
                !string.Equals(p.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Status, "Bounced", StringComparison.OrdinalIgnoreCase) &&
                !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var allocations = await _context.Set<PaymentAllocation>()
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId &&
                receiptIds.Contains(a.CustomerPaymentId) &&
                !a.IsDeleted &&
                !a.IsReversal &&
                a.AllocationDate.Date <= asOfDate.Date)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var receipt in receipts)
        {
            var applied = Round(allocations
                .Where(a => a.CustomerPaymentId == receipt.Id)
                .Sum(a => a.AllocatedAmount));
            var unapplied = Round(receipt.TotalAmount - applied);

            var flags = new List<string>();
            if (applied > Round(receipt.TotalAmount))
            {
                flags.Add("OverAppliedReceipt");
                diagnostics.Add(BuildDiagnostic(
                    SubledgerSettlementModules.AccountsReceivable,
                    "OverAppliedReceipt",
                    "Posted customer receipt allocations exceed the receipt cash amount.",
                    receipt.Id,
                    postedReceiptEvents[receipt.Id].Id,
                    Round(applied - receipt.TotalAmount)));
            }

            if (unapplied <= 0m)
            {
                continue;
            }

            _context.SubledgerUnappliedSettlementBalances.Add(BuildUnappliedBalance(
                tenantId,
                SubledgerSettlementModules.AccountsReceivable,
                receipt.CustomerId,
                CustomerPaymentType,
                receipt.Id,
                receipt.PaymentNumber,
                receipt.IsCustomerAdvance
                    ? SubledgerUnappliedSettlementClassifications.CustomerAdvance
                    : SubledgerUnappliedSettlementClassifications.UnappliedCustomerReceipt,
                postedReceiptEvents[receipt.Id],
                receipt.PaymentDate,
                receipt.CurrencyCode,
                receipt.TotalAmount,
                applied,
                unapplied,
                rebuildBatchId,
                rebuiltAt,
                flags));
            count++;
        }

        return count;
    }

    private async Task AddOpeningAdvancePostingEventsAsync(
        Guid tenantId,
        DateTime asOfDate,
        IDictionary<Guid, FinancePostingEvent> destination,
        bool isSupplierAdvance,
        CancellationToken cancellationToken)
    {
        // Normal payments own their FinancePostingEvent. Cutover advances are different: the
        // approved OpeningBalanceBatch owns the central posting because no new cash moves at
        // go-live, while VendorPayment/CustomerPayment remains the canonical advance lot. Resolve
        // that deliberate two-record lineage here instead of manufacturing duplicate posting events.
        var sourceRows = isSupplierAdvance
            ? await _context.Set<VendorPayment>()
                .AsNoTracking()
                .Where(payment => payment.TenantId == tenantId && !payment.IsDeleted
                    && payment.IsSupplierAdvance
                    && payment.OpeningBalanceType == SupplierAdvanceOpeningType
                    && payment.OpeningBalanceBatchId.HasValue
                    && payment.JournalEntryId.HasValue
                    && payment.PaymentDate.Date <= asOfDate.Date)
                .Select(payment => new OpeningAdvanceSource(payment.Id, payment.OpeningBalanceBatchId!.Value, payment.JournalEntryId!.Value))
                .ToListAsync(cancellationToken)
            : await _context.Set<CustomerPayment>()
                .AsNoTracking()
                .Where(payment => payment.TenantId == tenantId && !payment.IsDeleted
                    && payment.IsCustomerAdvance
                    && payment.OpeningBalanceType == CustomerAdvanceOpeningType
                    && payment.OpeningBalanceBatchId.HasValue
                    && payment.JournalEntryId.HasValue
                    && payment.PaymentDate.Date <= asOfDate.Date)
                .Select(payment => new OpeningAdvanceSource(payment.Id, payment.OpeningBalanceBatchId!.Value, payment.JournalEntryId!.Value))
                .ToListAsync(cancellationToken);

        if (sourceRows.Count == 0)
        {
            return;
        }

        var batchIds = sourceRows.Select(item => item.BatchId).Distinct().ToList();
        var batchEvents = await _context.FinancePostingEvents
            .AsNoTracking()
            .Where(postingEvent => postingEvent.TenantId == tenantId && !postingEvent.IsDeleted
                && postingEvent.SourceModule == MigrationModule
                && postingEvent.SourceDocumentType == OpeningBalanceBatchType
                // Opening batches use a distinct action so operational payment postings and
                // cutover postings remain distinguishable in the immutable posting ledger.
                && postingEvent.PostingAction == OpeningBalancePostingAction
                && postingEvent.PostingStatus == PostedStatus
                && postingEvent.JournalEntryId.HasValue
                && postingEvent.PostingDate.Date <= asOfDate.Date
                && batchIds.Contains(postingEvent.SourceDocumentId))
            .OrderByDescending(postingEvent => postingEvent.PostedAt ?? postingEvent.PostingDate)
            .ToListAsync(cancellationToken);

        var eventByBatch = batchEvents
            .GroupBy(postingEvent => postingEvent.SourceDocumentId)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var source in sourceRows)
        {
            // Require the source back-reference and batch event to name the same journal. A stale
            // or tampered linkage is excluded from settlement evidence rather than guessed.
            if (eventByBatch.TryGetValue(source.BatchId, out var postingEvent)
                && postingEvent.JournalEntryId == source.JournalEntryId)
            {
                destination[source.SourceId] = postingEvent;
            }
        }
    }

    private async Task<Dictionary<Guid, FinancePostingEvent>> GetPostingEventsAsync(
        Guid tenantId,
        string sourceModule,
        string sourceDocumentType,
        DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        var events = await _context.FinancePostingEvents
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId &&
                e.SourceModule == sourceModule &&
                e.SourceDocumentType == sourceDocumentType &&
                e.PostingAction == PostAction &&
                e.PostingStatus == PostedStatus &&
                e.SourceDocumentId != Guid.Empty &&
                e.PostingDate.Date <= asOfDate.Date &&
                !e.IsDeleted)
            .OrderByDescending(e => e.PostedAt ?? e.PostingDate)
            .ToListAsync(cancellationToken);

        return events
            .GroupBy(e => e.SourceDocumentId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private sealed record OpeningAdvanceSource(Guid SourceId, Guid BatchId, Guid JournalEntryId);

    private async Task<Dictionary<Guid, FxRealizedSettlement>> GetFxSettlementsAsync(
        Guid tenantId,
        string sourceModule,
        CancellationToken cancellationToken)
    {
        var settlements = await _context.FxRealizedSettlements
            .AsNoTracking()
            .Where(f =>
                f.TenantId == tenantId &&
                f.SourceModule == sourceModule &&
                f.Status == PostedStatus &&
                !f.IsDeleted)
            .ToListAsync(cancellationToken);

        return settlements
            .GroupBy(f => f.SettlementAllocationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.PostedAt).First());
    }

    private async Task<HashSet<Guid>> GetTenantJournalIdsAsync(
        Guid tenantId,
        IEnumerable<Guid> journalIds,
        CancellationToken cancellationToken)
    {
        var ids = journalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var validIds = await _context.JournalEntries
            .AsNoTracking()
            .Where(j => j.TenantId == tenantId && ids.Contains(j.Id) && j.PostingStatus == PostedStatus)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);

        return validIds.ToHashSet();
    }

    private List<string> ValidatePostingReference(
        string module,
        Guid sourceDocumentId,
        FinancePostingEvent sourceEvent,
        IReadOnlySet<Guid> tenantJournalIds,
        ICollection<SubledgerSettlementDiagnosticDto> diagnostics)
    {
        var flags = new List<string>();
        if (!sourceEvent.JournalEntryId.HasValue)
        {
            flags.Add("MissingSourceJournal");
            diagnostics.Add(BuildDiagnostic(module, "MissingSourceJournal", "Posted source document has no journal reference.", sourceDocumentId, sourceEvent.Id));
        }
        else if (!tenantJournalIds.Contains(sourceEvent.JournalEntryId.Value))
        {
            flags.Add("InvalidSourceJournalTenantOrStatus");
            diagnostics.Add(BuildDiagnostic(
                module,
                "InvalidSourceJournalTenantOrStatus",
                "Posted source document journal reference is missing, not posted, or belongs to another tenant.",
                sourceDocumentId,
                sourceEvent.Id));
        }

        return flags;
    }

    private SubledgerSettlementBalance BuildBalance(
        Guid tenantId,
        string module,
        Guid counterpartyId,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string sourceDocumentNumber,
        FinancePostingEvent sourceEvent,
        DateTime transactionDate,
        DateTime? dueDate,
        string documentCurrencyCode,
        decimal originalDocumentAmount,
        decimal originalFunctionalAmount,
        decimal exchangeRate,
        decimal settledAmount,
        decimal creditedAmount,
        decimal withheldAmount,
        decimal outstandingAmount,
        decimal operationalPaidAmount,
        decimal operationalCreditedAmount,
        decimal operationalOutstandingAmount,
        decimal operationalVariance,
        Guid rebuildBatchId,
        DateTime rebuiltAt,
        IReadOnlyCollection<string> diagnostics)
    {
        var functionalAmount = originalFunctionalAmount != 0
            ? originalFunctionalAmount
            : sourceEvent.TotalDebitAmount != 0
                ? sourceEvent.TotalDebitAmount
                : Round(originalDocumentAmount * (exchangeRate <= 0 ? 1 : exchangeRate));

        return new SubledgerSettlementBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = module,
            CounterpartyId = counterpartyId,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentNumber = sourceDocumentNumber,
            SourcePostingEventId = sourceEvent.Id,
            SourceJournalEntryId = sourceEvent.JournalEntryId,
            TransactionDate = transactionDate,
            DueDate = dueDate,
            DocumentCurrencyCode = string.IsNullOrWhiteSpace(documentCurrencyCode) ? "GHS" : documentCurrencyCode,
            FunctionalCurrencyCode = string.IsNullOrWhiteSpace(sourceEvent.FunctionalCurrencyCode) ? "GHS" : sourceEvent.FunctionalCurrencyCode,
            OriginalDocumentAmount = Round(originalDocumentAmount),
            OriginalFunctionalAmount = Round(functionalAmount),
            SettledAmount = Round(settledAmount),
            CreditedAmount = Round(creditedAmount),
            WithheldAmount = Round(withheldAmount),
            OutstandingAmount = Round(outstandingAmount),
            SettlementStatus = ResolveStatus(outstandingAmount),
            RebuildBatchId = rebuildBatchId,
            LastRebuiltAt = rebuiltAt,
            HasDiagnostics = diagnostics.Count > 0,
            DiagnosticFlags = diagnostics.Count == 0 ? null : string.Join(";", diagnostics.Distinct()),
            OperationalPaidAmountSnapshot = Round(operationalPaidAmount),
            OperationalCreditedAmountSnapshot = Round(operationalCreditedAmount),
            OperationalOutstandingSnapshot = Round(operationalOutstandingAmount),
            OperationalVariance = Round(operationalVariance),
            Status = "Active",
            CreatedAt = rebuiltAt
        };
    }

    private static SubledgerUnappliedSettlementBalance BuildUnappliedBalance(
        Guid tenantId,
        string module,
        Guid counterpartyId,
        string settlementSourceType,
        Guid settlementSourceId,
        string settlementSourceNumber,
        string classification,
        FinancePostingEvent settlementEvent,
        DateTime settlementDate,
        string documentCurrencyCode,
        decimal originalAmount,
        decimal appliedAmount,
        decimal unappliedAmount,
        Guid rebuildBatchId,
        DateTime rebuiltAt,
        IReadOnlyCollection<string> diagnostics)
    {
        return new SubledgerUnappliedSettlementBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = module,
            CounterpartyId = counterpartyId,
            SettlementSourceType = settlementSourceType,
            SettlementSourceId = settlementSourceId,
            SettlementSourceNumber = settlementSourceNumber,
            Classification = classification,
            SettlementPostingEventId = settlementEvent.Id,
            SettlementJournalEntryId = settlementEvent.JournalEntryId,
            SettlementDate = settlementDate,
            DocumentCurrencyCode = string.IsNullOrWhiteSpace(documentCurrencyCode) ? "GHS" : documentCurrencyCode,
            FunctionalCurrencyCode = string.IsNullOrWhiteSpace(settlementEvent.FunctionalCurrencyCode) ? "GHS" : settlementEvent.FunctionalCurrencyCode,
            OriginalAmount = Round(originalAmount),
            AppliedAmount = Round(appliedAmount),
            UnappliedAmount = Round(unappliedAmount),
            RebuildBatchId = rebuildBatchId,
            LastRebuiltAt = rebuiltAt,
            HasDiagnostics = diagnostics.Count > 0,
            DiagnosticFlags = diagnostics.Count == 0 ? null : string.Join(";", diagnostics.Distinct()),
            Status = "Active",
            CreatedAt = rebuiltAt
        };
    }

    private static SubledgerSettlementApplication BuildApplication(
        Guid tenantId,
        string module,
        Guid counterpartyId,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string settlementSourceType,
        Guid settlementSourceId,
        Guid? settlementAllocationId,
        FinancePostingEvent settlementEvent,
        DateTime settlementDate,
        string documentCurrencyCode,
        string functionalCurrencyCode,
        decimal settledAmount,
        decimal creditedAmount,
        decimal withheldAmount,
        Guid? fxRealizedSettlementId,
        Guid rebuildBatchId,
        string? notes)
    {
        return new SubledgerSettlementApplication
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = module,
            CounterpartyId = counterpartyId,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SettlementSourceType = settlementSourceType,
            SettlementSourceId = settlementSourceId,
            SettlementAllocationId = settlementAllocationId,
            SettlementPostingEventId = settlementEvent.Id,
            SettlementJournalEntryId = settlementEvent.JournalEntryId,
            SettlementDate = settlementDate,
            DocumentCurrencyCode = string.IsNullOrWhiteSpace(documentCurrencyCode) ? "GHS" : documentCurrencyCode,
            FunctionalCurrencyCode = string.IsNullOrWhiteSpace(functionalCurrencyCode) ? "GHS" : functionalCurrencyCode,
            SettledAmount = Round(settledAmount),
            CreditedAmount = Round(creditedAmount),
            WithheldAmount = Round(withheldAmount),
            FxRealizedSettlementId = fxRealizedSettlementId,
            RebuildBatchId = rebuildBatchId,
            Notes = notes,
            Status = "Active"
        };
    }

    private static decimal CalculateArAllocationWithholding(
        PaymentAllocation allocation,
        IReadOnlyCollection<PaymentAllocation> paymentAllocationsForReport)
    {
        var payment = allocation.CustomerPayment;
        if (payment == null)
        {
            return 0m;
        }

        var totalWithholding = payment.WithholdingTaxAmount + payment.VatWithholdingAmount;
        if (totalWithholding <= 0)
        {
            return 0m;
        }

        var paymentAllocationTotal = paymentAllocationsForReport
            .Where(a => a.CustomerPaymentId == payment.Id)
            .Sum(a => a.AllocatedAmount);

        if (paymentAllocationTotal <= 0)
        {
            return 0m;
        }

        return Round(totalWithholding * allocation.AllocatedAmount / paymentAllocationTotal);
    }

    private static string ResolveStatus(decimal outstandingAmount)
    {
        var amount = Round(outstandingAmount);
        if (amount < 0)
        {
            return SubledgerSettlementStatuses.OverSettled;
        }

        if (amount == 0)
        {
            return SubledgerSettlementStatuses.Settled;
        }

        return SubledgerSettlementStatuses.Open;
    }

    private static SubledgerSettlementDiagnosticDto BuildDiagnostic(
        string module,
        string code,
        string message,
        Guid? sourceDocumentId,
        Guid? postingEventId = null,
        decimal? varianceAmount = null)
    {
        return new SubledgerSettlementDiagnosticDto
        {
            SourceModule = module,
            Code = code,
            Message = message,
            SourceDocumentId = sourceDocumentId,
            PostingEventId = postingEventId,
            VarianceAmount = varianceAmount
        };
    }

    private static decimal Round(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static string NormalizeModule(string? module)
    {
        if (string.IsNullOrWhiteSpace(module) || module.Equals("Both", StringComparison.OrdinalIgnoreCase))
        {
            return "Both";
        }

        if (module.Equals(SubledgerSettlementModules.AccountsPayable, StringComparison.OrdinalIgnoreCase) ||
            module.Equals("AccountsPayable", StringComparison.OrdinalIgnoreCase))
        {
            return SubledgerSettlementModules.AccountsPayable;
        }

        if (module.Equals(SubledgerSettlementModules.AccountsReceivable, StringComparison.OrdinalIgnoreCase) ||
            module.Equals("AccountsReceivable", StringComparison.OrdinalIgnoreCase))
        {
            return SubledgerSettlementModules.AccountsReceivable;
        }

        throw new ArgumentException($"Unsupported subledger settlement module '{module}'.", nameof(module));
    }

    private async Task RecordAuditAsync(string eventType, object payload, CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        try
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = TenantId,
                SourceModule = "Finance",
                SourceDocumentType = "SubledgerSettlementReadModel",
                Resource = "Finance.SubledgerSettlementReadModel",
                ResourceId = TenantId.ToString(),
                AfterValues = payload
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record finance audit event {EventType}", eventType);
        }
    }

    private sealed class ModuleRebuildResult
    {
        public int DocumentCount { get; set; }
        public int UnappliedSettlementCount { get; set; }
        public int ApplicationCount { get; set; }
        public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; } = new();
    }
}
