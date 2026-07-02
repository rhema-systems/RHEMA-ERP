using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
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
        private readonly ISubledgerPostingService _subledgerPostingService;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IWorkflowService _workflowService;

        public VendorPaymentService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ISubledgerPostingService subledgerPostingService,
            ILogger<VendorPaymentService> logger,
            IDocumentNumberingService documentNumberingService,
            IWorkflowService workflowService)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _subledgerPostingService = subledgerPostingService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _workflowService = workflowService;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
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
                queryable = queryable.Where(p => p.SupplierId == query.SupplierId.Value);

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
            var supplier = await _unitOfWork.Repository<Supplier>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == dto.SupplierId);

            if (supplier == null)
                throw new KeyNotFoundException($"Supplier with Id '{dto.SupplierId}' not found.");

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
                SupplierId = dto.SupplierId,
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

            // Post to GL
            await _subledgerPostingService.PostApPaymentAsync(payment!.Id, cancellationToken);

            return MapToDto(payment);
        }

        // ═════════════════════════════════════════════════════════════════
        //  ALLOCATIONS
        // ═════════════════════════════════════════════════════════════════

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

            var result = new VendorPaymentAllocationResultDto
            {
                PaymentId = paymentId
            };

            var now = DateTime.UtcNow;

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
                var maxAllocatable = Math.Min(balance, payment.TotalAmount - payment.AllocatedAmount);
                var allocAmount = Math.Min(alloc.AllocatedAmount, maxAllocatable);
                var discountAmount = Math.Min(alloc.DiscountAmount, Math.Max(balance - allocAmount, 0m));

                if (allocAmount <= 0)
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

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.SupplierId == supplierId &&
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

                    payment.Status = VendorPaymentStatus.Processed;
                    payment.UpdatedAt = now;
                    payment.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorPayment>().UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Post to GL
                    await _subledgerPostingService.PostApPaymentAsync(payment.Id, cancellationToken);

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
                DiscountTaken = payment.DiscountTaken,
                Status = payment.Status,
                PaymentBatchId = payment.PaymentBatchId,
                PaymentBatchNumber = payment.PaymentBatch?.BatchNumber,
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
