using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public VendorPaymentService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ILogger<VendorPaymentService> logger,
            IDocumentNumberingService documentNumberingService,
            IWorkflowService workflowService,
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            IFxAccountingService? fxAccountingService = null)
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

        public async Task<VendorPaymentDto> CreateAsync(VendorPaymentCreateDto dto, CancellationToken cancellationToken = default)
        {
            var supplier = await ResolveSupplierForPaymentAsync(dto.SupplierId, cancellationToken);

            var paymentNumber = await GeneratePaymentNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var paymentCurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode)
                ? baseCurrencyCode
                : dto.CurrencyCode.Trim().ToUpperInvariant();

            // Calculate WHT
            decimal whtAmount = 0;
            if (dto.WithholdingTaxRate > 0)
            {
                whtAmount = dto.TotalAmount * (dto.WithholdingTaxRate / 100);
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
                PaymentMethod = dto.PaymentMethod,
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
                CreatedBy = UserName
            };

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
                    .Include(p => p.Allocations)
                        .ThenInclude(a => a.VendorInvoice)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Created vendor payment {PaymentNumber} for supplier {SupplierId}, amount {Amount}",
                paymentNumber, supplier.Id, dto.TotalAmount);

            if (payment == null)
                throw new InvalidOperationException("Failed to load created vendor payment.");

            return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment);
        }

        // ═════════════════════════════════════════════════════════════════
        //  ALLOCATIONS
        // ═════════════════════════════════════════════════════════════════

        public async Task<VendorPaymentDto> PostAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP payment posting.");

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

        public async Task<VendorPaymentAllocationResultDto> AllocatePaymentAsync(
            Guid paymentId,
            List<VendorPaymentAllocationCreateDto> allocations,
            CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == paymentId)
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Vendor payment with Id '{paymentId}' not found.");

            if (payment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted vendor payments cannot be allocated. Use a reversal, void, or adjustment workflow.");

            var result = new VendorPaymentAllocationResultDto
            {
                PaymentId = paymentId
            };

            var now = DateTime.UtcNow;
            var createdAllocations = new List<VendorPaymentAllocation>();

            foreach (var alloc in allocations)
            {
                var invoice = await _unitOfWork.Repository<VendorInvoice>()
                    .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == alloc.VendorInvoiceId);

                if (invoice == null)
                {
                    result.Warnings.Add($"Invoice '{alloc.VendorInvoiceId}' not found, skipped.");
                    continue;
                }

                var balance = invoice.TotalAmount - invoice.PaidAmount;
                if (balance <= 0)
                {
                    result.Warnings.Add($"Invoice '{invoice.InvoiceNumber}' is already fully paid, skipped.");
                    continue;
                }

                // Cannot allocate more cash than remaining balance or unallocated payment amount.
                var maxAllocatable = Math.Min(balance, Math.Max(payment.TotalAmount - payment.AllocatedAmount, 0m));
                var allocAmount = Math.Min(Math.Max(alloc.AllocatedAmount, 0m), maxAllocatable);
                var discountAmount = Math.Min(Math.Max(alloc.DiscountAmount, 0m), Math.Max(balance - allocAmount, 0m));

                if (allocAmount <= 0 && discountAmount <= 0)
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
                    WithholdingTaxAmount = alloc.WithholdingTaxAmount,
                    AllocationDate = now,
                    Notes = alloc.Notes,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                await _unitOfWork.Repository<VendorPaymentAllocation>().AddAsync(allocation);
                createdAllocations.Add(allocation);

                // Update invoice paid amount. Supplier discounts reduce the payable balance but are not cash.
                invoice.PaidAmount += allocAmount + discountAmount;
                if (invoice.PaidAmount >= invoice.TotalAmount)
                    invoice.Status = VendorInvoiceStatus.Paid;
                else if (invoice.PaidAmount > 0)
                    invoice.Status = VendorInvoiceStatus.PartiallyPaid;

                invoice.UpdatedAt = now;
                invoice.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);

                // Update payment allocated amount
                payment.AllocatedAmount += allocAmount;

                result.Allocations.Add(new VendorPaymentAllocationDto
                {
                    Id = allocation.Id,
                    VendorPaymentId = paymentId,
                    VendorInvoiceId = alloc.VendorInvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    AllocatedAmount = allocAmount,
                    DiscountAmount = discountAmount,
                    WithholdingTaxAmount = alloc.WithholdingTaxAmount,
                    AllocationDate = now,
                    Notes = alloc.Notes
                });
            }

            // Update payment status
            if (payment.AllocatedAmount >= payment.TotalAmount)
                payment.Status = VendorPaymentStatus.Processed;

            var createdAllocationIds = createdAllocations.Select(a => a.Id).ToHashSet();
            payment.DiscountTaken = payment.Allocations
                .Where(a => !a.IsReversal && !createdAllocationIds.Contains(a.Id))
                .Sum(a => a.DiscountAmount) + createdAllocations.Sum(a => a.DiscountAmount);
            payment.UpdatedAt = now;
            payment.UpdatedBy = UserName;
            await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            result.TotalAllocated = payment.AllocatedAmount;
            result.RemainingUnallocated = payment.TotalAmount - payment.AllocatedAmount;

            _logger.LogInformation("Allocated {AllocCount} invoices to payment {PaymentId}, total allocated: {Total}",
                result.Allocations.Count, paymentId, result.TotalAllocated);

            return result;
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

            // Restore invoice balance
            allocation.VendorInvoice.PaidAmount -= allocation.AllocatedAmount + allocation.DiscountAmount;
            if (allocation.VendorInvoice.PaidAmount <= 0)
            {
                allocation.VendorInvoice.PaidAmount = 0;
                allocation.VendorInvoice.Status = VendorInvoiceStatus.Approved;
            }
            else
            {
                allocation.VendorInvoice.Status = VendorInvoiceStatus.PartiallyPaid;
            }
            allocation.VendorInvoice.UpdatedAt = now;
            allocation.VendorInvoice.UpdatedBy = UserName;
            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(allocation.VendorInvoice);

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
                IsReversal = a.IsReversal
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

            return invoices.Select(i =>
            {
                var discountAvailable = i.EarlyPaymentDiscountPercentage > 0
                    && i.EarlyPaymentDiscountDueDate.HasValue
                    && i.EarlyPaymentDiscountDueDate.Value >= now.Date;

                return new OutstandingVendorInvoiceDto
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
                    DiscountAmount = discountAvailable ? i.EarlyPaymentDiscountAmount : 0
                };
            }).ToList();
        }

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

        public async Task<VendorPaymentDto> VoidPaymentAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id)
                .Include(p => p.Supplier)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.VendorInvoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Vendor payment with Id '{id}' not found.");

            if (payment.Status == VendorPaymentStatus.Voided)
                throw new InvalidOperationException("Payment is already voided.");

            if (payment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted vendor payments cannot be voided by mutation until AP payment reversal posting is implemented.");

            var now = DateTime.UtcNow;

            // Reverse all non-reversal allocations
            foreach (var alloc in payment.Allocations.Where(a => !a.IsReversal).ToList())
            {
                alloc.VendorInvoice.PaidAmount -= alloc.AllocatedAmount + alloc.DiscountAmount;
                if (alloc.VendorInvoice.PaidAmount <= 0)
                {
                    alloc.VendorInvoice.PaidAmount = 0;
                    alloc.VendorInvoice.Status = VendorInvoiceStatus.Approved;
                }
                else
                {
                    alloc.VendorInvoice.Status = VendorInvoiceStatus.PartiallyPaid;
                }
                alloc.VendorInvoice.UpdatedAt = now;
                alloc.VendorInvoice.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(alloc.VendorInvoice);
            }

            payment.Status = VendorPaymentStatus.Voided;
            payment.AllocatedAmount = 0;
            payment.Notes = $"{payment.Notes}\n\nVoided on {now:yyyy-MM-dd HH:mm}: {reason}";
            payment.UpdatedAt = now;
            payment.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Voided vendor payment {PaymentNumber}. Reason: {Reason}", payment.PaymentNumber, reason);
            return MapToDto(payment);
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

        public async Task<PaymentBatchDto> CreatePaymentBatchAsync(PaymentBatchCreateDto dto, CancellationToken cancellationToken = default)
        {
            var batchNumber = await GenerateBatchNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Load the invoices
            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    dto.InvoiceIds.Contains(i.Id) &&
                    i.Status == VendorInvoiceStatus.Approved &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .Include(i => i.Supplier)
                .ToListAsync(cancellationToken);

            if (!invoices.Any())
                throw new InvalidOperationException("No approved outstanding invoices found for the provided IDs.");

            var batch = new PaymentBatch
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BatchNumber = batchNumber,
                Description = dto.Description,
                BatchDate = dto.BatchDate,
                DueDateFrom = dto.DueDateFrom,
                DueDateTo = dto.DueDateTo,
                PaymentMethod = dto.PaymentMethod,
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
                    PaymentMethod = dto.PaymentMethod,
                    CurrencyCode = group
                        .Select(i => i.CurrencyCode)
                        .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))?
                        .Trim()
                        .ToUpperInvariant() ?? baseCurrencyCode,
                    ExchangeRate = 1.0m,
                    BankAccountId = dto.BankAccountId,
                    PaymentBatchId = batch.Id,
                    Status = VendorPaymentStatus.PendingAuthorization,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                await _unitOfWork.Repository<VendorPayment>().AddAsync(payment);

                batch.Items.Add(new PaymentBatchItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PaymentBatchId = batch.Id,
                    VendorPaymentId = payment.Id,
                    Amount = supplierTotal,
                    ItemStatus = "Pending",
                    CreatedAt = now,
                    CreatedBy = UserName
                });

                totalAmount += supplierTotal;
                paymentCount++;
            }

            batch.TotalAmount = totalAmount;
            batch.PaymentCount = paymentCount;

            await _unitOfWork.Repository<PaymentBatch>().AddAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("PaymentBatch", batch.Id);
            if (!workflowResult.Success)
            {
                batch.Status = PaymentBatchStatus.Draft;
                batch.UpdatedAt = DateTime.UtcNow;
                batch.UpdatedBy = UserName;
                await _unitOfWork.Repository<PaymentBatch>().UpdateAsync(batch);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                throw new InvalidOperationException(workflowResult.Message ?? "Unable to start payment batch approval workflow.");
            }

            _logger.LogInformation("Created payment batch {BatchNumber} with {Count} payments, total {Total}",
                batchNumber, paymentCount, totalAmount);

            return await GetPaymentBatchAsync(batch.Id, cancellationToken) ?? throw new InvalidOperationException("Failed to retrieve created batch.");
        }

        public async Task<PaymentBatchDto?> GetPaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            var batch = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId && b.Id == batchId)
                .Include(b => b.Items)
                    .ThenInclude(i => i.VendorPayment)
                        .ThenInclude(p => p.Supplier)
                .Include(b => b.BankAccount)
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
                .ToListAsync(cancellationToken);

            return new PagedResult<PaymentBatchDto>
            {
                Items = batches.Select(MapBatchToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<PaymentBatchDto> ApprovePaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            var batch = await _unitOfWork.Repository<PaymentBatch>()
                .FirstOrDefaultAsync(b => b.TenantId == TenantId && b.Id == batchId);

            if (batch == null)
                throw new KeyNotFoundException($"Payment batch with Id '{batchId}' not found.");

            if (batch.Status != PaymentBatchStatus.PendingApproval)
                throw new InvalidOperationException("Only pending batches can be approved.");

            if (CurrentUserId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            if (!await _workflowService.CanUserApproveAsync("PaymentBatch", batchId, CurrentUserId))
                throw new InvalidOperationException("This payment batch is assigned to another workflow approver.");

            var workflowResult = await _workflowService.ProcessApprovalStepAsync("PaymentBatch", batchId, CurrentUserId, "Approve");
            if (!workflowResult.Success)
                throw new InvalidOperationException(workflowResult.Message ?? "Unable to process payment batch approval.");

            if (workflowResult.Status != WorkflowInstanceStatus.Completed)
                return await GetPaymentBatchAsync(batchId, cancellationToken) ?? throw new InvalidOperationException("Batch not found after approval.");

            batch.Status = PaymentBatchStatus.Approved;
            batch.ApprovedById = CurrentUserId;
            batch.ApprovedDate = DateTime.UtcNow;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = UserName;

            await _unitOfWork.Repository<PaymentBatch>().UpdateAsync(batch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Approved payment batch {BatchNumber}", batch.BatchNumber);
            return await GetPaymentBatchAsync(batchId, cancellationToken) ?? throw new InvalidOperationException("Batch not found after approval.");
        }

        public async Task<PaymentBatchDto> ProcessPaymentBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            var batch = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId && b.Id == batchId)
                .Include(b => b.Items)
                    .ThenInclude(i => i.VendorPayment)
                .FirstOrDefaultAsync(cancellationToken);

            if (batch == null)
                throw new KeyNotFoundException($"Payment batch with Id '{batchId}' not found.");

            if (batch.Status != PaymentBatchStatus.Approved)
                throw new InvalidOperationException("Only approved batches can be processed.");

            var now = DateTime.UtcNow;
            batch.Status = PaymentBatchStatus.Processing;
            int processedCount = 0;
            int failedCount = 0;

            foreach (var item in batch.Items)
            {
                try
                {
                    // Get outstanding invoices for this supplier
                    var payment = item.VendorPayment;
                    var invoices = await _unitOfWork.Repository<VendorInvoice>()
                        .GetQueryable(i =>
                            i.TenantId == TenantId &&
                            i.SupplierId == payment.SupplierId &&
                            i.Status == VendorInvoiceStatus.Approved &&
                            (i.TotalAmount - i.PaidAmount) > 0)
                        .OrderBy(i => i.DueDate)
                        .ToListAsync(cancellationToken);

                    // Auto-allocate payment to invoices by due date
                    var remaining = payment.TotalAmount;
                    var allocs = new List<VendorPaymentAllocationCreateDto>();

                    foreach (var inv in invoices)
                    {
                        if (remaining <= 0) break;
                        var balance = inv.TotalAmount - inv.PaidAmount;
                        var allocAmount = Math.Min(balance, remaining);

                        allocs.Add(new VendorPaymentAllocationCreateDto
                        {
                            VendorInvoiceId = inv.Id,
                            AllocatedAmount = allocAmount
                        });

                        remaining -= allocAmount;
                    }

                    if (allocs.Any())
                    {
                        await AllocatePaymentAsync(payment.Id, allocs, cancellationToken);
                    }

                    payment.Status = VendorPaymentStatus.Authorized;
                    payment.UpdatedAt = now;
                    payment.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    await PostAsync(payment.Id, cancellationToken);

                    item.ItemStatus = "Processed";
                    processedCount++;
                }
                catch (Exception ex)
                {
                    item.ItemStatus = "Failed";
                    item.FailureReason = ex.Message;
                    failedCount++;
                    _logger.LogError(ex, "Failed to process batch item {ItemId}", item.Id);
                }
            }

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

        // ═════════════════════════════════════════════════════════════════
        //  UTILITIES
        // ═════════════════════════════════════════════════════════════════

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

            if (activeAllocations.Count == 0)
                throw new InvalidOperationException("AP payment must have at least one invoice allocation before posting.");

            foreach (var allocation in activeAllocations)
            {
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

            var allocatedCashAmount = RoundMoney(activeAllocations.Sum(a => a.AllocatedAmount));
            if (allocatedCashAmount != RoundMoney(payment.TotalAmount))
            {
                throw new InvalidOperationException("AP payment amount must equal allocated cash amount before posting. Overpayments are not supported in this batch.");
            }

            var supplier = await ResolvePaymentSupplierForPostingAsync(payment, cancellationToken);
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
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
                apAccountId,
                $"AP payment {payment.PaymentNumber}",
                debitTransactionAmount: apSettlementAmount,
                creditTransactionAmount: 0m,
                paymentCurrency,
                functionalCurrency,
                exchangeRate,
                payment.PaymentDate,
                payment.PaymentNumber,
                lineNumber++,
                "AP-Control"));

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

            return new FinancePostingRequestDto
            {
                SourceModule = "AP",
                SourceDocumentType = "VendorPayment",
                SourceDocumentId = payment.Id,
                SourceDocumentTenantId = payment.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = payment.PaymentNumber,
                Description = $"Vendor payment {payment.PaymentNumber} - {supplier.Name}",
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
                    IsReversal = a.IsReversal
                }).ToList() ?? new List<VendorPaymentAllocationDto>()
            };
        }

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
                BankAccountId = batch.BankAccountId,
                BankAccountName = batch.BankAccount?.AccountName,
                Status = batch.Status,
                ApprovedDate = batch.ApprovedDate,
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
                    FailureReason = i.FailureReason
                }).ToList() ?? new List<PaymentBatchItemDto>()
            };
        }
    }
}
