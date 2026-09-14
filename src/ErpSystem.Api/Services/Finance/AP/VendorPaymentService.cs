using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Services.Finance.AP
{
    /// <summary>
    /// Manages vendor payments, allocations, early-payment discounts, and payment batches.
    /// </summary>
    public partial class VendorPaymentService : IVendorPaymentService
    {
        private const int DefaultOutstandingInvoicePageSize = 50;
        private const int MaximumOutstandingInvoicePageSize = 100;
        // Kept local until the shared Finance audit catalogue is updated by its owner.
        private const string SupplierDebitApplicationReservedEvent = "Finance.APSupplierDebitNote.ApplicationReserved";
        private const string SupplierDebitApplicationReversedEvent = "Finance.APSupplierDebitNote.ApplicationReversed";
        private const string SupplierDebitApplicationFinalizedEvent = "Finance.APSupplierDebitNote.ApplicationFinalized";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<VendorPaymentService> _logger;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IWorkflowService _workflowService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IFxAccountingService? _fxAccountingService;
        private readonly IFinanceAccessScopeService _financeAccessScopeService;
        private readonly IFinanceReversalPolicyService _financeReversalPolicyService;
        private readonly IWorkflowApprovalPolicyResolver? _approvalPolicyResolver;
        private readonly IWithholdingTaxCertificateService? _withholdingTaxService;
        private readonly IExchangeRateService? _exchangeRateService;
        private readonly IApSupplierIdentityService? _apSupplierIdentityService;

        private static readonly JsonSerializerOptions PaymentControlJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        private readonly IVendorInvoiceService? _vendorInvoiceService;
        private readonly IProcurementControlEventService? _procurementControlEvents;
        private readonly IProcurementInvoicePaymentSodService? _invoicePaymentSod;

        public VendorPaymentService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ILogger<VendorPaymentService> logger,
            IDocumentNumberingService documentNumberingService,
            IWorkflowService workflowService,
            IFinanceAccessScopeService financeAccessScopeService,
            IFinanceReversalPolicyService financeReversalPolicyService,
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            IFxAccountingService? fxAccountingService = null,
            IWorkflowApprovalPolicyResolver? approvalPolicyResolver = null,
            IWithholdingTaxCertificateService? withholdingTaxService = null,
            IVendorInvoiceService? vendorInvoiceService = null,
            IProcurementControlEventService? procurementControlEvents = null,
            IProcurementInvoicePaymentSodService? invoicePaymentSod = null,
            IExchangeRateService? exchangeRateService = null,
            IApSupplierIdentityService? apSupplierIdentityService = null,
            IControlledFileUploadService? controlledFiles = null,
            ICentralDocumentRepositoryFileService? centralDocuments = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _workflowService = workflowService;
            _financeAccessScopeService = financeAccessScopeService;
            _financeReversalPolicyService = financeReversalPolicyService;
            _financePostingEngine = financePostingEngine;
            _financeAuditService = financeAuditService;
            _fxAccountingService = fxAccountingService;
            _approvalPolicyResolver = approvalPolicyResolver;
            _withholdingTaxService = withholdingTaxService;
            _vendorInvoiceService = vendorInvoiceService;
            _procurementControlEvents = procurementControlEvents;
            _invoicePaymentSod = invoicePaymentSod;
            _exchangeRateService = exchangeRateService;
            _apSupplierIdentityService = apSupplierIdentityService;
            _controlledFiles = controlledFiles;
            _centralDocuments = centralDocuments;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";
        private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

        // ═════════════════════════════════════════════════════════════════
        //  GET
        // ═════════════════════════════════════════════════════════════════

        public async Task<VendorPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var permittedBankAccountIds = await _financeAccessScopeService
                .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read, cancellationToken);
            var query = _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id);
            if (permittedBankAccountIds != null)
            {
                // A restricted user must never learn that an out-of-scope payment exists. Applying
                // the scope in SQL makes the result indistinguishable from an unknown identifier.
                query = query.Where(payment =>
                    payment.BankAccountId.HasValue &&
                    permittedBankAccountIds.Contains(payment.BankAccountId.Value));
            }

            var payment = await query
                .Include(p => p.Supplier)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.VendorInvoice)
                .Include(p => p.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                    .ThenInclude(application => application.SupplierDebitNote)
                        .ThenInclude(note => note.Vendor)
                .Include(p => p.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                    .ThenInclude(application => application.VendorInvoice)
                .Include(p => p.BankAccount)
                .Include(p => p.ConfiguredPaymentMethod)
                .FirstOrDefaultAsync(cancellationToken);

            return payment == null ? null : MapToDto(payment);
        }

        public async Task<VendorPaymentTraceDto?> GetTraceAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                .Include(item => item.Supplier)
                .Include(item => item.BankAccount)
                .Include(item => item.ConfiguredPaymentMethod)
                .Include(item => item.Allocations)
                    .ThenInclude(item => item.VendorInvoice)
                .Include(item => item.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                    .ThenInclude(application => application.SupplierDebitNote)
                        .ThenInclude(note => note.Vendor)
                .Include(item => item.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                    .ThenInclude(application => application.VendorInvoice)
                .FirstOrDefaultAsync(cancellationToken);
            if (payment == null)
                return null;

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Read,
                cancellationToken);

            var allocationIds = payment.Allocations.Select(item => item.Id).ToList();
            var postingEvents = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    !item.IsDeleted &&
                    ((item.SourceDocumentType == "VendorPayment" && item.SourceDocumentId == payment.Id) ||
                     (allocationIds.Contains(item.SourceDocumentId) &&
                      (item.SourceDocumentType == "VendorPaymentAllocation" ||
                       item.SourceDocumentType == "VendorPaymentAdvanceApplication"))))
                .Include(item => item.JournalEntry)
                    .ThenInclude(item => item!.Transactions)
                        .ThenInclude(item => item.Account)
                .OrderBy(item => item.PostingDate)
                .ThenBy(item => item.RequestedAt)
                .ToListAsync(cancellationToken);

            var auditEvents = await _unitOfWork.Repository<AuditLog>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.Resource == "Finance.APPayment" &&
                    item.ResourceId == payment.Id.ToString())
                .AsNoTracking()
                .OrderBy(item => item.Timestamp)
                .ToListAsync(cancellationToken);

            var trace = new VendorPaymentTraceDto
            {
                Payment = MapToDto(payment),
                Postings = postingEvents.Select(MapPostingTrace).ToList(),
                AuditEvents = auditEvents.Select(item => new FinanceAuditTraceDto
                {
                    AuditLogId = item.Id,
                    EventType = item.Action,
                    Timestamp = item.Timestamp,
                    UserId = item.UserId,
                    Username = item.Username,
                    BeforeValuesJson = item.OldValues,
                    DetailsJson = item.NewValues
                }).ToList()
            };

            await RecordApPaymentAuditAsync(
                FinanceAuditEvents.ApPaymentTraceViewed,
                payment,
                afterValues: new
                {
                    PostingCount = trace.Postings.Count,
                    AuditEventCount = trace.AuditEvents.Count
                },
                comment: "AP payment source-to-ledger trace viewed.",
                cancellationToken: cancellationToken);

            return trace;
        }

        public async Task<PagedResult<VendorPaymentDto>> GetAllAsync(VendorPaymentQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId);

            var permittedBankAccountIds = await _financeAccessScopeService
                .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read, cancellationToken);
            if (permittedBankAccountIds != null)
            {
                queryable = queryable.Where(payment =>
                    payment.BankAccountId.HasValue &&
                    permittedBankAccountIds.Contains(payment.BankAccountId.Value));
            }

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                queryable = queryable.Where(p =>
                    p.PaymentNumber.Contains(query.SearchTerm) ||
                    p.Supplier.Name.Contains(query.SearchTerm) ||
                    (p.TransactionReference != null && p.TransactionReference.Contains(query.SearchTerm)) ||
                    (p.ChequeNumber != null && p.ChequeNumber.Contains(query.SearchTerm)));
            }

            if (query.SupplierId.HasValue)
            {
                var resolvedSupplierId = await ResolveSupplierIdForQueryAsync(query.SupplierId.Value, cancellationToken);
                queryable = resolvedSupplierId.HasValue
                    ? queryable.Where(p => p.SupplierId == resolvedSupplierId.Value)
                    : queryable.Where(p => false);
            }

            if (query.Status.HasValue)
                queryable = queryable.Where(p => p.Status == query.Status.Value);

            if (query.PaymentMethod.HasValue)
                queryable = queryable.Where(p => p.PaymentMethod == query.PaymentMethod.Value);

            if (query.PaymentMethodId.HasValue)
                queryable = queryable.Where(p => p.PaymentMethodId == query.PaymentMethodId.Value);

            if (query.PaymentBatchId.HasValue)
                queryable = queryable.Where(p => p.PaymentBatchId == query.PaymentBatchId.Value);

            if (query.FromDate.HasValue)
                queryable = queryable.Where(p => p.PaymentDate >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                queryable = queryable.Where(p => p.PaymentDate <= query.ToDate.Value);

            var totalCount = await queryable.CountAsync(cancellationToken);

            queryable = query.SortBy?.ToLower() switch
            {
                "paymentnumber" => query.SortDescending
                    ? queryable.OrderByDescending(p => p.PaymentNumber)
                    : queryable.OrderBy(p => p.PaymentNumber),
                "supplier" => query.SortDescending
                    ? queryable.OrderByDescending(p => p.Supplier.Name)
                    : queryable.OrderBy(p => p.Supplier.Name),
                "amount" => query.SortDescending
                    ? queryable.OrderByDescending(p => p.TotalAmount)
                    : queryable.OrderBy(p => p.TotalAmount),
                _ => queryable.OrderByDescending(p => p.PaymentDate)
            };

            var payments = await queryable
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Include(p => p.Supplier)
                .Include(p => p.BankAccount)
                .Include(p => p.ConfiguredPaymentMethod)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.VendorInvoice)
                .ToListAsync(cancellationToken);

            return new PagedResult<VendorPaymentDto>
            {
                Items = payments.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<List<PostedSupplierAdvanceDto>> GetPostedSupplierAdvancesAsync(
            Guid supplierOrBusinessPartnerId,
            CancellationToken cancellationToken = default)
        {
            if (supplierOrBusinessPartnerId == Guid.Empty)
                return new List<PostedSupplierAdvanceDto>();

            var supplierId = await ResolveSupplierIdForQueryAsync(supplierOrBusinessPartnerId, cancellationToken);
            if (!supplierId.HasValue)
                return new List<PostedSupplierAdvanceDto>();

            return await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(payment =>
                    payment.TenantId == TenantId &&
                    !payment.IsDeleted &&
                    payment.SupplierId == supplierId.Value &&
                    payment.IsSupplierAdvance &&
                    payment.JournalEntryId.HasValue &&
                    payment.TotalAmount > payment.AllocatedAmount &&
                    payment.Status != VendorPaymentStatus.Voided &&
                    payment.Status != VendorPaymentStatus.Failed &&
                    payment.Status != VendorPaymentStatus.Reversed)
                .AsNoTracking()
                .OrderByDescending(payment => payment.PaymentDate)
                .Select(payment => new PostedSupplierAdvanceDto
                {
                    Id = payment.Id,
                    PaymentNumber = payment.PaymentNumber,
                    SupplierId = payment.SupplierId,
                    SupplierName = payment.Supplier.Name,
                    PaymentDate = payment.PaymentDate,
                    TotalAmount = payment.TotalAmount,
                    AllocatedAmount = payment.AllocatedAmount,
                    AvailableAmount = payment.TotalAmount - payment.AllocatedAmount,
                    CurrencyCode = payment.CurrencyCode,
                    Status = payment.Status,
                    JournalEntryId = payment.JournalEntryId!.Value
                })
                .ToListAsync(cancellationToken);
        }

        // ═════════════════════════════════════════════════════════════════
        //  CREATE PAYMENT
        // ═════════════════════════════════════════════════════════════════

        public Task<VendorPaymentDto> CreateAsync(
            VendorPaymentCreateDto dto,
            CancellationToken cancellationToken = default) =>
            CreateAsync(dto, cancellationToken, executionStrategyScope: false);

        private async Task<VendorPaymentDto> CreateAsync(
            VendorPaymentCreateDto dto,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (dto.Allocations?.Any() == true &&
                !_unitOfWork.HasActiveTransaction &&
                !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => CreateAsync(dto, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            var supplier = await ResolveSupplierForPaymentAsync(dto.SupplierId, cancellationToken);

            // Resolve and persist the effective account, including the tenant default. Leaving the
            // source field null would make later list, trace, and approval scope decisions depend on
            // a setting that could change after the payment was created.
            var effectiveBankAccountId = await ResolveBankAccountIdForScopeAsync(
                dto.BankAccountId,
                cancellationToken);
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                effectiveBankAccountId,
                FinanceAccessLevel.Operate,
                cancellationToken);

            var paymentNumber = await GeneratePaymentNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var paymentCurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode)
                ? baseCurrencyCode
                : dto.CurrencyCode.Trim().ToUpperInvariant();
            // Production DI supplies the tenant-scoped exchange-rate service. Resolving the rate
            // here freezes the approved source before allocations are calculated; tests that do
            // not exercise FX may continue to use the explicit functional/same-currency value.
            var paymentRate = await ResolveApprovedSettlementRateAsync(
                paymentCurrencyCode,
                baseCurrencyCode,
                dto.PaymentDate,
                dto.ExchangeRateId,
                dto.ExchangeRate,
                // An unallocated foreign payment becomes a supplier-advance currency lot. Its
                // original carrying value will be released during later applications, so the
                // origin rate must be just as authoritative as an immediately allocated payment.
                requireApprovedSource: dto.Allocations?.Any() == true ||
                    !string.Equals(paymentCurrencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase),
                cancellationToken);
            var configuredPaymentMethod = await ResolveConfiguredPaymentMethodAsync(
                dto.PaymentMethodId,
                effectiveBankAccountId,
                dto.TransactionReference ?? dto.ChequeNumber,
                "vendor payment",
                enforceReference: true,
                cancellationToken);
            var paymentMethod = configuredPaymentMethod == null
                ? dto.PaymentMethod
                : MapConfiguredPaymentMethodToVendorPaymentMethod(configuredPaymentMethod.Type);

            // WHT is a configured-tax decision, not a free-form rate calculation. The server
            // recomputes the annual supplier threshold and requires allocation totals to match
            // the statutory result so a stale browser cannot bypass the Finance control.
            // WHT thresholds and statutory registers are functional-currency controls. Once one
            // payment can settle invoices in several currencies, summing native WHT amounts at
            // the header is mathematically invalid. Resolve each invoice's approved settlement
            // rate first and compare the configured calculation with the functional allocation
            // total. The allocation still retains the invoice-native amount for aging.
            var allocationWhtFunctionalAmount = 0m;
            var allocationSettlementFunctionalBase = 0m;
            var withholdingInvoices = new List<VendorInvoice>();
            if (dto.Allocations?.Any() == true)
            {
                foreach (var requestedAllocation in dto.Allocations)
                {
                    var invoice = await _unitOfWork.Repository<VendorInvoice>()
                        .FirstOrDefaultAsync(candidate =>
                            candidate.TenantId == TenantId &&
                            candidate.Id == requestedAllocation.VendorInvoiceId &&
                            !candidate.IsDeleted);
                    if (invoice == null)
                        throw new KeyNotFoundException($"Vendor invoice with Id '{requestedAllocation.VendorInvoiceId}' not found.");
                    if (invoice.SupplierId != supplier.Id)
                        throw new InvalidOperationException("A selected invoice does not belong to this payment supplier.");
                    withholdingInvoices.Add(invoice);

                    var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, baseCurrencyCode);
                    var invoiceRate = await ResolveApprovedSettlementRateAsync(
                        invoiceCurrency,
                        baseCurrencyCode,
                        dto.PaymentDate,
                        requestedAllocation.InvoiceSettlementExchangeRateId,
                        string.Equals(invoiceCurrency, paymentCurrencyCode, StringComparison.OrdinalIgnoreCase)
                            ? paymentRate.Rate
                            : invoice.ExchangeRate,
                        requireApprovedSource: !string.Equals(invoiceCurrency, baseCurrencyCode, StringComparison.OrdinalIgnoreCase),
                        cancellationToken);
                    allocationWhtFunctionalAmount += RoundMoney(
                        Math.Max(requestedAllocation.WithholdingTaxAmount, 0m) * invoiceRate.Rate);
                    allocationSettlementFunctionalBase += RoundMoney((
                        Math.Max(requestedAllocation.AllocatedAmount, 0m) +
                        Math.Max(requestedAllocation.DiscountAmount, 0m) +
                        Math.Max(requestedAllocation.WithholdingTaxAmount, 0m)) * invoiceRate.Rate);
                }
            }
            allocationWhtFunctionalAmount = RoundMoney(allocationWhtFunctionalAmount);
            var invoiceWithholding = ApInvoiceWithholdingPolicy.Resolve(withholdingInvoices, dto.WithholdingTaxId);
            if (invoiceWithholding != null)
                dto.WithholdingTaxId = invoiceWithholding.TaxId;
            allocationSettlementFunctionalBase = RoundMoney(allocationSettlementFunctionalBase);
            var requestedWhtAmount = allocationWhtFunctionalAmount;
            // The header value is retained for API compatibility and functional-currency
            // reporting, but allocation rows are now the authoritative source. Reject a stale
            // client total instead of silently accepting two contradictory WHT representations.
            // Zero is the documented cross-currency client sentinel requesting server derivation;
            // only a positive legacy/header assertion is compared with the line roll-up.
            if (dto.WithholdingTaxAmount is > 0m &&
                Math.Abs(RoundMoney(dto.WithholdingTaxAmount.Value - requestedWhtAmount)) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"Payment WHT total {dto.WithholdingTaxAmount.Value:N2} does not match the line-level functional WHT total {requestedWhtAmount:N2}. Recalculate the payment before saving.");
            }
            WhtCalculationResultDto? whtCalculation = null;
            if (dto.WithholdingTaxId.HasValue)
            {
                if (dto.Allocations?.Any() != true)
                {
                    throw new InvalidOperationException("Configured WHT may only be applied to allocated supplier invoices; supplier advances must not carry WHT.");
                }
                if (_withholdingTaxService == null)
                {
                    throw new InvalidOperationException("WHT compliance service is not configured.");
                }

                var taxableBase = RoundMoney(dto.WithholdingTaxBaseAmount is > 0m
                    ? dto.WithholdingTaxBaseAmount.Value
                    : allocationSettlementFunctionalBase);
                whtCalculation = await _withholdingTaxService.CalculateApWithholdingAsync(new WhtCalculationRequestDto
                {
                    TaxId = dto.WithholdingTaxId.Value,
                    SupplierId = supplier.Id,
                    PaymentDate = dto.PaymentDate,
                    TaxableBase = taxableBase,
                    VendorInvoiceIds = withholdingInvoices.Select(invoice => invoice.Id).Distinct().ToList()
                }, cancellationToken);

                if (Math.Abs(RoundMoney(requestedWhtAmount - whtCalculation.WithholdingAmount)) > 0.01m)
                {
                    throw new InvalidOperationException(
                        $"WHT allocations total {requestedWhtAmount:N2}, but configured tax {whtCalculation.TaxCode} requires {whtCalculation.WithholdingAmount:N2}. Recalculate the payment before saving.");
                }
            }
            else if (requestedWhtAmount > 0m || dto.WithholdingTaxRate > 0m)
            {
                throw new InvalidOperationException("Select an active configured WHT tax before entering a withholding amount or rate.");
            }

            var whtAmount = whtCalculation?.WithholdingAmount ?? 0m;

            var payment = new VendorPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = paymentNumber,
                SupplierId = supplier.Id,
                PaymentDate = dto.PaymentDate,
                TotalAmount = dto.TotalAmount,
                AllocatedAmount = 0,
                PaymentMethod = paymentMethod,
                PaymentMethodId = configuredPaymentMethod?.Id,
                CurrencyCode = paymentCurrencyCode,
                ExchangeRate = paymentRate.Rate,
                ExchangeRateId = paymentRate.Id,
                BankAccountId = effectiveBankAccountId,
                ChequeNumber = dto.ChequeNumber,
                TransactionReference = dto.TransactionReference,
                WithholdingTaxRate = whtCalculation?.TaxRate ?? 0m,
                WithholdingTaxAmount = whtAmount,
                WithholdingTaxBaseAmount = whtCalculation?.TaxableBase ?? 0m,
                WithholdingTaxCumulativeBefore = whtCalculation?.CumulativeBefore ?? 0m,
                WithholdingTaxThresholdAmount = whtCalculation?.ThresholdAmount,
                WithholdingTaxThresholdApplied = whtCalculation?.ThresholdApplied ?? false,
                WithholdingTaxCalculationNote = whtCalculation?.CalculationNote,
                WithholdingTaxId = dto.WithholdingTaxId,
                WithholdingTaxAccountId = whtCalculation?.TaxPayableAccountId,
                WithholdingCertificateNumber = dto.WithholdingCertificateNumber,
                WithholdingCertificateDate = dto.WithholdingCertificateDate,
                Status = VendorPaymentStatus.Draft,
                Notes = dto.Notes,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
            };

            if (dto.Allocations?.Any() == true && !_unitOfWork.HasActiveTransaction)
            {
                foreach (var invoiceId in dto.Allocations.Select(item => item.VendorInvoiceId).Distinct())
                    await RequirePaymentReadinessAsync(
                        invoiceId,
                        ProcurementPaymentReadinessRules.AllocateAction,
                        payment.Id,
                        batchId: null,
                        cancellationToken);
            }

            var ownsTransaction = dto.Allocations?.Any() == true && !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
            await _unitOfWork.Repository<VendorPayment>().AddAsync(payment);

            // If allocations were provided, process them
            if (dto.Allocations?.Any() == true)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await AllocatePaymentAsync(payment.Id, dto.Allocations, cancellationToken);
                // Refresh the payment to get updated amounts
                payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(p => p.Id == payment.Id)
                    .Include(p => p.Supplier)
                    .Include(p => p.BankAccount)
                    .Include(p => p.ConfiguredPaymentMethod)
                    .Include(p => p.Allocations)
                        .ThenInclude(a => a.VendorInvoice)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (ownsTransaction)
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }

            _logger.LogInformation("Created vendor payment {PaymentNumber} for supplier {SupplierId}, amount {Amount}",
                paymentNumber, supplier.Id, dto.TotalAmount);

            if (payment == null)
                throw new InvalidOperationException("Failed to load created vendor payment.");

            return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
        }

        /// <summary>
        /// Submits a direct payment into the existing workflow engine. The selected effective-dated
        /// policy is snapshotted before workflow creation so later configuration retirement cannot
        /// obscure which evidence and monetary authority rules governed this payment.
        /// </summary>
        public async Task<VendorPaymentDto> SubmitAsync(
            Guid id,
            SubmitVendorPaymentDto dto,
            CancellationToken cancellationToken = default)
        {
            if (_approvalPolicyResolver == null)
                throw new InvalidOperationException("The AP payment approval policy resolver is not configured.");
            if (CurrentUserId == Guid.Empty)
                throw new UnauthorizedAccessException("An authenticated Finance user is required to submit a payment.");

            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                .Include(item => item.BankAccount)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Vendor payment with Id '{id}' not found.");

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (payment.PaymentBatchId.HasValue)
                throw new InvalidOperationException("Payments created by a payment batch must use the batch approval workflow.");
            if (payment.Status != VendorPaymentStatus.Draft)
                throw new InvalidOperationException("Only a draft direct payment can be submitted for approval.");
            if (payment.TotalAmount <= 0m)
                throw new InvalidOperationException("A payment must have a positive amount before submission.");

            if (!await IsPaymentApprovalRequiredAsync("VendorPayment", payment.Id))
                return await SubmitWithoutApprovalAsync(id, dto, cancellationToken);

            var baseCurrency = (await _tenantSettingsService.GetBaseCurrencyAsync()).Trim().ToUpperInvariant();
            var functionalAmount = decimal.Round(
                payment.TotalAmount * (payment.ExchangeRate <= 0m ? 1m : payment.ExchangeRate),
                2,
                MidpointRounding.AwayFromZero);
            var now = DateTime.UtcNow;
            var resolution = await _approvalPolicyResolver.ResolveAsync(
                new WorkflowApprovalPolicyContext(
                    TenantId,
                    "Vendor Payment",
                    now,
                    Module: "Finance",
                    Category: payment.PaymentMethod.ToString(),
                    Amount: functionalAmount,
                    CurrencyCode: baseCurrency),
                cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No published AP payment approval policy covers {baseCurrency} {functionalAmount:N2} using {payment.PaymentMethod}.");

            var control = resolution.ApprovalConfig;
            var minimumReasonLength = Math.Max(control.MinimumExceptionReasonLength, 1);
            var exceptionalReason = dto.ExceptionalPaymentReason?.Trim();
            var evidenceExceptionReason = dto.EvidenceExceptionReason?.Trim();

            if (dto.IsExceptionalPayment && (exceptionalReason?.Length ?? 0) < minimumReasonLength)
                throw new InvalidOperationException(
                    $"Exceptional payments require a reason of at least {minimumReasonLength} characters.");
            if (dto.RequestEvidenceException && !control.AllowEvidenceException)
                throw new InvalidOperationException("The applied payment policy does not permit an evidence exception.");
            if (dto.RequestEvidenceException && (evidenceExceptionReason?.Length ?? 0) < minimumReasonLength)
                throw new InvalidOperationException(
                    $"Evidence exception requests require a reason of at least {minimumReasonLength} characters.");

            var requiresManagingDirector =
                control.RequiresManagingDirectorApproval ||
                dto.IsExceptionalPayment ||
                dto.RequestEvidenceException;
            var managingDirectorRole = string.IsNullOrWhiteSpace(control.ManagingDirectorApproverRole)
                ? "Managing Director"
                : control.ManagingDirectorApproverRole.Trim();
            if (requiresManagingDirector && !control.ApproverRules.Any(rule =>
                    rule.AssignmentType == WorkflowAssignmentType.Role &&
                    string.Equals(rule.Role, managingDirectorRole, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"The selected policy requires Managing Director authority but has no '{managingDirectorRole}' approver rule.");
            }

            var snapshotJson = JsonSerializer.Serialize(control, PaymentControlJsonOptions);
            var snapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));

            payment.Status = VendorPaymentStatus.PendingAuthorization;
            payment.SubmittedById = CurrentUserId;
            payment.SubmittedAt = now;
            payment.AppliedApprovalPolicySetId = resolution.PolicySetId;
            payment.AppliedApprovalPolicyCode = resolution.PolicyCode;
            payment.ApprovalControlSnapshotJson = snapshotJson;
            payment.ApprovalControlSnapshotHash = snapshotHash;
            payment.IsExceptionalPayment = dto.IsExceptionalPayment;
            payment.ExceptionalPaymentReason = dto.IsExceptionalPayment ? exceptionalReason : null;
            payment.RequiresManagingDirectorApproval = requiresManagingDirector;
            payment.ManagingDirectorApprovedById = null;
            payment.ManagingDirectorApprovedAt = null;
            payment.EvidenceExceptionRequested = dto.RequestEvidenceException;
            payment.EvidenceExceptionReason = dto.RequestEvidenceException ? evidenceExceptionReason : null;
            payment.EvidenceExceptionRequestedById = dto.RequestEvidenceException ? CurrentUserId : null;
            payment.EvidenceExceptionRequestedAt = dto.RequestEvidenceException ? now : null;
            payment.EvidenceExceptionApprovedById = null;
            payment.EvidenceExceptionApprovedAt = null;
            payment.UpdatedAt = now;
            payment.UpdatedBy = UserName;
            await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var workflowResult = await _workflowService.StartApprovalWorkflowAsync("VendorPayment", payment.Id);
                if (!workflowResult.Success || !workflowResult.WorkflowInstanceId.HasValue)
                    throw new InvalidOperationException(
                        workflowResult.Message ?? "Unable to start the direct-payment approval workflow.");

                payment.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
                payment.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await RecordApPaymentAuditAsync(
                    FinanceAuditEvents.ApPaymentSubmitted,
                    payment,
                    afterValues: new
                    {
                        payment.Status,
                        payment.SubmittedById,
                        payment.SubmittedAt,
                        payment.WorkflowInstanceId,
                        payment.AppliedApprovalPolicySetId,
                        payment.AppliedApprovalPolicyCode,
                        payment.ApprovalControlSnapshotHash,
                        FunctionalAmount = functionalAmount,
                        FunctionalCurrencyCode = baseCurrency,
                        payment.IsExceptionalPayment,
                        payment.RequiresManagingDirectorApproval,
                        payment.EvidenceExceptionRequested
                    },
                    comment: "Direct AP payment submitted under the snapshotted TDC evidence and authority policy.",
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Workflow creation can fail because tenant configuration is incomplete. Return the
                // payment to Draft so the maker can correct configuration and retry without a stuck
                // PendingAuthorization record that has no active workflow.
                payment.Status = VendorPaymentStatus.Draft;
                payment.WorkflowInstanceId = null;
                payment.SubmittedById = null;
                payment.SubmittedAt = null;
                // The policy snapshot belongs to a specific submission attempt. Clearing every
                // derived control value prevents a failed workflow start from making a Draft
                // payment appear submitted or from reusing a stale policy after configuration is
                // corrected and the maker retries.
                payment.AppliedApprovalPolicySetId = null;
                payment.AppliedApprovalPolicyCode = null;
                payment.ApprovalControlSnapshotJson = null;
                payment.ApprovalControlSnapshotHash = null;
                payment.IsExceptionalPayment = false;
                payment.ExceptionalPaymentReason = null;
                payment.RequiresManagingDirectorApproval = false;
                payment.ManagingDirectorApprovedById = null;
                payment.ManagingDirectorApprovedAt = null;
                payment.EvidenceExceptionRequested = false;
                payment.EvidenceExceptionReason = null;
                payment.EvidenceExceptionRequestedById = null;
                payment.EvidenceExceptionRequestedAt = null;
                payment.EvidenceExceptionApprovedById = null;
                payment.EvidenceExceptionApprovedAt = null;
                payment.UpdatedAt = DateTime.UtcNow;
                payment.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
        }

        public async Task<VendorPaymentControlDto?> GetControlAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (payment == null)
                return null;

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Read,
                cancellationToken);

            var control = DeserializePaymentControlSnapshot(payment.ApprovalControlSnapshotJson);
            var approvalRequired = payment.Status == VendorPaymentStatus.Draft
                ? await IsPaymentApprovalRequiredAsync("VendorPayment", payment.Id)
                : payment.ApprovalRequired;
            WorkflowApprovalPolicyResolution? previewResolution = null;
            if (control == null && payment.Status == VendorPaymentStatus.Draft && _approvalPolicyResolver != null)
            {
                var baseCurrency = (await _tenantSettingsService.GetBaseCurrencyAsync()).Trim().ToUpperInvariant();
                var functionalAmount = decimal.Round(
                    payment.TotalAmount * (payment.ExchangeRate <= 0m ? 1m : payment.ExchangeRate),
                    2,
                    MidpointRounding.AwayFromZero);
                previewResolution = await _approvalPolicyResolver.ResolveAsync(
                    new WorkflowApprovalPolicyContext(
                        TenantId,
                        "Vendor Payment",
                        DateTime.UtcNow,
                        Module: "Finance",
                        Category: payment.PaymentMethod.ToString(),
                        Amount: functionalAmount,
                        CurrencyCode: baseCurrency),
                    cancellationToken);
                control = previewResolution?.ApprovalConfig;
            }

            control ??= new ErpSystem.Core.DTOs.Workflow.WorkflowApprovalConfigDto();
            var instance = payment.WorkflowInstanceId.HasValue
                ? await _unitOfWork.Repository<WorkflowInstance>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == payment.WorkflowInstanceId.Value && !item.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var stepInstances = instance == null
                ? new List<WorkflowStepInstance>()
                : await _unitOfWork.Repository<WorkflowStepInstance>()
                    .GetQueryable(item => item.TenantId == TenantId && item.WorkflowInstanceId == instance.Id && !item.IsDeleted)
                    .Include(item => item.WorkflowStep)
                    .OrderBy(item => item.WorkflowStep.Order)
                    .ThenBy(item => item.CreatedDate)
                    .ToListAsync(cancellationToken);
            var stepIds = stepInstances.Select(item => item.Id).ToList();
            var evidence = stepIds.Count == 0
                ? new List<WorkflowEvidenceDocument>()
                : await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        stepIds.Contains(item.StepInstanceId) &&
                        item.IsCurrent &&
                        !item.IsDeleted)
                    .OrderBy(item => item.DocumentName)
                    .ThenByDescending(item => item.Version)
                    .ToListAsync(cancellationToken);

            var currentStep = stepInstances
                .Where(item => item.Status is WorkflowStepInstanceStatus.Pending or WorkflowStepInstanceStatus.InProgress)
                .OrderByDescending(item => item.StartedDate ?? item.CreatedDate)
                .FirstOrDefault();
            var requirementStatuses = control.EvidenceRequirements
                .Where(item => !string.IsNullOrWhiteSpace(item.RequirementKey))
                .Select(requirement =>
                {
                    var matching = evidence.Where(item =>
                        string.Equals(item.RequirementKey, requirement.RequirementKey, StringComparison.OrdinalIgnoreCase) &&
                        item.MalwareScanStatus == WorkflowMalwareScanStatus.Clean &&
                        (!item.ExpiryDate.HasValue || item.ExpiryDate.Value >= DateTime.UtcNow)).ToList();
                    // Match the approval gate: an uploader cannot independently verify their own
                    // supporting evidence for maker-checker purposes.
                    var verified = matching.Count(item =>
                        item.VerificationStatus == WorkflowEvidenceVerificationStatus.Verified &&
                        item.VerifiedById.HasValue &&
                        item.VerifiedById.Value != item.UploadedById);
                    var minimum = Math.Max(requirement.MinimumDocuments, 1);
                    return new VendorPaymentEvidenceRequirementStatusDto
                    {
                        RequirementKey = requirement.RequirementKey,
                        DocumentName = string.IsNullOrWhiteSpace(requirement.DocumentName)
                            ? requirement.RequirementKey
                            : requirement.DocumentName,
                        DocumentType = requirement.DocumentType,
                        MinimumDocuments = minimum,
                        RequireVerification = requirement.RequireVerification,
                        CurrentDocumentCount = matching.Count,
                        VerifiedDocumentCount = verified,
                        IsSatisfied = requirement.RequireVerification
                            ? verified >= minimum
                            : matching.Count >= minimum
                    };
                })
                .ToList();
            List<VendorPaymentEvidenceDocumentDto>? directEvidenceDocuments = null;
            if (!approvalRequired)
            {
                var directEvidence = await GetPaymentEvidenceReadinessAsync(payment, control, cancellationToken);
                requirementStatuses = directEvidence.Requirements;
                directEvidenceDocuments = directEvidence.Documents;
            }
            var evidenceSatisfied = requirementStatuses.All(item => item.IsSatisfied);
            var blockingReasons = requirementStatuses
                .Where(item => !item.IsSatisfied)
                .Select(item => item.RequireVerification
                    ? $"{item.DocumentName}: {item.VerifiedDocumentCount} of {item.MinimumDocuments} verified document(s)."
                    : $"{item.DocumentName}: {item.CurrentDocumentCount} of {item.MinimumDocuments} valid document(s).")
                .ToList();
            if (payment.EvidenceExceptionRequested && !payment.EvidenceExceptionApprovedById.HasValue)
                blockingReasons.Add("The evidence exception is pending Managing Director approval.");
            if (payment.RequiresManagingDirectorApproval && !payment.ManagingDirectorApprovedById.HasValue)
                blockingReasons.Add("Managing Director approval is still required.");
            if (!approvalRequired && control.RequiresManagingDirectorApproval)
                blockingReasons.Add("The payment policy requires separate Managing Director authority; it has not been waived.");
            if (!approvalRequired && control.SignaturePolicy?.IsRequired == true)
                blockingReasons.Add("The payment policy requires a mandatory signature; it has not been waived.");
            if (!string.IsNullOrWhiteSpace(payment.ApprovalControlSnapshotJson) &&
                !string.IsNullOrWhiteSpace(payment.ApprovalControlSnapshotHash))
            {
                var currentHash = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(payment.ApprovalControlSnapshotJson)));
                if (!string.Equals(currentHash, payment.ApprovalControlSnapshotHash, StringComparison.OrdinalIgnoreCase))
                {
                    blockingReasons.Add("The approval policy snapshot integrity check failed; authorization is disabled pending administrator review.");
                }
            }

            var canUploadEvidence = !approvalRequired && payment.Status == VendorPaymentStatus.Draft && !payment.PaymentBatchId.HasValue;
            if (canUploadEvidence)
            {
                var permittedBanks = await _financeAccessScopeService.GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Operate, cancellationToken);
                var paymentBank = await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken);
                canUploadEvidence = permittedBanks == null || (paymentBank.HasValue && permittedBanks.Contains(paymentBank.Value));
            }
            return new VendorPaymentControlDto
            {
                ApprovalRequired = approvalRequired,
                CanUploadEvidence = canUploadEvidence,
                PaymentId = payment.Id,
                PolicyCode = payment.AppliedApprovalPolicyCode ?? previewResolution?.PolicyCode,
                PolicySetId = payment.AppliedApprovalPolicySetId ?? previewResolution?.PolicySetId,
                PolicySnapshotHash = payment.ApprovalControlSnapshotHash,
                WorkflowInstanceId = payment.WorkflowInstanceId,
                WorkflowStatus = instance?.Status.ToString(),
                CurrentStepInstanceId = currentStep?.Id,
                CurrentStepName = currentStep?.WorkflowStep?.Name,
                IsExceptionalPayment = payment.IsExceptionalPayment,
                RequiresManagingDirectorApproval = payment.RequiresManagingDirectorApproval || control.RequiresManagingDirectorApproval,
                ManagingDirectorApprovalCompleted = payment.ManagingDirectorApprovedById.HasValue,
                EvidenceExceptionRequested = payment.EvidenceExceptionRequested,
                EvidenceExceptionApproved = payment.EvidenceExceptionApprovedById.HasValue,
                EvidenceRequirementsSatisfied = evidenceSatisfied,
                CanSubmit = payment.Status == VendorPaymentStatus.Draft &&
                    (approvalRequired ? previewResolution != null : CanCompleteWithoutApproval(control) && evidenceSatisfied && blockingReasons.Count == 0),
                MinimumExceptionReasonLength = Math.Max(control.MinimumExceptionReasonLength, 1),
                EvidenceRequirements = requirementStatuses,
                EvidenceDocuments = directEvidenceDocuments ?? evidence.Select(item => new VendorPaymentEvidenceDocumentDto
                {
                    Id = item.Id,
                    AttachmentId = item.AttachmentId,
                    RequirementKey = item.RequirementKey,
                    DocumentName = item.DocumentName,
                    DocumentType = item.DocumentType,
                    FileName = item.FileName,
                    VerificationStatus = item.VerificationStatus.ToString(),
                    MalwareScanStatus = item.MalwareScanStatus.ToString(),
                    UploadedAt = item.UploadedAt,
                    UploadedById = item.UploadedById,
                    VerifiedById = item.VerifiedById,
                    VerifiedAt = item.VerifiedAt,
                    VerificationNotes = item.VerificationNotes,
                    Sha256 = item.Sha256
                }).ToList(),
                BlockingReasons = blockingReasons
            };
        }

        private static ErpSystem.Core.DTOs.Workflow.WorkflowApprovalConfigDto? DeserializePaymentControlSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<ErpSystem.Core.DTOs.Workflow.WorkflowApprovalConfigDto>(
                    json,
                    PaymentControlJsonOptions);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("The AP payment control snapshot is invalid and requires administrator review.");
            }
        }

        // The incoming Procurement/Finance SoD slice exposes a compatibility submission method
        // used by its controller/service tests. Retain it beside the richer evidence-aware command
        // during this merge; both paths start the same canonical VendorPayment workflow and the
        // post/approval gates below revalidate the authoritative SoD evidence.
        public Task<VendorPaymentDto> SubmitForAuthorizationAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            SubmitForAuthorizationAsync(id, cancellationToken, executionStrategyScope: false);

        private async Task<VendorPaymentDto> SubmitForAuthorizationAsync(
            Guid id,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => SubmitForAuthorizationAsync(id, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            if (CurrentUserId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current payment submitter.");

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, id), cancellationToken);
                var payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                    .Include(item => item.Allocations.Where(allocation => !allocation.IsDeleted))
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Vendor payment with Id '{id}' not found.");

                if (payment.PaymentBatchId.HasValue)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_BATCH_SUBMISSION_BLOCKED",
                        "Batch-owned payments are submitted and approved only through their payment batch.");
                if (payment.Status != VendorPaymentStatus.Draft)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_SUBMISSION_STATE_INVALID",
                        "Only draft manual payments can be submitted for authorization.");

                if (!await IsPaymentApprovalRequiredAsync("VendorPayment", payment.Id))
                {
                    var completed = await SubmitWithoutApprovalAsync(id, new SubmitVendorPaymentDto(), cancellationToken);
                    if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
                    return completed;
                }

                var effectiveAllocations = GetEffectiveAllocations(payment.Allocations);
                EnsureAllocationTotalIsValid(payment, effectiveAllocations);
                foreach (var allocation in effectiveAllocations.OrderBy(item => item.VendorInvoiceId))
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, allocation.VendorInvoiceId), cancellationToken);
                    await RequirePaymentReadinessAsync(
                        allocation.VendorInvoiceId,
                        ProcurementPaymentReadinessRules.PostAction,
                        payment.Id,
                        batchId: null,
                        cancellationToken,
                        allowSettledInvoice: true);
                }

                payment.Status = VendorPaymentStatus.PendingAuthorization;
                payment.UpdatedAt = DateTime.UtcNow;
                payment.UpdatedBy = UserName;
                payment.LastModifiedById = CurrentUserId;
                await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var workflowResult = await _workflowService.StartApprovalWorkflowAsync("VendorPayment", payment.Id);
                if (!workflowResult.Success)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_WORKFLOW_START_FAILED",
                        workflowResult.Message ?? "Unable to start the vendor payment authorization workflow.");

                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);

                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  ALLOCATIONS
        // ═════════════════════════════════════════════════════════════════

        public Task<VendorPaymentDto> PostAsync(Guid id, CancellationToken cancellationToken = default) =>
            PostAsync(id, cancellationToken, executionStrategyScope: false);

        private async Task<VendorPaymentDto> PostAsync(
            Guid id,
            CancellationToken cancellationToken,
            bool executionStrategyScope,
            bool batchProcessorScope = false)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP payment posting.");
            if (_invoicePaymentSod == null)
                throw new VendorPaymentControlException(
                    ProcurementInvoicePaymentSodRules.EvidenceCode,
                    "The authoritative invoice/payment SOD service is not configured.");
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => PostAsync(id, cancellationToken, executionStrategyScope: true, batchProcessorScope),
                    cancellationToken);

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            VendorPayment? payment = null;
            try
            {
                // Every payment mutation, supplier-credit reservation and posting uses this same
                // lock. Invoice then debit-note locks are acquired in stable sorted order so the
                // reload-to-finalize sequence is one atomic settlement boundary.
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, id), cancellationToken);
                payment = await LoadPaymentForPostingAsync(id, cancellationToken);
                if (payment.PaymentBatchId.HasValue && !payment.JournalEntryId.HasValue && !batchProcessorScope)
                    throw new VendorPaymentControlException("AP_PAYMENT_BATCH_DIRECT_POST_BLOCKED",
                        "Batch-owned payments must be posted through Process on their payment batch.");
                var invoiceIds = payment.Allocations.Where(item => !item.IsDeleted)
                    .Select(item => item.VendorInvoiceId)
                    .Concat(payment.SupplierDebitNoteApplications.Where(item => !item.IsDeleted)
                        .Select(item => item.VendorInvoiceId))
                    .Distinct()
                    .OrderBy(item => item)
                    .ToList();
                foreach (var invoiceId in invoiceIds)
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, invoiceId), cancellationToken);
                foreach (var noteId in payment.SupplierDebitNoteApplications.Where(item => !item.IsDeleted)
                             .Select(item => item.SupplierDebitNoteId)
                             .Distinct()
                             .OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.SupplierDebitNote(TenantId, noteId), cancellationToken);

                await _invoicePaymentSod.RevalidatePaymentAuthorizationAsync(id, cancellationToken);
                await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                    await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                    FinanceAccessLevel.Operate,
                    cancellationToken);
                var wasAlreadyLinked = payment.JournalEntryId.HasValue;
                var postingRequest = await BuildApPaymentPostingRequestAsync(payment, cancellationToken);
                var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

                if (payment.JournalEntryId.HasValue && payment.JournalEntryId.Value != postingResult.JournalEntryId)
                    throw new InvalidOperationException("Vendor payment is linked to a different journal entry than the posting engine result.");
                if (!payment.JournalEntryId.HasValue)
                {
                    await ApplyPostedPaymentAllocationsAsync(payment, postingResult.PostingEventId, postingResult.JournalEntryId, cancellationToken);
                    await ApplyPostedSupplierDebitNoteApplicationsAsync(payment, postingResult.PostingEventId, postingResult.JournalEntryId, cancellationToken);
                    payment.JournalEntryId = postingResult.JournalEntryId;
                }
                if (payment.Status is VendorPaymentStatus.Authorized or VendorPaymentStatus.PendingAuthorization or VendorPaymentStatus.Failed)
                    payment.Status = VendorPaymentStatus.Processed;
                payment.UpdatedAt = DateTime.UtcNow;
                payment.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await RecordApPaymentAuditAsync(
                    postingResult.WasDuplicate || wasAlreadyLinked
                        ? FinanceAuditEvents.ApPaymentDuplicatePostingAttempt
                        : FinanceAuditEvents.ApPaymentPosted,
                    payment,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        postingResult.PostingEventId,
                        postingResult.JournalEntryId,
                        postingResult.JournalEntryNumber,
                        postingResult.TotalDebitAmount,
                        postingResult.TotalCreditAmount,
                        postingResult.FunctionalCurrencyCode,
                        postingResult.PostingDate,
                        postingResult.WasDuplicate
                    },
                    comment: postingResult.WasDuplicate || wasAlreadyLinked
                        ? "Duplicate AP payment posting request returned the existing posting."
                        : "AP payment posted through the central finance posting engine.",
                    cancellationToken: cancellationToken);
                await PostRealizedFxIfRequiredAsync(payment, cancellationToken);
                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Posted AP payment {PaymentNumber} through finance posting engine with journal {JournalEntryId}. Duplicate={WasDuplicate}",
                    payment.PaymentNumber,
                    postingResult.JournalEntryId,
                    postingResult.WasDuplicate);
                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
            }
            catch (Exception ex)
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                if (payment != null && ownsTransaction)
                {
                    await RecordApPaymentAuditAsync(
                        FinanceAuditEvents.ApPaymentPostingFailed,
                        payment,
                        afterValues: new { payment.JournalEntryId, error = ex.Message },
                        reason: ex.Message,
                        cancellationToken: cancellationToken);
                }
                _logger.LogError(ex, "Failed to post AP payment {PaymentId}", id);
                throw;
            }
        }

        public Task<VendorPaymentDto> ReversePaymentAsync(
            Guid id,
            ReverseVendorPaymentDto dto,
            CancellationToken cancellationToken = default) =>
            ReversePaymentAsync(id, dto, cancellationToken, executionStrategyScope: false);

        private async Task<VendorPaymentDto> ReversePaymentAsync(
            Guid id,
            ReverseVendorPaymentDto dto,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            ArgumentNullException.ThrowIfNull(dto);
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP payment reversal.");

            // SQL Server's retrying execution strategy must own the complete serializable
            // transaction. Starting a user transaction before entering the strategy causes every
            // production reversal to fail before the first write. The recursive scope mirrors the
            // existing AP create/allocation/authorization transaction boundary and remains safe
            // when a caller already owns a wider transaction.
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => ReversePaymentAsync(id, dto, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            var initialPayment = await LoadPaymentForPostingAsync(id, cancellationToken);
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(initialPayment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Approve,
                cancellationToken);

            // AP and AR deliberately share this evaluator so policy changes cannot produce
            // different reversal dates or narrative thresholds across the subledgers.
            var policyDecision = await _financeReversalPolicyService.ResolveAsync(
                initialPayment.PaymentDate,
                dto.Reason,
                dto.ReversalDate,
                cancellationToken);
            var reason = policyDecision.Reason;
            var reversalDate = policyDecision.ReversalDate;

            var transactionStarted = false;
            try
            {
                // Serializable isolation protects the source payment and invoice settlement totals
                // from a competing clear, allocation, or reversal operation while the compensating
                // journals and subledger records are created as one business transaction.
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                transactionStarted = true;

                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, id), cancellationToken);

                var payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                    .Include(item => item.Supplier)
                    .Include(item => item.BankAccount)
                    .Include(item => item.ConfiguredPaymentMethod)
                    .Include(item => item.Allocations)
                        .ThenInclude(item => item.VendorInvoice)
                    .Include(item => item.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                        .ThenInclude(application => application.SupplierDebitNote)
                    .Include(item => item.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                        .ThenInclude(application => application.VendorInvoice)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Vendor payment with Id '{id}' not found.");

                // A successful retry returns the existing result. The Finance posting engine also
                // uses deterministic idempotency keys, providing protection at both source and GL
                // layers if two requests reach the service together.
                if (payment.Status == VendorPaymentStatus.Reversed &&
                    payment.ReversalJournalEntryId.HasValue &&
                    payment.ReversalPostingEventId.HasValue)
                {
                    await _unitOfWork.CommitAsync(cancellationToken);
                    transactionStarted = false;
                    return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
                }

                if (!payment.JournalEntryId.HasValue)
                    throw new InvalidOperationException("Only a posted AP payment can be reversed.");
                if (payment.Status == VendorPaymentStatus.Reconciled)
                {
                    throw new InvalidOperationException(
                        "A reconciled AP payment must first be removed from its bank reconciliation before reversal.");
                }
                if (payment.Status is not (VendorPaymentStatus.Processed or VendorPaymentStatus.Cleared))
                    throw new InvalidOperationException("Only a processed or cleared AP payment can be reversed.");

                var activeAllocations = payment.Allocations
                    .Where(item => !item.IsReversal && !item.IsDeleted)
                    .OrderBy(item => item.AllocationDate)
                    .ThenBy(item => item.Id)
                    .ToList();
                var activeSupplierDebitApplications = GetEffectiveSupplierDebitNoteApplications(
                    payment.SupplierDebitNoteApplications);

                // Ordinary invoice-settlement allocations are linked to the payment's own posting
                // event for traceability. Only a posted supplier advance creates a later, separate
                // application journal that must be unwound before the original cash payment.
                if (payment.IsSupplierAdvance &&
                    activeAllocations.Any(item => item.ApplicationPostingEventId.HasValue))
                {
                    // A supplier advance application has its own reclassification journal. Reversing
                    // the original cash payment without first unwinding that application would leave
                    // AP control and the supplier-advance account inconsistent.
                    throw new InvalidOperationException(
                        "This supplier advance has posted applications. Reverse those applications before reversing the original payment.");
                }

                var originalPosting = await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.SourceDocumentType == "VendorPayment" &&
                        item.SourceDocumentId == payment.Id &&
                        item.PostingAction == "Post" &&
                        item.PostingStatus == "Posted" &&
                        !item.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The original AP payment posting event was not found.");

                var reversalPlan = await _financePostingEngine.GetReversalPlanAsync(
                    originalPosting.Id,
                    reason,
                    reversalDate,
                    cancellationToken);
                var reversalResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                {
                    SourceModule = "AP",
                    SourceDocumentType = "VendorPayment",
                    SourceDocumentId = payment.Id,
                    SourceDocumentTenantId = payment.TenantId,
                    PostingAction = "Reverse",
                    SourceDocumentReference = payment.PaymentNumber,
                    Description = $"Reverse vendor payment {payment.PaymentNumber} - {payment.Supplier.Name}",
                    PostingDate = reversalDate,
                    JournalType = "AP Payment Reversal",
                    BookClassification = "IFRS",
                    FunctionalCurrencyCode = originalPosting.FunctionalCurrencyCode,
                    ReversalOfJournalEntryId = reversalPlan.OriginalJournalEntryId,
                    ReversalReason = reason,
                    ReversalType = "SourceDocument",
                    IdempotencyKey = $"AP:VendorPayment:{payment.TenantId:N}:{payment.Id:N}:Reverse",
                    ReturnExistingOnDuplicate = true,
                    Lines = reversalPlan.ReversalLines.ToList()
                }, cancellationToken);

                // Realized FX postings are separate source events. Reverse each one inside the same
                // transaction so the payment cannot be reversed while its exchange gain/loss remains
                // in the ledger.
                var realizedSettlements = await _unitOfWork.Repository<FxRealizedSettlement>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.SettlementDocumentType == "VendorPayment" &&
                        item.SettlementDocumentId == payment.Id &&
                        item.Status == "Posted" &&
                        !item.IsDeleted)
                    .OrderBy(item => item.SettlementAllocationId)
                    .ToListAsync(cancellationToken);
                foreach (var settlement in realizedSettlements)
                {
                    if (!settlement.PostingEventId.HasValue || !settlement.JournalEntryId.HasValue)
                        throw new InvalidOperationException("A realized FX settlement is missing its original posting links.");

                    var fxPlan = await _financePostingEngine.GetReversalPlanAsync(
                        settlement.PostingEventId.Value,
                        reason,
                        reversalDate,
                        cancellationToken);
                    var fxResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = "FX",
                        SourceDocumentType = "VendorPaymentAllocation",
                        SourceDocumentId = settlement.SettlementAllocationId,
                        SourceDocumentTenantId = payment.TenantId,
                        PostingAction = "ReverseRealizedFx",
                        SourceDocumentReference = payment.PaymentNumber,
                        Description = $"Reverse AP realized FX for {payment.PaymentNumber}",
                        PostingDate = reversalDate,
                        JournalType = "Realized FX Reversal",
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = settlement.FunctionalCurrencyCode,
                        ReversalOfJournalEntryId = fxPlan.OriginalJournalEntryId,
                        ReversalReason = reason,
                        ReversalType = "SourceDocument",
                        IdempotencyKey = $"FX:Realized:AP:{payment.TenantId:N}:{settlement.SettlementAllocationId:N}:Reverse",
                        ReturnExistingOnDuplicate = true,
                        Lines = fxPlan.ReversalLines.ToList()
                    }, cancellationToken);

                    settlement.ReversalJournalEntryId = fxResult.JournalEntryId;
                    settlement.ReversalPostingEventId = fxResult.PostingEventId;
                    settlement.ReversedAt = DateTime.UtcNow;
                    settlement.ReversalReason = reason;
                    settlement.Status = "Reversed";
                    settlement.UpdatedAt = DateTime.UtcNow;
                    settlement.UpdatedBy = UserName;
                    await _unitOfWork.Repository<FxRealizedSettlement>().UpdateAsync(settlement);
                }

                var now = DateTime.UtcNow;
                foreach (var allocation in activeAllocations)
                {
                    var settledAmount = allocation.AllocatedAmount +
                                        allocation.DiscountAmount +
                                        allocation.WithholdingTaxAmount;
                    allocation.VendorInvoice.PaidAmount = Math.Max(
                        0m,
                        RoundMoney(allocation.VendorInvoice.PaidAmount - settledAmount));
                    allocation.VendorInvoice.Status = allocation.VendorInvoice.PaidAmount == 0m
                        ? VendorInvoiceStatus.Approved
                        : VendorInvoiceStatus.PartiallyPaid;
                    allocation.VendorInvoice.UpdatedAt = now;
                    allocation.VendorInvoice.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(allocation.VendorInvoice);

                    // Preserve the original settlement row and record a compensating allocation.
                    // Reports can therefore reconstruct both the original action and correction.
                    await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(new VendorPaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = payment.TenantId,
                        VendorPaymentId = payment.Id,
                        VendorInvoiceId = allocation.VendorInvoiceId,
                        AllocatedAmount = -allocation.AllocatedAmount,
                        PaymentCurrencyAmount = -allocation.PaymentCurrencyAmount,
                        InvoiceCurrencyCode = allocation.InvoiceCurrencyCode,
                        PaymentCurrencyCode = allocation.PaymentCurrencyCode,
                        IsCrossCurrency = allocation.IsCrossCurrency,
                        InvoiceSettlementExchangeRateId = allocation.InvoiceSettlementExchangeRateId,
                        InvoiceSettlementExchangeRate = allocation.InvoiceSettlementExchangeRate,
                        PaymentExchangeRateId = allocation.PaymentExchangeRateId,
                        PaymentExchangeRate = allocation.PaymentExchangeRate,
                        PaymentFunctionalAmount = -allocation.PaymentFunctionalAmount,
                        SettlementFunctionalAmount = -allocation.SettlementFunctionalAmount,
                        DiscountAmount = -allocation.DiscountAmount,
                        DiscountFunctionalAmount = -allocation.DiscountFunctionalAmount,
                        WithholdingTaxAmount = -allocation.WithholdingTaxAmount,
                        WithholdingTaxFunctionalAmount = -allocation.WithholdingTaxFunctionalAmount,
                        AllocationDate = reversalDate,
                        Notes = $"Payment reversal of allocation {allocation.Id}: {reason}",
                        IsReversal = true,
                        OriginalAllocationId = allocation.Id,
                        CreatedAt = now,
                        CreatedBy = UserName,
                        CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
                    });
                }

                foreach (var application in activeSupplierDebitApplications)
                {
                    var invoice = application.VendorInvoice;
                    if (invoice == null || invoice.TenantId != TenantId)
                        throw new InvalidOperationException(
                            "Supplier debit-note application references an invoice from another tenant.");
                    invoice.PaidAmount = Math.Max(
                        0m,
                        RoundMoney(invoice.PaidAmount - application.ApplicationAmount));
                    invoice.Status = invoice.PaidAmount <= 0.01m
                        ? VendorInvoiceStatus.Approved
                        : VendorInvoiceStatus.PartiallyPaid;
                    invoice.UpdatedAt = now;
                    invoice.UpdatedBy = UserName;
                    invoice.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                    await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);

                    await _unitOfWork.Repository<SupplierDebitNoteApplication>().AddAsync(
                        CreateSupplierDebitNoteApplicationReversal(
                            application,
                            now,
                            $"Payment reversal: {reason}",
                            reversalResult.PostingEventId,
                            reversalResult.JournalEntryId));
                }

                payment.Status = VendorPaymentStatus.Reversed;
                payment.AllocatedAmount = 0m;
                payment.ReversalJournalEntryId = reversalResult.JournalEntryId;
                payment.ReversalPostingEventId = reversalResult.PostingEventId;
                payment.ReversalDate = reversalDate;
                payment.ReversedAt = now;
                payment.ReversedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                payment.ReversalReason = reason;
                payment.UpdatedAt = now;
                payment.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await RecordApPaymentAuditAsync(
                    FinanceAuditEvents.ApPaymentReversed,
                    payment,
                    postingEventId: reversalResult.PostingEventId,
                    journalEntryId: reversalResult.JournalEntryId,
                    beforeValues: new
                    {
                        Status = initialPayment.Status,
                        initialPayment.JournalEntryId,
                        initialPayment.AllocatedAmount
                    },
                    afterValues: new
                    {
                        payment.Status,
                        payment.ReversalJournalEntryId,
                        payment.ReversalPostingEventId,
                        payment.ReversalDate,
                        ReversedAllocationCount = activeAllocations.Count,
                        ReversedSupplierDebitApplicationCount = activeSupplierDebitApplications.Count,
                        ReversedRealizedFxCount = realizedSettlements.Count
                    },
                    reason: reason,
                    comment: "Posted AP payment reversed through linked compensating Finance postings.",
                    cancellationToken: cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);
                transactionStarted = false;

                _logger.LogWarning(
                    "Reversed AP payment {PaymentNumber} with journal {ReversalJournalEntryId}. Reason: {Reason}",
                    payment.PaymentNumber,
                    payment.ReversalJournalEntryId,
                    reason);
                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
            }
            catch (Exception ex)
            {
                if (transactionStarted)
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    // A rollback reverts the database, not EF's in-memory entity states. Clear the
                    // failed graph before recording the audit event so its SaveChanges cannot
                    // accidentally persist part of the rejected AP correction.
                    _unitOfWork.ClearTrackedChanges();
                }

                await RecordApPaymentAuditAsync(
                    FinanceAuditEvents.ApPaymentReversalFailed,
                    initialPayment,
                    afterValues: new { PaymentId = id, ReversalDate = reversalDate, error = ex.Message },
                    reason: reason,
                    comment: "AP payment reversal failed before the correction could be committed.",
                    cancellationToken: cancellationToken);
                _logger.LogError(ex, "Failed to reverse AP payment {PaymentNumber}", initialPayment.PaymentNumber);
                throw;
            }
        }

        public Task<VendorPaymentAllocationResultDto> AllocatePaymentAsync(
            Guid paymentId,
            List<VendorPaymentAllocationCreateDto> allocations,
            CancellationToken cancellationToken = default) =>
            AllocatePaymentAsync(
                paymentId,
                allocations,
                cancellationToken,
                executionStrategyScope: false,
                batchProcessorScope: false);

        private async Task<VendorPaymentAllocationResultDto> AllocatePaymentAsync(
            Guid paymentId,
            List<VendorPaymentAllocationCreateDto> allocations,
            CancellationToken cancellationToken,
            bool executionStrategyScope,
            bool batchProcessorScope)
        {
            var duplicateInvoiceIds = allocations
                .GroupBy(item => item.VendorInvoiceId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateInvoiceIds.Count > 0)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_DUPLICATE_INVOICE_ALLOCATION",
                    "Each vendor invoice may appear only once in a payment allocation request.");

            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => AllocatePaymentAsync(
                        paymentId,
                        allocations,
                        cancellationToken,
                        executionStrategyScope: true,
                        batchProcessorScope),
                    cancellationToken);
            }

            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId)
                .Include(p => p.Allocations)
                .Include(p => p.PaymentBatch)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Vendor payment with Id '{paymentId}' not found.");

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (payment.JournalEntryId.HasValue)
            {
                if (!payment.IsSupplierAdvance)
                    throw new InvalidOperationException("Posted vendor payments cannot be allocated. Use a reversal, void, or adjustment workflow.");

                // A posted supplier advance is the explicit exception: applying it creates a new
                // AP-control/advance reclassification through the central posting engine.
                return await AllocatePostedSupplierAdvanceAsync(paymentId, allocations, cancellationToken);
            }

            EnsureAllocationMutationAllowed(payment, batchProcessorScope);

            if (!_unitOfWork.HasActiveTransaction)
            {
                foreach (var invoiceId in allocations.Select(item => item.VendorInvoiceId).Distinct())
                    await RequirePaymentReadinessAsync(
                        invoiceId,
                        ProcurementPaymentReadinessRules.AllocateAction,
                        paymentId,
                        payment.PaymentBatchId,
                        cancellationToken);
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, paymentId), cancellationToken);
                foreach (var invoiceId in allocations.Select(item => item.VendorInvoiceId).Distinct().OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, invoiceId), cancellationToken);

                payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId && !p.IsDeleted)
                    .Include(p => p.Allocations)
                    .Include(p => p.PaymentBatch)
                    .SingleAsync(cancellationToken);
                EnsureAllocationMutationAllowed(payment, batchProcessorScope);

            var whtInvoiceIds = allocations.Select(item => item.VendorInvoiceId)
                .Concat(payment.Allocations.Where(item => !item.IsDeleted && !item.IsReversal).Select(item => item.VendorInvoiceId))
                .Distinct().ToList();
            var whtInvoices = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(invoice =>
                invoice.TenantId == TenantId && !invoice.IsDeleted && invoice.SupplierId == payment.SupplierId &&
                whtInvoiceIds.Contains(invoice.Id)).ToListAsync(cancellationToken);
            if (whtInvoices.Count != whtInvoiceIds.Count)
                throw new InvalidOperationException("A selected WHT invoice does not belong to this payment supplier and tenant.");
            var whtDecision = ApInvoiceWithholdingPolicy.Resolve(whtInvoices, payment.WithholdingTaxId);
            if (whtDecision != null) payment.WithholdingTaxId = whtDecision.TaxId;

            var result = new VendorPaymentAllocationResultDto
            {
                PaymentId = paymentId
            };

            var now = DateTime.UtcNow;
            var createdAllocations = new List<VendorPaymentAllocation>();
            var functionalCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync(), "GHS");
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            var paymentRate = await ResolveApprovedSettlementRateAsync(
                paymentCurrency,
                functionalCurrency,
                payment.PaymentDate,
                payment.ExchangeRateId,
                payment.ExchangeRate,
                requireApprovedSource: !string.Equals(paymentCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

            foreach (var alloc in allocations)
            {
                var paymentDecision = await RequirePaymentReadinessAsync(
                    alloc.VendorInvoiceId,
                    ProcurementPaymentReadinessRules.AllocateAction,
                    paymentId,
                    batchId: payment.PaymentBatchId,
                    cancellationToken);
                var invoice = await _unitOfWork.Repository<VendorInvoice>()
                    .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == alloc.VendorInvoiceId);

                if (invoice == null)
                    throw new KeyNotFoundException($"Vendor invoice with Id '{alloc.VendorInvoiceId}' not found.");

                var balance = await GetInvoiceUnreservedBalanceAsync(
                    invoice,
                    payment.Id,
                    cancellationToken);
                if (balance <= 0)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_BALANCE_RESERVED",
                        $"Invoice '{invoice.InvoiceNumber}' has no unreserved outstanding balance.");

                if (invoice.SupplierId != payment.SupplierId)
                {
                    throw new InvalidOperationException($"Invoice '{invoice.InvoiceNumber}' does not belong to this payment's supplier.");
                }

                var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, functionalCurrency);
                var isCrossCurrency = !string.Equals(paymentCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase);
                if (isCrossCurrency && !alloc.PaymentCurrencyAmount.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Payment-currency amount is required to settle invoice '{invoice.InvoiceNumber}' in {invoiceCurrency} from a {paymentCurrency} payment.");
                }

                // AllocatedAmount is the invoice-currency cash equivalent. The separately supplied
                // payment amount is what consumes the payment balance; conflating them caused the
                // original FIN-LIM-0022 restriction.
                var requestedInvoiceCashAmount = Math.Max(alloc.AllocatedAmount, 0m);
                var requestedPaymentCashAmount = Math.Max(
                    alloc.PaymentCurrencyAmount ?? alloc.AllocatedAmount,
                    0m);
                var requestedDiscountAmount = Math.Max(alloc.DiscountAmount, 0m);
                var requestedWithholdingAmount = Math.Max(alloc.WithholdingTaxAmount, 0m);
                if (requestedWithholdingAmount > 0m && !payment.WithholdingTaxId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Select the configured AP WHT tax before allocating WHT to a supplier invoice.");
                }
                if (requestedDiscountAmount > 0m)
                {
                    if (invoice.EarlyPaymentDiscountPercentage <= 0m ||
                        !invoice.EarlyPaymentDiscountDueDate.HasValue ||
                        payment.PaymentDate.Date > invoice.EarlyPaymentDiscountDueDate.Value.Date)
                    {
                        throw new InvalidOperationException(
                            $"Invoice '{invoice.InvoiceNumber}' is not eligible for an early-payment discount on {payment.PaymentDate:yyyy-MM-dd}.");
                    }

                    var maximumDiscount = RoundMoney(balance * invoice.EarlyPaymentDiscountPercentage / 100m);
                    if (RoundMoney(requestedDiscountAmount) > maximumDiscount)
                    {
                        throw new InvalidOperationException(
                            $"Discount {requestedDiscountAmount:C} exceeds the eligible amount {maximumDiscount:C} for invoice '{invoice.InvoiceNumber}'.");
                    }

                    if (Math.Abs(RoundMoney(requestedInvoiceCashAmount + requestedDiscountAmount + requestedWithholdingAmount - balance)) > 0.01m)
                    {
                        throw new InvalidOperationException(
                            $"An early-payment discount may only be taken when invoice '{invoice.InvoiceNumber}' is fully settled by this allocation.");
                    }

                    var availableCash = Math.Max(payment.TotalAmount - payment.AllocatedAmount, 0m);
                    if (requestedPaymentCashAmount > availableCash)
                    {
                        throw new InvalidOperationException("The payment does not have enough unallocated cash to complete the discounted settlement.");
                    }
                }

                var availablePaymentCash = Math.Max(payment.TotalAmount - payment.AllocatedAmount, 0m);
                decimal allocAmount;
                decimal paymentCashAmount;
                decimal discountAmount;
                decimal withholdingTaxAmount;
                if (isCrossCurrency)
                {
                    // A cross-currency conversion is an explicit commercial decision. Silently
                    // clipping either side would change the agreed rate, so reject over-allocation
                    // and require the maker to correct the amounts.
                    if (requestedPaymentCashAmount > availablePaymentCash)
                        throw new InvalidOperationException("Cross-currency allocation exceeds the unallocated vendor-payment amount.");
                    if (RoundMoney(requestedInvoiceCashAmount + requestedDiscountAmount + requestedWithholdingAmount) > RoundMoney(balance))
                        throw new InvalidOperationException($"Cross-currency allocation would over-settle invoice '{invoice.InvoiceNumber}'.");

                    allocAmount = requestedInvoiceCashAmount;
                    paymentCashAmount = requestedPaymentCashAmount;
                    discountAmount = requestedDiscountAmount;
                    withholdingTaxAmount = requestedWithholdingAmount;
                }
                else
                {
                    // Preserve the established same-currency convenience behavior: the server may
                    // clip a draft allocation to the smaller invoice/payment balance.
                    var maxAllocatable = Math.Min(balance, availablePaymentCash);
                    allocAmount = Math.Min(requestedInvoiceCashAmount, maxAllocatable);
                    paymentCashAmount = allocAmount;
                    discountAmount = Math.Min(requestedDiscountAmount, Math.Max(balance - allocAmount, 0m));
                    withholdingTaxAmount = Math.Min(requestedWithholdingAmount, Math.Max(balance - allocAmount - discountAmount, 0m));
                }

                if (allocAmount <= 0 && discountAmount <= 0 && withholdingTaxAmount <= 0)
                {
                    result.Warnings.Add($"No funds available to allocate to invoice '{invoice.InvoiceNumber}'.");
                    continue;
                }

                var invoiceRate = await ResolveApprovedSettlementRateAsync(
                    invoiceCurrency,
                    functionalCurrency,
                    payment.PaymentDate,
                    alloc.InvoiceSettlementExchangeRateId,
                    string.Equals(invoiceCurrency, paymentCurrency, StringComparison.OrdinalIgnoreCase)
                        ? paymentRate.Rate
                        : invoice.ExchangeRate,
                    requireApprovedSource: !string.Equals(invoiceCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase),
                    cancellationToken);
                var settlement = CrossCurrencySettlementCalculator.CalculateWithDeductions(
                    paymentCurrency,
                    invoiceCurrency,
                    paymentCashAmount,
                    allocAmount,
                    discountAmount,
                    withholdingTaxAmount,
                    invoiceVatWithholdingAmount: 0m,
                    paymentRate.Rate,
                    invoiceRate.Rate);

                var allocation = new VendorPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorPaymentId = paymentId,
                    VendorInvoiceId = alloc.VendorInvoiceId,
                    AllocatedAmount = allocAmount,
                    PaymentCurrencyAmount = settlement.PaymentCurrencyAmount,
                    InvoiceCurrencyCode = settlement.InvoiceCurrency,
                    PaymentCurrencyCode = settlement.PaymentCurrency,
                    IsCrossCurrency = settlement.IsCrossCurrency,
                    InvoiceSettlementExchangeRateId = invoiceRate.Id,
                    InvoiceSettlementExchangeRate = settlement.InvoiceSettlementExchangeRate,
                    PaymentExchangeRateId = paymentRate.Id,
                    PaymentExchangeRate = settlement.PaymentExchangeRate,
                    PaymentFunctionalAmount = settlement.PaymentFunctionalAmount,
                    SettlementFunctionalAmount = settlement.SettlementFunctionalAmount,
                    DiscountAmount = discountAmount,
                    DiscountFunctionalAmount = settlement.DiscountFunctionalAmount,
                    WithholdingTaxAmount = withholdingTaxAmount,
                    WithholdingTaxFunctionalAmount = settlement.WithholdingFunctionalAmount,
                    AllocationDate = now,
                    Notes = alloc.Notes,
                    PaymentReadinessControlEventId = paymentDecision.Event.Id,
                    PaymentReadinessSnapshotHash = paymentDecision.Readiness.SnapshotHash,
                    PaymentReadinessEvaluatedAtUtc = paymentDecision.Readiness.EvaluatedAtUtc,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(allocation);
                createdAllocations.Add(allocation);

                // Allocation reserves the intended settlement only. The payable balance is
                // changed atomically with the successful Finance posting in PostAsync; a Draft
                // or merely Authorized payment must never make an invoice appear paid.
                payment.AllocatedAmount += settlement.PaymentCurrencyAmount;

                result.Allocations.Add(new VendorPaymentAllocationDto
                {
                    Id = allocation.Id,
                    VendorPaymentId = paymentId,
                    VendorInvoiceId = alloc.VendorInvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    AllocatedAmount = allocAmount,
                    PaymentCurrencyAmount = settlement.PaymentCurrencyAmount,
                    InvoiceCurrencyCode = settlement.InvoiceCurrency,
                    PaymentCurrencyCode = settlement.PaymentCurrency,
                    IsCrossCurrency = settlement.IsCrossCurrency,
                    InvoiceSettlementExchangeRateId = invoiceRate.Id,
                    InvoiceSettlementExchangeRate = settlement.InvoiceSettlementExchangeRate,
                    PaymentExchangeRateId = paymentRate.Id,
                    PaymentExchangeRate = settlement.PaymentExchangeRate,
                    PaymentFunctionalAmount = settlement.PaymentFunctionalAmount,
                    SettlementFunctionalAmount = settlement.SettlementFunctionalAmount,
                    DiscountAmount = discountAmount,
                    DiscountFunctionalAmount = settlement.DiscountFunctionalAmount,
                    WithholdingTaxAmount = withholdingTaxAmount,
                    WithholdingTaxFunctionalAmount = settlement.WithholdingFunctionalAmount,
                    AllocationDate = now,
                    Notes = alloc.Notes
                    ,PaymentReadinessControlEventId = paymentDecision.Event.Id
                    ,PaymentReadinessSnapshotHash = paymentDecision.Readiness.SnapshotHash
                    ,PaymentReadinessEvaluatedAtUtc = paymentDecision.Readiness.EvaluatedAtUtc
                });
            }

            // Allocation completeness does not advance the payment lifecycle. Manual payments
            // remain Draft until submitted and approved; batch payments remain Authorized until
            // their central Finance posting succeeds.

            var createdAllocationIds = createdAllocations.Select(a => a.Id).ToHashSet();
            // Header discount is a functional-currency roll-up. Native discounts remain on each
            // allocation because adding USD, EUR and GHS invoice deductions would be meaningless.
            var effectiveAllocations = GetEffectiveAllocations(
                payment.Allocations
                    .Where(item => !createdAllocationIds.Contains(item.Id))
                    .Concat(createdAllocations));
            payment.DiscountTaken = RoundMoney(effectiveAllocations.Sum(item => item.DiscountFunctionalAmount));
            // Allocation APIs remain available while a payment is a draft. Recalculate the
            // server-owned WHT threshold evidence after every allocation change so a caller
            // cannot bypass the configured Ghana WHT policy by adding lines after creation.
            await SynchronizeApWithholdingComplianceAsync(payment, effectiveAllocations, cancellationToken);
            payment.UpdatedAt = now;
            payment.UpdatedBy = UserName;
            await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (ownsTransaction)
                await _unitOfWork.CommitAsync(cancellationToken);

            result.TotalAllocated = payment.AllocatedAmount;
            result.RemainingUnallocated = payment.TotalAmount - payment.AllocatedAmount;

            _logger.LogInformation("Allocated {AllocCount} invoices to payment {PaymentId}, total allocated: {Total}",
                result.Allocations.Count, paymentId, result.TotalAllocated);

            return result;
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task<VendorPaymentAllocationResultDto> AllocatePostedSupplierAdvanceAsync(
            Guid paymentId,
            IReadOnlyCollection<VendorPaymentAllocationCreateDto> requestedAllocations,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for supplier advance application.");
            if (requestedAllocations.Count == 0)
                throw new InvalidOperationException("At least one supplier-invoice allocation is required to apply an advance.");
            if (requestedAllocations.Any(a => a.AllocatedAmount <= 0m || a.DiscountAmount != 0m || a.WithholdingTaxAmount != 0m))
                throw new InvalidOperationException("Supplier advance applications support positive cash allocations only; use a dedicated adjustment workflow for discounts or withholding.");

            if (!_unitOfWork.HasActiveTransaction)
            {
                foreach (var invoiceId in requestedAllocations.Select(item => item.VendorInvoiceId).Distinct())
                    await RequirePaymentReadinessAsync(
                        invoiceId,
                        ProcurementPaymentReadinessRules.SupplierAdvanceAction,
                        paymentId,
                        batchId: null,
                        cancellationToken);
            }

            var transactionStarted = false;
            try
            {
                // The available advance, invoice balances, allocation facts, and reclassification
                // postings have to commit together. Serializable isolation prevents double use of one advance.
                if (!_unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                    transactionStarted = true;
                }
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, paymentId), cancellationToken);
                foreach (var invoiceId in requestedAllocations.Select(item => item.VendorInvoiceId).Distinct().OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, invoiceId), cancellationToken);

                var payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId && !p.IsDeleted)
                    .Include(p => p.Allocations)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Supplier advance with Id '{paymentId}' was not found.");

                if (!payment.JournalEntryId.HasValue || !payment.IsSupplierAdvance)
                    throw new InvalidOperationException("Only a posted supplier advance can be applied through this workflow.");

                var settings = await GetFinanceSettingsAsync(cancellationToken);
                var supplier = await ResolvePaymentSupplierForPostingAsync(payment, cancellationToken);
                var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
                var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
                var paymentRate = NormalizeExchangeRate(payment.ExchangeRate);
                if (!string.Equals(paymentCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase) &&
                    (!payment.ExchangeRateId.HasValue || payment.ExchangeRate <= 0m))
                {
                    // The posted payment is the advance lot. Never replace its historical
                    // carrying rate with today's rate: doing so would erase realized FX.
                    throw new InvalidOperationException(
                        "The foreign-currency supplier advance is missing its approved origin-rate evidence.");
                }

                var apAccountId = supplier.DefaultApAccountId
                    ?? settings.ControlAccountApId
                    ?? throw new InvalidOperationException("AP control account is not configured for this tenant.");
                var advanceAccountId = settings.SupplierAdvanceAccountId
                    ?? throw new InvalidOperationException("Supplier advance account is not configured for this tenant.");
                var accountCache = new Dictionary<Guid, Account>();
                await ResolvePaymentPostingAccountAsync(apAccountId, "AP control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);
                var advanceAccount = await ResolvePaymentPostingAccountAsync(advanceAccountId, "supplier advance account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);
                if (advanceAccount.AccountType != AccountType.Asset)
                    throw new InvalidOperationException("Supplier advance account must be an asset account.");

                var currentlyAllocated = RoundMoney(GetEffectiveAllocations(payment.Allocations)
                    .Sum(a => a.PaymentCurrencyAmount > 0m ? a.PaymentCurrencyAmount : a.AllocatedAmount));
                var requestedTotal = RoundMoney(requestedAllocations.Sum(a =>
                    a.PaymentCurrencyAmount ?? a.AllocatedAmount));
                if (requestedTotal > RoundMoney(payment.TotalAmount - currentlyAllocated))
                    throw new InvalidOperationException("Supplier advance application exceeds the unallocated advance balance.");

                var now = DateTime.UtcNow;
                var applicationDate = await ResolveCurrentOpenPostingDateAsync(cancellationToken);
                var result = new VendorPaymentAllocationResultDto { PaymentId = payment.Id };
                foreach (var requested in requestedAllocations)
                {
                    var paymentDecision = await RequirePaymentReadinessAsync(
                        requested.VendorInvoiceId,
                        ProcurementPaymentReadinessRules.SupplierAdvanceAction,
                        payment.Id,
                        batchId: payment.PaymentBatchId,
                        cancellationToken);
                    var invoice = await _unitOfWork.Repository<VendorInvoice>()
                        .GetQueryable(i => i.TenantId == TenantId && i.Id == requested.VendorInvoiceId && !i.IsDeleted)
                        .FirstOrDefaultAsync(cancellationToken)
                        ?? throw new InvalidOperationException("Supplier advance application invoice was not found for this tenant.");
                    if (invoice.SupplierId != payment.SupplierId)
                        throw new InvalidOperationException("Supplier advance can only be applied to invoices for the same supplier.");
                    if (!invoice.JournalEntryId.HasValue)
                        throw new InvalidOperationException($"Supplier advance cannot be applied to unposted invoice '{invoice.InvoiceNumber}'.");
                    var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, functionalCurrency);
                    var invoiceRate = await ResolveApprovedSettlementRateAsync(
                        invoiceCurrency,
                        functionalCurrency,
                        applicationDate,
                        requested.InvoiceSettlementExchangeRateId,
                        fallbackRate: 1m,
                        requireApprovedSource: !string.Equals(
                            invoiceCurrency,
                            functionalCurrency,
                            StringComparison.OrdinalIgnoreCase),
                        cancellationToken);
                    var requestedAdvanceAmount = RoundMoney(
                        requested.PaymentCurrencyAmount ?? requested.AllocatedAmount);
                    if (!string.Equals(paymentCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase) &&
                        !requested.PaymentCurrencyAmount.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Cross-currency supplier advance application requires the advance-currency amount to consume.");
                    }

                    // Reuse the established settlement calculator so AP and AR retain identical
                    // amount-pair validation and rounding. PaymentFunctionalAmount is the advance
                    // lot's historical carrying value; InvoiceSettlementFunctionalAmount is the
                    // AP-control value at the application-date rate. Their difference is realized FX.
                    var settlement = CrossCurrencySettlementCalculator.Calculate(
                        paymentCurrency,
                        invoiceCurrency,
                        requestedAdvanceAmount,
                        RoundMoney(requested.AllocatedAmount),
                        invoiceDeductionAmount: 0m,
                        paymentExchangeRate: paymentRate,
                        invoiceSettlementExchangeRate: invoiceRate.Rate);

                    var invoiceOutstanding = await GetInvoiceUnreservedBalanceAsync(
                        invoice,
                        payment.Id,
                        cancellationToken);
                    if (requested.AllocatedAmount > invoiceOutstanding)
                        throw new VendorPaymentControlException(
                            "AP_PAYMENT_BALANCE_RESERVED",
                            $"Supplier advance application exceeds the unreserved outstanding balance of invoice '{invoice.InvoiceNumber}'.");

                    var allocation = new VendorPaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        VendorPaymentId = payment.Id,
                        VendorInvoiceId = invoice.Id,
                        AllocatedAmount = settlement.InvoiceCurrencyAmount,
                        PaymentCurrencyAmount = settlement.PaymentCurrencyAmount,
                        InvoiceCurrencyCode = settlement.InvoiceCurrency,
                        PaymentCurrencyCode = settlement.PaymentCurrency,
                        IsCrossCurrency = settlement.IsCrossCurrency,
                        InvoiceSettlementExchangeRateId = invoiceRate.Id,
                        InvoiceSettlementExchangeRate = settlement.InvoiceSettlementExchangeRate,
                        PaymentExchangeRateId = payment.ExchangeRateId,
                        PaymentExchangeRate = settlement.PaymentExchangeRate,
                        PaymentFunctionalAmount = settlement.PaymentFunctionalAmount,
                        SettlementFunctionalAmount = settlement.InvoiceSettlementFunctionalAmount,
                        AllocationDate = applicationDate,
                        Notes = requested.Notes,
                        PaymentReadinessControlEventId = paymentDecision.Event.Id,
                        PaymentReadinessSnapshotHash = paymentDecision.Readiness.SnapshotHash,
                        PaymentReadinessEvaluatedAtUtc = paymentDecision.Readiness.EvaluatedAtUtc,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    // Register the allocation explicitly as Added. Updating the payment source
                    // record later must not turn this new row into a modified-only graph entry.
                    // EF relationship fixup adds it to payment.Allocations; adding it manually as
                    // well would duplicate the same object in the in-memory lot calculation.
                    await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(allocation);
                    // This is a read-side operational snapshot. Recompute it from allocations so
                    // a stale value cannot cause an advance to be over-applied after a retry.
                    payment.AllocatedAmount = RoundMoney(GetEffectiveAllocations(payment.Allocations)
                        .Sum(a => a.PaymentCurrencyAmount > 0m ? a.PaymentCurrencyAmount : a.AllocatedAmount));
                    payment.UpdatedAt = now;
                    payment.UpdatedBy = UserName;
                    invoice.PaidAmount = RoundMoney(invoice.PaidAmount + allocation.AllocatedAmount);
                    invoice.Status = invoice.PaidAmount >= invoice.TotalAmount
                        ? VendorInvoiceStatus.Paid
                        : VendorInvoiceStatus.PartiallyPaid;
                    invoice.UpdatedAt = now;
                    invoice.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                    await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    var realizedFxDelta = RoundMoney(
                        allocation.SettlementFunctionalAmount - allocation.PaymentFunctionalAmount);
                    var postingLines = new List<FinancePostingLineDto>
                    {
                        BuildPostingLine(
                            apAccountId,
                            $"Apply supplier advance {payment.PaymentNumber}",
                            allocation.AllocatedAmount,
                            0m,
                            allocation.InvoiceCurrencyCode,
                            functionalCurrency,
                            allocation.InvoiceSettlementExchangeRate,
                            applicationDate,
                            payment.PaymentNumber,
                            1,
                            "AP-Control",
                            allocation.InvoiceSettlementExchangeRateId,
                            functionalDebitOverride: allocation.SettlementFunctionalAmount),
                        BuildPostingLine(
                            advanceAccountId,
                            $"Release supplier advance {payment.PaymentNumber}",
                            0m,
                            allocation.PaymentCurrencyAmount,
                            allocation.PaymentCurrencyCode,
                            functionalCurrency,
                            allocation.PaymentExchangeRate,
                            payment.PaymentDate,
                            payment.PaymentNumber,
                            2,
                            "AP-SupplierAdvance",
                            allocation.PaymentExchangeRateId,
                            functionalCreditOverride: allocation.PaymentFunctionalAmount)
                    };
                    if (realizedFxDelta != 0m)
                    {
                        // Supplier advances are assets. If the invoice value now exceeds the
                        // advance carrying value, the asset appreciated and Finance recognizes a
                        // gain; the opposite movement is a loss.
                        var isGain = realizedFxDelta > 0m;
                        var fxAccountId = isGain
                            ? settings.RealizedFxGainAccountId
                            : settings.RealizedFxLossAccountId;
                        var fxAccount = await ResolvePaymentPostingAccountAsync(
                            fxAccountId ?? throw new InvalidOperationException(
                                $"Realized FX {(isGain ? "gain" : "loss")} account is not configured for this tenant."),
                            isGain ? "realized FX gain account" : "realized FX loss account",
                            accountCache,
                            allowControlAccount: false,
                            requireDirectPosting: true,
                            cancellationToken);
                        postingLines.Add(BuildPostingLine(
                            fxAccount.Id,
                            $"Supplier advance realized FX {(isGain ? "gain" : "loss")} {payment.PaymentNumber}",
                            debitTransactionAmount: isGain ? 0m : Math.Abs(realizedFxDelta),
                            creditTransactionAmount: isGain ? Math.Abs(realizedFxDelta) : 0m,
                            functionalCurrency,
                            functionalCurrency,
                            1m,
                            applicationDate,
                            payment.PaymentNumber,
                            3,
                            isGain ? "FX-Realized-Gain" : "FX-Realized-Loss"));
                    }

                    var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = "AP",
                        SourceDocumentType = "VendorPaymentAdvanceApplication",
                        SourceDocumentId = allocation.Id,
                        SourceDocumentTenantId = payment.TenantId,
                        PostingAction = "Post",
                        SourceDocumentReference = $"{payment.PaymentNumber}:{invoice.InvoiceNumber}",
                        Description = $"Apply supplier advance {payment.PaymentNumber} to invoice {invoice.InvoiceNumber}",
                        PostingDate = applicationDate,
                        JournalType = "AP Supplier Advance Application",
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = functionalCurrency,
                        IdempotencyKey = $"AP:VendorPaymentAdvanceApplication:{payment.TenantId:N}:{allocation.Id:N}:Post",
                        ReturnExistingOnDuplicate = true,
                        Lines = postingLines
                    }, cancellationToken);

                    allocation.ApplicationJournalEntryId = postingResult.JournalEntryId;
                    allocation.ApplicationPostingEventId = postingResult.PostingEventId;
                    allocation.UpdatedAt = now;
                    allocation.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorPaymentAllocation>().UpdateAsync(allocation);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    result.Allocations.Add(new VendorPaymentAllocationDto
                    {
                        Id = allocation.Id,
                        VendorPaymentId = payment.Id,
                        VendorInvoiceId = invoice.Id,
                        InvoiceNumber = invoice.InvoiceNumber,
                        AllocatedAmount = allocation.AllocatedAmount,
                        PaymentCurrencyAmount = allocation.PaymentCurrencyAmount,
                        InvoiceCurrencyCode = allocation.InvoiceCurrencyCode,
                        PaymentCurrencyCode = allocation.PaymentCurrencyCode,
                        IsCrossCurrency = allocation.IsCrossCurrency,
                        InvoiceSettlementExchangeRateId = allocation.InvoiceSettlementExchangeRateId,
                        InvoiceSettlementExchangeRate = allocation.InvoiceSettlementExchangeRate,
                        PaymentExchangeRateId = allocation.PaymentExchangeRateId,
                        PaymentExchangeRate = allocation.PaymentExchangeRate,
                        PaymentFunctionalAmount = allocation.PaymentFunctionalAmount,
                        SettlementFunctionalAmount = allocation.SettlementFunctionalAmount,
                        AllocationDate = allocation.AllocationDate,
                        Notes = allocation.Notes,
                        PaymentReadinessControlEventId = paymentDecision.Event.Id,
                        PaymentReadinessSnapshotHash = paymentDecision.Readiness.SnapshotHash,
                        PaymentReadinessEvaluatedAtUtc = paymentDecision.Readiness.EvaluatedAtUtc
                    });
                }

                if (transactionStarted)
                {
                    await _unitOfWork.CommitAsync(cancellationToken);
                    transactionStarted = false;
                }
                result.TotalAllocated = payment.AllocatedAmount;
                result.RemainingUnallocated = RoundMoney(payment.TotalAmount - payment.AllocatedAmount);
                await RecordApPaymentAuditAsync(
                    FinanceAuditEvents.ApSupplierAdvanceApplied,
                    payment,
                    afterValues: new
                    {
                        result.TotalAllocated,
                        result.RemainingUnallocated,
                        AllocationCount = result.Allocations.Count,
                        CurrencyLot = paymentCurrency
                    },
                    comment: "Posted currency-lotted supplier advance application and any realized FX through the central finance posting engine.",
                    cancellationToken: cancellationToken);
                return result;
            }
            catch
            {
                if (transactionStarted)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task<decimal> GetInvoiceUnreservedBalanceAsync(
            VendorInvoice invoice,
            Guid? currentPaymentId,
            CancellationToken cancellationToken)
        {
            var liveAllocationReservation = await _unitOfWork.Repository<VendorPaymentAllocation>()
                .GetQueryable(allocation =>
                    allocation.TenantId == TenantId &&
                    allocation.VendorInvoiceId == invoice.Id &&
                    !allocation.IsDeleted &&
                    !allocation.VendorPayment.IsDeleted &&
                    !allocation.VendorPayment.JournalEntryId.HasValue &&
                    allocation.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    allocation.VendorPayment.Status != VendorPaymentStatus.Failed)
                .SumAsync(
                    allocation => (decimal?)(allocation.AllocatedAmount +
                                               allocation.DiscountAmount +
                                               allocation.WithholdingTaxAmount),
                    cancellationToken) ?? 0m;

            var supplierDebitApplications = _unitOfWork.Repository<SupplierDebitNoteApplication>()
                .GetQueryable(application =>
                    application.TenantId == TenantId &&
                    application.VendorInvoiceId == invoice.Id &&
                    !application.IsDeleted);
            var liveSupplierDebitReservation = await supplierDebitApplications
                .Where(application =>
                    !application.IsReversal &&
                    !application.VendorPayment.IsDeleted &&
                    !application.VendorPayment.JournalEntryId.HasValue &&
                    application.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    application.VendorPayment.Status != VendorPaymentStatus.Failed &&
                    !supplierDebitApplications.Any(reversal =>
                        reversal.IsReversal &&
                        reversal.OriginalApplicationId == application.Id))
                .SumAsync(application => (decimal?)application.ApplicationAmount, cancellationToken) ?? 0m;

            var reservingBatchStatuses = new[]
            {
                PaymentBatchStatus.Draft,
                PaymentBatchStatus.PendingApproval,
                PaymentBatchStatus.Approved,
                PaymentBatchStatus.Processing
            };
            var batchSelectionReservation = await _unitOfWork.Repository<PaymentBatchInvoice>()
                .GetQueryable(selection =>
                    selection.TenantId == TenantId &&
                    selection.VendorInvoiceId == invoice.Id &&
                    !selection.IsDeleted &&
                    (!currentPaymentId.HasValue || selection.VendorPaymentId != currentPaymentId.Value) &&
                    !selection.PaymentBatch.IsDeleted &&
                    reservingBatchStatuses.Contains(selection.PaymentBatch.Status) &&
                    !selection.VendorPayment.IsDeleted &&
                    !selection.VendorPayment.JournalEntryId.HasValue &&
                    selection.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    selection.VendorPayment.Status != VendorPaymentStatus.Failed &&
                    !selection.VendorPayment.Allocations.Any(allocation =>
                        allocation.TenantId == TenantId &&
                        allocation.VendorInvoiceId == invoice.Id &&
                        !allocation.IsDeleted &&
                        !allocation.IsReversal))
                .SumAsync(selection => (decimal?)selection.Amount, cancellationToken) ?? 0m;

            return RoundMoney(Math.Max(
                invoice.TotalAmount -
                invoice.PaidAmount -
                liveAllocationReservation -
                liveSupplierDebitReservation -
                batchSelectionReservation,
                0m));
        }

        private async Task<DateTime> ResolveCurrentOpenPostingDateAsync(CancellationToken cancellationToken)
        {
            // Applying a posted advance is a new accounting event, not a backdated mutation of
            // the original cash payment. Use the latest deliberately open period and clamp the
            // server date to it so controlled catch-up processing remains possible after month end.
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.IsOpen &&
                    !item.IsClosed &&
                    !item.IsLocked &&
                    !item.IsDeleted)
                .OrderByDescending(item => item.StartDate)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "No open fiscal period is available for the supplier advance application.");
            var today = DateTime.UtcNow.Date;
            if (today < period.StartDate.Date)
                return period.StartDate.Date;
            return today > period.EndDate.Date ? period.EndDate.Date : today;
        }

        private async Task<IReadOnlyDictionary<Guid, decimal>>
            GetInvoiceUnreservedBalancesAsync(
                IReadOnlyCollection<VendorInvoice> invoices,
                Guid? currentPaymentId,
                CancellationToken cancellationToken)
        {
            if (invoices.Count == 0)
                return new Dictionary<Guid, decimal>();

            var invoiceIds = invoices.Select(item => item.Id).ToArray();
            var liveAllocationReservations = await _unitOfWork
                .Repository<VendorPaymentAllocation>()
                .GetQueryable(allocation =>
                    allocation.TenantId == TenantId &&
                    invoiceIds.Contains(allocation.VendorInvoiceId) &&
                    !allocation.IsDeleted &&
                    !allocation.VendorPayment.IsDeleted &&
                    !allocation.VendorPayment.JournalEntryId.HasValue &&
                    allocation.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    allocation.VendorPayment.Status != VendorPaymentStatus.Failed)
                .GroupBy(allocation => allocation.VendorInvoiceId)
                .Select(group => new
                {
                    InvoiceId = group.Key,
                    Amount = group.Sum(allocation =>
                        allocation.AllocatedAmount +
                        allocation.DiscountAmount +
                        allocation.WithholdingTaxAmount)
                })
                .ToDictionaryAsync(
                    item => item.InvoiceId,
                    item => item.Amount,
                    cancellationToken);

            var supplierDebitApplications = _unitOfWork.Repository<SupplierDebitNoteApplication>()
                .GetQueryable(application =>
                    application.TenantId == TenantId &&
                    invoiceIds.Contains(application.VendorInvoiceId) &&
                    !application.IsDeleted);
            var liveSupplierDebitReservations = await supplierDebitApplications
                .Where(application =>
                    !application.IsReversal &&
                    !application.VendorPayment.IsDeleted &&
                    !application.VendorPayment.JournalEntryId.HasValue &&
                    application.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    application.VendorPayment.Status != VendorPaymentStatus.Failed &&
                    !supplierDebitApplications.Any(reversal =>
                        reversal.IsReversal &&
                        reversal.OriginalApplicationId == application.Id))
                .GroupBy(application => application.VendorInvoiceId)
                .Select(group => new
                {
                    InvoiceId = group.Key,
                    Amount = group.Sum(application => application.ApplicationAmount)
                })
                .ToDictionaryAsync(
                    item => item.InvoiceId,
                    item => item.Amount,
                    cancellationToken);

            var reservingBatchStatuses = new[]
            {
                PaymentBatchStatus.Draft,
                PaymentBatchStatus.PendingApproval,
                PaymentBatchStatus.Approved,
                PaymentBatchStatus.Processing
            };
            var batchSelectionReservations = await _unitOfWork
                .Repository<PaymentBatchInvoice>()
                .GetQueryable(selection =>
                    selection.TenantId == TenantId &&
                    invoiceIds.Contains(selection.VendorInvoiceId) &&
                    !selection.IsDeleted &&
                    (!currentPaymentId.HasValue ||
                     selection.VendorPaymentId != currentPaymentId.Value) &&
                    !selection.PaymentBatch.IsDeleted &&
                    reservingBatchStatuses.Contains(selection.PaymentBatch.Status) &&
                    !selection.VendorPayment.IsDeleted &&
                    !selection.VendorPayment.JournalEntryId.HasValue &&
                    selection.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    selection.VendorPayment.Status != VendorPaymentStatus.Failed &&
                    !selection.VendorPayment.Allocations.Any(allocation =>
                        allocation.TenantId == TenantId &&
                        allocation.VendorInvoiceId == selection.VendorInvoiceId &&
                        !allocation.IsDeleted &&
                        !allocation.IsReversal))
                .GroupBy(selection => selection.VendorInvoiceId)
                .Select(group => new
                {
                    InvoiceId = group.Key,
                    Amount = group.Sum(selection => selection.Amount)
                })
                .ToDictionaryAsync(
                    item => item.InvoiceId,
                    item => item.Amount,
                    cancellationToken);

            return invoices.ToDictionary(
                invoice => invoice.Id,
                invoice => RoundMoney(Math.Max(
                    invoice.TotalAmount -
                    invoice.PaidAmount -
                    liveAllocationReservations.GetValueOrDefault(invoice.Id) -
                    liveSupplierDebitReservations.GetValueOrDefault(invoice.Id) -
                    batchSelectionReservations.GetValueOrDefault(invoice.Id),
                    0m)));
        }

        public Task ReverseAllocationAsync(
            Guid allocationId,
            string reason,
            CancellationToken cancellationToken = default) =>
            ReverseAllocationAsync(
                allocationId,
                reason,
                cancellationToken,
                executionStrategyScope: false);

        private async Task ReverseAllocationAsync(
            Guid allocationId,
            string reason,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                await _unitOfWork.ExecuteInStrategyAsync(
                    async () =>
                    {
                        await ReverseAllocationAsync(
                            allocationId,
                            reason,
                            cancellationToken,
                            executionStrategyScope: true);
                        return true;
                    },
                    cancellationToken);
                return;
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                var allocationIdentity = await _unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == allocationId && !item.IsDeleted)
                    .Select(item => new { item.VendorPaymentId, item.VendorInvoiceId })
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Allocation with Id '{allocationId}' not found.");
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, allocationIdentity.VendorPaymentId), cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Invoice(TenantId, allocationIdentity.VendorInvoiceId), cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"tdc0505-allocation-reversal:{TenantId:N}:{allocationId:N}", cancellationToken);

                var allocation = await _unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryable(a => a.TenantId == TenantId && a.Id == allocationId)
                    .Include(a => a.VendorPayment)
                        .ThenInclude(payment => payment.Allocations)
                            .ThenInclude(item => item.VendorInvoice)
                    .Include(a => a.VendorInvoice)
                    .SingleOrDefaultAsync(cancellationToken);

                if (allocation == null)
                    throw new KeyNotFoundException($"Allocation with Id '{allocationId}' not found.");
                if (allocation.IsReversal)
                    throw new InvalidOperationException("Cannot reverse a reversal allocation.");
                if (allocation.VendorPayment.PaymentBatchId.HasValue)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_BATCH_ALLOCATION_FROZEN",
                        "Batch-owned payment allocations are immutable after the batch invoice set is submitted.");

                var alreadyReversed = await _unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryableIncludingDeleted(item =>
                        item.TenantId == TenantId &&
                        item.IsReversal &&
                        item.OriginalAllocationId == allocationId)
                    .AnyAsync(cancellationToken);
                if (alreadyReversed)
                    throw AllocationAlreadyReversed(allocationId);

                if (allocation.VendorPayment.JournalEntryId.HasValue)
                {
                    if (allocation.VendorPayment.IsSupplierAdvance &&
                        allocation.ApplicationPostingEventId.HasValue)
                    {
                        // A posted advance application is its own accounting document. Reverse
                        // that document and restore the currency lot; never mutate the original
                        // allocation or reverse the original bank payment as a shortcut.
                        await ReversePostedSupplierAdvanceApplicationAsync(
                            allocation,
                            reason,
                            cancellationToken);
                        if (ownsTransaction)
                            await _unitOfWork.CommitAsync(cancellationToken);
                        return;
                    }

                    throw new InvalidOperationException(
                        "Posted vendor payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");
                }

                var now = DateTime.UtcNow;
                var reversal = new VendorPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorPaymentId = allocation.VendorPaymentId,
                    VendorInvoiceId = allocation.VendorInvoiceId,
                    AllocatedAmount = -allocation.AllocatedAmount,
                    PaymentCurrencyAmount = -allocation.PaymentCurrencyAmount,
                    InvoiceCurrencyCode = allocation.InvoiceCurrencyCode,
                    PaymentCurrencyCode = allocation.PaymentCurrencyCode,
                    IsCrossCurrency = allocation.IsCrossCurrency,
                    InvoiceSettlementExchangeRateId = allocation.InvoiceSettlementExchangeRateId,
                    InvoiceSettlementExchangeRate = allocation.InvoiceSettlementExchangeRate,
                    PaymentExchangeRateId = allocation.PaymentExchangeRateId,
                    PaymentExchangeRate = allocation.PaymentExchangeRate,
                    PaymentFunctionalAmount = -allocation.PaymentFunctionalAmount,
                    SettlementFunctionalAmount = -allocation.SettlementFunctionalAmount,
                    DiscountAmount = -allocation.DiscountAmount,
                    DiscountFunctionalAmount = -allocation.DiscountFunctionalAmount,
                    WithholdingTaxAmount = -allocation.WithholdingTaxAmount,
                    WithholdingTaxFunctionalAmount = -allocation.WithholdingTaxFunctionalAmount,
                    AllocationDate = now,
                    Notes = $"Reversal of allocation {allocationId}: {reason}",
                    IsReversal = true,
                    OriginalAllocationId = allocationId,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(reversal);

                allocation.VendorPayment.AllocatedAmount = Math.Max(
                    0m,
                    allocation.VendorPayment.AllocatedAmount -
                    (allocation.PaymentCurrencyAmount > 0m ? allocation.PaymentCurrencyAmount : allocation.AllocatedAmount));
                var remainingAllocations = GetEffectiveAllocations(allocation.VendorPayment.Allocations)
                    .Where(item => item.Id != allocationId)
                    .ToList();
                // Established same-currency drafts may predate the functional deduction columns.
                // Normalize their deterministic evidence before rebuilding header roll-ups; doing
                // this after summing would incorrectly erase an unaffected remaining discount.
                var functionalCurrency = NormalizeCurrency(
                    await _tenantSettingsService.GetBaseCurrencyAsync(),
                    "GHS");
                NormalizeAndValidateAllocationCurrencyEvidence(
                    allocation.VendorPayment,
                    remainingAllocations,
                    functionalCurrency);
                allocation.VendorPayment.DiscountTaken = RoundMoney(
                    remainingAllocations.Sum(item => item.DiscountFunctionalAmount));
                await SynchronizeApWithholdingComplianceAsync(
                    allocation.VendorPayment,
                    remainingAllocations,
                    cancellationToken);
                allocation.VendorPayment.UpdatedAt = now;
                allocation.VendorPayment.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorPayment>().UpdateAsync(allocation.VendorPayment);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Reversed allocation {AllocationId}. Reason: {Reason}", allocationId, reason);
            }
            catch (DbUpdateException exception) when (IsDuplicateAllocationReversal(exception))
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw AllocationAlreadyReversed(allocationId);
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task ReversePostedSupplierAdvanceApplicationAsync(
            VendorPaymentAllocation allocation,
            string reason,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null || !allocation.ApplicationPostingEventId.HasValue)
                throw new InvalidOperationException(
                    "The supplier advance application has no authoritative posting event to reverse.");

            var trimmedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason))
                throw new InvalidOperationException("A reason is required to reverse a supplier advance application.");

            var reversalResult = await ReversePostedEventAsync(
                allocation.ApplicationPostingEventId.Value,
                trimmedReason,
                $"AP:VendorPaymentAdvanceApplication:{TenantId:N}:{allocation.Id:N}:Reverse",
                "Supplier Advance Application Reversal",
                $"Reverse supplier advance application {allocation.VendorPayment.PaymentNumber}",
                cancellationToken,
                // ReversalType is a compact, indexed classification capped at 20 characters;
                // the journal/description retain the full application-specific wording.
                reversalType: "Supplier advance");

            var now = DateTime.UtcNow;
            var invoice = allocation.VendorInvoice;
            invoice.PaidAmount = Math.Max(0m, RoundMoney(invoice.PaidAmount - allocation.AllocatedAmount));
            invoice.Status = invoice.PaidAmount <= 0.01m
                ? VendorInvoiceStatus.Approved
                : VendorInvoiceStatus.PartiallyPaid;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;
            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);

            // The negative linked row preserves the reviewed currency pair, both rate snapshots,
            // both functional values, and the correction journal. Reports can rebuild the lot
            // balance without treating the original application as though it never occurred.
            await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(new VendorPaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VendorPaymentId = allocation.VendorPaymentId,
                VendorInvoiceId = allocation.VendorInvoiceId,
                AllocatedAmount = -allocation.AllocatedAmount,
                PaymentCurrencyAmount = -allocation.PaymentCurrencyAmount,
                InvoiceCurrencyCode = allocation.InvoiceCurrencyCode,
                PaymentCurrencyCode = allocation.PaymentCurrencyCode,
                IsCrossCurrency = allocation.IsCrossCurrency,
                InvoiceSettlementExchangeRateId = allocation.InvoiceSettlementExchangeRateId,
                InvoiceSettlementExchangeRate = allocation.InvoiceSettlementExchangeRate,
                PaymentExchangeRateId = allocation.PaymentExchangeRateId,
                PaymentExchangeRate = allocation.PaymentExchangeRate,
                PaymentFunctionalAmount = -allocation.PaymentFunctionalAmount,
                SettlementFunctionalAmount = -allocation.SettlementFunctionalAmount,
                AllocationDate = now,
                Notes = $"Controlled reversal of supplier advance application {allocation.Id}: {trimmedReason}",
                IsReversal = true,
                OriginalAllocationId = allocation.Id,
                ApplicationPostingEventId = reversalResult.PostingEventId,
                ApplicationJournalEntryId = reversalResult.JournalEntryId,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
            });

            var paymentCurrencyAmount = allocation.PaymentCurrencyAmount > 0m
                ? allocation.PaymentCurrencyAmount
                : allocation.AllocatedAmount;
            allocation.VendorPayment.AllocatedAmount = Math.Max(
                0m,
                RoundMoney(allocation.VendorPayment.AllocatedAmount - paymentCurrencyAmount));
            allocation.VendorPayment.UpdatedAt = now;
            allocation.VendorPayment.UpdatedBy = UserName;
            await _unitOfWork.Repository<VendorPayment>().UpdateAsync(allocation.VendorPayment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordApPaymentAuditAsync(
                FinanceAuditEvents.ApSupplierAdvanceApplicationReversed,
                allocation.VendorPayment,
                postingEventId: reversalResult.PostingEventId,
                journalEntryId: reversalResult.JournalEntryId,
                afterValues: new
                {
                    ReversedAllocationId = allocation.Id,
                    RestoredAdvanceCurrencyAmount = paymentCurrencyAmount,
                    allocation.PaymentCurrencyCode,
                    RestoredInvoiceCurrencyAmount = allocation.AllocatedAmount,
                    allocation.InvoiceCurrencyCode
                },
                reason: trimmedReason,
                comment: "Reversed a posted supplier advance application and restored its immutable currency lot.",
                cancellationToken: cancellationToken);
        }

        public async Task<List<VendorPaymentAllocationDto>> GetPaymentAllocationsAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            var paymentBankAccountId = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == paymentId && !item.IsDeleted)
                .Select(item => item.BankAccountId)
                .SingleOrDefaultAsync(cancellationToken);
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(paymentBankAccountId, cancellationToken),
                FinanceAccessLevel.Read,
                cancellationToken);

            var allocations = await _unitOfWork.Repository<VendorPaymentAllocation>()
                .GetQueryable(a => a.TenantId == TenantId && a.VendorPaymentId == paymentId)
                .Include(a => a.VendorInvoice)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync(cancellationToken);

            return allocations.Select(a => new VendorPaymentAllocationDto
            {
                Id = a.Id,
                VendorPaymentId = a.VendorPaymentId,
                VendorInvoiceId = a.VendorInvoiceId,
                InvoiceNumber = a.VendorInvoice.InvoiceNumber,
                AllocatedAmount = a.AllocatedAmount,
                PaymentCurrencyAmount = a.PaymentCurrencyAmount,
                InvoiceCurrencyCode = a.InvoiceCurrencyCode,
                PaymentCurrencyCode = a.PaymentCurrencyCode,
                IsCrossCurrency = a.IsCrossCurrency,
                InvoiceSettlementExchangeRateId = a.InvoiceSettlementExchangeRateId,
                InvoiceSettlementExchangeRate = a.InvoiceSettlementExchangeRate,
                PaymentExchangeRateId = a.PaymentExchangeRateId,
                PaymentExchangeRate = a.PaymentExchangeRate,
                PaymentFunctionalAmount = a.PaymentFunctionalAmount,
                SettlementFunctionalAmount = a.SettlementFunctionalAmount,
                DiscountAmount = a.DiscountAmount,
                DiscountFunctionalAmount = a.DiscountFunctionalAmount,
                WithholdingTaxAmount = a.WithholdingTaxAmount,
                WithholdingTaxFunctionalAmount = a.WithholdingTaxFunctionalAmount,
                AllocationDate = a.AllocationDate,
                Notes = a.Notes,
                IsReversal = a.IsReversal,
                PaymentReadinessControlEventId = a.PaymentReadinessControlEventId,
                PaymentReadinessSnapshotHash = a.PaymentReadinessSnapshotHash,
                PaymentReadinessEvaluatedAtUtc = a.PaymentReadinessEvaluatedAtUtc
            }).ToList();
        }

        public Task<SupplierDebitNoteApplicationResultDto> ApplySupplierDebitNotesAsync(
            Guid paymentId,
            List<SupplierDebitNoteApplicationCreateDto> applications,
            CancellationToken cancellationToken = default) =>
            ApplySupplierDebitNotesAsync(
                paymentId,
                applications,
                cancellationToken,
                executionStrategyScope: false);

        private async Task<SupplierDebitNoteApplicationResultDto> ApplySupplierDebitNotesAsync(
            Guid paymentId,
            IReadOnlyCollection<SupplierDebitNoteApplicationCreateDto> applications,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            ArgumentNullException.ThrowIfNull(applications);
            if (applications.Count == 0)
                throw new ArgumentException("At least one supplier debit-note application is required.", nameof(applications));
            if (applications.Any(item => item.SupplierDebitNoteId == Guid.Empty ||
                                         item.VendorInvoiceId == Guid.Empty ||
                                         item.ApplicationAmount <= 0m))
                throw new ArgumentException("Every supplier debit-note application requires a note, invoice, and positive amount.", nameof(applications));
            if (applications.GroupBy(item => new { item.SupplierDebitNoteId, item.VendorInvoiceId })
                .Any(group => group.Count() > 1))
                throw new InvalidOperationException("Each supplier debit note and invoice pair may appear only once in an application request.");

            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => ApplySupplierDebitNotesAsync(
                        paymentId,
                        applications,
                        cancellationToken,
                        executionStrategyScope: true),
                    cancellationToken);
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, paymentId), cancellationToken);
                foreach (var invoiceId in applications.Select(item => item.VendorInvoiceId).Distinct().OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, invoiceId), cancellationToken);
                foreach (var noteId in applications.Select(item => item.SupplierDebitNoteId).Distinct().OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.SupplierDebitNote(TenantId, noteId), cancellationToken);

                var payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == paymentId && !item.IsDeleted)
                    .Include(item => item.Supplier)
                    .Include(item => item.PaymentBatch)
                    .Include(item => item.Allocations.Where(allocation => !allocation.IsDeleted))
                    .Include(item => item.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Vendor payment with Id '{paymentId}' not found.");

                await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                    await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                    FinanceAccessLevel.Operate,
                    cancellationToken);
                if (payment.JournalEntryId.HasValue)
                    throw new InvalidOperationException("Posted vendor-payment settlements cannot accept new supplier debit-note applications.");
                EnsureAllocationMutationAllowed(payment, batchProcessorScope: false);

                var created = new List<SupplierDebitNoteApplication>();
                var invoiceConsumedInRequest = new Dictionary<Guid, decimal>();
                var noteConsumedInRequest = new Dictionary<Guid, decimal>();
                var now = DateTime.UtcNow;

                foreach (var request in applications)
                {
                    var note = await _unitOfWork.Repository<SupplierDebitNote>()
                        .GetQueryable(item =>
                            item.TenantId == TenantId &&
                            item.Id == request.SupplierDebitNoteId &&
                            !item.IsDeleted)
                        .Include(item => item.Vendor)
                        .Include(item => item.Supplier)
                        .Include(item => item.Applications.Where(application => !application.IsDeleted))
                        .SingleOrDefaultAsync(cancellationToken)
                        ?? throw new KeyNotFoundException($"Supplier debit note with Id '{request.SupplierDebitNoteId}' was not found.");
                    if (note.Status != SupplierDebitNoteStatus.Posted ||
                        !note.PostingEventId.HasValue ||
                        !note.JournalEntryId.HasValue)
                        throw new InvalidOperationException($"Supplier debit note '{note.DebitNoteNumber}' must be posted before application.");
                    if (!note.SupplierId.HasValue || note.SupplierId.Value != payment.SupplierId)
                        throw new InvalidOperationException($"Supplier debit note '{note.DebitNoteNumber}' does not belong to this payment's supplier.");
                    if (payment.PaymentDate.Date < note.DebitNoteDate.Date)
                        throw new InvalidOperationException(
                            $"Payment date cannot precede supplier debit note '{note.DebitNoteNumber}'.");
                    if (note.ExchangeRate <= 0m)
                        throw new InvalidOperationException(
                            $"Supplier debit note '{note.DebitNoteNumber}' has no valid frozen carrying rate and cannot be applied.");

                    var invoice = await _unitOfWork.Repository<VendorInvoice>()
                        .GetQueryable(item =>
                            item.TenantId == TenantId &&
                            item.Id == request.VendorInvoiceId &&
                            !item.IsDeleted)
                        .SingleOrDefaultAsync(cancellationToken)
                        ?? throw new KeyNotFoundException($"Vendor invoice with Id '{request.VendorInvoiceId}' was not found.");
                    if (invoice.SupplierId != payment.SupplierId)
                        throw new InvalidOperationException($"Invoice '{invoice.InvoiceNumber}' does not belong to this payment's supplier.");
                    if (!invoice.JournalEntryId.HasValue)
                        throw new InvalidOperationException($"Supplier debit note cannot be applied to unposted invoice '{invoice.InvoiceNumber}'.");
                    if (invoice.Status == VendorInvoiceStatus.Voided)
                        throw new InvalidOperationException($"Supplier debit note cannot be applied to voided invoice '{invoice.InvoiceNumber}'.");
                    await EnsureSupplierCreditSourceJournalsActiveAsync(note, invoice, cancellationToken);

                    var noteCurrency = NormalizeCurrency(note.CurrencyCode, "GHS");
                    var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, "GHS");
                    if (!string.Equals(noteCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"Supplier debit-note application currently requires the note and invoice to share a currency; '{note.DebitNoteNumber}' is {noteCurrency} and '{invoice.InvoiceNumber}' is {invoiceCurrency}.");
                    if (RoundRate(note.ExchangeRate) != RoundRate(invoice.ExchangeRate))
                        throw new InvalidOperationException(
                            "Supplier debit-note application requires the note and invoice to share the same frozen carrying rate until a controlled AP/realized-FX reclassification is implemented.");
                    var noteControlAccountId = await ResolvePostedApControlAccountAsync(
                        note.JournalEntryId!.Value, debitSide: true, cancellationToken);
                    var invoiceControlAccountId = await ResolvePostedApControlAccountAsync(
                        invoice.JournalEntryId.Value, debitSide: false, cancellationToken);
                    if (noteControlAccountId != invoiceControlAccountId)
                        throw new InvalidOperationException(
                            "Supplier debit-note application requires the note and invoice to use the same immutable AP-control account.");

                    var requestedAmount = RoundMoney(request.ApplicationAmount);
                    // Relationship fix-up may attach earlier unsaved rows from this request to the
                    // tracked note. Exclude them here because requestNoteUsed accounts for those
                    // rows exactly once.
                    var noteUsed = RoundMoney(GetEffectiveSupplierDebitNoteApplications(
                            note.Applications.Where(item => !created.Contains(item)))
                        .Sum(item => item.ApplicationAmount));
                    var requestNoteUsed = noteConsumedInRequest.GetValueOrDefault(note.Id);
                    var noteAvailable = RoundMoney(note.TotalAmount - note.DirectInvoiceAppliedAmount - noteUsed - requestNoteUsed);
                    if (requestedAmount > noteAvailable + 0.01m)
                        throw new InvalidOperationException(
                            $"Application exceeds the {noteAvailable:N2} remaining balance on supplier debit note '{note.DebitNoteNumber}'.");

                    var invoiceAvailable = await GetInvoiceUnreservedBalanceAsync(invoice, payment.Id, cancellationToken);
                    invoiceAvailable = RoundMoney(invoiceAvailable - invoiceConsumedInRequest.GetValueOrDefault(invoice.Id));
                    if (requestedAmount > invoiceAvailable + 0.01m)
                        throw new InvalidOperationException(
                            $"Application exceeds the {invoiceAvailable:N2} unreserved balance on invoice '{invoice.InvoiceNumber}'.");

                    var application = new SupplierDebitNoteApplication
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        SupplierDebitNoteId = note.Id,
                        VendorPaymentId = payment.Id,
                        VendorInvoiceId = invoice.Id,
                        ApplicationAmount = requestedAmount,
                        // A posted supplier debit note must carry immutable measurement evidence.
                        // Never convert missing/invalid evidence to 1.0 at settlement time.
                        FunctionalAmount = RoundMoney(requestedAmount * note.ExchangeRate),
                        CurrencyCode = noteCurrency,
                        ExchangeRate = note.ExchangeRate,
                        ApplicationDate = payment.PaymentDate,
                        Notes = request.Notes?.Trim(),
                        CreatedAt = now,
                        CreatedBy = UserName,
                        CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId,
                        SupplierDebitNote = note,
                        VendorPayment = payment,
                        VendorInvoice = invoice
                    };
                    await _unitOfWork.Repository<SupplierDebitNoteApplication>().AddAsync(application);
                    created.Add(application);
                    noteConsumedInRequest[note.Id] = RoundMoney(requestNoteUsed + requestedAmount);
                    invoiceConsumedInRequest[invoice.Id] = RoundMoney(
                        invoiceConsumedInRequest.GetValueOrDefault(invoice.Id) + requestedAmount);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordApPaymentAuditAsync(
                    SupplierDebitApplicationReservedEvent,
                    payment,
                    afterValues: new
                    {
                        ApplicationIds = created.Select(item => item.Id),
                        DebitNoteIds = created.Select(item => item.SupplierDebitNoteId).Distinct(),
                        InvoiceIds = created.Select(item => item.VendorInvoiceId).Distinct(),
                        Total = created.Sum(item => item.FunctionalAmount)
                    },
                    comment: "Reserved posted supplier credits against this draft AP payment; no additional GL entry was created.",
                    cancellationToken: cancellationToken);
                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);

                var all = await GetSupplierDebitNoteApplicationsAsync(payment.Id, cancellationToken);
                return new SupplierDebitNoteApplicationResultDto
                {
                    PaymentId = payment.Id,
                    TotalSupplierCreditsApplied = RoundMoney(
                        GetEffectiveSupplierDebitNoteApplicationDtos(all).Sum(item => item.ApplicationAmount)),
                    Applications = all
                };
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<List<SupplierDebitNoteApplicationDto>> GetSupplierDebitNoteApplicationsAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == paymentId && !item.IsDeleted)
                .Select(item => new { item.Id, item.BankAccountId })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Vendor payment with Id '{paymentId}' not found.");
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Read,
                cancellationToken);

            var rows = await _unitOfWork.Repository<SupplierDebitNoteApplication>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.VendorPaymentId == paymentId &&
                    !item.IsDeleted)
                .Include(item => item.SupplierDebitNote)
                .Include(item => item.VendorInvoice)
                .Include(item => item.VendorPayment)
                .OrderBy(item => item.ApplicationDate)
                .ThenBy(item => item.Id)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return rows.Select(MapSupplierDebitNoteApplication).ToList();
        }

        public Task ReverseSupplierDebitNoteApplicationAsync(
            Guid applicationId,
            string reason,
            CancellationToken cancellationToken = default) =>
            ReverseSupplierDebitNoteApplicationAsync(
                applicationId,
                reason,
                cancellationToken,
                executionStrategyScope: false);

        private async Task ReverseSupplierDebitNoteApplicationAsync(
            Guid applicationId,
            string reason,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            var trimmedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason))
                throw new ArgumentException("A reason is required to reverse a supplier debit-note application.", nameof(reason));
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                await _unitOfWork.ExecuteInStrategyAsync(async () =>
                {
                    await ReverseSupplierDebitNoteApplicationAsync(
                        applicationId,
                        trimmedReason,
                        cancellationToken,
                        executionStrategyScope: true);
                    return true;
                }, cancellationToken);
                return;
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var applicationIdentity = await _unitOfWork.Repository<SupplierDebitNoteApplication>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == applicationId && !item.IsDeleted)
                    .Select(item => new
                    {
                        item.VendorPaymentId,
                        item.VendorInvoiceId,
                        item.SupplierDebitNoteId
                    })
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Supplier debit-note application with Id '{applicationId}' was not found.");
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, applicationIdentity.VendorPaymentId), cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Invoice(TenantId, applicationIdentity.VendorInvoiceId), cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.SupplierDebitNote(TenantId, applicationIdentity.SupplierDebitNoteId), cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"ap-supplier-debit-application:{TenantId:N}:{applicationId:N}", cancellationToken);
                var application = await _unitOfWork.Repository<SupplierDebitNoteApplication>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == applicationId && !item.IsDeleted)
                    .Include(item => item.SupplierDebitNote)
                    .Include(item => item.VendorInvoice)
                    .Include(item => item.VendorPayment)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Supplier debit-note application with Id '{applicationId}' was not found.");

                await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                    await ResolveBankAccountIdForScopeAsync(application.VendorPayment.BankAccountId, cancellationToken),
                    FinanceAccessLevel.Operate,
                    cancellationToken);
                if (application.IsReversal)
                    throw new InvalidOperationException("A supplier debit-note application reversal cannot itself be reversed.");
                if (application.PaymentPostingEventId.HasValue || application.VendorPayment.JournalEntryId.HasValue)
                    throw new InvalidOperationException("A posted supplier debit-note application must be reversed with its containing payment.");
                EnsureAllocationMutationAllowed(application.VendorPayment, batchProcessorScope: false);
                if (await _unitOfWork.Repository<SupplierDebitNoteApplication>()
                    .GetQueryableIncludingDeleted(item =>
                        item.TenantId == TenantId &&
                        item.IsReversal &&
                        item.OriginalApplicationId == application.Id)
                    .AnyAsync(cancellationToken))
                    throw new InvalidOperationException("This supplier debit-note application already has an immutable reversal.");

                var now = DateTime.UtcNow;
                await _unitOfWork.Repository<SupplierDebitNoteApplication>().AddAsync(new SupplierDebitNoteApplication
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    SupplierDebitNoteId = application.SupplierDebitNoteId,
                    VendorPaymentId = application.VendorPaymentId,
                    VendorInvoiceId = application.VendorInvoiceId,
                    ApplicationAmount = -application.ApplicationAmount,
                    FunctionalAmount = -application.FunctionalAmount,
                    CurrencyCode = application.CurrencyCode,
                    ExchangeRate = application.ExchangeRate,
                    ApplicationDate = now,
                    Notes = $"Reversal of supplier debit-note application {application.Id}: {trimmedReason}",
                    IsReversal = true,
                    OriginalApplicationId = application.Id,
                    CreatedAt = now,
                    CreatedBy = UserName,
                    CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
                });
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordApPaymentAuditAsync(
                    SupplierDebitApplicationReversedEvent,
                    application.VendorPayment,
                    beforeValues: new
                    {
                        application.Id,
                        application.SupplierDebitNoteId,
                        application.VendorInvoiceId,
                        application.ApplicationAmount
                    },
                    afterValues: new { ReleasedAmount = application.ApplicationAmount },
                    reason: trimmedReason,
                    comment: "Released an unposted supplier-credit reservation through an immutable compensating row.",
                    cancellationToken: cancellationToken);
                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<List<OutstandingVendorInvoiceDto>> GetOutstandingInvoicesAsync(
            Guid supplierId,
            int pageNumber = 1,
            int pageSize = DefaultOutstandingInvoicePageSize,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var resolvedSupplierId = await ResolveSupplierIdForQueryAsync(supplierId, cancellationToken);
            if (!resolvedSupplierId.HasValue)
            {
                return new List<OutstandingVendorInvoiceDto>();
            }

            var boundedPageNumber = Math.Max(pageNumber, 1);
            var boundedPageSize = Math.Clamp(
                pageSize,
                1,
                MaximumOutstandingInvoicePageSize);
            var skip = (int)Math.Min(
                (long)(boundedPageNumber - 1) * boundedPageSize,
                int.MaxValue);
            var liveAllocations = _unitOfWork.Repository<VendorPaymentAllocation>()
                .GetQueryable(allocation =>
                    allocation.TenantId == TenantId &&
                    !allocation.IsDeleted &&
                    !allocation.VendorPayment.IsDeleted &&
                    !allocation.VendorPayment.JournalEntryId.HasValue &&
                    allocation.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    allocation.VendorPayment.Status != VendorPaymentStatus.Failed);
            var supplierDebitApplications = _unitOfWork.Repository<SupplierDebitNoteApplication>()
                .GetQueryable(application =>
                    application.TenantId == TenantId &&
                    !application.IsDeleted);
            var liveSupplierDebitApplications = supplierDebitApplications.Where(application =>
                !application.IsReversal &&
                !application.VendorPayment.IsDeleted &&
                !application.VendorPayment.JournalEntryId.HasValue &&
                application.VendorPayment.Status != VendorPaymentStatus.Voided &&
                application.VendorPayment.Status != VendorPaymentStatus.Failed &&
                !supplierDebitApplications.Any(reversal =>
                    reversal.IsReversal &&
                    reversal.OriginalApplicationId == application.Id));
            var reservingBatchStatuses = new[]
            {
                PaymentBatchStatus.Draft,
                PaymentBatchStatus.PendingApproval,
                PaymentBatchStatus.Approved,
                PaymentBatchStatus.Processing
            };
            var batchSelections = _unitOfWork.Repository<PaymentBatchInvoice>()
                .GetQueryable(selection =>
                    selection.TenantId == TenantId &&
                    !selection.IsDeleted &&
                    !selection.PaymentBatch.IsDeleted &&
                    reservingBatchStatuses.Contains(selection.PaymentBatch.Status) &&
                    !selection.VendorPayment.IsDeleted &&
                    !selection.VendorPayment.JournalEntryId.HasValue &&
                    selection.VendorPayment.Status != VendorPaymentStatus.Voided &&
                    selection.VendorPayment.Status != VendorPaymentStatus.Failed &&
                    !selection.VendorPayment.Allocations.Any(allocation =>
                        allocation.TenantId == TenantId &&
                        allocation.VendorInvoiceId == selection.VendorInvoiceId &&
                        !allocation.IsDeleted &&
                        !allocation.IsReversal));
            var invoicePage = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.SupplierId == resolvedSupplierId.Value &&
                    (i.Status == VendorInvoiceStatus.Approved ||
                     i.Status == VendorInvoiceStatus.PartiallyPaid ||
                     i.Status == VendorInvoiceStatus.Overdue) &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .Select(invoice => new
                {
                    Invoice = invoice,
                    UnreservedBalance = invoice.TotalAmount - invoice.PaidAmount -
                        (liveAllocations
                            .Where(allocation => allocation.VendorInvoiceId == invoice.Id)
                            .Sum(allocation => (decimal?)(allocation.AllocatedAmount +
                                                         allocation.DiscountAmount +
                                                         allocation.WithholdingTaxAmount)) ?? 0m) -
                        (liveSupplierDebitApplications
                            .Where(application => application.VendorInvoiceId == invoice.Id)
                            .Sum(application => (decimal?)application.ApplicationAmount) ?? 0m) -
                        (batchSelections
                            .Where(selection => selection.VendorInvoiceId == invoice.Id)
                            .Sum(selection => (decimal?)selection.Amount) ?? 0m)
                })
                .Where(item => item.UnreservedBalance > 0m)
                .OrderBy(item => item.Invoice.DueDate)
                .ThenBy(item => item.Invoice.Id)
                .Skip(skip)
                .Take(boundedPageSize)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var result = new List<OutstandingVendorInvoiceDto>();
            foreach (var row in invoicePage)
            {
                var i = row.Invoice;
                var unreservedBalance = RoundMoney(row.UnreservedBalance);

                var discountAvailable = i.EarlyPaymentDiscountPercentage > 0
                    && i.EarlyPaymentDiscountDueDate.HasValue
                    && i.EarlyPaymentDiscountDueDate.Value >= now.Date;

                result.Add(new OutstandingVendorInvoiceDto
                {
                    InvoiceId = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    SupplierInvoiceNumber = i.SupplierInvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    TotalAmount = i.TotalAmount,
                    PaidAmount = i.PaidAmount,
                    BalanceAmount = unreservedBalance,
                    ApplySupplierWithholdingDefaults = i.ApplySupplierWithholdingDefaults,
                    WithholdingTaxId = i.WithholdingTaxId,
                    WithholdingTaxRate = i.WithholdingTaxRate,
                    WithholdingTaxRateOverride = i.WithholdingTaxRateOverride,
                    WithholdingTaxAccountId = i.WithholdingTaxAccountId,
                    CurrencyCode = NormalizeCurrency(i.CurrencyCode, "GHS"),
                    DaysOverdue = i.DueDate.HasValue && i.DueDate.Value < now
                        ? (int)(now - i.DueDate.Value).TotalDays
                        : 0,
                    EarlyPaymentDiscountPercentage = i.EarlyPaymentDiscountPercentage,
                    EarlyPaymentDiscountDueDate = i.EarlyPaymentDiscountDueDate,
                    IsDiscountAvailable = discountAvailable,
                    DiscountAmount = discountAvailable
                        ? RoundMoney(unreservedBalance *
                                     i.EarlyPaymentDiscountPercentage / 100m)
                        : 0m,
                    PaymentReadiness = await EvaluateInvoicePaymentReadinessAsync(
                        i.Id,
                        cancellationToken,
                        preloadedInvoice: i)
                });
            }

            return result;
        }

        public Task<VendorPaymentInvoiceReadinessDto> GetInvoicePaymentReadinessAsync(
            Guid invoiceId,
            CancellationToken cancellationToken = default) =>
            EvaluateInvoicePaymentReadinessAsync(invoiceId, cancellationToken);

        // ═════════════════════════════════════════════════════════════════
        //  PAYMENT STATUS
        // ═════════════════════════════════════════════════════════════════

        public async Task<VendorPaymentDto> ClearPaymentAsync(Guid id, DateTime clearedDate, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Vendor payment with Id '{id}' not found.");

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                await ResolveBankAccountIdForScopeAsync(payment.BankAccountId, cancellationToken),
                FinanceAccessLevel.Operate,
                cancellationToken);

            payment.Status = VendorPaymentStatus.Cleared;
            payment.ClearedDate = clearedDate;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cleared vendor payment {PaymentNumber} on {ClearedDate}", payment.PaymentNumber, clearedDate);
            return MapToDto(payment);
        }

        public Task<VendorPaymentDto> VoidPaymentAsync(
            Guid id,
            string reason,
            CancellationToken cancellationToken = default) =>
            VoidPaymentAsync(id, reason, cancellationToken, executionStrategyScope: false);

        private async Task<VendorPaymentDto> VoidPaymentAsync(
            Guid id,
            string reason,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A void and reversal reason is required.", nameof(reason));
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP payment reversal.");

            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => VoidPaymentAsync(id, reason, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            VendorPayment? payment = null;
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Payment(TenantId, id), cancellationToken);

                payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(p => p.TenantId == TenantId && p.Id == id && !p.IsDeleted)
                    .Include(p => p.Supplier)
                    .Include(p => p.Allocations.Where(a => !a.IsDeleted))
                        .ThenInclude(a => a.VendorInvoice)
                    .Include(p => p.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                        .ThenInclude(application => application.SupplierDebitNote)
                    .Include(p => p.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                        .ThenInclude(application => application.VendorInvoice)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Vendor payment with Id '{id}' not found.");

                if (payment.Status is VendorPaymentStatus.Cleared or VendorPaymentStatus.Reconciled)
                {
                    throw new InvalidOperationException(
                        "A cleared or reconciled vendor payment must first be removed from bank reconciliation before it can be voided.");
                }

                var originalAllocations = payment.Allocations
                    .Where(item => !item.IsReversal)
                    .OrderBy(item => item.VendorInvoiceId)
                    .ThenBy(item => item.Id)
                    .ToList();
                var reversedAllocationIds = payment.Allocations
                    .Where(item => item.IsReversal && item.OriginalAllocationId.HasValue)
                    .Select(item => item.OriginalAllocationId!.Value)
                    .ToHashSet();
                var activeSupplierDebitApplications = GetEffectiveSupplierDebitNoteApplications(
                    payment.SupplierDebitNoteApplications);
                if (payment.IsSupplierAdvance && originalAllocations.Any(item =>
                        !reversedAllocationIds.Contains(item.Id) &&
                        item.ApplicationPostingEventId.HasValue))
                {
                    // The cash origin and each application are separate accounting documents.
                    // Require application reversals first so a reviewer can see the lot restored
                    // before the bank payment itself is removed.
                    throw new InvalidOperationException(
                        "This supplier advance has posted applications. Reverse those applications before reversing the original payment.");
                }

                foreach (var invoiceId in originalAllocations
                             .Select(item => item.VendorInvoiceId)
                             .Concat(activeSupplierDebitApplications.Select(item => item.VendorInvoiceId))
                             .Distinct()
                             .OrderBy(item => item))
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, invoiceId), cancellationToken);
                }

                FinancePostingEvent? paymentPostingEvent = null;
                FinancePostingResultDto? paymentReversal = null;
                if (payment.JournalEntryId.HasValue)
                {
                    paymentPostingEvent = await GetPostedPaymentEventAsync(payment, cancellationToken);
                }

                var pendingAllocationReversals = new List<PendingAllocationReversal>();
                var auxiliaryReversalIds = new List<Guid>();
                foreach (var allocation in originalAllocations.Where(item => !reversedAllocationIds.Contains(item.Id)))
                {
                    if (allocation.VendorInvoice == null || allocation.VendorInvoice.TenantId != TenantId)
                        throw new InvalidOperationException(
                            "AP payment allocation references an invoice from another tenant.");

                    FinancePostingResultDto? applicationReversal = null;
                    var realizedFx = await _unitOfWork.Repository<FxRealizedSettlement>()
                        .GetQueryable(item =>
                            item.TenantId == TenantId &&
                            item.SourceModule == "AP" &&
                            item.SettlementDocumentType == "VendorPayment" &&
                            item.SettlementDocumentId == payment.Id &&
                            item.SettlementAllocationId == allocation.Id &&
                            !item.IsDeleted)
                        .OrderByDescending(item => item.PostedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (realizedFx?.PostingEventId is Guid fxPostingEventId &&
                        string.Equals(realizedFx.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                    {
                        var fxReversal = await ReversePostedEventAsync(
                            fxPostingEventId,
                            reason,
                            $"FX:Realized:AP:{TenantId:N}:{allocation.Id:N}:Reverse",
                            "Realized FX Reversal",
                            $"Reverse realized FX for AP payment {payment.PaymentNumber}",
                            cancellationToken);
                        realizedFx.Status = "Reversed";
                        realizedFx.UpdatedAt = DateTime.UtcNow;
                        realizedFx.UpdatedBy = UserName;
                        await _unitOfWork.Repository<FxRealizedSettlement>().UpdateAsync(realizedFx);
                        auxiliaryReversalIds.Add(fxReversal.PostingEventId);
                    }

                    if (payment.IsSupplierAdvance &&
                        allocation.ApplicationPostingEventId.HasValue &&
                        paymentPostingEvent != null &&
                        allocation.ApplicationPostingEventId.Value != paymentPostingEvent.Id)
                    {
                        applicationReversal = await ReversePostedEventAsync(
                            allocation.ApplicationPostingEventId.Value,
                            reason,
                            $"AP:VendorPaymentAdvanceApplication:{TenantId:N}:{allocation.Id:N}:Reverse",
                            "Supplier Advance Application Reversal",
                            $"Reverse supplier advance application {payment.PaymentNumber}",
                            cancellationToken);
                        auxiliaryReversalIds.Add(applicationReversal.PostingEventId);
                    }

                    pendingAllocationReversals.Add(new PendingAllocationReversal(
                        allocation,
                        allocation.VendorInvoice,
                        applicationReversal));
                }

                if (paymentPostingEvent != null)
                {
                    paymentReversal = await ReversePostedEventAsync(
                        paymentPostingEvent.Id,
                        reason,
                        $"AP:VendorPayment:{TenantId:N}:{payment.Id:N}:Reverse",
                        "AP Payment Reversal",
                        $"Reverse AP payment {payment.PaymentNumber}",
                        cancellationToken);
                }

                var now = DateTime.UtcNow;
                foreach (var pending in pendingAllocationReversals)
                {
                    var original = pending.Allocation;
                    var invoice = pending.Invoice;
                    var settlementAmount = RoundMoney(
                        original.AllocatedAmount +
                        original.DiscountAmount +
                        original.WithholdingTaxAmount);

                    if (payment.JournalEntryId.HasValue)
                    {
                        invoice.PaidAmount = Math.Max(0m, RoundMoney(invoice.PaidAmount - settlementAmount));
                        invoice.Status = invoice.PaidAmount <= 0.01m
                            ? VendorInvoiceStatus.Approved
                            : VendorInvoiceStatus.PartiallyPaid;
                        invoice.UpdatedAt = now;
                        invoice.UpdatedBy = UserName;
                        invoice.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                        await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                    }

                    var allocationPosting = pending.ApplicationReversal ?? paymentReversal;
                    await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(new VendorPaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        VendorPaymentId = payment.Id,
                        VendorInvoiceId = invoice.Id,
                        AllocatedAmount = -original.AllocatedAmount,
                        PaymentCurrencyAmount = -original.PaymentCurrencyAmount,
                        InvoiceCurrencyCode = original.InvoiceCurrencyCode,
                        PaymentCurrencyCode = original.PaymentCurrencyCode,
                        IsCrossCurrency = original.IsCrossCurrency,
                        InvoiceSettlementExchangeRateId = original.InvoiceSettlementExchangeRateId,
                        InvoiceSettlementExchangeRate = original.InvoiceSettlementExchangeRate,
                        PaymentExchangeRateId = original.PaymentExchangeRateId,
                        PaymentExchangeRate = original.PaymentExchangeRate,
                        PaymentFunctionalAmount = -original.PaymentFunctionalAmount,
                        SettlementFunctionalAmount = -original.SettlementFunctionalAmount,
                        DiscountAmount = -original.DiscountAmount,
                        DiscountFunctionalAmount = -original.DiscountFunctionalAmount,
                        WithholdingTaxAmount = -original.WithholdingTaxAmount,
                        WithholdingTaxFunctionalAmount = -original.WithholdingTaxFunctionalAmount,
                        AllocationDate = now,
                        Notes = $"Controlled void reversal of allocation {original.Id}: {reason.Trim()}",
                        IsReversal = true,
                        OriginalAllocationId = original.Id,
                        ApplicationPostingEventId = allocationPosting?.PostingEventId,
                        ApplicationJournalEntryId = allocationPosting?.JournalEntryId,
                        CreatedAt = now,
                        CreatedBy = UserName,
                        CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
                    });
                }

                foreach (var application in activeSupplierDebitApplications)
                {
                    var invoice = application.VendorInvoice;
                    if (invoice == null || invoice.TenantId != TenantId)
                        throw new InvalidOperationException(
                            "Supplier debit-note application references an invoice from another tenant.");
                    if (application.PaymentPostingEventId.HasValue || payment.JournalEntryId.HasValue)
                    {
                        invoice.PaidAmount = Math.Max(
                            0m,
                            RoundMoney(invoice.PaidAmount - application.ApplicationAmount));
                        invoice.Status = invoice.PaidAmount <= 0.01m
                            ? VendorInvoiceStatus.Approved
                            : VendorInvoiceStatus.PartiallyPaid;
                        invoice.UpdatedAt = now;
                        invoice.UpdatedBy = UserName;
                        invoice.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                        await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                    }

                    await _unitOfWork.Repository<SupplierDebitNoteApplication>().AddAsync(
                        CreateSupplierDebitNoteApplicationReversal(
                            application,
                            now,
                            $"Controlled payment void: {reason.Trim()}",
                            paymentReversal?.PostingEventId,
                            paymentReversal?.JournalEntryId));
                }

                var alreadyVoided = payment.Status == VendorPaymentStatus.Voided;
                payment.Status = VendorPaymentStatus.Voided;
                payment.AllocatedAmount = 0m;
                payment.DiscountTaken = 0m;
                if (!alreadyVoided)
                {
                    payment.Notes = AppendLifecycleNote(
                        payment.Notes,
                        $"Voided on {now:yyyy-MM-dd HH:mm}: {reason.Trim()}");
                }
                payment.UpdatedAt = now;
                payment.UpdatedBy = UserName;
                payment.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (!alreadyVoided || pendingAllocationReversals.Count > 0 || paymentReversal?.WasDuplicate == false)
                {
                    await RecordApPaymentAuditAsync(
                        payment.JournalEntryId.HasValue
                            ? FinanceAuditEvents.ApPaymentReversed
                            : FinanceAuditEvents.ApPaymentVoided,
                        payment,
                        postingEventId: paymentReversal?.PostingEventId,
                        journalEntryId: paymentReversal?.JournalEntryId,
                        beforeValues: new
                        {
                            status = alreadyVoided ? VendorPaymentStatus.Voided : (VendorPaymentStatus?)null,
                            payment.JournalEntryId
                        },
                        afterValues: new
                        {
                            payment.Status,
                            payment.AllocatedAmount,
                            OriginalJournalEntryId = payment.JournalEntryId,
                            ReversalPostingEventId = paymentReversal?.PostingEventId,
                            ReversalJournalEntryId = paymentReversal?.JournalEntryId,
                            AllocationReversalCount = pendingAllocationReversals.Count,
                            SupplierDebitApplicationReversalCount = activeSupplierDebitApplications.Count,
                            AuxiliaryReversalPostingEventIds = auxiliaryReversalIds
                        },
                        reason: reason.Trim(),
                        comment: "AP payment, allocation, invoice-balance, realized-FX and supplier-advance reversals were committed atomically.",
                        cancellationToken: cancellationToken);
                }

                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogWarning(
                    "Voided AP payment {PaymentNumber}. OriginalJournal={OriginalJournalId}; ReversalJournal={ReversalJournalId}; AllocationReversals={AllocationReversalCount}; Reason={Reason}",
                    payment.PaymentNumber,
                    payment.JournalEntryId,
                    paymentReversal?.JournalEntryId,
                    pendingAllocationReversals.Count,
                    reason.Trim());

                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
            }
            catch (Exception ex)
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();

                if (payment != null)
                {
                    await RecordApPaymentAuditAsync(
                        FinanceAuditEvents.ApPaymentReversalFailed,
                        payment,
                        afterValues: new { payment.JournalEntryId, error = ex.Message },
                        reason: ex.Message,
                        comment: "AP payment void was rejected; no payment, allocation, invoice-balance, or ledger mutation was committed.",
                        cancellationToken: cancellationToken);
                }

                throw;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EARLY PAYMENT DISCOUNT
        // ═════════════════════════════════════════════════════════════════

        public async Task<EarlyPaymentDiscountResultDto> CalculateEarlyPaymentDiscountAsync(
            Guid invoiceId, DateTime paymentDate, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == invoiceId);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{invoiceId}' not found.");

            var balance = invoice.TotalAmount - invoice.PaidAmount;
            var isEligible = invoice.EarlyPaymentDiscountPercentage > 0
                          && invoice.EarlyPaymentDiscountDueDate.HasValue
                          && paymentDate <= invoice.EarlyPaymentDiscountDueDate.Value;

            var discountAmount = isEligible
                ? balance * (invoice.EarlyPaymentDiscountPercentage / 100)
                : 0;

            return new EarlyPaymentDiscountResultDto
            {
                InvoiceId = invoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceBalance = balance,
                DiscountPercentage = invoice.EarlyPaymentDiscountPercentage,
                DiscountAmount = discountAmount,
                NetPayableAmount = balance - discountAmount,
                DiscountDueDate = invoice.EarlyPaymentDiscountDueDate ?? DateTime.MinValue,
                IsEligible = isEligible,
                DaysUntilExpiry = invoice.EarlyPaymentDiscountDueDate.HasValue
                    ? Math.Max(0, (int)(invoice.EarlyPaymentDiscountDueDate.Value - paymentDate).TotalDays)
                    : 0
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  PAYMENT BATCHES
        // ═════════════════════════════════════════════════════════════════

        public Task<PaymentBatchDto> CreatePaymentBatchAsync(
            PaymentBatchCreateDto dto,
            CancellationToken cancellationToken = default) =>
            CreatePaymentBatchAsync(dto, cancellationToken, executionStrategyScope: false);

        private async Task<PaymentBatchDto> CreatePaymentBatchAsync(
            PaymentBatchCreateDto dto,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => CreatePaymentBatchAsync(dto, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            if (dto.InvoiceIds.Count == 0)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_EMPTY", "At least one vendor invoice is required.");
            if (dto.InvoiceIds.Count != dto.InvoiceIds.Distinct().Count())
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_DUPLICATE_INVOICE", "A vendor invoice may only appear once in a payment batch.");

            var batchNumber = await GenerateBatchNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var batchId = Guid.NewGuid();
            if (CurrentUserId == Guid.Empty)
                throw new UnauthorizedAccessException("An authenticated Finance user is required to create a payment batch.");
            var approvalRequired = await IsPaymentApprovalRequiredAsync("PaymentBatch", batchId);

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    dto.InvoiceIds.Contains(i.Id) && !i.IsDeleted)
                .Include(i => i.Supplier)
                .ToListAsync(cancellationToken);

            if (invoices.Count != dto.InvoiceIds.Count)
                throw new KeyNotFoundException("One or more selected vendor invoices were not found in the current tenant.");

            var configuredPaymentMethod = await ResolveConfiguredPaymentMethodAsync(
                dto.PaymentMethodId,
                dto.BankAccountId,
                referenceNumber: null,
                label: "payment batch",
                enforceReference: false,
                cancellationToken);
            var paymentMethod = configuredPaymentMethod == null
                ? dto.PaymentMethod
                : MapConfiguredPaymentMethodToVendorPaymentMethod(configuredPaymentMethod.Type);

            if (!_unitOfWork.HasActiveTransaction)
            {
                foreach (var invoice in invoices.OrderBy(item => item.Id))
                    await RequirePaymentReadinessAsync(
                        invoice.Id,
                        ProcurementPaymentReadinessRules.BatchCreateAction,
                        paymentId: null,
                        batchId,
                        cancellationToken);
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
            foreach (var invoiceId in dto.InvoiceIds.OrderBy(item => item))
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.Invoice(TenantId, invoiceId), cancellationToken);

            // Refresh after taking the per-invoice locks. Another allocator may have
            // committed between the initial UI/readiness query and this transaction.
            invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    dto.InvoiceIds.Contains(i.Id) &&
                    !i.IsDeleted)
                .Include(i => i.Supplier)
                .ToListAsync(cancellationToken);
            var availableBalanceByInvoice = new Dictionary<Guid, decimal>();
            foreach (var invoice in invoices.OrderBy(item => item.Id))
            {
                var availableBalance = await GetInvoiceUnreservedBalanceAsync(
                    invoice,
                    currentPaymentId: null,
                    cancellationToken);
                if (availableBalance <= 0m)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_BALANCE_RESERVED",
                        $"Invoice '{invoice.InvoiceNumber}' has no unreserved outstanding balance.");
                availableBalanceByInvoice[invoice.Id] = availableBalance;
            }

            var batch = new PaymentBatch
            {
                Id = batchId,
                TenantId = TenantId,
                BatchNumber = batchNumber,
                Description = dto.Description,
                BatchDate = dto.BatchDate,
                DueDateFrom = dto.DueDateFrom,
                DueDateTo = dto.DueDateTo,
                PaymentMethod = paymentMethod,
                PaymentMethodId = configuredPaymentMethod?.Id,
                BankAccountId = dto.BankAccountId,
                Status = approvalRequired ? PaymentBatchStatus.PendingApproval : PaymentBatchStatus.Approved,
                ApprovalRequired = approvalRequired,
                CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId,
                Notes = dto.Notes,
                CreatedAt = now,
                CreatedBy = UserName
            };

            var baseCurrencyCode = NormalizeCurrency(
                await _tenantSettingsService.GetBaseCurrencyAsync(),
                "GHS");
            var selectedCurrencies = invoices
                .Select(invoice => NormalizeCurrency(
                    invoice.CurrencyCode,
                    baseCurrencyCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (selectedCurrencies.Count != 1)
            {
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_CURRENCY_MISMATCH",
                    "A payment batch must contain invoices in exactly one currency. Create a separate batch for each currency.");
            }
            if (!approvalRequired && !string.Equals(selectedCurrencies[0], baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                // The legacy batch factory below retains a placeholder rate of one. It
                // cannot establish functional-currency authority thresholds for a foreign
                // payment. A direct payment with validated settlement FX remains available.
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_SETTLEMENT_RATE_REQUIRED",
                    "This foreign-currency batch needs a validated settlement exchange rate before completion. Record a direct payment with the correct rate; a rate of one cannot be assumed.");
            }
            // A payment is denominated in exactly one currency. Splitting on
            // both dimensions prevents unconverted mixed-currency totals and
            // guarantees that every later allocation matches its payment.
            var bySupplierAndCurrency = invoices.GroupBy(invoice => new
            {
                invoice.SupplierId,
                CurrencyCode = NormalizeCurrency(
                    invoice.CurrencyCode,
                    baseCurrencyCode)
            });
            decimal totalAmount = 0;
            int paymentCount = 0;

            foreach (var group in bySupplierAndCurrency)
            {
                var supplier = group.First().Supplier;
                var supplierTotal = group.Sum(i => availableBalanceByInvoice[i.Id]);

                var paymentNumber = await GeneratePaymentNumberAsync(cancellationToken);
                var payment = new VendorPayment
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PaymentNumber = paymentNumber,
                    SupplierId = group.Key.SupplierId,
                    PaymentDate = dto.BatchDate,
                    TotalAmount = supplierTotal,
                    AllocatedAmount = 0,
                    PaymentMethod = paymentMethod,
                    PaymentMethodId = configuredPaymentMethod?.Id,
                    CurrencyCode = group.Key.CurrencyCode,
                    ExchangeRate = 1.0m,
                    BankAccountId = dto.BankAccountId,
                    PaymentBatchId = batch.Id,
                    PaymentBatch = batch,
                    Status = VendorPaymentStatus.PendingAuthorization,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                var batchItem = new PaymentBatchItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PaymentBatchId = batch.Id,
                    PaymentBatch = batch,
                    VendorPaymentId = payment.Id,
                    VendorPayment = payment,
                    Amount = supplierTotal,
                    ItemStatus = "Pending",
                    CreatedAt = now,
                    CreatedBy = UserName
                };
                batch.Items.Add(batchItem);

                foreach (var invoice in group.OrderBy(item => item.DueDate).ThenBy(item => item.Id))
                {
                    var selection = new PaymentBatchInvoice
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        PaymentBatchId = batch.Id,
                        PaymentBatch = batch,
                        PaymentBatchItemId = batchItem.Id,
                        PaymentBatchItem = batchItem,
                        VendorPaymentId = payment.Id,
                        VendorPayment = payment,
                        VendorInvoiceId = invoice.Id,
                        VendorInvoice = invoice,
                        Amount = availableBalanceByInvoice[invoice.Id],
                        Status = "Pending",
                        CreatedAt = now,
                        CreatedBy = UserName
                    };
                    var decision = await RequireBatchInvoiceReadinessAsync(
                        selection,
                        ProcurementPaymentReadinessRules.BatchCreateAction,
                        cancellationToken);
                    selection.PaymentReadinessControlEventId = decision.Event.Id;
                    selection.PaymentReadinessSnapshotHash = decision.Readiness.SnapshotHash;
                    selection.PaymentReadinessEvaluatedAtUtc = decision.Readiness.EvaluatedAtUtc;
                    batchItem.Invoices.Add(selection);
                }

                totalAmount += supplierTotal;
                paymentCount++;
            }

            batch.TotalAmount = totalAmount;
            batch.PaymentCount = paymentCount;

            // Attach the fully evidenced aggregate only after every AP-003 event has been
            // appended. The shared control-event service saves immediately; attaching a
            // partially built batch-owned payment earlier would flush an incomplete graph
            // (payment FK before batch/selection evidence) during that save.
            if (!approvalRequired)
            {
                foreach (var item in batch.Items)
                    await CaptureNoApprovalPaymentPolicyAsync(item.VendorPayment, new SubmitVendorPaymentDto(), cancellationToken);
            }
            await _unitOfWork.Repository<PaymentBatch>().AddAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (approvalRequired)
            {
                var workflowResult = await _workflowService.StartApprovalWorkflowAsync("PaymentBatch", batch.Id);
                if (!workflowResult.Success || !workflowResult.WorkflowInstanceId.HasValue)
                    throw new InvalidOperationException(workflowResult.Message ?? "Unable to start payment batch approval workflow.");
            }
            else
            {
                foreach (var item in batch.Items)
                    await RecordNoApprovalPaymentAuditAsync(item.VendorPayment, cancellationToken);
            }

            if (ownsTransaction)
                await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Created payment batch {BatchNumber} with {Count} payments, total {Total}",
                batchNumber, paymentCount, totalAmount);

            return await GetPaymentBatchAsync(batch.Id, cancellationToken) ?? throw new InvalidOperationException("Failed to retrieve created batch.");
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<PaymentBatchDto?> GetPaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            var batch = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId && b.Id == batchId)
                .Include(b => b.Items)
                    .ThenInclude(i => i.VendorPayment)
                        .ThenInclude(p => p.Supplier)
                .Include(b => b.Items)
                    .ThenInclude(i => i.Invoices)
                        .ThenInclude(i => i.VendorInvoice)
                .Include(b => b.BankAccount)
                .Include(b => b.ConfiguredPaymentMethod)
                .FirstOrDefaultAsync(cancellationToken);

            return batch == null ? null : MapBatchToDto(batch);
        }

        public async Task<PagedResult<PaymentBatchDto>> GetAllBatchesAsync(PaymentBatchQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId);

            if (query.Status.HasValue)
                queryable = queryable.Where(b => b.Status == query.Status.Value);

            if (query.FromDate.HasValue)
                queryable = queryable.Where(b => b.BatchDate >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                queryable = queryable.Where(b => b.BatchDate <= query.ToDate.Value);

            var totalCount = await queryable.CountAsync(cancellationToken);

            queryable = query.SortBy?.ToLower() switch
            {
                "batchnumber" => query.SortDescending
                    ? queryable.OrderByDescending(b => b.BatchNumber)
                    : queryable.OrderBy(b => b.BatchNumber),
                "amount" => query.SortDescending
                    ? queryable.OrderByDescending(b => b.TotalAmount)
                    : queryable.OrderBy(b => b.TotalAmount),
                _ => queryable.OrderByDescending(b => b.BatchDate)
            };

            var batches = await queryable
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Include(b => b.Items)
                    .ThenInclude(i => i.VendorPayment)
                        .ThenInclude(p => p.Supplier)
                .Include(b => b.Items)
                    .ThenInclude(i => i.Invoices)
                        .ThenInclude(i => i.VendorInvoice)
                .Include(b => b.ConfiguredPaymentMethod)
                .ToListAsync(cancellationToken);

            return new PagedResult<PaymentBatchDto>
            {
                Items = batches.Select(MapBatchToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.Page,
                PageSize = query.PageSize
            };
        }

        public Task<PaymentBatchDto> ApprovePaymentBatchAsync(
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            ApprovePaymentBatchAsync(batchId, cancellationToken, executionStrategyScope: false);

        private async Task<PaymentBatchDto> ApprovePaymentBatchAsync(
            Guid batchId,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => ApprovePaymentBatchAsync(batchId, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            if (_invoicePaymentSod == null)
                throw new VendorPaymentControlException(
                    ProcurementInvoicePaymentSodRules.EvidenceCode,
                    "The authoritative invoice/payment SOD service is not configured.");

            var batch = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId && b.Id == batchId && !b.IsDeleted)
                .Include(b => b.Items)
                    .ThenInclude(item => item.Invoices)
                .SingleOrDefaultAsync(cancellationToken);

            if (batch == null)
                throw new KeyNotFoundException($"Payment batch with Id '{batchId}' not found.");

            if (batch.Status != PaymentBatchStatus.PendingApproval)
                throw new InvalidOperationException("Only pending batches can be approved.");
            if (!batch.ApprovalRequired)
                throw new InvalidOperationException("This batch does not require approval. Use Process instead.");

            if (CurrentUserId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            var selections = batch.Items.SelectMany(item => item.Invoices).ToList();
            if (selections.Count == 0)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_SELECTION_MISSING",
                    "The payment batch has no immutable invoice selections.");

            if (!_unitOfWork.HasActiveTransaction)
            {
                foreach (var selection in selections.OrderBy(item => item.VendorInvoiceId))
                    await RequireBatchInvoiceReadinessAsync(
                        selection,
                        ProcurementPaymentReadinessRules.BatchApproveAction,
                        cancellationToken);
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                foreach (var selection in selections.OrderBy(item => item.VendorInvoiceId))
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, selection.VendorInvoiceId), cancellationToken);
                    var decision = await RequireBatchInvoiceReadinessAsync(
                        selection,
                        ProcurementPaymentReadinessRules.BatchApproveAction,
                        cancellationToken);
                    selection.PaymentReadinessControlEventId = decision.Event.Id;
                    selection.PaymentReadinessSnapshotHash = decision.Readiness.SnapshotHash;
                    selection.PaymentReadinessEvaluatedAtUtc = decision.Readiness.EvaluatedAtUtc;
                    selection.UpdatedAt = DateTime.UtcNow;
                    selection.UpdatedBy = UserName;
                    await _unitOfWork.Repository<PaymentBatchInvoice>().UpdateAsync(selection);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var sod = await _invoicePaymentSod.EnforceBatchApprovalAsync(
                    batchId,
                    $"tdc0506-batch-approve-{batchId:N}-{CurrentUserId:N}",
                    cancellationToken);
                batch.InvoicePaymentSodControlEventId = sod.ControlEventId;
                batch.UpdatedAt = DateTime.UtcNow;
                batch.UpdatedBy = UserName;
                await _unitOfWork.Repository<PaymentBatch>().UpdateAsync(batch);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (!await _workflowService.CanUserApproveAsync("PaymentBatch", batchId, CurrentUserId))
                    throw new InvalidOperationException("This payment batch is assigned to another workflow approver.");

                var workflowResult = await _workflowService.ProcessApprovalStepAsync(
                    "PaymentBatch", batchId, CurrentUserId, "Approve");
                if (!workflowResult.Success)
                    throw new InvalidOperationException(workflowResult.Message ?? "Unable to process payment batch approval.");

                if (workflowResult.Status == WorkflowInstanceStatus.Completed)
                {
                    batch.Status = PaymentBatchStatus.Approved;
                    batch.ApprovedById = CurrentUserId;
                    batch.ApprovedDate = DateTime.UtcNow;
                    batch.InvoicePaymentSodControlEventId = sod.ControlEventId;
                    batch.UpdatedAt = DateTime.UtcNow;
                    batch.UpdatedBy = UserName;
                    await _unitOfWork.Repository<PaymentBatch>().UpdateAsync(batch);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Processed approval for payment batch {BatchNumber}; workflow status {Status}",
                    batch.BatchNumber, workflowResult.Status);
                return await GetPaymentBatchAsync(batchId, cancellationToken)
                    ?? throw new InvalidOperationException("Batch not found after approval.");
            }
            catch
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<PaymentBatchDto> ProcessPaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            if (_invoicePaymentSod == null)
                throw new VendorPaymentControlException(
                    ProcurementInvoicePaymentSodRules.EvidenceCode,
                    "The authoritative invoice/payment SOD service is not configured.");

            var claimAt = DateTime.UtcNow;
            var interruptedBefore = claimAt.AddMinutes(-30);
            var processorId = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId;
            var claimed = await ClaimBatchProcessingAsync(batchId, interruptedBefore, async () =>
                await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(batch =>
                    batch.TenantId == TenantId &&
                    batch.Id == batchId &&
                    !batch.IsDeleted &&
                    (batch.Status == PaymentBatchStatus.Approved ||
                     (batch.Status == PaymentBatchStatus.Processing &&
                      (!batch.ProcessedDate.HasValue || batch.ProcessedDate < interruptedBefore))))
                .ExecuteUpdateAsync(updates => updates
                        .SetProperty(batch => batch.Status, PaymentBatchStatus.Processing)
                        .SetProperty(batch => batch.ProcessedById, processorId)
                        .SetProperty(batch => batch.ProcessedDate, claimAt)
                        .SetProperty(batch => batch.UpdatedAt, claimAt)
                        .SetProperty(batch => batch.UpdatedBy, UserName),
                    cancellationToken), cancellationToken);

            if (claimed == 0)
            {
                var currentState = await _unitOfWork.Repository<PaymentBatch>()
                    .GetQueryable(batch =>
                        batch.TenantId == TenantId &&
                        batch.Id == batchId &&
                        !batch.IsDeleted)
                    .AsNoTracking()
                    .Select(batch => (PaymentBatchStatus?)batch.Status)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!currentState.HasValue)
                    throw new KeyNotFoundException($"Payment batch with Id '{batchId}' not found.");
                if (currentState == PaymentBatchStatus.Processing)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_BATCH_ALREADY_PROCESSING",
                        "This payment batch is already being processed. An interrupted claim can be resumed after its 30-minute lease expires.");
                throw new InvalidOperationException(
                    "Only approved or interrupted processing batches can be processed.");
            }

            var batch = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId && b.Id == batchId && !b.IsDeleted)
                .Include(b => b.Items.Where(item => !item.IsDeleted))
                    .ThenInclude(i => i.VendorPayment)
                        .ThenInclude(payment => payment.Allocations.Where(allocation =>
                            !allocation.IsDeleted))
                .Include(b => b.Items.Where(item => !item.IsDeleted))
                    .ThenInclude(i => i.Invoices.Where(selection => !selection.IsDeleted))
                        .ThenInclude(i => i.VendorInvoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (batch == null)
                throw new KeyNotFoundException($"Payment batch with Id '{batchId}' not found after its processing claim was acquired.");

            if (batch.Items.Count == 0 || batch.Items.Any(item => item.Invoices.Count == 0))
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_SELECTION_MISSING",
                    "Every payment batch item must retain at least one immutable invoice selection.");

            await _invoicePaymentSod.RevalidateBatchAuthorizationAsync(batchId, cancellationToken);

            var now = claimAt;
            var resumableItems = batch.Items
                .Where(item => !IsDurablyPosted(item.VendorPayment))
                .ToList();
            foreach (var selection in resumableItems.SelectMany(item => item.Invoices)
                         .OrderBy(item => item.VendorInvoiceId))
            {
                var decision = await RequireBatchInvoiceReadinessAsync(
                    selection,
                    ProcurementPaymentReadinessRules.BatchProcessAction,
                    cancellationToken);
                selection.PaymentReadinessControlEventId = decision.Event.Id;
                selection.PaymentReadinessSnapshotHash = decision.Readiness.SnapshotHash;
                selection.PaymentReadinessEvaluatedAtUtc = decision.Readiness.EvaluatedAtUtc;
                selection.UpdatedAt = now;
                selection.UpdatedBy = UserName;
                await _unitOfWork.Repository<PaymentBatchInvoice>().UpdateAsync(selection);
            }
            batch.Status = PaymentBatchStatus.Processing;
            batch.UpdatedAt = now;
            batch.UpdatedBy = UserName;
            await _unitOfWork.Repository<PaymentBatch>().UpdateAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            int processedCount = 0;
            int failedCount = 0;

            foreach (var item in batch.Items)
            {
                if (IsDurablyPosted(item.VendorPayment))
                {
                    MarkBatchItemProcessed(item);
                    processedCount++;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    continue;
                }

                try
                {
                    var payment = item.VendorPayment;
                    var allocs = item.Invoices
                        .OrderBy(selection => selection.VendorInvoice?.DueDate)
                        .ThenBy(selection => selection.VendorInvoiceId)
                        .Select(selection => new VendorPaymentAllocationCreateDto
                        {
                            VendorInvoiceId = selection.VendorInvoiceId,
                            AllocatedAmount = selection.Amount,
                            Notes = $"Payment batch {batch.BatchNumber} exact invoice selection"
                        }).ToList();

                    payment.Status = VendorPaymentStatus.Authorized;
                    payment.AuthorizedById = batch.ApprovedById;
                    payment.AuthorizedDate = batch.ApprovedDate;
                    payment.InvoicePaymentSodControlEventId = batch.InvoicePaymentSodControlEventId;
                    payment.UpdatedAt = now;
                    payment.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    var existingAllocations = GetEffectiveAllocations(payment.Allocations);
                    if (allocs.Any() && existingAllocations.Count == 0)
                    {
                        await AllocatePaymentAsync(
                            payment.Id,
                            allocs,
                            cancellationToken,
                            executionStrategyScope: false,
                            batchProcessorScope: true);
                    }
                    else if (allocs.Any())
                    {
                        EnsureBatchResumeAllocationsMatch(item, existingAllocations);
                    }

                    await PostAsync(payment.Id, cancellationToken, executionStrategyScope: false, batchProcessorScope: true);

                    MarkBatchItemProcessed(item);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    var postedPayment = await _unitOfWork
                        .Repository<VendorPayment>()
                        .GetQueryable(payment =>
                            payment.TenantId == TenantId &&
                            payment.Id == item.VendorPaymentId &&
                            !payment.IsDeleted)
                        .AsNoTracking()
                        .Select(payment => new
                        {
                            payment.Status,
                            payment.JournalEntryId
                        })
                        .SingleOrDefaultAsync(cancellationToken);
                    var postingCommitted = postedPayment?.JournalEntryId is not null &&
                                           postedPayment.Status is
                                               VendorPaymentStatus.Processed or
                                               VendorPaymentStatus.Cleared or
                                               VendorPaymentStatus.Reconciled;

                    if (postingCommitted)
                    {
                        item.ItemStatus = "Processed";
                        item.FailureReason = null;
                        item.VendorPayment.Status = VendorPaymentStatus.Processed;
                        item.VendorPayment.JournalEntryId =
                            postedPayment!.JournalEntryId;
                        foreach (var selection in item.Invoices)
                        {
                            selection.Status = "Processed";
                            selection.FailureReason = null;
                        }
                        processedCount++;
                        _logger.LogError(
                            ex,
                            "Payment batch item {ItemId} posted durably but a post-posting audit or FX step failed; preserving the committed payment outcome",
                            item.Id);
                    }
                    else
                    {
                        item.ItemStatus = "Failed";
                        item.FailureReason = ex.Message;
                        item.VendorPayment.Status = VendorPaymentStatus.Failed;
                        item.VendorPayment.UpdatedAt = DateTime.UtcNow;
                        item.VendorPayment.UpdatedBy = UserName;
                        foreach (var selection in item.Invoices)
                        {
                            selection.Status = "Failed";
                            selection.FailureReason = ex.Message;
                        }
                        failedCount++;
                        _logger.LogError(ex,
                            "Failed to process batch item {ItemId}", item.Id);
                    }
                }

                // Checkpoint each item while the batch remains Processing. A
                // crash or cancellation can therefore resume without replaying
                // a durable posting or its exact invoice allocations.
                batch.ProcessedDate = DateTime.UtcNow;
                batch.UpdatedAt = batch.ProcessedDate.Value;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Persist item/selection outcomes while the batch is still Processing.
            // The protected final batch transition then observes the exact durable outcomes.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            batch.Status = failedCount == 0
                ? PaymentBatchStatus.Completed
                : (processedCount > 0 ? PaymentBatchStatus.PartiallyCompleted : PaymentBatchStatus.Cancelled);

            batch.ProcessedById = _currentUser.UserId != null ? Guid.Parse(_currentUser.UserId) : null;
            batch.ProcessedDate = now;
            batch.UpdatedAt = now;
            batch.UpdatedBy = UserName;

            await _unitOfWork.Repository<PaymentBatch>().UpdateAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Processed payment batch {BatchNumber}: {Processed} processed, {Failed} failed",
                batch.BatchNumber, processedCount, failedCount);

            return await GetPaymentBatchAsync(batchId, cancellationToken) ?? throw new InvalidOperationException("Batch not found after processing.");
        }

        private static bool IsDurablyPosted(VendorPayment payment) =>
            payment.JournalEntryId.HasValue && payment.Status is
                VendorPaymentStatus.Processed or
                VendorPaymentStatus.Cleared or
                VendorPaymentStatus.Reconciled;

        private static void MarkBatchItemProcessed(PaymentBatchItem item)
        {
            item.ItemStatus = "Processed";
            item.FailureReason = null;
            foreach (var selection in item.Invoices)
            {
                selection.Status = "Processed";
                selection.FailureReason = null;
            }
        }

        private static void EnsureBatchResumeAllocationsMatch(
            PaymentBatchItem item,
            IReadOnlyCollection<VendorPaymentAllocation> existingAllocations)
        {
            var expected = item.Invoices
                .GroupBy(selection => selection.VendorInvoiceId)
                .ToDictionary(group => group.Key, group => group.Sum(selection => selection.Amount));
            var actual = existingAllocations
                .GroupBy(allocation => allocation.VendorInvoiceId)
                .ToDictionary(group => group.Key, group => group.Sum(allocation =>
                    allocation.AllocatedAmount + allocation.DiscountAmount +
                    allocation.WithholdingTaxAmount));
            var exact = expected.Count == actual.Count && expected.All(pair =>
                actual.TryGetValue(pair.Key, out var allocated) &&
                Math.Abs(allocated - pair.Value) <= 0.01m);
            if (!exact)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_RESUME_ALLOCATION_MISMATCH",
                    "The interrupted payment's saved allocations no longer match the batch's immutable invoice selections.");
        }

        private async Task<VendorPaymentInvoiceReadinessDto> EvaluateInvoicePaymentReadinessAsync(
            Guid invoiceId,
            CancellationToken cancellationToken,
            bool allowSettledInvoice = false,
            VendorInvoice? preloadedInvoice = null)
        {
            var invoice = preloadedInvoice;
            if (invoice is null)
            {
                invoice = await _unitOfWork.Repository<VendorInvoice>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.Id == invoiceId &&
                        !item.IsDeleted)
                    .AsNoTracking()
                    .SingleOrDefaultAsync(cancellationToken);
            }
            if (invoice is null || invoice.TenantId != TenantId ||
                invoice.Id != invoiceId || invoice.IsDeleted)
            {
                throw new KeyNotFoundException(
                    $"Vendor invoice with Id '{invoiceId}' not found.");
            }

            var evaluatedAtUtc = DateTime.UtcNow;
            var stateReady = ProcurementPaymentReadinessRules.IsInvoiceStatePaymentEligible(invoice.Status) ||
                allowSettledInvoice && invoice.Status == VendorInvoiceStatus.Paid;
            var outstandingAmount = RoundMoney(invoice.TotalAmount - invoice.PaidAmount);
            var matching = _vendorInvoiceService == null
                ? new InvoiceMatchingResultDto
                {
                    VendorInvoiceId = invoice.Id,
                    IsRequired = ProcurementInvoiceThreeWayMatchRules.IsRequired(
                        invoice.PurchaseOrderId, invoice.IsOpeningBalance),
                    Message = "The authoritative invoice matching service is not registered.",
                    DecisionKeys = ProcurementPaymentReadinessRules.DecisionKeys.ToList(),
                    Checks = new List<InvoiceMatchingCheckDto>
                    {
                        PaymentCheck("AP-PAYMENT-MATCH-SERVICE", "Mandatory matching service", false,
                            "The authoritative invoice matching service is not registered.")
                    }
                }
                : await _vendorInvoiceService.GetThreeWayMatchReadinessAsync(invoice.Id, cancellationToken);

            var required = matching.IsRequired;
            var persistedMatchEventValid = !required;
            if (required && invoice.MatchingControlEventId.HasValue &&
                !string.IsNullOrWhiteSpace(invoice.MatchingSnapshotHash) &&
                string.Equals(invoice.MatchingSnapshotHash, matching.SnapshotHash, StringComparison.OrdinalIgnoreCase))
            {
                persistedMatchEventValid = await _unitOfWork.Repository<ProcurementControlEvent>()
                    .GetQueryable(item => item.TenantId == TenantId &&
                        item.Id == invoice.MatchingControlEventId.Value &&
                        item.SourceType == "VendorInvoice" && item.SourceId == invoice.Id &&
                        item.RuleCode == ProcurementInvoiceThreeWayMatchRules.RuleCode &&
                        item.RuleVersion == ProcurementInvoiceThreeWayMatchRules.RuleVersion &&
                        item.Action == ProcurementInvoiceThreeWayMatchRules.EvaluationAction &&
                        item.Result == ProcurementControlEventResult.Allowed && !item.IsDeleted)
                    .AsNoTracking()
                    .AnyAsync(cancellationToken);
            }

            if (required && matching.ApprovedExceptionApplied)
            {
                persistedMatchEventValid = persistedMatchEventValid &&
                    invoice.MatchExceptionControlEventId.HasValue &&
                    invoice.MatchExceptionControlEventId == matching.MatchExceptionControlEventId;
            }

            var inspectionReady = !required || matching.Checks.Any(item =>
                item.CheckKey == "AP-MATCH-INSPECTION" && item.Passed);
            var matchReady = !required || matching.ApprovalReady && persistedMatchEventValid;
            var auditReady = _procurementControlEvents != null;
            var balanceReady = outstandingAmount > 0m || allowSettledInvoice && invoice.Status == VendorInvoiceStatus.Paid;
            var paymentReady = stateReady && balanceReady && matchReady && inspectionReady && auditReady;

            var checks = matching.Checks.ToList();
            checks.Add(PaymentCheck("AP-PAYMENT-INVOICE-STATE", "Payable invoice state", stateReady,
                stateReady
                    ? $"Invoice state {invoice.Status} is eligible for settlement."
                    : $"Invoice state {invoice.Status} is not eligible for settlement."));
            checks.Add(PaymentCheck("AP-PAYMENT-BALANCE", "Outstanding or settled balance", balanceReady,
                outstandingAmount > 0m
                    ? $"Outstanding amount {outstandingAmount:0.00} remains payable."
                    : allowSettledInvoice && invoice.Status == VendorInvoiceStatus.Paid
                        ? "The invoice is fully settled by the payment being revalidated for posting."
                    : "The invoice has no positive outstanding balance."));
            checks.Add(PaymentCheck("AP-PAYMENT-PERSISTED-MATCH", "Current persisted match decision",
                persistedMatchEventValid,
                persistedMatchEventValid
                    ? required
                        ? "The current invoice snapshot is bound to an allowed AP-002 / TDC-0504 decision."
                        : "Three-way matching is not required for this invoice."
                    : "The persisted AP-002 / TDC-0504 decision is missing, denied, or stale."));
            checks.Add(PaymentCheck("AP-PAYMENT-RECEIPT-INSPECTION", "Approved GRN and inspection", inspectionReady,
                inspectionReady
                    ? required
                        ? "The latest independently approved receipt inspection is AP eligible."
                        : "Receipt inspection is not applicable to this invoice."
                    : "A latest independently approved AP-eligible receipt inspection is required."));
            checks.Add(PaymentCheck("AP-PAYMENT-EXCEPTION", "Strict match exception status",
                !required || matching.IsMatched || matching.ApprovedExceptionApplied,
                matching.ApprovedExceptionApplied
                    ? "A current independently approved AP-006 / TDC-0507 exception covers the matching variance."
                    : matching.IsMatched || !required
                        ? "No matching exception is required."
                        : "No current independently approved match exception covers the variance."));
            checks.Add(PaymentCheck("AP-PAYMENT-AUDIT", "Immutable payment decision audit", auditReady,
                auditReady
                    ? "The shared procurement control-event service is available."
                    : "The shared procurement control-event service is not registered."));

            var message = paymentReady
                ? "Invoice is ready for controlled payment allocation and batch processing."
                : checks.FirstOrDefault(item => !item.Passed)?.Message ??
                  "Invoice is not ready for payment.";
            var snapshotHash = ProcurementPaymentReadinessRules.HashSnapshot(new
            {
                schemaVersion = "tdc.ap-payment-readiness.v1",
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.Status,
                invoice.TotalAmount,
                invoice.PaidAmount,
                outstandingAmount,
                allowSettledInvoice,
                invoice.PurchaseOrderId,
                matching.IsRequired,
                matching.IsMatched,
                matching.ApprovalReady,
                matching.ApprovedExceptionApplied,
                matching.SnapshotHash,
                invoice.MatchingControlEventId,
                invoice.MatchExceptionControlEventId,
                persistedMatchEventValid,
                inspectionReady,
                matching.ConfigurationProfileCode,
                matching.ConfigurationProfileVersion
            });

            return new VendorPaymentInvoiceReadinessDto
            {
                VendorInvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceStatus = invoice.Status,
                OutstandingAmount = outstandingAmount,
                IsPaymentReady = paymentReady,
                InvoiceStateReady = stateReady,
                ThreeWayMatchRequired = required,
                ThreeWayMatchReady = matchReady,
                ReceiptInspectionReady = inspectionReady,
                ApprovedExceptionApplied = matching.ApprovedExceptionApplied,
                PersistedMatchCurrent = persistedMatchEventValid,
                MatchingControlEventId = invoice.MatchingControlEventId,
                MatchExceptionControlEventId = invoice.MatchExceptionControlEventId,
                MatchSnapshotHash = matching.SnapshotHash,
                SnapshotHash = snapshotHash,
                EvaluatedAtUtc = evaluatedAtUtc,
                Message = message,
                ConfigurationProfileCode = matching.ConfigurationProfileCode,
                ConfigurationProfileVersion = matching.ConfigurationProfileVersion,
                DecisionKeys = ProcurementPaymentReadinessRules.DecisionKeys.ToList(),
                Checks = checks
            };
        }

        private async Task<ProcurementControlEventDto> RecordPaymentReadinessDecisionAsync(
            VendorPaymentInvoiceReadinessDto readiness,
            string action,
            Guid? paymentId,
            Guid? batchId,
            CancellationToken cancellationToken)
        {
            if (_procurementControlEvents == null)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_AUDIT_UNAVAILABLE",
                    "The shared procurement control-event service is required for payment decisions.");

            var correlationId = $"tdc0505-{readiness.VendorInvoiceId:N}-{Guid.NewGuid():N}";
            return await _procurementControlEvents.RecordAsync(new ProcurementControlEventWriteRequest
            {
                EventKey = ProcurementControlEventKey.Create(
                    "ap-payment-readiness", TenantId, readiness.VendorInvoiceId, action, correlationId),
                EventType = ProcurementPaymentReadinessRules.EventType,
                Action = action,
                Result = readiness.IsPaymentReady
                    ? ProcurementControlEventResult.Allowed
                    : ProcurementControlEventResult.Denied,
                RuleCode = ProcurementPaymentReadinessRules.RuleCode,
                RuleVersion = ProcurementPaymentReadinessRules.RuleVersion,
                DecisionKeys = ProcurementPaymentReadinessRules.DecisionKeys.ToList(),
                SourceType = "VendorInvoice",
                SourceId = readiness.VendorInvoiceId,
                SourceReference = readiness.InvoiceNumber,
                Reason = readiness.Message,
                InputValues = new
                {
                    paymentId,
                    batchId,
                    invoiceStatus = readiness.InvoiceStatus,
                    readiness.OutstandingAmount,
                    readiness.MatchingControlEventId,
                    readiness.MatchExceptionControlEventId
                },
                ResultValues = new
                {
                    readiness.IsPaymentReady,
                    readiness.InvoiceStateReady,
                    readiness.ThreeWayMatchRequired,
                    readiness.ThreeWayMatchReady,
                    readiness.ReceiptInspectionReady,
                    readiness.ApprovedExceptionApplied,
                    readiness.PersistedMatchCurrent,
                    readiness.SnapshotHash,
                    readiness.Checks
                },
                CorrelationId = correlationId,
                OccurredAtUtc = readiness.EvaluatedAtUtc,
                Evidence = new[]
                    {
                        readiness.MatchingControlEventId,
                        readiness.MatchExceptionControlEventId
                    }
                    .Where(item => item.HasValue)
                    .Select((item, index) => new ProcurementControlEventEvidenceReference
                    {
                        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                        Reference = item!.Value.ToString(),
                        Label = index == 0 ? "Mandatory three-way match decision" : "Approved match exception",
                        RequirementKey = index == 0
                            ? ProcurementInvoiceThreeWayMatchRules.RuleCode
                            : ProcurementInvoiceThreeWayMatchRules.ExceptionRuleCode
                    }).ToList()
            }, cancellationToken);
        }

        private async Task<(VendorPaymentInvoiceReadinessDto Readiness, ProcurementControlEventDto Event)>
            RequirePaymentReadinessAsync(
                Guid invoiceId,
                string action,
                Guid? paymentId,
                Guid? batchId,
                CancellationToken cancellationToken,
                bool allowSettledInvoice = false)
        {
            var readiness = await EvaluateInvoicePaymentReadinessAsync(
                invoiceId, cancellationToken, allowSettledInvoice);
            var controlEvent = await RecordPaymentReadinessDecisionAsync(
                readiness, action, paymentId, batchId, cancellationToken);
            readiness.PaymentReadinessControlEventId = controlEvent.Id;
            if (!readiness.IsPaymentReady)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_READINESS_BLOCKED", readiness.Message);
            return (readiness, controlEvent);
        }

        private async Task<(VendorPaymentInvoiceReadinessDto Readiness, ProcurementControlEventDto Event)>
            RequireBatchInvoiceReadinessAsync(
                PaymentBatchInvoice selection,
                string action,
                CancellationToken cancellationToken)
        {
            var readiness = await EvaluateInvoicePaymentReadinessAsync(selection.VendorInvoiceId, cancellationToken);
            var amountReady = selection.Amount > 0m && selection.Amount <= readiness.OutstandingAmount;
            readiness.Checks.Add(PaymentCheck(
                "AP-PAYMENT-BATCH-AMOUNT",
                "Locked batch amount",
                amountReady,
                amountReady
                    ? $"The locked batch amount {selection.Amount:0.00} remains within the current outstanding amount."
                    : $"The locked batch amount {selection.Amount:0.00} exceeds the current outstanding amount {readiness.OutstandingAmount:0.00}."));
            readiness.IsPaymentReady = readiness.IsPaymentReady && amountReady;
            readiness.SnapshotHash = ProcurementPaymentReadinessRules.HashSnapshot(new
            {
                readiness.SnapshotHash,
                selection.PaymentBatchId,
                selection.PaymentBatchItemId,
                selection.VendorPaymentId,
                selection.VendorInvoiceId,
                selection.Amount
            });
            if (!amountReady)
                readiness.Message = readiness.Checks.Last().Message;

            var controlEvent = await RecordPaymentReadinessDecisionAsync(
                readiness, action, selection.VendorPaymentId, selection.PaymentBatchId, cancellationToken);
            readiness.PaymentReadinessControlEventId = controlEvent.Id;
            if (!readiness.IsPaymentReady)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_READINESS_BLOCKED", readiness.Message);
            return (readiness, controlEvent);
        }

        private static InvoiceMatchingCheckDto PaymentCheck(
            string key,
            string label,
            bool passed,
            string message) => new()
        {
            CheckKey = key,
            Label = label,
            Passed = passed,
            ExceptionEligible = false,
            Message = message
        };

        // ═════════════════════════════════════════════════════════════════
        //  UTILITIES
        // ═════════════════════════════════════════════════════════════════

        private async Task<FinancePostingEvent> GetPostedPaymentEventAsync(
            VendorPayment payment,
            CancellationToken cancellationToken)
        {
            if (!payment.JournalEntryId.HasValue)
                throw new InvalidOperationException("The AP payment is not linked to a posted journal.");

            return await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.SourceModule == "AP" &&
                    item.SourceDocumentType == "VendorPayment" &&
                    item.SourceDocumentId == payment.Id &&
                    item.PostingAction == "Post" &&
                    item.PostingStatus == "Posted" &&
                    item.JournalEntryId == payment.JournalEntryId.Value &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The authoritative AP payment posting event was not found for this tenant.");
        }

        private async Task<FinancePostingResultDto> ReversePostedEventAsync(
            Guid postingEventId,
            string reason,
            string idempotencyKey,
            string journalType,
            string description,
            CancellationToken cancellationToken,
            string reversalType = "Controlled AP void")
        {
            var originalEvent = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.Id == postingEventId &&
                    item.PostingStatus == "Posted" &&
                    item.JournalEntryId.HasValue &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The authoritative Finance posting event was not found for this tenant.");

            var originalJournal = await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.Id == originalEvent.JournalEntryId!.Value &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The Finance posting journal was not found for this tenant.");

            if (originalJournal.IsReversed)
            {
                if (!originalJournal.ReversalJournalEntryId.HasValue)
                    throw new InvalidOperationException(
                        "The original journal is marked reversed but has no reversal-journal lineage.");

                var existing = await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.SourceModule == originalEvent.SourceModule &&
                        item.SourceDocumentType == originalEvent.SourceDocumentType &&
                        item.SourceDocumentId == originalEvent.SourceDocumentId &&
                        item.PostingAction != "Post" &&
                        item.PostingStatus == "Posted" &&
                        item.JournalEntryId == originalJournal.ReversalJournalEntryId.Value &&
                        !item.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The existing Finance reversal event was not found for this tenant.");

                return ToPostingResult(existing, wasDuplicate: true);
            }

            var plan = await _financePostingEngine!.GetReversalPlanAsync(
                originalEvent.Id,
                reason.Trim(),
                DateTime.UtcNow.Date,
                cancellationToken);
            return await _financePostingEngine.PostAsync(new FinancePostingRequestDto
            {
                SourceModule = originalEvent.SourceModule,
                OriginModuleCode = originalEvent.OriginModuleCode,
                SourceDocumentType = originalEvent.SourceDocumentType,
                SourceDocumentId = originalEvent.SourceDocumentId,
                SourceDocumentTenantId = originalEvent.TenantId,
                PostingAction = "Reverse",
                SourceDocumentReference = originalEvent.SourceDocumentReference,
                Description = description,
                PostingDate = plan.ReversalDate,
                JournalType = journalType,
                BookClassification = originalEvent.BookClassification,
                FunctionalCurrencyCode = originalEvent.FunctionalCurrencyCode,
                ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
                ReversalReason = reason.Trim(),
                ReversalType = reversalType,
                IdempotencyKey = idempotencyKey,
                ReturnExistingOnDuplicate = true,
                Lines = plan.ReversalLines
            }, cancellationToken);
        }

        private static FinancePostingResultDto ToPostingResult(
            FinancePostingEvent postingEvent,
            bool wasDuplicate) => new()
        {
            PostingEventId = postingEvent.Id,
            JournalEntryId = postingEvent.JournalEntryId!.Value,
            PostingStatus = postingEvent.PostingStatus,
            WasDuplicate = wasDuplicate,
            TotalDebitAmount = postingEvent.TotalDebitAmount,
            TotalCreditAmount = postingEvent.TotalCreditAmount,
            FunctionalCurrencyCode = postingEvent.FunctionalCurrencyCode,
            PostingDate = postingEvent.PostingDate,
            SourceModule = postingEvent.SourceModule,
            OriginModuleCode = postingEvent.OriginModuleCode ?? postingEvent.SourceModule,
            SourceDocumentType = postingEvent.SourceDocumentType,
            SourceDocumentId = postingEvent.SourceDocumentId,
            PostingAction = postingEvent.PostingAction
        };

        private static string AppendLifecycleNote(string? notes, string entry) =>
            string.IsNullOrWhiteSpace(notes) ? entry : $"{notes.TrimEnd()}\n\n{entry}";

        private async Task<VendorPayment> LoadPaymentForPostingAsync(Guid id, CancellationToken cancellationToken)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id && !p.IsDeleted)
                .Include(p => p.Supplier)
                .Include(p => p.BankAccount)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.VendorInvoice)
                .Include(p => p.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                    .ThenInclude(application => application.SupplierDebitNote)
                        .ThenInclude(note => note.Vendor)
                .Include(p => p.SupplierDebitNoteApplications.Where(application => !application.IsDeleted))
                    .ThenInclude(application => application.VendorInvoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Vendor payment with Id '{id}' not found.");

            return payment;
        }

        private async Task ApplyPostedPaymentAllocationsAsync(
            VendorPayment payment,
            Guid postingEventId,
            Guid journalEntryId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            foreach (var allocation in GetEffectiveAllocations(payment.Allocations)
                         .OrderBy(item => item.AllocationDate)
                         .ThenBy(item => item.Id))
            {
                if (allocation.ApplicationPostingEventId.HasValue)
                {
                    if (allocation.ApplicationPostingEventId.Value != postingEventId ||
                        allocation.ApplicationJournalEntryId != journalEntryId)
                    {
                        throw new InvalidOperationException(
                            $"Payment allocation '{allocation.Id}' is already bound to a different Finance posting.");
                    }

                    continue;
                }

                var invoice = allocation.VendorInvoice;
                if (invoice == null || invoice.TenantId != TenantId)
                    throw new InvalidOperationException("AP payment allocation references an invoice from another tenant.");

                var settlementAmount = RoundMoney(
                    allocation.AllocatedAmount +
                    allocation.DiscountAmount +
                    allocation.WithholdingTaxAmount);
                var resultingPaidAmount = RoundMoney(invoice.PaidAmount + settlementAmount);
                if (resultingPaidAmount > RoundMoney(invoice.TotalAmount) + 0.01m)
                    throw new InvalidOperationException(
                        $"AP payment would over-settle invoice '{invoice.InvoiceNumber}'.");

                invoice.PaidAmount = Math.Min(resultingPaidAmount, RoundMoney(invoice.TotalAmount));
                invoice.Status = invoice.PaidAmount >= RoundMoney(invoice.TotalAmount)
                    ? VendorInvoiceStatus.Paid
                    : VendorInvoiceStatus.PartiallyPaid;
                invoice.UpdatedAt = now;
                invoice.UpdatedBy = UserName;
                invoice.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);

                allocation.ApplicationPostingEventId = postingEventId;
                allocation.ApplicationJournalEntryId = journalEntryId;
                allocation.UpdatedAt = now;
                allocation.UpdatedBy = UserName;
                allocation.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                await _unitOfWork.Repository<VendorPaymentAllocation>().UpdateAsync(allocation);
            }
        }

        private async Task ApplyPostedSupplierDebitNoteApplicationsAsync(
            VendorPayment payment,
            Guid postingEventId,
            Guid journalEntryId,
            CancellationToken cancellationToken)
        {
            var applications = GetEffectiveSupplierDebitNoteApplications(payment.SupplierDebitNoteApplications);
            if (applications.Count == 0)
                return;

            var now = DateTime.UtcNow;
            foreach (var application in applications
                         .OrderBy(item => item.ApplicationDate)
                         .ThenBy(item => item.Id))
            {
                if (application.PaymentPostingEventId.HasValue)
                {
                    if (application.PaymentPostingEventId != postingEventId ||
                        application.PaymentJournalEntryId != journalEntryId)
                        throw new InvalidOperationException(
                            $"Supplier debit-note application '{application.Id}' is bound to a different payment posting.");
                    continue;
                }

                var note = application.SupplierDebitNote;
                var invoice = application.VendorInvoice;
                if (note == null || note.TenantId != TenantId || note.Status != SupplierDebitNoteStatus.Posted ||
                    !note.PostingEventId.HasValue || !note.JournalEntryId.HasValue)
                    throw new InvalidOperationException("AP payment references an unposted or cross-tenant supplier debit note.");
                if (invoice == null || invoice.TenantId != TenantId || !invoice.JournalEntryId.HasValue)
                    throw new InvalidOperationException("AP payment supplier-credit application references an unposted or cross-tenant invoice.");

                var resultingPaidAmount = RoundMoney(invoice.PaidAmount + application.ApplicationAmount);
                if (resultingPaidAmount > RoundMoney(invoice.TotalAmount) + 0.01m)
                    throw new InvalidOperationException(
                        $"Supplier debit-note application would over-settle invoice '{invoice.InvoiceNumber}'.");

                invoice.PaidAmount = Math.Min(resultingPaidAmount, RoundMoney(invoice.TotalAmount));
                invoice.Status = invoice.PaidAmount >= RoundMoney(invoice.TotalAmount)
                    ? VendorInvoiceStatus.Paid
                    : VendorInvoiceStatus.PartiallyPaid;
                invoice.UpdatedAt = now;
                invoice.UpdatedBy = UserName;
                invoice.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);

                application.PaymentPostingEventId = postingEventId;
                application.PaymentJournalEntryId = journalEntryId;
                application.AppliedAt = now;
                application.UpdatedAt = now;
                application.UpdatedBy = UserName;
                application.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                await _unitOfWork.Repository<SupplierDebitNoteApplication>().UpdateAsync(application);
            }

            await RecordApPaymentAuditAsync(
                SupplierDebitApplicationFinalizedEvent,
                payment,
                postingEventId: postingEventId,
                journalEntryId: journalEntryId,
                afterValues: new
                {
                    ApplicationIds = applications.Select(item => item.Id),
                    DebitNoteIds = applications.Select(item => item.SupplierDebitNoteId).Distinct(),
                    InvoiceIds = applications.Select(item => item.VendorInvoiceId).Distinct(),
                    Total = applications.Sum(item => item.FunctionalAmount)
                },
                comment: "Finalized supplier-credit subledger applications with the AP payment posting; no duplicate GL lines were created.",
                cancellationToken: cancellationToken);
        }

        private async Task<FinancePostingRequestDto> BuildApPaymentPostingRequestAsync(
            VendorPayment payment,
            CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            if (payment.TenantId != tenantId)
                throw new InvalidOperationException("Vendor payment belongs to another tenant.");

            if (!payment.ApprovalRequired && !payment.JournalEntryId.HasValue)
                await ValidateNoApprovalPaymentSnapshotAsync(payment, cancellationToken);

            if (!payment.JournalEntryId.HasValue &&
                payment.Status != VendorPaymentStatus.Authorized &&
                payment.Status != VendorPaymentStatus.Processed)
            {
                throw new InvalidOperationException("AP payment workflow approval is not complete.");
            }

            if (payment.Status == VendorPaymentStatus.Voided || payment.Status == VendorPaymentStatus.Failed)
                throw new InvalidOperationException("Voided or failed AP payments cannot be posted.");

            if (payment.TotalAmount <= 0m)
                throw new InvalidOperationException("AP payment amount must be positive.");

            var allAllocations = payment.IsSupplierAdvance && payment.JournalEntryId.HasValue
                ? new List<VendorPaymentAllocation>()
                : payment.Allocations?.Where(a => !a.IsDeleted).ToList()
                  ?? new List<VendorPaymentAllocation>();
            var activeAllocations = GetEffectiveAllocations(allAllocations);
            EnsureAllocationTotalIsValid(payment, activeAllocations);
            var activeSupplierDebitApplications = GetEffectiveSupplierDebitNoteApplications(
                payment.SupplierDebitNoteApplications);
            if (activeSupplierDebitApplications.Count > 0 && activeAllocations.Count == 0)
                throw new InvalidOperationException(
                    "A supplier debit note cannot turn a cash payment into a supplier advance. Allocate the payment cash before posting or use a standalone debit-note application workflow.");

            var isSupplierAdvance = activeAllocations.Count == 0;
            if (isSupplierAdvance &&
                (payment.WithholdingTaxAmount != 0m || payment.DiscountTaken != 0m))
            {
                throw new InvalidOperationException(
                    "Supplier advances cannot include withholding tax or settlement discounts. Apply the advance to a posted invoice before using those settlement features.");
            }

            foreach (var allocation in activeAllocations)
            {
                var paymentDecision = await RequirePaymentReadinessAsync(
                    allocation.VendorInvoiceId,
                    ProcurementPaymentReadinessRules.PostAction,
                    payment.Id,
                    payment.PaymentBatchId,
                    cancellationToken,
                    allowSettledInvoice: true);
                allocation.PaymentReadinessControlEventId = paymentDecision.Event.Id;
                allocation.PaymentReadinessSnapshotHash = paymentDecision.Readiness.SnapshotHash;
                allocation.PaymentReadinessEvaluatedAtUtc = paymentDecision.Readiness.EvaluatedAtUtc;
                allocation.UpdatedAt = DateTime.UtcNow;
                allocation.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorPaymentAllocation>().UpdateAsync(allocation);

                if (allocation.TenantId != tenantId || allocation.VendorPaymentId != payment.Id)
                    throw new InvalidOperationException("AP payment allocation belongs to another tenant or payment.");

                if (allocation.VendorInvoice == null || allocation.VendorInvoice.TenantId != tenantId)
                    throw new InvalidOperationException("AP payment allocation references an invoice from another tenant.");

                if (allocation.AllocatedAmount < 0m || allocation.DiscountAmount < 0m || allocation.WithholdingTaxAmount < 0m)
                    throw new InvalidOperationException("AP payment allocation amounts cannot be negative.");

                if (!allocation.VendorInvoice.JournalEntryId.HasValue)
                    throw new InvalidOperationException($"AP payment cannot settle unposted invoice '{allocation.VendorInvoice.InvoiceNumber}'.");

                var invoicePostingExists = await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(e =>
                        e.TenantId == tenantId &&
                        e.SourceDocumentType == "VendorInvoice" &&
                        e.SourceDocumentId == allocation.VendorInvoiceId &&
                        e.PostingAction == "Post" &&
                        e.PostingStatus == "Posted" &&
                        e.JournalEntryId.HasValue &&
                        !e.IsDeleted)
                    .AnyAsync(cancellationToken);

                if (!invoicePostingExists)
                    throw new InvalidOperationException($"AP payment cannot settle invoice '{allocation.VendorInvoice.InvoiceNumber}' because its central posting event was not found.");

                var invoiceAllocationHistory = await _unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryable(a =>
                        a.TenantId == tenantId &&
                        a.VendorInvoiceId == allocation.VendorInvoiceId &&
                        !a.IsDeleted)
                    .ToListAsync(cancellationToken);
                var totalInvoiceSettlement = GetEffectiveAllocations(invoiceAllocationHistory)
                    .Sum(a => a.AllocatedAmount + a.DiscountAmount + a.WithholdingTaxAmount);

                if (RoundMoney(totalInvoiceSettlement) > RoundMoney(allocation.VendorInvoice.TotalAmount))
                    throw new InvalidOperationException($"AP payment would over-settle invoice '{allocation.VendorInvoice.InvoiceNumber}'.");
            }

            foreach (var application in activeSupplierDebitApplications)
            {
                if (application.TenantId != tenantId || application.VendorPaymentId != payment.Id)
                    throw new InvalidOperationException("Supplier debit-note application belongs to another tenant or payment.");
                if (application.ApplicationAmount <= 0m)
                    throw new InvalidOperationException("Effective supplier debit-note applications must be positive.");
                if (application.SupplierDebitNote == null ||
                    application.SupplierDebitNote.TenantId != tenantId ||
                    application.SupplierDebitNote.Status != SupplierDebitNoteStatus.Posted ||
                    !application.SupplierDebitNote.PostingEventId.HasValue ||
                    !application.SupplierDebitNote.JournalEntryId.HasValue)
                    throw new InvalidOperationException("AP payment references an unposted or cross-tenant supplier debit note.");
                if (!application.SupplierDebitNote.SupplierId.HasValue ||
                    application.SupplierDebitNote.SupplierId.Value != payment.SupplierId)
                    throw new InvalidOperationException(
                        $"Supplier debit note '{application.SupplierDebitNote.DebitNoteNumber}' does not belong to this payment's supplier.");
                if (application.VendorInvoice == null ||
                    application.VendorInvoice.TenantId != tenantId ||
                    application.VendorInvoice.SupplierId != payment.SupplierId ||
                    !application.VendorInvoice.JournalEntryId.HasValue ||
                    application.VendorInvoice.Status == VendorInvoiceStatus.Voided)
                    throw new InvalidOperationException("AP payment supplier-credit application references an invalid invoice.");
                await EnsureSupplierCreditSourceJournalsActiveAsync(
                    application.SupplierDebitNote,
                    application.VendorInvoice,
                    cancellationToken);
                if (!string.Equals(
                        NormalizeCurrency(application.CurrencyCode, "GHS"),
                        NormalizeCurrency(application.VendorInvoice.CurrencyCode, "GHS"),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Supplier debit-note and invoice currencies no longer match.");
                if (payment.PaymentDate.Date < application.SupplierDebitNote.DebitNoteDate.Date)
                    throw new InvalidOperationException("Payment date cannot precede an applied supplier debit note.");
                if (application.SupplierDebitNote.ExchangeRate <= 0m ||
                    application.ExchangeRate <= 0m ||
                    RoundRate(application.ExchangeRate) != RoundRate(application.SupplierDebitNote.ExchangeRate))
                    throw new InvalidOperationException(
                        "Supplier debit-note application no longer matches the note's positive frozen carrying rate.");
                if (RoundRate(application.SupplierDebitNote.ExchangeRate) !=
                    RoundRate(application.VendorInvoice.ExchangeRate))
                    throw new InvalidOperationException(
                        "Supplier debit-note and invoice carrying rates no longer match; a controlled AP/FX reclassification is required before application.");
                var noteControlAccountId = await ResolvePostedApControlAccountAsync(
                    application.SupplierDebitNote.JournalEntryId!.Value, debitSide: true, cancellationToken);
                var invoiceControlAccountId = await ResolvePostedApControlAccountAsync(
                    application.VendorInvoice.JournalEntryId!.Value, debitSide: false, cancellationToken);
                if (noteControlAccountId != invoiceControlAccountId)
                    throw new InvalidOperationException(
                        "Supplier debit-note and invoice AP-control account lineage no longer matches.");

                var invoiceCashHistory = await _unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        item.VendorInvoiceId == application.VendorInvoiceId &&
                        !item.IsDeleted)
                    .ToListAsync(cancellationToken);
                var invoiceCreditHistory = await _unitOfWork.Repository<SupplierDebitNoteApplication>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        item.VendorInvoiceId == application.VendorInvoiceId &&
                        !item.IsDeleted)
                    .ToListAsync(cancellationToken);
                var totalInvoiceSettlement = RoundMoney(
                    GetEffectiveAllocations(invoiceCashHistory)
                        .Sum(item => item.AllocatedAmount + item.DiscountAmount + item.WithholdingTaxAmount) +
                    GetEffectiveSupplierDebitNoteApplications(invoiceCreditHistory)
                        .Sum(item => item.ApplicationAmount));
                if (totalInvoiceSettlement > RoundMoney(application.VendorInvoice.TotalAmount) + 0.01m)
                    throw new InvalidOperationException(
                        $"Payment and supplier credits would over-settle invoice '{application.VendorInvoice.InvoiceNumber}'.");

                var noteHistory = await _unitOfWork.Repository<SupplierDebitNoteApplication>()
                    .GetQueryable(item =>
                        item.TenantId == tenantId &&
                        item.SupplierDebitNoteId == application.SupplierDebitNoteId &&
                        !item.IsDeleted)
                    .ToListAsync(cancellationToken);
                if (RoundMoney(GetEffectiveSupplierDebitNoteApplications(noteHistory)
                        .Sum(item => item.ApplicationAmount)) >
                    RoundMoney(application.SupplierDebitNote.TotalAmount - application.SupplierDebitNote.DirectInvoiceAppliedAmount) + 0.01m)
                    throw new InvalidOperationException(
                        $"Applications exceed supplier debit note '{application.SupplierDebitNote.DebitNoteNumber}'.");
            }

            if (activeAllocations.Count > 0 || activeSupplierDebitApplications.Count > 0)
                await _unitOfWork.SaveChangesAsync(cancellationToken);

            var supplier = await ResolvePaymentSupplierForPostingAsync(payment, cancellationToken);
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            NormalizeAndValidateAllocationCurrencyEvidence(
                payment,
                activeAllocations,
                functionalCurrency);
            payment.DiscountTaken = RoundMoney(activeAllocations.Sum(item => item.DiscountFunctionalAmount));
            await SynchronizeApWithholdingComplianceAsync(payment, activeAllocations, cancellationToken);
            var exchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
            var accountCache = new Dictionary<Guid, Account>();

            var apAccountId = supplier.DefaultApAccountId
                ?? settings.ControlAccountApId
                ?? throw new InvalidOperationException("AP control account is not configured for this tenant.");
            await ResolvePaymentPostingAccountAsync(apAccountId, "AP control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            Guid debitAccountId = apAccountId;
            if (isSupplierAdvance)
            {
                debitAccountId = settings.SupplierAdvanceAccountId
                    ?? throw new InvalidOperationException("Supplier advance account is not configured for this tenant.");
                var supplierAdvanceAccount = await ResolvePaymentPostingAccountAsync(
                    debitAccountId,
                    "supplier advance account",
                    accountCache,
                    allowControlAccount: false,
                    requireDirectPosting: true,
                    cancellationToken);
                if (supplierAdvanceAccount.AccountType != AccountType.Asset)
                    throw new InvalidOperationException("Supplier advance account must be an asset account.");
            }

            var bankAccountId = payment.BankAccountId
                ?? settings.DefaultBankAccountId
                ?? throw new InvalidOperationException("Bank account is not configured for AP payment posting.");
            var bankAccount = await _unitOfWork.Repository<BankAccount>()
                .GetQueryable(a => a.TenantId == tenantId && a.Id == bankAccountId && !a.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (bankAccount == null)
                throw new InvalidOperationException("AP payment bank account was not found for this tenant.");

            if (!bankAccount.IsActive)
                throw new InvalidOperationException($"AP payment bank account '{bankAccount.AccountName}' is inactive.");

            if (!bankAccount.GLAccountId.HasValue)
                throw new InvalidOperationException($"AP payment bank account '{bankAccount.AccountName}' is not linked to a GL account.");

            await ResolvePaymentPostingAccountAsync(bankAccount.GLAccountId.Value, "bank/cash account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            var postingLines = new List<FinancePostingLineDto>();
            var lineNumber = 1;
            if (isSupplierAdvance)
            {
                postingLines.Add(BuildPostingLine(
                    debitAccountId,
                    $"Supplier advance {payment.PaymentNumber}",
                    debitTransactionAmount: payment.TotalAmount,
                    creditTransactionAmount: 0m,
                    paymentCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    lineNumber++,
                    "AP-SupplierAdvance",
                    payment.ExchangeRateId));
            }
            else
            {
                foreach (var allocation in activeAllocations)
                {
                    var invoiceGrossAmount = RoundMoney(
                        allocation.AllocatedAmount + allocation.DiscountAmount + allocation.WithholdingTaxAmount);
                    var settlementFunctionalAmount = allocation.SettlementFunctionalAmount > 0m
                        ? RoundMoney(allocation.SettlementFunctionalAmount)
                        : RoundMoney(invoiceGrossAmount * NormalizeExchangeRate(allocation.InvoiceSettlementExchangeRate));
                    var effectiveControlRate = invoiceGrossAmount <= 0m
                        ? 1m
                        : decimal.Round(settlementFunctionalAmount / invoiceGrossAmount, 6, MidpointRounding.AwayFromZero);

                    // One AP-control line per invoice preserves that invoice's native-currency
                    // clearing quantity. Its functional amount is the actual settlement value;
                    // the separate FX event then restores the control account to historical cost.
                    postingLines.Add(BuildPostingLine(
                        debitAccountId,
                        $"AP settlement {payment.PaymentNumber} / {allocation.VendorInvoice.InvoiceNumber}",
                        debitTransactionAmount: invoiceGrossAmount,
                        creditTransactionAmount: 0m,
                        allocation.InvoiceCurrencyCode,
                        functionalCurrency,
                        effectiveControlRate,
                        payment.PaymentDate,
                        payment.PaymentNumber,
                        lineNumber++,
                        "AP-Control",
                        exchangeRateId: null,
                        functionalDebitOverride: settlementFunctionalAmount));
                }
            }

            postingLines.Add(BuildPostingLine(
                bankAccount.GLAccountId.Value,
                $"AP payment {payment.PaymentNumber}",
                debitTransactionAmount: 0m,
                creditTransactionAmount: payment.TotalAmount,
                paymentCurrency,
                functionalCurrency,
                exchangeRate,
                payment.PaymentDate,
                payment.PaymentNumber,
                lineNumber++,
                "AP-Bank",
                payment.ExchangeRateId));

            if (activeAllocations.Any(a => a.DiscountAmount > 0m))
            {
                var discountAccountId = settings.DiscountReceivedAccountId
                    ?? throw new InvalidOperationException("Purchase discount received account is not configured for this tenant.");
                await ResolvePaymentPostingAccountAsync(discountAccountId, "purchase discount received account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                foreach (var allocation in activeAllocations.Where(a => a.DiscountAmount > 0m))
                    postingLines.Add(BuildPostingLine(
                        discountAccountId,
                        $"Purchase discount - {payment.PaymentNumber} / {allocation.VendorInvoice.InvoiceNumber}",
                        debitTransactionAmount: 0m,
                        creditTransactionAmount: allocation.DiscountAmount,
                        allocation.InvoiceCurrencyCode,
                        functionalCurrency,
                        allocation.InvoiceSettlementExchangeRate,
                        payment.PaymentDate,
                        payment.PaymentNumber,
                        lineNumber++,
                        "AP-Discount",
                        allocation.InvoiceSettlementExchangeRateId,
                        functionalCreditOverride: allocation.DiscountFunctionalAmount));
            }

            if (activeAllocations.Any(a => a.WithholdingTaxAmount > 0m))
            {
                var taxAccountId = payment.WithholdingTaxAccountId
                    ?? await ResolveConfiguredWithholdingTaxPayableAccountAsync(payment.PaymentDate, payment.WithholdingTaxId, cancellationToken)
                    ?? throw new InvalidOperationException("AP withholding tax payable account is not configured for this tenant.");
                await ResolvePaymentPostingAccountAsync(taxAccountId, "withholding tax payable account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

                foreach (var allocation in activeAllocations.Where(a => a.WithholdingTaxAmount > 0m))
                    postingLines.Add(BuildPostingLine(
                        taxAccountId,
                        $"Withholding tax - {payment.PaymentNumber} / {allocation.VendorInvoice.InvoiceNumber}",
                        debitTransactionAmount: 0m,
                        creditTransactionAmount: allocation.WithholdingTaxAmount,
                        allocation.InvoiceCurrencyCode,
                        functionalCurrency,
                        allocation.InvoiceSettlementExchangeRate,
                        payment.PaymentDate,
                        payment.PaymentNumber,
                        lineNumber++,
                        "AP-WHT",
                        allocation.InvoiceSettlementExchangeRateId,
                        functionalCreditOverride: allocation.WithholdingTaxFunctionalAmount));
            }

            if (RoundMoney(postingLines.Sum(l => l.DebitAmount)) != RoundMoney(postingLines.Sum(l => l.CreditAmount)))
                throw new InvalidOperationException("AP payment posting is not balanced.");

            payment.BankAccountId ??= bankAccount.Id;
            payment.IsSupplierAdvance = isSupplierAdvance;

            return new FinancePostingRequestDto
            {
                SourceModule = "AP",
                SourceDocumentType = "VendorPayment",
                SourceDocumentId = payment.Id,
                SourceDocumentTenantId = payment.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = payment.PaymentNumber,
                Description = isSupplierAdvance
                    ? $"Supplier advance {payment.PaymentNumber} - {supplier.Name}"
                    : $"Vendor payment {payment.PaymentNumber} - {supplier.Name}",
                PostingDate = payment.PaymentDate,
                JournalType = "AP Payment",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"AP:VendorPayment:{payment.TenantId:N}:{payment.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines
            };
        }

        private async Task PostRealizedFxIfRequiredAsync(
            VendorPayment payment,
            CancellationToken cancellationToken)
        {
            var functionalCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync(), "GHS");
            // Realized settlement FX belongs to a foreign invoice exposure, not merely to foreign
            // cash. An unapplied foreign payment is an advance lot with no invoice basis, while a
            // foreign payment of a functional-currency invoice has no foreign AP carrying amount
            // to remeasure. Advance-application FX is posted later in that application's journal.
            var requiresFx = payment.Allocations.Any(allocation =>
                !allocation.IsReversal &&
                !string.Equals(
                    NormalizeCurrency(allocation.InvoiceCurrencyCode, functionalCurrency),
                    functionalCurrency,
                    StringComparison.OrdinalIgnoreCase));
            if (!requiresFx)
            {
                return;
            }

            if (_fxAccountingService == null)
            {
                throw new InvalidOperationException("FX accounting service is not configured for AP realized FX settlement posting.");
            }

            await _fxAccountingService.PostRealizedFxForApPaymentAsync(payment.Id, cancellationToken);
        }

        private async Task<Guid?> ResolveConfiguredWithholdingTaxPayableAccountAsync(
            DateTime paymentDate,
            Guid? taxId,
            CancellationToken cancellationToken)
        {
            var configuredTax = await _unitOfWork.Repository<Tax>()
                .GetQueryable(t =>
                    t.TenantId == TenantId &&
                    !t.IsDeleted &&
                    t.IsActive &&
                    (!taxId.HasValue || t.Id == taxId.Value) &&
                    t.Category == TaxCategory.Withholding &&
                    (t.Applicability == TaxApplicability.Purchases || t.Applicability == TaxApplicability.Both) &&
                    t.EffectiveFrom.Date <= paymentDate.Date &&
                    t.TaxPayableAccountId.HasValue)
                .OrderByDescending(t => t.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

            return configuredTax?.TaxPayableAccountId;
        }

        private async Task<Supplier> ResolvePaymentSupplierForPostingAsync(VendorPayment payment, CancellationToken cancellationToken)
        {
            var supplier = await _unitOfWork.Repository<Supplier>()
                .GetQueryable(s => s.TenantId == TenantId && s.Id == payment.SupplierId && !s.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (supplier == null)
                throw new InvalidOperationException("AP payment supplier was not found for this tenant.");

            if (!supplier.IsActive || supplier.IsBlacklisted || string.Equals(supplier.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Supplier '{supplier.Name}' is not active for AP payment posting.");

            return supplier;
        }

        private async Task<FinanceSettings> GetFinanceSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            return settings ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        }

        private async Task<Guid?> ResolveBankAccountIdForScopeAsync(
            Guid? paymentBankAccountId,
            CancellationToken cancellationToken)
        {
            if (paymentBankAccountId.HasValue)
                return paymentBankAccountId;

            // AP payments may omit a bank account at draft time and rely on the tenant default
            // during posting. Scope enforcement must resolve that same effective account; checking
            // only the nullable source field would otherwise let a restricted user bypass scope by
            // leaving the draft value blank.
            return await _unitOfWork.Repository<FinanceSettings>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
                .Select(item => item.DefaultBankAccountId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<Account> ResolvePaymentPostingAccountAsync(
            Guid accountId,
            string role,
            Dictionary<Guid, Account> accountCache,
            bool allowControlAccount,
            bool requireDirectPosting,
            CancellationToken cancellationToken)
        {
            if (accountCache.TryGetValue(accountId, out var cached))
            {
                return cached;
            }

            var account = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId && a.Id == accountId && !a.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (account == null)
                throw new InvalidOperationException($"AP payment posting {role} was not found for this tenant.");

            if (account.Status != AccountStatus.Active)
                throw new InvalidOperationException($"AP payment posting {role} account '{account.AccountNumber}' is not active.");

            if (account.IsControlAccount && !allowControlAccount)
                throw new InvalidOperationException($"AP payment posting {role} account '{account.AccountNumber}' is a control account and cannot be used for this line.");

            if (requireDirectPosting && !account.AllowDirectPosting)
                throw new InvalidOperationException($"AP payment posting {role} account '{account.AccountNumber}' does not allow direct posting.");

            accountCache[accountId] = account;
            return account;
        }

        private static FinancePostingLineDto BuildPostingLine(
            Guid accountId,
            string description,
            decimal debitTransactionAmount,
            decimal creditTransactionAmount,
            string transactionCurrency,
            string functionalCurrency,
            decimal exchangeRate,
            DateTime exchangeRateDate,
            string reference,
            int lineNumber,
            string transactionTag,
            Guid? exchangeRateId = null,
            decimal? functionalDebitOverride = null,
            decimal? functionalCreditOverride = null)
        {
            var isForeign = !string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
            var debitAmount = functionalDebitOverride ?? ToFunctionalAmount(debitTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate);
            var creditAmount = functionalCreditOverride ?? ToFunctionalAmount(creditTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate);

            return new FinancePostingLineDto
            {
                AccountId = accountId,
                Description = description,
                DebitAmount = debitAmount,
                CreditAmount = creditAmount,
                TransactionCurrency = transactionCurrency,
                TransactionDebitAmount = debitTransactionAmount,
                TransactionCreditAmount = creditTransactionAmount,
                ForeignCurrencyAmount = isForeign
                    ? debitTransactionAmount > 0m ? debitTransactionAmount : creditTransactionAmount
                    : null,
                ExchangeRateId = isForeign ? exchangeRateId : null,
                ExchangeRate = isForeign ? exchangeRate : null,
                ExchangeRateSource = isForeign ? "AP payment exchange-rate snapshot" : null,
                ExchangeRateDate = isForeign ? exchangeRateDate.Date : null,
                SourceReferenceNumber = reference,
                LineNumber = lineNumber,
                TransactionTag = transactionTag
            };
        }

        private async Task RecordApPaymentAuditAsync(
            string eventType,
            VendorPayment payment,
            Guid? postingEventId = null,
            Guid? journalEntryId = null,
            object? beforeValues = null,
            object? afterValues = null,
            string? reason = null,
            string? comment = null,
            CancellationToken cancellationToken = default)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = payment.TenantId,
                SourceModule = "AP",
                SourceDocumentType = "VendorPayment",
                SourceDocumentId = payment.Id,
                JournalEntryId = journalEntryId ?? payment.JournalEntryId,
                PostingEventId = postingEventId,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Comment = comment,
                Resource = "Finance.APPayment",
                ResourceId = payment.Id.ToString()
            }, cancellationToken);
        }

        private static decimal ToFunctionalAmount(
            decimal transactionAmount,
            string transactionCurrency,
            string functionalCurrency,
            decimal exchangeRate)
        {
            if (transactionAmount == 0m)
            {
                return 0m;
            }

            var normalizedRate = NormalizeExchangeRate(exchangeRate);
            return string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
                ? RoundMoney(transactionAmount)
                : RoundMoney(transactionAmount * normalizedRate);
        }

        private static decimal NormalizeExchangeRate(decimal exchangeRate)
            => exchangeRate <= 0m ? 1m : exchangeRate;

        /// <summary>
        /// Resolves the immutable tenant-approved daily mid-rate used by settlement. The fallback
        /// exists only for functional-currency and non-FX unit-test paths; a foreign allocation is
        /// never allowed to proceed without an approved rate-master record.
        /// </summary>
        private async Task<SettlementRateSnapshot> ResolveApprovedSettlementRateAsync(
            string transactionCurrency,
            string functionalCurrency,
            DateTime settlementDate,
            Guid? requestedRateId,
            decimal fallbackRate,
            bool requireApprovedSource,
            CancellationToken cancellationToken)
        {
            transactionCurrency = NormalizeCurrency(transactionCurrency, functionalCurrency);
            functionalCurrency = NormalizeCurrency(functionalCurrency, "GHS");
            if (string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                if (requestedRateId.HasValue)
                    throw new InvalidOperationException("A functional-currency AP settlement must not specify an exchange-rate id.");
                return new SettlementRateSnapshot(null, 1m);
            }

            if (_exchangeRateService == null)
            {
                if (requireApprovedSource)
                    throw new InvalidOperationException("Approved exchange-rate resolution is not configured for foreign-currency AP settlement.");
                return new SettlementRateSnapshot(null, NormalizeExchangeRate(fallbackRate));
            }

            var rate = requestedRateId.HasValue
                ? await _exchangeRateService.GetExchangeRateByIdAsync(requestedRateId.Value, cancellationToken)
                : await _exchangeRateService.GetCurrentRateAsync(
                    transactionCurrency,
                    functionalCurrency,
                    settlementDate,
                    "Daily",
                    "Mid",
                    cancellationToken);

            if (rate == null || !rate.IsActive || rate.Rate <= 0m ||
                !(rate.ApprovalStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
                  rate.ApprovalStatus.Equals("AutoApproved", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"No active approved daily mid-rate exists for {transactionCurrency} to {functionalCurrency} on {settlementDate:yyyy-MM-dd}.");

            if (!rate.BaseCurrencyCode.Equals(functionalCurrency, StringComparison.OrdinalIgnoreCase) ||
                !rate.TargetCurrencyCode.Equals(transactionCurrency, StringComparison.OrdinalIgnoreCase) ||
                !rate.RateType.Equals("Daily", StringComparison.OrdinalIgnoreCase) ||
                !rate.QuoteSide.Equals("Mid", StringComparison.OrdinalIgnoreCase) ||
                rate.EffectiveDate.Date > settlementDate.Date ||
                (rate.ExpiryDate.HasValue && rate.ExpiryDate.Value.Date < settlementDate.Date))
                throw new InvalidOperationException("The selected AP settlement rate does not match the required currency pair, date, daily rate type, or mid quote policy.");

            return new SettlementRateSnapshot(rate.Id, NormalizeExchangeRate(rate.Rate));
        }

        private static string NormalizeCurrency(string? currencyCode, string defaultValue)
            => string.IsNullOrWhiteSpace(currencyCode)
                ? defaultValue.Trim().ToUpperInvariant()
                : currencyCode.Trim().ToUpperInvariant();

        private sealed record SettlementRateSnapshot(Guid? Id, decimal Rate);

        private static decimal RoundMoney(decimal amount)
            => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        private static decimal RoundRate(decimal amount)
            => decimal.Round(amount, 6, MidpointRounding.AwayFromZero);

        private static VendorPaymentControlException AllocationAlreadyReversed(Guid allocationId) =>
            new(
                "AP_PAYMENT_ALLOCATION_ALREADY_REVERSED",
                $"Allocation '{allocationId}' already has an immutable reversal.");

        private static bool IsDuplicateAllocationReversal(DbUpdateException exception) =>
            exception.ToString().Contains(
                "UX_VendorPaymentAllocation_TenantId_OriginalAllocationId_Reversal",
                StringComparison.OrdinalIgnoreCase);

        private static void EnsureAllocationMutationAllowed(
            VendorPayment payment,
            bool batchProcessorScope)
        {
            if (payment.PaymentBatchId.HasValue)
            {
                if (!batchProcessorScope ||
                    payment.PaymentBatch?.Status != PaymentBatchStatus.Processing ||
                    payment.Status != VendorPaymentStatus.Authorized)
                {
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_BATCH_DIRECT_ALLOCATION_BLOCKED",
                        "Batch-owned payment allocations are immutable outside the approved batch processor.");
                }

                return;
            }

            if (payment.Status != VendorPaymentStatus.Draft)
            {
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_ALLOCATION_SET_FROZEN",
                    "A manual payment's invoice set cannot change after it is submitted for authorization.");
            }
        }

        private static List<VendorPaymentAllocation> GetEffectiveAllocations(
            IEnumerable<VendorPaymentAllocation>? allocations)
        {
            var live = allocations?.Where(item => !item.IsDeleted).ToList()
                       ?? new List<VendorPaymentAllocation>();
            var reversedOriginalIds = live
                .Where(item => item.IsReversal && item.OriginalAllocationId.HasValue)
                .Select(item => item.OriginalAllocationId!.Value)
                .ToHashSet();

            return live
                .Where(item => !item.IsReversal && !reversedOriginalIds.Contains(item.Id))
                .OrderBy(item => item.AllocationDate)
                .ThenBy(item => item.Id)
                .ToList();
        }

        internal static List<SupplierDebitNoteApplication> GetEffectiveSupplierDebitNoteApplications(
            IEnumerable<SupplierDebitNoteApplication>? applications)
        {
            var live = applications?.Where(item => !item.IsDeleted).ToList()
                       ?? new List<SupplierDebitNoteApplication>();
            var reversedOriginalIds = live
                .Where(item => item.IsReversal && item.OriginalApplicationId.HasValue)
                .Select(item => item.OriginalApplicationId!.Value)
                .ToHashSet();
            return live
                .Where(item => !item.IsReversal && !reversedOriginalIds.Contains(item.Id))
                .OrderBy(item => item.ApplicationDate)
                .ThenBy(item => item.Id)
                .ToList();
        }

        private static List<SupplierDebitNoteApplicationDto> GetEffectiveSupplierDebitNoteApplicationDtos(
            IEnumerable<SupplierDebitNoteApplicationDto>? applications)
        {
            var live = applications?.ToList() ?? new List<SupplierDebitNoteApplicationDto>();
            var reversedOriginalIds = live
                .Where(item => item.IsReversal && item.OriginalApplicationId.HasValue)
                .Select(item => item.OriginalApplicationId!.Value)
                .ToHashSet();
            return live.Where(item => !item.IsReversal && !reversedOriginalIds.Contains(item.Id)).ToList();
        }

        private async Task<Guid> ResolvePostedApControlAccountAsync(
            Guid journalEntryId,
            bool debitSide,
            CancellationToken cancellationToken)
        {
            var candidates = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.JournalEntryId == journalEntryId &&
                    !item.IsDeleted &&
                    (item.TransactionTag == "AP-Control" || item.TransactionTag == "AP-SupplierDebitNote-Control") &&
                    (debitSide ? item.DebitAmount > 0m : item.CreditAmount > 0m))
                .Select(item => item.AccountId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (candidates.Count != 1)
                throw new InvalidOperationException("Posted AP-control account lineage is missing or ambiguous.");
            return candidates[0];
        }

        private async Task EnsureSupplierCreditSourceJournalsActiveAsync(
            SupplierDebitNote note,
            VendorInvoice invoice,
            CancellationToken cancellationToken)
        {
            if (!note.JournalEntryId.HasValue || !invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("Supplier credit source journals are incomplete.");
            var journalIds = new[] { note.JournalEntryId.Value, invoice.JournalEntryId.Value }.Distinct().ToList();
            var journals = await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    journalIds.Contains(item.Id) &&
                    !item.IsDeleted)
                .Select(item => new { item.Id, item.IsReversed })
                .ToListAsync(cancellationToken);
            if (journals.Count != journalIds.Count || journals.Any(item => item.IsReversed))
                throw new InvalidOperationException(
                    "Supplier debit-note application references a missing or reversed note/invoice journal.");
        }

        private SupplierDebitNoteApplication CreateSupplierDebitNoteApplicationReversal(
            SupplierDebitNoteApplication original,
            DateTime reversalDate,
            string reason,
            Guid? paymentPostingEventId,
            Guid? paymentJournalEntryId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SupplierDebitNoteId = original.SupplierDebitNoteId,
            VendorPaymentId = original.VendorPaymentId,
            VendorInvoiceId = original.VendorInvoiceId,
            ApplicationAmount = -original.ApplicationAmount,
            FunctionalAmount = -original.FunctionalAmount,
            CurrencyCode = original.CurrencyCode,
            ExchangeRate = original.ExchangeRate,
            ApplicationDate = reversalDate,
            Notes = $"Reversal of supplier debit-note application {original.Id}: {reason}",
            IsReversal = true,
            OriginalApplicationId = original.Id,
            PaymentPostingEventId = paymentPostingEventId,
            PaymentJournalEntryId = paymentJournalEntryId,
            AppliedAt = paymentPostingEventId.HasValue ? reversalDate : null,
            CreatedAt = reversalDate,
            CreatedBy = UserName,
            CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
        };

        private static void EnsureAllocationTotalIsValid(
            VendorPayment payment,
            IReadOnlyCollection<VendorPaymentAllocation> effectiveAllocations)
        {
            if (payment.IsSupplierAdvance && payment.JournalEntryId.HasValue)
                return;

            var hasInitialAllocationHistory = payment.Allocations?.Any(item =>
                !item.IsDeleted && !item.IsReversal) == true;
            if (!hasInitialAllocationHistory)
                return;

            // Payment completeness is measured in payment currency. AllocatedAmount is an
            // invoice-currency value for cross-currency rows and cannot be compared to TotalAmount.
            var allocatedCashAmount = RoundMoney(effectiveAllocations.Sum(item =>
                item.PaymentCurrencyAmount > 0m ? item.PaymentCurrencyAmount : item.AllocatedAmount));
            if (allocatedCashAmount != RoundMoney(payment.TotalAmount))
            {
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_ALLOCATION_TOTAL_MISMATCH",
                    "AP payment amount must equal the effective allocated cash amount. Reversed allocations must be replaced before authorization or posting.");
            }
        }

        /// <summary>
        /// Reconciles every persisted allocation snapshot to its native amounts and frozen rates
        /// before a journal is constructed. New cross-currency rows must carry complete immutable
        /// evidence. The narrowly scoped same-currency fallback only preserves established draft
        /// workflows created before line-level functional columns were introduced; it never
        /// invents a foreign-currency conversion.
        /// </summary>
        private static void NormalizeAndValidateAllocationCurrencyEvidence(
            VendorPayment payment,
            IReadOnlyCollection<VendorPaymentAllocation> allocations,
            string functionalCurrency)
        {
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            foreach (var allocation in allocations)
            {
                var invoiceCurrency = NormalizeCurrency(
                    allocation.VendorInvoice?.CurrencyCode,
                    functionalCurrency);
                var storedInvoiceCurrency = NormalizeCurrency(allocation.InvoiceCurrencyCode, invoiceCurrency);
                var storedPaymentCurrency = NormalizeCurrency(allocation.PaymentCurrencyCode, paymentCurrency);
                if (!string.Equals(storedInvoiceCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(storedPaymentCurrency, paymentCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"AP allocation '{allocation.Id}' currency evidence does not match its payment and invoice.");
                }

                var paymentRequiresFx = !string.Equals(
                    paymentCurrency,
                    functionalCurrency,
                    StringComparison.OrdinalIgnoreCase);
                var invoiceRequiresFx = !string.Equals(
                    invoiceCurrency,
                    functionalCurrency,
                    StringComparison.OrdinalIgnoreCase);
                // A missing foreign rate cannot be normalized safely. Treating zero as 1.0 here
                // would manufacture accounting evidence and could post a balanced but incorrect
                // journal. This applies to both cross-currency settlements and same-foreign-
                // currency settlements because both still require functional measurement.
                if ((invoiceRequiresFx && allocation.InvoiceSettlementExchangeRate <= 0m) ||
                    (paymentRequiresFx &&
                     allocation.PaymentExchangeRate <= 0m &&
                     payment.ExchangeRate <= 0m))
                {
                    throw new InvalidOperationException(
                        $"AP allocation '{allocation.Id}' is missing its frozen foreign-currency exchange-rate evidence.");
                }

                var paymentCash = allocation.PaymentCurrencyAmount > 0m
                    ? allocation.PaymentCurrencyAmount
                    : allocation.AllocatedAmount;
                var expected = CrossCurrencySettlementCalculator.CalculateWithDeductions(
                    paymentCurrency,
                    invoiceCurrency,
                    paymentCash,
                    allocation.AllocatedAmount,
                    allocation.DiscountAmount,
                    allocation.WithholdingTaxAmount,
                    invoiceVatWithholdingAmount: 0m,
                    NormalizeExchangeRate(allocation.PaymentExchangeRate > 0m
                        ? allocation.PaymentExchangeRate
                        : payment.ExchangeRate),
                    NormalizeExchangeRate(allocation.InvoiceSettlementExchangeRate));

                // Same-currency draft allocations historically stored only native values. Fill
                // their deterministic 1:1/approved-rate evidence in the active transaction, but
                // fail closed when any cross-currency snapshot is absent or contradictory.
                var mayNormalizeSameCurrency = !expected.IsCrossCurrency;
                allocation.PaymentCurrencyAmount = NormalizeEvidenceAmount(
                    allocation.PaymentCurrencyAmount,
                    expected.PaymentCurrencyAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "payment-currency amount");
                allocation.PaymentFunctionalAmount = NormalizeEvidenceAmount(
                    allocation.PaymentFunctionalAmount,
                    expected.PaymentFunctionalAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "payment functional amount");
                allocation.DiscountFunctionalAmount = NormalizeEvidenceAmount(
                    allocation.DiscountFunctionalAmount,
                    expected.DiscountFunctionalAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "discount functional amount");
                allocation.WithholdingTaxFunctionalAmount = NormalizeEvidenceAmount(
                    allocation.WithholdingTaxFunctionalAmount,
                    expected.WithholdingFunctionalAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "WHT functional amount");
                allocation.SettlementFunctionalAmount = NormalizeEvidenceAmount(
                    allocation.SettlementFunctionalAmount,
                    expected.SettlementFunctionalAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "settlement functional amount");
                allocation.InvoiceCurrencyCode = expected.InvoiceCurrency;
                allocation.PaymentCurrencyCode = expected.PaymentCurrency;
                allocation.IsCrossCurrency = expected.IsCrossCurrency;
            }
        }

        private static decimal NormalizeEvidenceAmount(
            decimal stored,
            decimal expected,
            bool mayPopulateMissing,
            Guid allocationId,
            string evidenceName)
        {
            if (stored == 0m && expected != 0m && mayPopulateMissing)
                return expected;
            if (Math.Abs(RoundMoney(stored - expected)) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"AP allocation '{allocationId}' {evidenceName} does not reconcile to its frozen currency evidence.");
            }

            return stored;
        }

        /// <summary>
        /// Makes the allocation snapshots authoritative for the payment header and re-runs the
        /// configured WHT threshold calculation. Header values are reporting/policy roll-ups in
        /// functional currency; they are never a second write path for mixed native currencies.
        /// </summary>
        private async Task SynchronizeApWithholdingComplianceAsync(
            VendorPayment payment,
            IReadOnlyCollection<VendorPaymentAllocation> allocations,
            CancellationToken cancellationToken)
        {
            var functionalWht = RoundMoney(allocations.Sum(item => item.WithholdingTaxFunctionalAmount));
            var invoiceIds = allocations.Select(item => item.VendorInvoiceId).Distinct().ToList();
            var withholdingInvoices = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(invoice =>
                invoice.TenantId == TenantId && !invoice.IsDeleted && invoice.SupplierId == payment.SupplierId &&
                invoiceIds.Contains(invoice.Id)).ToListAsync(cancellationToken);
            if (withholdingInvoices.Count != invoiceIds.Count)
                throw new InvalidOperationException("A selected WHT invoice does not belong to this payment supplier and tenant.");
            var invoiceWithholding = ApInvoiceWithholdingPolicy.Resolve(withholdingInvoices, payment.WithholdingTaxId);
            if (invoiceWithholding != null)
                payment.WithholdingTaxId = invoiceWithholding.TaxId;
            payment.WithholdingTaxAmount = functionalWht;
            // A configured payment below its threshold must keep its taxable base: later
            // payments use that evidence when evaluating the supplier's cumulative threshold.
            if (!payment.WithholdingTaxId.HasValue && functionalWht == 0m)
            {
                payment.WithholdingTaxBaseAmount = 0m;
                payment.WithholdingTaxRate = 0m;
                payment.WithholdingTaxCumulativeBefore = 0m;
                payment.WithholdingTaxThresholdAmount = null;
                payment.WithholdingTaxThresholdApplied = false;
                payment.WithholdingTaxCalculationNote = null;
                payment.WithholdingTaxAccountId = null;
                return;
            }

            if (!payment.WithholdingTaxId.HasValue)
                throw new InvalidOperationException("AP WHT allocations require a configured WHT tax.");
            if (_withholdingTaxService == null)
                throw new InvalidOperationException("WHT compliance service is not configured.");

            var taxableBase = RoundMoney(allocations.Sum(item => item.SettlementFunctionalAmount));
            var calculation = await _withholdingTaxService.CalculateApWithholdingAsync(
                new WhtCalculationRequestDto
                {
                    TaxId = payment.WithholdingTaxId.Value,
                    SupplierId = payment.SupplierId,
                    PaymentDate = payment.PaymentDate,
                    TaxableBase = taxableBase,
                    // Allocation edits and posting revalidate an already persisted payment.
                    // Its current base is added by the calculator, not counted twice as history.
                    ExcludeVendorPaymentId = payment.Id,
                    VendorInvoiceIds = invoiceIds
                },
                cancellationToken);
            if (Math.Abs(RoundMoney(functionalWht - calculation.WithholdingAmount)) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"AP allocation WHT totals {functionalWht:N2}, but configured tax {calculation.TaxCode} requires {calculation.WithholdingAmount:N2}.");
            }

            payment.WithholdingTaxBaseAmount = calculation.TaxableBase;
            payment.WithholdingTaxRate = calculation.TaxRate;
            payment.WithholdingTaxCumulativeBefore = calculation.CumulativeBefore;
            payment.WithholdingTaxThresholdAmount = calculation.ThresholdAmount;
            payment.WithholdingTaxThresholdApplied = calculation.ThresholdApplied;
            payment.WithholdingTaxCalculationNote = calculation.CalculationNote;
            payment.WithholdingTaxAccountId = calculation.TaxPayableAccountId;
        }

        private async Task<Supplier> ResolveSupplierForPaymentAsync(Guid supplierOrBusinessPartnerId, CancellationToken cancellationToken)
        {
            var supplierRepository = _unitOfWork.Repository<Supplier>();
            Guid canonicalSupplierId;
            if (_apSupplierIdentityService != null)
            {
                // Payment entry is a Finance command, so it may create the durable exact-ID/code
                // bridge. It must never name-match or create a Procurement supplier master.
                canonicalSupplierId = (await _apSupplierIdentityService.ResolveAsync(
                    supplierOrBusinessPartnerId, cancellationToken)).SupplierId;
            }
            else
            {
                // Legacy test hosts may not register the bridge. Preserve only exact Supplier-ID
                // operation; Business Partner translation fails closed instead of guessing.
                canonicalSupplierId = supplierOrBusinessPartnerId;
            }

            var supplier = await supplierRepository
                .GetQueryable(s =>
                    s.TenantId == TenantId &&
                    !s.IsDeleted &&
                    s.IsActive &&
                    s.Id == canonicalSupplierId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "An active, unambiguous AP supplier identity is required before payment entry.");

            if (supplier.IsBlacklisted || string.Equals(supplier.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Supplier '{supplier.Name}' is not eligible for AP payment entry.");

            return supplier;
        }

        private async Task<Guid?> ResolveSupplierIdForQueryAsync(Guid supplierOrBusinessPartnerId, CancellationToken cancellationToken)
        {
            var supplierRepository = _unitOfWork.Repository<Supplier>();
            var supplierId = await supplierRepository
                .GetQueryable(s =>
                    s.TenantId == TenantId &&
                    !s.IsDeleted &&
                    s.Id == supplierOrBusinessPartnerId)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (supplierId.HasValue)
            {
                return supplierId;
            }

            if (_apSupplierIdentityService == null)
                return null;

            try
            {
                // Query translation is strictly side-effect free. Lookup may expose one exact
                // ID/code candidate but never persists a link, guesses by name, or creates a
                // Procurement master record.
                return (await _apSupplierIdentityService.LookupAsync(
                    supplierOrBusinessPartnerId, cancellationToken)).SupplierId;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private async Task CreateCashTransactionForPaymentAsync(VendorPayment payment, Supplier supplier, CancellationToken cancellationToken)
        {
            if (!payment.BankAccountId.HasValue)
            {
                return;
            }

            var cashTransactionRepository = _unitOfWork.Repository<CashTransaction>();
            var exists = await cashTransactionRepository
                .GetQueryable(t =>
                    !t.IsDeleted &&
                    t.TransactionType == CashTransactionType.Payment &&
                    t.BankAccountId == payment.BankAccountId.Value &&
                    t.ReferenceNumber == payment.PaymentNumber)
                .AnyAsync(cancellationToken);

            if (exists)
            {
                return;
            }

            var bankAccountRepository = _unitOfWork.Repository<BankAccount>();
            var bankAccount = await bankAccountRepository
                .GetQueryable(a => a.Id == payment.BankAccountId.Value && !a.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Bank account not found for AP vendor payment.");

            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var currencyCode = string.IsNullOrWhiteSpace(payment.CurrencyCode)
                ? baseCurrencyCode
                : payment.CurrencyCode.Trim().ToUpperInvariant();
            var exchangeRate = payment.ExchangeRate <= 0m ? 1m : payment.ExchangeRate;
            var baseAmount = decimal.Round(payment.TotalAmount * exchangeRate, 2, MidpointRounding.AwayFromZero);
            var apAccountId = supplier.DefaultApAccountId ?? await _unitOfWork.Repository<FinanceSettings>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .Select(s => s.ControlAccountApId)
                .FirstOrDefaultAsync(cancellationToken);

            var transactionNumber = await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.CashPayment,
                TenantId,
                payment.PaymentDate,
                nameof(CashTransaction),
                cancellationToken: cancellationToken);

            await cashTransactionRepository.AddAsync(new CashTransaction
            {
                Id = Guid.NewGuid(),
                TransactionNumber = transactionNumber,
                TransactionDate = payment.PaymentDate,
                TransactionType = CashTransactionType.Payment,
                BankAccountId = payment.BankAccountId.Value,
                Amount = payment.TotalAmount,
                Currency = currencyCode,
                ExchangeRate = exchangeRate,
                BaseAmount = baseAmount,
                PaymentMethodId = payment.PaymentMethodId,
                ReferenceNumber = payment.PaymentNumber,
                PayeeOrPayer = supplier.Name,
                Description = $"AP Vendor Payment {payment.PaymentNumber} - {supplier.Name}",
                GLAccountId = apAccountId,
                IsReconciled = false,
                IsPosted = true,
                PostedDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            });

            bankAccount.CurrentBalance -= payment.TotalAmount;
            bankAccount.AvailableBalance -= payment.TotalAmount;
            bankAccount.UpdatedAt = DateTime.UtcNow;
            bankAccount.UpdatedBy = UserName;
            await bankAccountRepository.UpdateAsync(bankAccount);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task<FinancePaymentMethod?> ResolveConfiguredPaymentMethodAsync(
            Guid? paymentMethodId,
            Guid? bankAccountId,
            string? referenceNumber,
            string label,
            bool enforceReference,
            CancellationToken cancellationToken)
        {
            if (!paymentMethodId.HasValue)
            {
                return null;
            }

            var method = await _unitOfWork.Repository<FinancePaymentMethod>()
                .GetQueryable(m => m.TenantId == TenantId && m.Id == paymentMethodId.Value && !m.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (method == null)
            {
                throw new InvalidOperationException($"The selected {label} payment method was not found for this tenant.");
            }

            if (!method.IsActive)
            {
                throw new InvalidOperationException($"The selected {label} payment method is inactive.");
            }

            if (method.RequiresBankAccount && !bankAccountId.HasValue)
            {
                throw new InvalidOperationException($"The selected {label} payment method requires a bank account.");
            }

            if (enforceReference && method.RequiresReference && string.IsNullOrWhiteSpace(referenceNumber))
            {
                throw new InvalidOperationException($"The selected {label} payment method requires a reference number.");
            }

            return method;
        }

        private static VendorPaymentMethod MapConfiguredPaymentMethodToVendorPaymentMethod(PaymentMethodType type)
        {
            return type switch
            {
                PaymentMethodType.Cash => VendorPaymentMethod.Cash,
                PaymentMethodType.Cheque => VendorPaymentMethod.Cheque,
                PaymentMethodType.MobileMoney => VendorPaymentMethod.MobileMoney,
                PaymentMethodType.DirectDebit => VendorPaymentMethod.DirectDebit,
                PaymentMethodType.EFT or PaymentMethodType.BankTransfer or PaymentMethodType.StandingOrder => VendorPaymentMethod.BankTransfer,
                _ => VendorPaymentMethod.Other
            };
        }

        private async Task<string> GeneratePaymentNumberAsync(CancellationToken cancellationToken)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.APPayment,
                TenantId,
                DateTime.UtcNow,
                nameof(VendorPayment),
                cancellationToken: cancellationToken);
        }

        private async Task<string> GenerateBatchNumberAsync(CancellationToken cancellationToken)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.APPaymentBatch,
                TenantId,
                DateTime.UtcNow,
                nameof(PaymentBatch),
                cancellationToken: cancellationToken);
        }

        private VendorPaymentDto MapToDto(VendorPayment payment)
        {
            return new VendorPaymentDto
            {
                Id = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                SupplierId = payment.SupplierId,
                SupplierName = payment.Supplier?.Name ?? string.Empty,
                PaymentDate = payment.PaymentDate,
                TotalAmount = payment.TotalAmount,
                AllocatedAmount = payment.AllocatedAmount,
                UnallocatedAmount = payment.UnallocatedAmount,
                PaymentMethod = payment.PaymentMethod,
                PaymentMethodId = payment.PaymentMethodId,
                PaymentMethodName = payment.ConfiguredPaymentMethod?.Name,
                CurrencyCode = payment.CurrencyCode,
                ExchangeRate = payment.ExchangeRate,
                ExchangeRateId = payment.ExchangeRateId,
                BankAccountId = payment.BankAccountId,
                BankAccountName = payment.BankAccount?.AccountName,
                ChequeNumber = payment.ChequeNumber,
                TransactionReference = payment.TransactionReference,
                WithholdingTaxRate = payment.WithholdingTaxRate,
                WithholdingTaxAmount = payment.WithholdingTaxAmount,
                WithholdingTaxBaseAmount = payment.WithholdingTaxBaseAmount,
                WithholdingTaxCumulativeBefore = payment.WithholdingTaxCumulativeBefore,
                WithholdingTaxThresholdAmount = payment.WithholdingTaxThresholdAmount,
                WithholdingTaxThresholdApplied = payment.WithholdingTaxThresholdApplied,
                WithholdingTaxCalculationNote = payment.WithholdingTaxCalculationNote,
                WithholdingTaxId = payment.WithholdingTaxId,
                WithholdingTaxAccountId = payment.WithholdingTaxAccountId,
                WithholdingCertificateNumber = payment.WithholdingCertificateNumber,
                WithholdingCertificateDate = payment.WithholdingCertificateDate,
                DiscountTaken = payment.DiscountTaken,
                Status = payment.Status,
                SubmittedById = payment.SubmittedById,
                ApprovalRequired = payment.ApprovalRequired,
                SubmittedAt = payment.SubmittedAt,
                WorkflowInstanceId = payment.WorkflowInstanceId,
                AppliedApprovalPolicySetId = payment.AppliedApprovalPolicySetId,
                AppliedApprovalPolicyCode = payment.AppliedApprovalPolicyCode,
                ApprovalControlSnapshotHash = payment.ApprovalControlSnapshotHash,
                IsExceptionalPayment = payment.IsExceptionalPayment,
                ExceptionalPaymentReason = payment.ExceptionalPaymentReason,
                RequiresManagingDirectorApproval = payment.RequiresManagingDirectorApproval,
                ManagingDirectorApprovedById = payment.ManagingDirectorApprovedById,
                ManagingDirectorApprovedAt = payment.ManagingDirectorApprovedAt,
                EvidenceExceptionRequested = payment.EvidenceExceptionRequested,
                EvidenceExceptionReason = payment.EvidenceExceptionReason,
                EvidenceExceptionRequestedById = payment.EvidenceExceptionRequestedById,
                EvidenceExceptionRequestedAt = payment.EvidenceExceptionRequestedAt,
                EvidenceExceptionApprovedById = payment.EvidenceExceptionApprovedById,
                EvidenceExceptionApprovedAt = payment.EvidenceExceptionApprovedAt,
                AuthorizedById = payment.AuthorizedById,
                AuthorizedDate = payment.AuthorizedDate,
                InvoicePaymentSodControlEventId = payment.InvoicePaymentSodControlEventId,
                PaymentBatchId = payment.PaymentBatchId,
                PaymentBatchNumber = payment.PaymentBatch?.BatchNumber,
                JournalEntryId = payment.JournalEntryId,
                ReversalJournalEntryId = payment.ReversalJournalEntryId,
                ReversalPostingEventId = payment.ReversalPostingEventId,
                ReversalDate = payment.ReversalDate,
                ReversedAt = payment.ReversedAt,
                ReversedById = payment.ReversedById,
                ReversalReason = payment.ReversalReason,
                Notes = payment.Notes,
                CreatedAt = payment.CreatedAt,
                Allocations = payment.Allocations?.Select(a => new VendorPaymentAllocationDto
                {
                    Id = a.Id,
                    VendorPaymentId = a.VendorPaymentId,
                    VendorInvoiceId = a.VendorInvoiceId,
                    InvoiceNumber = a.VendorInvoice?.InvoiceNumber ?? string.Empty,
                    AllocatedAmount = a.AllocatedAmount,
                    PaymentCurrencyAmount = a.PaymentCurrencyAmount,
                    InvoiceCurrencyCode = a.InvoiceCurrencyCode,
                    PaymentCurrencyCode = a.PaymentCurrencyCode,
                    IsCrossCurrency = a.IsCrossCurrency,
                    InvoiceSettlementExchangeRateId = a.InvoiceSettlementExchangeRateId,
                    InvoiceSettlementExchangeRate = a.InvoiceSettlementExchangeRate,
                    PaymentExchangeRateId = a.PaymentExchangeRateId,
                    PaymentExchangeRate = a.PaymentExchangeRate,
                    PaymentFunctionalAmount = a.PaymentFunctionalAmount,
                    SettlementFunctionalAmount = a.SettlementFunctionalAmount,
                    DiscountAmount = a.DiscountAmount,
                    DiscountFunctionalAmount = a.DiscountFunctionalAmount,
                    WithholdingTaxAmount = a.WithholdingTaxAmount,
                    WithholdingTaxFunctionalAmount = a.WithholdingTaxFunctionalAmount,
                    AllocationDate = a.AllocationDate,
                    Notes = a.Notes,
                    IsReversal = a.IsReversal,
                    PaymentReadinessControlEventId = a.PaymentReadinessControlEventId,
                    PaymentReadinessSnapshotHash = a.PaymentReadinessSnapshotHash,
                    PaymentReadinessEvaluatedAtUtc = a.PaymentReadinessEvaluatedAtUtc
                }).ToList() ?? new List<VendorPaymentAllocationDto>(),
                SupplierDebitNoteApplications = payment.SupplierDebitNoteApplications?
                    .Where(item => !item.IsDeleted)
                    .Select(MapSupplierDebitNoteApplication)
                    .ToList() ?? new List<SupplierDebitNoteApplicationDto>()
            };
        }

        private static SupplierDebitNoteApplicationDto MapSupplierDebitNoteApplication(
            SupplierDebitNoteApplication application) => new()
        {
            Id = application.Id,
            SupplierDebitNoteId = application.SupplierDebitNoteId,
            DebitNoteNumber = application.SupplierDebitNote?.DebitNoteNumber ?? string.Empty,
            SupplierCreditNoteReference = application.SupplierDebitNote?.SupplierCreditNoteReference,
            VendorPaymentId = application.VendorPaymentId,
            PaymentNumber = application.VendorPayment?.PaymentNumber ?? string.Empty,
            VendorInvoiceId = application.VendorInvoiceId,
            InvoiceNumber = application.VendorInvoice?.InvoiceNumber ?? string.Empty,
            ApplicationAmount = application.ApplicationAmount,
            FunctionalAmount = application.FunctionalAmount,
            CurrencyCode = application.CurrencyCode,
            ExchangeRate = application.ExchangeRate,
            ApplicationDate = application.ApplicationDate,
            Notes = application.Notes,
            IsReversal = application.IsReversal,
            OriginalApplicationId = application.OriginalApplicationId,
            PaymentPostingEventId = application.PaymentPostingEventId,
            PaymentJournalEntryId = application.PaymentJournalEntryId,
            AppliedAt = application.AppliedAt,
            CreatedAt = application.CreatedAt,
            CreatedBy = application.CreatedBy
        };

        private static FinancePostingTraceDto MapPostingTrace(FinancePostingEvent postingEvent)
        {
            var journal = postingEvent.JournalEntry;
            return new FinancePostingTraceDto
            {
                PostingEventId = postingEvent.Id,
                PostingAction = postingEvent.PostingAction,
                PostingStatus = postingEvent.PostingStatus,
                PostingDate = postingEvent.PostingDate,
                PostedAt = postingEvent.PostedAt,
                JournalEntryId = postingEvent.JournalEntryId,
                JournalEntryNumber = journal?.JournalEntryNumber,
                OriginalJournalEntryId = journal?.OriginalJournalEntryId,
                ReversalJournalEntryId = journal?.ReversalJournalEntryId,
                TotalDebitAmount = postingEvent.TotalDebitAmount,
                TotalCreditAmount = postingEvent.TotalCreditAmount,
                FunctionalCurrencyCode = postingEvent.FunctionalCurrencyCode,
                Lines = journal?.Transactions
                    .OrderBy(item => item.LineNumber)
                    .Select(item => new FinanceJournalLineTraceDto
                    {
                        TransactionId = item.Id,
                        LineNumber = item.LineNumber,
                        AccountId = item.AccountId,
                        AccountNumber = item.Account?.AccountNumber ?? string.Empty,
                        AccountName = item.Account?.AccountName ?? string.Empty,
                        Description = item.Description ?? string.Empty,
                        DebitAmount = item.DebitAmount,
                        CreditAmount = item.CreditAmount,
                        // Older journal-line producers may omit the line currency. The posting event
                        // is the authoritative functional-currency context for the trace fallback.
                        TransactionCurrency = item.TransactionCurrency ?? postingEvent.FunctionalCurrencyCode,
                        ForeignCurrencyAmount = item.ForeignCurrencyAmount,
                        ExchangeRate = item.ExchangeRate,
                        OriginalTransactionId = item.OriginalTransactionId,
                        ReversalTransactionId = item.ReversalTransactionId
                    })
                    .ToList() ?? new List<FinanceJournalLineTraceDto>()
            };
        }

        private sealed record PendingAllocationReversal(
            VendorPaymentAllocation Allocation,
            VendorInvoice Invoice,
            FinancePostingResultDto? ApplicationReversal);

        private PaymentBatchDto MapBatchToDto(PaymentBatch batch)
        {
            return new PaymentBatchDto
            {
                Id = batch.Id,
                BatchNumber = batch.BatchNumber,
                Description = batch.Description,
                BatchDate = batch.BatchDate,
                DueDateFrom = batch.DueDateFrom,
                DueDateTo = batch.DueDateTo,
                TotalAmount = batch.TotalAmount,
                PaymentCount = batch.PaymentCount,
                PaymentMethod = batch.PaymentMethod,
                PaymentMethodId = batch.PaymentMethodId,
                PaymentMethodName = batch.ConfiguredPaymentMethod?.Name,
                BankAccountId = batch.BankAccountId,
                BankAccountName = batch.BankAccount?.AccountName,
                Status = batch.Status,
                ApprovalRequired = batch.ApprovalRequired,
                CreatedById = batch.CreatedById,
                ApprovedById = batch.ApprovedById,
                ApprovedDate = batch.ApprovedDate,
                InvoicePaymentSodControlEventId = batch.InvoicePaymentSodControlEventId,
                ProcessedById = batch.ProcessedById,
                ProcessedDate = batch.ProcessedDate,
                Notes = batch.Notes,
                CreatedAt = batch.CreatedAt,
                Items = batch.Items?.Select(i => new PaymentBatchItemDto
                {
                    Id = i.Id,
                    VendorPaymentId = i.VendorPaymentId,
                    PaymentNumber = i.VendorPayment?.PaymentNumber ?? string.Empty,
                    SupplierName = i.VendorPayment?.Supplier?.Name ?? string.Empty,
                    Amount = i.Amount,
                    ItemStatus = i.ItemStatus,
                    FailureReason = i.FailureReason,
                    Invoices = i.Invoices?.OrderBy(selection => selection.VendorInvoice?.DueDate)
                        .ThenBy(selection => selection.VendorInvoiceId)
                        .Select(selection => new PaymentBatchInvoiceDto
                        {
                            Id = selection.Id,
                            VendorInvoiceId = selection.VendorInvoiceId,
                            InvoiceNumber = selection.VendorInvoice?.InvoiceNumber ?? string.Empty,
                            Amount = selection.Amount,
                            Status = selection.Status,
                            FailureReason = selection.FailureReason,
                            PaymentReadinessControlEventId = selection.PaymentReadinessControlEventId,
                            PaymentReadinessSnapshotHash = selection.PaymentReadinessSnapshotHash,
                            PaymentReadinessEvaluatedAtUtc = selection.PaymentReadinessEvaluatedAtUtc
                        }).ToList() ?? new List<PaymentBatchInvoiceDto>()
                }).ToList() ?? new List<PaymentBatchItemDto>()
            };
        }
    }
}
