using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Services.Finance.AR
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<PaymentService> _logger;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IFinanceAccessScopeService _financeAccessScopeService;
        private readonly IFinanceReversalPolicyService _financeReversalPolicyService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IFxAccountingService? _fxAccountingService;
        private readonly IFinanceControlledDocumentIssueService? _controlledDocumentIssueService;
        private readonly IExchangeRateService? _exchangeRateService;
        private ExchangeRateQuoteSide? _settlementQuoteSide;

        public PaymentService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ILogger<PaymentService> logger,
            IDocumentNumberingService documentNumberingService,
            IFinanceAccessScopeService financeAccessScopeService,
            IFinanceReversalPolicyService financeReversalPolicyService,
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            IFxAccountingService? fxAccountingService = null,
            IFinanceControlledDocumentIssueService? controlledDocumentIssueService = null,
            IExchangeRateService? exchangeRateService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _financeAccessScopeService = financeAccessScopeService;
            _financeReversalPolicyService = financeReversalPolicyService;
            _financePostingEngine = financePostingEngine;
            _financeAuditService = financeAuditService;
            _fxAccountingService = fxAccountingService;
            _controlledDocumentIssueService = controlledDocumentIssueService;
            _exchangeRateService = exchangeRateService;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";
        private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

        public async Task<CustomerPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var permittedBankAccountIds = await _financeAccessScopeService
                .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read, cancellationToken);
            var query = _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id);
            if (permittedBankAccountIds != null)
            {
                // Non-bank receipts live in controlled liquidity accounts rather than a bank.
                // Until liquidity accounts become an assignable scope dimension, only a tenant-wide
                // grant may expose them; this deliberately fails closed for bank-restricted users.
                query = query.Where(payment =>
                    payment.BankAccountId.HasValue &&
                    permittedBankAccountIds.Contains(payment.BankAccountId.Value));
            }

            var payment = await query
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.Invoice)
                .Include(p => p.ConfiguredPaymentMethod)
                .Include(p => p.BankAccount)
                .Include(p => p.LiquidityAccount)
                .FirstOrDefaultAsync(cancellationToken);

            var customer = payment == null
                ? null
                : await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);

            if (payment == null)
                return null;

            var result = MapToDto(payment, customer);
            if (_controlledDocumentIssueService != null && !payment.IsCreditNote)
            {
                // Receipt issue state is read from the common append-only output register. The
                // source AR payment remains free of duplicate print counters or copy-status fields.
                result.ReceiptIssuance = await _controlledDocumentIssueService.GetSummaryAsync(
                    DocumentTypes.FinanceArCustomerReceipt,
                    payment.Id,
                    cancellationToken);
            }

            return result;
        }

        public async Task<CustomerPaymentTraceDto?> GetTraceAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                .Include(item => item.Allocations)
                    .ThenInclude(item => item.Invoice)
                .Include(item => item.BankAccount)
                .Include(item => item.LiquidityAccount)
                .Include(item => item.ConfiguredPaymentMethod)
                .FirstOrDefaultAsync(cancellationToken);
            if (payment == null)
                return null;

            // Trace visibility follows the same Finance data scope as the source receipt. Passing
            // null for a liquidity-held receipt intentionally requires tenant-wide scope because
            // liquidity-account grants are not yet an assignable Finance dimension.
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                payment.BankAccountId,
                FinanceAccessLevel.Read,
                cancellationToken);

            var customer = await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);
            var allocationIds = payment.Allocations.Select(item => item.Id).ToList();
            var postingEvents = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    !item.IsDeleted &&
                    ((item.SourceDocumentType == "CustomerPayment" && item.SourceDocumentId == payment.Id) ||
                     (allocationIds.Contains(item.SourceDocumentId) &&
                      (item.SourceDocumentType == "PaymentAllocation" ||
                       item.SourceDocumentType == "CustomerPaymentAdvanceApplication"))))
                .Include(item => item.JournalEntry)
                    .ThenInclude(item => item!.Transactions)
                        .ThenInclude(item => item.Account)
                .OrderBy(item => item.PostingDate)
                .ThenBy(item => item.RequestedAt)
                .ToListAsync(cancellationToken);

            var cashTransactions = await _unitOfWork.Repository<CashTransaction>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    !item.IsDeleted &&
                    ((item.TransactionType == CashTransactionType.Receipt &&
                      item.ReferenceNumber == payment.PaymentNumber) ||
                     (payment.ReversalCashTransactionId.HasValue &&
                      item.Id == payment.ReversalCashTransactionId.Value)))
                .AsNoTracking()
                .OrderBy(item => item.TransactionDate)
                .ToListAsync(cancellationToken);
            var liquidityEntries = await _unitOfWork.Repository<LiquidityAccountEntry>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    !item.IsDeleted &&
                    ((item.SourceDocumentType == nameof(CustomerPayment) &&
                      item.SourceDocumentId == payment.Id) ||
                     (payment.ReversalLiquidityAccountEntryId.HasValue &&
                      item.Id == payment.ReversalLiquidityAccountEntryId.Value)))
                .AsNoTracking()
                .OrderBy(item => item.EntryDate)
                .ToListAsync(cancellationToken);
            var auditEvents = await _unitOfWork.Repository<AuditLog>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.Resource == "Finance.ARReceipt" &&
                    item.ResourceId == payment.Id.ToString())
                .AsNoTracking()
                .OrderBy(item => item.Timestamp)
                .ToListAsync(cancellationToken);

            var trace = new CustomerPaymentTraceDto
            {
                Payment = MapToDto(payment, customer),
                Postings = postingEvents.Select(MapPostingTrace).ToList(),
                OperationalEntries = cashTransactions.Select(item => new FinanceOperationalTraceDto
                {
                    RecordType = nameof(CashTransaction),
                    RecordId = item.Id,
                    Reference = item.TransactionNumber,
                    Status = item.ApprovalStatus.ToString(),
                    RecordDate = item.TransactionDate,
                    Amount = item.Amount,
                    CurrencyCode = item.Currency,
                    IsReconciled = item.IsReconciled
                }).Concat(liquidityEntries.Select(item => new FinanceOperationalTraceDto
                {
                    RecordType = nameof(LiquidityAccountEntry),
                    RecordId = item.Id,
                    OriginalRecordId = item.ReversalOfEntryId,
                    Reference = item.EntryNumber,
                    Status = item.IsReversed ? "Reversed" : item.Direction.ToString(),
                    RecordDate = item.EntryDate,
                    Amount = item.Amount,
                    CurrencyCode = item.Currency,
                    IsReconciled = item.AllocatedAmount > 0m
                })).ToList(),
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

            await RecordArReceiptAuditAsync(
                FinanceAuditEvents.ArReceiptTraceViewed,
                payment,
                afterValues: new
                {
                    PostingCount = trace.Postings.Count,
                    OperationalEntryCount = trace.OperationalEntries.Count,
                    AuditEventCount = trace.AuditEvents.Count
                },
                comment: "AR receipt source-to-ledger trace viewed.",
                cancellationToken: cancellationToken);

            return trace;
        }

        public async Task<CustomerPaymentDto?> GetByPaymentNumberAsync(string paymentNumber, CancellationToken cancellationToken = default)
        {
            var permittedBankAccountIds = await _financeAccessScopeService
                .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read, cancellationToken);
            var query = _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.PaymentNumber == paymentNumber);
            if (permittedBankAccountIds != null)
            {
                query = query.Where(payment =>
                    payment.BankAccountId.HasValue &&
                    permittedBankAccountIds.Contains(payment.BankAccountId.Value));
            }

            var payment = await query
                .Include(p => p.Allocations)
                .Include(p => p.ConfiguredPaymentMethod)
                .FirstOrDefaultAsync(cancellationToken);

            var customer = payment == null
                ? null
                : await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);

            return payment == null ? null : MapToDto(payment, customer);
        }

        public async Task<PagedResult<CustomerPaymentDto>> GetAllAsync(PaymentQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId);

            var permittedBankAccountIds = await _financeAccessScopeService
                .GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read, cancellationToken);
            if (permittedBankAccountIds != null)
            {
                queryable = queryable.Where(payment =>
                    payment.BankAccountId.HasValue &&
                    permittedBankAccountIds.Contains(payment.BankAccountId.Value));
            }

            // Apply filters
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                queryable = queryable.Where(p =>
                    p.PaymentNumber.Contains(query.SearchTerm) ||
                    (p.TransactionReference != null && p.TransactionReference.Contains(query.SearchTerm)));
            }

            if (query.CustomerId.HasValue)
                queryable = queryable.Where(p => p.CustomerId == query.CustomerId.Value);

            if (!string.IsNullOrWhiteSpace(query.PaymentMethod))
                queryable = queryable.Where(p => p.PaymentMethod == query.PaymentMethod);

            if (query.PaymentMethodId.HasValue)
                queryable = queryable.Where(p => p.PaymentMethodId == query.PaymentMethodId.Value);

            if (!string.IsNullOrWhiteSpace(query.Status))
                queryable = queryable.Where(p => p.Status == query.Status);

            if (query.FromDate.HasValue)
                queryable = queryable.Where(p => p.PaymentDate >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                queryable = queryable.Where(p => p.PaymentDate <= query.ToDate.Value);

            if (query.HasUnallocatedAmount.HasValue && query.HasUnallocatedAmount.Value)
                queryable = queryable.Where(p => p.UnallocatedAmount > 0);

            // Get total count
            var totalCount = await queryable.CountAsync(cancellationToken);

            // Apply sorting
            queryable = query.SortBy?.ToLower() switch
            {
                "paymentnumber" => query.SortDescending
                    ? queryable.OrderByDescending(p => p.PaymentNumber)
                    : queryable.OrderBy(p => p.PaymentNumber),
                "amount" => query.SortDescending
                    ? queryable.OrderByDescending(p => p.TotalAmount)
                    : queryable.OrderBy(p => p.TotalAmount),
                _ => queryable.OrderByDescending(p => p.PaymentDate)
            };

            // Apply pagination
            var payments = await queryable
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Include(p => p.ConfiguredPaymentMethod)
                .Include(p => p.BankAccount)
                .Include(p => p.LiquidityAccount)
                .ToListAsync(cancellationToken);
            var customerIds = payments.Select(p => p.CustomerId).Distinct().ToList();
            var customerMap = customerIds.Count == 0
                ? new Dictionary<Guid, BusinessPartner>()
                : await _unitOfWork.Repository<BusinessPartner>()
                    .GetQueryable(p => p.TenantId == TenantId && !p.IsDeleted && customerIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

            return new PagedResult<CustomerPaymentDto>
            {
                Items = payments
                    .Select(payment => MapToDto(
                        payment,
                        customerMap.TryGetValue(payment.CustomerId, out var customer) ? customer : null))
                    .ToList(),
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        public Task<CustomerPaymentDto> CreateAsync(
            PaymentCreateDto dto,
            CancellationToken cancellationToken = default) =>
            CreateAsync(dto, cancellationToken, executionStrategyScope: false);

        private async Task<CustomerPaymentDto> CreateAsync(
            PaymentCreateDto dto,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => CreateAsync(dto, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            if (dto.IsCreditNote)
            {
                // FIN-LIM-0013: CustomerPayment.IsCreditNote is retained only so historical rows
                // remain readable. New customer credits must use the primary Sales CreditNote
                // workflow, which owns approval, posting, application, and immutable correction.
                throw new InvalidOperationException(
                    "The legacy AR payment credit-note path is retired. Create the credit through the Sales credit-note workflow.");
            }

            CustomerPayment? payment = null;
            BusinessPartner? customer = null;
            var transactionStarted = false;

            try
            {
                // Receipt creation is a single accounting command: source row, allocations,
                // operational snapshots and posting-engine output must commit or roll back together.
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                transactionStarted = true;

                customer = await GetCustomerPartnerAsync(dto.CustomerId, cancellationToken);
                if (customer == null)
                    throw new KeyNotFoundException($"Customer with Id '{dto.CustomerId}' not found.");

                var paymentNumber = await GeneratePaymentNumberAsync(dto.IsCreditNote, cancellationToken);
                var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
                var paymentCurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode)
                    ? baseCurrencyCode
                    : dto.CurrencyCode.Trim().ToUpperInvariant();
                // Freeze the tenant-approved receipt rate before allocating invoices. This keeps
                // every downstream posting and reversal tied to the exact rate evidence reviewed
                // by Finance rather than a value looked up after approval.
                var paymentRate = await ResolveApprovedSettlementRateAsync(
                    paymentCurrencyCode,
                    baseCurrencyCode,
                    dto.PaymentDate,
                    dto.ExchangeRateId,
                    dto.ExchangeRate,
                    // An unallocated foreign receipt becomes a customer-advance currency lot.
                    // Its historical carrying value must come from the same approved rate master
                    // used by immediately allocated receipts because later applications realize FX.
                    requireApprovedSource: dto.Allocations?.Any() == true ||
                        !string.Equals(paymentCurrencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase),
                    cancellationToken);
                var configuredPaymentMethod = dto.IsCreditNote
                    ? null
                    : await ResolveConfiguredPaymentMethodAsync(
                        dto.PaymentMethodId,
                        dto.BankAccountId,
                        dto.TransactionReference ?? dto.CheckNumber,
                        "customer payment",
                        enforceReference: true,
                        cancellationToken);
                var paymentMethod = configuredPaymentMethod == null
                    ? dto.PaymentMethod
                    : MapConfiguredPaymentMethodToCustomerPaymentMethod(configuredPaymentMethod.Type);
                var receiptDestination = dto.IsCreditNote
                    ? new ReceiptDestination(null, null)
                    : await ResolveReceiptDestinationAsync(
                        configuredPaymentMethod?.Type,
                        paymentMethod,
                        dto.BankAccountId,
                        dto.LiquidityAccountId,
                        paymentCurrencyCode,
                        cancellationToken);
                await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                    receiptDestination.BankAccountId,
                    FinanceAccessLevel.Operate,
                    cancellationToken);

                var hasLineWithholding = dto.Allocations?.Any(allocation => allocation.WithholdingTaxAmount > 0m) == true;
                var hasLineVatWithholding = dto.Allocations?.Any(allocation => allocation.VatWithholdingAmount > 0m) == true;
                if (dto.WithholdingTaxAmount < 0m || dto.VatWithholdingAmount < 0m ||
                    dto.Allocations?.Any(allocation =>
                        allocation.WithholdingTaxAmount < 0m || allocation.VatWithholdingAmount < 0m) == true)
                {
                    throw new InvalidOperationException("AR receipt withholding amounts cannot be negative.");
                }
                if ((hasLineWithholding || hasLineVatWithholding)
                    && string.IsNullOrWhiteSpace(dto.WithholdingCertificateNumber))
                {
                    throw new InvalidOperationException("Customer withholding certificate/reference number is required when WHT or VAT withholding is recorded.");
                }
                if ((dto.WithholdingTaxAmount > 0m || dto.VatWithholdingAmount > 0m) &&
                    !(hasLineWithholding || hasLineVatWithholding))
                {
                    // Header-only deductions cannot identify which invoice currency and rate
                    // reduced the receivable. New writes must provide the statutory amount on
                    // each allocation; the header is now a functional-currency roll-up.
                    throw new InvalidOperationException(
                        "Allocate AR WHT and VAT withholding to individual invoices; receipt-header deduction amounts are calculated by Finance.");
                }

                // Resolve the account exclusively from the selected, effective-dated sales tax.
                // This prevents a browser from redirecting statutory receivables to an unrelated
                // same-tenant account while retaining the existing posting engine as authority.
                Guid? withholdingReceivableAccountId = hasLineWithholding
                    ? await ResolveConfiguredWithholdingReceivableAccountAsync(
                        dto.PaymentDate,
                        dto.WithholdingTaxId ?? throw new InvalidOperationException("Configured WHT receivable tax is required."),
                        TaxCategory.Withholding,
                        cancellationToken)
                        ?? throw new InvalidOperationException("The selected WHT tax has no effective receivable account configured.")
                    : null;
                Guid? vatWithholdingReceivableAccountId = hasLineVatWithholding
                    ? await ResolveConfiguredWithholdingReceivableAccountAsync(
                        dto.PaymentDate,
                        dto.VatWithholdingTaxId ?? throw new InvalidOperationException("Configured VAT withholding receivable tax is required."),
                        TaxCategory.VatWithholding,
                        cancellationToken)
                        ?? throw new InvalidOperationException("The selected VAT withholding tax has no effective receivable account configured.")
                    : null;

                var now = DateTime.UtcNow;
                payment = new CustomerPayment
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PaymentNumber = paymentNumber,
                    CustomerId = dto.CustomerId,
                    PaymentDate = dto.PaymentDate,
                    TotalAmount = dto.TotalAmount,
                    AllocatedAmount = 0,
                    PaymentMethod = paymentMethod,
                    PaymentMethodId = configuredPaymentMethod?.Id,
                    CurrencyCode = paymentCurrencyCode,
                    ExchangeRate = paymentRate.Rate,
                    ExchangeRateId = paymentRate.Id,
                    BankAccountId = receiptDestination.BankAccountId,
                    LiquidityAccountId = receiptDestination.LiquidityAccountId,
                    CheckNumber = dto.CheckNumber,
                    ChequeDrawerBank = dto.ChequeDrawerBank,
                    TransactionReference = dto.TransactionReference,
                    WithholdingTaxId = hasLineWithholding ? dto.WithholdingTaxId : null,
                    WithholdingTaxAccountId = withholdingReceivableAccountId,
                    WithholdingTaxAmount = 0m,
                    VatWithholdingTaxId = hasLineVatWithholding ? dto.VatWithholdingTaxId : null,
                    VatWithholdingAccountId = vatWithholdingReceivableAccountId,
                    VatWithholdingAmount = 0m,
                    WithholdingCertificateNumber = dto.WithholdingCertificateNumber,
                    WithholdingCertificateDate = dto.WithholdingCertificateDate,
                    Notes = dto.Notes,
                    Status = "Pending",
                    IsCreditNote = dto.IsCreditNote,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                // Keep the customer partner audit trail in sync with payment activity.
                customer.UpdatedAt = now;
                customer.UpdatedBy = UserName;
                await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customer);

                await _unitOfWork.Repository<CustomerPayment>().AddAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (dto.Allocations?.Any() == true && !dto.IsCreditNote)
                {
                    var allocationResult = await AllocatePaymentCoreAsync(
                        payment.Id,
                        dto.Allocations,
                        postDiscountAdjustmentsForPostedPayment: false,
                        cancellationToken);

                    if (!allocationResult.Success || allocationResult.Allocations.Count != dto.Allocations.Count)
                    {
                        throw new InvalidOperationException(
                            allocationResult.Message ?? "The customer receipt allocations could not be applied completely.");
                    }

                    // Receipt header amounts are functional-currency statutory roll-ups. The
                    // native evidence remains line-scoped because a receipt may settle USD and
                    // EUR invoices together while the GRA/control accounts report in GHS.
                    payment.WithholdingTaxAmount = RoundMoney(payment.Allocations
                        .Where(allocation => !allocation.IsReversal)
                        .Sum(allocation => allocation.WithholdingTaxFunctionalAmount));
                    payment.VatWithholdingAmount = RoundMoney(payment.Allocations
                        .Where(allocation => !allocation.IsReversal)
                        .Sum(allocation => allocation.VatWithholdingFunctionalAmount));
                }

                if (dto.IsCreditNote)
                {
                    await PostCustomerCreditNoteAsync(payment.Id, cancellationToken);
                }
                else
                {
                    if (_financePostingEngine == null)
                        throw new InvalidOperationException("Central finance posting engine is not configured for AR receipt posting.");

                    payment = await LoadPaymentForPostingAsync(payment.Id, cancellationToken);
                    var postingOutcome = await PostArReceiptCoreAsync(payment, cancellationToken);
                    await FinalizeArReceiptPostingAsync(postingOutcome, cancellationToken);
                }

                // UnitOfWork.CommitAsync owns rollback/disposal if the commit itself fails.
                // Clear this guard first so the outer catch does not mask that error with a second rollback.
                transactionStarted = false;
                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("Created payment {PaymentNumber} for customer {CustomerId}, Amount: {Amount}",
                    payment.PaymentNumber, customer.Id, dto.TotalAmount);

                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment, customer);
            }
            catch (Exception ex)
            {
                if (ex is DbUpdateConcurrencyException concurrencyException)
                {
                    var staleEntities = concurrencyException.Entries
                        .Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.Property("Id").CurrentValue}")
                        .ToArray();
                    _logger.LogError(
                        concurrencyException,
                        "AR receipt creation hit optimistic concurrency for {Entities}",
                        string.Join(", ", staleEntities));
                }

                if (transactionStarted)
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                }

                throw;
            }
        }

        public async Task<CustomerPaymentDto> UpdateAsync(PaymentUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == dto.Id)
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{dto.Id}' not found.");

            EnsureCompatibilityCreditNoteIsReadOnly(payment);

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                payment.BankAccountId,
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (payment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted customer payments cannot be updated. Use a reversal, void, or adjustment workflow.");

            // Only allow updates if status is Pending
            if (payment.Status != "Pending")
                throw new InvalidOperationException("Only pending payments can be updated.");

            var configuredPaymentMethod = payment.IsCreditNote
                ? null
                : await ResolveConfiguredPaymentMethodAsync(
                    dto.PaymentMethodId ?? payment.PaymentMethodId,
                    dto.BankAccountId,
                    dto.TransactionReference ?? dto.CheckNumber,
                    "customer payment",
                    enforceReference: true,
                    cancellationToken);
            var paymentMethod = configuredPaymentMethod == null
                ? dto.PaymentMethod
                : MapConfiguredPaymentMethodToCustomerPaymentMethod(configuredPaymentMethod.Type);
            var receiptDestination = await ResolveReceiptDestinationAsync(
                configuredPaymentMethod?.Type,
                paymentMethod,
                dto.BankAccountId,
                dto.LiquidityAccountId,
                payment.CurrencyCode,
                cancellationToken);
            // Re-check the resolved destination because an update may move a pending receipt to a
            // different bank account than the one against which the command was initially loaded.
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                receiptDestination.BankAccountId,
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (dto.WithholdingTaxAmount != 0m || dto.VatWithholdingAmount != 0m)
            {
                // Deductions are invoice-currency facts. Accepting a header amount here would
                // discard the invoice/rate evidence required for cross-currency posting.
                throw new InvalidOperationException(
                    "AR receipt WHT and VAT-WHT must be recorded against invoice allocations, not on the receipt header.");
            }
            var activeAllocations = payment.Allocations.Where(allocation => !allocation.IsDeleted).ToList();
            var withholdingFunctionalAmount = RoundMoney(activeAllocations.Sum(allocation => allocation.WithholdingTaxFunctionalAmount));
            var vatWithholdingFunctionalAmount = RoundMoney(activeAllocations.Sum(allocation => allocation.VatWithholdingFunctionalAmount));
            if ((withholdingFunctionalAmount > 0m || vatWithholdingFunctionalAmount > 0m)
                && string.IsNullOrWhiteSpace(dto.WithholdingCertificateNumber))
            {
                throw new InvalidOperationException("Customer withholding certificate/reference number is required when WHT or VAT withholding is recorded.");
            }

            // Pending-receipt edits have the same configuration boundary as creation. Never let
            // an update reintroduce client-selected GL accounts after the create path was hardened.
            Guid? withholdingReceivableAccountId = withholdingFunctionalAmount > 0m
                ? await ResolveConfiguredWithholdingReceivableAccountAsync(
                    dto.PaymentDate,
                    dto.WithholdingTaxId ?? throw new InvalidOperationException("Configured WHT receivable tax is required."),
                    TaxCategory.Withholding,
                    cancellationToken)
                    ?? throw new InvalidOperationException("The selected WHT tax has no effective receivable account configured.")
                : null;
            Guid? vatWithholdingReceivableAccountId = vatWithholdingFunctionalAmount > 0m
                ? await ResolveConfiguredWithholdingReceivableAccountAsync(
                    dto.PaymentDate,
                    dto.VatWithholdingTaxId ?? throw new InvalidOperationException("Configured VAT withholding receivable tax is required."),
                    TaxCategory.VatWithholding,
                    cancellationToken)
                    ?? throw new InvalidOperationException("The selected VAT withholding tax has no effective receivable account configured.")
                : null;

            var now = DateTime.UtcNow;
            payment.PaymentDate = dto.PaymentDate;
            payment.TotalAmount = dto.TotalAmount;
            payment.PaymentMethod = paymentMethod;
            payment.PaymentMethodId = configuredPaymentMethod?.Id;
            payment.BankAccountId = receiptDestination.BankAccountId;
            payment.LiquidityAccountId = receiptDestination.LiquidityAccountId;
            payment.CheckNumber = dto.CheckNumber;
            payment.ChequeDrawerBank = dto.ChequeDrawerBank;
            payment.TransactionReference = dto.TransactionReference;
            // Header tax values are reporting conveniences only. Rebuild them from immutable
            // allocation snapshots on every edit so a client cannot make the header disagree.
            payment.WithholdingTaxId = withholdingFunctionalAmount > 0m ? dto.WithholdingTaxId : null;
            payment.WithholdingTaxAccountId = withholdingReceivableAccountId;
            payment.WithholdingTaxAmount = withholdingFunctionalAmount;
            payment.VatWithholdingTaxId = vatWithholdingFunctionalAmount > 0m ? dto.VatWithholdingTaxId : null;
            payment.VatWithholdingAccountId = vatWithholdingReceivableAccountId;
            payment.VatWithholdingAmount = vatWithholdingFunctionalAmount;
            payment.WithholdingCertificateNumber = dto.WithholdingCertificateNumber;
            payment.WithholdingCertificateDate = dto.WithholdingCertificateDate;
            payment.Notes = dto.Notes;
            payment.UpdatedAt = now;
            payment.UpdatedBy = UserName;

            await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated payment {PaymentId}", payment.Id);

            return MapToDto(payment);
        }

        public async Task<CustomerPaymentDto> PostAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AR receipt posting.");

            CustomerPayment? payment = null;
            var transactionStarted = false;

            try
            {
                // Keep the receipt source state and the Finance-engine journal/event in one commit.
                // Serializable isolation prevents concurrent posted credits/receipts from over-settling an invoice.
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                transactionStarted = true;

                payment = await LoadPaymentForPostingAsync(id, cancellationToken);
                EnsureCompatibilityCreditNoteIsReadOnly(payment);
                await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                    payment.BankAccountId,
                    FinanceAccessLevel.Operate,
                    cancellationToken);
                var postingOutcome = await PostArReceiptCoreAsync(payment, cancellationToken);
                await FinalizeArReceiptPostingAsync(postingOutcome, cancellationToken);

                // UnitOfWork.CommitAsync owns rollback/disposal if the commit itself fails.
                transactionStarted = false;
                await _unitOfWork.CommitAsync(cancellationToken);

                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
            }
            catch (Exception ex)
            {
                if (transactionStarted)
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                }

                if (payment != null)
                {
                    await RecordArReceiptAuditAsync(
                        FinanceAuditEvents.ArReceiptPostingFailed,
                        payment,
                        afterValues: new
                        {
                            payment.JournalEntryId,
                            error = ex.Message
                        },
                        reason: ex.Message,
                        cancellationToken: cancellationToken);
                }

                _logger.LogError(ex, "Failed to post AR receipt {PaymentNumber}", payment?.PaymentNumber ?? id.ToString());
                throw;
            }
        }

        public async Task<CustomerPaymentDto> ReversePaymentAsync(
            Guid id,
            ReverseCustomerPaymentDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AR receipt reversal.");

            var initialPayment = await LoadPaymentForPostingAsync(id, cancellationToken);
            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                initialPayment.BankAccountId,
                FinanceAccessLevel.Approve,
                cancellationToken);

            // AP and AR share the same evaluator so reason length, closed-period behaviour, and
            // date selection remain one tenant policy instead of diverging by subledger.
            var policyDecision = await _financeReversalPolicyService.ResolveAsync(
                initialPayment.PaymentDate,
                dto.Reason,
                dto.ReversalDate,
                cancellationToken);
            var reason = policyDecision.Reason;
            var reversalDate = policyDecision.ReversalDate;
            var initialStatus = initialPayment.Status;
            var initialAllocatedAmount = initialPayment.AllocatedAmount;
            var initialJournalEntryId = initialPayment.JournalEntryId;

            var transactionStarted = false;
            try
            {
                // All compensating records are one accounting command. Serializable isolation
                // prevents an allocation, bank reconciliation, or deposit from consuming the
                // receipt while its GL and operational footprints are being reversed.
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                transactionStarted = true;

                var payment = await _unitOfWork.Repository<CustomerPayment>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                    .Include(item => item.BankAccount)
                    .Include(item => item.LiquidityAccount)
                    .Include(item => item.LiquidityAccountEntry)
                    .Include(item => item.ConfiguredPaymentMethod)
                    .Include(item => item.Allocations)
                        .ThenInclude(item => item.Invoice)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Payment with Id '{id}' not found.");
                var customer = await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken)
                    ?? throw new InvalidOperationException("Customer was not found while reversing the AR receipt.");

                // A successful retry returns the original correction. Deterministic posting keys
                // also protect the GL layer if concurrent requests pass the source-state check.
                if (string.Equals(payment.Status, "Reversed", StringComparison.OrdinalIgnoreCase) &&
                    payment.ReversalJournalEntryId.HasValue &&
                    payment.ReversalPostingEventId.HasValue)
                {
                    await _unitOfWork.CommitAsync(cancellationToken);
                    transactionStarted = false;
                    return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment, customer);
                }

                if (payment.IsCreditNote)
                {
                    throw new InvalidOperationException(
                        "Customer credit notes use their own correction workflow and cannot be reversed as cash receipts.");
                }
                if (!payment.JournalEntryId.HasValue ||
                    !string.Equals(payment.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Only a posted AR customer receipt can be reversed.");
                }

                var activeAllocations = payment.Allocations
                    .Where(item => !item.IsReversal && !item.IsDeleted)
                    .OrderBy(item => item.AllocationDate)
                    .ThenBy(item => item.Id)
                    .ToList();
                if (activeAllocations.Any(item => item.ApplicationPostingEventId.HasValue))
                {
                    // Applying a posted customer advance creates a separate advance-to-AR journal.
                    // Removing the original cash first would leave that reclassification orphaned.
                    throw new InvalidOperationException(
                        "This customer advance has posted applications. Reverse those applications before reversing the original receipt.");
                }

                var originalPosting = await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.SourceDocumentType == "CustomerPayment" &&
                        item.SourceDocumentId == payment.Id &&
                        item.PostingAction == "Post" &&
                        item.PostingStatus == "Posted" &&
                        !item.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The original AR receipt posting event was not found.");

                CashTransaction? originalCashTransaction = null;
                LiquidityAccountEntry? originalLiquidityEntry = null;
                if (payment.BankAccountId.HasValue)
                {
                    originalCashTransaction = await _unitOfWork.Repository<CashTransaction>()
                        .GetQueryable(item =>
                            item.TenantId == TenantId &&
                            item.BankAccountId == payment.BankAccountId.Value &&
                            item.TransactionType == CashTransactionType.Receipt &&
                            item.ReferenceNumber == payment.PaymentNumber &&
                            item.JournalEntryId == payment.JournalEntryId &&
                            !item.IsDeleted)
                        .SingleOrDefaultAsync(cancellationToken)
                        ?? throw new InvalidOperationException("The original bank receipt transaction was not found.");
                    if (originalCashTransaction.IsReconciled || originalCashTransaction.ReconciliationId.HasValue)
                    {
                        throw new InvalidOperationException(
                            "This receipt is bank-reconciled. Remove it from the reconciliation before reversal.");
                    }
                }
                else if (payment.LiquidityAccountEntryId.HasValue)
                {
                    originalLiquidityEntry = await _unitOfWork.Repository<LiquidityAccountEntry>()
                        .GetQueryable(item =>
                            item.TenantId == TenantId &&
                            item.Id == payment.LiquidityAccountEntryId.Value &&
                            !item.IsDeleted)
                        .SingleOrDefaultAsync(cancellationToken)
                        ?? throw new InvalidOperationException("The original receipt holding-account entry was not found.");
                    if (originalLiquidityEntry.AllocatedAmount > 0m)
                    {
                        throw new InvalidOperationException(
                            "This receipt has already been included in a bank deposit or settlement. Reverse that banking transaction first.");
                    }
                    if (originalLiquidityEntry.IsReversed)
                    {
                        throw new InvalidOperationException(
                            "The receipt holding-account entry has already been reversed by another controlled workflow.");
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        "The posted receipt has no bank or liquidity operational footprint to reverse.");
                }

                var reversalPlan = await _financePostingEngine.GetReversalPlanAsync(
                    originalPosting.Id,
                    reason,
                    reversalDate,
                    cancellationToken);
                var reversalResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                {
                    SourceModule = "AR",
                    SourceDocumentType = "CustomerPayment",
                    SourceDocumentId = payment.Id,
                    SourceDocumentTenantId = payment.TenantId,
                    PostingAction = "Reverse",
                    SourceDocumentReference = payment.PaymentNumber,
                    Description = $"Reverse customer receipt {payment.PaymentNumber} - {customer.PartnerName}",
                    PostingDate = reversalDate,
                    JournalType = "AR Receipt Reversal",
                    BookClassification = "IFRS",
                    FunctionalCurrencyCode = originalPosting.FunctionalCurrencyCode,
                    ReversalOfJournalEntryId = reversalPlan.OriginalJournalEntryId,
                    ReversalReason = reason,
                    ReversalType = "SourceDocument",
                    IdempotencyKey = $"AR:CustomerPayment:{payment.TenantId:N}:{payment.Id:N}:Reverse",
                    ReturnExistingOnDuplicate = true,
                    Lines = reversalPlan.ReversalLines.ToList()
                }, cancellationToken);

                // Realized FX is a distinct event from the receipt journal. Reverse each event in
                // this same transaction so no gain/loss survives after its settlement is removed.
                var realizedSettlements = await _unitOfWork.Repository<FxRealizedSettlement>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.SettlementDocumentType == "CustomerPayment" &&
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
                        SourceDocumentType = "PaymentAllocation",
                        SourceDocumentId = settlement.SettlementAllocationId,
                        SourceDocumentTenantId = payment.TenantId,
                        PostingAction = "ReverseRealizedFx",
                        SourceDocumentReference = payment.PaymentNumber,
                        Description = $"Reverse AR realized FX for {payment.PaymentNumber}",
                        PostingDate = reversalDate,
                        JournalType = "Realized FX Reversal",
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = settlement.FunctionalCurrencyCode,
                        ReversalOfJournalEntryId = fxPlan.OriginalJournalEntryId,
                        ReversalReason = reason,
                        ReversalType = "SourceDocument",
                        IdempotencyKey = $"FX:Realized:AR:{payment.TenantId:N}:{settlement.SettlementAllocationId:N}:Reverse",
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
                var restoredCustomerBalance = 0m;
                foreach (var allocation in activeAllocations)
                {
                    var settledAmount = allocation.AllocatedAmount + allocation.DiscountAmount +
                        allocation.WithholdingTaxAmount + allocation.VatWithholdingAmount;
                    restoredCustomerBalance += settledAmount;
                    allocation.Invoice.PaidAmount = Math.Max(
                        0m,
                        RoundMoney(allocation.Invoice.PaidAmount - settledAmount));
                    allocation.Invoice.Status = await ResolveInvoiceStatusAfterReceiptReversalAsync(
                        allocation.Invoice,
                        reversalDate,
                        cancellationToken);
                    allocation.Invoice.UpdatedAt = now;
                    allocation.Invoice.UpdatedBy = UserName;
                    await _unitOfWork.Repository<Invoice>().UpdateAsync(allocation.Invoice);

                    // Never rewrite or flag the original allocation as if it did not happen. A
                    // negative linked row gives reports an immutable settlement/correction chain.
                    await _unitOfWork.Repository<PaymentAllocation>().AddAsync(new PaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = payment.TenantId,
                        CustomerPaymentId = payment.Id,
                        InvoiceId = allocation.InvoiceId,
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
                        VatWithholdingAmount = -allocation.VatWithholdingAmount,
                        VatWithholdingFunctionalAmount = -allocation.VatWithholdingFunctionalAmount,
                        AllocationDate = reversalDate,
                        Notes = $"Receipt reversal of allocation {allocation.Id}: {reason}",
                        IsReversal = true,
                        OriginalAllocationId = allocation.Id,
                        CreatedAt = now,
                        CreatedBy = UserName
                    });
                }

                customer.OutstandingBalance = RoundMoney(
                    (customer.OutstandingBalance ?? 0m) + restoredCustomerBalance);
                customer.UpdatedAt = now;
                customer.UpdatedBy = UserName;
                await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customer);

                if (originalCashTransaction != null)
                {
                    var bankAccount = payment.BankAccount
                        ?? throw new InvalidOperationException("The receipt bank account was not loaded for reversal.");
                    var reversalTransactionNumber = await _documentNumberingService.GenerateAsync(
                        DocumentNumberingModules.Finance,
                        FinanceDocumentTypes.CashPayment,
                        TenantId,
                        reversalDate,
                        nameof(CashTransaction),
                        cancellationToken: cancellationToken);
                    var reversalCashTransaction = new CashTransaction
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        TransactionNumber = reversalTransactionNumber,
                        TransactionDate = reversalDate,
                        TransactionType = CashTransactionType.Payment,
                        BankAccountId = bankAccount.Id,
                        Amount = originalCashTransaction.Amount,
                        Currency = originalCashTransaction.Currency,
                        ExchangeRate = originalCashTransaction.ExchangeRate,
                        BaseAmount = originalCashTransaction.BaseAmount,
                        PaymentMethodId = originalCashTransaction.PaymentMethodId,
                        ReferenceNumber = $"{payment.PaymentNumber}-REV",
                        PayeeOrPayer = customer.PartnerName,
                        Description = $"Reversal of AR customer receipt {payment.PaymentNumber}: {reason}",
                        GLAccountId = originalCashTransaction.GLAccountId,
                        IsReconciled = false,
                        IsPosted = true,
                        ApprovalStatus = CashTransactionApprovalStatus.Posted,
                        JournalEntryId = reversalResult.JournalEntryId,
                        PostedDate = now,
                        PostedBy = CurrentUserId == Guid.Empty ? null : CurrentUserId,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };
                    await _unitOfWork.Repository<CashTransaction>().AddAsync(reversalCashTransaction);

                    // The cash transaction is an operational mirror of the already-posted journal;
                    // it must reduce the same bank balance increased by receipt finalization.
                    bankAccount.CurrentBalance -= originalCashTransaction.Amount;
                    bankAccount.AvailableBalance -= originalCashTransaction.Amount;
                    bankAccount.UpdatedAt = now;
                    bankAccount.UpdatedBy = UserName;
                    await _unitOfWork.Repository<BankAccount>().UpdateAsync(bankAccount);
                    payment.ReversalCashTransactionId = reversalCashTransaction.Id;
                }
                else if (originalLiquidityEntry != null)
                {
                    var reversalEntryNumber = await _documentNumberingService.GenerateAsync(
                        DocumentNumberingModules.Finance,
                        FinanceDocumentTypes.LiquidityEntry,
                        TenantId,
                        reversalDate,
                        nameof(LiquidityAccountEntry),
                        cancellationToken: cancellationToken);
                    var reversalLiquidityEntry = new LiquidityAccountEntry
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        LiquidityAccountId = originalLiquidityEntry.LiquidityAccountId,
                        EntryNumber = reversalEntryNumber,
                        EntryDate = reversalDate,
                        EntryType = LiquidityEntryType.Reversal,
                        Direction = LiquidityEntryDirection.Decrease,
                        Amount = originalLiquidityEntry.Amount,
                        AllocatedAmount = 0m,
                        Currency = originalLiquidityEntry.Currency,
                        SourceDocumentType = nameof(CustomerPayment),
                        SourceDocumentId = payment.Id,
                        ReferenceNumber = $"{payment.PaymentNumber}-REV",
                        CounterpartyName = customer.PartnerName,
                        Description = $"Reversal of AR customer receipt {payment.PaymentNumber}: {reason}",
                        ReversalOfEntryId = originalLiquidityEntry.Id,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };
                    await _unitOfWork.Repository<LiquidityAccountEntry>().AddAsync(reversalLiquidityEntry);
                    originalLiquidityEntry.IsReversed = true;
                    originalLiquidityEntry.UpdatedAt = now;
                    originalLiquidityEntry.UpdatedBy = UserName;
                    await _unitOfWork.Repository<LiquidityAccountEntry>().UpdateAsync(originalLiquidityEntry);
                    payment.ReversalLiquidityAccountEntryId = reversalLiquidityEntry.Id;
                }

                payment.Status = "Reversed";
                payment.AllocatedAmount = 0m;
                payment.ReversalJournalEntryId = reversalResult.JournalEntryId;
                payment.ReversalPostingEventId = reversalResult.PostingEventId;
                payment.ReversalDate = reversalDate;
                payment.ReversedAt = now;
                payment.ReversedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                payment.ReversalReason = reason;
                payment.UpdatedAt = now;
                payment.UpdatedBy = UserName;
                await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await RecordArReceiptAuditAsync(
                    FinanceAuditEvents.ArReceiptReversed,
                    payment,
                    postingEventId: reversalResult.PostingEventId,
                    journalEntryId: reversalResult.JournalEntryId,
                    beforeValues: new
                    {
                        Status = initialStatus,
                        JournalEntryId = initialJournalEntryId,
                        AllocatedAmount = initialAllocatedAmount
                    },
                    afterValues: new
                    {
                        payment.Status,
                        payment.ReversalJournalEntryId,
                        payment.ReversalPostingEventId,
                        payment.ReversalCashTransactionId,
                        payment.ReversalLiquidityAccountEntryId,
                        payment.ReversalDate,
                        ReversedAllocationCount = activeAllocations.Count,
                        ReversedRealizedFxCount = realizedSettlements.Count
                    },
                    reason: reason,
                    comment: "Posted AR receipt reversed through linked compensating Finance and operational entries.",
                    cancellationToken: cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);
                transactionStarted = false;
                _logger.LogWarning(
                    "Reversed AR receipt {PaymentNumber} with journal {ReversalJournalEntryId}. Reason: {Reason}",
                    payment.PaymentNumber,
                    payment.ReversalJournalEntryId,
                    reason);
                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment, customer);
            }
            catch (Exception ex)
            {
                if (transactionStarted)
                {
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    // EF does not automatically restore tracked entity state after a database
                    // rollback. Detach the failed business graph before the audit service saves
                    // its failure event, otherwise that save could leak uncommitted corrections.
                    _unitOfWork.ClearTrackedChanges();
                }

                await RecordArReceiptAuditAsync(
                    FinanceAuditEvents.ArReceiptReversalFailed,
                    initialPayment,
                    afterValues: new { PaymentId = id, ReversalDate = reversalDate, error = ex.Message },
                    reason: reason,
                    comment: "AR receipt reversal failed before the correction could be committed.",
                    cancellationToken: cancellationToken);
                _logger.LogError(ex, "Failed to reverse AR receipt {PaymentNumber}", initialPayment.PaymentNumber);
                throw;
            }
        }

        private async Task<ArReceiptPostingOutcome> PostArReceiptCoreAsync(
            CustomerPayment payment,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AR receipt posting.");

            var wasAlreadyLinked = payment.JournalEntryId.HasValue;
            var postingRequest = await BuildArReceiptPostingRequestAsync(payment, cancellationToken);
            var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

            if (payment.JournalEntryId.HasValue && payment.JournalEntryId.Value != postingResult.JournalEntryId)
                throw new InvalidOperationException("Customer payment is linked to a different journal entry than the posting engine result.");

            if (!payment.JournalEntryId.HasValue)
            {
                payment.JournalEntryId = postingResult.JournalEntryId;
                payment.Status = "Posted";
                payment.UpdatedAt = DateTime.UtcNow;
                payment.UpdatedBy = UserName;
                await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new ArReceiptPostingOutcome(payment, postingResult, wasAlreadyLinked);
        }

        private async Task FinalizeArReceiptPostingAsync(
            ArReceiptPostingOutcome outcome,
            CancellationToken cancellationToken)
        {
            var payment = outcome.Payment;
            var postingResult = outcome.PostingResult;

            if (postingResult.WasDuplicate || outcome.WasAlreadyLinked)
            {
                await RecordArReceiptAuditAsync(
                    FinanceAuditEvents.ArReceiptDuplicatePostingAttempt,
                    payment,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        postingResult.PostingEventId,
                        postingResult.JournalEntryId,
                        postingResult.PostingAction,
                        postingResult.WasDuplicate
                    },
                    comment: "Duplicate AR receipt posting request returned the existing posting.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await RecordArReceiptAuditAsync(
                    FinanceAuditEvents.ArReceiptPosted,
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
                        postingResult.PostingDate
                    },
                    comment: "AR receipt posted through the central finance posting engine.",
                    cancellationToken: cancellationToken);
            }

            // Realized FX is part of settlement accounting, so it participates in the same
            // transaction as the receipt and allocation instead of becoming a later partial commit.
            await PostRealizedFxIfRequiredAsync(payment, cancellationToken);

            var customer = await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken)
                ?? throw new InvalidOperationException("Customer was not found while finalizing the AR receipt.");
            if (payment.LiquidityAccountId.HasValue)
            {
                await CreateLiquidityEntryForReceiptAsync(payment, customer, cancellationToken);
            }
            else
            {
                await CreateCashTransactionForReceiptAsync(payment, customer, cancellationToken);
            }

            _logger.LogInformation(
                "Posted AR receipt {PaymentNumber} through finance posting engine with journal {JournalEntryId}. Duplicate={WasDuplicate}",
                payment.PaymentNumber,
                postingResult.JournalEntryId,
                postingResult.WasDuplicate);
        }

        private async Task<CustomerPaymentDto> PostCustomerCreditNoteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AR credit note posting.");

            var payment = await LoadPaymentForPostingAsync(id, cancellationToken);
            if (!payment.IsCreditNote)
                throw new InvalidOperationException("Customer payment is not an AR credit note.");

            var wasAlreadyLinked = payment.JournalEntryId.HasValue;

            try
            {
                var postingRequest = await BuildCustomerCreditNotePostingRequestAsync(payment, cancellationToken);
                var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

                if (payment.JournalEntryId.HasValue && payment.JournalEntryId.Value != postingResult.JournalEntryId)
                    throw new InvalidOperationException("Customer credit note is linked to a different journal entry than the posting engine result.");

                if (!payment.JournalEntryId.HasValue)
                {
                    payment.JournalEntryId = postingResult.JournalEntryId;
                    payment.Status = "Posted";
                    payment.UpdatedAt = DateTime.UtcNow;
                    payment.UpdatedBy = UserName;
                    await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                if (postingResult.WasDuplicate || wasAlreadyLinked)
                {
                    await RecordArCreditNoteAuditAsync(
                        FinanceAuditEvents.ArCreditNoteDuplicatePostingAttempt,
                        payment,
                        postingEventId: postingResult.PostingEventId,
                        journalEntryId: postingResult.JournalEntryId,
                        afterValues: new
                        {
                            postingResult.PostingEventId,
                            postingResult.JournalEntryId,
                            postingResult.PostingAction,
                            postingResult.WasDuplicate
                        },
                        comment: "Duplicate AR credit note posting request returned the existing posting.",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await RecordArCreditNoteAuditAsync(
                        FinanceAuditEvents.ArCreditNotePosted,
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
                            postingResult.PostingDate
                        },
                        comment: "AR credit note posted through the central finance posting engine.",
                        cancellationToken: cancellationToken);
                }

                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
            }
            catch (Exception ex)
            {
                await RecordArCreditNoteAuditAsync(
                    FinanceAuditEvents.ArCreditNotePostingFailed,
                    payment,
                    afterValues: new { payment.JournalEntryId, error = ex.Message },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);

                _logger.LogError(ex, "Failed to post AR credit note {PaymentNumber}", payment.PaymentNumber);
                throw;
            }
        }

        public async Task<PaymentAllocationResultDto> AllocatePaymentAsync(PaymentAllocation_CreateDto dto, CancellationToken cancellationToken = default)
        {
            return await AllocatePaymentCoreAsync(
                dto.CustomerPaymentId,
                dto.Allocations,
                postDiscountAdjustmentsForPostedPayment: true,
                cancellationToken);
        }

        private async Task<PaymentAllocationResultDto> AllocatePaymentCoreAsync(
            Guid paymentId,
            List<InvoiceAllocationDto> allocations,
            bool postDiscountAdjustmentsForPostedPayment,
            CancellationToken cancellationToken)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId)
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{paymentId}' not found.");

            EnsureCompatibilityCreditNoteIsReadOnly(payment);

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                payment.BankAccountId,
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (payment.JournalEntryId.HasValue)
            {
                if (!payment.IsCustomerAdvance)
                    throw new InvalidOperationException("Posted customer payments cannot be allocated. Use a reversal, void, or adjustment workflow.");

                // A posted customer advance is the explicit exception: applying it creates a
                // separate advance-to-AR-control reclassification through the posting engine.
                return await AllocatePostedCustomerAdvanceAsync(
                    paymentId,
                    allocations,
                    cancellationToken,
                    executionStrategyScope: false);
            }

            var requestedInvoices = await ResolveRequestedAllocationInvoicesAsync(
                payment,
                allocations,
                cancellationToken);

            var result = new PaymentAllocationResultDto
            {
                Success = false
            };

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

            // Calculate available cash. Discounts close invoice balance but do not consume cash availability.
            var availableAmount = payment.TotalAmount - payment.AllocatedAmount;

            if (availableAmount <= 0)
            {
                result.Message = "No unallocated amount available.";
                return result;
            }

            // Payment availability is denominated in receipt currency. For cross-currency rows the
            // invoice amount cannot be summed against it, so use the explicit receipt-currency leg.
            var totalAllocationRequested = allocations.Sum(a => Math.Max(
                a.PaymentCurrencyAmount ?? a.AllocatedAmount,
                0m));

            if (totalAllocationRequested > availableAmount)
            {
                result.Message = $"Allocation requested ({totalAllocationRequested:C}) exceeds available amount ({availableAmount:C}).";
                return result;
            }

            var now = DateTime.UtcNow;
            var allocatedInvoices = new List<Invoice>();
            var createdAllocations = new List<PaymentAllocation>();

            foreach (var allocationDto in allocations)
            {
                var invoice = requestedInvoices[allocationDto.InvoiceId];

                var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, functionalCurrency);
                var isCrossCurrency = !string.Equals(paymentCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase);
                if (isCrossCurrency && !allocationDto.PaymentCurrencyAmount.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Receipt-currency amount is required to settle invoice '{invoice.InvoiceNumber}' in {invoiceCurrency} from a {paymentCurrency} receipt.");
                }

                var cashAmount = Math.Max(allocationDto.AllocatedAmount, 0m);
                var paymentCashAmount = Math.Max(
                    allocationDto.PaymentCurrencyAmount ?? allocationDto.AllocatedAmount,
                    0m);
                var requestedDiscountAmount = Math.Max(allocationDto.DiscountAmount, 0m);
                var requestedWithholdingAmount = Math.Max(allocationDto.WithholdingTaxAmount, 0m);
                var requestedVatWithholdingAmount = Math.Max(allocationDto.VatWithholdingAmount, 0m);

                if (cashAmount <= 0 && requestedDiscountAmount <= 0 &&
                    requestedWithholdingAmount <= 0 && requestedVatWithholdingAmount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Allocation for invoice '{invoice.InvoiceNumber}' must include a positive cash or deduction amount.");
                }
                if (requestedWithholdingAmount > 0m && !payment.WithholdingTaxId.HasValue)
                    throw new InvalidOperationException("Select the configured AR WHT tax before allocating WHT to an invoice.");
                if (requestedVatWithholdingAmount > 0m && !payment.VatWithholdingTaxId.HasValue)
                    throw new InvalidOperationException("Select the configured AR VAT withholding tax before allocating VAT-WHT to an invoice.");

                // Cap at outstanding balance to prevent over-allocation
                var totalApplied = cashAmount + requestedDiscountAmount + requestedWithholdingAmount + requestedVatWithholdingAmount;
                var postedSalesCreditTotal = await GetPostedSalesCreditAmountForInvoiceAsync(invoice.Id, cancellationToken);
                var outstandingBalance = RoundMoney(invoice.TotalAmount - invoice.PaidAmount - postedSalesCreditTotal);
                if (outstandingBalance <= 0m)
                {
                    throw new InvalidOperationException(
                        $"Invoice '{invoice.InvoiceNumber}' has no outstanding balance available for allocation.");
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

                    var maximumDiscount = RoundMoney(outstandingBalance * invoice.EarlyPaymentDiscountPercentage / 100m);
                    if (RoundMoney(requestedDiscountAmount) > maximumDiscount)
                    {
                        throw new InvalidOperationException(
                            $"Discount {requestedDiscountAmount:C} exceeds the eligible amount {maximumDiscount:C} for invoice '{invoice.InvoiceNumber}'.");
                    }

                    if (Math.Abs(RoundMoney(
                        cashAmount + requestedDiscountAmount + requestedWithholdingAmount + requestedVatWithholdingAmount - outstandingBalance)) > 0.01m)
                    {
                        throw new InvalidOperationException(
                            $"An early-payment discount may only be taken when invoice '{invoice.InvoiceNumber}' is fully settled by this allocation.");
                    }
                }

                if (totalApplied > outstandingBalance)
                {
                    if (isCrossCurrency)
                    {
                        // Never change one side of an explicit conversion: doing so silently
                        // changes the commercial cross-rate approved by the maker.
                        throw new InvalidOperationException(
                            $"Cross-currency allocation would over-settle invoice '{invoice.InvoiceNumber}'.");
                    }

                    _logger.LogWarning(
                        "Allocation of {Requested} exceeds outstanding balance {Outstanding} on invoice {InvoiceNumber}. Capping.",
                        totalApplied, outstandingBalance, invoice.InvoiceNumber);

                    // Proportionally reduce every native component so their sum equals the
                    // outstanding invoice balance without changing the maker's relative split.
                    var ratio = totalApplied > 0 ? outstandingBalance / totalApplied : 0m;
                    cashAmount = Math.Round(cashAmount * ratio, 2);
                    requestedDiscountAmount = Math.Round(requestedDiscountAmount * ratio, 2);
                    requestedWithholdingAmount = Math.Round(requestedWithholdingAmount * ratio, 2);
                    requestedVatWithholdingAmount = Math.Max(
                        outstandingBalance - cashAmount - requestedDiscountAmount - requestedWithholdingAmount,
                        0m);
                    paymentCashAmount = cashAmount;
                    totalApplied = outstandingBalance;
                }

                var invoiceRate = await ResolveApprovedSettlementRateAsync(
                    invoiceCurrency,
                    functionalCurrency,
                    payment.PaymentDate,
                    allocationDto.InvoiceSettlementExchangeRateId,
                    string.Equals(invoiceCurrency, paymentCurrency, StringComparison.OrdinalIgnoreCase)
                        ? paymentRate.Rate
                        : invoice.ExchangeRate,
                    requireApprovedSource: !string.Equals(invoiceCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase),
                    cancellationToken);
                var settlement = CrossCurrencySettlementCalculator.CalculateWithDeductions(
                    paymentCurrency,
                    invoiceCurrency,
                    paymentCashAmount,
                    cashAmount,
                    requestedDiscountAmount,
                    requestedWithholdingAmount,
                    requestedVatWithholdingAmount,
                    paymentRate.Rate,
                    invoiceRate.Rate);

                // Create the allocation only after all currency and balance validations succeed.
                // This avoids leaving a partially described cross-currency fact in the DbContext.
                var allocation = new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CustomerPaymentId = payment.Id,
                    InvoiceId = invoice.Id,
                    AllocatedAmount = cashAmount,
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
                    DiscountAmount = requestedDiscountAmount,
                    DiscountFunctionalAmount = settlement.DiscountFunctionalAmount,
                    WithholdingTaxAmount = requestedWithholdingAmount,
                    WithholdingTaxFunctionalAmount = settlement.WithholdingFunctionalAmount,
                    VatWithholdingAmount = requestedVatWithholdingAmount,
                    VatWithholdingFunctionalAmount = settlement.VatWithholdingFunctionalAmount,
                    AllocationDate = now,
                    Notes = allocationDto.Notes,
                    IsReversal = false,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                // Keep the new allocation in Added state when the tracked payment graph is
                // updated below. With a client-generated Guid, graph Update otherwise treats
                // this row as existing and issues an UPDATE that cannot match any database row.
                await _unitOfWork.Repository<PaymentAllocation>().AddAsync(allocation);
                createdAllocations.Add(allocation);

                // Update invoice balances
                invoice.PaidAmount += totalApplied;

                // Update invoice status
                var operationalOutstanding = RoundMoney(invoice.TotalAmount - invoice.PaidAmount - postedSalesCreditTotal);
                if (operationalOutstanding <= 0.01m) // Account for rounding
                {
                    invoice.Status = InvoiceStatus.Paid;
                }
                else if (invoice.PaidAmount > 0 || postedSalesCreditTotal > 0)
                {
                    invoice.Status = InvoiceStatus.PartiallyPaid;
                }

                invoice.UpdatedAt = now;
                invoice.UpdatedBy = UserName;

                allocatedInvoices.Add(invoice);
                await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);

                result.Allocations.Add(new PaymentAllocationDto
                {
                    Id = allocation.Id,
                    CustomerPaymentId = payment.Id,
                    PaymentNumber = payment.PaymentNumber,
                    InvoiceId = invoice.Id,
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
                    DiscountAmount = allocation.DiscountAmount,
                    DiscountFunctionalAmount = allocation.DiscountFunctionalAmount,
                    WithholdingTaxAmount = allocation.WithholdingTaxAmount,
                    WithholdingTaxFunctionalAmount = allocation.WithholdingTaxFunctionalAmount,
                    VatWithholdingAmount = allocation.VatWithholdingAmount,
                    VatWithholdingFunctionalAmount = allocation.VatWithholdingFunctionalAmount,
                    AllocationDate = allocation.AllocationDate,
                    Notes = allocation.Notes
                });
            }

            // Update payment allocated amount
            payment.AllocatedAmount = payment.Allocations
                .Where(a => !a.IsReversal)
                .Sum(a => a.PaymentCurrencyAmount > 0m ? a.PaymentCurrencyAmount : a.AllocatedAmount);

            // Update customer outstanding balance (deduct only the newly allocated amount, not cumulative PaidAmount)
            var customerPartner = await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);
            if (customerPartner != null)
            {
                var totalNewlyAllocated = payment.Allocations
                    .Where(a => a.CreatedAt == now) // Only allocations created in this request
                    .Sum(a => a.AllocatedAmount + a.DiscountAmount + a.WithholdingTaxAmount + a.VatWithholdingAmount);
                customerPartner.OutstandingBalance = (customerPartner.OutstandingBalance ?? 0m) - totalNewlyAllocated;
                customerPartner.UpdatedAt = now;
                customerPartner.UpdatedBy = UserName;
                await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customerPartner);
            }

            await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            result.Success = true;
            result.TotalAllocated = payment.AllocatedAmount;
            result.RemainingUnallocated = payment.UnallocatedAmount;
            result.Message = $"Successfully allocated to {result.Allocations.Count} invoice(s).";

            _logger.LogInformation("Allocated payment {PaymentNumber}: {Amount} to {Count} invoices",
                payment.PaymentNumber, payment.AllocatedAmount, result.Allocations.Count);

            return result;
        }

        private async Task<IReadOnlyDictionary<Guid, Invoice>> ResolveRequestedAllocationInvoicesAsync(
            CustomerPayment payment,
            IReadOnlyCollection<InvoiceAllocationDto> allocations,
            CancellationToken cancellationToken)
        {
            if (allocations.Count == 0)
                throw new InvalidOperationException("At least one invoice allocation is required.");

            if (allocations.Any(a => a.InvoiceId == Guid.Empty))
                throw new InvalidOperationException("Every customer receipt allocation must reference an invoice.");

            var invoiceIds = allocations.Select(a => a.InvoiceId).ToList();
            if (invoiceIds.Distinct().Count() != invoiceIds.Count)
                throw new InvalidOperationException("A customer receipt cannot allocate to the same invoice more than once in one request.");

            var invoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    !i.IsDeleted &&
                    invoiceIds.Contains(i.Id))
                .ToListAsync(cancellationToken);

            if (invoices.Count != invoiceIds.Count)
            {
                var missingIds = invoiceIds.Except(invoices.Select(i => i.Id));
                throw new KeyNotFoundException(
                    $"Customer receipt allocation invoice(s) were not found for this tenant: {string.Join(", ", missingIds)}.");
            }

            var wrongCustomer = invoices.FirstOrDefault(i => i.BusinessPartnerId != payment.CustomerId);
            if (wrongCustomer != null)
            {
                throw new InvalidOperationException(
                    $"Invoice '{wrongCustomer.InvoiceNumber}' does not belong to the customer selected for this receipt.");
            }

            foreach (var invoice in invoices)
            {
                EnsureInvoiceCollectibleForReceipt(invoice);
            }

            return invoices.ToDictionary(i => i.Id);
        }

        internal static void EnsureInvoiceCollectibleForReceipt(Invoice invoice)
        {
            var collectibleStatus = invoice.Status is
                InvoiceStatus.Sent or
                InvoiceStatus.PartiallyPaid or
                InvoiceStatus.Overdue;

            if (!collectibleStatus)
            {
                throw new InvalidOperationException(
                    $"Invoice '{invoice.InvoiceNumber}' is not collectible while its status is {invoice.Status}.");
            }

            // Opening balances become operational receivables only after their governed
            // approval has produced immutable GL evidence. Do not infer posting from Sent alone.
            if (invoice.IsOpeningBalance && !invoice.JournalEntryId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Opening-balance invoice '{invoice.InvoiceNumber}' has no posting evidence and cannot receive a customer receipt.");
            }
        }

        private async Task<PaymentAllocationResultDto> AllocatePostedCustomerAdvanceAsync(
            Guid paymentId,
            IReadOnlyCollection<InvoiceAllocationDto> requestedAllocations,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for customer advance application.");
            if (requestedAllocations.Count == 0)
                throw new InvalidOperationException("At least one customer-invoice allocation is required to apply an advance.");
            if (requestedAllocations.Any(a =>
                    a.AllocatedAmount <= 0m ||
                    a.DiscountAmount != 0m ||
                    a.WithholdingTaxAmount != 0m ||
                    a.VatWithholdingAmount != 0m))
            {
                // The advance application journal releases only the historical cash lot and AR
                // control. Discounts and withholding are separate accounting documents with their
                // own tax evidence, so accepting them here would silently omit required postings.
                throw new InvalidOperationException(
                    "Customer advance applications support positive cash allocations only; use a dedicated adjustment workflow for discounts or withholding.");
            }

            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                // SQL Server's retrying execution strategy must own the complete transaction. Keep
                // the public path retry-safe while allowing an existing Finance transaction to
                // compose this operation without attempting to create a nested transaction.
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => AllocatePostedCustomerAdvanceAsync(
                        paymentId,
                        requestedAllocations,
                        cancellationToken,
                        executionStrategyScope: true),
                    cancellationToken);
            }

            var transactionStarted = false;
            try
            {
                // Keep the allocation, customer/invoice snapshots and the advance reclassification
                // in one serializable transaction so an advance cannot be applied twice.
                if (!_unitOfWork.HasActiveTransaction)
                {
                    await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                    transactionStarted = true;
                }
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"ar-advance-payment:{TenantId:N}:{paymentId:N}",
                    cancellationToken);
                foreach (var invoiceId in requestedAllocations
                             .Select(item => item.InvoiceId)
                             .Distinct()
                             .OrderBy(item => item))
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"ar-advance-invoice:{TenantId:N}:{invoiceId:N}",
                        cancellationToken);
                }

                var payment = await _unitOfWork.Repository<CustomerPayment>()
                    .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId && !p.IsDeleted)
                    .Include(p => p.Allocations)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Customer advance with Id '{paymentId}' was not found.");

                if (!payment.JournalEntryId.HasValue || !payment.IsCustomerAdvance || payment.IsCreditNote)
                    throw new InvalidOperationException("Only a posted customer advance can be applied through this workflow.");

                var settings = await GetFinanceSettingsAsync(cancellationToken);
                var customer = await ResolveCustomerForPostingAsync(payment, cancellationToken);
                var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
                var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
                var paymentRate = NormalizeExchangeRate(payment.ExchangeRate);
                if (!string.Equals(paymentCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase) &&
                    (!payment.ExchangeRateId.HasValue || payment.ExchangeRate <= 0m))
                {
                    throw new InvalidOperationException(
                        "The foreign-currency customer advance is missing its approved origin-rate evidence.");
                }

                var arAccountId = customer.DefaultArAccountId
                    ?? settings.ControlAccountArId
                    ?? throw new InvalidOperationException("AR control account is not configured for this tenant.");
                var advanceAccountId = settings.CustomerAdvanceAccountId
                    ?? throw new InvalidOperationException("Customer advance account is not configured for this tenant.");
                var accountCache = new Dictionary<Guid, Account>();
                await ResolveReceiptPostingAccountAsync(arAccountId, "AR control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);
                var advanceAccount = await ResolveReceiptPostingAccountAsync(advanceAccountId, "customer advance account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);
                if (advanceAccount.AccountType != AccountType.Liability)
                    throw new InvalidOperationException("Customer advance account must be a liability account.");

                var currentlyAllocated = RoundMoney(GetEffectiveAllocations(payment.Allocations)
                    .Sum(a => a.PaymentCurrencyAmount > 0m ? a.PaymentCurrencyAmount : a.AllocatedAmount));
                var requestedTotal = RoundMoney(requestedAllocations.Sum(a =>
                    a.PaymentCurrencyAmount ?? a.AllocatedAmount));
                if (requestedTotal > RoundMoney(payment.TotalAmount - currentlyAllocated))
                    throw new InvalidOperationException("Customer advance application exceeds the unallocated advance balance.");

                var now = DateTime.UtcNow;
                var applicationDate = await ResolveCurrentOpenPostingDateAsync(cancellationToken);
                var result = new PaymentAllocationResultDto { Success = false };
                var newlyApplied = 0m;
                foreach (var requested in requestedAllocations)
                {
                    var invoice = await _unitOfWork.Repository<Invoice>()
                        .GetQueryable(i => i.TenantId == TenantId && i.Id == requested.InvoiceId && !i.IsDeleted)
                        .FirstOrDefaultAsync(cancellationToken)
                        ?? throw new InvalidOperationException("Customer advance application invoice was not found for this tenant.");
                    if (invoice.BusinessPartnerId != payment.CustomerId)
                        throw new InvalidOperationException("Customer advance can only be applied to invoices for the same customer.");
                    if (!invoice.JournalEntryId.HasValue)
                        throw new InvalidOperationException($"Customer advance cannot be applied to unposted invoice '{invoice.InvoiceNumber}'.");
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
                            "Cross-currency customer advance application requires the advance-currency amount to consume.");
                    }

                    // The posted receipt is the advance lot. Release it at its immutable origin
                    // rate, value the invoice at the approved application-date rate, and retain
                    // both values on the allocation so the realized FX line is reconstructable.
                    var settlement = CrossCurrencySettlementCalculator.Calculate(
                        paymentCurrency,
                        invoiceCurrency,
                        requestedAdvanceAmount,
                        RoundMoney(requested.AllocatedAmount),
                        invoiceDeductionAmount: 0m,
                        paymentExchangeRate: paymentRate,
                        invoiceSettlementExchangeRate: invoiceRate.Rate);

                    var postedSalesCredits = await GetPostedSalesCreditAmountForInvoiceAsync(invoice.Id, cancellationToken);
                    var invoiceOutstanding = RoundMoney(invoice.TotalAmount - invoice.PaidAmount - postedSalesCredits);
                    if (requested.AllocatedAmount > invoiceOutstanding)
                        throw new InvalidOperationException($"Customer advance application exceeds the outstanding balance of invoice '{invoice.InvoiceNumber}'.");

                    var allocation = new PaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        CustomerPaymentId = payment.Id,
                        InvoiceId = invoice.Id,
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
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    // Register the allocation explicitly as Added. Updating the receipt source
                    // record later must not turn this new row into a modified-only graph entry.
                    // EF relationship fixup adds it to payment.Allocations; a second manual add
                    // would double the same currency-lot consumption in the tracked collection.
                    await _unitOfWork.Repository<PaymentAllocation>().AddAsync(allocation);
                    // This is a read-side operational snapshot. Recompute it from allocations so
                    // a stale value cannot cause an advance to be over-applied after a retry. The
                    // effective view also excludes original applications cancelled by linked facts.
                    payment.AllocatedAmount = RoundMoney(GetEffectiveAllocations(payment.Allocations)
                        .Sum(a => a.PaymentCurrencyAmount > 0m ? a.PaymentCurrencyAmount : a.AllocatedAmount));
                    payment.UpdatedAt = now;
                    payment.UpdatedBy = UserName;
                    invoice.PaidAmount = RoundMoney(invoice.PaidAmount + allocation.AllocatedAmount);
                    var operationalOutstanding = RoundMoney(invoice.TotalAmount - invoice.PaidAmount - postedSalesCredits);
                    invoice.Status = operationalOutstanding <= 0.01m ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                    invoice.UpdatedAt = now;
                    invoice.UpdatedBy = UserName;
                    newlyApplied += allocation.AllocatedAmount;
                    await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
                    await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    var realizedFxDelta = RoundMoney(
                        allocation.SettlementFunctionalAmount - allocation.PaymentFunctionalAmount);
                    var postingLines = new List<FinancePostingLineDto>
                    {
                        BuildPostingLine(
                            advanceAccountId,
                            $"Release customer advance {payment.PaymentNumber}",
                            allocation.PaymentCurrencyAmount,
                            0m,
                            allocation.PaymentCurrencyCode,
                            functionalCurrency,
                            allocation.PaymentExchangeRate,
                            payment.PaymentDate,
                            payment.PaymentNumber,
                            1,
                            "AR-CustomerAdvance",
                            allocation.PaymentExchangeRateId,
                            functionalDebitOverride: allocation.PaymentFunctionalAmount),
                        BuildPostingLine(
                            arAccountId,
                            $"Apply customer advance {payment.PaymentNumber}",
                            0m,
                            allocation.AllocatedAmount,
                            allocation.InvoiceCurrencyCode,
                            functionalCurrency,
                            allocation.InvoiceSettlementExchangeRate,
                            applicationDate,
                            payment.PaymentNumber,
                            2,
                            "AR-Control",
                            allocation.InvoiceSettlementExchangeRateId,
                            functionalCreditOverride: allocation.SettlementFunctionalAmount)
                    };
                    if (realizedFxDelta != 0m)
                    {
                        // Customer advances are liabilities. An increase in the invoice value
                        // relative to the carried advance is therefore a loss; a decrease is a gain.
                        var isGain = realizedFxDelta < 0m;
                        var fxAccountId = isGain
                            ? settings.RealizedFxGainAccountId
                            : settings.RealizedFxLossAccountId;
                        var fxAccount = await ResolveReceiptPostingAccountAsync(
                            fxAccountId ?? throw new InvalidOperationException(
                                $"Realized FX {(isGain ? "gain" : "loss")} account is not configured for this tenant."),
                            isGain ? "realized FX gain account" : "realized FX loss account",
                            accountCache,
                            allowControlAccount: false,
                            requireDirectPosting: true,
                            cancellationToken);
                        postingLines.Add(BuildPostingLine(
                            fxAccount.Id,
                            $"Customer advance realized FX {(isGain ? "gain" : "loss")} {payment.PaymentNumber}",
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
                        SourceModule = "AR",
                        SourceDocumentType = "CustomerPaymentAdvanceApplication",
                        SourceDocumentId = allocation.Id,
                        SourceDocumentTenantId = payment.TenantId,
                        PostingAction = "Post",
                        SourceDocumentReference = $"{payment.PaymentNumber}:{invoice.InvoiceNumber}",
                        Description = $"Apply customer advance {payment.PaymentNumber} to invoice {invoice.InvoiceNumber}",
                        PostingDate = applicationDate,
                        JournalType = "AR Customer Advance Application",
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = functionalCurrency,
                        IdempotencyKey = $"AR:CustomerPaymentAdvanceApplication:{payment.TenantId:N}:{allocation.Id:N}:Post",
                        ReturnExistingOnDuplicate = true,
                        Lines = postingLines
                    }, cancellationToken);

                    allocation.ApplicationJournalEntryId = postingResult.JournalEntryId;
                    allocation.ApplicationPostingEventId = postingResult.PostingEventId;
                    allocation.UpdatedAt = now;
                    allocation.UpdatedBy = UserName;
                    await _unitOfWork.Repository<PaymentAllocation>().UpdateAsync(allocation);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    result.Allocations.Add(new PaymentAllocationDto
                    {
                        Id = allocation.Id,
                        CustomerPaymentId = payment.Id,
                        PaymentNumber = payment.PaymentNumber,
                        InvoiceId = invoice.Id,
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
                        Notes = allocation.Notes
                    });
                }

                var customerPartner = await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);
                if (customerPartner != null)
                {
                    customerPartner.OutstandingBalance = (customerPartner.OutstandingBalance ?? 0m) - newlyApplied;
                    customerPartner.UpdatedAt = now;
                    customerPartner.UpdatedBy = UserName;
                    await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customerPartner);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                if (transactionStarted)
                {
                    await _unitOfWork.CommitAsync(cancellationToken);
                    transactionStarted = false;
                }
                result.Success = true;
                result.TotalAllocated = payment.AllocatedAmount;
                result.RemainingUnallocated = RoundMoney(payment.TotalAmount - payment.AllocatedAmount);
                result.Message = $"Applied customer advance to {result.Allocations.Count} invoice(s).";
                await RecordArReceiptAuditAsync(
                    FinanceAuditEvents.ArCustomerAdvanceApplied,
                    payment,
                    afterValues: new
                    {
                        result.TotalAllocated,
                        result.RemainingUnallocated,
                        AllocationCount = result.Allocations.Count,
                        CurrencyLot = paymentCurrency
                    },
                    comment: "Posted currency-lotted customer advance application and any realized FX through the central finance posting engine.",
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

        public async Task ReverseAllocationAsync(Guid allocationId, string reason, CancellationToken cancellationToken = default)
        {
            var allocation = await _unitOfWork.Repository<PaymentAllocation>()
                .GetQueryable(a => a.TenantId == TenantId && a.Id == allocationId)
                .Include(a => a.CustomerPayment)
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (allocation == null)
                throw new KeyNotFoundException($"Allocation with Id '{allocationId}' not found.");

            EnsureCompatibilityCreditNoteIsReadOnly(allocation.CustomerPayment);

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                allocation.CustomerPayment.BankAccountId,
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (allocation.IsReversal)
                throw new InvalidOperationException("This allocation has already been reversed.");

            if (allocation.CustomerPayment.JournalEntryId.HasValue)
            {
                if (allocation.CustomerPayment.IsCustomerAdvance &&
                    allocation.ApplicationPostingEventId.HasValue)
                {
                    await ReversePostedCustomerAdvanceApplicationAsync(
                        allocation.Id,
                        reason,
                        cancellationToken,
                        executionStrategyScope: false);
                    return;
                }

                throw new InvalidOperationException("Posted customer payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");
            }

            var now = DateTime.UtcNow;

            // Reverse invoice balances
            var totalApplied = allocation.AllocatedAmount + allocation.DiscountAmount +
                allocation.WithholdingTaxAmount + allocation.VatWithholdingAmount;
            allocation.Invoice.PaidAmount -= totalApplied;

            // Update invoice status
            if (allocation.Invoice.PaidAmount <= 0)
            {
                allocation.Invoice.Status = InvoiceStatus.Sent;
            }
            else
            {
                allocation.Invoice.Status = InvoiceStatus.PartiallyPaid;
            }

            allocation.Invoice.UpdatedAt = now;
            allocation.Invoice.UpdatedBy = UserName;
            await _unitOfWork.Repository<Invoice>().UpdateAsync(allocation.Invoice);

            // Reverse payment allocated amount
            // The receipt balance is denominated in payment currency; subtracting the invoice
            // amount here would corrupt remaining cash for a pre-post cross-currency allocation.
            allocation.CustomerPayment.AllocatedAmount -= allocation.PaymentCurrencyAmount > 0m
                ? allocation.PaymentCurrencyAmount
                : allocation.AllocatedAmount;
            allocation.CustomerPayment.UpdatedAt = now;
            allocation.CustomerPayment.UpdatedBy = UserName;

            // Reverse customer outstanding balance
            var allocationCustomer = await GetCustomerPartnerAsync(allocation.CustomerPayment.CustomerId, cancellationToken);
            if (allocationCustomer != null)
            {
                allocationCustomer.OutstandingBalance = (allocationCustomer.OutstandingBalance ?? 0m) + totalApplied;
                allocationCustomer.UpdatedAt = now;
                allocationCustomer.UpdatedBy = UserName;
                await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(allocationCustomer);
            }

            // Mark allocation as reversed
            allocation.IsReversal = true;
            allocation.Notes = $"{allocation.Notes}\n\nReversed on {now:yyyy-MM-dd HH:mm}: {reason}";
            allocation.UpdatedAt = now;
            allocation.UpdatedBy = UserName;

            await _unitOfWork.Repository<PaymentAllocation>().UpdateAsync(allocation);
            await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(allocation.CustomerPayment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Reversed allocation {AllocationId}. Reason: {Reason}", allocationId, reason);
        }

        private async Task ReversePostedCustomerAdvanceApplicationAsync(
            Guid allocationId,
            string reason,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException(
                    "Central finance posting engine is not configured for customer advance application reversal.");
            var trimmedReason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason))
                throw new InvalidOperationException("A reason is required to reverse a customer advance application.");

            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                await _unitOfWork.ExecuteInStrategyAsync(
                    async () =>
                    {
                        await ReversePostedCustomerAdvanceApplicationAsync(
                            allocationId,
                            trimmedReason,
                            cancellationToken,
                            executionStrategyScope: true);
                        return true;
                    },
                    cancellationToken);
                return;
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            try
            {
                if (ownsTransaction)
                    await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"ar-advance-application-reversal:{TenantId:N}:{allocationId:N}",
                    cancellationToken);

                var allocation = await _unitOfWork.Repository<PaymentAllocation>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.Id == allocationId &&
                        !item.IsDeleted)
                    .Include(item => item.CustomerPayment)
                        .ThenInclude(payment => payment.Allocations)
                    .Include(item => item.Invoice)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Allocation with Id '{allocationId}' not found.");
                if (!allocation.CustomerPayment.IsCustomerAdvance ||
                    !allocation.ApplicationPostingEventId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Only a posted customer advance application can use this reversal workflow.");
                }

                var alreadyReversed = await _unitOfWork.Repository<PaymentAllocation>()
                    .GetQueryableIncludingDeleted(item =>
                        item.TenantId == TenantId &&
                        item.IsReversal &&
                        item.OriginalAllocationId == allocationId)
                    .AnyAsync(cancellationToken);
                if (alreadyReversed)
                    throw new InvalidOperationException("This customer advance application has already been reversed.");

                var originalEvent = await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.Id == allocation.ApplicationPostingEventId.Value &&
                        item.PostingStatus == "Posted" &&
                        item.JournalEntryId.HasValue &&
                        !item.IsDeleted)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The authoritative customer advance application posting event was not found.");
                var reversalDate = await ResolveCurrentOpenPostingDateAsync(cancellationToken);
                var plan = await _financePostingEngine.GetReversalPlanAsync(
                    originalEvent.Id,
                    trimmedReason,
                    reversalDate,
                    cancellationToken);
                var reversalResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                {
                    SourceModule = originalEvent.SourceModule,
                    OriginModuleCode = originalEvent.OriginModuleCode,
                    SourceDocumentType = originalEvent.SourceDocumentType,
                    SourceDocumentId = originalEvent.SourceDocumentId,
                    SourceDocumentTenantId = originalEvent.TenantId,
                    PostingAction = "Reverse",
                    SourceDocumentReference = originalEvent.SourceDocumentReference,
                    Description = $"Reverse customer advance application {allocation.CustomerPayment.PaymentNumber}",
                    PostingDate = plan.ReversalDate,
                    JournalType = "Customer Advance Application Reversal",
                    BookClassification = originalEvent.BookClassification,
                    FunctionalCurrencyCode = originalEvent.FunctionalCurrencyCode,
                    ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
                    ReversalReason = trimmedReason,
                    // The posting engine caps this indexed classification at 20 characters. The
                    // journal type and description retain the full application context.
                    ReversalType = "Customer advance",
                    IdempotencyKey = $"AR:CustomerPaymentAdvanceApplication:{TenantId:N}:{allocation.Id:N}:Reverse",
                    ReturnExistingOnDuplicate = true,
                    Lines = plan.ReversalLines.ToList()
                }, cancellationToken);

                var now = DateTime.UtcNow;
                allocation.Invoice.PaidAmount = Math.Max(
                    0m,
                    RoundMoney(allocation.Invoice.PaidAmount - allocation.AllocatedAmount));
                allocation.Invoice.Status = await ResolveInvoiceStatusAfterReceiptReversalAsync(
                    allocation.Invoice,
                    plan.ReversalDate,
                    cancellationToken);
                allocation.Invoice.UpdatedAt = now;
                allocation.Invoice.UpdatedBy = UserName;
                await _unitOfWork.Repository<Invoice>().UpdateAsync(allocation.Invoice);

                // Preserve the original application and add a linked negative fact carrying the
                // identical native amounts and rate snapshots. This makes both the invoice and
                // advance-lot balances rebuildable from immutable evidence.
                await _unitOfWork.Repository<PaymentAllocation>().AddAsync(new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CustomerPaymentId = allocation.CustomerPaymentId,
                    InvoiceId = allocation.InvoiceId,
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
                    AllocationDate = plan.ReversalDate,
                    Notes = $"Controlled reversal of customer advance application {allocation.Id}: {trimmedReason}",
                    IsReversal = true,
                    OriginalAllocationId = allocation.Id,
                    ApplicationPostingEventId = reversalResult.PostingEventId,
                    ApplicationJournalEntryId = reversalResult.JournalEntryId,
                    CreatedAt = now,
                    CreatedBy = UserName
                });

                var restoredAdvanceAmount = allocation.PaymentCurrencyAmount > 0m
                    ? allocation.PaymentCurrencyAmount
                    : allocation.AllocatedAmount;
                allocation.CustomerPayment.AllocatedAmount = Math.Max(
                    0m,
                    RoundMoney(allocation.CustomerPayment.AllocatedAmount - restoredAdvanceAmount));
                allocation.CustomerPayment.UpdatedAt = now;
                allocation.CustomerPayment.UpdatedBy = UserName;
                await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(allocation.CustomerPayment);

                var customer = await GetCustomerPartnerAsync(
                    allocation.CustomerPayment.CustomerId,
                    cancellationToken);
                if (customer != null)
                {
                    customer.OutstandingBalance = RoundMoney(
                        (customer.OutstandingBalance ?? 0m) + allocation.AllocatedAmount);
                    customer.UpdatedAt = now;
                    customer.UpdatedBy = UserName;
                    await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customer);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await RecordArReceiptAuditAsync(
                    FinanceAuditEvents.ArCustomerAdvanceApplicationReversed,
                    allocation.CustomerPayment,
                    postingEventId: reversalResult.PostingEventId,
                    journalEntryId: reversalResult.JournalEntryId,
                    afterValues: new
                    {
                        ReversedAllocationId = allocation.Id,
                        RestoredAdvanceCurrencyAmount = restoredAdvanceAmount,
                        allocation.PaymentCurrencyCode,
                        RestoredInvoiceCurrencyAmount = allocation.AllocatedAmount,
                        allocation.InvoiceCurrencyCode
                    },
                    reason: trimmedReason,
                    comment: "Reversed a posted customer advance application and restored its immutable currency lot.",
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

        public async Task<CustomerPaymentDto> ClearPaymentAsync(Guid id, DateTime clearedDate, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == id);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{id}' not found.");

            EnsureCompatibilityCreditNoteIsReadOnly(payment);

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                payment.BankAccountId,
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (payment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted customer payments cannot be cleared by mutation until bank reconciliation integration is migrated to the posting engine.");

            payment.Status = "Cleared";
            payment.ClearedDate = clearedDate;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.UpdatedBy = UserName;

            await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cleared payment {PaymentNumber}", payment.PaymentNumber);

            return MapToDto(payment);
        }

        public async Task<CustomerPaymentDto> BouncedPaymentAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.Invoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{id}' not found.");

            EnsureCompatibilityCreditNoteIsReadOnly(payment);

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                payment.BankAccountId,
                FinanceAccessLevel.Operate,
                cancellationToken);

            if (payment.JournalEntryId.HasValue)
            {
                throw new InvalidOperationException(
                    "Posted receipts cannot be bounced by mutation. Use the posted receipt reversal workflow, or the returned-cheque workflow after deposit.");
            }

            var now = DateTime.UtcNow;

            // Capture the amount to reverse BEFORE the loop marks allocations as reversed
            var activeAllocations = payment.Allocations.Where(a => !a.IsReversal).ToList();
            var reversedAmount = activeAllocations.Sum(a =>
                a.AllocatedAmount + a.DiscountAmount + a.WithholdingTaxAmount + a.VatWithholdingAmount);

            // Reverse all active allocations
            foreach (var allocation in activeAllocations)
            {
                var totalApplied = allocation.AllocatedAmount + allocation.DiscountAmount +
                    allocation.WithholdingTaxAmount + allocation.VatWithholdingAmount;
                allocation.Invoice.PaidAmount -= totalApplied;

                if (allocation.Invoice.PaidAmount <= 0)
                    allocation.Invoice.Status = InvoiceStatus.Sent;
                else
                    allocation.Invoice.Status = InvoiceStatus.PartiallyPaid;

                allocation.Invoice.UpdatedAt = now;
                await _unitOfWork.Repository<Invoice>().UpdateAsync(allocation.Invoice);

                allocation.IsReversal = true;
                allocation.UpdatedAt = now;
                await _unitOfWork.Repository<PaymentAllocation>().UpdateAsync(allocation);
            }

            // Update payment status
            payment.Status = "Bounced";
            payment.AllocatedAmount = 0;
            payment.Notes = $"{payment.Notes}\n\nBounced on {now:yyyy-MM-dd HH:mm}: {reason}";
            payment.UpdatedAt = now;
            payment.UpdatedBy = UserName;

            // Update customer outstanding balance using pre-computed amount
            var bouncedCustomer = await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);
            if (bouncedCustomer != null)
            {
                bouncedCustomer.OutstandingBalance = (bouncedCustomer.OutstandingBalance ?? 0m) + reversedAmount;
                bouncedCustomer.UpdatedAt = now;
                bouncedCustomer.UpdatedBy = UserName;
                await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(bouncedCustomer);
            }

            await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Bounced payment {PaymentNumber}. Reason: {Reason}", payment.PaymentNumber, reason);

            return MapToDto(payment);
        }

        public async Task<List<PaymentAllocationDto>> GetPaymentAllocationsAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == paymentId);
            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{paymentId}' not found.");

            await _financeAccessScopeService.EnsureBankAccountAccessAsync(
                payment.BankAccountId,
                FinanceAccessLevel.Read,
                cancellationToken);

            var allocations = await _unitOfWork.Repository<PaymentAllocation>()
                .GetQueryable(a => a.TenantId == TenantId && a.CustomerPaymentId == paymentId)
                .Include(a => a.Invoice)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync(cancellationToken);

            return allocations.Select(a => new PaymentAllocationDto
            {
                Id = a.Id,
                CustomerPaymentId = a.CustomerPaymentId,
                InvoiceId = a.InvoiceId,
                InvoiceNumber = a.Invoice.InvoiceNumber,
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
                VatWithholdingAmount = a.VatWithholdingAmount,
                VatWithholdingFunctionalAmount = a.VatWithholdingFunctionalAmount,
                AllocationDate = a.AllocationDate,
                Notes = a.Notes,
                IsReversal = a.IsReversal,
                OriginalAllocationId = a.OriginalAllocationId
            }).ToList();
        }

        public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var invoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.BusinessPartnerId == customerId &&
                    (i.TotalAmount - i.PaidAmount) > 0 &&
                    (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue) &&
                    (!i.IsOpeningBalance || i.JournalEntryId.HasValue))
                .OrderBy(i => i.InvoiceDate)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;

            return invoices.Select(i =>
            {
                var discountAvailable = i.EarlyPaymentDiscountPercentage > 0
                    && i.EarlyPaymentDiscountDueDate.HasValue
                    && i.EarlyPaymentDiscountDueDate.Value.Date >= now.Date;
                var discountAmount = discountAvailable
                    ? Math.Round(i.BalanceAmount * (i.EarlyPaymentDiscountPercentage / 100m), 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new OutstandingInvoiceDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    TotalAmount = i.TotalAmount,
                    PaidAmount = i.PaidAmount,
                    BalanceAmount = i.BalanceAmount,
                    DaysOverdue = i.DueDate.HasValue && i.DueDate.Value < now
                        ? (now - i.DueDate.Value).Days
                        : 0,
                    CurrencyCode = i.CurrencyCode,
                    EarlyPaymentDiscountPercentage = i.EarlyPaymentDiscountPercentage,
                    EarlyPaymentDiscountDueDate = i.EarlyPaymentDiscountDueDate,
                    IsDiscountAvailable = discountAvailable,
                    DiscountAmount = discountAmount
                };
            }).ToList();
        }

        public async Task<byte[]> GeneratePaymentReceiptAsync(Guid id, string format = "PDF", CancellationToken cancellationToken = default)
        {
            // Placeholder - would integrate with a PDF generation library
            await Task.CompletedTask;
            throw new NotImplementedException("Payment receipt printing will be implemented with a PDF library.");
        }

        public Task<CustomerPaymentDto> CreateCreditNoteAsync(CreditNoteCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Keep the compatibility endpoint long enough to return a clear domain error instead
            // of silently producing a weaker document. The read DTO still exposes IsCreditNote so
            // imported/history rows can be inspected without granting them a mutation lifecycle.
            throw new InvalidOperationException(
                "The legacy AR payment credit-note endpoint is retired. Use the Sales credit-note workflow.");
        }

        private static void EnsureCompatibilityCreditNoteIsReadOnly(CustomerPayment payment)
        {
            if (payment.IsCreditNote)
            {
                throw new InvalidOperationException(
                    "Legacy CustomerPayment credit notes are read-only history. Use the Sales credit-note workflow for new credits or corrections.");
            }
        }

        private async Task<string> GeneratePaymentNumberAsync(bool isCreditNote, CancellationToken cancellationToken)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                isCreditNote ? FinanceDocumentTypes.ARCreditNote : FinanceDocumentTypes.ARPayment,
                TenantId,
                DateTime.UtcNow,
                nameof(CustomerPayment),
                cancellationToken: cancellationToken);
        }

        private async Task<decimal> GetPostedSalesCreditAmountForInvoiceAsync(
            Guid invoiceId,
            CancellationToken cancellationToken)
        {
            var postedCreditNoteIds = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e =>
                    e.TenantId == TenantId &&
                    e.SourceModule == "AR" &&
                    e.SourceDocumentType == "SalesCreditNote" &&
                    e.PostingAction == "Post" &&
                    e.PostingStatus == "Posted" &&
                    e.JournalEntryId.HasValue &&
                    !e.IsDeleted)
                .Select(e => e.SourceDocumentId)
                .ToListAsync(cancellationToken);

            if (postedCreditNoteIds.Count == 0)
                return 0m;

            // Sales CreditNote carries a legacy Sales customer key, so invoice settlement is
            // deliberately resolved from its explicit invoice references and posted event.
            return RoundMoney(await _unitOfWork.Repository<CreditNote>()
                .GetQueryable(c =>
                    c.TenantId == TenantId &&
                    c.CreditNoteStatus == CreditNoteStatus.Applied &&
                    postedCreditNoteIds.Contains(c.Id) &&
                    !c.IsDeleted &&
                    (c.AppliedToInvoiceId == invoiceId ||
                     (!c.AppliedToInvoiceId.HasValue && c.OriginalInvoiceId == invoiceId)))
                .SumAsync(c => c.TotalAmount, cancellationToken));
        }

        private async Task<InvoiceStatus> ResolveInvoiceStatusAfterReceiptReversalAsync(
            Invoice invoice,
            DateTime reversalDate,
            CancellationToken cancellationToken)
        {
            // Posted sales credits also settle an invoice but are not represented by PaidAmount.
            // Recomputing with both sources prevents a receipt reversal from incorrectly reopening
            // an invoice that remains fully or partially settled by a valid credit note.
            var postedSalesCredits = await GetPostedSalesCreditAmountForInvoiceAsync(invoice.Id, cancellationToken);
            var operationalOutstanding = RoundMoney(invoice.TotalAmount - invoice.PaidAmount - postedSalesCredits);
            if (operationalOutstanding <= 0.01m)
                return InvoiceStatus.Paid;
            if (invoice.PaidAmount > 0m || postedSalesCredits > 0m)
                return InvoiceStatus.PartiallyPaid;
            return invoice.DueDate.HasValue && invoice.DueDate.Value.Date < reversalDate.Date
                ? InvoiceStatus.Overdue
                : InvoiceStatus.Sent;
        }

        private async Task<DateTime> ResolveCurrentOpenPostingDateAsync(CancellationToken cancellationToken)
        {
            // Advance application is a new accounting event, so it belongs in the latest period
            // Finance has deliberately left open. Clamping the server date to that period also
            // supports controlled catch-up processing after the calendar month has moved on.
            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.IsOpen &&
                    !item.IsClosed &&
                    !item.IsLocked &&
                    !item.IsDeleted)
                .OrderByDescending(item => item.StartDate)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("No open fiscal period is available for the customer advance application.");
            var today = DateTime.UtcNow.Date;
            if (today < period.StartDate.Date)
                return period.StartDate.Date;
            return today > period.EndDate.Date ? period.EndDate.Date : today;
        }

        private async Task<CustomerPayment> LoadPaymentForPostingAsync(Guid id, CancellationToken cancellationToken)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id && !p.IsDeleted)
                .Include(p => p.BankAccount)
                .Include(p => p.LiquidityAccount)
                .Include(p => p.ConfiguredPaymentMethod)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.Invoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{id}' not found.");

            return payment;
        }

        private async Task<FinancePostingRequestDto> BuildArReceiptPostingRequestAsync(
            CustomerPayment payment,
            CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            if (payment.TenantId != tenantId)
                throw new InvalidOperationException("Customer payment belongs to another tenant.");

            if (payment.IsCreditNote)
                throw new InvalidOperationException("AR credit note posting is not part of the AR receipt posting migration batch.");

            if (string.Equals(payment.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payment.Status, "Bounced", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cancelled or bounced AR receipts cannot be posted.");
            }

            var existingPostedEvent = payment.JournalEntryId.HasValue ||
                await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(e =>
                        e.TenantId == tenantId &&
                        e.SourceDocumentType == "CustomerPayment" &&
                        e.SourceDocumentId == payment.Id &&
                        e.PostingAction == "Post" &&
                        e.PostingStatus == "Posted" &&
                        e.JournalEntryId.HasValue &&
                        !e.IsDeleted)
                    .AnyAsync(cancellationToken);

            if (!existingPostedEvent &&
                !string.Equals(payment.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(payment.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(payment.Status, "Processed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AR receipt workflow approval is not complete.");
            }

            if (payment.TotalAmount <= 0m)
                throw new InvalidOperationException("AR receipt amount must be positive.");

            var customer = await ResolveCustomerForPostingAsync(payment, cancellationToken);
            var activeAllocations = payment.Allocations?
                .Where(a => !a.IsReversal)
                .OrderBy(a => a.AllocationDate)
                .ThenBy(a => a.Id)
                .ToList() ?? new List<PaymentAllocation>();

            var isCustomerAdvance = activeAllocations.Count == 0;
            if (isCustomerAdvance &&
                (payment.WithholdingTaxAmount != 0m || payment.VatWithholdingAmount != 0m))
            {
                throw new InvalidOperationException(
                    "Customer advances cannot include withholding tax. Apply the advance to a posted invoice before recording withholding settlement amounts.");
            }

            foreach (var allocation in activeAllocations)
            {
                if (allocation.TenantId != tenantId || allocation.CustomerPaymentId != payment.Id)
                    throw new InvalidOperationException("AR receipt allocation belongs to another tenant or payment.");

                if (allocation.Invoice == null || allocation.Invoice.TenantId != tenantId)
                    throw new InvalidOperationException("AR receipt allocation references an invoice from another tenant.");

                if (allocation.Invoice.BusinessPartnerId != payment.CustomerId)
                    throw new InvalidOperationException("AR receipt allocation references an invoice for another customer.");

                if (allocation.AllocatedAmount < 0m || allocation.DiscountAmount < 0m ||
                    allocation.WithholdingTaxAmount < 0m || allocation.VatWithholdingAmount < 0m)
                    throw new InvalidOperationException("AR receipt allocation amounts cannot be negative.");

                if (!allocation.Invoice.JournalEntryId.HasValue)
                    throw new InvalidOperationException($"AR receipt cannot settle unposted invoice '{allocation.Invoice.InvoiceNumber}'.");

                var invoicePostingExists = await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(e =>
                        e.TenantId == tenantId &&
                        e.SourceDocumentType == "CustomerInvoice" &&
                        e.SourceDocumentId == allocation.InvoiceId &&
                        e.PostingAction == "Post" &&
                        e.PostingStatus == "Posted" &&
                        e.JournalEntryId.HasValue &&
                        !e.IsDeleted)
                    .AnyAsync(cancellationToken);

                if (!invoicePostingExists)
                    throw new InvalidOperationException($"AR receipt cannot settle invoice '{allocation.Invoice.InvoiceNumber}' because its central posting event was not found.");

                var totalInvoiceSettlement = await _unitOfWork.Repository<PaymentAllocation>()
                    .GetQueryable(a =>
                        a.TenantId == tenantId &&
                        a.InvoiceId == allocation.InvoiceId &&
                        !a.IsReversal &&
                        !a.IsDeleted)
                    .SumAsync(a =>
                        a.AllocatedAmount + a.DiscountAmount + a.WithholdingTaxAmount + a.VatWithholdingAmount,
                        cancellationToken);
                var postedSalesCreditTotal = await GetPostedSalesCreditAmountForInvoiceAsync(
                    allocation.InvoiceId,
                    cancellationToken);

                if (RoundMoney(totalInvoiceSettlement + postedSalesCreditTotal) > RoundMoney(allocation.Invoice.TotalAmount))
                    throw new InvalidOperationException($"AR receipt would over-settle invoice '{allocation.Invoice.InvoiceNumber}'.");
            }

            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            NormalizeAndValidateAllocationCurrencyEvidence(
                payment,
                activeAllocations,
                functionalCurrency);
            var exchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
            var accountCache = new Dictionary<Guid, Account>();

            var arAccountId = customer.DefaultArAccountId
                ?? settings.ControlAccountArId
                ?? throw new InvalidOperationException("AR control account is not configured for this tenant.");
            await ResolveReceiptPostingAccountAsync(arAccountId, "AR control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            Guid creditAccountId = arAccountId;
            if (isCustomerAdvance)
            {
                creditAccountId = settings.CustomerAdvanceAccountId
                    ?? throw new InvalidOperationException("Customer advance account is not configured for this tenant.");
                var customerAdvanceAccount = await ResolveReceiptPostingAccountAsync(
                    creditAccountId,
                    "customer advance account",
                    accountCache,
                    allowControlAccount: false,
                    requireDirectPosting: true,
                    cancellationToken);
                if (customerAdvanceAccount.AccountType != AccountType.Liability)
                    throw new InvalidOperationException("Customer advance account must be a liability account.");
            }

            Guid receiptDebitAccountId;
            string receiptDebitTag;
            if (payment.LiquidityAccountId.HasValue)
            {
                var liquidityAccount = await _unitOfWork.Repository<LiquidityAccount>()
                    .GetQueryable(a =>
                        a.TenantId == tenantId &&
                        a.Id == payment.LiquidityAccountId.Value &&
                        a.IsActive &&
                        !a.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The selected receipt holding account was not found or is inactive.");
                if (!liquidityAccount.Currency.Equals(paymentCurrency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Receipt and holding-account currencies must match.");
                receiptDebitAccountId = liquidityAccount.GLAccountId;
                receiptDebitTag = "AR-Liquidity";
                payment.BankAccountId = null;
            }
            else
            {
                var bankAccountId = payment.BankAccountId
                    ?? settings.DefaultBankAccountId
                    ?? throw new InvalidOperationException("A bank account is required for a direct-bank AR receipt.");
                var bankAccount = await _unitOfWork.Repository<BankAccount>()
                    .GetQueryable(a => a.TenantId == tenantId && a.Id == bankAccountId && !a.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken);

                if (bankAccount == null)
                    throw new InvalidOperationException("AR receipt bank account was not found for this tenant.");
                if (!bankAccount.IsActive)
                    throw new InvalidOperationException($"AR receipt bank account '{bankAccount.AccountName}' is inactive.");
                if (!bankAccount.GLAccountId.HasValue)
                    throw new InvalidOperationException($"AR receipt bank account '{bankAccount.AccountName}' is not linked to a GL account.");
                if (!bankAccount.Currency.Equals(paymentCurrency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Receipt and bank-account currencies must match.");

                receiptDebitAccountId = bankAccount.GLAccountId.Value;
                receiptDebitTag = "AR-Bank";
                payment.BankAccountId = bankAccount.Id;
                payment.LiquidityAccountId = null;
            }

            await ResolveReceiptPostingAccountAsync(
                receiptDebitAccountId,
                payment.LiquidityAccountId.HasValue ? "liquidity control account" : "bank account",
                accountCache,
                allowControlAccount: true,
                requireDirectPosting: false,
                cancellationToken);

            var discountAllowed = RoundMoney(activeAllocations.Sum(a => a.DiscountFunctionalAmount));
            var withholdingTaxAmount = RoundMoney(activeAllocations.Sum(a => a.WithholdingTaxFunctionalAmount));
            var vatWithholdingAmount = RoundMoney(activeAllocations.Sum(a => a.VatWithholdingFunctionalAmount));
            if (withholdingTaxAmount < 0m || vatWithholdingAmount < 0m)
                throw new InvalidOperationException("AR receipt withholding amounts cannot be negative.");

            // Validate in functional currency because native receipt cash, invoice reductions and
            // statutory deductions may all use different currencies. Each allocation already
            // freezes the approved conversion used in this comparison.
            var allocatedSettlementFunctionalAmount = RoundMoney(activeAllocations.Sum(a => a.SettlementFunctionalAmount));
            var expectedSettlementFunctionalAmount = RoundMoney(
                RoundMoney(payment.TotalAmount * exchangeRate) + discountAllowed + withholdingTaxAmount + vatWithholdingAmount);
            if (!isCustomerAdvance && allocatedSettlementFunctionalAmount != expectedSettlementFunctionalAmount)
                throw new InvalidOperationException("AR receipt allocations do not reconcile to cash and line-scoped deductions in functional currency.");

            payment.WithholdingTaxAmount = withholdingTaxAmount;
            payment.VatWithholdingAmount = vatWithholdingAmount;

            var postingLines = new List<FinancePostingLineDto>();
            var lineNumber = 1;

            postingLines.Add(BuildPostingLine(
                receiptDebitAccountId,
                $"AR receipt {payment.PaymentNumber}",
                debitTransactionAmount: payment.TotalAmount,
                creditTransactionAmount: 0m,
                paymentCurrency,
                functionalCurrency,
                exchangeRate,
                payment.PaymentDate,
                payment.PaymentNumber,
                lineNumber++,
                    receiptDebitTag,
                    payment.ExchangeRateId));

            if (withholdingTaxAmount > 0m)
            {
                var withholdingAccountId = payment.WithholdingTaxAccountId
                    ?? await ResolveConfiguredWithholdingReceivableAccountAsync(payment.PaymentDate, payment.WithholdingTaxId, TaxCategory.Withholding, cancellationToken)
                    ?? throw new InvalidOperationException("AR withholding tax receivable account is not configured for this tenant.");
                await ResolveReceiptPostingAccountAsync(withholdingAccountId, "withholding tax receivable account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

                foreach (var allocation in activeAllocations.Where(a => a.WithholdingTaxAmount > 0m))
                    postingLines.Add(BuildPostingLine(
                        withholdingAccountId,
                        $"Withholding tax receivable - {payment.PaymentNumber} / {allocation.Invoice.InvoiceNumber}",
                        debitTransactionAmount: allocation.WithholdingTaxAmount,
                        creditTransactionAmount: 0m,
                        allocation.InvoiceCurrencyCode,
                        functionalCurrency,
                        allocation.InvoiceSettlementExchangeRate,
                        payment.PaymentDate,
                        payment.PaymentNumber,
                        lineNumber++,
                        "AR-WHT",
                        allocation.InvoiceSettlementExchangeRateId,
                        functionalDebitOverride: allocation.WithholdingTaxFunctionalAmount));
            }

            if (vatWithholdingAmount > 0m)
            {
                var vatWithholdingAccountId = payment.VatWithholdingAccountId
                    ?? await ResolveConfiguredWithholdingReceivableAccountAsync(payment.PaymentDate, payment.VatWithholdingTaxId, TaxCategory.VatWithholding, cancellationToken)
                    ?? throw new InvalidOperationException("AR VAT withholding receivable account is not configured for this tenant.");
                await ResolveReceiptPostingAccountAsync(vatWithholdingAccountId, "VAT withholding receivable account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

                foreach (var allocation in activeAllocations.Where(a => a.VatWithholdingAmount > 0m))
                    postingLines.Add(BuildPostingLine(
                        vatWithholdingAccountId,
                        $"VAT withholding receivable - {payment.PaymentNumber} / {allocation.Invoice.InvoiceNumber}",
                        debitTransactionAmount: allocation.VatWithholdingAmount,
                        creditTransactionAmount: 0m,
                        allocation.InvoiceCurrencyCode,
                        functionalCurrency,
                        allocation.InvoiceSettlementExchangeRate,
                        payment.PaymentDate,
                        payment.PaymentNumber,
                        lineNumber++,
                        "AR-VAT-WHT",
                        allocation.InvoiceSettlementExchangeRateId,
                        functionalDebitOverride: allocation.VatWithholdingFunctionalAmount));
            }

            if (discountAllowed > 0m)
            {
                var discountAccountId = settings.DiscountAllowedAccountId
                    ?? throw new InvalidOperationException("Sales discounts allowed account is not configured for this tenant.");
                await ResolveReceiptPostingAccountAsync(discountAccountId, "sales discount allowed account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                foreach (var allocation in activeAllocations.Where(a => a.DiscountAmount > 0m))
                    postingLines.Add(BuildPostingLine(
                        discountAccountId,
                        $"Sales discount allowed - {payment.PaymentNumber} / {allocation.Invoice.InvoiceNumber}",
                        debitTransactionAmount: allocation.DiscountAmount,
                        creditTransactionAmount: 0m,
                        allocation.InvoiceCurrencyCode,
                        functionalCurrency,
                        allocation.InvoiceSettlementExchangeRate,
                        payment.PaymentDate,
                        payment.PaymentNumber,
                        lineNumber++,
                        "AR-Discount",
                        allocation.InvoiceSettlementExchangeRateId,
                        functionalDebitOverride: allocation.DiscountFunctionalAmount));
            }

            if (isCustomerAdvance)
            {
                postingLines.Add(BuildPostingLine(
                    creditAccountId,
                    isCustomerAdvance
                        ? $"Customer advance {payment.PaymentNumber}"
                        : $"AR receipt {payment.PaymentNumber}",
                    debitTransactionAmount: 0m,
                    creditTransactionAmount: payment.TotalAmount,
                    paymentCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    lineNumber++,
                    "AR-CustomerAdvance",
                    payment.ExchangeRateId));
            }
            else
            {
                foreach (var allocation in activeAllocations)
                {
                    var invoiceGrossAmount = RoundMoney(
                        allocation.AllocatedAmount + allocation.DiscountAmount +
                        allocation.WithholdingTaxAmount + allocation.VatWithholdingAmount);
                    var settlementFunctionalAmount = RoundMoney(allocation.SettlementFunctionalAmount);
                    var effectiveControlRate = invoiceGrossAmount <= 0m
                        ? 1m
                        : decimal.Round(settlementFunctionalAmount / invoiceGrossAmount, 6, MidpointRounding.AwayFromZero);

                    // Preserve the invoice's native clearing quantity while crediting AR by the
                    // actual functional value received. The linked realized-FX event subsequently
                    // adjusts AR back to the invoice's historical carrying value.
                    postingLines.Add(BuildPostingLine(
                        creditAccountId,
                        $"AR settlement {payment.PaymentNumber} / {allocation.Invoice.InvoiceNumber}",
                        debitTransactionAmount: 0m,
                        creditTransactionAmount: invoiceGrossAmount,
                        allocation.InvoiceCurrencyCode,
                        functionalCurrency,
                        effectiveControlRate,
                        payment.PaymentDate,
                        payment.PaymentNumber,
                        lineNumber++,
                        "AR-Control",
                        exchangeRateId: null,
                        functionalCreditOverride: settlementFunctionalAmount));
                }
            }

            if (RoundMoney(postingLines.Sum(l => l.DebitAmount)) != RoundMoney(postingLines.Sum(l => l.CreditAmount)))
                throw new InvalidOperationException("AR receipt posting is not balanced.");

            payment.IsCustomerAdvance = isCustomerAdvance;

            return new FinancePostingRequestDto
            {
                SourceModule = "AR",
                SourceDocumentType = "CustomerPayment",
                SourceDocumentId = payment.Id,
                SourceDocumentTenantId = payment.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = payment.PaymentNumber,
                Description = isCustomerAdvance
                    ? $"Customer advance {payment.PaymentNumber} - {customer.PartnerName}"
                    : $"Customer receipt {payment.PaymentNumber} - {customer.PartnerName}",
                PostingDate = payment.PaymentDate,
                JournalType = "AR Receipt",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"AR:CustomerPayment:{payment.TenantId:N}:{payment.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines
            };
        }

        private async Task PostRealizedFxIfRequiredAsync(
            CustomerPayment payment,
            CancellationToken cancellationToken)
        {
            if (payment.IsCreditNote)
            {
                return;
            }

            var functionalCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync(), "GHS");
            // Foreign receipt currency alone does not create a realized AR settlement. There must
            // be a foreign invoice carrying amount to compare. This deliberately excludes both an
            // unapplied customer-advance lot and settlement of a functional-currency invoice;
            // advance-application FX is recognized by the later application journal instead.
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
                throw new InvalidOperationException("FX accounting service is not configured for AR realized FX settlement posting.");
            }

            await _fxAccountingService.PostRealizedFxForArReceiptAsync(payment.Id, cancellationToken);
        }

        /// <summary>
        /// Recomputes receipt allocation evidence from native cash/deduction values and the
        /// frozen rates before posting. Cross-currency evidence must already be complete and
        /// immutable. Missing same-currency functional values may be populated because they are
        /// deterministic and preserve the established pre-FIN-LIM-0022 receipt workflow.
        /// </summary>
        private static void NormalizeAndValidateAllocationCurrencyEvidence(
            CustomerPayment payment,
            IReadOnlyCollection<PaymentAllocation> allocations,
            string functionalCurrency)
        {
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            foreach (var allocation in allocations)
            {
                var invoiceCurrency = NormalizeCurrency(allocation.Invoice?.CurrencyCode, functionalCurrency);
                var storedInvoiceCurrency = NormalizeCurrency(allocation.InvoiceCurrencyCode, invoiceCurrency);
                var storedPaymentCurrency = NormalizeCurrency(allocation.PaymentCurrencyCode, paymentCurrency);
                if (!string.Equals(storedInvoiceCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(storedPaymentCurrency, paymentCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"AR allocation '{allocation.Id}' currency evidence does not match its receipt and invoice.");
                }

                var paymentRequiresFx = !string.Equals(
                    paymentCurrency,
                    functionalCurrency,
                    StringComparison.OrdinalIgnoreCase);
                var invoiceRequiresFx = !string.Equals(
                    invoiceCurrency,
                    functionalCurrency,
                    StringComparison.OrdinalIgnoreCase);
                // Foreign snapshots are immutable audit evidence. Do not let the generic rate
                // normalizer turn a missing rate into 1.0. A same-USD receipt/invoice is not
                // cross-currency commercially, but it still requires GHS functional measurement.
                if ((invoiceRequiresFx && allocation.InvoiceSettlementExchangeRate <= 0m) ||
                    (paymentRequiresFx &&
                     allocation.PaymentExchangeRate <= 0m &&
                     payment.ExchangeRate <= 0m))
                {
                    throw new InvalidOperationException(
                        $"AR allocation '{allocation.Id}' is missing its frozen foreign-currency exchange-rate evidence.");
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
                    allocation.VatWithholdingAmount,
                    NormalizeExchangeRate(allocation.PaymentExchangeRate > 0m
                        ? allocation.PaymentExchangeRate
                        : payment.ExchangeRate),
                    NormalizeExchangeRate(allocation.InvoiceSettlementExchangeRate));

                var mayNormalizeSameCurrency = !expected.IsCrossCurrency;
                allocation.PaymentCurrencyAmount = NormalizeEvidenceAmount(
                    allocation.PaymentCurrencyAmount,
                    expected.PaymentCurrencyAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "receipt-currency amount");
                allocation.PaymentFunctionalAmount = NormalizeEvidenceAmount(
                    allocation.PaymentFunctionalAmount,
                    expected.PaymentFunctionalAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "receipt functional amount");
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
                allocation.VatWithholdingFunctionalAmount = NormalizeEvidenceAmount(
                    allocation.VatWithholdingFunctionalAmount,
                    expected.VatWithholdingFunctionalAmount,
                    mayNormalizeSameCurrency,
                    allocation.Id,
                    "VAT-WHT functional amount");
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
                    $"AR allocation '{allocationId}' {evidenceName} does not reconcile to its frozen currency evidence.");
            }

            return stored;
        }

        private async Task<Guid?> ResolveConfiguredWithholdingReceivableAccountAsync(
            DateTime paymentDate,
            Guid? taxId,
            TaxCategory category,
            CancellationToken cancellationToken)
        {
            var configuredTax = await _unitOfWork.Repository<Tax>()
                .GetQueryable(t =>
                    t.TenantId == TenantId &&
                    !t.IsDeleted &&
                    t.IsActive &&
                    (!taxId.HasValue || t.Id == taxId.Value) &&
                    t.Category == category &&
                    (t.Applicability == TaxApplicability.Sales || t.Applicability == TaxApplicability.Both) &&
                    t.EffectiveFrom.Date <= paymentDate.Date &&
                    t.TaxReceivableAccountId.HasValue)
                .OrderByDescending(t => t.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

            return configuredTax?.TaxReceivableAccountId;
        }

        private async Task<FinancePostingRequestDto> BuildCustomerCreditNotePostingRequestAsync(
            CustomerPayment payment,
            CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            if (payment.TenantId != tenantId)
                throw new InvalidOperationException("Customer credit note belongs to another tenant.");

            if (!payment.IsCreditNote)
                throw new InvalidOperationException("Customer payment is not an AR credit note.");

            var existingPostedEvent = payment.JournalEntryId.HasValue ||
                await _unitOfWork.Repository<FinancePostingEvent>()
                    .GetQueryable(e =>
                        e.TenantId == tenantId &&
                        e.SourceDocumentType == "CustomerCreditNote" &&
                        e.SourceDocumentId == payment.Id &&
                        e.PostingAction == "Post" &&
                        e.PostingStatus == "Posted" &&
                        e.JournalEntryId.HasValue &&
                        !e.IsDeleted)
                    .AnyAsync(cancellationToken);

            if (!existingPostedEvent &&
                !string.Equals(payment.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(payment.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AR credit note workflow approval is not complete.");
            }

            if (payment.TotalAmount <= 0m)
                throw new InvalidOperationException("AR credit note amount must be positive.");

            var customer = await ResolveCustomerForPostingAsync(payment, cancellationToken);
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var creditNoteCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            var exchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
            var accountCache = new Dictionary<Guid, Account>();

            var arAccountId = customer.DefaultArAccountId
                ?? settings.ControlAccountArId
                ?? throw new InvalidOperationException("AR control account is not configured for this tenant.");
            await ResolveReceiptPostingAccountAsync(arAccountId, "AR control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            var salesReturnsAccountId = settings.DiscountAllowedAccountId
                ?? throw new InvalidOperationException("Sales returns/allowance account is not configured for this tenant.");
            await ResolveReceiptPostingAccountAsync(salesReturnsAccountId, "sales returns/allowance account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

            var postingLines = new List<FinancePostingLineDto>
            {
                BuildPostingLine(
                    salesReturnsAccountId,
                    $"Customer credit note {payment.PaymentNumber}",
                    debitTransactionAmount: payment.TotalAmount,
                    creditTransactionAmount: 0m,
                    creditNoteCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    1,
                    "AR-CreditNote-SalesReturn"),
                BuildPostingLine(
                    arAccountId,
                    $"Customer credit note {payment.PaymentNumber}",
                    debitTransactionAmount: 0m,
                    creditTransactionAmount: payment.TotalAmount,
                    creditNoteCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    2,
                    "AR-Control")
            };

            if (RoundMoney(postingLines.Sum(l => l.DebitAmount)) != RoundMoney(postingLines.Sum(l => l.CreditAmount)))
                throw new InvalidOperationException("AR credit note posting is not balanced.");

            return new FinancePostingRequestDto
            {
                SourceModule = "AR",
                SourceDocumentType = "CustomerCreditNote",
                SourceDocumentId = payment.Id,
                SourceDocumentTenantId = payment.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = payment.PaymentNumber,
                Description = $"Customer credit note {payment.PaymentNumber} - {customer.PartnerName}",
                PostingDate = payment.PaymentDate,
                JournalType = "AR Credit Note",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"AR:CustomerCreditNote:{payment.TenantId:N}:{payment.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines
            };
        }

        private async Task<BusinessPartner> ResolveCustomerForPostingAsync(CustomerPayment payment, CancellationToken cancellationToken)
        {
            var customer = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.Id == payment.CustomerId &&
                    !p.IsDeleted &&
                    (p.PartnerType == "Customer" || p.PartnerType == "Both"))
                .FirstOrDefaultAsync(cancellationToken);

            if (customer == null)
                throw new InvalidOperationException("AR receipt customer was not found for this tenant.");

            if (!customer.IsActive || customer.IsBlacklisted || !string.Equals(customer.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Customer '{customer.PartnerName}' is not active for AR receipt posting.");

            return customer;
        }

        private async Task<FinanceSettings> GetFinanceSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            return settings ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        }

        private async Task<Account> ResolveReceiptPostingAccountAsync(
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
                throw new InvalidOperationException($"AR receipt posting {role} was not found for this tenant.");

            if (account.Status != AccountStatus.Active)
                throw new InvalidOperationException($"AR receipt posting {role} account '{account.AccountNumber}' is not active.");

            if (account.IsControlAccount && !allowControlAccount)
                throw new InvalidOperationException($"AR receipt posting {role} account '{account.AccountNumber}' is a control account and cannot be used for this line.");

            if (requireDirectPosting && !account.AllowDirectPosting)
                throw new InvalidOperationException($"AR receipt posting {role} account '{account.AccountNumber}' does not allow direct posting.");

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
                ExchangeRateSource = isForeign ? "AR receipt exchange-rate snapshot" : null,
                ExchangeRateDate = isForeign ? exchangeRateDate.Date : null,
                SourceReferenceNumber = reference,
                LineNumber = lineNumber,
                TransactionTag = transactionTag
            };
        }

        private async Task RecordArReceiptAuditAsync(
            string eventType,
            CustomerPayment payment,
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
                SourceModule = "AR",
                SourceDocumentType = "CustomerPayment",
                SourceDocumentId = payment.Id,
                JournalEntryId = journalEntryId ?? payment.JournalEntryId,
                PostingEventId = postingEventId,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Comment = comment,
                Resource = "Finance.ARReceipt",
                ResourceId = payment.Id.ToString()
            }, cancellationToken);
        }

        private async Task RecordArCreditNoteAuditAsync(
            string eventType,
            CustomerPayment payment,
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
                SourceModule = "AR",
                SourceDocumentType = "CustomerCreditNote",
                SourceDocumentId = payment.Id,
                JournalEntryId = journalEntryId ?? payment.JournalEntryId,
                PostingEventId = postingEventId,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Comment = comment,
                Resource = "Finance.ARCreditNote",
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
        /// Resolves the approved rate evidence used by an AR receipt and its allocations. Keeping
        /// this validation at the service boundary prevents a browser-supplied decimal from
        /// bypassing tenant rate governance in cross-currency settlement.
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
                    throw new InvalidOperationException("A functional-currency AR settlement must not specify an exchange-rate id.");
                return new SettlementRateSnapshot(null, 1m);
            }

            if (_exchangeRateService == null)
            {
                if (requireApprovedSource)
                    throw new InvalidOperationException("Approved exchange-rate resolution is not configured for foreign-currency AR settlement.");
                return new SettlementRateSnapshot(null, NormalizeExchangeRate(fallbackRate));
            }

            var quoteSide = await GetSettlementQuoteSideAsync(cancellationToken);
            var rate = requestedRateId.HasValue
                ? await _exchangeRateService.GetExchangeRateByIdAsync(requestedRateId.Value, cancellationToken)
                : await _exchangeRateService.GetCurrentRateAsync(
                    transactionCurrency,
                    functionalCurrency,
                    settlementDate,
                    "Daily",
                    quoteSide.ToString(),
                    cancellationToken);

            if (rate == null || !rate.IsActive || rate.Rate <= 0m ||
                !(rate.ApprovalStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
                  rate.ApprovalStatus.Equals("AutoApproved", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"No active approved daily {quoteSide} rate exists for {transactionCurrency} to {functionalCurrency} on {settlementDate:yyyy-MM-dd}.");

            if (!rate.BaseCurrencyCode.Equals(functionalCurrency, StringComparison.OrdinalIgnoreCase) ||
                !rate.TargetCurrencyCode.Equals(transactionCurrency, StringComparison.OrdinalIgnoreCase) ||
                !rate.RateType.Equals("Daily", StringComparison.OrdinalIgnoreCase) ||
                !rate.QuoteSide.Equals(quoteSide.ToString(), StringComparison.OrdinalIgnoreCase) ||
                rate.EffectiveDate.Date > settlementDate.Date ||
                (rate.ExpiryDate.HasValue && rate.ExpiryDate.Value.Date < settlementDate.Date))
                throw new InvalidOperationException($"The selected AR settlement rate does not match the required currency pair, date, daily rate type, or {quoteSide} quote policy.");

            return new SettlementRateSnapshot(rate.Id, NormalizeExchangeRate(rate.Rate));
        }

        private async Task<ExchangeRateQuoteSide> GetSettlementQuoteSideAsync(CancellationToken cancellationToken)
        {
            if (_settlementQuoteSide.HasValue)
                return _settlementQuoteSide.Value;

            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && !item.IsDeleted);
            _settlementQuoteSide = settings?.DirectionalExchangeRatePolicyEnabled == true
                ? settings.ArSettlementQuoteSide
                : ExchangeRateQuoteSide.Mid;
            return _settlementQuoteSide.Value;
        }

        private static string NormalizeCurrency(string? currencyCode, string defaultValue)
            => string.IsNullOrWhiteSpace(currencyCode)
                ? defaultValue.Trim().ToUpperInvariant()
                : currencyCode.Trim().ToUpperInvariant();

        private sealed record SettlementRateSnapshot(Guid? Id, decimal Rate);

        private static decimal RoundMoney(decimal amount)
            => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Returns the immutable allocation facts that remain economically active. Posted advance
        /// application reversals are represented by linked negative rows, so filtering only on
        /// IsReversal would incorrectly continue consuming the original currency lot.
        /// </summary>
        private static List<PaymentAllocation> GetEffectiveAllocations(
            IEnumerable<PaymentAllocation>? allocations)
        {
            var live = allocations?.Where(item => !item.IsDeleted).ToList()
                       ?? new List<PaymentAllocation>();
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

        private async Task<BusinessPartner?> GetCustomerPartnerAsync(Guid customerId, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == TenantId &&
                    p.Id == customerId &&
                    !p.IsDeleted &&
                    (p.PartnerType == "Customer" || p.PartnerType == "Both"));
        }

        private async Task CreateCashTransactionForReceiptAsync(CustomerPayment payment, BusinessPartner customer, CancellationToken cancellationToken)
        {
            if (payment.IsCreditNote)
            {
                return;
            }

            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .GetQueryable(s => s.TenantId == TenantId && !s.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            var bankAccountId = payment.BankAccountId ?? settings?.DefaultBankAccountId;
            if (!bankAccountId.HasValue)
            {
                return;
            }

            var cashTransactionRepository = _unitOfWork.Repository<CashTransaction>();
            var exists = await cashTransactionRepository
                .GetQueryable(t =>
                    !t.IsDeleted &&
                    t.TransactionType == CashTransactionType.Receipt &&
                    t.BankAccountId == bankAccountId.Value &&
                    t.ReferenceNumber == payment.PaymentNumber)
                .AnyAsync(cancellationToken);

            if (exists)
            {
                return;
            }

            var bankAccountRepository = _unitOfWork.Repository<BankAccount>();
            var bankAccount = await bankAccountRepository
                .GetQueryable(a => a.Id == bankAccountId.Value && !a.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Bank account not found for AR customer payment.");

            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var currencyCode = string.IsNullOrWhiteSpace(payment.CurrencyCode)
                ? baseCurrencyCode
                : payment.CurrencyCode.Trim().ToUpperInvariant();
            var exchangeRate = payment.ExchangeRate <= 0m ? 1m : payment.ExchangeRate;
            var baseAmount = decimal.Round(payment.TotalAmount * exchangeRate, 2, MidpointRounding.AwayFromZero);
            var arAccountId = customer.DefaultArAccountId ?? settings?.ControlAccountArId;

            var transactionNumber = await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.CashReceipt,
                TenantId,
                payment.PaymentDate,
                nameof(CashTransaction),
                cancellationToken: cancellationToken);

            await cashTransactionRepository.AddAsync(new CashTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TransactionNumber = transactionNumber,
                TransactionDate = payment.PaymentDate,
                TransactionType = CashTransactionType.Receipt,
                BankAccountId = bankAccountId.Value,
                Amount = payment.TotalAmount,
                Currency = currencyCode,
                ExchangeRate = exchangeRate,
                BaseAmount = baseAmount,
                PaymentMethodId = payment.PaymentMethodId,
                ReferenceNumber = payment.PaymentNumber,
                PayeeOrPayer = customer.PartnerName,
                Description = $"AR Customer Payment {payment.PaymentNumber} - {customer.PartnerName}",
                GLAccountId = arAccountId,
                IsReconciled = false,
                IsPosted = true,
                ApprovalStatus = CashTransactionApprovalStatus.Posted,
                JournalEntryId = payment.JournalEntryId,
                PostedDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            });

            payment.BankAccountId ??= bankAccountId.Value;
            bankAccount.CurrentBalance += payment.TotalAmount;
            bankAccount.AvailableBalance += payment.TotalAmount;
            bankAccount.UpdatedAt = DateTime.UtcNow;
            bankAccount.UpdatedBy = UserName;
            await bankAccountRepository.UpdateAsync(bankAccount);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task CreateLiquidityEntryForReceiptAsync(
            CustomerPayment payment,
            BusinessPartner customer,
            CancellationToken cancellationToken)
        {
            if (payment.IsCreditNote || !payment.LiquidityAccountId.HasValue)
            {
                return;
            }

            var repository = _unitOfWork.Repository<LiquidityAccountEntry>();
            var existing = await repository
                .GetQueryable(entry =>
                    entry.TenantId == TenantId &&
                    entry.SourceDocumentType == nameof(CustomerPayment) &&
                    entry.SourceDocumentId == payment.Id &&
                    !entry.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing != null)
            {
                payment.LiquidityAccountEntryId ??= existing.Id;
                return;
            }

            var liquidityAccount = await _unitOfWork.Repository<LiquidityAccount>()
                .GetQueryable(account =>
                    account.TenantId == TenantId &&
                    account.Id == payment.LiquidityAccountId.Value &&
                    account.IsActive &&
                    !account.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Receipt holding account was not found.");
            var currency = NormalizeCurrency(payment.CurrencyCode, await _tenantSettingsService.GetBaseCurrencyAsync());
            if (!liquidityAccount.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Receipt and holding-account currencies must match.");
            }

            var entryNumber = await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.LiquidityEntry,
                TenantId,
                payment.PaymentDate,
                nameof(LiquidityAccountEntry),
                cancellationToken: cancellationToken);
            var entry = new LiquidityAccountEntry
            {
                TenantId = TenantId,
                LiquidityAccountId = liquidityAccount.Id,
                EntryNumber = entryNumber,
                EntryDate = payment.PaymentDate,
                EntryType = LiquidityEntryType.CustomerReceipt,
                Direction = LiquidityEntryDirection.Increase,
                Amount = payment.TotalAmount,
                AllocatedAmount = 0m,
                Currency = currency,
                SourceDocumentType = nameof(CustomerPayment),
                SourceDocumentId = payment.Id,
                ReferenceNumber = payment.CheckNumber ?? payment.TransactionReference ?? payment.PaymentNumber,
                CounterpartyName = customer.PartnerName,
                Description = $"{payment.PaymentMethod} receipt {payment.PaymentNumber}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };
            await repository.AddAsync(entry);
            payment.LiquidityAccountEntryId = entry.Id;
            payment.BankAccountId = null;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.UpdatedBy = UserName;
            await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
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

            if (enforceReference && method.RequiresReference && string.IsNullOrWhiteSpace(referenceNumber))
            {
                throw new InvalidOperationException($"The selected {label} payment method requires a reference number.");
            }

            return method;
        }

        private async Task<ReceiptDestination> ResolveReceiptDestinationAsync(
            PaymentMethodType? configuredType,
            string? paymentMethod,
            Guid? bankAccountId,
            Guid? liquidityAccountId,
            string currency,
            CancellationToken cancellationToken)
        {
            var normalizedCurrency = NormalizeCurrency(currency, await _tenantSettingsService.GetBaseCurrencyAsync());
            var isDirectBank = configuredType is PaymentMethodType.EFT
                or PaymentMethodType.DirectDebit
                or PaymentMethodType.StandingOrder
                or PaymentMethodType.BankTransfer;
            if (!configuredType.HasValue)
            {
                var normalizedMethod = (paymentMethod ?? string.Empty)
                    .Replace(" ", string.Empty, StringComparison.Ordinal)
                    .Replace("-", string.Empty, StringComparison.Ordinal)
                    .ToUpperInvariant();
                isDirectBank = normalizedMethod is "BANKTRANSFER" or "EFT" or "DIRECTDEBIT" or "STANDINGORDER";
            }

            if (isDirectBank)
            {
                var settings = await GetFinanceSettingsAsync(cancellationToken);
                var resolvedBankId = bankAccountId ?? settings.DefaultBankAccountId
                    ?? throw new InvalidOperationException("Select a bank account for a direct bank receipt.");
                var bank = await _unitOfWork.Repository<BankAccount>()
                    .GetQueryable(item =>
                        item.TenantId == TenantId &&
                        item.Id == resolvedBankId &&
                        item.IsActive &&
                        !item.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("The selected bank account was not found or is inactive.");
                if (!bank.Currency.Equals(normalizedCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Receipt and bank-account currencies must match.");
                }
                return new ReceiptDestination(bank.Id, null);
            }

            var targetType = configuredType switch
            {
                PaymentMethodType.Cheque => LiquidityAccountType.ChequesAwaitingDeposit,
                PaymentMethodType.Card => LiquidityAccountType.CardSettlementClearing,
                PaymentMethodType.MobileMoney => LiquidityAccountType.MobileMoneyClearing,
                PaymentMethodType.Cash => LiquidityAccountType.UndepositedCash,
                _ => InferLiquidityType(paymentMethod)
            };
            var liquidityQuery = _unitOfWork.Repository<LiquidityAccount>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.Currency == normalizedCurrency &&
                    item.IsActive &&
                    !item.IsDeleted);
            var account = liquidityAccountId.HasValue
                ? await liquidityQuery.FirstOrDefaultAsync(item => item.Id == liquidityAccountId.Value, cancellationToken)
                : await liquidityQuery
                    .OrderByDescending(item => item.AccountType == targetType)
                    .ThenBy(item => item.Code)
                    .FirstOrDefaultAsync(item => item.AccountType == targetType, cancellationToken);
            if (account == null)
            {
                throw new InvalidOperationException(
                    $"No active {targetType} holding account exists for {normalizedCurrency}. Complete Banking & Settlement setup first.");
            }
            if (account.AccountType != targetType &&
                account.AccountType != LiquidityAccountType.OtherSettlementClearing &&
                // A physical till is a valid explicit destination only for cash. It is never
                // selected implicitly, which preserves the general undeposited-cash queue for
                // receipts captured outside a cashier custody session.
                !(targetType == LiquidityAccountType.UndepositedCash &&
                  account.AccountType == LiquidityAccountType.CashTill))
            {
                throw new InvalidOperationException(
                    $"The selected holding account is not suitable for {paymentMethod ?? configuredType?.ToString() ?? "this payment method"}.");
            }
            if (account.AccountType == LiquidityAccountType.CashTill)
            {
                var hasCashierCustody = CurrentUserId != Guid.Empty &&
                    await _unitOfWork.Repository<CashierTillSession>()
                        .GetQueryable(session =>
                            session.TenantId == TenantId &&
                            session.LiquidityAccountId == account.Id &&
                            session.CashierUserId == CurrentUserId &&
                            session.Status == CashierTillSessionStatus.Open &&
                            !session.IsDeleted)
                        .AnyAsync(cancellationToken);
                if (!hasCashierCustody)
                {
                    throw new InvalidOperationException(
                        "Open this cash till under your cashier session before recording a receipt into it.");
                }
            }
            return new ReceiptDestination(null, account.Id);
        }

        private static LiquidityAccountType InferLiquidityType(string? paymentMethod)
        {
            var value = (paymentMethod ?? string.Empty)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
            if (value.Contains("CHEQUE", StringComparison.Ordinal) || value.Contains("CHECK", StringComparison.Ordinal))
                return LiquidityAccountType.ChequesAwaitingDeposit;
            if (value.Contains("MOBILE", StringComparison.Ordinal) || value.Contains("MOMO", StringComparison.Ordinal))
                return LiquidityAccountType.MobileMoneyClearing;
            if (value.Contains("CARD", StringComparison.Ordinal))
                return LiquidityAccountType.CardSettlementClearing;
            return LiquidityAccountType.UndepositedCash;
        }

        private static string MapConfiguredPaymentMethodToCustomerPaymentMethod(PaymentMethodType type)
        {
            return type switch
            {
                PaymentMethodType.Cash => "Cash",
                PaymentMethodType.Cheque => "Cheque",
                PaymentMethodType.EFT => "EFT",
                PaymentMethodType.Card => "Card",
                PaymentMethodType.MobileMoney => "Mobile Money",
                PaymentMethodType.DirectDebit => "Direct Debit",
                PaymentMethodType.StandingOrder => "Standing Order",
                PaymentMethodType.BankTransfer => "Bank Transfer",
                _ => "Other"
            };
        }

        private sealed record ArReceiptPostingOutcome(
            CustomerPayment Payment,
            FinancePostingResultDto PostingResult,
            bool WasAlreadyLinked);

        private sealed record ReceiptDestination(Guid? BankAccountId, Guid? LiquidityAccountId);

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
                        // The posting event is authoritative when an older producer omitted a
                        // line-level transaction currency.
                        TransactionCurrency = item.TransactionCurrency ?? postingEvent.FunctionalCurrencyCode,
                        ForeignCurrencyAmount = item.ForeignCurrencyAmount,
                        ExchangeRate = item.ExchangeRate,
                        OriginalTransactionId = item.OriginalTransactionId,
                        ReversalTransactionId = item.ReversalTransactionId
                    })
                    .ToList() ?? new List<FinanceJournalLineTraceDto>()
            };
        }

        private CustomerPaymentDto MapToDto(CustomerPayment payment, BusinessPartner? customer = null)
        {
            return new CustomerPaymentDto
            {
                Id = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                CustomerId = payment.CustomerId,
                CustomerName = customer?.PartnerName ?? payment.Customer?.CustomerName ?? string.Empty,
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
                LiquidityAccountId = payment.LiquidityAccountId,
                LiquidityAccountName = payment.LiquidityAccount?.Name,
                LiquidityAccountEntryId = payment.LiquidityAccountEntryId,
                CheckNumber = payment.CheckNumber,
                ChequeDrawerBank = payment.ChequeDrawerBank,
                TransactionReference = payment.TransactionReference,
                WithholdingTaxId = payment.WithholdingTaxId,
                WithholdingTaxAccountId = payment.WithholdingTaxAccountId,
                WithholdingTaxAmount = payment.WithholdingTaxAmount,
                VatWithholdingTaxId = payment.VatWithholdingTaxId,
                VatWithholdingAccountId = payment.VatWithholdingAccountId,
                VatWithholdingAmount = payment.VatWithholdingAmount,
                WithholdingCertificateNumber = payment.WithholdingCertificateNumber,
                WithholdingCertificateDate = payment.WithholdingCertificateDate,
                Notes = payment.Notes,
                Status = payment.Status,
                ClearedDate = payment.ClearedDate,
                IsCreditNote = payment.IsCreditNote,
                JournalEntryId = payment.JournalEntryId,
                ReversalJournalEntryId = payment.ReversalJournalEntryId,
                ReversalPostingEventId = payment.ReversalPostingEventId,
                ReversalCashTransactionId = payment.ReversalCashTransactionId,
                ReversalLiquidityAccountEntryId = payment.ReversalLiquidityAccountEntryId,
                ReversalDate = payment.ReversalDate,
                ReversedAt = payment.ReversedAt,
                ReversedById = payment.ReversedById,
                ReversalReason = payment.ReversalReason,
                Allocations = payment.Allocations?.Select(a => new PaymentAllocationDto
                {
                    Id = a.Id,
                    CustomerPaymentId = a.CustomerPaymentId,
                    InvoiceId = a.InvoiceId,
                    InvoiceNumber = a.Invoice?.InvoiceNumber ?? string.Empty,
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
                    VatWithholdingAmount = a.VatWithholdingAmount,
                    VatWithholdingFunctionalAmount = a.VatWithholdingFunctionalAmount,
                    AllocationDate = a.AllocationDate,
                    Notes = a.Notes,
                    IsReversal = a.IsReversal,
                    OriginalAllocationId = a.OriginalAllocationId
                }).ToList() ?? new List<PaymentAllocationDto>(),
                CreatedAt = payment.CreatedAt
            };
        }
    }
}
