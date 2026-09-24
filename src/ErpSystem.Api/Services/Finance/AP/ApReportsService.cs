using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services.Finance.AP
{
    /// <summary>
    /// Provides AP reporting: supplier aging, cash requirement forecast,
    /// supplier statements, withholding tax summaries, and AP dashboard metrics.
    /// </summary>
    public class ApReportsService : IApReportsService
    {
        private static readonly JsonSerializerOptions BudgetCommitmentSnapshotJsonOptions =
            new(JsonSerializerDefaults.Web);
        private static readonly JsonSerializerOptions PurchaseOrderSnapshotJsonOptions =
            new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<ApReportsService> _logger;
        private readonly ISubledgerSettlementReadModelService _settlementReadModelService;
        private readonly IFinanceAuditService? _financeAuditService;

        public ApReportsService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ILogger<ApReportsService> logger,
            ISubledgerSettlementReadModelService settlementReadModelService,
            IFinanceAuditService? financeAuditService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
            _settlementReadModelService = settlementReadModelService ?? throw new ArgumentNullException(nameof(settlementReadModelService));
            _financeAuditService = financeAuditService;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

        public async Task<SubledgerUnappliedSettlementReportDto> GetUnappliedSettlementsAsync(
            DateTime? asOfDate = null,
            Guid? supplierId = null,
            CancellationToken cancellationToken = default)
        {
            var date = asOfDate ?? DateTime.UtcNow;
            var rebuild = await _settlementReadModelService.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsPayable,
                AsOfDate = date,
                RecordAudit = false
            }, cancellationToken);

            var balances = (await _settlementReadModelService.GetUnappliedBalancesAsync(
                    SubledgerSettlementModules.AccountsPayable,
                    date,
                    supplierId,
                    cancellationToken))
                .Where(b => b.UnappliedAmount != 0m)
                .ToList();

            var supplierIds = balances.Select(b => b.CounterpartyId).Distinct().ToList();
            var supplierNames = await _unitOfWork.Repository<Supplier>()
                .GetQueryable(s => s.TenantId == TenantId && supplierIds.Contains(s.Id) && !s.IsDeleted)
                .Select(s => new { s.Id, s.Name })
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

            var report = new SubledgerUnappliedSettlementReportDto
            {
                SourceModule = SubledgerSettlementModules.AccountsPayable,
                AsOfDate = date,
                TotalUnappliedAmount = balances.Sum(b => b.UnappliedAmount),
                CounterpartyCount = balances.Select(b => b.CounterpartyId).Distinct().Count(),
                Diagnostics = rebuild.Diagnostics
            };

            report.Lines = balances.Select(b => new SubledgerUnappliedSettlementBalanceDto
            {
                Id = b.Id,
                SourceModule = b.SourceModule,
                CounterpartyId = b.CounterpartyId,
                CounterpartyName = supplierNames.GetValueOrDefault(b.CounterpartyId) ?? "Supplier",
                SettlementSourceType = b.SettlementSourceType,
                SettlementSourceId = b.SettlementSourceId,
                SettlementSourceNumber = b.SettlementSourceNumber,
                Classification = b.Classification,
                SettlementPostingEventId = b.SettlementPostingEventId,
                SettlementJournalEntryId = b.SettlementJournalEntryId,
                SettlementDate = b.SettlementDate,
                DocumentCurrencyCode = b.DocumentCurrencyCode,
                FunctionalCurrencyCode = b.FunctionalCurrencyCode,
                OriginalAmount = b.OriginalAmount,
                AppliedAmount = b.AppliedAmount,
                UnappliedAmount = b.UnappliedAmount,
                HasDiagnostics = b.HasDiagnostics,
                DiagnosticFlags = b.DiagnosticFlags
            }).ToList();

            await RecordReportAuditAsync(FinanceAuditEvents.ApUnappliedSettlementsGenerated, report, cancellationToken);
            return report;
        }

        // ═════════════════════════════════════════════════════════════════
        //  AGING REPORT
        // ═════════════════════════════════════════════════════════════════

        public async Task<ApAgingReportDto> GetAgingReportAsync(
            DateTime? asOfDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default)
        {
            // The constructor requires the projection service so production aging cannot
            // silently regress to mutable VendorInvoice.PaidAmount snapshots.
            if (_settlementReadModelService != null)
                return await GetSettlementReadModelAgingReportAsync(asOfDate, supplierId, includeInvoiceDetails: false, cancellationToken);

            var date = asOfDate ?? DateTime.UtcNow;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            queryable = ApplyPostedApInvoiceFilter(queryable);

            if (supplierId.HasValue)
                queryable = queryable.Where(i => i.BusinessPartnerId == supplierId.Value);

            var invoices = await queryable.ToListAsync(cancellationToken);
            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);

            var report = new ApAgingReportDto
            {
                AsOfDate = date,
                CurrencyCode = baseCurrencyCode
            };

            var detailBySupplier = new Dictionary<Guid, SupplierAgingDetailDto>();

            SupplierAgingDetailDto GetOrCreateDetail(Guid id, string name)
            {
                if (detailBySupplier.TryGetValue(id, out var existing))
                    return existing;

                var detail = new SupplierAgingDetailDto
                {
                    SupplierId = id,
                    SupplierName = name
                };
                detailBySupplier[id] = detail;
                report.SupplierDetails.Add(detail);
                return detail;
            }

            foreach (var invoice in invoices)
            {
                var detail = GetOrCreateDetail(invoice.BusinessPartnerId, invoice.SupplierName);
                var balance = invoice.TotalAmount - invoice.PaidAmount;
                AddToSupplierAgingBucket(detail, balance, invoice.DueDate, invoice.InvoiceDate, date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || invoice.InvoiceDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = invoice.InvoiceDate;
            }

            foreach (var adjustment in adjustments)
            {
                var amount = GetSignedApSubledgerAmount(adjustment);
                if (amount == 0)
                    continue;

                var detail = GetOrCreateDetail(adjustment.BusinessPartnerId, adjustment.BusinessPartnerName);
                AddToSupplierAgingBucket(detail, amount, adjustment.DueDate, adjustment.AdjustmentDate, date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || adjustment.AdjustmentDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = adjustment.AdjustmentDate;
            }

            report.SupplierDetails = report.SupplierDetails
                .Where(d => d.TotalOutstanding != 0)
                .OrderByDescending(d => d.TotalOutstanding)
                .ToList();
            report.TotalOutstanding = report.SupplierDetails.Sum(d => d.TotalOutstanding);
            report.Current = report.SupplierDetails.Sum(d => d.Current);
            report.ThirtyDays = report.SupplierDetails.Sum(d => d.ThirtyDays);
            report.SixtyDays = report.SupplierDetails.Sum(d => d.SixtyDays);
            report.NinetyPlusDays = report.SupplierDetails.Sum(d => d.NinetyPlusDays);
            report.TotalSuppliers = report.SupplierDetails.Count;
            report.TotalInvoices = invoices.Count;

            return report;
        }

        public async Task<ApAgingReportDto> GetDetailedAgingReportAsync(
            DateTime? asOfDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default)
        {
            // See GetAgingReportAsync: mandatory dependency prevents a silent legacy fallback.
            if (_settlementReadModelService != null)
                return await GetSettlementReadModelAgingReportAsync(asOfDate, supplierId, includeInvoiceDetails: true, cancellationToken);

            var report = await GetAgingReportAsync(asOfDate, supplierId, cancellationToken);
            var date = report.AsOfDate;

            // Reload invoices to populate per-invoice details
            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            queryable = ApplyPostedApInvoiceFilter(queryable);

            if (supplierId.HasValue)
                queryable = queryable.Where(i => i.BusinessPartnerId == supplierId.Value);

            var invoices = await queryable.ToListAsync(cancellationToken);
            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);
            var bySupplier = invoices.GroupBy(i => i.BusinessPartnerId).ToDictionary(g => g.Key, g => g.ToList());
            var adjustmentsBySupplier = adjustments
                .GroupBy(a => a.BusinessPartnerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var detail in report.SupplierDetails)
            {
                if (bySupplier.TryGetValue(detail.SupplierId, out var supplierInvoices))
                {
                    detail.Invoices = supplierInvoices.Select(i =>
                    {
                        var agingDate = i.DueDate ?? i.InvoiceDate;
                        var daysOutstanding = (int)(date - agingDate).TotalDays;
                        var bucket = daysOutstanding <= 30 ? "Current"
                                   : daysOutstanding <= 60 ? "31-60"
                                   : daysOutstanding <= 90 ? "61-90"
                                   : "90+";

                        return new ApAgingInvoiceDto
                        {
                            InvoiceId = i.Id,
                            InvoiceNumber = i.InvoiceNumber,
                            InvoiceDate = i.InvoiceDate,
                            DueDate = i.DueDate,
                            TotalAmount = i.TotalAmount,
                            BalanceAmount = i.TotalAmount - i.PaidAmount,
                            DaysOutstanding = Math.Max(0, daysOutstanding),
                            AgingBucket = bucket
                        };
                    }).ToList();
                }

                if (adjustmentsBySupplier.TryGetValue(detail.SupplierId, out var supplierAdjustments))
                {
                    detail.Invoices.AddRange(supplierAdjustments.Select(a =>
                    {
                        var amount = GetSignedApSubledgerAmount(a);
                        var agingDate = a.DueDate ?? a.AdjustmentDate;
                        var daysOutstanding = (int)(date - agingDate).TotalDays;
                        var bucket = daysOutstanding <= 30 ? "Current"
                                   : daysOutstanding <= 60 ? "31-60"
                                   : daysOutstanding <= 90 ? "61-90"
                                   : "90+";

                        return new ApAgingInvoiceDto
                        {
                            InvoiceId = a.Id,
                            InvoiceNumber = a.AdjustmentNumber,
                            InvoiceDate = a.AdjustmentDate,
                            DueDate = a.DueDate,
                            TotalAmount = amount,
                            BalanceAmount = amount,
                            DaysOutstanding = Math.Max(0, daysOutstanding),
                            AgingBucket = bucket
                        };
                    }));
                }

                detail.Invoices = detail.Invoices.OrderBy(i => i.DueDate ?? i.InvoiceDate).ToList();
            }

            return report;
        }

        public Task<SubledgerSettlementRebuildResultDto> RebuildSettlementReadModelAsync(
            DateTime? asOfDate = null,
            CancellationToken cancellationToken = default)
        {
            if (_settlementReadModelService == null)
            {
                throw new InvalidOperationException("AP settlement read-model service is not configured.");
            }

            return _settlementReadModelService.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsPayable,
                AsOfDate = asOfDate
            }, cancellationToken);
        }

        public Task<SubledgerControlReconciliationDto> GetControlReconciliationAsync(
            DateTime? asOfDate = null,
            CancellationToken cancellationToken = default)
        {
            if (_settlementReadModelService == null)
            {
                throw new InvalidOperationException("AP settlement read-model service is not configured.");
            }

            return _settlementReadModelService.GetControlReconciliationAsync(
                SubledgerSettlementModules.AccountsPayable,
                asOfDate,
                cancellationToken);
        }

        public async Task<ProcurementFinanceReconciliationReportDto> GetProcurementFinanceReconciliationAsync(
            DateTime? asOfDate = null,
            Guid? purchaseOrderId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var date = (asOfDate ?? DateTime.UtcNow).Date;
            var cutoffExclusive = date.AddDays(1);

            var purchaseOrderCandidates = await _unitOfWork.Repository<PurchaseOrder>()
                .GetQueryableIncludingDeleted(item =>
                    item.TenantId == tenantId &&
                    item.CreatedAt < cutoffExclusive &&
                    item.OrderDate < cutoffExclusive &&
                    (!item.IsDeleted || !item.DeletedAt.HasValue ||
                     item.DeletedAt.Value >= cutoffExclusive))
                .Include(item => item.Items.Where(line => !line.IsDeleted))
                .Include(item => item.Receipts.Where(receipt =>
                    !receipt.IsDeleted && receipt.ReceiptDate < cutoffExclusive))
                    .ThenInclude(receipt => receipt.Items.Where(line => !line.IsDeleted))
                .OrderBy(item => item.OrderNumber)
                .ToListAsync(cancellationToken);

            var purchaseOrderResourceIds = purchaseOrderCandidates
                .Select(item => item.Id.ToString())
                .ToList();
            var purchaseOrderAudits = purchaseOrderResourceIds.Count == 0
                ? new List<AuditLog>()
                : await _unitOfWork.Repository<AuditLog>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        !item.IsDeleted &&
                        (item.Resource == nameof(PurchaseOrder) ||
                         item.Resource == "Procurement.PurchaseOrder") &&
                        item.ResourceId != null &&
                        purchaseOrderResourceIds.Contains(item.ResourceId))
                    .AsNoTracking()
                    .OrderBy(item => item.Timestamp)
                    .ToListAsync(cancellationToken);
            var purchaseOrderStatesAsOf = purchaseOrderCandidates
                .Select(item => ResolvePurchaseOrderStateAsOf(
                    item,
                    purchaseOrderAudits.Where(audit => audit.ResourceId == item.Id.ToString()),
                    cutoffExclusive))
                .Where(state => state is not null && IsOperativePurchaseOrderStatus(state.Status))
                .Select(state => state!)
                .ToDictionary(state => state.Id);
            var purchaseOrders = purchaseOrderCandidates
                .Where(item => purchaseOrderStatesAsOf.ContainsKey(item.Id))
                .ToList();
            var operativePurchaseOrderIds = purchaseOrders.Select(item => item.Id).ToList();
            var purchaseOrderAmendments = operativePurchaseOrderIds.Count == 0
                ? new List<ProcurementPurchaseOrderAmendment>()
                : await _unitOfWork.Repository<ProcurementPurchaseOrderAmendment>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        operativePurchaseOrderIds.Contains(item.PurchaseOrderId) &&
                        item.Status == ProcurementPurchaseOrderAmendmentStatus.Applied &&
                        item.AppliedAtUtc.HasValue &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .OrderBy(item => item.AppliedAtUtc)
                    .ThenBy(item => item.AmendmentSequence)
                    .ToListAsync(cancellationToken);
            var purchaseOrderCommercialStates = purchaseOrders.ToDictionary(
                item => item.Id,
                item => ResolvePurchaseOrderCommercialStateAsOf(
                    item,
                    purchaseOrderAmendments.Where(amendment => amendment.PurchaseOrderId == item.Id),
                    cutoffExclusive));

            var selectedPurchaseOrders = purchaseOrderId.HasValue
                ? purchaseOrders.Where(item => item.Id == purchaseOrderId.Value).ToList()
                : purchaseOrders;
            if (purchaseOrderId.HasValue && selectedPurchaseOrders.Count == 0)
                throw new KeyNotFoundException(
                    $"Purchase order with Id '{purchaseOrderId.Value}' was not found for this tenant.");

            var selectedPoIds = selectedPurchaseOrders.Select(item => item.Id).ToList();
            var selectedReceipts = selectedPurchaseOrders
                .SelectMany(item => item.Receipts)
                .ToList();
            var selectedReceiptIds = selectedReceipts.Select(item => item.Id).ToList();
            var receiptItemsById = selectedReceipts
                .SelectMany(item => item.Items)
                .ToDictionary(item => item.Id);
            var receiptInspectionCases = selectedReceiptIds.Count == 0
                ? new List<ProcurementReceiptInspectionCase>()
                : await _unitOfWork.Repository<ProcurementReceiptInspectionCase>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        selectedReceiptIds.Contains(item.PurchaseOrderReceiptId) &&
                        !item.IsDeleted)
                    .Include(item => item.Lines.Where(line => !line.IsDeleted))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
            var governedReceiptIds = receiptInspectionCases
                .Select(item => item.PurchaseOrderReceiptId)
                .ToHashSet();
            var acceptedInspectionByReceiptAsOf = receiptInspectionCases
                .Where(item =>
                    item.StockPostedAtUtc.HasValue &&
                    item.StockPostedAtUtc.Value.Date <= date &&
                    ProcurementReceiptInspectionRules.IsApEligible(
                        item.Status,
                        item.PendingQuantity,
                        item.ApEligibleQuantity))
                .GroupBy(item => item.PurchaseOrderReceiptId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(item => item.Sequence).First());
            var sourceRequisitionIds = selectedPurchaseOrders
                .Select(item => purchaseOrderCommercialStates[item.Id].SourceRequisitionId)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .Distinct()
                .ToList();
            var contractIds = selectedPurchaseOrders
                .Where(item => item.ContractId.HasValue)
                .Select(item => item.ContractId!.Value)
                .Distinct()
                .ToList();

            var commitments = await _unitOfWork.Repository<ProcurementBudgetCommitment>()
                .GetQueryableIncludingDeleted(item =>
                    item.TenantId == tenantId &&
                    sourceRequisitionIds.Contains(item.PurchaseRequisitionId) &&
                    item.CreatedAt < cutoffExclusive &&
                    (!item.IsDeleted || !item.DeletedAt.HasValue ||
                     item.DeletedAt.Value >= cutoffExclusive))
                .ToListAsync(cancellationToken);
            var commitmentLifecycleActions = new[]
            {
                "BudgetCommitmentReserved",
                "BudgetReservationReused",
                "BudgetCommitmentReleased",
                "BudgetCommitmentConsumed"
            };
            var commitmentLifecycleEvents = sourceRequisitionIds.Count == 0
                ? new List<ProcurementControlEvent>()
                : await _unitOfWork.Repository<ProcurementControlEvent>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        !item.IsDeleted &&
                        item.EventType == "PurchaseRequisitionBudgetControl" &&
                        item.SourceId.HasValue &&
                        sourceRequisitionIds.Contains(item.SourceId.Value) &&
                        commitmentLifecycleActions.Contains(item.Action))
                    .AsNoTracking()
                    .OrderBy(item => item.OccurredAtUtc)
                    .ToListAsync(cancellationToken);
            var activeCommitmentsAsOf = commitments
                .Select(item => ResolveBudgetCommitmentStateAsOf(
                    item,
                    commitmentLifecycleEvents.Where(controlEvent =>
                        controlEvent.SourceId == item.PurchaseRequisitionId),
                    cutoffExclusive))
                .Where(item => item is not null &&
                               item.Status == ProcurementBudgetCommitmentStatus.Reserved)
                .Select(item => item!)
                .ToList();
            var allInvoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.PurchaseOrderId.HasValue &&
                    selectedPoIds.Contains(item.PurchaseOrderId.Value) &&
                    item.InvoiceDate < cutoffExclusive &&
                    item.SubmittedDate.HasValue &&
                    item.SubmittedDate.Value < cutoffExclusive &&
                    item.ApprovedDate.HasValue &&
                    item.ApprovedDate.Value < cutoffExclusive)
                .ToListAsync(cancellationToken);
            var invoiceIds = allInvoices.Select(item => item.Id).ToList();
            var invoiceResourceIds = invoiceIds.Select(item => item.ToString()).ToList();
            var invoiceTerminationsAsOf = invoiceResourceIds.Count == 0
                ? new HashSet<Guid>()
                : (await _unitOfWork.Repository<AuditLog>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        !item.IsDeleted &&
                        item.Resource == "Finance.APInvoice" &&
                        item.ResourceId != null &&
                        invoiceResourceIds.Contains(item.ResourceId) &&
                        item.Timestamp < cutoffExclusive &&
                        (item.Action == FinanceAuditEvents.ApInvoiceVoided ||
                         item.Action == FinanceAuditEvents.ApInvoiceReversed))
                    .AsNoTracking()
                    .Select(item => item.ResourceId!)
                    .ToListAsync(cancellationToken))
                    .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToHashSet();
            var allocations = await _unitOfWork.Repository<VendorPaymentAllocation>()
                .GetQueryable(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    invoiceIds.Contains(item.VendorInvoiceId) &&
                    item.AllocationDate.Date <= date)
                .Include(item => item.VendorPayment)
                .ToListAsync(cancellationToken);
            var contracts = await _unitOfWork.Repository<Contract>()
                .GetQueryable(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    contractIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            var contractValueAmendments = await _unitOfWork.Repository<ContractAmendment>()
                .GetQueryableIncludingDeleted(item =>
                    item.TenantId == tenantId &&
                    contractIds.Contains(item.ContractId) &&
                    item.Status == "Approved" &&
                    item.ApprovedDate.HasValue &&
                    item.AmendmentType == "ValueChange")
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var contractValuesAsOf = contracts.ToDictionary(
                contract => contract.Id,
                contract => ResolveContractValueAsOf(
                    contract,
                    contractValueAmendments.Where(item => item.ContractId == contract.Id),
                    cutoffExclusive));
            var milestones = await _unitOfWork.Repository<ContractMilestone>()
                .GetQueryableIncludingDeleted(item =>
                    item.TenantId == tenantId &&
                    contractIds.Contains(item.ContractId) &&
                    item.CreatedAt < cutoffExclusive &&
                    (!item.IsDeleted || !item.DeletedAt.HasValue || item.DeletedAt.Value >= cutoffExclusive))
                .ToListAsync(cancellationToken);
            var milestoneResourceIds = milestones.Select(item => item.Id.ToString()).ToList();
            var milestoneAuditSnapshots = milestoneResourceIds.Count == 0
                ? new List<AuditLog>()
                : await _unitOfWork.Repository<AuditLog>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        !item.IsDeleted &&
                        item.Resource == nameof(ContractMilestone) &&
                        item.ResourceId != null &&
                        milestoneResourceIds.Contains(item.ResourceId))
                    .AsNoTracking()
                    .OrderBy(item => item.Timestamp)
                    .ToListAsync(cancellationToken);
            var milestoneAuditsByResourceId = milestoneAuditSnapshots
                .ToLookup(item => item.ResourceId ?? string.Empty);
            var certificates = await _unitOfWork.Repository<ProjectPaymentCertificate>()
                .GetQueryableIncludingDeleted(item =>
                    item.TenantId == tenantId &&
                    item.ContractId.HasValue &&
                    contractIds.Contains(item.ContractId.Value) &&
                    item.CreatedAt < cutoffExclusive &&
                    item.IssueDate < cutoffExclusive &&
                    (!item.IsDeleted || !item.DeletedAt.HasValue || item.DeletedAt.Value >= cutoffExclusive))
                .ToListAsync(cancellationToken);
            var certificateResourceIds = certificates.Select(item => item.Id.ToString()).ToList();
            var certificateAuditSnapshots = certificateResourceIds.Count == 0
                ? new List<AuditLog>()
                : await _unitOfWork.Repository<AuditLog>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        !item.IsDeleted &&
                        item.Resource == ProjectPaymentCertificateAuditEvents.Resource &&
                        item.Action == ProjectPaymentCertificateAuditEvents.Snapshot &&
                        item.ResourceId != null &&
                        certificateResourceIds.Contains(item.ResourceId))
                    .AsNoTracking()
                    .OrderBy(item => item.Timestamp)
                    .ToListAsync(cancellationToken);
            var certificateStatesAsOf = certificates
                .Select(certificate => ResolvePaymentCertificateStateAsOf(
                    certificate,
                    certificateAuditSnapshots.Where(audit => audit.ResourceId == certificate.Id.ToString()),
                    cutoffExclusive))
                .Where(state => state is not null &&
                                !state.IsDeleted &&
                                state.IssueDate < cutoffExclusive &&
                                state.Status != ProjectPaymentCertificateStatuses.Draft &&
                                state.Status != ProjectPaymentCertificateStatuses.Cancelled)
                .Select(state => state!)
                .ToList();
            var retentionReleaseActions = await _unitOfWork.Repository<ProcurementWorksCloseoutAction>()
                .GetQueryable(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    contractIds.Contains(item.ContractId) &&
                    item.ActionType == ProcurementWorksCloseoutActionType.RetentionRelease &&
                    item.Status == ProcurementWorksCloseoutActionStatus.Approved &&
                    item.Amount.HasValue &&
                    (item.EffectiveAtUtc ?? item.DecidedAtUtc ?? item.SubmittedAtUtc) < cutoffExclusive)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var paymentIds = allocations.Select(item => item.VendorPaymentId).Distinct().ToList();
            var allocationIds = allocations.Select(item => item.Id).Distinct().ToList();
            var paymentAllocationHistory = paymentIds.Count == 0
                ? new List<VendorPaymentAllocation>()
                : await _unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        !item.IsDeleted &&
                        paymentIds.Contains(item.VendorPaymentId) &&
                        item.AllocationDate.Date <= date)
                    .Include(item => item.VendorPayment)
                    .ToListAsync(cancellationToken);
            var effectivePaymentSettlementById = GetEffectiveAllocationsAsOf(paymentAllocationHistory)
                .GroupBy(item => item.VendorPaymentId)
                .ToDictionary(
                    group => group.Key,
                    group => RoundMoney(group.Sum(item =>
                        item.AllocatedAmount + item.DiscountAmount + item.WithholdingTaxAmount)));
            var postingEvents = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.PostingStatus == "Posted" &&
                    item.PostingDate < cutoffExclusive &&
                    ((item.SourceDocumentType == "VendorInvoice" && invoiceIds.Contains(item.SourceDocumentId)) ||
                     (item.SourceDocumentType == "VendorPayment" && paymentIds.Contains(item.SourceDocumentId)) ||
                     (item.SourceDocumentType == "VendorPaymentAdvanceApplication" && allocationIds.Contains(item.SourceDocumentId)) ||
                     (item.SourceDocumentType == "VendorPaymentAllocation" && allocationIds.Contains(item.SourceDocumentId))))
                .Include(item => item.JournalEntry)
                    .ThenInclude(item => item!.Transactions)
                .ToListAsync(cancellationToken);

            var rows = new List<ProcurementFinanceReconciliationRowDto>();
            foreach (var purchaseOrder in selectedPurchaseOrders)
            {
                var commercialState = purchaseOrderCommercialStates[purchaseOrder.Id];
                var currency = NormalizeCurrency(commercialState.Currency, "GHS");
                var poInvoices = allInvoices
                    .Where(item => item.PurchaseOrderId == purchaseOrder.Id)
                    .ToList();
                var poInvoiceIds = poInvoices.Select(item => item.Id).ToHashSet();
                var poAllocations = allocations
                    .Where(item => poInvoiceIds.Contains(item.VendorInvoiceId))
                    .ToList();
                var effectivePoAllocations = GetEffectiveAllocationsAsOf(poAllocations);
                var poPaymentIds = poAllocations.Select(item => item.VendorPaymentId).ToHashSet();
                var poAllocationIds = poAllocations.Select(item => item.Id).ToHashSet();
                var poPostings = postingEvents
                    .Where(item =>
                        (item.SourceDocumentType == "VendorInvoice" && poInvoiceIds.Contains(item.SourceDocumentId)) ||
                        (item.SourceDocumentType == "VendorPayment" && poPaymentIds.Contains(item.SourceDocumentId)) ||
                        ((item.SourceDocumentType == "VendorPaymentAdvanceApplication" ||
                          item.SourceDocumentType == "VendorPaymentAllocation") &&
                         poAllocationIds.Contains(item.SourceDocumentId)))
                    .ToList();
                var reversedInvoiceIdsAsOf = poPostings
                    .Where(item =>
                        item.SourceDocumentType == "VendorInvoice" &&
                        item.PostingAction != "Post")
                    .Select(item => item.SourceDocumentId)
                    .ToHashSet();
                var activeInvoices = poInvoices
                    .Where(item => !invoiceTerminationsAsOf.Contains(item.Id) &&
                                   !reversedInvoiceIdsAsOf.Contains(item.Id))
                    .ToList();
                var activeInvoiceIds = activeInvoices.Select(item => item.Id).ToHashSet();

                var commitmentGroupOrders = commercialState.SourceRequisitionId.HasValue
                    ? purchaseOrders
                        .Select(item => purchaseOrderCommercialStates[item.Id])
                        .Where(item =>
                            item.SourceRequisitionId == commercialState.SourceRequisitionId &&
                            NormalizeCurrency(item.Currency, currency) == currency)
                        .ToList()
                    : new List<PurchaseOrderCommercialState>();
                var activeCommitments = commercialState.SourceRequisitionId.HasValue
                    ? activeCommitmentsAsOf.Where(item =>
                        item.PurchaseRequisitionId == commercialState.SourceRequisitionId.Value).ToList()
                    : new List<ProcurementBudgetCommitment>();
                var contractMilestones = purchaseOrder.ContractId.HasValue
                    ? milestones.Where(item => item.ContractId == purchaseOrder.ContractId.Value).ToList()
                    : new List<ContractMilestone>();
                var milestoneAmountsAsOf = purchaseOrder.ContractId.HasValue &&
                                           contractValuesAsOf.TryGetValue(
                                               purchaseOrder.ContractId.Value,
                                               out var contractValueAsOf)
                    ? contractMilestones.ToDictionary(
                        item => item.Id,
                        item => ResolveMilestoneAmountAsOf(
                            item,
                            milestoneAuditsByResourceId[item.Id.ToString()],
                            contractValueAsOf,
                            cutoffExclusive))
                    : new Dictionary<Guid, decimal>();
                var contractCertificates = purchaseOrder.ContractId.HasValue
                    ? certificateStatesAsOf.Where(item =>
                        item.ContractId == purchaseOrder.ContractId.Value &&
                        NormalizeCurrency(item.Currency, currency) == currency).ToList()
                    : new List<PaymentCertificateStateAsOf>();
                var controlledRetentionReleased = purchaseOrder.ContractId.HasValue
                    ? retentionReleaseActions
                        .Where(item =>
                            item.ContractId == purchaseOrder.ContractId.Value &&
                            NormalizeCurrency(item.Currency, currency) == currency)
                        .Sum(item => item.Amount ?? 0m)
                    : 0m;

                var itemPrices = commercialState.ItemUnitPrices;
                var acceptedReceiptValue = 0m;
                foreach (var receipt in purchaseOrder.Receipts.Where(receipt =>
                             receipt.Status != "Rejected" && receipt.Status != "Cancelled"))
                {
                    if (acceptedInspectionByReceiptAsOf.TryGetValue(receipt.Id, out var inspection))
                    {
                        acceptedReceiptValue += inspection.Lines.Sum(line =>
                            receiptItemsById.TryGetValue(line.PurchaseOrderReceiptItemId, out var receiptItem) &&
                            itemPrices.TryGetValue(receiptItem.PurchaseOrderItemId, out var unitPrice)
                                ? line.AcceptedQuantity * unitPrice
                                : 0m);
                        continue;
                    }

                    // Legacy receipts without a governed inspection case retain their
                    // explicit inspection timestamp. Never fall back to their current
                    // accepted quantity when a governed decision exists after the cutoff.
                    if (!governedReceiptIds.Contains(receipt.Id) &&
                        receipt.InspectionDate.HasValue &&
                        receipt.InspectionDate.Value.Date <= date)
                    {
                        acceptedReceiptValue += receipt.Items.Sum(item =>
                            itemPrices.TryGetValue(item.PurchaseOrderItemId, out var unitPrice)
                                ? item.AcceptedQuantity * unitPrice
                                : 0m);
                    }
                }
                var acceptedReceiptAmount = RoundMoney(acceptedReceiptValue);
                var settledAmount = RoundMoney(effectivePoAllocations
                    .Where(item => IsAllocationPostedAsOf(item, poPostings))
                    .Sum(item => item.AllocatedAmount + item.DiscountAmount + item.WithholdingTaxAmount));
                var invoicePostedAmount = RoundMoney(poPostings
                    .Where(item =>
                        item.SourceDocumentType == "VendorInvoice" &&
                        item.PostingAction == "Post" &&
                        activeInvoiceIds.Contains(item.SourceDocumentId) &&
                        GetActivePostingAsOf(poPostings, "VendorInvoice", item.SourceDocumentId)?.Id == item.Id)
                    .Sum(item => GetTransactionDebit(item, currency)));
                var activePoAllocations = effectivePoAllocations
                    .Where(item => IsAllocationPostedAsOf(item, poPostings))
                    .ToList();
                var paymentPostedAmount = RoundMoney(activePoAllocations
                    .GroupBy(item => item.VendorPaymentId)
                    .Sum(group =>
                    {
                        var payment = group.First().VendorPayment;
                        if (payment.IsSupplierAdvance)
                        {
                            var selectedAllocationIds = group.Select(item => item.Id).ToHashSet();
                            return selectedAllocationIds.Sum(allocationId =>
                            {
                                var applicationPosting = GetActivePostingAsOf(
                                        poPostings,
                                        "VendorPaymentAdvanceApplication",
                                        allocationId)
                                    ?? GetActivePostingAsOf(
                                        poPostings,
                                        "VendorPaymentAllocation",
                                        allocationId);
                                return applicationPosting == null
                                    ? 0m
                                    : GetTransactionDebit(applicationPosting, currency);
                            });
                        }

                        var paymentPosting = GetActivePostingAsOf(
                            poPostings,
                            "VendorPayment",
                            payment.Id);
                        if (paymentPosting == null) return 0m;

                        var paymentSettlement = effectivePaymentSettlementById
                            .GetValueOrDefault(payment.Id);
                        if (paymentSettlement <= 0m) return 0m;
                        var orderSettlement = RoundMoney(group.Sum(item =>
                            item.AllocatedAmount + item.DiscountAmount + item.WithholdingTaxAmount));
                        return RoundMoney(
                            GetTransactionDebit(paymentPosting, currency) * orderSettlement / paymentSettlement);
                    }));

                var row = new ProcurementFinanceReconciliationRowDto
                {
                    PurchaseOrderId = purchaseOrder.Id,
                    PurchaseOrderNumber = purchaseOrder.OrderNumber,
                    PurchaseOrderStatus = purchaseOrderStatesAsOf[purchaseOrder.Id].Status,
                    CurrencyCode = currency,
                    SourceRequisitionId = commercialState.SourceRequisitionId,
                    ContractId = purchaseOrder.ContractId,
                    PurchaseOrderAmount = RoundMoney(commercialState.TotalAmount),
                    CommitmentAmount = RoundMoney(activeCommitments
                        .Where(item => NormalizeCurrency(item.Currency, currency) == currency)
                        .Sum(item => item.ReservedAmount)),
                    CommitmentGroupOrderAmount = RoundMoney(commitmentGroupOrders.Sum(item => item.TotalAmount)),
                    AcceptedReceiptAmount = acceptedReceiptAmount,
                    InvoiceAmount = RoundMoney(activeInvoices.Sum(item => item.TotalAmount)),
                    SettledAmount = settledAmount,
                    InvoicePostedAmount = invoicePostedAmount,
                    PaymentPostedAmount = paymentPostedAmount,
                    RetentionHeldAmount = RoundMoney(contractCertificates.Sum(item => item.RetentionHeldAmount)),
                    RetentionReleasedAmount = RoundMoney(Math.Max(
                        contractCertificates.Sum(item => item.RetentionReleasedAmount),
                        controlledRetentionReleased)),
                    MilestoneAmount = RoundMoney(contractMilestones.Sum(item =>
                        milestoneAmountsAsOf.GetValueOrDefault(item.Id))),
                    CompletedMilestoneAmount = RoundMoney(contractMilestones
                        .Where(item => item.CompletedAt.HasValue && item.CompletedAt.Value < cutoffExclusive)
                        .Sum(item => milestoneAmountsAsOf.GetValueOrDefault(item.Id))),
                    InvoicedMilestoneAmount = RoundMoney(contractMilestones
                        .Where(item => item.InvoicedAt.HasValue && item.InvoicedAt.Value < cutoffExclusive)
                        .Sum(item => milestoneAmountsAsOf.GetValueOrDefault(item.Id))),
                    PaidMilestoneAmount = RoundMoney(contractMilestones
                        .Where(item => item.PaidAt.HasValue && item.PaidAt.Value < cutoffExclusive)
                        .Sum(item => milestoneAmountsAsOf.GetValueOrDefault(item.Id))),
                    InvoiceCount = activeInvoices.Count,
                    PaymentCount = activePoAllocations.Select(item => item.VendorPaymentId).Distinct().Count(),
                    PostingCount = poPostings.Count,
                    ControlledReversalCount = poPostings.Count(item => item.PostingAction != "Post")
                };
                row.RetentionOutstandingAmount = RoundMoney(
                    row.RetentionHeldAmount - row.RetentionReleasedAmount);

                var issues = BuildProcurementFinanceIssues(
                    purchaseOrder,
                    commercialState.SourceRequisitionId,
                    row,
                    activeCommitments,
                    contracts.FirstOrDefault(item => item.Id == purchaseOrder.ContractId),
                    activeInvoices,
                    poAllocations,
                    poPostings,
                    cutoffExclusive);
                row.Issues = issues;
                row.IsReconciled = issues.All(item => item.Severity != "Error");
                rows.Add(row);
            }

            var apControl = await GetControlReconciliationAsync(date, cancellationToken);
            var report = new ProcurementFinanceReconciliationReportDto
            {
                AsOfDate = date,
                GeneratedAtUtc = DateTime.UtcNow,
                DecisionKeys = Enumerable.Range(1, 14)
                    .Select(index => $"DEC-{index:000}")
                    .ToArray(),
                PurchaseOrderCount = rows.Count,
                IssueCount = rows.Sum(item => item.Issues.Count),
                UnbalancedPostingCount = postingEvents.Count(item =>
                    RoundMoney(item.TotalDebitAmount) != RoundMoney(item.TotalCreditAmount)),
                ControlledReversalCount = postingEvents.Count(item => item.PostingAction != "Post"),
                ApControlReconciliation = apControl,
                Rows = rows,
                CurrencySummaries = BuildProcurementFinanceCurrencySummaries(
                    rows,
                    activeCommitmentsAsOf)
            };
            report.IsReconciled =
                report.UnbalancedPostingCount == 0 &&
                Math.Abs(report.ApControlReconciliation.Variance) <= 0.01m &&
                rows.All(item => item.IsReconciled);

            await RecordReportAuditAsync(
                FinanceAuditEvents.ApProcurementReconciliationGenerated,
                report,
                cancellationToken);
            return report;
        }

        public async Task<byte[]> ExportProcurementFinanceReconciliationAsync(
            DateTime? asOfDate = null,
            Guid? purchaseOrderId = null,
            string format = "Csv",
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(format, "Csv", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Only CSV export is supported for procurement/Finance reconciliation.");

            var report = await GetProcurementFinanceReconciliationAsync(
                asOfDate, purchaseOrderId, cancellationToken);
            var builder = new StringBuilder();
            builder.AppendLine(
                "PurchaseOrder,Status,Currency,PO Amount,Commitment,Commitment Group Orders,Accepted Receipts,Invoices,Settled,Invoice GL,Payment GL,Retention Held,Retention Released,Milestones,Reversals,Reconciled,Issues");
            foreach (var row in report.Rows)
            {
                builder.AppendLine(string.Join(",",
                    Csv(row.PurchaseOrderNumber),
                    Csv(row.PurchaseOrderStatus),
                    Csv(row.CurrencyCode),
                    Csv(row.PurchaseOrderAmount),
                    Csv(row.CommitmentAmount),
                    Csv(row.CommitmentGroupOrderAmount),
                    Csv(row.AcceptedReceiptAmount),
                    Csv(row.InvoiceAmount),
                    Csv(row.SettledAmount),
                    Csv(row.InvoicePostedAmount),
                    Csv(row.PaymentPostedAmount),
                    Csv(row.RetentionHeldAmount),
                    Csv(row.RetentionReleasedAmount),
                    Csv(row.MilestoneAmount),
                    row.ControlledReversalCount,
                    row.IsReconciled,
                    Csv(string.Join(" | ", row.Issues.Select(item => $"{item.Code}: {item.Message}")))));
            }

            await RecordReportAuditAsync(
                FinanceAuditEvents.ApProcurementReconciliationExported,
                new
                {
                    report.AsOfDate,
                    report.PurchaseOrderCount,
                    report.IssueCount,
                    report.IsReconciled,
                    Format = "Csv"
                },
                cancellationToken);
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static IReadOnlyList<ProcurementFinanceReconciliationIssueDto> BuildProcurementFinanceIssues(
            PurchaseOrder purchaseOrder,
            Guid? sourceRequisitionId,
            ProcurementFinanceReconciliationRowDto row,
            IReadOnlyCollection<ProcurementBudgetCommitment> activeCommitments,
            Contract? contract,
            IReadOnlyCollection<VendorInvoice> invoices,
            IReadOnlyCollection<VendorPaymentAllocation> allocations,
            IReadOnlyCollection<FinancePostingEvent> postingEvents,
            DateTime cutoffExclusive)
        {
            var issues = new List<ProcurementFinanceReconciliationIssueDto>();
            void Add(
                string code,
                string area,
                string message,
                decimal? expected = null,
                decimal? actual = null,
                Guid? sourceDocumentId = null,
                string severity = "Error")
            {
                issues.Add(new ProcurementFinanceReconciliationIssueDto
                {
                    Code = code,
                    Severity = severity,
                    Area = area,
                    Message = message,
                    ExpectedAmount = expected,
                    ActualAmount = actual,
                    VarianceAmount = expected.HasValue && actual.HasValue
                        ? RoundMoney(actual.Value - expected.Value)
                        : null,
                    SourceDocumentId = sourceDocumentId
                });
            }

            if (sourceRequisitionId.HasValue)
            {
                if (activeCommitments.Count == 0)
                {
                    Add(
                        "COMMITMENT_MISSING",
                        "Commitment",
                        "The governed purchase-order requisition has no active budget commitment.",
                        row.CommitmentGroupOrderAmount,
                        0m,
                        sourceRequisitionId);
                }
                else
                {
                    var mismatchedCurrency = activeCommitments.FirstOrDefault(item =>
                        NormalizeCurrency(item.Currency, row.CurrencyCode) != row.CurrencyCode);
                    if (mismatchedCurrency != null)
                    {
                        Add(
                            "COMMITMENT_CURRENCY_MISMATCH",
                            "Commitment",
                            $"Commitment currency {mismatchedCurrency.Currency} does not match purchase-order currency {row.CurrencyCode}.",
                            sourceDocumentId: mismatchedCurrency.Id);
                    }

                    if (Math.Abs(row.CommitmentAmount - row.CommitmentGroupOrderAmount) > 0.01m)
                    {
                        Add(
                            "COMMITMENT_ORDER_VARIANCE",
                            "Commitment",
                            "The active requisition commitment does not equal the operative purchase-order family amount.",
                            row.CommitmentGroupOrderAmount,
                            row.CommitmentAmount,
                            sourceRequisitionId);
                    }
                }
            }

            if (row.AcceptedReceiptAmount > row.PurchaseOrderAmount + 0.01m)
            {
                Add(
                    "RECEIPT_EXCEEDS_ORDER",
                    "Receipt",
                    "Accepted receipt value exceeds the purchase-order amount.",
                    row.PurchaseOrderAmount,
                    row.AcceptedReceiptAmount,
                    purchaseOrder.Id);
            }
            if (row.InvoiceAmount > row.AcceptedReceiptAmount + 0.01m)
            {
                Add(
                    row.AcceptedReceiptAmount <= 0.01m
                        ? "INVOICE_WITHOUT_ACCEPTED_RECEIPT"
                        : "INVOICE_EXCEEDS_ACCEPTED_RECEIPT",
                    "AP",
                    "Active AP invoice value exceeds cumulative accepted receipt value.",
                    row.AcceptedReceiptAmount,
                    row.InvoiceAmount,
                    purchaseOrder.Id);
            }
            if (row.InvoiceAmount > row.PurchaseOrderAmount + 0.01m)
            {
                Add(
                    "INVOICE_EXCEEDS_ORDER",
                    "AP",
                    "Active AP invoice value exceeds the purchase-order amount.",
                    row.PurchaseOrderAmount,
                    row.InvoiceAmount,
                    purchaseOrder.Id);
            }
            if (row.SettledAmount > row.InvoiceAmount + 0.01m)
            {
                Add(
                    "PAYMENT_EXCEEDS_INVOICE",
                    "Settlement",
                    "Posted AP settlement exceeds active AP invoice value.",
                    row.InvoiceAmount,
                    row.SettledAmount,
                    purchaseOrder.Id);
            }
            if (Math.Abs(row.InvoicePostedAmount - row.InvoiceAmount) > 0.01m)
            {
                Add(
                    "INVOICE_GL_VARIANCE",
                    "GL",
                    "Active AP invoice value does not equal its active central-Finance posting amount.",
                    row.InvoiceAmount,
                    row.InvoicePostedAmount,
                    purchaseOrder.Id);
            }
            if (Math.Abs(row.PaymentPostedAmount - row.SettledAmount) > 0.01m)
            {
                Add(
                    "PAYMENT_GL_VARIANCE",
                    "GL",
                    "Posted AP settlement does not equal the active central-Finance payment or advance-application amount traced to this purchase order.",
                    row.SettledAmount,
                    row.PaymentPostedAmount,
                    purchaseOrder.Id);
            }

            foreach (var invoice in invoices)
            {
                if (NormalizeCurrency(invoice.CurrencyCode, row.CurrencyCode) != row.CurrencyCode)
                {
                    Add(
                        "INVOICE_CURRENCY_MISMATCH",
                        "AP",
                        $"Invoice {invoice.InvoiceNumber} currency {invoice.CurrencyCode} does not match purchase-order currency {row.CurrencyCode}.",
                        sourceDocumentId: invoice.Id);
                }

                var post = postingEvents.FirstOrDefault(item =>
                    item.SourceDocumentType == "VendorInvoice" &&
                    item.SourceDocumentId == invoice.Id &&
                    item.PostingAction == "Post");
                var reversedAsOf = postingEvents.Any(item =>
                    item.SourceDocumentType == "VendorInvoice" &&
                    item.SourceDocumentId == invoice.Id &&
                    item.PostingAction != "Post");
                var voidedAsOf = invoice.Status == VendorInvoiceStatus.Voided &&
                                 (!invoice.UpdatedAt.HasValue || invoice.UpdatedAt.Value < cutoffExclusive);
                if (voidedAsOf && post != null && !reversedAsOf)
                {
                    Add(
                        "VOIDED_INVOICE_REVERSAL_MISSING",
                        "Reversal",
                        $"Voided invoice {invoice.InvoiceNumber} has no controlled GL reversal.",
                        sourceDocumentId: invoice.Id);
                }
                else if (!voidedAsOf && reversedAsOf)
                {
                    Add(
                        "ACTIVE_INVOICE_HAS_REVERSED_GL",
                        "Reversal",
                        $"Active invoice {invoice.InvoiceNumber} points to a reversed GL journal.",
                        sourceDocumentId: invoice.Id);
                }
            }

            foreach (var payment in allocations
                         .Where(item => !item.IsReversal && item.VendorPayment != null)
                         .Select(item => item.VendorPayment)
                         .DistinctBy(item => item.Id))
            {
                var post = postingEvents.FirstOrDefault(item =>
                    item.SourceDocumentType == "VendorPayment" &&
                    item.SourceDocumentId == payment.Id &&
                    item.PostingAction == "Post");
                var reversedAsOf = postingEvents.Any(item =>
                    item.SourceDocumentType == "VendorPayment" &&
                    item.SourceDocumentId == payment.Id &&
                    item.PostingAction != "Post");
                var voidedAsOf = payment.Status == VendorPaymentStatus.Voided &&
                                 (!payment.UpdatedAt.HasValue || payment.UpdatedAt.Value < cutoffExclusive);
                if (voidedAsOf && post != null && !reversedAsOf)
                {
                    Add(
                        "VOIDED_PAYMENT_REVERSAL_MISSING",
                        "Reversal",
                        $"Voided payment {payment.PaymentNumber} has no controlled GL reversal.",
                        sourceDocumentId: payment.Id);
                }
                else if (!voidedAsOf && reversedAsOf)
                {
                    Add(
                        "ACTIVE_PAYMENT_HAS_REVERSED_GL",
                        "Reversal",
                        $"Active payment {payment.PaymentNumber} points to a reversed GL journal.",
                        sourceDocumentId: payment.Id);
                }

                if (voidedAsOf)
                {
                    foreach (var original in allocations.Where(item =>
                                 item.VendorPaymentId == payment.Id && !item.IsReversal))
                    {
                        if (!allocations.Any(item =>
                                item.IsReversal && item.OriginalAllocationId == original.Id))
                        {
                            Add(
                                "VOIDED_PAYMENT_ALLOCATION_REVERSAL_MISSING",
                                "Settlement",
                                $"Voided payment {payment.PaymentNumber} has an unreversed allocation.",
                                sourceDocumentId: original.Id);
                        }
                    }
                }
            }

            foreach (var posting in postingEvents.Where(item =>
                         RoundMoney(item.TotalDebitAmount) != RoundMoney(item.TotalCreditAmount)))
            {
                Add(
                    "UNBALANCED_FINANCE_POSTING",
                    "GL",
                    $"Finance posting {posting.Id} is not balanced.",
                    posting.TotalCreditAmount,
                    posting.TotalDebitAmount,
                    posting.Id);
            }

            if (row.RetentionReleasedAmount > row.RetentionHeldAmount + 0.01m)
            {
                Add(
                    "RETENTION_RELEASE_EXCEEDS_HELD",
                    "Retention",
                    "Released retention exceeds retention held on operative payment certificates.",
                    row.RetentionHeldAmount,
                    row.RetentionReleasedAmount,
                    purchaseOrder.ContractId);
            }
            if (contract != null && NormalizeCurrency(contract.Currency, row.CurrencyCode) != row.CurrencyCode)
            {
                Add(
                    "CONTRACT_CURRENCY_MISMATCH",
                    "Milestone",
                    $"Contract currency {contract.Currency} does not match purchase-order currency {row.CurrencyCode}.",
                    sourceDocumentId: contract.Id);
            }
            if (row.PaidMilestoneAmount > row.SettledAmount + 0.01m)
            {
                Add(
                    "PAID_MILESTONE_EXCEEDS_SETTLEMENT",
                    "Milestone",
                    "Milestones marked Paid exceed posted AP settlement traced to this purchase order.",
                    row.SettledAmount,
                    row.PaidMilestoneAmount,
                    purchaseOrder.ContractId,
                    severity: "Warning");
            }

            return issues;
        }

        private static decimal GetTransactionDebit(FinancePostingEvent postingEvent, string currencyCode)
        {
            if (postingEvent.JournalEntry?.Transactions == null)
                return postingEvent.TotalDebitAmount;

            var transactions = postingEvent.JournalEntry.Transactions
                .Where(item =>
                    string.Equals(
                        NormalizeCurrency(item.TransactionCurrency, currencyCode),
                        currencyCode,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
            return transactions.Count == 0
                ? postingEvent.TotalDebitAmount
                : RoundMoney(transactions.Sum(item => item.TransactionDebitAmount ?? item.DebitAmount));
        }

        private static IReadOnlyList<ProcurementFinanceReconciliationCurrencySummaryDto>
            BuildProcurementFinanceCurrencySummaries(
                IReadOnlyCollection<ProcurementFinanceReconciliationRowDto> rows,
                IReadOnlyCollection<ProcurementBudgetCommitment> commitments)
        {
            var representedRequisitions = rows
                .Where(item => item.SourceRequisitionId.HasValue)
                .Select(item => item.SourceRequisitionId!.Value)
                .ToHashSet();
            var currencies = rows.Select(item => item.CurrencyCode)
                .Concat(commitments
                    .Where(item => representedRequisitions.Contains(item.PurchaseRequisitionId))
                    .Select(item => NormalizeCurrency(item.Currency, "GHS")))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToList();

            return currencies.Select(currency =>
            {
                var currencyRows = rows.Where(item => item.CurrencyCode == currency).ToList();
                return new ProcurementFinanceReconciliationCurrencySummaryDto
                {
                    CurrencyCode = currency,
                    PurchaseOrderAmount = RoundMoney(currencyRows.Sum(item => item.PurchaseOrderAmount)),
                    CommitmentAmount = RoundMoney(commitments
                        .Where(item =>
                            representedRequisitions.Contains(item.PurchaseRequisitionId) &&
                            NormalizeCurrency(item.Currency, currency) == currency)
                        .Sum(item => item.ReservedAmount)),
                    AcceptedReceiptAmount = RoundMoney(currencyRows.Sum(item => item.AcceptedReceiptAmount)),
                    InvoiceAmount = RoundMoney(currencyRows.Sum(item => item.InvoiceAmount)),
                    SettledAmount = RoundMoney(currencyRows.Sum(item => item.SettledAmount)),
                    InvoicePostedAmount = RoundMoney(currencyRows.Sum(item => item.InvoicePostedAmount)),
                    PaymentPostedAmount = RoundMoney(currencyRows.Sum(item => item.PaymentPostedAmount)),
                    RetentionHeldAmount = RoundMoney(currencyRows.Sum(item => item.RetentionHeldAmount)),
                    RetentionReleasedAmount = RoundMoney(currencyRows.Sum(item => item.RetentionReleasedAmount)),
                    MilestoneAmount = RoundMoney(currencyRows.Sum(item => item.MilestoneAmount))
                };
            }).ToList();
        }

        private static FinancePostingEvent? GetActivePostingAsOf(
            IEnumerable<FinancePostingEvent> postingEvents,
            string sourceDocumentType,
            Guid sourceDocumentId)
        {
            var sourceEvents = postingEvents
                .Where(item =>
                    item.SourceDocumentType == sourceDocumentType &&
                    item.SourceDocumentId == sourceDocumentId)
                .ToList();
            if (sourceEvents.Any(item => item.PostingAction != "Post"))
                return null;
            return sourceEvents
                .Where(item => item.PostingAction == "Post")
                .OrderByDescending(item => item.PostingDate)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefault();
        }

        private static bool IsAllocationPostedAsOf(
            VendorPaymentAllocation allocation,
            IEnumerable<FinancePostingEvent> postingEvents)
        {
            if (allocation.VendorPayment?.IsSupplierAdvance == true)
            {
                return GetActivePostingAsOf(
                           postingEvents,
                           "VendorPaymentAdvanceApplication",
                           allocation.Id) != null ||
                       GetActivePostingAsOf(
                           postingEvents,
                           "VendorPaymentAllocation",
                           allocation.Id) != null;
            }

            return GetActivePostingAsOf(
                postingEvents,
                "VendorPayment",
                allocation.VendorPaymentId) != null;
        }

        private static List<VendorPaymentAllocation> GetEffectiveAllocationsAsOf(
            IEnumerable<VendorPaymentAllocation> allocations)
        {
            var allocationHistory = allocations.ToList();
            var reversedOriginalIds = allocationHistory
                .Where(item => item.IsReversal && item.OriginalAllocationId.HasValue)
                .Select(item => item.OriginalAllocationId!.Value)
                .ToHashSet();

            return allocationHistory
                .Where(item => !item.IsReversal && !reversedOriginalIds.Contains(item.Id))
                .ToList();
        }

        private static decimal ResolveContractValueAsOf(
            Contract contract,
            IEnumerable<ContractAmendment> amendments,
            DateTime cutoffExclusive)
        {
            var ordered = amendments
                .Where(item =>
                    item.Status == "Approved" &&
                    item.ApprovedDate.HasValue &&
                    item.AmendmentType == "ValueChange")
                .OrderBy(item => item.ApprovedDate)
                .ThenBy(item => item.SequenceNumber)
                .ToList();

            // Contract approval applies NewValue and recalculates every milestone in-place.
            // The first later amendment therefore carries the exact value immediately before
            // it, while the latest amendment at the cutoff carries the effective new value.
            var firstAfterCutoff = ordered.FirstOrDefault(item =>
                item.ApprovedDate!.Value >= cutoffExclusive && item.PreviousValue.HasValue);
            if (firstAfterCutoff?.PreviousValue is decimal precedingValue)
                return precedingValue;

            var latestAtCutoff = ordered.LastOrDefault(item =>
                item.ApprovedDate!.Value < cutoffExclusive && item.NewValue.HasValue);
            return latestAtCutoff?.NewValue ?? contract.ContractValue;
        }

        private static decimal ResolveMilestoneAmountAsOf(
            ContractMilestone milestone,
            IEnumerable<AuditLog> auditSnapshots,
            decimal contractValueAsOf,
            DateTime cutoffExclusive)
        {
            var snapshots = auditSnapshots.OrderBy(item => item.Timestamp).ToList();
            decimal? amount = null;
            foreach (var snapshot in snapshots.Where(item => item.Timestamp < cutoffExclusive))
            {
                if (TryReadDecimalProperty(
                        snapshot.NewValues,
                        nameof(ContractMilestone.PaymentAmount),
                        out var persistedAmount))
                    amount = persistedAmount;
            }

            if (amount.HasValue)
                return RoundMoney(amount.Value);

            // The first mutation after the cutoff retains the state that immediately
            // preceded it. This covers a later UpdateMilestoneAsync percentage change
            // as well as a later contract-value recalculation of the same milestone.
            var firstAfterCutoff = snapshots.FirstOrDefault(item =>
                item.Timestamp >= cutoffExclusive &&
                TryReadDecimalProperty(
                    item.OldValues,
                    nameof(ContractMilestone.PaymentAmount),
                    out _));
            if (firstAfterCutoff is not null &&
                TryReadDecimalProperty(
                    firstAfterCutoff.OldValues,
                    nameof(ContractMilestone.PaymentAmount),
                    out var precedingAmount))
                return RoundMoney(precedingAmount);

            // Compatibility fallback for legacy milestones that predate generic audit
            // snapshots. The effective-dated contract value still prevents a later
            // contract amendment from rewriting the earlier report.
            return RoundMoney(contractValueAsOf * milestone.PaymentPercentage / 100m);
        }

        private static PurchaseOrderCommercialState ResolvePurchaseOrderCommercialStateAsOf(
            PurchaseOrder purchaseOrder,
            IEnumerable<ProcurementPurchaseOrderAmendment> amendments,
            DateTime cutoffExclusive)
        {
            var ordered = amendments
                .Where(item =>
                    item.Status == ProcurementPurchaseOrderAmendmentStatus.Applied &&
                    item.AppliedAtUtc.HasValue)
                .OrderBy(item => item.AppliedAtUtc)
                .ThenBy(item => item.AmendmentSequence)
                .ToList();
            var firstAfterCutoff = ordered.FirstOrDefault(item =>
                item.AppliedAtUtc!.Value >= cutoffExclusive);
            var effectiveAmendment = firstAfterCutoff ?? ordered.LastOrDefault(item =>
                item.AppliedAtUtc!.Value < cutoffExclusive);
            if (effectiveAmendment == null)
            {
                return new PurchaseOrderCommercialState(
                    purchaseOrder.Id,
                    purchaseOrder.SourceRequisitionId,
                    purchaseOrder.Currency,
                    purchaseOrder.TotalAmount,
                    purchaseOrder.Items
                        .Where(item => !item.IsDeleted)
                        .ToDictionary(item => item.Id, item => item.UnitPrice));
            }

            var useBefore = firstAfterCutoff != null;
            var snapshotJson = useBefore
                ? effectiveAmendment.BeforeSnapshotJson
                : effectiveAmendment.ProposedSnapshotJson;
            var expectedHash = useBefore
                ? effectiveAmendment.BeforeIntegrityHash
                : effectiveAmendment.ProposedIntegrityHash;
            if (string.IsNullOrWhiteSpace(snapshotJson) ||
                string.IsNullOrWhiteSpace(expectedHash) ||
                !string.Equals(HashJson(snapshotJson), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"PO_AMENDMENT_SNAPSHOT_INVALID: amendment {effectiveAmendment.Id} has no trustworthy commercial snapshot.");

            PurchaseOrderCommercialSnapshot? snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<PurchaseOrderCommercialSnapshot>(
                    snapshotJson,
                    PurchaseOrderSnapshotJsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"PO_AMENDMENT_SNAPSHOT_INVALID: amendment {effectiveAmendment.Id} has an unreadable commercial snapshot.",
                    exception);
            }
            if (snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot.Currency) ||
                snapshot.TotalAmount < 0)
                throw new InvalidOperationException(
                    $"PO_AMENDMENT_SNAPSHOT_INVALID: amendment {effectiveAmendment.Id} has an incomplete commercial snapshot.");

            Dictionary<Guid, decimal> prices;
            try
            {
                prices = snapshot.Items
                    .Where(item => item.PurchaseOrderItemId.HasValue)
                    .ToDictionary(
                        item => item.PurchaseOrderItemId!.Value,
                        item => item.UnitPrice);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"PO_AMENDMENT_SNAPSHOT_INVALID: amendment {effectiveAmendment.Id} repeats a purchase-order line.",
                    exception);
            }

            // A proposed amendment snapshot deliberately has no database line ID
            // for lines introduced by that amendment. Once applied, those IDs live
            // on the current PO. With no later amendment, merge only those missing
            // IDs from the protected current state; when a later amendment exists,
            // its before-snapshot already carries the generated IDs.
            if (!useBefore)
            {
                foreach (var item in purchaseOrder.Items.Where(item => !item.IsDeleted))
                {
                    prices.TryAdd(item.Id, item.UnitPrice);
                }
            }

            return new PurchaseOrderCommercialState(
                purchaseOrder.Id,
                snapshot.SourceRequisitionId == Guid.Empty
                    ? null
                    : snapshot.SourceRequisitionId,
                snapshot.Currency,
                snapshot.TotalAmount,
                prices);
        }

        private static string HashJson(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
                .ToLowerInvariant();

        private static PurchaseOrderStateAsOf? ResolvePurchaseOrderStateAsOf(
            PurchaseOrder purchaseOrder,
            IEnumerable<AuditLog> auditSnapshots,
            DateTime cutoffExclusive)
        {
            if (purchaseOrder.CreatedAt >= cutoffExclusive)
                return null;

            var snapshots = auditSnapshots.OrderBy(item => item.Timestamp).ToList();
            string? status = null;
            foreach (var snapshot in snapshots.Where(item => item.Timestamp < cutoffExclusive))
            {
                if (TryReadStringProperty(snapshot.NewValues, nameof(PurchaseOrder.Status), out var persistedStatus))
                    status = persistedStatus;
            }

            var statusFromAudit = !string.IsNullOrWhiteSpace(status);
            if (!statusFromAudit)
            {
                foreach (var snapshot in snapshots.Where(item => item.Timestamp >= cutoffExclusive))
                {
                    if (!TryReadStringProperty(snapshot.OldValues, nameof(PurchaseOrder.Status), out var precedingStatus))
                        continue;
                    status = precedingStatus;
                    statusFromAudit = true;
                    break;
                }
            }

            if (!statusFromAudit)
            {
                status = purchaseOrder.Status;

                // ApprovedAt is the authoritative lifecycle boundary for records
                // whose approval occurred after the reporting cutoff. Legacy rows
                // without an approval timestamp retain their persisted state.
                if (purchaseOrder.ApprovedAt.HasValue &&
                    purchaseOrder.ApprovedAt.Value >= cutoffExclusive)
                {
                    status = "Draft";
                }
                else if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                         ((purchaseOrder.CancelledAtUtc.HasValue &&
                           purchaseOrder.CancelledAtUtc.Value >= cutoffExclusive) ||
                          (!purchaseOrder.CancelledAtUtc.HasValue &&
                           purchaseOrder.UpdatedAt.HasValue &&
                           purchaseOrder.UpdatedAt.Value >= cutoffExclusive)))
                {
                    // The cancellation lifecycle timestamp is authoritative. UpdatedAt
                    // remains a compatibility fallback for rows cancelled before the
                    // timestamp column was introduced and backfilled.
                    status = "Approved";
                }
            }

            return new PurchaseOrderStateAsOf(purchaseOrder.Id, status ?? "Draft");
        }

        private static ProcurementBudgetCommitment? ResolveBudgetCommitmentStateAsOf(
            ProcurementBudgetCommitment commitment,
            IEnumerable<ProcurementControlEvent> lifecycleEvents,
            DateTime cutoffExclusive)
        {
            var events = lifecycleEvents
                .OrderBy(item => item.OccurredAtUtc)
                .ThenBy(item => item.CreatedAt)
                .ToList();
            var latestAtCutoff = events.LastOrDefault(item => item.OccurredAtUtc < cutoffExclusive);
            if (TryReadBudgetCommitmentSnapshot(latestAtCutoff?.AfterJson, out var persistedAtCutoff))
                return persistedAtCutoff;

            // The first lifecycle event after the cutoff carries the authoritative
            // state immediately before that mutation. This preserves both status and
            // reserved amount across later release/re-reservation cycles.
            var firstAfterCutoff = events.FirstOrDefault(item => item.OccurredAtUtc >= cutoffExclusive);
            if (TryReadBudgetCommitmentSnapshot(firstAfterCutoff?.BeforeJson, out var precedingState))
                return precedingState;

            if (commitment.ReservedAtUtc >= cutoffExclusive)
                return null;

            if (commitment.Status == ProcurementBudgetCommitmentStatus.Reserved)
                return CopyBudgetCommitment(commitment, ProcurementBudgetCommitmentStatus.Reserved);

            var terminalAt = commitment.Status == ProcurementBudgetCommitmentStatus.Consumed
                ? commitment.ConsumedAtUtc ?? commitment.UpdatedAt
                : commitment.ReleasedAtUtc ?? commitment.UpdatedAt;
            return terminalAt.HasValue && terminalAt.Value >= cutoffExclusive
                ? CopyBudgetCommitment(commitment, ProcurementBudgetCommitmentStatus.Reserved)
                : CopyBudgetCommitment(commitment, commitment.Status);
        }

        private static bool TryReadBudgetCommitmentSnapshot(
            string? json,
            out ProcurementBudgetCommitment? commitment)
        {
            commitment = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var snapshot = JsonSerializer.Deserialize<BudgetCommitmentSnapshot>(
                    json,
                    BudgetCommitmentSnapshotJsonOptions);
                if (snapshot == null || !TryReadBudgetCommitmentStatus(snapshot.Status, out var status))
                {
                    return false;
                }

                commitment = new ProcurementBudgetCommitment
                {
                    Id = snapshot.Id,
                    ProcurementBudgetId = snapshot.ProcurementBudgetId,
                    PurchaseRequisitionId = snapshot.PurchaseRequisitionId,
                    ReservationReference = snapshot.ReservationReference,
                    ReservationSequence = snapshot.ReservationSequence,
                    Status = status,
                    ReservedAmount = snapshot.ReservedAmount,
                    Currency = snapshot.Currency,
                    BudgetCommittedBefore = snapshot.BudgetCommittedBefore,
                    BudgetAvailableBefore = snapshot.BudgetAvailableBefore,
                    BudgetCommittedAfter = snapshot.BudgetCommittedAfter,
                    BudgetAvailableAfter = snapshot.BudgetAvailableAfter,
                    IsOverride = snapshot.IsOverride,
                    OverrideRuleCode = snapshot.OverrideRuleCode,
                    OverrideApprovalReference = snapshot.OverrideApprovalReference,
                    ReservedAtUtc = snapshot.ReservedAtUtc,
                    ReleasedAtUtc = snapshot.ReleasedAtUtc,
                    ConsumedAtUtc = snapshot.ConsumedAtUtc,
                    ReleaseReason = snapshot.ReleaseReason
                };
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadBudgetCommitmentStatus(
            JsonElement value,
            out ProcurementBudgetCommitmentStatus status)
        {
            status = default;
            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var numericStatus) &&
                Enum.IsDefined(typeof(ProcurementBudgetCommitmentStatus), numericStatus))
            {
                status = (ProcurementBudgetCommitmentStatus)numericStatus;
                return true;
            }

            return value.ValueKind == JsonValueKind.String &&
                   Enum.TryParse(value.GetString(), ignoreCase: true, out status) &&
                   Enum.IsDefined(typeof(ProcurementBudgetCommitmentStatus), status);
        }

        private static ProcurementBudgetCommitment CopyBudgetCommitment(
            ProcurementBudgetCommitment source,
            ProcurementBudgetCommitmentStatus status) => new()
        {
            Id = source.Id,
            TenantId = source.TenantId,
            ProcurementBudgetId = source.ProcurementBudgetId,
            PurchaseRequisitionId = source.PurchaseRequisitionId,
            ReservationReference = source.ReservationReference,
            ReservationSequence = source.ReservationSequence,
            Status = status,
            ReservedAmount = source.ReservedAmount,
            Currency = source.Currency,
            BudgetCommittedBefore = source.BudgetCommittedBefore,
            BudgetAvailableBefore = source.BudgetAvailableBefore,
            BudgetCommittedAfter = source.BudgetCommittedAfter,
            BudgetAvailableAfter = source.BudgetAvailableAfter,
            IsOverride = source.IsOverride,
            OverrideRuleCode = source.OverrideRuleCode,
            OverrideApprovalReference = source.OverrideApprovalReference,
            ReservedAtUtc = source.ReservedAtUtc,
            ReleasedAtUtc = source.ReleasedAtUtc,
            ConsumedAtUtc = source.ConsumedAtUtc,
            ReleaseReason = source.ReleaseReason,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };

        private static bool IsOperativePurchaseOrderStatus(string status) =>
            !string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Pending Approval", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "PendingApproval", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);

        private static bool TryReadStringProperty(
            string? json,
            string propertyName,
            out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return false;
                var property = document.RootElement.EnumerateObject()
                    .FirstOrDefault(item => string.Equals(
                        item.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase));
                if (property.Value.ValueKind != JsonValueKind.String)
                    return false;
                value = property.Value.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadDecimalProperty(
            string? json,
            string propertyName,
            out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return false;
                var property = document.RootElement.EnumerateObject()
                    .FirstOrDefault(item => string.Equals(
                        item.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase));
                if (property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetDecimal(out value))
                    return true;
                if (property.Value.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(
                        property.Value.GetString(),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out value))
                    return true;
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static PaymentCertificateStateAsOf? ResolvePaymentCertificateStateAsOf(
            ProjectPaymentCertificate certificate,
            IEnumerable<AuditLog> auditSnapshots,
            DateTime cutoffExclusive)
        {
            var snapshots = auditSnapshots.OrderBy(item => item.Timestamp).ToList();
            var latestAtCutoff = snapshots.LastOrDefault(item => item.Timestamp < cutoffExclusive);
            if (TryReadPaymentCertificateSnapshot(latestAtCutoff?.NewValues, out var persistedAtCutoff))
                return persistedAtCutoff;

            // The first mutation after the cutoff contains the authoritative state that
            // immediately preceded it, which also supports certificates created before
            // snapshot auditing was introduced.
            var firstAfterCutoff = snapshots.FirstOrDefault(item => item.Timestamp >= cutoffExclusive);
            if (TryReadPaymentCertificateSnapshot(firstAfterCutoff?.OldValues, out var precedingState))
                return precedingState;

            if (certificate.CreatedAt >= cutoffExclusive)
                return null;

            var status = certificate.Status;
            var releasedAmount = certificate.RetentionReleasedAmount;
            var isDeleted = certificate.IsDeleted &&
                            certificate.DeletedAt.HasValue &&
                            certificate.DeletedAt.Value < cutoffExclusive;
            if (certificate.UpdatedAt.HasValue && certificate.UpdatedAt.Value >= cutoffExclusive)
            {
                // Conservative compatibility for legacy rows whose first post-cutoff
                // mutation predates explicit snapshots.
                if (status == ProjectPaymentCertificateStatuses.Cancelled)
                    status = ProjectPaymentCertificateStatuses.Approved;
                if (releasedAmount > 0m)
                    releasedAmount = 0m;
            }

            return new PaymentCertificateStateAsOf(
                certificate.Id,
                certificate.ContractId,
                status,
                certificate.Currency,
                certificate.RetentionHeldAmount,
                releasedAmount,
                certificate.IssueDate,
                isDeleted);
        }

        private static bool TryReadPaymentCertificateSnapshot(
            string? json,
            out PaymentCertificateStateAsOf? state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                var snapshot = JsonSerializer.Deserialize<PaymentCertificateAuditSnapshot>(json);
                if (snapshot == null)
                    return false;
                state = new PaymentCertificateStateAsOf(
                    snapshot.Id,
                    snapshot.ContractId,
                    snapshot.Status,
                    snapshot.Currency,
                    snapshot.RetentionHeldAmount,
                    snapshot.RetentionReleasedAmount,
                    snapshot.IssueDate,
                    snapshot.IsDeleted);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private sealed record PaymentCertificateAuditSnapshot(
            Guid Id,
            Guid? ContractId,
            string Status,
            string Currency,
            decimal RetentionHeldAmount,
            decimal RetentionReleasedAmount,
            DateTime IssueDate,
            DateTime CreatedAt,
            DateTime? UpdatedAt,
            bool IsDeleted,
            DateTime? DeletedAt);

        private sealed record PaymentCertificateStateAsOf(
            Guid Id,
            Guid? ContractId,
            string Status,
            string Currency,
            decimal RetentionHeldAmount,
            decimal RetentionReleasedAmount,
            DateTime IssueDate,
            bool IsDeleted);

        private sealed record PurchaseOrderStateAsOf(Guid Id, string Status);

        private sealed record BudgetCommitmentSnapshot(
            Guid Id,
            Guid ProcurementBudgetId,
            Guid PurchaseRequisitionId,
            string ReservationReference,
            int ReservationSequence,
            JsonElement Status,
            decimal ReservedAmount,
            string Currency,
            decimal BudgetCommittedBefore,
            decimal BudgetAvailableBefore,
            decimal BudgetCommittedAfter,
            decimal BudgetAvailableAfter,
            bool IsOverride,
            string? OverrideRuleCode,
            string? OverrideApprovalReference,
            DateTime ReservedAtUtc,
            DateTime? ReleasedAtUtc,
            DateTime? ConsumedAtUtc,
            string? ReleaseReason);

        private async Task<ApAgingReportDto> GetSettlementReadModelAgingReportAsync(
            DateTime? asOfDate,
            Guid? supplierId,
            bool includeInvoiceDetails,
            CancellationToken cancellationToken)
        {
            var date = asOfDate ?? DateTime.UtcNow;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var rebuild = await _settlementReadModelService!.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsPayable,
                AsOfDate = date,
                RecordAudit = false
            }, cancellationToken);

            var balances = (await _settlementReadModelService.GetBalancesAsync(
                    SubledgerSettlementModules.AccountsPayable,
                    date,
                    supplierId,
                    cancellationToken))
                .Where(b => b.OutstandingAmount != 0)
                .ToList();

            var documentIds = balances.Select(b => b.SourceDocumentId).Distinct().ToList();
            var invoiceNames = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && documentIds.Contains(i.Id))
                .Select(i => new { i.Id, i.SupplierName })
                .ToDictionaryAsync(i => i.Id, i => i.SupplierName, cancellationToken);

            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);
            var report = new ApAgingReportDto
            {
                AsOfDate = date,
                CurrencyCode = baseCurrencyCode,
                UsesSettlementReadModel = true,
                Diagnostics = rebuild.Diagnostics
            };

            var detailBySupplier = new Dictionary<Guid, SupplierAgingDetailDto>();
            SupplierAgingDetailDto GetOrCreateDetail(Guid id, string name)
            {
                if (detailBySupplier.TryGetValue(id, out var existing))
                    return existing;

                var detail = new SupplierAgingDetailDto
                {
                    SupplierId = id,
                    SupplierName = name,
                    Invoices = new List<ApAgingInvoiceDto>()
                };
                detailBySupplier[id] = detail;
                report.SupplierDetails.Add(detail);
                return detail;
            }

            foreach (var balance in balances)
            {
                var detail = GetOrCreateDetail(
                    balance.CounterpartyId,
                    invoiceNames.GetValueOrDefault(balance.SourceDocumentId) ?? "Supplier");
                AddToSupplierAgingBucket(
                    detail,
                    ToFunctionalAmount(balance, balance.OutstandingAmount),
                    balance.DueDate,
                    balance.TransactionDate,
                    date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || balance.TransactionDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = balance.TransactionDate;

                if (includeInvoiceDetails && detail.Invoices != null)
                {
                    detail.Invoices.Add(MapApBalanceToAgingInvoice(balance, date));
                }
            }

            foreach (var adjustment in adjustments)
            {
                var amount = GetSignedApSubledgerFunctionalAmount(adjustment);
                if (amount == 0)
                    continue;

                var detail = GetOrCreateDetail(adjustment.BusinessPartnerId, adjustment.BusinessPartnerName);
                AddToSupplierAgingBucket(detail, amount, adjustment.DueDate, adjustment.AdjustmentDate, date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || adjustment.AdjustmentDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = adjustment.AdjustmentDate;

                if (includeInvoiceDetails && detail.Invoices != null)
                {
                    detail.Invoices.Add(MapApAdjustmentToAgingInvoice(adjustment, date, baseCurrencyCode));
                }
            }

            report.SupplierDetails = report.SupplierDetails
                .Where(d => d.TotalOutstanding != 0)
                .OrderByDescending(d => d.TotalOutstanding)
                .ToList();
            report.TotalOutstanding = report.SupplierDetails.Sum(d => d.TotalOutstanding);
            report.Current = report.SupplierDetails.Sum(d => d.Current);
            report.ThirtyDays = report.SupplierDetails.Sum(d => d.ThirtyDays);
            report.SixtyDays = report.SupplierDetails.Sum(d => d.SixtyDays);
            report.NinetyPlusDays = report.SupplierDetails.Sum(d => d.NinetyPlusDays);
            report.TotalSuppliers = report.SupplierDetails.Count;
            report.TotalInvoices = balances.Count + adjustments.Count;

            await RecordReportAuditAsync(FinanceAuditEvents.ApAgingGeneratedFromSettlementReadModel, report, cancellationToken);
            return report;
        }

        // ═════════════════════════════════════════════════════════════════
        //  CASH REQUIREMENT FORECAST
        // ═════════════════════════════════════════════════════════════════

        public async Task<CashRequirementForecastDto> GetCashRequirementForecastAsync(
            DateTime? asOfDate = null, CancellationToken cancellationToken = default)
        {
            var now = asOfDate ?? DateTime.UtcNow;
            var today = now.Date;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.Status == VendorInvoiceStatus.Approved ||
                     i.Status == VendorInvoiceStatus.PartiallyPaid ||
                     i.Status == VendorInvoiceStatus.Overdue) &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .ToListAsync(cancellationToken);
            var adjustments = (await GetPostedApAdjustmentsAsync(null, cancellationToken))
                .Where(a => GetSignedApSubledgerAmount(a) > 0)
                .ToList();

            var forecast = new CashRequirementForecastDto
            {
                AsOfDate = today,
                CurrencyCode = baseCurrencyCode,
                TotalPayable = invoices.Sum(i => i.TotalAmount - i.PaidAmount)
                    + adjustments.Sum(GetSignedApSubledgerAmount)
            };

            // Define periods
            var periods = new[]
            {
                ("Overdue", DateTime.MinValue, today.AddDays(-1)),
                ("This Week", today, today.AddDays(6 - (int)today.DayOfWeek)),
                ("Next Week", today.AddDays(7 - (int)today.DayOfWeek), today.AddDays(13 - (int)today.DayOfWeek)),
                ("Next 30 Days", today, today.AddDays(30)),
                ("31-60 Days", today.AddDays(31), today.AddDays(60)),
                ("61-90 Days", today.AddDays(61), today.AddDays(90))
            };

            foreach (var (name, start, end) in periods)
            {
                IEnumerable<VendorInvoice> periodInvoices;

                if (name == "Overdue")
                {
                    periodInvoices = invoices.Where(i => i.DueDate.HasValue && i.DueDate.Value < today);
                    forecast.OverdueAmount = periodInvoices.Sum(i => i.TotalAmount - i.PaidAmount);
                    forecast.OverdueAmount += adjustments
                        .Where(a => (a.DueDate ?? a.AdjustmentDate) < today)
                        .Sum(GetSignedApSubledgerAmount);
                }
                else
                {
                    periodInvoices = invoices.Where(i =>
                        i.DueDate.HasValue &&
                        i.DueDate.Value >= start &&
                        i.DueDate.Value <= end);
                }

                var periodList = periodInvoices.ToList();
                var periodAdjustments = adjustments.Where(a =>
                {
                    var dueDate = a.DueDate ?? a.AdjustmentDate;
                    return dueDate >= start && dueDate <= end;
                }).ToList();
                forecast.Periods.Add(new CashRequirementPeriodDto
                {
                    Period = name,
                    PeriodStart = start == DateTime.MinValue ? today : start,
                    PeriodEnd = end,
                    AmountDue = periodList.Sum(i => i.TotalAmount - i.PaidAmount)
                        + periodAdjustments.Sum(GetSignedApSubledgerAmount),
                    InvoiceCount = periodList.Count + periodAdjustments.Count,
                    DiscountAvailable = periodList
                        .Where(i => i.EarlyPaymentDiscountPercentage > 0
                            && i.EarlyPaymentDiscountDueDate.HasValue
                            && i.EarlyPaymentDiscountDueDate.Value >= today)
                        .Sum(i => i.EarlyPaymentDiscountAmount)
                });
            }

            return forecast;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SUPPLIER STATEMENT
        // ═════════════════════════════════════════════════════════════════

        public async Task<SupplierStatementDto> GetSupplierStatementAsync(
            Guid supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            // Keep this compatibility-shaped endpoint on the same supplier detailed-ledger path
            // used by the report screen and controlled exports. The previous implementation
            // independently queried operational invoice/payment fields and used the opposite
            // debit/credit convention, which allowed viewed and downloaded statements to drift.
            var detailed = await GetSupplierDetailedLedgerAsync(
                fromDate,
                toDate,
                new[] { supplierId },
                showSupplierCurrency: false,
                cancellationToken);
            var supplier = detailed.Suppliers.SingleOrDefault(item =>
                item.SupplierId == supplierId || item.BusinessPartnerId == supplierId)
                ?? throw new KeyNotFoundException($"Supplier with Id '{supplierId}' was not found in the current tenant.");

            return new SupplierStatementDto
            {
                SupplierId = supplier.SupplierId,
                SupplierName = supplier.SupplierName,
                SupplierCode = supplier.SupplierCode,
                FromDate = detailed.FromDate,
                ToDate = detailed.ToDate,
                CurrencyCode = supplier.CurrencyCode,
                OpeningBalance = supplier.OpeningBalance,
                // These two property names are retained for the narrow compatibility DTO. The
                // values now follow the canonical AP control-account convention: credits increase
                // the payable and debits reduce it.
                TotalInvoices = supplier.TotalCredits,
                TotalPayments = supplier.TotalDebits,
                ClosingBalance = supplier.ClosingBalance,
                Lines = supplier.Lines.Select(line => new SupplierStatementLineDto
                {
                    Date = line.TransactionDate,
                    TransactionType = line.TransactionType,
                    DocumentNumber = line.DocumentNumber,
                    Reference = line.Reference,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    RunningBalance = line.RunningBalance
                }).ToList()
            };
        }

        public async Task<SupplierDetailedLedgerReportDto> GetSupplierDetailedLedgerAsync(
            DateTime fromDate,
            DateTime toDate,
            IReadOnlyCollection<Guid>? supplierIds = null,
            bool showSupplierCurrency = false,
            CancellationToken cancellationToken = default)
        {
            var startDate = fromDate.Date;
            var endDate = toDate.Date;
            if (endDate < startDate)
                throw new ArgumentException("The end date must be on or after the start date.");

            var endExclusive = endDate.AddDays(1);
            var baseCurrencyCode = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync(), "GHS");
            var requestedSupplierIds = supplierIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();

            var selections = await GetSupplierLedgerSelectionsAsync(requestedSupplierIds, endExclusive, cancellationToken);
            var report = new SupplierDetailedLedgerReportDto
            {
                FromDate = startDate,
                ToDate = endDate,
                CurrencyCode = showSupplierCurrency ? "Supplier Currency" : baseCurrencyCode,
                ShowSupplierCurrency = showSupplierCurrency
            };

            foreach (var selection in selections.OrderBy(s => s.SupplierName).ThenBy(s => s.SupplierCode))
            {
                var reportCurrencyCode = showSupplierCurrency
                    ? NormalizeCurrency(selection.CurrencyCode, baseCurrencyCode)
                    : baseCurrencyCode;

                var transactions = selection.SupplierId.HasValue
                    ? await GetSupplierLedgerTransactionsAsync(selection, startDate, endExclusive, reportCurrencyCode, baseCurrencyCode, report.Warnings, cancellationToken)
                    : new List<SupplierLedgerTransaction>();

                var openingBalance = transactions
                    .Where(t => t.TransactionDate.Date < startDate)
                    .Sum(t => t.Credit - t.Debit);

                var periodTransactions = transactions
                    .Where(t => t.TransactionDate.Date >= startDate && t.TransactionDate < endExclusive)
                    .OrderBy(t => t.TransactionDate)
                    .ThenBy(t => t.TransactionType)
                    .ThenBy(t => t.DocumentNumber)
                    .ToList();

                var runningBalance = openingBalance;
                var lines = new List<SupplierDetailedLedgerLineDto>();

                foreach (var transaction in periodTransactions)
                {
                    runningBalance += transaction.Credit - transaction.Debit;
                    lines.Add(new SupplierDetailedLedgerLineDto
                    {
                        SourceDocumentId = transaction.SourceDocumentId,
                        TransactionDate = transaction.TransactionDate,
                        TransactionType = transaction.TransactionType,
                        DocumentNumber = transaction.DocumentNumber,
                        Reference = transaction.Reference,
                        Description = transaction.Description,
                        TransactionCurrencyCode = transaction.TransactionCurrencyCode,
                        ExchangeRate = transaction.ExchangeRate,
                        Debit = transaction.Debit,
                        Credit = transaction.Credit,
                        RunningBalance = runningBalance
                    });
                }

                var totalDebits = lines.Sum(l => l.Debit);
                var totalCredits = lines.Sum(l => l.Credit);
                var closingBalance = openingBalance + totalCredits - totalDebits;

                if (requestedSupplierIds.Count == 0 && openingBalance == 0m && closingBalance == 0m && lines.Count == 0)
                    continue;

                report.Suppliers.Add(new SupplierDetailedLedgerAccountDto
                {
                    SupplierId = selection.SupplierId ?? selection.BusinessPartnerId ?? Guid.Empty,
                    BusinessPartnerId = selection.BusinessPartnerId,
                    SupplierCode = selection.SupplierCode,
                    SupplierName = selection.SupplierName,
                    CurrencyCode = reportCurrencyCode,
                    OpeningBalance = RoundMoney(openingBalance),
                    TotalDebits = RoundMoney(totalDebits),
                    TotalCredits = RoundMoney(totalCredits),
                    ClosingBalance = RoundMoney(closingBalance),
                    Lines = lines
                });
            }

            report.TotalOpeningBalance = RoundMoney(report.Suppliers.Sum(s => s.OpeningBalance));
            report.TotalDebits = RoundMoney(report.Suppliers.Sum(s => s.TotalDebits));
            report.TotalCredits = RoundMoney(report.Suppliers.Sum(s => s.TotalCredits));
            report.TotalClosingBalance = RoundMoney(report.Suppliers.Sum(s => s.ClosingBalance));
            report.CurrencyTotals = report.Suppliers
                .GroupBy(s => s.CurrencyCode, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new DetailedLedgerCurrencyTotalDto
                {
                    CurrencyCode = group.Key,
                    OpeningBalance = RoundMoney(group.Sum(s => s.OpeningBalance)),
                    TotalDebits = RoundMoney(group.Sum(s => s.TotalDebits)),
                    TotalCredits = RoundMoney(group.Sum(s => s.TotalCredits)),
                    ClosingBalance = RoundMoney(group.Sum(s => s.ClosingBalance))
                })
                .ToList();
            report.Warnings = report.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            return report;
        }

        // ═════════════════════════════════════════════════════════════════
        //  WITHHOLDING TAX SUMMARY
        // ═════════════════════════════════════════════════════════════════

        public async Task<WithholdingTaxSummaryDto> GetWithholdingTaxSummaryAsync(
            DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var payments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate >= fromDate &&
                    p.PaymentDate <= toDate &&
                    p.Status != VendorPaymentStatus.Voided &&
                    p.WithholdingTaxAmount > 0)
                .Include(p => p.BusinessPartner)
                .Include(p => p.Allocations)
                .ToListAsync(cancellationToken);
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            var summary = new WithholdingTaxSummaryDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                CurrencyCode = baseCurrencyCode,
                TotalWithheld = payments.Sum(p => p.WithholdingTaxAmount),
                TransactionCount = payments.Count,
                SupplierCount = payments.Select(p => p.BusinessPartnerId).Distinct().Count()
            };

            summary.BySupplier = payments
                .GroupBy(p => new
                {
                    SupplierId = p.BusinessPartnerId,
                    Name = p.BusinessPartnerName,
                    TaxId = p.BusinessPartnerTaxIdentificationNumber
                })
                .Select(g => new WithholdingTaxBySupplierDto
                {
                    SupplierId = g.Key.SupplierId,
                    SupplierName = g.Key.Name,
                    TaxId = g.Key.TaxId,
                    // All summary money is labelled in functional currency. Allocation snapshots
                    // prevent payment-currency cash from being combined with functional WHT.
                    TotalInvoiceAmount = g.Sum(p => p.Allocations
                        .Where(a => !a.IsDeleted)
                        .Sum(a => a.SettlementFunctionalAmount)),
                    TotalWithholdingTax = g.Sum(p => p.WithholdingTaxAmount),
                    TotalNetPayment = g.Sum(p => p.Allocations
                        .Where(a => !a.IsDeleted)
                        .Sum(a => a.PaymentFunctionalAmount)),
                    TransactionCount = g.Count()
                })
                .OrderByDescending(s => s.TotalWithholdingTax)
                .ToList();

            return summary;
        }

        // ═════════════════════════════════════════════════════════════════
        //  AP SUMMARY (DASHBOARD)
        // ═════════════════════════════════════════════════════════════════

        public async Task<ApSummaryDto> GetApSummaryAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var invoiceQuery = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft);
            var invoices = await ApplyPostedApInvoiceFilter(invoiceQuery).ToListAsync(cancellationToken);

            var outstanding = invoices.Where(i => (i.TotalAmount - i.PaidAmount) > 0).ToList();
            var overdue = outstanding.Where(i => i.DueDate.HasValue && i.DueDate.Value < now).ToList();
            var adjustments = await GetPostedApAdjustmentsAsync(null, cancellationToken);
            var positiveAdjustments = adjustments.Where(a => GetSignedApSubledgerAmount(a) > 0).ToList();
            var overdueAdjustments = positiveAdjustments
                .Where(a => (a.DueDate ?? a.AdjustmentDate) < now)
                .ToList();

            // Payments this month
            var monthPayments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate >= monthStart &&
                    p.Status != VendorPaymentStatus.Voided)
                .ToListAsync(cancellationToken);

            // Average days to payment
            var paidInvoices = invoices.Where(i => i.Status == VendorInvoiceStatus.Paid && i.DueDate.HasValue).ToList();
            var avgDays = paidInvoices.Any()
                ? paidInvoices.Average(i => (i.UpdatedAt ?? i.CreatedAt).Subtract(i.InvoiceDate).TotalDays)
                : 0;

            // Discounts — taken vs missed
            var discountEligible = invoices.Where(i => i.EarlyPaymentDiscountPercentage > 0);
            var taken = discountEligible.Where(i =>
                i.Status == VendorInvoiceStatus.Paid &&
                i.EarlyPaymentDiscountDueDate.HasValue &&
                (i.UpdatedAt ?? i.CreatedAt) <= i.EarlyPaymentDiscountDueDate.Value)
                .Sum(i => i.EarlyPaymentDiscountAmount);

            var missed = discountEligible.Where(i =>
                i.EarlyPaymentDiscountDueDate.HasValue &&
                i.EarlyPaymentDiscountDueDate.Value < now &&
                (i.Status != VendorInvoiceStatus.Paid
                 || (i.UpdatedAt ?? i.CreatedAt) > i.EarlyPaymentDiscountDueDate.Value))
                .Sum(i => i.EarlyPaymentDiscountAmount);

            // Pending approvals and batches
            var pendingApprovals = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Status == VendorInvoiceStatus.PendingApproval)
                .CountAsync(cancellationToken);

            var pendingBatches = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId &&
                    (b.Status == PaymentBatchStatus.Draft || b.Status == PaymentBatchStatus.PendingApproval))
                .CountAsync(cancellationToken);

            return new ApSummaryDto
            {
                TotalOutstanding = outstanding.Sum(i => i.TotalAmount - i.PaidAmount)
                    + adjustments.Sum(GetSignedApSubledgerAmount),
                TotalOverdue = overdue.Sum(i => i.TotalAmount - i.PaidAmount)
                    + overdueAdjustments.Sum(GetSignedApSubledgerAmount),
                OutstandingInvoiceCount = outstanding.Count + positiveAdjustments.Count,
                OverdueInvoiceCount = overdue.Count + overdueAdjustments.Count,
                AverageDaysToPayment = (decimal)avgDays,
                TotalPaidThisMonth = monthPayments.Sum(p => p.TotalAmount),
                DiscountsTaken = taken,
                DiscountsMissed = missed,
                WithholdingTaxThisMonth = monthPayments.Sum(p => p.WithholdingTaxAmount),
                PendingApprovalCount = pendingApprovals,
                PendingBatchCount = pendingBatches
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXPORTS (STUBS)
        // ═════════════════════════════════════════════════════════════════

        public async Task<VendorInvoiceMatchExceptionReportDto> GetThreeWayMatchExceptionsAsync(
            DateTime fromDate,
            DateTime toDate,
            VendorInvoiceMatchExceptionStatus? status = null,
            Guid? supplierId = null,
            CancellationToken cancellationToken = default)
        {
            var from = EnsureUtc(fromDate);
            var to = EnsureUtc(toDate);
            if (to < from) throw new ArgumentException("The report end date must not precede the start date.");
            var endExclusive = to.Date.AddDays(1);
            var now = DateTime.UtcNow;

            IQueryable<VendorInvoiceMatchException> query = _unitOfWork.Repository<VendorInvoiceMatchException>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted &&
                                      item.RequestedAtUtc >= from.Date && item.RequestedAtUtc < endExclusive)
                .AsNoTracking()
                .Include(item => item.VendorInvoice).ThenInclude(invoice => invoice.BusinessPartner)
                .Include(item => item.PurchaseOrder)
                .Include(item => item.Variances)
                .Include(item => item.Evidence);
            if (supplierId.HasValue)
                query = query.Where(item => item.VendorInvoice.BusinessPartnerId == supplierId.Value);

            var source = await query.OrderByDescending(item => item.RequestedAtUtc).ToListAsync(cancellationToken);
            var rows = source.Select(item => new VendorInvoiceMatchExceptionReportRowDto
            {
                ExceptionId = item.Id,
                VendorInvoiceId = item.VendorInvoiceId,
                InvoiceNumber = item.VendorInvoice.InvoiceNumber,
                BusinessPartnerId = item.VendorInvoice.BusinessPartnerId,
                BusinessPartnerRoleId = item.VendorInvoice.BusinessPartnerRoleId,
                BusinessPartnerApProfileVersionId = item.VendorInvoice.BusinessPartnerApProfileVersionId,
                BusinessPartnerCode = item.VendorInvoice.BusinessPartnerCode,
                BusinessPartnerLegalName = item.VendorInvoice.BusinessPartnerLegalName,
                BusinessPartnerTaxIdentificationNumber = item.VendorInvoice.BusinessPartnerTaxIdentificationNumber,
                SupplierName = item.VendorInvoice.SupplierName,
                PurchaseOrderId = item.PurchaseOrderId,
                PurchaseOrderNumber = item.PurchaseOrder.OrderNumber,
                Status = VendorInvoiceMatchExceptionRules.EffectiveStatus(item.Status, item.ExpiresAtUtc, now),
                VarianceType = item.VarianceType,
                MaximumVariancePercentage = item.Variances.Count == 0
                    ? 0m
                    : item.Variances.Max(variance => Math.Abs(variance.VariancePercentage)),
                RootCauseCategory = item.RootCauseCategory,
                RootCauseDescription = item.RootCauseDescription,
                CorrectiveAction = item.CorrectiveAction,
                CorrectiveActionOwnerName = item.CorrectiveActionOwnerName,
                CorrectiveActionDueAtUtc = item.CorrectiveActionDueAtUtc,
                CorrectiveActionStatus = item.CorrectiveActionStatus,
                RequestedAtUtc = item.RequestedAtUtc,
                ExpiresAtUtc = item.ExpiresAtUtc,
                RequestedByName = item.RequestedByName,
                FinalApprovedByName = item.FinalApprovedByName,
                FinalApprovedAtUtc = item.FinalApprovedAtUtc,
                WorkflowInstanceId = item.WorkflowInstanceId,
                ApprovalControlEventId = item.ApprovalControlEventId,
                EvidenceCount = item.Evidence.Count
            }).ToList();
            if (status.HasValue) rows = rows.Where(row => row.Status == status.Value).ToList();

            var report = new VendorInvoiceMatchExceptionReportDto
            {
                FromDate = from.Date,
                ToDate = to.Date,
                Status = status,
                SupplierId = supplierId,
                TotalCount = rows.Count,
                ApprovedCount = rows.Count(row => row.Status == VendorInvoiceMatchExceptionStatus.Approved),
                ExpiredCount = rows.Count(row => row.Status == VendorInvoiceMatchExceptionStatus.Expired),
                OpenCorrectiveActionCount = rows.Count(row =>
                    VendorInvoiceMatchExceptionRules.CanCompleteCorrectiveAction(
                        row.Status,
                        row.CorrectiveActionStatus,
                        row.ExpiresAtUtc,
                        now)),
                Rows = rows
            };
            await RecordReportAuditAsync(FinanceAuditEvents.ApMatchExceptionsGenerated, report, cancellationToken);
            return report;
        }

        public async Task<byte[]> ExportThreeWayMatchExceptionsAsync(
            DateTime fromDate,
            DateTime toDate,
            VendorInvoiceMatchExceptionStatus? status = null,
            Guid? supplierId = null,
            string format = "Csv",
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(format, "Csv", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("The AP-006 register currently supports CSV export only.");
            var report = await GetThreeWayMatchExceptionsAsync(
                fromDate, toDate, status, supplierId, cancellationToken);
            var csv = new StringBuilder();
            csv.AppendLine("Invoice,Supplier,Purchase Order,Status,Variance Type,Maximum Variance %,Root Cause,Corrective Action,Owner,Corrective Due,Corrective Status,Requested At,Expires At,Requester,Final Approver,Workflow Instance,Approval Event,Evidence Count");
            foreach (var row in report.Rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(row.InvoiceNumber), Csv(row.SupplierName), Csv(row.PurchaseOrderNumber), Csv(row.Status.ToString()),
                    Csv(row.VarianceType), row.MaximumVariancePercentage.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                    Csv(row.RootCauseDescription), Csv(row.CorrectiveAction), Csv(row.CorrectiveActionOwnerName),
                    row.CorrectiveActionDueAtUtc.ToString("O"), Csv(row.CorrectiveActionStatus.ToString()),
                    row.RequestedAtUtc.ToString("O"), row.ExpiresAtUtc.ToString("O"), Csv(row.RequestedByName),
                    Csv(row.FinalApprovedByName ?? string.Empty), row.WorkflowInstanceId?.ToString() ?? string.Empty,
                    row.ApprovalControlEventId?.ToString() ?? string.Empty, row.EvidenceCount.ToString()
                }));
            }
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            await RecordReportAuditAsync(FinanceAuditEvents.ApMatchExceptionsExported, new
            {
                report.FromDate,
                report.ToDate,
                report.Status,
                report.SupplierId,
                report.TotalCount,
                Format = "Csv"
            }, cancellationToken);
            return bytes;
        }

        public async Task<byte[]> ExportAgingReportAsync(DateTime? asOfDate = null, string format = "Csv", CancellationToken cancellationToken = default)
        {
            if (!string.Equals(format, "Csv", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("AP aging export supports Csv in the backend reporting foundation. Use the central finance report export service for other formats when added.");
            }

            var exportReport = await GetDetailedAgingReportAsync(asOfDate, null, cancellationToken);
            if (!exportReport.UsesSettlementReadModel)
            {
                throw new InvalidOperationException("AP aging export requires the AP settlement read model. Legacy operational-field aging is not allowed for production export.");
            }

            var csv = new StringBuilder();
            csv.AppendLine("Supplier,InvoiceNumber,InvoiceDate,DueDate,TotalAmount,SettledAmount,CreditedAmount,WithheldAmount,OutstandingAmount,AgingBucket,SettlementStatus,SourcePostingEventId,SourceJournalEntryId,Diagnostics");
            foreach (var supplier in exportReport.SupplierDetails)
            {
                foreach (var invoice in supplier.Invoices)
                {
                    csv.AppendLine(string.Join(",", new[]
                    {
                        Csv(supplier.SupplierName),
                        Csv(invoice.InvoiceNumber),
                        Csv(invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                        Csv(invoice.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty),
                        invoice.TotalAmount.ToString("0.00"),
                        invoice.SettledAmount.ToString("0.00"),
                        invoice.CreditedAmount.ToString("0.00"),
                        invoice.WithheldAmount.ToString("0.00"),
                        invoice.BalanceAmount.ToString("0.00"),
                        Csv(invoice.AgingBucket),
                        Csv(invoice.SettlementStatus ?? string.Empty),
                        Csv(invoice.SourcePostingEventId?.ToString() ?? string.Empty),
                        Csv(invoice.SourceJournalEntryId?.ToString() ?? string.Empty),
                        Csv(invoice.DiagnosticFlags ?? string.Empty)
                    }));
                }
            }

            await RecordReportAuditAsync(FinanceAuditEvents.ApAgingExported, exportReport, cancellationToken);
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private Task<List<SubledgerAdjustmentJournal>> GetPostedApAdjustmentsAsync(
            Guid? supplierId,
            CancellationToken cancellationToken)
        {
            IQueryable<SubledgerAdjustmentJournal> query = _unitOfWork.Repository<SubledgerAdjustmentJournal>()
                .GetQueryable(a =>
                    a.TenantId == TenantId &&
                    a.Module == SubledgerModules.AccountsPayable &&
                    a.Status == SubledgerAdjustmentStatuses.Posted &&
                    !a.IsDeleted)
                .Include(a => a.BusinessPartner);

            if (supplierId.HasValue)
                query = query.Where(a => a.BusinessPartnerId == supplierId.Value);

            return query.ToListAsync(cancellationToken);
        }

        private IQueryable<VendorInvoice> ApplyPostedApInvoiceFilter(IQueryable<VendorInvoice> query)
        {
            var postedInvoiceEvents = _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e =>
                    e.TenantId == TenantId &&
                    e.SourceDocumentType == "VendorInvoice" &&
                    e.PostingAction == "Post" &&
                    e.PostingStatus == "Posted");

            return query.Where(i => postedInvoiceEvents.Any(e => e.SourceDocumentId == i.Id));
        }

        private static void AddToSupplierAgingBucket(
            SupplierAgingDetailDto detail,
            decimal amount,
            DateTime? dueDate,
            DateTime transactionDate,
            DateTime asOfDate)
        {
            var agingDate = dueDate ?? transactionDate;
            var daysOutstanding = (int)(asOfDate - agingDate).TotalDays;

            if (daysOutstanding <= 30)
                detail.Current += amount;
            else if (daysOutstanding <= 60)
                detail.ThirtyDays += amount;
            else if (daysOutstanding <= 90)
                detail.SixtyDays += amount;
            else
                detail.NinetyPlusDays += amount;

            detail.TotalOutstanding += amount;
        }

        private static decimal GetSignedApSubledgerAmount(SubledgerAdjustmentJournal adjustment)
        {
            var isCredit = string.Equals(adjustment.AdjustmentType, SubledgerAdjustmentTypes.Credit, StringComparison.OrdinalIgnoreCase);
            return isCredit ? adjustment.Amount : -adjustment.Amount;
        }

        private static string GetAdjustmentTransactionType(SubledgerAdjustmentJournal adjustment)
        {
            return string.Equals(adjustment.Purpose, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase)
                ? "Opening Balance"
                : "Adjustment";
        }

        private static ApAgingInvoiceDto MapApBalanceToAgingInvoice(
            SubledgerSettlementBalance balance,
            DateTime asOfDate)
        {
            var agingDate = balance.DueDate ?? balance.TransactionDate;
            var daysOutstanding = (int)(asOfDate - agingDate).TotalDays;
            return new ApAgingInvoiceDto
            {
                InvoiceId = balance.SourceDocumentId,
                InvoiceNumber = balance.SourceDocumentNumber,
                InvoiceDate = balance.TransactionDate,
                DueDate = balance.DueDate,
                TotalAmount = RoundMoney(balance.OriginalFunctionalAmount),
                SettledAmount = ToFunctionalAmount(balance, balance.SettledAmount),
                CreditedAmount = ToFunctionalAmount(balance, balance.CreditedAmount),
                WithheldAmount = ToFunctionalAmount(balance, balance.WithheldAmount),
                BalanceAmount = ToFunctionalAmount(balance, balance.OutstandingAmount),
                CurrencyCode = NormalizeCurrency(balance.FunctionalCurrencyCode, "GHS"),
                DocumentCurrencyCode = NormalizeCurrency(balance.DocumentCurrencyCode, "GHS"),
                DocumentTotalAmount = RoundMoney(balance.OriginalDocumentAmount),
                DocumentSettledAmount = RoundMoney(balance.SettledAmount),
                DocumentCreditedAmount = RoundMoney(balance.CreditedAmount),
                DocumentWithheldAmount = RoundMoney(balance.WithheldAmount),
                DocumentBalanceAmount = RoundMoney(balance.OutstandingAmount),
                SourcePostingEventId = balance.SourcePostingEventId,
                SourceJournalEntryId = balance.SourceJournalEntryId,
                SettlementStatus = balance.SettlementStatus,
                DiagnosticFlags = balance.DiagnosticFlags,
                DaysOutstanding = Math.Max(0, daysOutstanding),
                AgingBucket = daysOutstanding <= 30 ? "Current"
                    : daysOutstanding <= 60 ? "31-60"
                    : daysOutstanding <= 90 ? "61-90"
                    : "90+"
            };
        }

        private static ApAgingInvoiceDto MapApAdjustmentToAgingInvoice(
            SubledgerAdjustmentJournal adjustment,
            DateTime asOfDate,
            string functionalCurrencyCode)
        {
            var amount = GetSignedApSubledgerFunctionalAmount(adjustment);
            var documentAmount = GetSignedApSubledgerAmount(adjustment);
            var agingDate = adjustment.DueDate ?? adjustment.AdjustmentDate;
            var daysOutstanding = (int)(asOfDate - agingDate).TotalDays;
            return new ApAgingInvoiceDto
            {
                InvoiceId = adjustment.Id,
                InvoiceNumber = adjustment.AdjustmentNumber,
                InvoiceDate = adjustment.AdjustmentDate,
                DueDate = adjustment.DueDate,
                TotalAmount = amount,
                BalanceAmount = amount,
                CurrencyCode = functionalCurrencyCode,
                DocumentCurrencyCode = NormalizeCurrency(adjustment.CurrencyCode, functionalCurrencyCode),
                DocumentTotalAmount = documentAmount,
                DocumentBalanceAmount = documentAmount,
                DaysOutstanding = Math.Max(0, daysOutstanding),
                AgingBucket = daysOutstanding <= 30 ? "Current"
                    : daysOutstanding <= 60 ? "31-60"
                    : daysOutstanding <= 90 ? "61-90"
                    : "90+",
                SettlementStatus = "PostedAdjustment"
            };
        }

        private static decimal ToFunctionalAmount(SubledgerSettlementBalance balance, decimal documentAmount)
        {
            if (documentAmount == 0m)
                return 0m;

            if (balance.OriginalDocumentAmount == 0m)
                return RoundMoney(documentAmount);

            return RoundMoney(documentAmount * balance.OriginalFunctionalAmount / balance.OriginalDocumentAmount);
        }

        private static decimal GetSignedApSubledgerFunctionalAmount(SubledgerAdjustmentJournal adjustment)
        {
            var documentAmount = GetSignedApSubledgerAmount(adjustment);
            if (documentAmount == 0m)
                return 0m;

            var functionalAmount = adjustment.BaseCurrencyAmount != 0m
                ? Math.Abs(adjustment.BaseCurrencyAmount)
                : Math.Abs(adjustment.Amount) * NormalizeExchangeRate(adjustment.ExchangeRate);

            return RoundMoney(documentAmount > 0m ? functionalAmount : -functionalAmount);
        }

        private async Task<List<SupplierLedgerSelection>> GetSupplierLedgerSelectionsAsync(
            IReadOnlyCollection<Guid> requestedSupplierIds,
            DateTime endExclusive,
            CancellationToken cancellationToken)
        {
            var apPartnerIds = await _unitOfWork.Repository<BusinessPartnerRole>()
                .GetQueryable(role => role.TenantId == TenantId && !role.IsDeleted &&
                    (role.RoleType == BusinessPartnerRoleType.Supplier ||
                     role.RoleType == BusinessPartnerRoleType.Contractor))
                .Select(role => role.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var partners = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(partner => partner.TenantId == TenantId &&
                    apPartnerIds.Contains(partner.Id))
                .ToListAsync(cancellationToken);

            var selectedIds = requestedSupplierIds.Count > 0
                ? requestedSupplierIds.ToHashSet()
                : await GetSupplierIdsWithLedgerActivityAsync(endExclusive, cancellationToken);

            var selections = new List<SupplierLedgerSelection>();
            foreach (var id in selectedIds)
            {
                var partner = partners.FirstOrDefault(p => p.Id == id);
                if (partner == null)
                    continue;

                selections.Add(new SupplierLedgerSelection(
                    null,
                    partner.Id,
                    partner.PartnerCode,
                    partner.PartnerName,
                    partner.Currency ?? "GHS"));
            }

            return selections
                .GroupBy(s => s.BusinessPartnerId ?? s.SupplierId ?? Guid.Empty)
                .Select(g => g.First())
                .ToList();
        }

        private async Task<HashSet<Guid>> GetSupplierIdsWithLedgerActivityAsync(DateTime endExclusive, CancellationToken cancellationToken)
        {
            var invoiceSupplierIds = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.InvoiceDate < endExclusive &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Rejected)
                .Select(i => i.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var paymentSupplierIds = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate < endExclusive &&
                    p.Status != VendorPaymentStatus.Draft &&
                    p.Status != VendorPaymentStatus.Voided &&
                    p.Status != VendorPaymentStatus.Failed)
                .Select(p => p.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var adjustmentSupplierIds = await _unitOfWork.Repository<SubledgerAdjustmentJournal>()
                .GetQueryable(a =>
                    a.TenantId == TenantId &&
                    a.Module == SubledgerModules.AccountsPayable &&
                    a.Status == SubledgerAdjustmentStatuses.Posted &&
                    a.BusinessPartnerId != Guid.Empty &&
                    a.AdjustmentDate < endExclusive)
                .Select(a => a.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return invoiceSupplierIds
                .Concat(paymentSupplierIds)
                .Concat(adjustmentSupplierIds)
                .ToHashSet();
        }

        private async Task<List<SupplierLedgerTransaction>> GetSupplierLedgerTransactionsAsync(
            SupplierLedgerSelection selection,
            DateTime startDate,
            DateTime endExclusive,
            string reportCurrencyCode,
            string baseCurrencyCode,
            ICollection<string> warnings,
            CancellationToken cancellationToken)
        {
            if (!selection.SupplierId.HasValue)
                return new List<SupplierLedgerTransaction>();

            var supplierId = selection.SupplierId.Value;
            var transactions = new List<SupplierLedgerTransaction>();

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.BusinessPartnerId == supplierId &&
                    i.InvoiceDate < endExclusive &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Rejected)
                .ToListAsync(cancellationToken);

            foreach (var invoice in invoices)
            {
                var amount = AmountForLedgerCurrency(
                    invoice.TotalAmount,
                    invoice.BaseCurrencyAmount,
                    invoice.CurrencyCode,
                    invoice.ExchangeRate,
                    reportCurrencyCode,
                    baseCurrencyCode,
                    warnings,
                    invoice.InvoiceNumber);

                transactions.Add(new SupplierLedgerTransaction(
                    invoice.Id,
                    invoice.InvoiceDate,
                    "Invoice",
                    invoice.InvoiceNumber,
                    invoice.SupplierInvoiceNumber ?? invoice.Reference,
                    invoice.Notes ?? "Supplier invoice",
                    NormalizeCurrency(invoice.CurrencyCode, baseCurrencyCode),
                    NormalizeExchangeRate(invoice.ExchangeRate),
                    0m,
                    amount));
            }

            var payments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.BusinessPartnerId == supplierId &&
                    p.PaymentDate < endExclusive &&
                    p.Status != VendorPaymentStatus.Draft &&
                    p.Status != VendorPaymentStatus.Voided &&
                    p.Status != VendorPaymentStatus.Failed)
                .Include(p => p.Allocations)
                .ToListAsync(cancellationToken);

            foreach (var payment in payments)
            {
                var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, baseCurrencyCode);
                var paymentExchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
                var reference = payment.TransactionReference ?? payment.ChequeNumber;

                var cashAmount = AmountForLedgerCurrency(
                    payment.TotalAmount,
                    null,
                    paymentCurrency,
                    paymentExchangeRate,
                    reportCurrencyCode,
                    baseCurrencyCode,
                    warnings,
                    payment.PaymentNumber);

                transactions.Add(new SupplierLedgerTransaction(
                    payment.Id,
                    payment.PaymentDate,
                    "Payment",
                    payment.PaymentNumber,
                    reference,
                    $"Supplier payment - {payment.PaymentMethod}",
                    paymentCurrency,
                    paymentExchangeRate,
                    cashAmount,
                    0m));

                var activeAllocations = payment.Allocations.Where(a => !a.IsReversal).ToList();
                var discountTaken = activeAllocations.Sum(a => a.DiscountAmount);
                if (discountTaken == 0m)
                    discountTaken = payment.DiscountTaken;

                if (discountTaken > 0m)
                {
                    var discountAmount = AmountForLedgerCurrency(
                        discountTaken,
                        null,
                        paymentCurrency,
                        paymentExchangeRate,
                        reportCurrencyCode,
                        baseCurrencyCode,
                        warnings,
                        payment.PaymentNumber);

                    transactions.Add(new SupplierLedgerTransaction(
                        payment.Id,
                        payment.PaymentDate,
                        "Discount Taken",
                        payment.PaymentNumber,
                        reference,
                        "Supplier settlement discount",
                        paymentCurrency,
                        paymentExchangeRate,
                        discountAmount,
                        0m));
                }

                var withholdingTaxAmount = activeAllocations.Sum(a => a.WithholdingTaxAmount);
                if (withholdingTaxAmount == 0m)
                    withholdingTaxAmount = payment.WithholdingTaxAmount;

                if (withholdingTaxAmount > 0m)
                {
                    var whtAmount = AmountForLedgerCurrency(
                        withholdingTaxAmount,
                        null,
                        paymentCurrency,
                        paymentExchangeRate,
                        reportCurrencyCode,
                        baseCurrencyCode,
                        warnings,
                        payment.PaymentNumber);

                    transactions.Add(new SupplierLedgerTransaction(
                        payment.Id,
                        payment.PaymentDate,
                        "Withholding Tax",
                        payment.PaymentNumber,
                        payment.WithholdingCertificateNumber ?? reference,
                        "Withholding tax on supplier payment",
                        paymentCurrency,
                        paymentExchangeRate,
                        whtAmount,
                        0m));
                }
            }

            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);
            foreach (var adjustment in adjustments.Where(a => a.AdjustmentDate < endExclusive))
            {
                var signedAmount = GetSignedApSubledgerAmount(adjustment);
                if (signedAmount == 0m)
                    continue;

                var adjustmentAmount = AmountForLedgerCurrency(
                    Math.Abs(signedAmount),
                    Math.Abs(adjustment.BaseCurrencyAmount),
                    adjustment.CurrencyCode,
                    adjustment.ExchangeRate,
                    reportCurrencyCode,
                    baseCurrencyCode,
                    warnings,
                    adjustment.AdjustmentNumber);

                transactions.Add(new SupplierLedgerTransaction(
                    adjustment.Id,
                    adjustment.AdjustmentDate,
                    GetAdjustmentTransactionType(adjustment),
                    adjustment.AdjustmentNumber,
                    adjustment.Reference,
                    adjustment.Reason,
                    NormalizeCurrency(adjustment.CurrencyCode, baseCurrencyCode),
                    NormalizeExchangeRate(adjustment.ExchangeRate),
                    signedAmount < 0m ? adjustmentAmount : 0m,
                    signedAmount > 0m ? adjustmentAmount : 0m));
            }

            return transactions;
        }

        private static Supplier? FindMatchingSupplier(BusinessPartner partner, IEnumerable<Supplier> suppliers)
        {
            return suppliers.SingleOrDefault(s =>
                s.Id == partner.Id ||
                (!string.IsNullOrWhiteSpace(partner.PartnerCode) &&
                 string.Equals(s.SupplierCode, partner.PartnerCode, StringComparison.OrdinalIgnoreCase)));
        }

        private static BusinessPartner? FindMatchingSupplierPartner(Supplier supplier, IEnumerable<BusinessPartner> partners)
        {
            return partners.SingleOrDefault(p =>
                p.Id == supplier.Id ||
                (!string.IsNullOrWhiteSpace(supplier.SupplierCode) &&
                 string.Equals(p.PartnerCode, supplier.SupplierCode, StringComparison.OrdinalIgnoreCase)));
        }

        private static decimal AmountForLedgerCurrency(
            decimal transactionAmount,
            decimal? baseCurrencyAmount,
            string? transactionCurrencyCode,
            decimal exchangeRate,
            string reportCurrencyCode,
            string baseCurrencyCode,
            ICollection<string> warnings,
            string documentNumber)
        {
            var transactionCurrency = NormalizeCurrency(transactionCurrencyCode, baseCurrencyCode);
            var reportCurrency = NormalizeCurrency(reportCurrencyCode, baseCurrencyCode);
            var baseCurrency = NormalizeCurrency(baseCurrencyCode, "GHS");

            if (string.Equals(reportCurrency, transactionCurrency, StringComparison.OrdinalIgnoreCase))
                return RoundMoney(transactionAmount);

            var resolvedBaseAmount = baseCurrencyAmount.HasValue && baseCurrencyAmount.Value != 0m
                ? baseCurrencyAmount.Value
                : ToBaseCurrencyAmount(transactionAmount, transactionCurrency, baseCurrency, exchangeRate);

            if (string.Equals(reportCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase))
                return RoundMoney(resolvedBaseAmount);

            warnings.Add($"Document {documentNumber} is in {transactionCurrency}; shown using base currency {baseCurrency} because no direct {reportCurrency} amount is stored.");
            return RoundMoney(resolvedBaseAmount);
        }

        private static decimal ToBaseCurrencyAmount(decimal transactionAmount, string transactionCurrencyCode, string baseCurrencyCode, decimal exchangeRate)
        {
            if (string.Equals(transactionCurrencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return transactionAmount;

            return transactionAmount * NormalizeExchangeRate(exchangeRate);
        }

        private static string NormalizeCurrency(string? currencyCode, string fallback)
        {
            return string.IsNullOrWhiteSpace(currencyCode)
                ? fallback.Trim().ToUpperInvariant()
                : currencyCode.Trim().ToUpperInvariant();
        }

        private static decimal NormalizeExchangeRate(decimal exchangeRate)
        {
            return exchangeRate <= 0m ? 1m : exchangeRate;
        }

        private static decimal RoundMoney(decimal amount)
        {
            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        private static DateTime EnsureUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };

        private sealed record SupplierLedgerSelection(
            Guid? SupplierId,
            Guid? BusinessPartnerId,
            string SupplierCode,
            string SupplierName,
            string CurrencyCode);

        private sealed record SupplierLedgerTransaction(
            Guid SourceDocumentId,
            DateTime TransactionDate,
            string TransactionType,
            string DocumentNumber,
            string? Reference,
            string Description,
            string TransactionCurrencyCode,
            decimal ExchangeRate,
            decimal Debit,
            decimal Credit);

        private sealed record PurchaseOrderCommercialState(
            Guid PurchaseOrderId,
            Guid? SourceRequisitionId,
            string Currency,
            decimal TotalAmount,
            Dictionary<Guid, decimal> ItemUnitPrices);

        private sealed class PurchaseOrderCommercialSnapshot
        {
            public Guid SourceRequisitionId { get; set; }
            public string Currency { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public List<PurchaseOrderCommercialSnapshotItem> Items { get; set; } = [];
        }

        private sealed class PurchaseOrderCommercialSnapshotItem
        {
            public Guid? PurchaseOrderItemId { get; set; }
            public decimal UnitPrice { get; set; }
        }

        private async Task RecordReportAuditAsync(string eventType, object report, CancellationToken cancellationToken)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            try
            {
                var isMatchExceptionRegister = eventType is
                    FinanceAuditEvents.ApMatchExceptionsGenerated or FinanceAuditEvents.ApMatchExceptionsExported;
                var isProcurementReconciliation = eventType is
                    FinanceAuditEvents.ApProcurementReconciliationGenerated or
                    FinanceAuditEvents.ApProcurementReconciliationExported;
                await _financeAuditService.RecordAsync(new FinanceAuditEventDto
                {
                    EventType = eventType,
                    TenantId = TenantId,
                    SourceModule = "AP",
                    SourceDocumentType = isProcurementReconciliation
                        ? "ProcurementFinanceReconciliation"
                        : isMatchExceptionRegister
                            ? "MatchExceptionRegister"
                            : "AgingReport",
                    Resource = isProcurementReconciliation
                        ? "Finance.AP.ProcurementReconciliation"
                        : isMatchExceptionRegister
                            ? "Finance.AP.MatchExceptionRegister"
                            : "Finance.AP.AgingReport",
                    ResourceId = TenantId.ToString(),
                    AfterValues = report
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record AP report audit event {EventType}", eventType);
            }
        }

        private static string Csv(string value)
        {
            // Prevent spreadsheet applications from interpreting exported,
            // user-controlled text as a formula. The leading apostrophe is
            // displayed as text by Excel-compatible readers.
            var firstMeaningful = value.AsSpan().TrimStart();
            if (!firstMeaningful.IsEmpty &&
                firstMeaningful[0] is '=' or '+' or '-' or '@')
            {
                value = $"'{value}";
            }

            if (value.Contains('"', StringComparison.Ordinal))
            {
                value = value.Replace("\"", "\"\"", StringComparison.Ordinal);
            }

            return value.Contains(',', StringComparison.Ordinal) ||
                   value.Contains('\r', StringComparison.Ordinal) ||
                   value.Contains('\n', StringComparison.Ordinal) ||
                   value.Contains('"', StringComparison.Ordinal)
                ? $"\"{value}\""
                : value;
        }

        private static string Csv(decimal value) =>
            value.ToString("0.00####", System.Globalization.CultureInfo.InvariantCulture);
    }
}
