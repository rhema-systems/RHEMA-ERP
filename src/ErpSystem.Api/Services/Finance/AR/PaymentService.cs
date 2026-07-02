using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
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

namespace ErpSystem.Api.Services.Finance.AR
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ISubledgerPostingService _subledgerPostingService;
        private readonly ILogger<PaymentService> _logger;
        private readonly IDocumentNumberingService _documentNumberingService;

        public PaymentService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ISubledgerPostingService subledgerPostingService,
            ILogger<PaymentService> logger,
            IDocumentNumberingService documentNumberingService)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _subledgerPostingService = subledgerPostingService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
        private string UserName => _currentUser.UserName ?? "system";

        public async Task<CustomerPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.Invoice)
                .FirstOrDefaultAsync(cancellationToken);

            return payment == null ? null : MapToDto(payment);
        }

        public async Task<CustomerPaymentDto?> GetByPaymentNumberAsync(string paymentNumber, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.PaymentNumber == paymentNumber)
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(cancellationToken);

            return payment == null ? null : MapToDto(payment);
        }

        public async Task<PagedResult<CustomerPaymentDto>> GetAllAsync(PaymentQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId);

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
                .ToListAsync(cancellationToken);

            return new PagedResult<CustomerPaymentDto>
            {
                Items = payments.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        public async Task<CustomerPaymentDto> CreateAsync(PaymentCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate customer business partner exists
            var customer = await GetCustomerPartnerAsync(dto.CustomerId, cancellationToken);

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{dto.CustomerId}' not found.");

            // Generate payment number
            var paymentNumber = await GeneratePaymentNumberAsync(dto.IsCreditNote, cancellationToken);
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var paymentCurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode)
                ? baseCurrencyCode
                : dto.CurrencyCode.Trim().ToUpperInvariant();

            var now = DateTime.UtcNow;
            var payment = new CustomerPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PaymentNumber = paymentNumber,
                CustomerId = dto.CustomerId,
                PaymentDate = dto.PaymentDate,
                TotalAmount = dto.TotalAmount,
                AllocatedAmount = 0,
                PaymentMethod = dto.PaymentMethod,
                CurrencyCode = paymentCurrencyCode,
                ExchangeRate = dto.ExchangeRate,
                BankAccountId = dto.BankAccountId,
                CheckNumber = dto.CheckNumber,
                TransactionReference = dto.TransactionReference,
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

            // Post to GL
            await _subledgerPostingService.PostArPaymentAsync(payment.Id, cancellationToken);

            _logger.LogInformation("Created payment {PaymentNumber} for customer {CustomerId}, Amount: {Amount}",
                paymentNumber, customer.Id, dto.TotalAmount);

            return MapToDto(payment);
        }

        public async Task<CustomerPaymentDto> UpdateAsync(PaymentUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == dto.Id);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{dto.Id}' not found.");

            // Only allow updates if status is Pending
            if (payment.Status != "Pending")
                throw new InvalidOperationException("Only pending payments can be updated.");

            var now = DateTime.UtcNow;
            payment.PaymentDate = dto.PaymentDate;
            payment.TotalAmount = dto.TotalAmount;
            payment.PaymentMethod = dto.PaymentMethod;
            payment.BankAccountId = dto.BankAccountId;
            payment.CheckNumber = dto.CheckNumber;
            payment.TransactionReference = dto.TransactionReference;
            payment.Notes = dto.Notes;
            payment.UpdatedAt = now;
            payment.UpdatedBy = UserName;

            await _unitOfWork.Repository<CustomerPayment>().UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated payment {PaymentId}", payment.Id);

            return MapToDto(payment);
        }

        public async Task<PaymentAllocationResultDto> AllocatePaymentAsync(PaymentAllocation_CreateDto dto, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == dto.CustomerPaymentId)
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{dto.CustomerPaymentId}' not found.");

            var result = new PaymentAllocationResultDto
            {
                Success = false
            };

            // Calculate available cash. Discounts close invoice balance but do not consume cash availability.
            var availableAmount = payment.TotalAmount - payment.AllocatedAmount;

            if (availableAmount <= 0)
            {
                result.Message = "No unallocated amount available.";
                return result;
            }

            // Calculate total cash allocation requested.
            var totalAllocationRequested = dto.Allocations.Sum(a => a.AllocatedAmount);

            if (totalAllocationRequested > availableAmount)
            {
                result.Message = $"Allocation requested ({totalAllocationRequested:C}) exceeds available amount ({availableAmount:C}).";
                return result;
            }

            var now = DateTime.UtcNow;
            var allocatedInvoices = new List<Invoice>();

            foreach (var allocationDto in dto.Allocations)
            {
                var invoice = await _unitOfWork.Repository<Invoice>()
                    .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == allocationDto.InvoiceId);

                if (invoice == null)
                {
                    _logger.LogWarning("Invoice {InvoiceId} not found, skipping allocation", allocationDto.InvoiceId);
                    continue;
                }

                if (invoice.CustomerId != payment.CustomerId)
                {
                    _logger.LogWarning("Invoice {InvoiceId} does not belong to customer {CustomerId}, skipping",
                        allocationDto.InvoiceId, payment.CustomerId);
                    continue;
                }

                if (invoice.BalanceAmount <= 0)
                {
                    _logger.LogWarning("Invoice {InvoiceNumber} has no balance, skipping", invoice.InvoiceNumber);
                    continue;
                }

                // Create allocation
                var allocation = new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CustomerPaymentId = payment.Id,
                    InvoiceId = invoice.Id,
                    AllocatedAmount = allocationDto.AllocatedAmount,
                    DiscountAmount = allocationDto.DiscountAmount,
                    AllocationDate = now,
                    Notes = allocationDto.Notes,
                    IsReversal = false,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                // Cap at outstanding balance to prevent over-allocation
                var totalApplied = allocationDto.AllocatedAmount + allocationDto.DiscountAmount;
                var outstandingBalance = invoice.TotalAmount - invoice.PaidAmount;
                if (totalApplied > outstandingBalance)
                {
                    _logger.LogWarning(
                        "Allocation of {Requested} exceeds outstanding balance {Outstanding} on invoice {InvoiceNumber}. Capping.",
                        totalApplied, outstandingBalance, invoice.InvoiceNumber);

                    // Proportionally reduce both amounts so their sum equals outstandingBalance
                    var ratio = totalApplied > 0 ? outstandingBalance / totalApplied : 0m;
                    allocation.AllocatedAmount = Math.Round(allocationDto.AllocatedAmount * ratio, 2);
                    allocation.DiscountAmount = outstandingBalance - allocation.AllocatedAmount;
                    if (allocation.DiscountAmount < 0) allocation.DiscountAmount = 0;
                    totalApplied = outstandingBalance;
                }

                payment.Allocations.Add(allocation);

                // Update invoice balances
                invoice.PaidAmount += totalApplied;

                // Update invoice status
                if (invoice.BalanceAmount <= 0.01m) // Account for rounding
                {
                    invoice.Status = InvoiceStatus.Paid;
                }
                else if (invoice.PaidAmount > 0)
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
                    DiscountAmount = allocation.DiscountAmount,
                    AllocationDate = allocation.AllocationDate,
                    Notes = allocation.Notes
                });
            }

            // Update payment allocated amount
            payment.AllocatedAmount = payment.Allocations.Where(a => !a.IsReversal).Sum(a => a.AllocatedAmount);

            // Update customer outstanding balance (deduct only the newly allocated amount, not cumulative PaidAmount)
            var customerPartner = await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);
            if (customerPartner != null)
            {
                var totalNewlyAllocated = payment.Allocations
                    .Where(a => a.CreatedAt == now) // Only allocations created in this request
                    .Sum(a => a.AllocatedAmount + a.DiscountAmount);
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

        public async Task ReverseAllocationAsync(Guid allocationId, string reason, CancellationToken cancellationToken = default)
        {
            var allocation = await _unitOfWork.Repository<PaymentAllocation>()
                .GetQueryable(a => a.TenantId == TenantId && a.Id == allocationId)
                .Include(a => a.CustomerPayment)
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(cancellationToken);

            if (allocation == null)
                throw new KeyNotFoundException($"Allocation with Id '{allocationId}' not found.");

            if (allocation.IsReversal)
                throw new InvalidOperationException("This allocation has already been reversed.");

            var now = DateTime.UtcNow;

            // Reverse invoice balances
            var totalApplied = allocation.AllocatedAmount + allocation.DiscountAmount;
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
            allocation.CustomerPayment.AllocatedAmount -= allocation.AllocatedAmount;
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

        public async Task<CustomerPaymentDto> ClearPaymentAsync(Guid id, DateTime clearedDate, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == id);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{id}' not found.");

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

            var now = DateTime.UtcNow;

            // Capture the amount to reverse BEFORE the loop marks allocations as reversed
            var activeAllocations = payment.Allocations.Where(a => !a.IsReversal).ToList();
            var reversedAmount = activeAllocations.Sum(a => a.AllocatedAmount + a.DiscountAmount);

            // Reverse all active allocations
            foreach (var allocation in activeAllocations)
            {
                var totalApplied = allocation.AllocatedAmount + allocation.DiscountAmount;
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
                DiscountAmount = a.DiscountAmount,
                AllocationDate = a.AllocationDate,
                Notes = a.Notes,
                IsReversal = a.IsReversal
            }).ToList();
        }

        public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var invoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.BusinessPartnerId == customerId &&
                    (i.TotalAmount - i.PaidAmount) > 0 &&
                    (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue))
                .OrderBy(i => i.InvoiceDate)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;

            return invoices.Select(i => new OutstandingInvoiceDto
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
                CurrencyCode = i.CurrencyCode
            }).ToList();
        }

        public async Task<byte[]> GeneratePaymentReceiptAsync(Guid id, string format = "PDF", CancellationToken cancellationToken = default)
        {
            // Placeholder - would integrate with a PDF generation library
            await Task.CompletedTask;
            throw new NotImplementedException("Payment receipt printing will be implemented with a PDF library.");
        }

        public async Task<CustomerPaymentDto> CreateCreditNoteAsync(CreditNoteCreateDto dto, CancellationToken cancellationToken = default)
        {
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            // Create a negative payment (credit note)
            var paymentDto = new PaymentCreateDto
            {
                CustomerId = dto.CustomerId,
                PaymentDate = dto.CreditNoteDate,
                TotalAmount = dto.Amount,
                PaymentMethod = "CreditNote",
                CurrencyCode = baseCurrencyCode,
                ExchangeRate = 1.0m,
                TransactionReference = dto.Reference,
                Notes = $"Credit Note: {dto.Reason}\n{dto.Notes}",
                IsCreditNote = true
            };

            return await CreateAsync(paymentDto, cancellationToken);
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

        private async Task<BusinessPartner?> GetCustomerPartnerAsync(Guid customerId, CancellationToken cancellationToken)
        {
            return await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == TenantId &&
                    p.Id == customerId &&
                    !p.IsDeleted &&
                    (p.PartnerType == "Customer" || p.PartnerType == "Both"));
        }

        private CustomerPaymentDto MapToDto(CustomerPayment payment)
        {
            return new CustomerPaymentDto
            {
                Id = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                CustomerId = payment.CustomerId,
                CustomerName = string.Empty,
                PaymentDate = payment.PaymentDate,
                TotalAmount = payment.TotalAmount,
                AllocatedAmount = payment.AllocatedAmount,
                UnallocatedAmount = payment.UnallocatedAmount,
                PaymentMethod = payment.PaymentMethod,
                CurrencyCode = payment.CurrencyCode,
                ExchangeRate = payment.ExchangeRate,
                BankAccountId = payment.BankAccountId,
                CheckNumber = payment.CheckNumber,
                TransactionReference = payment.TransactionReference,
                Notes = payment.Notes,
                Status = payment.Status,
                ClearedDate = payment.ClearedDate,
                IsCreditNote = payment.IsCreditNote,
                Allocations = payment.Allocations?.Select(a => new PaymentAllocationDto
                {
                    Id = a.Id,
                    CustomerPaymentId = a.CustomerPaymentId,
                    InvoiceId = a.InvoiceId,
                    InvoiceNumber = a.Invoice?.InvoiceNumber ?? string.Empty,
                    AllocatedAmount = a.AllocatedAmount,
                    DiscountAmount = a.DiscountAmount,
                    AllocationDate = a.AllocationDate,
                    Notes = a.Notes,
                    IsReversal = a.IsReversal
                }).ToList() ?? new List<PaymentAllocationDto>(),
                CreatedAt = payment.CreatedAt
            };
        }
    }
}
