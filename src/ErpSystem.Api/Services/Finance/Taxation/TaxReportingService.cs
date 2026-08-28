using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Finance.Taxation;

public sealed class TaxReportingService : ITaxReportingService
{
    private const string PostedStatus = "Posted";
    private const string VendorInvoiceDocumentType = "VendorInvoice";
    private const string CustomerInvoiceDocumentType = "CustomerInvoice";
    private const string VendorPaymentDocumentType = "VendorPayment";
    private const string CustomerPaymentDocumentType = "CustomerPayment";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly ILogger<TaxReportingService> _logger;

    public TaxReportingService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<TaxReportingService> logger,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public async Task<GhanaTaxSnapshotReportDto> GetOutputTaxReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildTaxSnapshotReportAsync(
            request,
            FinanceAuditEvents.OutputTaxReportGenerated,
            "VAT/NHIL/GETFund Output Tax",
            CustomerInvoiceDocumentType,
            isOutputTax: true,
            cancellationToken);

        await RecordReportAuditAsync(FinanceAuditEvents.VatReportGenerated, report.ReportType, report.Lines.Count, report.Totals, cancellationToken);
        return report;
    }

    public async Task<GhanaTaxSnapshotReportDto> GetInputTaxReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await BuildTaxSnapshotReportAsync(
            request,
            FinanceAuditEvents.InputTaxReportGenerated,
            "VAT/NHIL/GETFund Recoverable Input Tax",
            VendorInvoiceDocumentType,
            isOutputTax: false,
            cancellationToken);
    }

    public async Task<GhanaTaxSnapshotReportDto> GetNetVatSummaryAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var output = await BuildTaxSnapshotReportAsync(request, null, "Output Tax", CustomerInvoiceDocumentType, true, cancellationToken);
        var input = await BuildTaxSnapshotReportAsync(request, null, "Recoverable Input Tax", VendorInvoiceDocumentType, false, cancellationToken);
        var (fromDate, toDate) = NormalizeRange(request);

        var report = new GhanaTaxSnapshotReportDto
        {
            ReportType = "Net VAT/NHIL/GETFund Summary",
            FromDate = fromDate,
            ToDate = toDate,
            SourceOfTruthMode = "Posted AP/AR tax snapshots reconciled to posted GL",
            Lines = output.Lines.Concat(input.Lines).ToList(),
            Diagnostics = output.Diagnostics.Concat(input.Diagnostics).ToList()
        };

        report.Totals = BuildSnapshotTotals(report.Lines);
        report.Totals.PostedGlAmount = output.Totals.PostedGlAmount - input.Totals.PostedGlAmount;
        report.Totals.Variance = report.Totals.TotalTaxAmount - report.Totals.PostedGlAmount;

        await RecordReportAuditAsync(FinanceAuditEvents.VatReportGenerated, report.ReportType, report.Lines.Count, report.Totals, cancellationToken);
        return report;
    }

    public async Task<GhanaTaxTreatmentReportDto> GetExemptZeroRatedOutOfScopeReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate) = NormalizeRange(request);
        await ValidateFiltersAsync(request, cancellationToken);

        var diagnostics = new List<TaxReportDiagnosticDto>();
        var rows = new List<GhanaTaxTreatmentLineDto>();

        var postedJournalIds = await _context.JournalEntries
            .Where(j => j.TenantId == TenantId && !j.IsDeleted && j.PostingStatus == PostedStatus)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);
        var postedJournalSet = postedJournalIds.ToHashSet();

        var postingEvents = await LoadPostingEventsAsync(new[] { VendorInvoiceDocumentType, CustomerInvoiceDocumentType }, cancellationToken);

        var apLines = await _context.Set<VendorInvoiceLineItem>()
            .AsNoTracking()
            .Include(l => l.VendorInvoice)
            .Where(l => l.TenantId == TenantId
                && !l.IsDeleted
                && l.TaxTreatment != TaxTreatment.Standard
                && l.VendorInvoice != null
                && l.VendorInvoice.TenantId == TenantId
                && l.VendorInvoice.InvoiceDate.Date >= fromDate
                && l.VendorInvoice.InvoiceDate.Date <= toDate)
            .ToListAsync(cancellationToken);

        foreach (var line in apLines.Where(l => SourceFiltersMatch(request, VendorInvoiceDocumentType, l.VendorInvoice.InvoiceNumber, null, l.VendorInvoice.SupplierId)))
        {
            var isPosted = line.VendorInvoice.JournalEntryId.HasValue && postedJournalSet.Contains(line.VendorInvoice.JournalEntryId.Value);
            if (!isPosted)
            {
                continue;
            }

            if (RoundMoney(line.TaxAmount) != 0m)
            {
                diagnostics.Add(Diagnostic("NO_TAX_TREATMENT_HAS_TAX", "Warning", $"AP line '{line.Description}' is {line.TaxTreatment} but carries tax amount {line.TaxAmount}.", line.VendorInvoiceId, VendorInvoiceDocumentType));
            }

            rows.Add(new GhanaTaxTreatmentLineDto
            {
                SourceModule = "AP",
                SourceDocumentType = VendorInvoiceDocumentType,
                SourceDocumentId = line.VendorInvoiceId,
                SourceDocumentNumber = line.VendorInvoice.InvoiceNumber,
                SourceDocumentDate = line.VendorInvoice.InvoiceDate.Date,
                SourceLineId = line.Id,
                Description = line.Description,
                TaxTreatment = line.TaxTreatment,
                LineAmount = RoundMoney(line.LineTotal),
                TaxAmount = RoundMoney(line.TaxAmount),
                JournalEntryId = line.VendorInvoice.JournalEntryId,
                PostingEventId = TryGetPostingEvent(postingEvents, VendorInvoiceDocumentType, line.VendorInvoiceId)?.Id
            });
        }

        var arLines = await _context.Set<InvoiceLineItem>()
            .AsNoTracking()
            .Include(l => l.Invoice)
            .Where(l => l.TenantId == TenantId
                && !l.IsDeleted
                && l.TaxTreatment != TaxTreatment.Standard
                && l.Invoice != null
                && l.Invoice.TenantId == TenantId
                && l.Invoice.InvoiceDate.Date >= fromDate
                && l.Invoice.InvoiceDate.Date <= toDate)
            .ToListAsync(cancellationToken);

        foreach (var line in arLines.Where(l => SourceFiltersMatch(request, CustomerInvoiceDocumentType, l.Invoice.InvoiceNumber, l.Invoice.BusinessPartnerId, null)))
        {
            var isPosted = line.Invoice.JournalEntryId.HasValue && postedJournalSet.Contains(line.Invoice.JournalEntryId.Value);
            if (!isPosted)
            {
                continue;
            }

            if (RoundMoney(line.TaxAmount) != 0m)
            {
                diagnostics.Add(Diagnostic("NO_TAX_TREATMENT_HAS_TAX", "Warning", $"AR line '{line.Description}' is {line.TaxTreatment} but carries tax amount {line.TaxAmount}.", line.InvoiceId, CustomerInvoiceDocumentType));
            }

            rows.Add(new GhanaTaxTreatmentLineDto
            {
                SourceModule = "AR",
                SourceDocumentType = CustomerInvoiceDocumentType,
                SourceDocumentId = line.InvoiceId,
                SourceDocumentNumber = line.Invoice.InvoiceNumber,
                SourceDocumentDate = line.Invoice.InvoiceDate.Date,
                SourceLineId = line.Id,
                Description = line.Description,
                TaxTreatment = line.TaxTreatment,
                LineAmount = RoundMoney(line.LineTotal),
                TaxAmount = RoundMoney(line.TaxAmount),
                JournalEntryId = line.Invoice.JournalEntryId,
                PostingEventId = TryGetPostingEvent(postingEvents, CustomerInvoiceDocumentType, line.InvoiceId)?.Id
            });
        }

        var report = new GhanaTaxTreatmentReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            Lines = rows.OrderBy(r => r.SourceDocumentDate).ThenBy(r => r.SourceDocumentNumber).ToList(),
            Diagnostics = diagnostics,
            Totals = new TaxReportTotalsDto
            {
                TaxableBase = rows.Sum(r => r.LineAmount),
                TotalTaxAmount = rows.Sum(r => r.TaxAmount)
            }
        };

        await RecordReportAuditAsync(FinanceAuditEvents.TaxReportGenerated, report.ReportType, report.Lines.Count, report.Totals, cancellationToken);
        return report;
    }

    public async Task<GhanaTaxWithholdingReportDto> GetVatWithholdingReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await BuildArWithholdingReportAsync(
            request,
            FinanceAuditEvents.VatWithholdingReportGenerated,
            "VAT Withholding Report",
            includeVatWithholding: true,
            includeStandardWithholding: false,
            cancellationToken);
    }

    public async Task<GhanaTaxWithholdingReportDto> GetWhtPayableReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await BuildApWithholdingReportAsync(request, cancellationToken);
    }

    public async Task<GhanaTaxWithholdingReportDto> GetWhtReceivableReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await BuildArWithholdingReportAsync(
            request,
            FinanceAuditEvents.WhtReportGenerated,
            "WHT Receivable/Credit Report",
            includeVatWithholding: false,
            includeStandardWithholding: true,
            cancellationToken);
    }

    public async Task<GhanaTaxAccountReconciliationReportDto> GetTaxAccountReconciliationReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var output = await BuildTaxSnapshotReportAsync(request, null, "Output Tax", CustomerInvoiceDocumentType, true, cancellationToken);
        var input = await BuildTaxSnapshotReportAsync(request, null, "Recoverable Input Tax", VendorInvoiceDocumentType, false, cancellationToken);
        var vatWht = await BuildArWithholdingReportAsync(request, null, "VAT Withholding Report", true, false, cancellationToken);
        var apWht = await BuildApWithholdingReportAsync(request, cancellationToken, auditEventType: null);
        var arWht = await BuildArWithholdingReportAsync(request, null, "WHT Receivable/Credit Report", false, true, cancellationToken);
        var (fromDate, toDate) = NormalizeRange(request);

        var expectedRows = new List<ExpectedTaxAccountAmount>();
        expectedRows.AddRange(output.Lines.Select(l => new ExpectedTaxAccountAmount("Output Tax", l.TaxAccountId, l.TaxAccountNumber, l.TaxAccountName, l.TaxAmount, true, l.JournalEntryId, l.PostingEventId)));
        expectedRows.AddRange(input.Lines.Select(l => new ExpectedTaxAccountAmount("Input Tax", l.TaxAccountId, l.TaxAccountNumber, l.TaxAccountName, l.TaxAmount, false, l.JournalEntryId, l.PostingEventId)));
        expectedRows.AddRange(vatWht.Lines.Select(l => new ExpectedTaxAccountAmount("VAT Withholding", l.TaxAccountId, l.TaxAccountNumber, l.TaxAccountName, l.WithholdingAmount, false, l.JournalEntryId, l.PostingEventId)));
        expectedRows.AddRange(apWht.Lines.Select(l => new ExpectedTaxAccountAmount("WHT Payable", l.TaxAccountId, l.TaxAccountNumber, l.TaxAccountName, l.WithholdingAmount, true, l.JournalEntryId, l.PostingEventId)));
        expectedRows.AddRange(arWht.Lines.Select(l => new ExpectedTaxAccountAmount("WHT Receivable", l.TaxAccountId, l.TaxAccountNumber, l.TaxAccountName, l.WithholdingAmount, false, l.JournalEntryId, l.PostingEventId)));

        var accountIds = expectedRows
            .Where(r => r.AccountId.HasValue)
            .Select(r => r.AccountId!.Value)
            .Distinct()
            .ToList();

        var glLines = accountIds.Count == 0
            ? new List<AccountTransaction>()
            : await _context.AccountTransactions
                .AsNoTracking()
                .Include(t => t.Account)
                .Include(t => t.JournalEntry)
                .Where(t => t.TenantId == TenantId
                    && !t.IsDeleted
                    && accountIds.Contains(t.AccountId)
                    && t.PostingStatus == PostedStatus
                    && t.JournalEntry.PostingStatus == PostedStatus
                    && t.TransactionDate.Date >= fromDate
                    && t.TransactionDate.Date <= toDate)
                .ToListAsync(cancellationToken);

        var rows = expectedRows
            .GroupBy(r => new { r.Area, r.AccountId, r.AccountNumber, r.AccountName, r.CreditNormal })
            .Select(group =>
            {
                var postedGlAmount = group.Key.AccountId.HasValue
                    ? glLines
                        .Where(t => t.AccountId == group.Key.AccountId.Value)
                        .Sum(t => group.Key.CreditNormal
                            ? RoundMoney(t.CreditAmount - t.DebitAmount)
                            : RoundMoney(t.DebitAmount - t.CreditAmount))
                    : 0m;

                var snapshotAmount = group.Sum(r => r.Amount);
                return new GhanaTaxAccountReconciliationLineDto
                {
                    Area = group.Key.Area,
                    TaxAccountId = group.Key.AccountId,
                    TaxAccountNumber = group.Key.AccountNumber,
                    TaxAccountName = group.Key.AccountName,
                    SnapshotAmount = RoundMoney(snapshotAmount),
                    PostedGlAmount = RoundMoney(postedGlAmount),
                    Variance = RoundMoney(snapshotAmount - postedGlAmount),
                    SnapshotLineCount = group.Count(),
                    PostedGlLineCount = group.Key.AccountId.HasValue ? glLines.Count(t => t.AccountId == group.Key.AccountId.Value) : 0,
                    MissingPostingReferenceCount = group.Count(r => !r.JournalEntryId.HasValue || !r.PostingEventId.HasValue)
                };
            })
            .OrderBy(r => r.Area)
            .ThenBy(r => r.TaxAccountNumber)
            .ToList();

        var diagnostics = output.Diagnostics
            .Concat(input.Diagnostics)
            .Concat(vatWht.Diagnostics)
            .Concat(apWht.Diagnostics)
            .Concat(arWht.Diagnostics)
            .ToList();

        foreach (var row in rows.Where(r => r.Variance != 0m))
        {
            diagnostics.Add(Diagnostic("TAX_GL_VARIANCE", "Warning", $"{row.Area} account {row.TaxAccountNumber ?? "(missing)"} has variance {row.Variance}."));
        }

        var report = new GhanaTaxAccountReconciliationReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            Lines = rows,
            Diagnostics = diagnostics,
            Totals = new TaxReportTotalsDto
            {
                TotalTaxAmount = rows.Sum(r => r.SnapshotAmount),
                PostedGlAmount = rows.Sum(r => r.PostedGlAmount),
                Variance = rows.Sum(r => r.Variance)
            }
        };

        await RecordReportAuditAsync(FinanceAuditEvents.TaxAccountReconciliationGenerated, report.ReportType, report.Lines.Count, report.Totals, cancellationToken);
        return report;
    }

    public async Task<GhanaTaxConfigurationHistoryReportDto> GetTaxConfigurationHistoryReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (_, toDate) = NormalizeRange(request);
        await ValidateFiltersAsync(request, cancellationToken);

        var taxes = await _context.Taxes
            .AsNoTracking()
            .Include(t => t.RateHistory)
            .Where(t => t.TenantId == TenantId && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        var rows = taxes
            .Where(t => !request.TaxId.HasValue || t.Id == request.TaxId.Value)
            .Select(t => new GhanaTaxConfigurationHistoryLineDto
            {
                TaxId = t.Id,
                TaxCode = t.Code,
                TaxName = t.Name,
                TaxCategory = t.Category,
                Applicability = t.Applicability,
                Rate = t.Rate,
                EffectiveFrom = t.EffectiveFrom.Date,
                EffectiveTo = null,
                IsActive = t.IsActive,
                IsCurrentActiveCovidLevy = IsCurrentActiveCovidLevy(t, toDate),
                HasOverlappingRateHistory = HasOverlappingRateHistory(t.RateHistory.Where(h => !h.IsDeleted)),
                TaxPayableAccountId = t.TaxPayableAccountId,
                TaxReceivableAccountId = t.TaxReceivableAccountId
            })
            .OrderBy(r => r.TaxCode)
            .ToList();

        var diagnostics = new List<TaxReportDiagnosticDto>();
        foreach (var row in rows.Where(r => r.IsCurrentActiveCovidLevy))
        {
            diagnostics.Add(Diagnostic("CURRENT_ACTIVE_COVID_LEVY", "Critical", $"Current active COVID-19 levy configuration found: {row.TaxCode}."));
        }

        foreach (var row in rows.Where(r => r.HasOverlappingRateHistory))
        {
            diagnostics.Add(Diagnostic("OVERLAPPING_TAX_RATE_HISTORY", "Warning", $"Tax {row.TaxCode} has overlapping effective-dated rate history."));
        }

        var report = new GhanaTaxConfigurationHistoryReportDto
        {
            AsOfDate = toDate,
            Lines = rows,
            Diagnostics = diagnostics
        };

        await RecordReportAuditAsync(FinanceAuditEvents.TaxReportGenerated, report.ReportType, report.Lines.Count, null, cancellationToken);
        return report;
    }

    public async Task<GhanaTaxCovidLevyDiagnosticReportDto> GetCurrentActiveCovidLevyDiagnosticReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (_, asOfDate) = NormalizeRange(request);
        var activeTaxes = await _context.Taxes
            .AsNoTracking()
            .Where(t => t.TenantId == TenantId
                && !t.IsDeleted
                && t.IsActive
                && t.EffectiveFrom.Date <= asOfDate)
            .ToListAsync(cancellationToken);
        var activeCovidTaxes = activeTaxes
            .Where(t => t.Code.Contains("COVID", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("COVID", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var diagnostics = activeCovidTaxes.Count == 0
            ? new List<TaxReportDiagnosticDto>
            {
                Diagnostic("NO_CURRENT_ACTIVE_COVID_LEVY", "Info", "No current active COVID-19 Health Recovery Levy configuration was found for this tenant.")
            }
            : activeCovidTaxes
                .Select(t => Diagnostic("CURRENT_ACTIVE_COVID_LEVY", "Critical", $"Current active COVID-19 levy configuration found: {t.Code} ({t.Name})."))
                .ToList();

        var report = new GhanaTaxCovidLevyDiagnosticReportDto
        {
            AsOfDate = asOfDate,
            Diagnostics = diagnostics
        };

        await RecordReportAuditAsync(FinanceAuditEvents.TaxReportGenerated, report.ReportType, diagnostics.Count, null, cancellationToken);
        return report;
    }

    private async Task<GhanaTaxSnapshotReportDto> BuildTaxSnapshotReportAsync(
        TaxReportRequestDto request,
        string? auditEventType,
        string reportType,
        string documentType,
        bool isOutputTax,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate) = NormalizeRange(request);
        await ValidateFiltersAsync(request, cancellationToken);

        var diagnostics = new List<TaxReportDiagnosticDto>();
        var calculations = await _context.Set<TaxCalculation>()
            .AsNoTracking()
            .Include(c => c.Tax)
            .Include(c => c.TaxGroup)
            .Where(c => c.TenantId == TenantId
                && !c.IsDeleted
                && c.DocumentType == documentType)
            .ToListAsync(cancellationToken);

        var docIds = calculations.Select(c => c.DocumentId).Distinct().ToList();
        var docInfos = isOutputTax
            ? await LoadArDocumentInfosAsync(docIds, cancellationToken)
            : await LoadApDocumentInfosAsync(docIds, cancellationToken);

        var accountIds = calculations
            .Select(c => isOutputTax ? c.Tax.TaxPayableAccountId : c.Tax.TaxReceivableAccountId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var accounts = await LoadAccountsAsync(accountIds, cancellationToken);
        var postingEvents = await LoadPostingEventsAsync(new[] { documentType }, cancellationToken);

        var lines = new List<GhanaTaxSnapshotLineDto>();
        foreach (var calc in calculations)
        {
            if (!IsVatNhilGetFundTax(calc.Tax))
            {
                continue;
            }

            if (!isOutputTax && !calc.Tax.IsInputTaxDeductible)
            {
                continue;
            }

            if (request.TaxId.HasValue && calc.TaxId != request.TaxId.Value)
            {
                continue;
            }

            if (request.TaxGroupId.HasValue && calc.TaxGroupId != request.TaxGroupId.Value)
            {
                continue;
            }

            if (!docInfos.TryGetValue(calc.DocumentId, out var docInfo))
            {
                diagnostics.Add(Diagnostic("TAX_SNAPSHOT_MISSING_SOURCE", "Warning", $"Tax snapshot {calc.Id} has no same-tenant source document.", calc.DocumentId, documentType));
                continue;
            }

            if (docInfo.DocumentDate < fromDate || docInfo.DocumentDate > toDate)
            {
                continue;
            }

            if (!SourceFiltersMatch(request, documentType, docInfo.DocumentNumber, docInfo.CustomerId, docInfo.SupplierId))
            {
                continue;
            }

            var taxAccountId = isOutputTax ? calc.Tax.TaxPayableAccountId : calc.Tax.TaxReceivableAccountId;
            if (request.TaxAccountId.HasValue && taxAccountId != request.TaxAccountId.Value)
            {
                continue;
            }

            accounts.TryGetValue(taxAccountId ?? Guid.Empty, out var account);
            if (!taxAccountId.HasValue || account == null)
            {
                diagnostics.Add(Diagnostic("TAX_SNAPSHOT_MISSING_ACCOUNT", "Critical", $"Tax snapshot {calc.Id} for {calc.Tax.Code} is missing a configured same-tenant tax account.", calc.DocumentId, documentType));
            }

            var postingEvent = TryGetPostingEvent(postingEvents, documentType, calc.DocumentId);
            if (!docInfo.JournalEntryId.HasValue || !docInfo.IsJournalPosted || postingEvent?.JournalEntryId == null)
            {
                diagnostics.Add(Diagnostic("TAX_SNAPSHOT_MISSING_POSTED_GL", "Warning", $"Tax snapshot {calc.Id} is missing a posted journal or posting-event reference.", calc.DocumentId, documentType));
            }

            lines.Add(new GhanaTaxSnapshotLineDto
            {
                TaxCalculationId = calc.Id,
                SourceModule = isOutputTax ? "AR" : "AP",
                SourceDocumentType = documentType,
                SourceDocumentId = calc.DocumentId,
                SourceDocumentNumber = docInfo.DocumentNumber,
                SourceDocumentDate = docInfo.DocumentDate,
                CounterpartyId = isOutputTax ? docInfo.CustomerId : docInfo.SupplierId,
                CounterpartyName = docInfo.CounterpartyName,
                TaxId = calc.TaxId,
                TaxCode = calc.Tax.Code,
                TaxName = calc.Tax.Name,
                TaxCategory = calc.Tax.Category,
                TaxGroupId = calc.TaxGroupId,
                TaxGroupCode = calc.TaxGroup?.Code,
                BaseAmount = RoundMoney(calc.BaseAmount),
                TaxableAmount = RoundMoney(calc.TaxableAmount),
                TaxRate = RoundRate(calc.TaxRate),
                TaxAmount = RoundMoney(calc.TaxAmount),
                IsRecoverableInputTax = !isOutputTax,
                TaxAccountId = taxAccountId,
                TaxAccountNumber = account?.AccountNumber,
                TaxAccountName = account?.AccountName,
                JournalEntryId = docInfo.JournalEntryId,
                PostingEventId = postingEvent?.Id,
                FilingPeriod = $"{docInfo.DocumentDate:yyyy-MM}"
            });
        }

        var report = new GhanaTaxSnapshotReportDto
        {
            ReportType = reportType,
            FromDate = fromDate,
            ToDate = toDate,
            Lines = lines.OrderBy(l => l.SourceDocumentDate).ThenBy(l => l.SourceDocumentNumber).ThenBy(l => l.TaxCode).ToList(),
            Diagnostics = diagnostics
        };
        report.Totals = BuildSnapshotTotals(report.Lines);
        report.Totals.PostedGlAmount = await CalculatePostedGlForSnapshotLinesAsync(report.Lines, isOutputTax, cancellationToken);
        report.Totals.Variance = RoundMoney(report.Totals.TotalTaxAmount - report.Totals.PostedGlAmount);

        if (auditEventType != null)
        {
            await RecordReportAuditAsync(auditEventType, report.ReportType, report.Lines.Count, report.Totals, cancellationToken);
        }

        return report;
    }

    private async Task<GhanaTaxWithholdingReportDto> BuildApWithholdingReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken,
        string? auditEventType = FinanceAuditEvents.WhtReportGenerated)
    {
        var (fromDate, toDate) = NormalizeRange(request);
        await ValidateFiltersAsync(request, cancellationToken);

        var payments = await _context.Set<VendorPayment>()
            .AsNoTracking()
            .Include(p => p.WithholdingTax)
            .Include(p => p.WithholdingTaxAccount)
            .Include(p => p.Allocations)
            .Where(p => p.TenantId == TenantId
                && !p.IsDeleted
                && p.PaymentDate.Date >= fromDate
                && p.PaymentDate.Date <= toDate
                && p.WithholdingTaxAmount > 0m)
            .ToListAsync(cancellationToken);

        var journalIds = payments.Where(p => p.JournalEntryId.HasValue).Select(p => p.JournalEntryId!.Value).Distinct().ToList();
        var postedJournals = await LoadPostedJournalIdsAsync(journalIds, cancellationToken);
        var postingEvents = await LoadPostingEventsAsync(new[] { VendorPaymentDocumentType }, cancellationToken);

        var lines = new List<GhanaTaxWithholdingLineDto>();
        var diagnostics = new List<TaxReportDiagnosticDto>();

        foreach (var payment in payments.Where(p => SourceFiltersMatch(request, VendorPaymentDocumentType, p.PaymentNumber, null, p.SupplierId)))
        {
            if (!payment.JournalEntryId.HasValue || !postedJournals.Contains(payment.JournalEntryId.Value))
            {
                diagnostics.Add(Diagnostic("WHT_PAYMENT_MISSING_POSTED_GL", "Warning", $"AP payment {payment.PaymentNumber} has WHT but no posted journal reference.", payment.Id, VendorPaymentDocumentType));
                continue;
            }

            if (request.TaxAccountId.HasValue && payment.WithholdingTaxAccountId != request.TaxAccountId.Value)
            {
                continue;
            }

            var postingEvent = TryGetPostingEvent(postingEvents, VendorPaymentDocumentType, payment.Id);
            var certificateStatus = CertificateStatus(payment.WithholdingCertificateNumber, payment.WithholdingCertificateDate, datedStatus: "Generated");
            if (!CertificateFilterMatches(request, certificateStatus))
            {
                continue;
            }

            if (postingEvent == null)
            {
                diagnostics.Add(Diagnostic("WHT_PAYMENT_MISSING_POSTING_EVENT", "Warning", $"AP payment {payment.PaymentNumber} has posted WHT but no posting-event reference.", payment.Id, VendorPaymentDocumentType));
            }

            lines.Add(new GhanaTaxWithholdingLineDto
            {
                WithholdingType = "AP WHT Payable",
                SourceModule = "AP",
                SourceDocumentType = VendorPaymentDocumentType,
                SourceDocumentId = payment.Id,
                SourceDocumentNumber = payment.PaymentNumber,
                SourceDocumentDate = payment.PaymentDate.Date,
                CounterpartyId = payment.SupplierId,
                TaxId = payment.WithholdingTaxId,
                TaxCode = payment.WithholdingTax?.Code,
                TaxName = payment.WithholdingTax?.Name,
                TaxRate = RoundRate(payment.WithholdingTaxRate != 0m ? payment.WithholdingTaxRate : payment.WithholdingTax?.Rate ?? 0m),
                // WHT headers are functional-currency roll-ups. Prefer the policy snapshot and
                // never add one to payment-currency cash for a cross-currency settlement.
                TaxableBase = RoundMoney(payment.WithholdingTaxBaseAmount > 0m
                    ? payment.WithholdingTaxBaseAmount
                    : payment.Allocations.Where(a => !a.IsDeleted).Sum(a => a.SettlementFunctionalAmount)),
                WithholdingAmount = RoundMoney(payment.WithholdingTaxAmount),
                TaxAccountId = payment.WithholdingTaxAccountId,
                TaxAccountNumber = payment.WithholdingTaxAccount?.AccountNumber,
                TaxAccountName = payment.WithholdingTaxAccount?.AccountName,
                CertificateNumber = payment.WithholdingCertificateNumber,
                CertificateDate = payment.WithholdingCertificateDate,
                CertificateStatus = certificateStatus,
                JournalEntryId = payment.JournalEntryId,
                PostingEventId = postingEvent?.Id
            });
        }

        var report = BuildWithholdingReport("WHT Payable Report", fromDate, toDate, lines, diagnostics);
        if (auditEventType != null)
        {
            await RecordReportAuditAsync(auditEventType, report.ReportType, report.Lines.Count, report.Totals, cancellationToken);
        }

        return report;
    }

    private async Task<GhanaTaxWithholdingReportDto> BuildArWithholdingReportAsync(
        TaxReportRequestDto request,
        string? auditEventType,
        string reportType,
        bool includeVatWithholding,
        bool includeStandardWithholding,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate) = NormalizeRange(request);
        await ValidateFiltersAsync(request, cancellationToken);

        var payments = await _context.Set<CustomerPayment>()
            .AsNoTracking()
            .Include(p => p.WithholdingTax)
            .Include(p => p.WithholdingTaxAccount)
            .Include(p => p.VatWithholdingTax)
            .Include(p => p.VatWithholdingAccount)
            .Include(p => p.Allocations)
            .Where(p => p.TenantId == TenantId
                && !p.IsDeleted
                && p.PaymentDate.Date >= fromDate
                && p.PaymentDate.Date <= toDate
                && ((includeStandardWithholding && p.WithholdingTaxAmount > 0m)
                    || (includeVatWithholding && p.VatWithholdingAmount > 0m)))
            .ToListAsync(cancellationToken);

        var journalIds = payments.Where(p => p.JournalEntryId.HasValue).Select(p => p.JournalEntryId!.Value).Distinct().ToList();
        var postedJournals = await LoadPostedJournalIdsAsync(journalIds, cancellationToken);
        var postingEvents = await LoadPostingEventsAsync(new[] { CustomerPaymentDocumentType }, cancellationToken);

        var partnerIds = payments.Select(p => p.CustomerId).Distinct().ToList();
        var partnerNames = await _context.Set<BusinessPartner>()
            .AsNoTracking()
            .Where(p => p.TenantId == TenantId && partnerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PartnerName, cancellationToken);

        var lines = new List<GhanaTaxWithholdingLineDto>();
        var diagnostics = new List<TaxReportDiagnosticDto>();

        foreach (var payment in payments.Where(p => SourceFiltersMatch(request, CustomerPaymentDocumentType, p.PaymentNumber, p.CustomerId, null)))
        {
            if (!payment.JournalEntryId.HasValue || !postedJournals.Contains(payment.JournalEntryId.Value))
            {
                diagnostics.Add(Diagnostic("WITHHOLDING_RECEIPT_MISSING_POSTED_GL", "Warning", $"AR receipt {payment.PaymentNumber} has withholding but no posted journal reference.", payment.Id, CustomerPaymentDocumentType));
                continue;
            }

            var postingEvent = TryGetPostingEvent(postingEvents, CustomerPaymentDocumentType, payment.Id);
            if (postingEvent == null)
            {
                diagnostics.Add(Diagnostic("WITHHOLDING_RECEIPT_MISSING_POSTING_EVENT", "Warning", $"AR receipt {payment.PaymentNumber} has posted withholding but no posting-event reference.", payment.Id, CustomerPaymentDocumentType));
            }

            var certificateStatus = CertificateStatus(payment.WithholdingCertificateNumber, payment.WithholdingCertificateDate);
            if (!CertificateFilterMatches(request, certificateStatus))
            {
                continue;
            }

            partnerNames.TryGetValue(payment.CustomerId, out var customerName);

            if (includeStandardWithholding && payment.WithholdingTaxAmount > 0m)
            {
                if (!request.TaxAccountId.HasValue || payment.WithholdingTaxAccountId == request.TaxAccountId.Value)
                {
                    lines.Add(new GhanaTaxWithholdingLineDto
                    {
                        WithholdingType = "AR WHT Receivable",
                        SourceModule = "AR",
                        SourceDocumentType = CustomerPaymentDocumentType,
                        SourceDocumentId = payment.Id,
                        SourceDocumentNumber = payment.PaymentNumber,
                        SourceDocumentDate = payment.PaymentDate.Date,
                        CounterpartyId = payment.CustomerId,
                        CounterpartyName = customerName,
                        TaxId = payment.WithholdingTaxId,
                        TaxCode = payment.WithholdingTax?.Code,
                        TaxName = payment.WithholdingTax?.Name,
                        TaxRate = RoundRate(payment.WithholdingTax?.Rate ?? 0m),
                        // SettlementFunctionalAmount already combines cash and all deductions at
                        // their frozen rates, so the statutory base remains meaningful when the
                        // receipt and invoices use different currencies.
                        TaxableBase = RoundMoney(payment.Allocations
                            .Where(a => !a.IsDeleted)
                            .Sum(a => a.SettlementFunctionalAmount)),
                        WithholdingAmount = RoundMoney(payment.WithholdingTaxAmount),
                        TaxAccountId = payment.WithholdingTaxAccountId,
                        TaxAccountNumber = payment.WithholdingTaxAccount?.AccountNumber,
                        TaxAccountName = payment.WithholdingTaxAccount?.AccountName,
                        CertificateNumber = payment.WithholdingCertificateNumber,
                        CertificateDate = payment.WithholdingCertificateDate,
                        CertificateStatus = certificateStatus,
                        JournalEntryId = payment.JournalEntryId,
                        PostingEventId = postingEvent?.Id
                    });
                }
            }

            if (includeVatWithholding && payment.VatWithholdingAmount > 0m)
            {
                if (!request.TaxAccountId.HasValue || payment.VatWithholdingAccountId == request.TaxAccountId.Value)
                {
                    lines.Add(new GhanaTaxWithholdingLineDto
                    {
                        WithholdingType = "VAT Withholding",
                        SourceModule = "AR",
                        SourceDocumentType = CustomerPaymentDocumentType,
                        SourceDocumentId = payment.Id,
                        SourceDocumentNumber = payment.PaymentNumber,
                        SourceDocumentDate = payment.PaymentDate.Date,
                        CounterpartyId = payment.CustomerId,
                        CounterpartyName = customerName,
                        TaxId = payment.VatWithholdingTaxId,
                        TaxCode = payment.VatWithholdingTax?.Code,
                        TaxName = payment.VatWithholdingTax?.Name,
                        TaxRate = RoundRate(payment.VatWithholdingTax?.Rate ?? 0m),
                        TaxableBase = RoundMoney(payment.Allocations
                            .Where(a => !a.IsDeleted)
                            .Sum(a => a.SettlementFunctionalAmount)),
                        WithholdingAmount = RoundMoney(payment.VatWithholdingAmount),
                        TaxAccountId = payment.VatWithholdingAccountId,
                        TaxAccountNumber = payment.VatWithholdingAccount?.AccountNumber,
                        TaxAccountName = payment.VatWithholdingAccount?.AccountName,
                        CertificateNumber = payment.WithholdingCertificateNumber,
                        CertificateDate = payment.WithholdingCertificateDate,
                        CertificateStatus = certificateStatus,
                        JournalEntryId = payment.JournalEntryId,
                        PostingEventId = postingEvent?.Id
                    });
                }
            }
        }

        var report = BuildWithholdingReport(reportType, fromDate, toDate, lines, diagnostics);
        if (auditEventType != null)
        {
            await RecordReportAuditAsync(auditEventType, report.ReportType, report.Lines.Count, report.Totals, cancellationToken);
        }

        return report;
    }

    private async Task ValidateFiltersAsync(TaxReportRequestDto request, CancellationToken cancellationToken)
    {
        if (request.TaxId.HasValue && !await _context.Taxes.AnyAsync(t => t.TenantId == TenantId && t.Id == request.TaxId.Value && !t.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Tax filter was not found for this tenant.");
        }

        if (request.TaxGroupId.HasValue && !await _context.TaxGroups.AnyAsync(g => g.TenantId == TenantId && g.Id == request.TaxGroupId.Value && !g.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Tax group filter was not found for this tenant.");
        }

        if (request.TaxAccountId.HasValue && !await _context.Accounts.AnyAsync(a => a.TenantId == TenantId && a.Id == request.TaxAccountId.Value && !a.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Tax account filter was not found for this tenant.");
        }

        if (request.SupplierId.HasValue && !await _context.Set<Supplier>().AnyAsync(s => s.TenantId == TenantId && s.Id == request.SupplierId.Value && !s.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Supplier filter was not found for this tenant.");
        }

        if (request.CustomerId.HasValue && !await _context.Set<BusinessPartner>().AnyAsync(c => c.TenantId == TenantId && c.Id == request.CustomerId.Value && !c.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Customer filter was not found for this tenant.");
        }
    }

    private async Task<Dictionary<Guid, SourceDocumentInfo>> LoadApDocumentInfosAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0)
        {
            return new Dictionary<Guid, SourceDocumentInfo>();
        }

        var docs = await _context.VendorInvoices
            .AsNoTracking()
            .Where(i => i.TenantId == TenantId && documentIds.Contains(i.Id) && !i.IsDeleted)
            .ToListAsync(cancellationToken);

        var journalIds = docs.Where(d => d.JournalEntryId.HasValue).Select(d => d.JournalEntryId!.Value).Distinct().ToList();
        var postedJournalIds = await LoadPostedJournalIdsAsync(journalIds, cancellationToken);

        return docs.ToDictionary(
            d => d.Id,
            d => new SourceDocumentInfo(
                d.InvoiceNumber,
                d.InvoiceDate.Date,
                null,
                d.SupplierId,
                d.SupplierName,
                d.JournalEntryId,
                d.JournalEntryId.HasValue && postedJournalIds.Contains(d.JournalEntryId.Value)));
    }

    private async Task<Dictionary<Guid, SourceDocumentInfo>> LoadArDocumentInfosAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0)
        {
            return new Dictionary<Guid, SourceDocumentInfo>();
        }

        var docs = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == TenantId && documentIds.Contains(i.Id) && !i.IsDeleted)
            .ToListAsync(cancellationToken);

        var journalIds = docs.Where(d => d.JournalEntryId.HasValue).Select(d => d.JournalEntryId!.Value).Distinct().ToList();
        var postedJournalIds = await LoadPostedJournalIdsAsync(journalIds, cancellationToken);

        return docs.ToDictionary(
            d => d.Id,
            d => new SourceDocumentInfo(
                d.InvoiceNumber,
                d.InvoiceDate.Date,
                d.BusinessPartnerId,
                null,
                d.CustomerName,
                d.JournalEntryId,
                d.JournalEntryId.HasValue && postedJournalIds.Contains(d.JournalEntryId.Value)));
    }

    private async Task<Dictionary<Guid, Account>> LoadAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, Account>();
        }

        return await _context.Accounts
            .AsNoTracking()
            .Where(a => a.TenantId == TenantId && accountIds.Contains(a.Id) && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Id, cancellationToken);
    }

    private async Task<HashSet<Guid>> LoadPostedJournalIdsAsync(
        IReadOnlyCollection<Guid> journalIds,
        CancellationToken cancellationToken)
    {
        if (journalIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = await _context.JournalEntries
            .AsNoTracking()
            .Where(j => j.TenantId == TenantId && journalIds.Contains(j.Id) && !j.IsDeleted && j.PostingStatus == PostedStatus)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private async Task<List<FinancePostingEvent>> LoadPostingEventsAsync(
        IReadOnlyCollection<string> documentTypes,
        CancellationToken cancellationToken)
    {
        return await _context.FinancePostingEvents
            .AsNoTracking()
            .Where(e => e.TenantId == TenantId
                && !e.IsDeleted
                && e.PostingStatus == PostedStatus
                && documentTypes.Contains(e.SourceDocumentType))
            .ToListAsync(cancellationToken);
    }

    private async Task<decimal> CalculatePostedGlForSnapshotLinesAsync(
        IReadOnlyCollection<GhanaTaxSnapshotLineDto> lines,
        bool isOutputTax,
        CancellationToken cancellationToken)
    {
        var journalIds = lines
            .Where(l => l.JournalEntryId.HasValue && l.TaxAccountId.HasValue)
            .Select(l => l.JournalEntryId!.Value)
            .Distinct()
            .ToList();
        var accountIds = lines
            .Where(l => l.TaxAccountId.HasValue)
            .Select(l => l.TaxAccountId!.Value)
            .Distinct()
            .ToList();

        if (journalIds.Count == 0 || accountIds.Count == 0)
        {
            return 0m;
        }

        var tags = isOutputTax ? "AR-Tax-" : "AP-Tax-";
        var glLines = await _context.AccountTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == TenantId
                && !t.IsDeleted
                && journalIds.Contains(t.JournalEntryId)
                && accountIds.Contains(t.AccountId)
                && t.PostingStatus == PostedStatus
                && t.TransactionTag != null
                && t.TransactionTag.StartsWith(tags))
            .ToListAsync(cancellationToken);

        return isOutputTax
            ? RoundMoney(glLines.Sum(t => t.CreditAmount - t.DebitAmount))
            : RoundMoney(glLines.Sum(t => t.DebitAmount - t.CreditAmount));
    }

    private static GhanaTaxWithholdingReportDto BuildWithholdingReport(
        string reportType,
        DateTime fromDate,
        DateTime toDate,
        List<GhanaTaxWithholdingLineDto> lines,
        List<TaxReportDiagnosticDto> diagnostics)
    {
        var orderedLines = lines.OrderBy(l => l.SourceDocumentDate).ThenBy(l => l.SourceDocumentNumber).ToList();
        return new GhanaTaxWithholdingReportDto
        {
            ReportType = reportType,
            FromDate = fromDate,
            ToDate = toDate,
            Lines = orderedLines,
            Diagnostics = diagnostics,
            Totals = new TaxReportTotalsDto
            {
                TaxableBase = orderedLines.Sum(l => l.TaxableBase),
                TotalWithholdingAmount = orderedLines.Sum(l => l.WithholdingAmount),
                TotalTaxAmount = orderedLines.Sum(l => l.WithholdingAmount)
            }
        };
    }

    private static TaxReportTotalsDto BuildSnapshotTotals(IReadOnlyCollection<GhanaTaxSnapshotLineDto> lines)
    {
        return new TaxReportTotalsDto
        {
            TaxableBase = RoundMoney(lines.Sum(l => l.BaseAmount)),
            TaxableAmount = RoundMoney(lines.Sum(l => l.TaxableAmount)),
            VatAmount = RoundMoney(lines.Where(l => IsVatCode(l.TaxCode)).Sum(l => l.TaxAmount)),
            NhilAmount = RoundMoney(lines.Where(l => IsNhilCode(l.TaxCode)).Sum(l => l.TaxAmount)),
            GetFundAmount = RoundMoney(lines.Where(l => IsGetFundCode(l.TaxCode)).Sum(l => l.TaxAmount)),
            TotalTaxAmount = RoundMoney(lines.Sum(l => l.TaxAmount))
        };
    }

    private async Task RecordReportAuditAsync(
        string eventType,
        string reportType,
        int rowCount,
        TaxReportTotalsDto? totals,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = "Finance.TaxReporting",
            SourceDocumentType = "TaxReport",
            AfterValues = new
            {
                reportType,
                rowCount,
                totals
            },
            Resource = "Finance.TaxReport",
            ResourceId = $"{reportType}:{DateTime.UtcNow:O}"
        }, cancellationToken);
    }

    private static (DateTime FromDate, DateTime ToDate) NormalizeRange(TaxReportRequestDto request)
    {
        var toDate = (request.ToDate ?? request.FromDate ?? DateTime.UtcNow.Date).Date;
        var fromDate = (request.FromDate ?? new DateTime(toDate.Year, toDate.Month, 1)).Date;

        if (fromDate > toDate)
        {
            throw new InvalidOperationException("Tax report from-date cannot be after to-date.");
        }

        return (fromDate, toDate);
    }

    private static bool SourceFiltersMatch(
        TaxReportRequestDto request,
        string documentType,
        string documentNumber,
        Guid? customerId,
        Guid? supplierId)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceDocumentType) &&
            !string.Equals(request.SourceDocumentType, documentType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.SourceDocumentNumber) &&
            !documentNumber.Contains(request.SourceDocumentNumber, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.CustomerId.HasValue && customerId != request.CustomerId.Value)
        {
            return false;
        }

        if (request.SupplierId.HasValue && supplierId != request.SupplierId.Value)
        {
            return false;
        }

        return true;
    }

    private static bool CertificateFilterMatches(TaxReportRequestDto request, string certificateStatus)
    {
        return string.IsNullOrWhiteSpace(request.CertificateStatus)
            || string.Equals(request.CertificateStatus, certificateStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static FinancePostingEvent? TryGetPostingEvent(
        IReadOnlyCollection<FinancePostingEvent> events,
        string documentType,
        Guid documentId)
    {
        return events.FirstOrDefault(e => e.SourceDocumentType == documentType && e.SourceDocumentId == documentId);
    }

    private static bool IsVatNhilGetFundTax(Tax tax)
    {
        return tax.Category is TaxCategory.Standard or TaxCategory.Levy
            && (IsVatCode(tax.Code) || IsNhilCode(tax.Code) || IsGetFundCode(tax.Code));
    }

    private static bool IsVatCode(string code)
        => code.Contains("VAT", StringComparison.OrdinalIgnoreCase)
            && !code.Contains("WHT", StringComparison.OrdinalIgnoreCase)
            && !code.Contains("WITHHOLD", StringComparison.OrdinalIgnoreCase);

    private static bool IsNhilCode(string code)
        => code.Contains("NHIL", StringComparison.OrdinalIgnoreCase);

    private static bool IsGetFundCode(string code)
        => code.Contains("GET", StringComparison.OrdinalIgnoreCase);

    private static bool IsCurrentActiveCovidLevy(Tax tax, DateTime asOfDate)
    {
        return tax.IsActive
            && tax.EffectiveFrom.Date <= asOfDate.Date
            && (tax.Code.Contains("COVID", StringComparison.OrdinalIgnoreCase)
                || tax.Name.Contains("COVID", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasOverlappingRateHistory(IEnumerable<TaxRateHistory> histories)
    {
        var ordered = histories.OrderBy(h => h.EffectiveFrom).ToList();
        for (var index = 0; index < ordered.Count - 1; index++)
        {
            var currentEnd = ordered[index].EffectiveTo?.Date ?? DateTime.MaxValue.Date;
            var nextStart = ordered[index + 1].EffectiveFrom.Date;
            if (currentEnd >= nextStart)
            {
                return true;
            }
        }

        return false;
    }

    private static string CertificateStatus(string? certificateNumber, DateTime? certificateDate, string datedStatus = "Received")
    {
        if (string.IsNullOrWhiteSpace(certificateNumber))
        {
            return "Missing";
        }

        return certificateDate.HasValue ? datedStatus : "NumberOnly";
    }

    private static TaxReportDiagnosticDto Diagnostic(
        string code,
        string severity,
        string message,
        Guid? sourceDocumentId = null,
        string? sourceDocumentType = null)
    {
        return new TaxReportDiagnosticDto
        {
            Code = code,
            Severity = severity,
            Message = message,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentType = sourceDocumentType
        };
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private sealed record SourceDocumentInfo(
        string DocumentNumber,
        DateTime DocumentDate,
        Guid? CustomerId,
        Guid? SupplierId,
        string? CounterpartyName,
        Guid? JournalEntryId,
        bool IsJournalPosted);

    private sealed record ExpectedTaxAccountAmount(
        string Area,
        Guid? AccountId,
        string? AccountNumber,
        string? AccountName,
        decimal Amount,
        bool CreditNormal,
        Guid? JournalEntryId,
        Guid? PostingEventId);
}
