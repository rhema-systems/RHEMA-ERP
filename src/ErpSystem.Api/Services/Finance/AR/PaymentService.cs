using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
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
using System.Data;
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
        private readonly ILogger<PaymentService> _logger;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IFxAccountingService? _fxAccountingService;

        public PaymentService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ILogger<PaymentService> logger,
            IDocumentNumberingService documentNumberingService,
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            IFxAccountingService? fxAccountingService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _financePostingEngine = financePostingEngine;
            _financeAuditService = financeAuditService;
            _fxAccountingService = fxAccountingService;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";

        public async Task<CustomerPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id)
                .Include(p => p.Allocations)
                    .ThenInclude(a => a.Invoice)
                .FirstOrDefaultAsync(cancellationToken);

            var customer = payment == null
                ? null
                : await GetCustomerPartnerAsync(payment.CustomerId, cancellationToken);

            return payment == null ? null : MapToDto(payment, customer);
        }

        public async Task<CustomerPaymentDto?> GetByPaymentNumberAsync(string paymentNumber, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.PaymentNumber == paymentNumber)
                .Include(p => p.Allocations)
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
                WithholdingTaxId = dto.WithholdingTaxId,
                WithholdingTaxAccountId = dto.WithholdingTaxAccountId,
                WithholdingTaxAmount = dto.WithholdingTaxAmount,
                VatWithholdingTaxId = dto.VatWithholdingTaxId,
                VatWithholdingAccountId = dto.VatWithholdingAccountId,
                VatWithholdingAmount = dto.VatWithholdingAmount,
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
                await AllocatePaymentCoreAsync(
                    payment.Id,
                    dto.Allocations,
                    postDiscountAdjustmentsForPostedPayment: false,
                    cancellationToken);
            }

            if (dto.IsCreditNote)
            {
                await PostCustomerCreditNoteAsync(payment.Id, cancellationToken);
            }
            else
            {
                await PostAsync(payment.Id, cancellationToken);
            }

            _logger.LogInformation("Created payment {PaymentNumber} for customer {CustomerId}, Amount: {Amount}",
                paymentNumber, customer.Id, dto.TotalAmount);

            return await GetByIdAsync(payment.Id, cancellationToken) ?? MapToDto(payment, customer);
        }

        public async Task<CustomerPaymentDto> UpdateAsync(PaymentUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == dto.Id);

            if (payment == null)
                throw new KeyNotFoundException($"Payment with Id '{dto.Id}' not found.");

            if (payment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted customer payments cannot be updated. Use a reversal, void, or adjustment workflow.");

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
            payment.WithholdingTaxId = dto.WithholdingTaxId;
            payment.WithholdingTaxAccountId = dto.WithholdingTaxAccountId;
            payment.WithholdingTaxAmount = dto.WithholdingTaxAmount;
            payment.VatWithholdingTaxId = dto.VatWithholdingTaxId;
            payment.VatWithholdingAccountId = dto.VatWithholdingAccountId;
            payment.VatWithholdingAmount = dto.VatWithholdingAmount;
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
            FinancePostingResultDto? postingResult = null;
            var wasAlreadyLinked = false;
            var transactionStarted = false;

            try
            {
                // Keep the receipt source state and the Finance-engine journal/event in one commit.
                // Serializable isolation prevents concurrent posted credits/receipts from over-settling an invoice.
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                transactionStarted = true;

                payment = await LoadPaymentForPostingAsync(id, cancellationToken);
                wasAlreadyLinked = payment.JournalEntryId.HasValue;

                var postingRequest = await BuildArReceiptPostingRequestAsync(payment, cancellationToken);
                postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

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

                await _unitOfWork.CommitAsync(cancellationToken);
                transactionStarted = false;

                if (postingResult.WasDuplicate || wasAlreadyLinked)
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

                _logger.LogInformation(
                    "Posted AR receipt {PaymentNumber} through finance posting engine with journal {JournalEntryId}. Duplicate={WasDuplicate}",
                    payment.PaymentNumber,
                    postingResult.JournalEntryId,
                    postingResult.WasDuplicate);

                await PostRealizedFxIfRequiredAsync(payment, cancellationToken);

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

            if (payment.JournalEntryId.HasValue)
            {
                if (!payment.IsCustomerAdvance)
                    throw new InvalidOperationException("Posted customer payments cannot be allocated. Use a reversal, void, or adjustment workflow.");

                // A posted customer advance is the explicit exception: applying it creates a
                // separate advance-to-AR-control reclassification through the posting engine.
                return await AllocatePostedCustomerAdvanceAsync(paymentId, allocations, cancellationToken);
            }

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
            var totalAllocationRequested = allocations.Sum(a => Math.Max(a.AllocatedAmount, 0m));

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

                var cashAmount = Math.Max(allocationDto.AllocatedAmount, 0m);
                var requestedDiscountAmount = Math.Max(allocationDto.DiscountAmount, 0m);

                if (cashAmount <= 0 && requestedDiscountAmount <= 0)
                {
                    continue;
                }

                // Create allocation
                var allocation = new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CustomerPaymentId = payment.Id,
                    InvoiceId = invoice.Id,
                    AllocatedAmount = cashAmount,
                    DiscountAmount = requestedDiscountAmount,
                    AllocationDate = now,
                    Notes = allocationDto.Notes,
                    IsReversal = false,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                // Cap at outstanding balance to prevent over-allocation
                var totalApplied = cashAmount + requestedDiscountAmount;
                var postedSalesCreditTotal = await GetPostedSalesCreditAmountForInvoiceAsync(invoice.Id, cancellationToken);
                var outstandingBalance = RoundMoney(invoice.TotalAmount - invoice.PaidAmount - postedSalesCreditTotal);
                if (outstandingBalance <= 0m)
                {
                    _logger.LogWarning("Invoice {InvoiceNumber} has no balance after posted credit notes, skipping", invoice.InvoiceNumber);
                    continue;
                }

                if (totalApplied > outstandingBalance)
                {
                    _logger.LogWarning(
                        "Allocation of {Requested} exceeds outstanding balance {Outstanding} on invoice {InvoiceNumber}. Capping.",
                        totalApplied, outstandingBalance, invoice.InvoiceNumber);

                    // Proportionally reduce both amounts so their sum equals outstandingBalance
                    var ratio = totalApplied > 0 ? outstandingBalance / totalApplied : 0m;
                    allocation.AllocatedAmount = Math.Round(cashAmount * ratio, 2);
                    allocation.DiscountAmount = outstandingBalance - allocation.AllocatedAmount;
                    if (allocation.DiscountAmount < 0) allocation.DiscountAmount = 0;
                    totalApplied = outstandingBalance;
                }

                payment.Allocations.Add(allocation);
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

        private async Task<PaymentAllocationResultDto> AllocatePostedCustomerAdvanceAsync(
            Guid paymentId,
            IReadOnlyCollection<InvoiceAllocationDto> requestedAllocations,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for customer advance application.");
            if (requestedAllocations.Count == 0)
                throw new InvalidOperationException("At least one customer-invoice allocation is required to apply an advance.");
            if (requestedAllocations.Any(a => a.AllocatedAmount <= 0m || a.DiscountAmount != 0m))
                throw new InvalidOperationException("Customer advance applications support positive cash allocations only; use a dedicated adjustment workflow for discounts.");

            var transactionStarted = false;
            try
            {
                // Keep the allocation, customer/invoice snapshots and the advance reclassification
                // in one serializable transaction so an advance cannot be applied twice.
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                transactionStarted = true;

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
                if (!string.Equals(NormalizeCurrency(payment.CurrencyCode, functionalCurrency), functionalCurrency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Foreign-currency customer advance application is not supported until advance FX settlement is implemented.");

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

                var currentlyAllocated = RoundMoney(payment.Allocations.Where(a => !a.IsReversal).Sum(a => a.AllocatedAmount));
                var requestedTotal = RoundMoney(requestedAllocations.Sum(a => a.AllocatedAmount));
                if (requestedTotal > RoundMoney(payment.TotalAmount - currentlyAllocated))
                    throw new InvalidOperationException("Customer advance application exceeds the unallocated advance balance.");

                var now = DateTime.UtcNow;
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
                    if (!string.Equals(NormalizeCurrency(invoice.CurrencyCode, functionalCurrency), functionalCurrency, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Foreign-currency customer advance application is not supported until advance FX settlement is implemented.");

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
                        AllocatedAmount = RoundMoney(requested.AllocatedAmount),
                        AllocationDate = now,
                        Notes = requested.Notes,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    // Register the allocation explicitly as Added. Updating the receipt source
                    // record later must not turn this new row into a modified-only graph entry.
                    await _unitOfWork.Repository<PaymentAllocation>().AddAsync(allocation);
                    payment.Allocations.Add(allocation);
                    // This is a read-side operational snapshot. Recompute it from allocations so
                    // a stale value cannot cause an advance to be over-applied after a retry.
                    payment.AllocatedAmount = RoundMoney(payment.Allocations
                        .Where(a => !a.IsReversal)
                        .Sum(a => a.AllocatedAmount));
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

                    var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = "AR",
                        SourceDocumentType = "CustomerPaymentAdvanceApplication",
                        SourceDocumentId = allocation.Id,
                        SourceDocumentTenantId = payment.TenantId,
                        PostingAction = "Post",
                        SourceDocumentReference = $"{payment.PaymentNumber}:{invoice.InvoiceNumber}",
                        Description = $"Apply customer advance {payment.PaymentNumber} to invoice {invoice.InvoiceNumber}",
                        PostingDate = now,
                        JournalType = "AR Customer Advance Application",
                        BookClassification = "IFRS",
                        FunctionalCurrencyCode = functionalCurrency,
                        IdempotencyKey = $"AR:CustomerPaymentAdvanceApplication:{payment.TenantId:N}:{allocation.Id:N}:Post",
                        ReturnExistingOnDuplicate = true,
                        Lines = new[]
                        {
                            BuildPostingLine(advanceAccountId, $"Apply customer advance {payment.PaymentNumber}", allocation.AllocatedAmount, 0m, functionalCurrency, functionalCurrency, 1m, now, payment.PaymentNumber, 1, "AR-CustomerAdvance"),
                            BuildPostingLine(arAccountId, $"Apply customer advance {payment.PaymentNumber}", 0m, allocation.AllocatedAmount, functionalCurrency, functionalCurrency, 1m, now, payment.PaymentNumber, 2, "AR-Control")
                        }
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

                await _unitOfWork.CommitAsync(cancellationToken);
                transactionStarted = false;
                result.Success = true;
                result.TotalAllocated = payment.AllocatedAmount;
                result.RemainingUnallocated = RoundMoney(payment.TotalAmount - payment.AllocatedAmount);
                result.Message = $"Applied customer advance to {result.Allocations.Count} invoice(s).";
                await RecordArReceiptAuditAsync(
                    FinanceAuditEvents.ArCustomerAdvanceApplied,
                    payment,
                    afterValues: new { result.TotalAllocated, result.RemainingUnallocated, AllocationCount = result.Allocations.Count },
                    comment: "Posted customer advance application through the central finance posting engine.",
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

            if (allocation.IsReversal)
                throw new InvalidOperationException("This allocation has already been reversed.");

            if (allocation.CustomerPayment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted customer payment allocations cannot be reversed by mutation. Use a reversal, void, or adjustment workflow.");

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

            if (payment.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted customer payments cannot be bounced by mutation until AR receipt reversal posting is implemented.");

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

        private async Task<CustomerPayment> LoadPaymentForPostingAsync(Guid id, CancellationToken cancellationToken)
        {
            var payment = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == id && !p.IsDeleted)
                .Include(p => p.BankAccount)
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

                if (allocation.AllocatedAmount < 0m || allocation.DiscountAmount < 0m)
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
                    .SumAsync(a => a.AllocatedAmount + a.DiscountAmount, cancellationToken);
                var postedSalesCreditTotal = await GetPostedSalesCreditAmountForInvoiceAsync(
                    allocation.InvoiceId,
                    cancellationToken);

                if (RoundMoney(totalInvoiceSettlement + postedSalesCreditTotal) > RoundMoney(allocation.Invoice.TotalAmount))
                    throw new InvalidOperationException($"AR receipt would over-settle invoice '{allocation.Invoice.InvoiceNumber}'.");
            }

            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            if (isCustomerAdvance && !string.Equals(paymentCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Foreign-currency customer advances are not supported until advance application FX settlement is implemented.");
            }
            foreach (var allocation in activeAllocations)
            {
                var invoiceCurrency = NormalizeCurrency(allocation.Invoice.CurrencyCode, paymentCurrency);
                if (!string.Equals(invoiceCurrency, paymentCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Cross-currency AR settlements are not supported in FX Batch 18. Customer receipt currency must match each allocated invoice currency.");
                }
            }

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

            var bankAccountId = payment.BankAccountId
                ?? settings.DefaultBankAccountId
                ?? throw new InvalidOperationException("Bank account is not configured for AR receipt posting.");
            var bankAccount = await _unitOfWork.Repository<BankAccount>()
                .GetQueryable(a => a.TenantId == tenantId && a.Id == bankAccountId && !a.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (bankAccount == null)
                throw new InvalidOperationException("AR receipt bank account was not found for this tenant.");

            if (!bankAccount.IsActive)
                throw new InvalidOperationException($"AR receipt bank account '{bankAccount.AccountName}' is inactive.");

            if (!bankAccount.GLAccountId.HasValue)
                throw new InvalidOperationException($"AR receipt bank account '{bankAccount.AccountName}' is not linked to a GL account.");

            await ResolveReceiptPostingAccountAsync(bankAccount.GLAccountId.Value, "bank/cash account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            var discountAllowed = RoundMoney(activeAllocations.Sum(a => a.DiscountAmount));
            var withholdingTaxAmount = RoundMoney(payment.WithholdingTaxAmount);
            var vatWithholdingAmount = RoundMoney(payment.VatWithholdingAmount);
            if (withholdingTaxAmount < 0m || vatWithholdingAmount < 0m)
                throw new InvalidOperationException("AR receipt withholding amounts cannot be negative.");

            var allocatedSettlementAmount = RoundMoney(activeAllocations.Sum(a => a.AllocatedAmount));
            if (!isCustomerAdvance && allocatedSettlementAmount != RoundMoney(payment.TotalAmount + withholdingTaxAmount + vatWithholdingAmount))
                throw new InvalidOperationException("AR receipt allocations must equal cash received plus configured withholding amounts before posting. Use the customer-advance path for an unapplied receipt.");

            var arSettlementAmount = RoundMoney(payment.TotalAmount + discountAllowed + withholdingTaxAmount + vatWithholdingAmount);

            var postingLines = new List<FinancePostingLineDto>();
            var lineNumber = 1;

            postingLines.Add(BuildPostingLine(
                bankAccount.GLAccountId.Value,
                $"AR receipt {payment.PaymentNumber}",
                debitTransactionAmount: payment.TotalAmount,
                creditTransactionAmount: 0m,
                paymentCurrency,
                functionalCurrency,
                exchangeRate,
                payment.PaymentDate,
                payment.PaymentNumber,
                lineNumber++,
                    "AR-Bank"));

            if (withholdingTaxAmount > 0m)
            {
                var withholdingAccountId = payment.WithholdingTaxAccountId
                    ?? await ResolveConfiguredWithholdingReceivableAccountAsync(payment.PaymentDate, payment.WithholdingTaxId, TaxCategory.Withholding, cancellationToken)
                    ?? throw new InvalidOperationException("AR withholding tax receivable account is not configured for this tenant.");
                await ResolveReceiptPostingAccountAsync(withholdingAccountId, "withholding tax receivable account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

                postingLines.Add(BuildPostingLine(
                    withholdingAccountId,
                    $"Withholding tax receivable - {payment.PaymentNumber}",
                    debitTransactionAmount: withholdingTaxAmount,
                    creditTransactionAmount: 0m,
                    paymentCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    lineNumber++,
                    "AR-WHT"));
            }

            if (vatWithholdingAmount > 0m)
            {
                var vatWithholdingAccountId = payment.VatWithholdingAccountId
                    ?? await ResolveConfiguredWithholdingReceivableAccountAsync(payment.PaymentDate, payment.VatWithholdingTaxId, TaxCategory.VatWithholding, cancellationToken)
                    ?? throw new InvalidOperationException("AR VAT withholding receivable account is not configured for this tenant.");
                await ResolveReceiptPostingAccountAsync(vatWithholdingAccountId, "VAT withholding receivable account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

                postingLines.Add(BuildPostingLine(
                    vatWithholdingAccountId,
                    $"VAT withholding receivable - {payment.PaymentNumber}",
                    debitTransactionAmount: vatWithholdingAmount,
                    creditTransactionAmount: 0m,
                    paymentCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    lineNumber++,
                    "AR-VAT-WHT"));
            }

            if (discountAllowed > 0m)
            {
                var discountAccountId = settings.DiscountAllowedAccountId
                    ?? throw new InvalidOperationException("Sales discounts allowed account is not configured for this tenant.");
                await ResolveReceiptPostingAccountAsync(discountAccountId, "sales discount allowed account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                postingLines.Add(BuildPostingLine(
                    discountAccountId,
                    $"Sales discount allowed - {payment.PaymentNumber}",
                    debitTransactionAmount: discountAllowed,
                    creditTransactionAmount: 0m,
                    paymentCurrency,
                    functionalCurrency,
                    exchangeRate,
                    payment.PaymentDate,
                    payment.PaymentNumber,
                    lineNumber++,
                    "AR-Discount"));
            }

            postingLines.Add(BuildPostingLine(
                creditAccountId,
                isCustomerAdvance
                    ? $"Customer advance {payment.PaymentNumber}"
                    : $"AR receipt {payment.PaymentNumber}",
                debitTransactionAmount: 0m,
                creditTransactionAmount: isCustomerAdvance ? payment.TotalAmount : arSettlementAmount,
                paymentCurrency,
                functionalCurrency,
                exchangeRate,
                payment.PaymentDate,
                payment.PaymentNumber,
                lineNumber++,
                isCustomerAdvance ? "AR-CustomerAdvance" : "AR-Control"));

            if (RoundMoney(postingLines.Sum(l => l.DebitAmount)) != RoundMoney(postingLines.Sum(l => l.CreditAmount)))
                throw new InvalidOperationException("AR receipt posting is not balanced.");

            payment.BankAccountId ??= bankAccount.Id;
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
            var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
            if (string.Equals(paymentCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_fxAccountingService == null)
            {
                throw new InvalidOperationException("FX accounting service is not configured for AR realized FX settlement posting.");
            }

            await _fxAccountingService.PostRealizedFxForArReceiptAsync(payment.Id, cancellationToken);
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

        private static string NormalizeCurrency(string? currencyCode, string defaultValue)
            => string.IsNullOrWhiteSpace(currencyCode)
                ? defaultValue.Trim().ToUpperInvariant()
                : currencyCode.Trim().ToUpperInvariant();

        private static decimal RoundMoney(decimal amount)
            => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

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
                TransactionNumber = transactionNumber,
                TransactionDate = payment.PaymentDate,
                TransactionType = CashTransactionType.Receipt,
                BankAccountId = bankAccountId.Value,
                Amount = payment.TotalAmount,
                Currency = currencyCode,
                ExchangeRate = exchangeRate,
                BaseAmount = baseAmount,
                ReferenceNumber = payment.PaymentNumber,
                PayeeOrPayer = customer.PartnerName,
                Description = $"AR Customer Payment {payment.PaymentNumber} - {customer.PartnerName}",
                GLAccountId = arAccountId,
                IsReconciled = false,
                IsPosted = true,
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
                CurrencyCode = payment.CurrencyCode,
                ExchangeRate = payment.ExchangeRate,
                BankAccountId = payment.BankAccountId,
                CheckNumber = payment.CheckNumber,
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
