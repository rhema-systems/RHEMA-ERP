using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Api.Services.Finance;
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

namespace ErpSystem.Api.Services.Finance.AP
{
    /// <summary>
    /// Manages vendor payments, allocations, early-payment discounts, and payment batches.
    /// </summary>
    public class VendorPaymentService : IVendorPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<VendorPaymentService> _logger;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IWorkflowService _workflowService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IFxAccountingService? _fxAccountingService;
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
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            IFxAccountingService? fxAccountingService = null,
            IVendorInvoiceService? vendorInvoiceService = null,
            IProcurementControlEventService? procurementControlEvents = null,
            IProcurementInvoicePaymentSodService? invoicePaymentSod = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _workflowService = workflowService;
            _financePostingEngine = financePostingEngine;
            _financeAuditService = financeAuditService;
            _fxAccountingService = fxAccountingService;
            _vendorInvoiceService = vendorInvoiceService;
            _procurementControlEvents = procurementControlEvents;
            _invoicePaymentSod = invoicePaymentSod;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";
        private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

        // ═════════════════════════════════════════════════════════════════
        //  GET
        // ═════════════════════════════════════════════════════════════════

        public async Task<VendorPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id)
                .Include(p => p.Supplier)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.VendorInvoice)
                .Include(p => p.BankAccount)
                .Include(p => p.ConfiguredPaymentMethod)
                .FirstOrDefaultAsync(cancellationToken);

            return payment == null ? null : MapToDto(payment);
        }

        public async Task<PagedResult<VendorPaymentDto>> GetAllAsync(VendorPaymentQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId);

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

            var paymentNumber = await GeneratePaymentNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var paymentCurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode)
                ? baseCurrencyCode
                : dto.CurrencyCode.Trim().ToUpperInvariant();
            var configuredPaymentMethod = await ResolveConfiguredPaymentMethodAsync(
                dto.PaymentMethodId,
                dto.BankAccountId,
                dto.TransactionReference ?? dto.ChequeNumber,
                "vendor payment",
                enforceReference: true,
                cancellationToken);
            var paymentMethod = configuredPaymentMethod == null
                ? dto.PaymentMethod
                : MapConfiguredPaymentMethodToVendorPaymentMethod(configuredPaymentMethod.Type);

            // Calculate WHT. Prefer the explicit settlement amount from the payment UI,
            // then allocation-level WHT, then the legacy rate-on-cash fallback.
            var allocationWhtAmount = dto.Allocations?
                .Sum(a => Math.Max(a.WithholdingTaxAmount, 0m)) ?? 0m;
            decimal whtAmount = 0;
            if (dto.WithholdingTaxAmount.GetValueOrDefault() > 0)
            {
                whtAmount = RoundMoney(dto.WithholdingTaxAmount.Value);
            }
            else if (allocationWhtAmount > 0m)
            {
                whtAmount = RoundMoney(allocationWhtAmount);
            }
            else if (dto.WithholdingTaxRate > 0)
            {
                whtAmount = RoundMoney(dto.TotalAmount * (dto.WithholdingTaxRate / 100));
            }

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
                ExchangeRate = dto.ExchangeRate,
                BankAccountId = dto.BankAccountId,
                ChequeNumber = dto.ChequeNumber,
                TransactionReference = dto.TransactionReference,
                WithholdingTaxRate = dto.WithholdingTaxRate,
                WithholdingTaxAmount = whtAmount,
                WithholdingTaxId = dto.WithholdingTaxId,
                WithholdingTaxAccountId = dto.WithholdingTaxAccountId,
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
                    $"tdc0506-payment:{TenantId:N}:{id:N}", cancellationToken);
                var payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                    .Include(item => item.Allocations.Where(allocation => !allocation.IsDeleted && !allocation.IsReversal))
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

                foreach (var allocation in payment.Allocations.OrderBy(item => item.VendorInvoiceId))
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"tdc0505-invoice:{TenantId:N}:{allocation.VendorInvoiceId:N}", cancellationToken);
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

        public async Task<VendorPaymentDto> PostAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP payment posting.");
            if (_invoicePaymentSod == null)
                throw new VendorPaymentControlException(
                    ProcurementInvoicePaymentSodRules.EvidenceCode,
                    "The authoritative invoice/payment SOD service is not configured.");

            await _invoicePaymentSod.RevalidatePaymentAuthorizationAsync(id, cancellationToken);

            var payment = await LoadPaymentForPostingAsync(id, cancellationToken);
            var wasAlreadyLinked = payment.JournalEntryId.HasValue;

            try
            {
                var postingRequest = await BuildApPaymentPostingRequestAsync(payment, cancellationToken);
                var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

                if (payment.JournalEntryId.HasValue && payment.JournalEntryId.Value != postingResult.JournalEntryId)
                    throw new InvalidOperationException("Vendor payment is linked to a different journal entry than the posting engine result.");

                if (!payment.JournalEntryId.HasValue)
                {
                    await ApplyPostedPaymentAllocationsAsync(
                        payment,
                        postingResult.PostingEventId,
                        postingResult.JournalEntryId,
                        cancellationToken);
                    payment.JournalEntryId = postingResult.JournalEntryId;
                    if (payment.Status == VendorPaymentStatus.Authorized || payment.Status == VendorPaymentStatus.PendingAuthorization)
                    {
                        payment.Status = VendorPaymentStatus.Processed;
                    }
                    payment.UpdatedAt = DateTime.UtcNow;
                    payment.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                if (postingResult.WasDuplicate || wasAlreadyLinked)
                {
                    await RecordApPaymentAuditAsync(
                        FinanceAuditEvents.ApPaymentDuplicatePostingAttempt,
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
                        comment: "Duplicate AP payment posting request returned the existing posting.",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await RecordApPaymentAuditAsync(
                        FinanceAuditEvents.ApPaymentPosted,
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
                        comment: "AP payment posted through the central finance posting engine.",
                        cancellationToken: cancellationToken);
                }

                _logger.LogInformation(
                    "Posted AP payment {PaymentNumber} through finance posting engine with journal {JournalEntryId}. Duplicate={WasDuplicate}",
                    payment.PaymentNumber,
                    postingResult.JournalEntryId,
                    postingResult.WasDuplicate);

                await PostRealizedFxIfRequiredAsync(payment, cancellationToken);

                return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
            }
            catch (Exception ex)
            {
                await RecordApPaymentAuditAsync(
                    FinanceAuditEvents.ApPaymentPostingFailed,
                    payment,
                    afterValues: new
                    {
                        payment.JournalEntryId,
                        error = ex.Message
                    },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);

                _logger.LogError(ex, "Failed to post AP payment {PaymentNumber}", payment.PaymentNumber);
                throw;
            }
        }

        public Task<VendorPaymentAllocationResultDto> AllocatePaymentAsync(
            Guid paymentId,
            List<VendorPaymentAllocationCreateDto> allocations,
            CancellationToken cancellationToken = default) =>
            AllocatePaymentAsync(paymentId, allocations, cancellationToken, executionStrategyScope: false);

        private async Task<VendorPaymentAllocationResultDto> AllocatePaymentAsync(
            Guid paymentId,
            List<VendorPaymentAllocationCreateDto> allocations,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => AllocatePaymentAsync(
                        paymentId,
                        allocations,
                        cancellationToken,
                        executionStrategyScope: true),
                    cancellationToken);
            }

            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId)
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Vendor payment with Id '{paymentId}' not found.");

            if (payment.JournalEntryId.HasValue)
            {
                if (!payment.IsSupplierAdvance)
                    throw new InvalidOperationException("Posted vendor payments cannot be allocated. Use a reversal, void, or adjustment workflow.");

                // A posted supplier advance is the explicit exception: applying it creates a new
                // AP-control/advance reclassification through the central posting engine.
                return await AllocatePostedSupplierAdvanceAsync(paymentId, allocations, cancellationToken);
            }

            if (payment.PaymentBatchId.HasValue && payment.Status != VendorPaymentStatus.Authorized)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_DIRECT_ALLOCATION_BLOCKED",
                    "Batch-owned payments can only be allocated by the approved batch processor.");

            if (!payment.PaymentBatchId.HasValue && payment.Status != VendorPaymentStatus.Draft)
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_ALLOCATION_SET_FROZEN",
                    "A manual payment's invoice set cannot change after it is submitted for authorization.");

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
                    $"tdc0505-payment:{TenantId:N}:{paymentId:N}", cancellationToken);
                foreach (var invoiceId in allocations.Select(item => item.VendorInvoiceId).Distinct().OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"tdc0505-invoice:{TenantId:N}:{invoiceId:N}", cancellationToken);

                payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId && !p.IsDeleted)
                    .Include(p => p.Allocations)
                    .SingleAsync(cancellationToken);

            var result = new VendorPaymentAllocationResultDto
            {
                PaymentId = paymentId
            };

            var now = DateTime.UtcNow;
            var createdAllocations = new List<VendorPaymentAllocation>();

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

                var balance = invoice.TotalAmount - invoice.PaidAmount;
                if (balance <= 0)
                    throw new VendorPaymentControlException(
                        "AP_PAYMENT_BALANCE_BLOCKED",
                        $"Invoice '{invoice.InvoiceNumber}' has no positive outstanding balance.");

                if (invoice.SupplierId != payment.SupplierId)
                {
                    throw new InvalidOperationException($"Invoice '{invoice.InvoiceNumber}' does not belong to this payment's supplier.");
                }

                var requestedCashAmount = Math.Max(alloc.AllocatedAmount, 0m);
                var requestedDiscountAmount = Math.Max(alloc.DiscountAmount, 0m);
                var requestedWithholdingAmount = Math.Max(alloc.WithholdingTaxAmount, 0m);
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

                    if (Math.Abs(RoundMoney(requestedCashAmount + requestedDiscountAmount + requestedWithholdingAmount - balance)) > 0.01m)
                    {
                        throw new InvalidOperationException(
                            $"An early-payment discount may only be taken when invoice '{invoice.InvoiceNumber}' is fully settled by this allocation.");
                    }

                    var availableCash = Math.Max(payment.TotalAmount - payment.AllocatedAmount, 0m);
                    if (requestedCashAmount > availableCash)
                    {
                        throw new InvalidOperationException("The payment does not have enough unallocated cash to complete the discounted settlement.");
                    }
                }

                // Cannot allocate more cash than remaining balance or unallocated payment amount.
                var maxAllocatable = Math.Min(balance, Math.Max(payment.TotalAmount - payment.AllocatedAmount, 0m));
                var allocAmount = Math.Min(Math.Max(alloc.AllocatedAmount, 0m), maxAllocatable);
                var discountAmount = Math.Min(Math.Max(alloc.DiscountAmount, 0m), Math.Max(balance - allocAmount, 0m));
                var withholdingTaxAmount = Math.Min(requestedWithholdingAmount, Math.Max(balance - allocAmount - discountAmount, 0m));

                if (allocAmount <= 0 && discountAmount <= 0 && withholdingTaxAmount <= 0)
                {
                    result.Warnings.Add($"No funds available to allocate to invoice '{invoice.InvoiceNumber}'.");
                    continue;
                }

                var allocation = new VendorPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorPaymentId = paymentId,
                    VendorInvoiceId = alloc.VendorInvoiceId,
                    AllocatedAmount = allocAmount,
                    DiscountAmount = discountAmount,
                    WithholdingTaxAmount = withholdingTaxAmount,
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
                payment.AllocatedAmount += allocAmount;

                result.Allocations.Add(new VendorPaymentAllocationDto
                {
                    Id = allocation.Id,
                    VendorPaymentId = paymentId,
                    VendorInvoiceId = alloc.VendorInvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    AllocatedAmount = allocAmount,
                    DiscountAmount = discountAmount,
                    WithholdingTaxAmount = withholdingTaxAmount,
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
            payment.DiscountTaken = payment.Allocations
                .Where(a => !a.IsReversal && !createdAllocationIds.Contains(a.Id))
                .Sum(a => a.DiscountAmount) + createdAllocations.Sum(a => a.DiscountAmount);
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
                    $"tdc0505-payment:{TenantId:N}:{paymentId:N}", cancellationToken);
                foreach (var invoiceId in requestedAllocations.Select(item => item.VendorInvoiceId).Distinct().OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"tdc0505-invoice:{TenantId:N}:{invoiceId:N}", cancellationToken);

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
                if (!string.Equals(NormalizeCurrency(payment.CurrencyCode, functionalCurrency), functionalCurrency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Foreign-currency supplier advance application is not supported until advance FX settlement is implemented.");

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

                var currentlyAllocated = RoundMoney(payment.Allocations.Where(a => !a.IsReversal).Sum(a => a.AllocatedAmount));
                var requestedTotal = RoundMoney(requestedAllocations.Sum(a => a.AllocatedAmount));
                if (requestedTotal > RoundMoney(payment.TotalAmount - currentlyAllocated))
                    throw new InvalidOperationException("Supplier advance application exceeds the unallocated advance balance.");

                var now = DateTime.UtcNow;
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
                    if (!string.Equals(NormalizeCurrency(invoice.CurrencyCode, functionalCurrency), functionalCurrency, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Foreign-currency supplier advance application is not supported until advance FX settlement is implemented.");

                    var invoiceOutstanding = RoundMoney(invoice.TotalAmount - invoice.PaidAmount);
                    if (requested.AllocatedAmount > invoiceOutstanding)
                        throw new InvalidOperationException($"Supplier advance application exceeds the outstanding balance of invoice '{invoice.InvoiceNumber}'.");

                    var allocation = new VendorPaymentAllocation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        VendorPaymentId = payment.Id,
                        VendorInvoiceId = invoice.Id,
                        AllocatedAmount = RoundMoney(requested.AllocatedAmount),
                        AllocationDate = now,
                        Notes = requested.Notes,
                        PaymentReadinessControlEventId = paymentDecision.Event.Id,
                        PaymentReadinessSnapshotHash = paymentDecision.Readiness.SnapshotHash,
                        PaymentReadinessEvaluatedAtUtc = paymentDecision.Readiness.EvaluatedAtUtc,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    // Register the allocation explicitly as Added. Updating the payment source
                    // record later must not turn this new row into a modified-only graph entry.
                    await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(allocation);
                    payment.Allocations.Add(allocation);
                    // This is a read-side operational snapshot. Recompute it from allocations so
                    // a stale value cannot cause an advance to be over-applied after a retry.
                    payment.AllocatedAmount = RoundMoney(payment.Allocations
                        .Where(a => !a.IsReversal)
                        .Sum(a => a.AllocatedAmount));
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

                    var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = "AP",
                        SourceDocumentType = "VendorPaymentAdvanceApplication",
                        SourceDocumentId = allocation.Id,
                        SourceDocumentTenantId = payment.TenantId,
                        PostingAction = "Post",
                        SourceDocumentReference = $"{payment.PaymentNumber}:{invoice.InvoiceNumber}",
                        Description = $"Apply supplier advance {payment.PaymentNumber} to invoice {invoice.InvoiceNumber}",
                        PostingDate = now,
                        JournalType = "AP Supplier Advance Application",
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = functionalCurrency,
                        IdempotencyKey = $"AP:VendorPaymentAdvanceApplication:{payment.TenantId:N}:{allocation.Id:N}:Post",
                        ReturnExistingOnDuplicate = true,
                        Lines = new[]
                        {
                            BuildPostingLine(apAccountId, $"Apply supplier advance {payment.PaymentNumber}", allocation.AllocatedAmount, 0m, functionalCurrency, functionalCurrency, 1m, now, payment.PaymentNumber, 1, "AP-Control"),
                            BuildPostingLine(advanceAccountId, $"Apply supplier advance {payment.PaymentNumber}", 0m, allocation.AllocatedAmount, functionalCurrency, functionalCurrency, 1m, now, payment.PaymentNumber, 2, "AP-SupplierAdvance")
                        }
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
                    afterValues: new { result.TotalAllocated, result.RemainingUnallocated, AllocationCount = result.Allocations.Count },
                    comment: "Posted supplier advance application through the central finance posting engine.",
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
            var allocation = await _unitOfWork.Repository<VendorPaymentAllocation>()
                .GetQueryable(a => a.TenantId == TenantId && a.Id == allocationId)
                .Include(a => a.VendorPayment)
                .Include(a => a.VendorInvoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (allocation == null)
                throw new KeyNotFoundException($"Allocation with Id '{allocationId}' not found.");

            if (allocation.IsReversal)
                throw new InvalidOperationException("Cannot reverse a reversal allocation.");

            if (allocation.VendorPayment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted vendor payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");

            var now = DateTime.UtcNow;

            // Create reversal allocation
            var reversal = new VendorPaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VendorPaymentId = allocation.VendorPaymentId,
                VendorInvoiceId = allocation.VendorInvoiceId,
                AllocatedAmount = -allocation.AllocatedAmount,
                DiscountAmount = -allocation.DiscountAmount,
                WithholdingTaxAmount = -allocation.WithholdingTaxAmount,
                AllocationDate = now,
                Notes = $"Reversal of allocation {allocationId}: {reason}",
                IsReversal = true,
                OriginalAllocationId = allocationId,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(reversal);

            // Restore payment unallocated
            allocation.VendorPayment.AllocatedAmount -= allocation.AllocatedAmount;
            if (allocation.VendorPayment.AllocatedAmount < 0)
                allocation.VendorPayment.AllocatedAmount = 0;
            allocation.VendorPayment.UpdatedAt = now;
            allocation.VendorPayment.UpdatedBy = UserName;
            await _unitOfWork.Repository<VendorPayment>().UpdateAsync(allocation.VendorPayment);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Reversed allocation {AllocationId}. Reason: {Reason}", allocationId, reason);
        }

        public async Task<List<VendorPaymentAllocationDto>> GetPaymentAllocationsAsync(Guid paymentId, CancellationToken cancellationToken = default)
        {
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
                DiscountAmount = a.DiscountAmount,
                WithholdingTaxAmount = a.WithholdingTaxAmount,
                AllocationDate = a.AllocationDate,
                Notes = a.Notes,
                IsReversal = a.IsReversal,
                PaymentReadinessControlEventId = a.PaymentReadinessControlEventId,
                PaymentReadinessSnapshotHash = a.PaymentReadinessSnapshotHash,
                PaymentReadinessEvaluatedAtUtc = a.PaymentReadinessEvaluatedAtUtc
            }).ToList();
        }

        public async Task<List<OutstandingVendorInvoiceDto>> GetOutstandingInvoicesAsync(Guid supplierId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var resolvedSupplierId = await ResolveSupplierIdForQueryAsync(supplierId, cancellationToken);
            if (!resolvedSupplierId.HasValue)
            {
                return new List<OutstandingVendorInvoiceDto>();
            }

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.SupplierId == resolvedSupplierId.Value &&
                    (i.Status == VendorInvoiceStatus.Approved ||
                     i.Status == VendorInvoiceStatus.PartiallyPaid ||
                     i.Status == VendorInvoiceStatus.Overdue) &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .OrderBy(i => i.DueDate)
                .ToListAsync(cancellationToken);

            var result = new List<OutstandingVendorInvoiceDto>();
            foreach (var i in invoices)
            {
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
                    BalanceAmount = i.BalanceAmount,
                    DaysOverdue = i.DueDate.HasValue && i.DueDate.Value < now
                        ? (int)(now - i.DueDate.Value).TotalDays
                        : 0,
                    EarlyPaymentDiscountPercentage = i.EarlyPaymentDiscountPercentage,
                    EarlyPaymentDiscountDueDate = i.EarlyPaymentDiscountDueDate,
                    IsDiscountAvailable = discountAvailable,
                    DiscountAmount = discountAvailable ? i.EarlyPaymentDiscountAmount : 0,
                    PaymentReadiness = await EvaluateInvoicePaymentReadinessAsync(i.Id, cancellationToken)
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
                    $"tdc0508-payment:{TenantId:N}:{id:N}", cancellationToken);

                payment = await _unitOfWork.Repository<VendorPayment>()
                    .GetQueryable(p => p.TenantId == TenantId && p.Id == id && !p.IsDeleted)
                    .Include(p => p.Supplier)
                    .Include(p => p.Allocations.Where(a => !a.IsDeleted))
                        .ThenInclude(a => a.VendorInvoice)
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

                foreach (var invoiceId in originalAllocations
                             .Select(item => item.VendorInvoiceId)
                             .Distinct()
                             .OrderBy(item => item))
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"tdc0508-invoice:{TenantId:N}:{invoiceId:N}", cancellationToken);
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
                        DiscountAmount = -original.DiscountAmount,
                        WithholdingTaxAmount = -original.WithholdingTaxAmount,
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
                    $"tdc0505-invoice:{TenantId:N}:{invoiceId:N}", cancellationToken);

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
                Status = PaymentBatchStatus.PendingApproval,
                CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId,
                Notes = dto.Notes,
                CreatedAt = now,
                CreatedBy = UserName
            };

            // Group invoices by supplier and create one payment per supplier
            var bySupplier = invoices.GroupBy(i => i.SupplierId);
            decimal totalAmount = 0;
            int paymentCount = 0;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            foreach (var group in bySupplier)
            {
                var supplier = group.First().Supplier;
                var supplierTotal = group.Sum(i => i.TotalAmount - i.PaidAmount);

                var paymentNumber = await GeneratePaymentNumberAsync(cancellationToken);
                var payment = new VendorPayment
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PaymentNumber = paymentNumber,
                    SupplierId = group.Key,
                    PaymentDate = dto.BatchDate,
                    TotalAmount = supplierTotal,
                    AllocatedAmount = 0,
                    PaymentMethod = paymentMethod,
                    PaymentMethodId = configuredPaymentMethod?.Id,
                    CurrencyCode = group
                        .Select(i => i.CurrencyCode)
                        .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))?
                        .Trim()
                        .ToUpperInvariant() ?? baseCurrencyCode,
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
                        Amount = RoundMoney(invoice.TotalAmount - invoice.PaidAmount),
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
            await _unitOfWork.Repository<PaymentBatch>().AddAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("PaymentBatch", batch.Id);
            if (!workflowResult.Success)
            {
                throw new InvalidOperationException(workflowResult.Message ?? "Unable to start payment batch approval workflow.");
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
                        $"tdc0505-invoice:{TenantId:N}:{selection.VendorInvoiceId:N}", cancellationToken);
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

            var batch = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId && b.Id == batchId)
                .Include(b => b.Items)
                    .ThenInclude(i => i.VendorPayment)
                .Include(b => b.Items)
                    .ThenInclude(i => i.Invoices)
                        .ThenInclude(i => i.VendorInvoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (batch == null)
                throw new KeyNotFoundException($"Payment batch with Id '{batchId}' not found.");

            if (batch.Status != PaymentBatchStatus.Approved)
                throw new InvalidOperationException("Only approved batches can be processed.");

            if (batch.Items.Count == 0 || batch.Items.Any(item => item.Invoices.Count == 0))
                throw new VendorPaymentControlException(
                    "AP_PAYMENT_BATCH_SELECTION_MISSING",
                    "Every payment batch item must retain at least one immutable invoice selection.");

            await _invoicePaymentSod.RevalidateBatchAuthorizationAsync(batchId, cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var selection in batch.Items.SelectMany(item => item.Invoices)
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

                    if (allocs.Any())
                    {
                        await AllocatePaymentAsync(payment.Id, allocs, cancellationToken);
                    }

                    await PostAsync(payment.Id, cancellationToken);

                    item.ItemStatus = "Processed";
                    foreach (var selection in item.Invoices)
                    {
                        selection.Status = "Processed";
                        selection.FailureReason = null;
                    }
                    processedCount++;
                }
                catch (Exception ex)
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
                    _logger.LogError(ex, "Failed to process batch item {ItemId}", item.Id);
                }
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

        private async Task<VendorPaymentInvoiceReadinessDto> EvaluateInvoicePaymentReadinessAsync(
            Guid invoiceId,
            CancellationToken cancellationToken,
            bool allowSettledInvoice = false)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == invoiceId && !item.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Vendor invoice with Id '{invoiceId}' not found.");

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
            CancellationToken cancellationToken)
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
                ReversalType = "Controlled AP void",
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
            foreach (var allocation in payment.Allocations
                         .Where(item => !item.IsDeleted && !item.IsReversal)
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

        private async Task<FinancePostingRequestDto> BuildApPaymentPostingRequestAsync(
            VendorPayment payment,
            CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            if (payment.TenantId != tenantId)
                throw new InvalidOperationException("Vendor payment belongs to another tenant.");

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

            var activeAllocations = payment.Allocations?
                .Where(a => !a.IsReversal)
                .OrderBy(a => a.AllocationDate)
                .ThenBy(a => a.Id)
                .ToList() ?? new List<VendorPaymentAllocation>();

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

                var totalInvoiceSettlement = await _unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryable(a =>
                        a.TenantId == tenantId &&
                        a.VendorInvoiceId == allocation.VendorInvoiceId &&
                        !a.IsReversal &&
                        !a.IsDeleted)
                    .SumAsync(a => a.AllocatedAmount + a.DiscountAmount + a.WithholdingTaxAmount, cancellationToken);

                if (RoundMoney(totalInvoiceSettlement) > RoundMoney(allocation.VendorInvoice.TotalAmount))
                    throw new InvalidOperationException($"AP payment would over-settle invoice '{allocation.VendorInvoice.InvoiceNumber}'.");
            }

            if (activeAllocations.Count > 0)
                await _unitOfWork.SaveChangesAsync(cancellationToken);

            var allocatedCashAmount = RoundMoney(activeAllocations.Sum(a => a.AllocatedAmount));
            if (!isSupplierAdvance && allocatedCashAmount != RoundMoney(payment.TotalAmount))
            {
                throw new InvalidOperationException("AP payment amount must equal allocated cash amount before posting. Use the supplier-advance path for an unapplied payment.");
            }

            var supplier = await ResolvePaymentSupplierForPostingAsync(payment, cancellationToken);
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            if (isSupplierAdvance && !string.Equals(paymentCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Foreign-currency supplier advances are not supported until advance application FX settlement is implemented.");
            }
            foreach (var allocation in activeAllocations)
            {
                var invoiceCurrency = NormalizeCurrency(allocation.VendorInvoice.CurrencyCode, paymentCurrency);
                if (!string.Equals(invoiceCurrency, paymentCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Cross-currency AP settlements are not supported in FX Batch 18. Vendor payment currency must match each allocated invoice currency.");
                }
            }

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

            var discountTaken = RoundMoney(activeAllocations.Sum(a => a.DiscountAmount));
            var allocationWithholdingTax = RoundMoney(activeAllocations.Sum(a => a.WithholdingTaxAmount));
            var withholdingTaxAmount = allocationWithholdingTax > 0m
                ? allocationWithholdingTax
                : RoundMoney(payment.WithholdingTaxAmount);
            var apSettlementAmount = RoundMoney(payment.TotalAmount + discountTaken + withholdingTaxAmount);

            var postingLines = new List<FinancePostingLineDto>();
            var lineNumber = 1;
            postingLines.Add(BuildPostingLine(
                debitAccountId,
                isSupplierAdvance
                    ? $"Supplier advance {payment.PaymentNumber}"
                    : $"AP payment {payment.PaymentNumber}",
                debitTransactionAmount: isSupplierAdvance ? payment.TotalAmount : apSettlementAmount,
                creditTransactionAmount: 0m,
                paymentCurrency,
                functionalCurrency,
                exchangeRate,
                payment.PaymentDate,
                payment.PaymentNumber,
                lineNumber++,
                isSupplierAdvance ? "AP-SupplierAdvance" : "AP-Control"));

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
                "AP-Bank"));

            if (discountTaken > 0m)
            {
                var discountAccountId = settings.DiscountReceivedAccountId
                    ?? throw new InvalidOperationException("Purchase discount received account is not configured for this tenant.");
                await ResolvePaymentPostingAccountAsync(discountAccountId, "purchase discount received account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                postingLines.Add(BuildPostingLine(
                    discountAccountId,
                    $"Purchase discount taken - {payment.PaymentNumber}",
                    debitTransactionAmount: 0m,
                    creditTransactionAmount: discountTaken,
                    paymentCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    lineNumber++,
                    "AP-Discount"));
            }

            if (withholdingTaxAmount > 0m)
            {
                var taxAccountId = payment.WithholdingTaxAccountId
                    ?? await ResolveConfiguredWithholdingTaxPayableAccountAsync(payment.PaymentDate, payment.WithholdingTaxId, cancellationToken)
                    ?? throw new InvalidOperationException("AP withholding tax payable account is not configured for this tenant.");
                await ResolvePaymentPostingAccountAsync(taxAccountId, "withholding tax payable account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

                postingLines.Add(BuildPostingLine(
                    taxAccountId,
                    $"Withholding tax - {payment.PaymentNumber}",
                    debitTransactionAmount: 0m,
                    creditTransactionAmount: withholdingTaxAmount,
                    paymentCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    lineNumber++,
                    "AP-WHT"));
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
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            if (string.Equals(paymentCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
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
            string transactionTag)
        {
            var isForeign = !string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
            var debitAmount = ToFunctionalAmount(debitTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate);
            var creditAmount = ToFunctionalAmount(creditTransactionAmount, transactionCurrency, functionalCurrency, exchangeRate);

            return new FinancePostingLineDto
            {
                AccountId = accountId,
                Description = description,
                DebitAmount = debitAmount,
                CreditAmount = creditAmount,
                TransactionCurrency = transactionCurrency,
                ForeignCurrencyAmount = isForeign
                    ? debitTransactionAmount > 0m ? debitTransactionAmount : creditTransactionAmount
                    : null,
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

        private static string NormalizeCurrency(string? currencyCode, string defaultValue)
            => string.IsNullOrWhiteSpace(currencyCode)
                ? defaultValue.Trim().ToUpperInvariant()
                : currencyCode.Trim().ToUpperInvariant();

        private static decimal RoundMoney(decimal amount)
            => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        private async Task<Supplier> ResolveSupplierForPaymentAsync(Guid supplierOrBusinessPartnerId, CancellationToken cancellationToken)
        {
            var supplierRepository = _unitOfWork.Repository<Supplier>();
            var supplier = await supplierRepository
                .GetQueryable(s =>
                    s.TenantId == TenantId &&
                    !s.IsDeleted &&
                    s.Id == supplierOrBusinessPartnerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (supplier != null)
            {
                return supplier;
            }

            var partner = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    !p.IsDeleted &&
                    p.Id == supplierOrBusinessPartnerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (partner == null)
            {
                throw new KeyNotFoundException($"Supplier or business partner with Id '{supplierOrBusinessPartnerId}' not found.");
            }

            if (string.Equals(partner.PartnerType, "Customer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Customer business partners cannot be used for AP vendor payments.");
            }

            if (partner.IsBlacklisted)
            {
                throw new InvalidOperationException($"Business partner '{partner.PartnerName}' is blacklisted and cannot be used for AP vendor payments.");
            }

            supplier = await supplierRepository
                .GetQueryable(s =>
                    s.TenantId == TenantId &&
                    !s.IsDeleted &&
                    (s.Id == partner.Id ||
                     s.SupplierCode == partner.PartnerCode ||
                     s.Name == partner.PartnerName))
                .FirstOrDefaultAsync(cancellationToken);

            if (supplier != null)
            {
                return supplier;
            }

            supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SupplierCode = string.IsNullOrWhiteSpace(partner.PartnerCode)
                    ? $"BP-{partner.Id.ToString("N")[..8].ToUpperInvariant()}"
                    : partner.PartnerCode,
                Name = partner.PartnerName,
                SupplierType = partner.PartnerType.Contains("Manufacturer", StringComparison.OrdinalIgnoreCase)
                    ? "Manufacturer"
                    : "Vendor",
                Address = partner.PhysicalAddress ?? partner.MailingAddress,
                City = partner.PhysicalCity ?? partner.MailingCity,
                State = partner.PhysicalState ?? partner.MailingState,
                Country = partner.PhysicalCountry ?? partner.MailingCountry,
                ZipCode = partner.PhysicalPostalCode ?? partner.MailingPostalCode,
                Phone = partner.PrimaryPhone,
                Email = partner.PrimaryEmail,
                Website = partner.Website,
                PrimaryContactName = partner.PrimaryContactName,
                PrimaryContactTitle = partner.PrimaryContactTitle,
                PrimaryContactPhone = partner.PrimaryPhone,
                PrimaryContactEmail = partner.PrimaryEmail,
                TaxId = partner.TaxIdentificationNumber ?? partner.VATNumber,
                PaymentTerms = partner.PaymentTerms ?? "Net 30",
                PaymentTermId = partner.PaymentTermId,
                IsActive = partner.IsActive,
                IsPreferred = partner.IsPreferred,
                Status = partner.IsActive ? "Active" : "Inactive",
                Notes = $"Auto-created from business partner {partner.PartnerCode} for AP vendor payment entry.",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await supplierRepository.AddAsync(supplier);
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

            var partner = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    !p.IsDeleted &&
                    p.Id == supplierOrBusinessPartnerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (partner == null)
            {
                return null;
            }

            return await supplierRepository
                .GetQueryable(s =>
                    s.TenantId == TenantId &&
                    !s.IsDeleted &&
                    (s.Id == partner.Id ||
                     s.SupplierCode == partner.PartnerCode ||
                     s.Name == partner.PartnerName))
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
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
                BankAccountId = payment.BankAccountId,
                BankAccountName = payment.BankAccount?.AccountName,
                ChequeNumber = payment.ChequeNumber,
                TransactionReference = payment.TransactionReference,
                WithholdingTaxRate = payment.WithholdingTaxRate,
                WithholdingTaxAmount = payment.WithholdingTaxAmount,
                WithholdingTaxId = payment.WithholdingTaxId,
                WithholdingTaxAccountId = payment.WithholdingTaxAccountId,
                WithholdingCertificateNumber = payment.WithholdingCertificateNumber,
                WithholdingCertificateDate = payment.WithholdingCertificateDate,
                DiscountTaken = payment.DiscountTaken,
                Status = payment.Status,
                AuthorizedById = payment.AuthorizedById,
                AuthorizedDate = payment.AuthorizedDate,
                InvoicePaymentSodControlEventId = payment.InvoicePaymentSodControlEventId,
                PaymentBatchId = payment.PaymentBatchId,
                PaymentBatchNumber = payment.PaymentBatch?.BatchNumber,
                JournalEntryId = payment.JournalEntryId,
                Notes = payment.Notes,
                CreatedAt = payment.CreatedAt,
                Allocations = payment.Allocations?.Select(a => new VendorPaymentAllocationDto
                {
                    Id = a.Id,
                    VendorPaymentId = a.VendorPaymentId,
                    VendorInvoiceId = a.VendorInvoiceId,
                    InvoiceNumber = a.VendorInvoice?.InvoiceNumber ?? string.Empty,
                    AllocatedAmount = a.AllocatedAmount,
                    DiscountAmount = a.DiscountAmount,
                    WithholdingTaxAmount = a.WithholdingTaxAmount,
                    AllocationDate = a.AllocationDate,
                    Notes = a.Notes,
                    IsReversal = a.IsReversal,
                    PaymentReadinessControlEventId = a.PaymentReadinessControlEventId,
                    PaymentReadinessSnapshotHash = a.PaymentReadinessSnapshotHash,
                    PaymentReadinessEvaluatedAtUtc = a.PaymentReadinessEvaluatedAtUtc
                }).ToList() ?? new List<VendorPaymentAllocationDto>()
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
