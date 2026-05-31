using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services.Finance.AR
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITaxCalculationEngine _taxEngine;
        private readonly ISubledgerPostingService _subledgerPostingService;
        private readonly IInventoryValuationService _inventoryValuationService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITaxCalculationEngine taxEngine,
            ISubledgerPostingService subledgerPostingService,
            IInventoryValuationService inventoryValuationService,
            ITenantSettingsService tenantSettingsService,
            ILogger<InvoiceService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _taxEngine = taxEngine;
            _subledgerPostingService = subledgerPostingService;
            _inventoryValuationService = inventoryValuationService;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
        private string UserName => _currentUser.UserName ?? "system";

        public async Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(cancellationToken);

            return invoice == null ? null : MapToDto(invoice);
        }

        public async Task<InvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.InvoiceNumber == invoiceNumber)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(cancellationToken);

            return invoice == null ? null : MapToDto(invoice);
        }

        public async Task<PagedResult<InvoiceDto>> GetAllAsync(InvoiceQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                queryable = queryable.Where(i =>
                    i.InvoiceNumber.Contains(query.SearchTerm) ||
                    i.CustomerName.Contains(query.SearchTerm) ||
                    (i.Reference != null && i.Reference.Contains(query.SearchTerm)));
            }

            if (query.CustomerId.HasValue)
                queryable = queryable.Where(i => i.CustomerId == query.CustomerId.Value);

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (Enum.TryParse<InvoiceStatus>(query.Status, out var status))
                    queryable = queryable.Where(i => i.Status == status);
            }

            if (query.FromDate.HasValue)
                queryable = queryable.Where(i => i.InvoiceDate >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                queryable = queryable.Where(i => i.InvoiceDate <= query.ToDate.Value);

            var now = DateTime.UtcNow;
            if (query.IsOverdue.HasValue && query.IsOverdue.Value)
            {
                queryable = queryable.Where(i =>
                    i.DueDate.HasValue &&
                    i.DueDate.Value < now &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            }

            // Get total count
            var totalCount = await queryable.CountAsync(cancellationToken);

            // Apply sorting
            queryable = query.SortBy?.ToLower() switch
            {
                "invoicenumber" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.InvoiceNumber)
                    : queryable.OrderBy(i => i.InvoiceNumber),
                "customer" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.CustomerName)
                    : queryable.OrderBy(i => i.CustomerName),
                "amount" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.TotalAmount)
                    : queryable.OrderBy(i => i.TotalAmount),
                "balance" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.TotalAmount - i.PaidAmount)
                    : queryable.OrderBy(i => i.TotalAmount - i.PaidAmount),
                _ => queryable.OrderByDescending(i => i.InvoiceDate)
            };

            // Apply pagination
            var invoices = await queryable
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .ToListAsync(cancellationToken);

            return new PagedResult<InvoiceDto>
            {
                Items = invoices.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        public async Task<InvoiceDto> CreateAsync(InvoiceCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate customer exists
            var customer = await _unitOfWork.Repository<Customer>()
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == dto.CustomerId);

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{dto.CustomerId}' not found.");

            // Check for duplicate invoice
            if (!string.IsNullOrWhiteSpace(dto.Reference))
            {
                var isDuplicate = await IsDuplicateAsync(dto.CustomerId, dto.Reference, dto.InvoiceDate, cancellationToken);
                if (isDuplicate)
                    throw new InvalidOperationException($"Duplicate invoice detected with reference '{dto.Reference}'.");
            }

            // Generate invoice number
            var invoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken);

            // Resolve base currency
            var baseCurrency = "GHS";
            try
            {
                baseCurrency = await _tenantSettingsService.GetBaseCurrencyAsync() ?? "GHS";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve base currency from tenant settings. Defaulting to GHS.");
            }

            var currency = (dto.CurrencyCode ?? baseCurrency).Trim().ToUpperInvariant();
            decimal exchangeRate = dto.ExchangeRate;

            if (currency == baseCurrency.ToUpperInvariant())
            {
                exchangeRate = 1.0m;
            }
            else
            {
                if (exchangeRate <= 0.0m)
                {
                    throw new ArgumentException($"Exchange rate must be greater than zero for foreign currency '{dto.CurrencyCode}'.");
                }
            }

            var now = DateTime.UtcNow;
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                InvoiceNumber = invoiceNumber,
                CustomerId = dto.CustomerId,
                CustomerName = customer.CustomerName,
                CustomerAddress = customer.Address,
                InvoiceDate = dto.InvoiceDate,
                DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(customer.PaymentTermsDays),
                Reference = dto.Reference,
                Notes = dto.Notes,
                CurrencyCode = currency,
                ExchangeRate = exchangeRate,
                PaymentTermsDays = customer.PaymentTermsDays,
                TaxGroupId = dto.TaxGroupId,
                Status = InvoiceStatus.Draft,
                CreatedAt = now,
                CreatedBy = UserName
            };

            // Process line items and calculate taxes
            decimal subtotal = 0;
            decimal totalTax = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineTotal = lineDto.Quantity * lineDto.UnitPrice;
                var lineDiscount = lineTotal * (lineDto.DiscountPercentage / 100);
                var lineNetAmount = lineTotal - lineDiscount;

                var lineTaxGroupId = lineDto.TaxGroupId ?? dto.TaxGroupId;
                decimal lineTax = 0;
                string? resolvedTaxCode = lineDto.TaxCode;
                decimal resolvedTaxRate = 0;

                if (lineTaxGroupId.HasValue)
                {
                    var transactionType = lineDto.LineItemType == "Product" 
                        ? TaxTransactionType.SaleOfGoods 
                        : TaxTransactionType.SaleOfServices;

                    var taxRequest = new TaxCalculationRequestDto
                    {
                        TransactionType = transactionType,
                        BaseAmount = lineNetAmount,
                        TaxGroupId = lineTaxGroupId.Value,
                        BusinessPartnerId = dto.BusinessPartnerId
                    };

                    var taxResult = await _taxEngine.CalculateTaxesAsync(taxRequest, cancellationToken);
                    lineTax = taxResult.TotalTaxAmount;
                    resolvedTaxRate = taxResult.EffectiveTaxRate;
                    resolvedTaxCode = taxResult.TaxGroupName ?? lineDto.TaxCode;
                }
                else if (!string.IsNullOrWhiteSpace(lineDto.TaxCode))
                {
                    var transactionType = lineDto.LineItemType == "Product" 
                        ? TaxTransactionType.SaleOfGoods 
                        : TaxTransactionType.SaleOfServices;

                    var taxRequest = new TaxCalculationRequestDto
                    {
                        TransactionType = transactionType,
                        BaseAmount = lineNetAmount,
                        CustomerId = dto.CustomerId
                    };

                    var taxResult = await _taxEngine.CalculateTaxesAsync(taxRequest, cancellationToken);
                    lineTax = taxResult.TotalTaxAmount;
                    resolvedTaxRate = taxResult.EffectiveTaxRate;
                    resolvedTaxCode = taxResult.TaxGroupName ?? lineDto.TaxCode;
                }

                // Parse LineItemType from string
                var lineItemType = Enum.TryParse<LineItemType>(lineDto.LineItemType, out var parsedType)
                    ? parsedType
                    : LineItemType.Product;

                var lineItem = new InvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    InvoiceId = invoice.Id,
                    LineItemType = lineItemType,
                    ProductId = lineDto.ProductId,
                    GLAccountId = lineDto.GLAccountId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxGroupId = lineTaxGroupId,
                    TaxCode = resolvedTaxCode,
                    TaxRate = resolvedTaxRate,
                    TaxAmount = lineTax,
                    Unit = lineDto.Unit,
                    DiscountPercentage = lineDto.DiscountPercentage,
                    DiscountAmount = lineDiscount,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                invoice.LineItems.Add(lineItem);
                subtotal += lineNetAmount;
                totalTax += lineTax;
            }

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.DiscountAmount = dto.DiscountAmount;
            invoice.TotalAmount = subtotal + totalTax - dto.DiscountAmount;
            invoice.PaidAmount = 0;
            
            // Set Multicurrency fields
            invoice.ExchangeRate = exchangeRate;
            invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

            // Check credit limit before saving (using Base Currency)
            // Note: Customer.OutstandingBalance is now assumed to be in Base Currency
            var newOutstanding = customer.OutstandingBalance + invoice.BaseCurrencyAmount;
            if (newOutstanding > customer.CreditLimit)
            {
                _logger.LogWarning("Credit limit exceeded for customer {CustomerId}. Limit: {Limit} {BaseCurrency}, New Outstanding: {Outstanding} {BaseCurrency}",
                    customer.Id, customer.CreditLimit, baseCurrency, newOutstanding, baseCurrency);
                // Allow creation but might flag for approval in a real system
            }

            await _unitOfWork.Repository<Invoice>().AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created invoice {InvoiceNumber} for customer {CustomerId}", invoiceNumber, customer.Id);

            return MapToDto(invoice);
        }

        public async Task<InvoiceDto> UpdateAsync(InvoiceUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == dto.Id)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{dto.Id}' not found.");

            // Only allow updates if invoice is in Draft status
            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be updated.");

            // Resolve base currency
            var baseCurrency = "GHS";
            try
            {
                baseCurrency = await _tenantSettingsService.GetBaseCurrencyAsync() ?? "GHS";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve base currency from tenant settings. Defaulting to GHS.");
            }

            var currency = (dto.CurrencyCode ?? baseCurrency).Trim().ToUpperInvariant();
            decimal exchangeRate = dto.ExchangeRate;

            if (currency == baseCurrency.ToUpperInvariant())
            {
                exchangeRate = 1.0m;
            }
            else
            {
                if (exchangeRate <= 0.0m)
                {
                    throw new ArgumentException($"Exchange rate must be greater than zero for foreign currency '{dto.CurrencyCode}'.");
                }
            }

            var now = DateTime.UtcNow;
            invoice.InvoiceDate = dto.InvoiceDate;
            invoice.DueDate = dto.DueDate;
            invoice.Reference = dto.Reference;
            invoice.Notes = dto.Notes;
            invoice.DiscountAmount = dto.DiscountAmount;
            invoice.TaxGroupId = dto.TaxGroupId;
            invoice.CurrencyCode = currency;
            invoice.ExchangeRate = exchangeRate;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // Remove existing line items
            foreach (var existingLine in invoice.LineItems.ToList())
            {
                await _unitOfWork.Repository<InvoiceLineItem>().DeleteAsync(existingLine);
            }
            invoice.LineItems.Clear();

            // Add updated line items
            decimal subtotal = 0;
            decimal totalTax = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineTotal = lineDto.Quantity * lineDto.UnitPrice;
                var lineDiscount = lineTotal * (lineDto.DiscountPercentage / 100);
                var lineNetAmount = lineTotal - lineDiscount;

                var lineTaxGroupId = lineDto.TaxGroupId ?? dto.TaxGroupId;
                decimal lineTax = 0;
                string? resolvedTaxCode = lineDto.TaxCode;
                decimal resolvedTaxRate = 0;

                if (lineTaxGroupId.HasValue)
                {
                    var transactionType = lineDto.LineItemType == "Product" 
                        ? TaxTransactionType.SaleOfGoods 
                        : TaxTransactionType.SaleOfServices;

                    var taxRequest = new TaxCalculationRequestDto
                    {
                        TransactionType = transactionType,
                        BaseAmount = lineNetAmount,
                        TaxGroupId = lineTaxGroupId.Value,
                        BusinessPartnerId = invoice.BusinessPartnerId
                    };

                    var taxResult = await _taxEngine.CalculateTaxesAsync(taxRequest, cancellationToken);
                    lineTax = taxResult.TotalTaxAmount;
                    resolvedTaxRate = taxResult.EffectiveTaxRate;
                    resolvedTaxCode = taxResult.TaxGroupName ?? lineDto.TaxCode;
                }
                else if (!string.IsNullOrWhiteSpace(lineDto.TaxCode))
                {
                    var transactionType = lineDto.LineItemType == "Product" 
                        ? TaxTransactionType.SaleOfGoods 
                        : TaxTransactionType.SaleOfServices;

                    var taxRequest = new TaxCalculationRequestDto
                    {
                        TransactionType = transactionType,
                        BaseAmount = lineNetAmount,
                        CustomerId = invoice.CustomerId
                    };

                    var taxResult = await _taxEngine.CalculateTaxesAsync(taxRequest, cancellationToken);
                    lineTax = taxResult.TotalTaxAmount;
                    resolvedTaxRate = taxResult.EffectiveTaxRate;
                    resolvedTaxCode = taxResult.TaxGroupName ?? lineDto.TaxCode;
                }

                // Parse LineItemType from string (mirrors CreateAsync logic)
                var lineItemType = Enum.TryParse<LineItemType>(lineDto.LineItemType, out var parsedType)
                    ? parsedType
                    : LineItemType.Product;

                var lineItem = new InvoiceLineItem
                {
                    Id = lineDto.Id ?? Guid.NewGuid(),
                    TenantId = TenantId,
                    InvoiceId = invoice.Id,
                    LineItemType = lineItemType,
                    ProductId = lineDto.ProductId,
                    GLAccountId = lineDto.GLAccountId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxGroupId = lineTaxGroupId,
                    TaxCode = resolvedTaxCode,
                    TaxRate = resolvedTaxRate,
                    TaxAmount = lineTax,
                    Unit = lineDto.Unit,
                    DiscountPercentage = lineDto.DiscountPercentage,
                    DiscountAmount = lineDiscount,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                invoice.LineItems.Add(lineItem);
                subtotal += lineNetAmount;
                totalTax += lineTax;
            }

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.TotalAmount = subtotal + totalTax - dto.DiscountAmount;
            
            // Recalculate BaseCurrencyAmount
            invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

            await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated invoice {InvoiceId}", invoice.Id);

            return MapToDto(invoice);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == id);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{id}' not found.");

            // Only allow deletion if invoice is in Draft status
            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be deleted.");

            await _unitOfWork.Repository<Invoice>().DeleteAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted invoice {InvoiceId}", id);
        }

        public async Task<InvoiceDto> SendInvoiceAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.Customer)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{id}' not found.");

            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be sent.");                 

            var now = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.Sent;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // Process Inventory Issues for Inventory-type line items
            foreach (var line in invoice.LineItems.Where(l => l.LineItemType == LineItemType.Inventory))
            {
                if (line.InventoryItemId.HasValue && line.WarehouseId.HasValue)
                {
                    var totalCost = await _inventoryValuationService.ProcessIssueAsync(
                        line.InventoryItemId.Value,
                        line.WarehouseId.Value,
                        line.LocationId,
                        line.Quantity,
                        ErpSystem.Core.Entities.Inventory.InventoryMovementType.SalesIssue,
                        ReferenceType.SalesInvoice,
                        invoice.InvoiceNumber,
                        invoice.Id,
                        line.LotNumber,
                        line.SerialNumber
                    );

                    line.CostTotal = totalCost;
                    line.UnitCost = line.Quantity > 0 ? totalCost / line.Quantity : 0;
                    
                    await _unitOfWork.Repository<InvoiceLineItem>().UpdateAsync(line);
                }
            }

            // Record Tax Audit Trail
            var taxResult = new TaxCalculationResultDto { BaseAmount = invoice.SubTotal, GrandTotal = invoice.TotalAmount, TotalTaxAmount = invoice.TaxAmount };
            foreach (var lineItem in invoice.LineItems.Where(li => li.TaxGroupId.HasValue))
            {
                var transactionType = lineItem.LineItemType == LineItemType.Product
                    ? TaxTransactionType.SaleOfGoods
                    : TaxTransactionType.SaleOfServices;

                var taxRequest = new TaxCalculationRequestDto
                {
                    TransactionType = transactionType,
                    BaseAmount = lineItem.LineTotal - lineItem.DiscountAmount,
                    TaxGroupId = lineItem.TaxGroupId.Value,
                    BusinessPartnerId = invoice.BusinessPartnerId
                };

                var lineTaxResult = await _taxEngine.CalculateTaxesAsync(taxRequest, cancellationToken);
                taxResult.TaxBreakdowns.AddRange(lineTaxResult.TaxBreakdowns);
            }
            
            await _taxAuditService.RecordTaxCalculationsAsync("CustomerInvoice", invoice.Id, taxResult, cancellationToken);

            // Update customer's outstanding balance
            if (invoice.Customer != null)
            {
                // Use BaseCurrencyAmount for standardized balance
                invoice.Customer.OutstandingBalance += invoice.BaseCurrencyAmount;
                invoice.Customer.LastOrderDate = invoice.InvoiceDate;
                invoice.Customer.UpdatedAt = now;
                invoice.Customer.UpdatedBy = UserName;
                await _unitOfWork.Repository<Customer>().UpdateAsync(invoice.Customer);
            }

            await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Post to GL
            await _subledgerPostingService.PostArInvoiceAsync(invoice.Id, cancellationToken);

            _logger.LogInformation("Sent invoice {InvoiceNumber}", invoice.InvoiceNumber);

            return MapToDto(invoice);
        }

        public async Task<InvoiceDto> VoidInvoiceAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{id}' not found.");

            if (invoice.Status == InvoiceStatus.Cancelled)
                throw new InvalidOperationException("Invoice is already cancelled.");

            var now = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.Cancelled;
            invoice.Notes = $"{invoice.Notes}\n\nVoided on {now:yyyy-MM-dd HH:mm}: {reason}";
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // Reverse the unpaid portion of the invoice from customer's outstanding balance.
            // If partially paid, only the remaining outstanding (BaseCurrencyAmount - PaidAmount) 
            // should be reversed. The paid allocations will be reversed separately via payment reversal.
            if (invoice.Customer != null)
            {
                var unpaidPortion = invoice.BaseCurrencyAmount - invoice.PaidAmount;
                if (unpaidPortion > 0)
                {
                    invoice.Customer.OutstandingBalance -= unpaidPortion;
                    invoice.Customer.UpdatedAt = now;
                    invoice.Customer.UpdatedBy = UserName;
                    await _unitOfWork.Repository<Customer>().UpdateAsync(invoice.Customer);
                }
            }

            await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Voided invoice {InvoiceNumber}. Reason: {Reason}", invoice.InvoiceNumber, reason);

            return MapToDto(invoice);
        }

        public async Task<List<PaymentAllocationDto>> GetInvoiceAllocationsAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var allocations = await _unitOfWork.Repository<PaymentAllocation>()
                .GetQueryable(a => a.TenantId == TenantId && a.InvoiceId == invoiceId)
                .Include(a => a.CustomerPayment)
                .OrderByDescending(a => a.AllocationDate)
                .ToListAsync(cancellationToken);

            return allocations.Select(a => new PaymentAllocationDto
            {
                Id = a.Id,
                CustomerPaymentId = a.CustomerPaymentId,
                PaymentNumber = a.CustomerPayment.PaymentNumber,
                InvoiceId = a.InvoiceId,
                AllocatedAmount = a.AllocatedAmount,
                DiscountAmount = a.DiscountAmount,
                AllocationDate = a.AllocationDate,
                Notes = a.Notes,
                IsReversal = a.IsReversal
            }).ToList();
        }

        public async Task<byte[]> GenerateInvoicePrintAsync(Guid id, string format = "PDF", CancellationToken cancellationToken = default)
        {
            // Placeholder - would integrate with a PDF generation library
            await Task.CompletedTask;
            throw new NotImplementedException("Invoice printing will be implemented with a PDF library.");
        }

        public async Task<bool> IsDuplicateAsync(Guid customerId, string reference, DateTime invoiceDate, CancellationToken cancellationToken = default)
        {
            var dateTolerance = invoiceDate.AddDays(-30); // Check within 30 days

            var duplicate = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.CustomerId == customerId &&
                    i.Reference == reference &&
                    i.InvoiceDate >= dateTolerance &&
                    i.Status != InvoiceStatus.Cancelled)
                .AnyAsync(cancellationToken);

            return duplicate;
        }

        private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
        {
            var prefix = "INV";
            var currentYear = DateTime.UtcNow.Year;
            var currentMonth = DateTime.UtcNow.Month;

            // Get the last invoice for this month
            var lastInvoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.InvoiceNumber.StartsWith($"{prefix}-{currentYear}{currentMonth:00}"))
                .OrderByDescending(i => i.InvoiceNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastInvoice == null)
                return $"{prefix}-{currentYear}{currentMonth:00}-0001";

            // Extract sequence number and increment
            var parts = lastInvoice.InvoiceNumber.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var lastSeq))
            {
                return $"{prefix}-{currentYear}{currentMonth:00}-{(lastSeq + 1):0000}";
            }

            return $"{prefix}-{currentYear}{currentMonth:00}-0001";
        }

        private InvoiceDto MapToDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerId = invoice.CustomerId,
                CustomerName = invoice.CustomerName,
                CustomerAddress = invoice.CustomerAddress,
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                SubTotal = invoice.SubTotal,
                TaxAmount = invoice.TaxAmount,
                DiscountAmount = invoice.DiscountAmount,
                TotalAmount = invoice.TotalAmount,
                PaidAmount = invoice.PaidAmount,
                BalanceAmount = invoice.BalanceAmount,
                Status = invoice.Status.ToString(),
                Notes = invoice.Notes,
                Reference = invoice.Reference,
                CurrencyCode = invoice.CurrencyCode,
                ExchangeRate = invoice.ExchangeRate,
                PaymentTermsDays = invoice.PaymentTermsDays,
                TaxGroupId = invoice.TaxGroupId,
                LineItems = invoice.LineItems.Select(li => new InvoiceLineItemDto
                {
                    Id = li.Id,
                    LineItemType = li.LineItemType.ToString(),
                    ProductId = li.ProductId,
                    GLAccountId = li.GLAccountId,
                    GLAccountCode = li.GLAccount?.AccountCode,
                    GLAccountName = li.GLAccount?.AccountName,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    LineTotal = li.LineTotal,
                    TaxGroupId = li.TaxGroupId,
                    TaxRate = li.TaxRate,
                    TaxAmount = li.TaxAmount,
                    TaxCode = li.TaxCode,
                    Unit = li.Unit,
                    DiscountPercentage = li.DiscountPercentage,
                    DiscountAmount = li.DiscountAmount
                }).ToList(),
                CreatedAt = invoice.CreatedAt
            };
        }
    }
}
