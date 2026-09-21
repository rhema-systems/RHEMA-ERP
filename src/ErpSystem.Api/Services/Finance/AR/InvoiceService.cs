using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Finance.Integration;
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
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ITaxCalculationEngine _taxEngine;
        private readonly IInventoryValuationService _inventoryValuationService;
        private readonly ILogger<InvoiceService> _logger;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IFinanceSourceDimensionService? _sourceDimensions;
        private readonly IWorkflowIntegrationService? _workflowIntegration;

        public InvoiceService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITaxCalculationEngine taxEngine,
            IInventoryValuationService inventoryValuationService,
            ILogger<InvoiceService> logger,
            IDocumentNumberingService documentNumberingService,
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            IFinanceSourceDimensionService? sourceDimensions = null,
            IWorkflowIntegrationService? workflowIntegration = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _taxEngine = taxEngine;
            _inventoryValuationService = inventoryValuationService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _financePostingEngine = financePostingEngine;
            _financeAuditService = financeAuditService;
            _sourceDimensions = sourceDimensions;
            _workflowIntegration = workflowIntegration;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";

        public async Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .FirstOrDefaultAsync(cancellationToken);

            return invoice == null ? null : MapToDto(invoice);
        }

        public async Task<InvoiceDto?> GetByIdAsync(
            Guid id,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default)
        {
            EnsureCustomerInvoiceRoute(producer);
            var invoice = await LoadInvoiceWithLinesAsync(id, cancellationToken);
            var result = MapToDto(invoice);
            if (_sourceDimensions is not null)
            {
                result.FinanceDimensions = await _sourceDimensions.GetAsync(
                    producer,
                    invoice.Id,
                    invoice.InvoiceDate,
                    await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                    cancellationToken);
            }

            return result;
        }

        public async Task<InvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.InvoiceNumber == invoiceNumber)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
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
                queryable = queryable.Where(i => i.BusinessPartnerId == query.CustomerId.Value);

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

            if (query.IsOpeningBalance.HasValue)
                queryable = queryable.Where(i => i.IsOpeningBalance == query.IsOpeningBalance.Value);

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

        public Task<InvoiceDto> CreateAsync(
            InvoiceCreateDto dto,
            CancellationToken cancellationToken = default) =>
            CreateCoreAsync(dto, null, cancellationToken);

        public Task<InvoiceDto> CreateAsync(
            InvoiceCreateDto dto,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            CreateCoreAsync(dto, EnsureCustomerInvoiceRoute(producer), cancellationToken);

        private async Task<InvoiceDto> CreateCoreAsync(
            InvoiceCreateDto dto,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            // Validate customer business partner exists
            var customer = await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(c =>
                    c.TenantId == TenantId &&
                    c.Id == dto.CustomerId &&
                    !c.IsDeleted &&
                    (c.PartnerType == "Customer" || c.PartnerType == "Both"));

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{dto.CustomerId}' not found.");

            var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId ?? customer.PaymentTermId, cancellationToken);
            var paymentTermsDays = paymentTerm?.DueDays ?? TryParsePaymentTermsDays(customer.PaymentTerms) ?? 30;
            var earlyPaymentDiscountPercentage = paymentTerm?.DiscountPercent ?? 0m;
            DateTime? earlyPaymentDiscountDueDate = null;
            if (paymentTerm != null && paymentTerm.DiscountPercent > 0 && paymentTerm.DiscountDays > 0)
            {
                earlyPaymentDiscountDueDate = dto.InvoiceDate.AddDays(paymentTerm.DiscountDays);
            }

            // The manual invoice UI uses stable line IDs to correlate dimension assignments. If
            // the browser times out after the transaction commits, its retry carries those same
            // IDs. Recover only an exact replay; otherwise the later INSERT fails with an opaque
            // PK_InvoiceLineItem violation (or, worse, a changed request could be accepted as the
            // original invoice).
            var replay = await TryResolveCreateReplayAsync(
                dto,
                dto.DueDate ?? dto.InvoiceDate.AddDays(paymentTermsDays),
                producer,
                cancellationToken);
            if (replay is not null)
                return replay;

            // Check for duplicate invoice
            if (!string.IsNullOrWhiteSpace(dto.Reference))
            {
                var isDuplicate = await IsDuplicateAsync(dto.CustomerId, dto.Reference, dto.InvoiceDate, cancellationToken);
                if (isDuplicate)
                    throw new InvalidOperationException($"Duplicate invoice detected with reference '{dto.Reference}'.");
            }

            // Generate invoice number
            var invoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var openingExchangeRate = await ResolveOpeningInvoiceExchangeRateAsync(
                dto.CurrencyCode,
                dto.InvoiceDate,
                dto.ExchangeRateId,
                dto.ExchangeRate,
                cancellationToken);
            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                InvoiceNumber = invoiceNumber,
                BusinessPartnerId = dto.CustomerId,
                CustomerName = customer.PartnerName,
                CustomerAddress = customer.PhysicalAddress ?? customer.MailingAddress,
                InvoiceDate = dto.InvoiceDate,
                DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(paymentTermsDays),
                Reference = dto.Reference,
                Notes = dto.Notes,
                IsOpeningBalance = dto.IsOpeningBalance,
                CurrencyCode = openingExchangeRate?.TransactionCurrency ?? dto.CurrencyCode,
                ExchangeRate = openingExchangeRate?.Rate ?? dto.ExchangeRate,
                ExchangeRateId = openingExchangeRate?.ExchangeRateId,
                PaymentTermsDays = paymentTermsDays,
                PaymentTermId = paymentTerm?.Id ?? customer.PaymentTermId,
                EarlyPaymentDiscountPercentage = earlyPaymentDiscountPercentage,
                EarlyPaymentDiscountDueDate = earlyPaymentDiscountDueDate,
                TaxGroupId = dto.IsOpeningBalance ? null : dto.TaxGroupId,
                Status = InvoiceStatus.Draft,
                CreatedAt = now,
                CreatedBy = UserName
            };

            // Process line items and calculate taxes
            decimal subtotal = 0;
            decimal totalTax = 0;
            var permitsDisposalAdjustment = await IsApprovedFixedAssetDisposalInvoiceAsync(
                dto.CustomerId,
                dto.Reference,
                cancellationToken);

            foreach (var lineDto in dto.LineItems)
            {
                var lineItemType = Enum.TryParse<LineItemType>(lineDto.LineItemType, out var parsedType)
                    ? parsedType
                    : LineItemType.Product;
                var lineTotal = RoundMoney(lineDto.Quantity * lineDto.UnitPrice);
                var lineDiscount = InvoiceTradeDiscountPolicy.CalculateLineDiscount(
                    lineTotal,
                    lineDto.DiscountPercentage,
                    "AR invoice line");
                var lineNetAmount = lineTotal - lineDiscount;
                var effectiveTaxGroupId = dto.IsOpeningBalance ? null : (lineDto.TaxGroupId ?? dto.TaxGroupId);

                ValidateControlledNegativeInvoiceLine(
                    lineItemType,
                    lineDto.TaxTreatment,
                    lineDto.DiscountPercentage,
                    lineNetAmount,
                    permitsDisposalAdjustment);

                decimal lineTax = 0;

                var lineItem = new InvoiceLineItem
                {
                    Id = lineDto.Id is { } requestedLineId && requestedLineId != Guid.Empty
                        ? requestedLineId
                        : Guid.NewGuid(),
                    TenantId = TenantId,
                    InvoiceId = invoice.Id,
                    LineItemType = lineItemType,
                    ProductId = lineDto.ProductId,
                    GLAccountId = lineDto.GLAccountId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxCode = lineDto.TaxCode,
                    TaxGroupId = effectiveTaxGroupId,
                    TaxTreatment = lineDto.TaxTreatment,
                    TaxRate = lineTax > 0 ? (lineTax / lineNetAmount * 100) : 0,
                    TaxAmount = lineTax,
                    Unit = lineDto.Unit,
                    DiscountPercentage = lineDto.DiscountPercentage,
                    DiscountAmount = lineDiscount,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                invoice.LineItems.Add(lineItem);
                subtotal += lineNetAmount;
            }

            totalTax = await RecalculateArTradeDiscountTaxesAsync(
                invoice,
                dto.DiscountAmount,
                cancellationToken);

            // Fetch Tenant for Base Currency
            var tenant = await _unitOfWork.Repository<Tenant>()
                .GetQueryable(t => t.Id == TenantId)
                .FirstOrDefaultAsync(cancellationToken);

            if (tenant == null)
                throw new InvalidOperationException("Tenant context not found.");

            // Calculate Base Currency Amount
            decimal baseCurrencyAmount;
            decimal exchangeRate = invoice.ExchangeRate;

            if (string.Equals(invoice.CurrencyCode, tenant.BaseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                exchangeRate = 1.0m;
                baseCurrencyAmount = subtotal + totalTax - dto.DiscountAmount; // Same as TotalAmount
            }
            else
            {
                // Ensure exchange rate is valid for foreign currency
                if (exchangeRate <= 0) exchangeRate = 1.0m; 
                baseCurrencyAmount = (subtotal + totalTax - dto.DiscountAmount) * exchangeRate;
            }

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.DiscountAmount = dto.DiscountAmount;
            invoice.TotalAmount = subtotal + totalTax - dto.DiscountAmount;
            invoice.EarlyPaymentDiscountAmount = CalculateEarlyPaymentDiscountAmount(
                invoice.TotalAmount,
                invoice.EarlyPaymentDiscountPercentage);
            invoice.PaidAmount = 0;
            
            // Set Multicurrency fields
            invoice.ExchangeRate = exchangeRate;
            invoice.BaseCurrencyAmount = baseCurrencyAmount;

            // Check credit limit before saving (using Base Currency)
            // Note: Customer.OutstandingBalance is now assumed to be in Base Currency
            var newOutstanding = customer.OutstandingBalance + invoice.BaseCurrencyAmount;
            if (newOutstanding > customer.CreditLimit)
            {
                _logger.LogWarning("Credit limit exceeded for customer {CustomerId}. Limit: {Limit} {BaseCurrency}, New Outstanding: {Outstanding} {BaseCurrency}",
                    customer.Id, customer.CreditLimit, tenant.BaseCurrency, newOutstanding, tenant.BaseCurrency);
                // Allow creation but might flag for approval in a real system
            }

            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AR invoice route.");
                await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await _unitOfWork.Repository<Invoice>().AddAsync(invoice);
                    await _unitOfWork.SaveChangesAsync(token);
                    await _sourceDimensions.SynchronizeDraftAsync(
                        producer,
                        invoice.Id,
                        invoice.InvoiceDate,
                        await BuildDimensionLineContextsAsync(invoice, token),
                        dto.FinanceDimensions,
                        inheritDefaultForUnassignedLines: true,
                        budgetReservationSourceDocumentType: null,
                        "Customer invoice created.",
                        token);
                }, cancellationToken);
            }
            else
            {
                await _unitOfWork.Repository<Invoice>().AddAsync(invoice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Created invoice {InvoiceNumber} for customer business partner {CustomerId}", invoiceNumber, customer.Id);

            return producer is null
                ? MapToDto(invoice)
                : await GetByIdAsync(invoice.Id, producer, cancellationToken) ?? MapToDto(invoice);
        }

        private async Task<InvoiceDto?> TryResolveCreateReplayAsync(
            InvoiceCreateDto dto,
            DateTime resolvedDueDate,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var requestedIds = dto.LineItems
                .Where(line => line.Id is { } id && id != Guid.Empty)
                .Select(line => line.Id!.Value)
                .ToArray();

            if (requestedIds.Length == 0)
                return null;

            if (requestedIds.Length != dto.LineItems.Count
                || requestedIds.Distinct().Count() != requestedIds.Length)
                throw new InvalidOperationException("Customer invoice line identities must be unique and non-empty.");

            var matchingLines = await _unitOfWork.Repository<InvoiceLineItem>()
                .GetQueryable(line => line.TenantId == TenantId && requestedIds.Contains(line.Id))
                .Include(line => line.Invoice)
                    .ThenInclude(invoice => invoice.LineItems)
                .ToListAsync(cancellationToken);

            if (matchingLines.Count == 0)
                return null;

            var invoiceIds = matchingLines.Select(line => line.InvoiceId).Distinct().ToArray();
            if (matchingLines.Count != requestedIds.Length || invoiceIds.Length != 1)
                throw new InvalidOperationException("Customer invoice line identities are already bound to another request.");

            var existing = matchingLines[0].Invoice;
            var activeLines = existing.LineItems.Where(line => !line.IsDeleted).ToDictionary(line => line.Id);
            var sameHeader = existing.TenantId == TenantId
                && existing.BusinessPartnerId == dto.CustomerId
                && existing.InvoiceDate == dto.InvoiceDate
                && existing.DueDate == resolvedDueDate
                && string.Equals(existing.Reference ?? string.Empty, dto.Reference ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(existing.Notes ?? string.Empty, dto.Notes ?? string.Empty, StringComparison.Ordinal)
                && existing.IsOpeningBalance == dto.IsOpeningBalance
                && string.Equals(existing.CurrencyCode, dto.CurrencyCode, StringComparison.OrdinalIgnoreCase)
                && existing.DiscountAmount == dto.DiscountAmount
                && existing.TaxGroupId == (dto.IsOpeningBalance ? null : dto.TaxGroupId)
                && activeLines.Count == dto.LineItems.Count;

            var sameLines = sameHeader && dto.LineItems.All(requested =>
            {
                var requestedId = requested.Id!.Value;
                if (!activeLines.TryGetValue(requestedId, out var persisted))
                    return false;

                var requestedType = Enum.TryParse<LineItemType>(requested.LineItemType, out var parsedType)
                    ? parsedType
                    : LineItemType.Product;
                var requestedTaxGroupId = dto.IsOpeningBalance ? null : (requested.TaxGroupId ?? dto.TaxGroupId);

                return persisted.LineItemType == requestedType
                    && persisted.ProductId == requested.ProductId
                    && persisted.GLAccountId == requested.GLAccountId
                    && string.Equals(persisted.Description, requested.Description, StringComparison.Ordinal)
                    && persisted.Quantity == requested.Quantity
                    && persisted.UnitPrice == requested.UnitPrice
                    && string.Equals(persisted.TaxCode ?? string.Empty, requested.TaxCode ?? string.Empty, StringComparison.Ordinal)
                    && persisted.TaxGroupId == requestedTaxGroupId
                    && persisted.TaxTreatment == requested.TaxTreatment
                    && string.Equals(persisted.Unit ?? string.Empty, requested.Unit ?? string.Empty, StringComparison.Ordinal)
                    && persisted.DiscountPercentage == requested.DiscountPercentage;
            });

            if (!sameLines)
                throw new InvalidOperationException("Customer invoice line identities are already bound to a different request.");

            _logger.LogInformation(
                "Recovered idempotent customer invoice create replay for {InvoiceId}",
                existing.Id);

            return producer is null
                ? MapToDto(existing)
                : await GetByIdAsync(existing.Id, producer, cancellationToken) ?? MapToDto(existing);
        }

        public Task<InvoiceDto> UpdateAsync(
            InvoiceUpdateDto dto,
            CancellationToken cancellationToken = default) =>
            UpdateCoreAsync(dto, null, cancellationToken);

        public Task<InvoiceDto> UpdateAsync(
            InvoiceUpdateDto dto,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            UpdateCoreAsync(dto, EnsureCustomerInvoiceRoute(producer), cancellationToken);

        private async Task<InvoiceDto> UpdateCoreAsync(
            InvoiceUpdateDto dto,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == dto.Id)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{dto.Id}' not found.");

            if (invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted customer invoices cannot be updated. Use a reversal, credit note, or adjustment.");

            if (invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Rejected)
                throw new InvalidOperationException("Only draft or rejected invoices can be updated.");
            
            // Fetch Tenant for Base Currency (needed for recalculation)
            var tenant = await _unitOfWork.Repository<Tenant>()
                .GetQueryable(t => t.Id == TenantId)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (tenant == null) throw new InvalidOperationException("Tenant context not found.");

            var openingExchangeRate = await ResolveOpeningInvoiceExchangeRateAsync(
                dto.CurrencyCode,
                dto.InvoiceDate,
                dto.ExchangeRateId,
                dto.ExchangeRate,
                cancellationToken);

            var now = DateTime.UtcNow;
            invoice.InvoiceDate = dto.InvoiceDate;
            invoice.DueDate = dto.DueDate;
            invoice.Reference = dto.Reference;
            invoice.Notes = dto.Notes;
            invoice.IsOpeningBalance = dto.IsOpeningBalance;
            invoice.CurrencyCode = openingExchangeRate?.TransactionCurrency ?? dto.CurrencyCode;
            invoice.ExchangeRate = openingExchangeRate?.Rate ?? dto.ExchangeRate;
            invoice.ExchangeRateId = openingExchangeRate?.ExchangeRateId;
            invoice.DiscountAmount = dto.DiscountAmount;
            invoice.TaxGroupId = dto.IsOpeningBalance ? null : dto.TaxGroupId;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // Stable line identities are part of the source-dimension evidence contract. Existing
            // lines are updated in place; only omitted lines are removed.
            var existingLines = invoice.LineItems.Where(line => !line.IsDeleted)
                .ToDictionary(line => line.Id);
            var requestedExistingIds = dto.LineItems.Where(line => line.Id.HasValue)
                .Select(line => line.Id!.Value)
                .ToArray();
            if (requestedExistingIds.Any(id => id == Guid.Empty)
                || requestedExistingIds.Distinct().Count() != requestedExistingIds.Length
                || requestedExistingIds.Any(id => !existingLines.ContainsKey(id)))
                throw new InvalidOperationException("Customer invoice line identities are invalid or belong to another document.");
            foreach (var existing in existingLines.Values.Where(line => !requestedExistingIds.Contains(line.Id)).ToList())
            {
                await _unitOfWork.Repository<InvoiceLineItem>().DeleteAsync(existing);
                invoice.LineItems.Remove(existing);
            }

            // Add updated line items
            decimal subtotal = 0;
            decimal totalTax = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineItemType = Enum.TryParse<LineItemType>(lineDto.LineItemType, out var parsedType)
                    ? parsedType
                    : LineItemType.Product;
                var lineTotal = RoundMoney(lineDto.Quantity * lineDto.UnitPrice);
                var lineDiscount = InvoiceTradeDiscountPolicy.CalculateLineDiscount(
                    lineTotal,
                    lineDto.DiscountPercentage,
                    "AR invoice line");
                var lineNetAmount = lineTotal - lineDiscount;
                var effectiveTaxGroupId = dto.IsOpeningBalance ? null : (lineDto.TaxGroupId ?? dto.TaxGroupId);

                // A posted disposal invoice is immutable, and general draft edits must never gain
                // the orchestrator-only negative-line privilege.
                ValidateControlledNegativeInvoiceLine(
                    lineItemType,
                    lineDto.TaxTreatment,
                    lineDto.DiscountPercentage,
                    lineNetAmount,
                    permitsDisposalAdjustment: false);

                decimal lineTax = 0;

                var lineItem = lineDto.Id.HasValue
                    ? existingLines[lineDto.Id.Value]
                    : new InvoiceLineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        InvoiceId = invoice.Id,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };
                lineItem.LineItemType = lineItemType;
                lineItem.ProductId = lineDto.ProductId;
                lineItem.GLAccountId = lineDto.GLAccountId;
                lineItem.Description = lineDto.Description;
                lineItem.Quantity = lineDto.Quantity;
                lineItem.UnitPrice = lineDto.UnitPrice;
                lineItem.TaxCode = lineDto.TaxCode;
                lineItem.TaxGroupId = effectiveTaxGroupId;
                lineItem.TaxTreatment = lineDto.TaxTreatment;
                lineItem.TaxRate = lineTax > 0 ? (lineTax / lineNetAmount * 100) : 0;
                lineItem.TaxAmount = lineTax;
                lineItem.Unit = lineDto.Unit;
                lineItem.DiscountPercentage = lineDto.DiscountPercentage;
                lineItem.DiscountAmount = lineDiscount;
                lineItem.UpdatedAt = now;
                lineItem.UpdatedBy = UserName;
                if (!lineDto.Id.HasValue)
                    invoice.LineItems.Add(lineItem);
                subtotal += lineNetAmount;
            }

            totalTax = await RecalculateArTradeDiscountTaxesAsync(
                invoice,
                dto.DiscountAmount,
                cancellationToken);

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.TotalAmount = subtotal + totalTax - dto.DiscountAmount;
            invoice.EarlyPaymentDiscountAmount = CalculateEarlyPaymentDiscountAmount(
                invoice.TotalAmount,
                invoice.EarlyPaymentDiscountPercentage);
            
            // Recalculate BaseCurrencyAmount
            if (string.Equals(invoice.CurrencyCode, tenant.BaseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                invoice.ExchangeRate = 1.0m;
                invoice.BaseCurrencyAmount = invoice.TotalAmount;
            }
            else
            {
                // Governed openings use the approved snapshot resolved above; ordinary draft
                // invoices retain the editable rate supplied by their existing update contract.
                invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;
            }

            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AR invoice route.");
                await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
                    await _unitOfWork.SaveChangesAsync(token);
                    await _sourceDimensions.SynchronizeDraftAsync(
                        producer,
                        invoice.Id,
                        invoice.InvoiceDate,
                        await BuildDimensionLineContextsAsync(invoice, token),
                        dto.FinanceDimensions,
                        inheritDefaultForUnassignedLines: true,
                        budgetReservationSourceDocumentType: null,
                        "Customer invoice draft changed.",
                        token);
                }, cancellationToken);
            }
            else
            {
                await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Updated invoice {InvoiceId}", invoice.Id);

            return producer is null
                ? MapToDto(invoice)
                : await GetByIdAsync(invoice.Id, producer, cancellationToken) ?? MapToDto(invoice);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == id);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{id}' not found.");

            if (invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted customer invoices cannot be deleted. Use a reversal, credit note, or adjustment.");

            // Only allow deletion if invoice is in Draft status
            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be deleted.");

            await _unitOfWork.Repository<Invoice>().DeleteAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted invoice {InvoiceId}", id);
        }

        public Task<InvoiceDto> SubmitAsync(Guid id, FinancePostingProducerContext producer, CancellationToken cancellationToken = default) =>
            ExecuteInvoiceLifecycleAsync(id, () => SubmitCoreAsync(id, EnsureCustomerInvoiceRoute(producer), cancellationToken), cancellationToken);

        private async Task<InvoiceDto> SubmitCoreAsync(Guid id, FinancePostingProducerContext? producer, CancellationToken cancellationToken)
        {
            var workflow = _workflowIntegration ?? throw new InvalidOperationException("Invoice workflow integration is not configured.");
            var invoice = await LoadInvoiceForPostingAsync(id, cancellationToken);
            if (invoice.Status is not (InvoiceStatus.Draft or InvoiceStatus.Rejected) || invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("Only unposted draft or rejected invoices can be submitted.");
            await ResolveCustomerForPostingAsync(invoice, cancellationToken);
            var lines = invoice.LineItems.Where(line => !line.IsDeleted).ToArray();
            if (lines.Length == 0 || lines.Any(line => line.InvoiceId != invoice.Id || line.TenantId != TenantId || line.Quantity <= 0))
                throw new InvalidOperationException("Invoice requires valid lines belonging to this invoice and tenant.");

            var approvalRequired = await workflow.HasActiveApprovalInstanceAsync("Invoice", id) ||
                await workflow.HasActiveApprovalWorkflowAsync("Invoice");
            if (approvalRequired)
            {
                invoice.Status = InvoiceStatus.PendingApproval;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            var result = await workflow.SubmitAsync("Invoice", id);
            if (!result.ExecutionResult.Success)
                throw new InvalidOperationException(result.ExecutionResult.Message ?? "Unable to submit invoice.");
            if (result.ApprovalRequired != approvalRequired ||
                (approvalRequired && (!result.ExecutionResult.WorkflowInstanceId.HasValue || result.ExecutionResult.WorkflowInstanceId == Guid.Empty)) ||
                (approvalRequired && result.Outcome is not (WorkflowOutcome.Pending or WorkflowOutcome.Approved)) ||
                (!approvalRequired && (result.ExecutionResult.WorkflowInstanceId.HasValue || result.Outcome != WorkflowOutcome.Approved ||
                    result.ExecutionResult.Status != WorkflowInstanceStatus.Completed)))
                throw new InvalidOperationException("Invoice approval configuration changed. Reload and retry.");
            invoice.ApprovalRequired = result.ApprovalRequired;
            invoice.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
            invoice.Status = !approvalRequired ? InvoiceStatus.ReadyToPost
                : result.Outcome == WorkflowOutcome.Approved ? InvoiceStatus.Approved : InvoiceStatus.PendingApproval;
            if (invoice.Status == InvoiceStatus.Approved)
                await RequireCompletedInvoiceWorkflowAsync(invoice, cancellationToken);
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.UpdatedBy = UserName;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return MapToDto(invoice);
        }

        // One lock/transaction contains stock issues, customer balance, source status and central GL posting.
        // Existing caller-owned approval/producer transactions remain owned by that caller.
        private async Task<InvoiceDto> ExecuteInvoiceLifecycleAsync(Guid id, Func<Task<InvoiceDto>> action, CancellationToken cancellationToken)
        {
            if (_unitOfWork.HasActiveTransaction)
            {
                await _unitOfWork.AcquireTransactionLockAsync($"AR:Invoice:{TenantId:N}:{id:N}", cancellationToken);
                return await action();
            }
            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync($"AR:Invoice:{TenantId:N}:{id:N}", cancellationToken);
                    var result = await action();
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    if (_unitOfWork.HasActiveTransaction)
                        await _unitOfWork.RollbackAsync(cancellationToken);
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            }, cancellationToken);
        }

        public Task<InvoiceDto> SendInvoiceAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ExecuteInvoiceLifecycleAsync(id, () => SendInvoiceCoreAsync(id, null, cancellationToken), cancellationToken);

        public Task<InvoiceDto> SendInvoiceAsync(
            Guid id,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            ExecuteInvoiceLifecycleAsync(id, () => SendInvoiceCoreAsync(id, EnsureCustomerInvoiceRoute(producer), cancellationToken), cancellationToken);

        private async Task<InvoiceDto> SendInvoiceCoreAsync(
            Guid id,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id && !i.IsDeleted)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{id}' not found.");

            if (invoice.Status == InvoiceStatus.Sent && invoice.JournalEntryId.HasValue)
                return MapToDto(invoice); // Retried release must never issue stock or increase debt twice.
            if (invoice.Status == InvoiceStatus.Draft)
            {
                // Trusted internal producers retain their release entry point, but cannot skip an active invoice process.
                var workflow = _workflowIntegration ?? throw new InvalidOperationException("Invoice workflow integration is not configured.");
                if (await workflow.HasActiveApprovalInstanceAsync("Invoice", id) || await workflow.HasActiveApprovalWorkflowAsync("Invoice"))
                    throw new InvalidOperationException("Submit the invoice to its active approval process before release.");
                await SubmitCoreAsync(id, producer, cancellationToken);
            }
            if (invoice.Status == InvoiceStatus.ReadyToPost)
            {
                if (invoice.ApprovalRequired || invoice.WorkflowInstanceId.HasValue)
                    throw new InvalidOperationException("Invoice does not have a retained direct posting decision.");
            }
            else if (invoice.Status is InvoiceStatus.PendingApproval or InvoiceStatus.Approved)
            {
                await RequireCompletedInvoiceWorkflowAsync(invoice, cancellationToken);
            }
            else throw new InvalidOperationException("Only ready-to-post or fully approved invoices can be released.");

            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AR invoice route.");
                await _sourceDimensions.ValidateAndFreezeAsync(
                    producer,
                    invoice.Id,
                    invoice.InvoiceDate,
                    await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                    requireCurrentBudgetEvidence: false,
                    cancellationToken);
            }

            var now = DateTime.UtcNow;
            var previousStatus = invoice.Status;
            invoice.Status = InvoiceStatus.Sent;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // Process Inventory Issues for Inventory-type line items
            foreach (var line in invoice.LineItems.Where(l => !invoice.IsOpeningBalance && l.LineItemType == LineItemType.Inventory))
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

            FinancePostingRequestV2Dto postingRequest;
            try
            {
                postingRequest = await BuildArInvoicePostingRequestAsync(
                    invoice,
                    allowDraftTransition: true,
                    producer,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await RecordArInvoiceAuditAsync(
                    FinanceAuditEvents.ArInvoicePostingFailed,
                    invoice,
                    afterValues: new
                    {
                        invoice.JournalEntryId,
                        error = ex.Message
                    },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);

                _logger.LogError(ex, "Failed to build AR invoice posting request while sending invoice {InvoiceNumber}", invoice.InvoiceNumber);
                throw;
            }

            // Update customer's outstanding balance as an operational read model. The posted GL remains the source of truth.
            var customerPartner = await GetCustomerPartnerAsync(invoice.CustomerId, cancellationToken);
            var previousOutstandingBalance = customerPartner?.OutstandingBalance;
            if (customerPartner != null)
            {
                // Use BaseCurrencyAmount for standardized balance
                customerPartner.OutstandingBalance = (customerPartner.OutstandingBalance ?? 0m) + invoice.BaseCurrencyAmount;
                customerPartner.UpdatedAt = now;
                customerPartner.UpdatedBy = UserName;
                await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customerPartner);
            }

            try
            {
                var result = await CompleteArInvoicePostingAsync(
                    invoice,
                    postingRequest,
                    wasAlreadyLinked: false,
                    rollbackBeforeFailureAudit: () =>
                    {
                        invoice.Status = previousStatus;
                        invoice.UpdatedAt = null;
                        invoice.UpdatedBy = null;

                        if (customerPartner != null)
                        {
                            customerPartner.OutstandingBalance = previousOutstandingBalance;
                            customerPartner.UpdatedAt = null;
                            customerPartner.UpdatedBy = null;
                        }

                        return Task.CompletedTask;
                    },
                    producer,
                    cancellationToken);

                _logger.LogInformation("Sent invoice {InvoiceNumber} and posted to GL through the finance posting engine", invoice.InvoiceNumber);

                return result;
            }
            catch
            {
                _logger.LogWarning("Invoice {InvoiceNumber} was not sent because AR posting failed.", invoice.InvoiceNumber);
                throw;
            }
        }

        private async Task RequireCompletedInvoiceWorkflowAsync(Invoice invoice, CancellationToken cancellationToken)
        {
            var workflow = _workflowIntegration ?? throw new InvalidOperationException("Invoice workflow integration is not configured.");
            if (!invoice.ApprovalRequired || await workflow.HasActiveApprovalInstanceAsync("Invoice", invoice.Id))
                throw new InvalidOperationException("The invoice approval process is not complete.");
            var candidates = await _unitOfWork.Repository<WorkflowInstance>()
                .GetQueryable(instance => instance.TenantId == TenantId && instance.EntityId == invoice.Id && !instance.IsDeleted)
                .Include(instance => instance.EntityType)
                .OrderByDescending(instance => instance.CreatedDate).ToListAsync(cancellationToken);
            static string Key(string? value) => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            var retained = candidates.FirstOrDefault(instance => instance.EntityType != null &&
                instance.EntityType.TenantId == TenantId && !instance.EntityType.IsDeleted &&
                (Key(instance.EntityType.Code) is "INVOICE" or "CUSTOMERINVOICE" || Key(instance.EntityType.Name) is "INVOICE" or "CUSTOMERINVOICE") &&
                (!invoice.WorkflowInstanceId.HasValue || instance.Id == invoice.WorkflowInstanceId));
            if (retained == null || retained.Status != WorkflowInstanceStatus.Completed || !retained.CompletedDate.HasValue)
                throw new InvalidOperationException("A completed approval workflow belonging to this invoice is required before release.");
            invoice.WorkflowInstanceId = retained.Id;
        }

        public Task<InvoiceDto> PostAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ExecuteInvoiceLifecycleAsync(id, () => PostCoreAsync(id, null, cancellationToken), cancellationToken);

        public Task<InvoiceDto> PostAsync(
            Guid id,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            ExecuteInvoiceLifecycleAsync(id, () => PostCoreAsync(id, EnsureCustomerInvoiceRoute(producer), cancellationToken), cancellationToken);

        private async Task<InvoiceDto> PostCoreAsync(
            Guid id,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AR invoice posting.");

            var invoice = await LoadInvoiceForPostingAsync(id, cancellationToken);
            if (invoice.Status is InvoiceStatus.ReadyToPost or InvoiceStatus.Approved)
                return await SendInvoiceCoreAsync(id, producer, cancellationToken);
            var wasAlreadyLinked = invoice.JournalEntryId.HasValue;

            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AR invoice route.");
                await _sourceDimensions.ValidateAndFreezeAsync(
                    producer,
                    invoice.Id,
                    invoice.InvoiceDate,
                    await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                    requireCurrentBudgetEvidence: false,
                    cancellationToken);
            }

            FinancePostingRequestV2Dto postingRequest;
            try
            {
                postingRequest = await BuildArInvoicePostingRequestAsync(
                    invoice,
                    allowDraftTransition: false,
                    producer,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await RecordArInvoiceAuditAsync(
                    FinanceAuditEvents.ArInvoicePostingFailed,
                    invoice,
                    afterValues: new
                    {
                        invoice.JournalEntryId,
                        error = ex.Message
                    },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);

                _logger.LogError(ex, "Failed to build AR invoice posting request for {InvoiceNumber}", invoice.InvoiceNumber);
                throw;
            }

            return await CompleteArInvoicePostingAsync(
                invoice,
                postingRequest,
                wasAlreadyLinked,
                rollbackBeforeFailureAudit: null,
                producer,
                cancellationToken);
        }

        public async Task<InvoiceDto> VoidInvoiceAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
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
            var customerPartner = await GetCustomerPartnerAsync(invoice.CustomerId, cancellationToken);
            if (customerPartner != null)
            {
                var unpaidPortion = invoice.BaseCurrencyAmount - invoice.PaidAmount;
                if (unpaidPortion > 0)
                {
                    customerPartner.OutstandingBalance = (customerPartner.OutstandingBalance ?? 0m) - unpaidPortion;
                    customerPartner.UpdatedAt = now;
                    customerPartner.UpdatedBy = UserName;
                    await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customerPartner);
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
                    i.BusinessPartnerId == customerId &&
                    i.Reference == reference &&
                    i.InvoiceDate >= dateTolerance &&
                    i.Status != InvoiceStatus.Cancelled)
                .AnyAsync(cancellationToken);

            return duplicate;
        }

        private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.ARInvoice,
                TenantId,
                DateTime.UtcNow,
                nameof(Invoice),
                cancellationToken: cancellationToken);
        }

        private async Task<Invoice> LoadInvoiceForPostingAsync(Guid id, CancellationToken cancellationToken)
        {
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id && !i.IsDeleted)
                .Include(i => i.BusinessPartner)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Invoice with Id '{id}' not found.");

            return invoice;
        }

        private Task<Invoice> LoadInvoiceWithLinesAsync(Guid id, CancellationToken cancellationToken) =>
            LoadInvoiceForPostingAsync(id, cancellationToken);

        private async Task<IReadOnlyList<FinanceSourceDocumentLineContext>> BuildDimensionLineContextsAsync(
            Invoice invoice,
            CancellationToken cancellationToken)
        {
            var lines = invoice.LineItems.Where(line => !line.IsDeleted)
                .OrderBy(line => line.CreatedAt)
                .ThenBy(line => line.Id)
                .ToList();
            if (lines.Count == 0)
                return Array.Empty<FinanceSourceDocumentLineContext>();

            if (invoice.IsOpeningBalance)
            {
                var settings = await GetFinanceSettingsAsync(cancellationToken);
                var clearingAccountId = settings.MigrationClearingAccountId
                    ?? throw new InvalidOperationException("Migration Clearing Account is not configured for AR opening balance dimensions.");
                return lines.Select(line => new FinanceSourceDocumentLineContext(line.Id, clearingAccountId)).ToArray();
            }

            return lines.Select(line => new FinanceSourceDocumentLineContext(
                line.Id,
                line.GLAccountId ?? throw new InvalidOperationException(
                    $"No revenue account specified for AR line '{line.Description}'.")))
                .ToArray();
        }

        private static FinancePostingProducerContext EnsureCustomerInvoiceRoute(
            FinancePostingProducerContext producer)
        {
            ArgumentNullException.ThrowIfNull(producer);
            if (producer.RouteId is not (FinanceDimensionRouteId.FinanceArCustomerInvoice
                or FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleInvoice))
                throw new InvalidOperationException("The trusted producer context is not a supported Finance AR customer-invoice route.");
            return producer;
        }

        private static void ApplySourceDimensions(
            FinancePostingLineDto postingLine,
            InvoiceLineItem sourceLine,
            IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>> sourceDimensions)
        {
            postingLine.SourceDocumentLineId = sourceLine.Id;
            postingLine.Dimensions = sourceDimensions.TryGetValue(sourceLine.Id, out var values)
                ? values
                : Array.Empty<FinancePostingDimensionValueDto>();
        }

        private static IReadOnlyDictionary<Guid, decimal> AllocateDocumentDiscount(
            decimal documentDiscount,
            IReadOnlyList<InvoiceLineItem> sourceLines)
        {
            return InvoiceTradeDiscountPolicy.AllocateDocumentDiscount(
                documentDiscount,
                sourceLines.Select(line => (
                    line.Id,
                    (line.Quantity * line.UnitPrice) - line.DiscountAmount)),
                "AR invoice");
        }

        private async Task<decimal> RecalculateArTradeDiscountTaxesAsync(
            Invoice invoice,
            decimal documentDiscount,
            CancellationToken cancellationToken)
        {
            var activeLines = invoice.LineItems.Where(line => !line.IsDeleted).ToList();
            var documentDiscounts = AllocateDocumentDiscount(documentDiscount, activeLines);
            var totalTax = 0m;

            foreach (var line in activeLines)
            {
                var taxableBase = RoundMoney(
                    (line.Quantity * line.UnitPrice)
                    - line.DiscountAmount
                    - documentDiscounts.GetValueOrDefault(line.Id));
                line.TaxAmount = 0m;
                line.TaxRate = 0m;

                if (taxableBase <= 0m
                    || line.TaxTreatment != TaxTreatment.Standard
                    || (!line.TaxGroupId.HasValue && string.IsNullOrWhiteSpace(line.TaxCode)))
                {
                    continue;
                }

                var taxResult = await _taxEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
                {
                    TransactionType = line.LineItemType == LineItemType.GLAccount
                        ? TaxTransactionType.SaleOfServices
                        : TaxTransactionType.SaleOfGoods,
                    BaseAmount = taxableBase,
                    TaxGroupId = line.TaxGroupId,
                    CustomerId = invoice.CustomerId,
                    TransactionDate = invoice.InvoiceDate
                }, cancellationToken);
                line.TaxAmount = taxResult.TotalTaxAmount;
                line.TaxRate = line.TaxAmount > 0m ? line.TaxAmount / taxableBase * 100m : 0m;
                totalTax += line.TaxAmount;
            }

            return RoundMoney(totalTax);
        }

        private async Task<FinancePostingRequestV2Dto> BuildArInvoicePostingRequestAsync(
            Invoice invoice,
            bool allowDraftTransition,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            if (invoice.TenantId != tenantId)
                throw new InvalidOperationException("AR invoice belongs to another tenant.");

            if (!allowDraftTransition && invoice.Status != InvoiceStatus.Sent)
                throw new InvalidOperationException("Only sent AR invoices can be posted.");

            if (allowDraftTransition && invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Sent)
                throw new InvalidOperationException("Only draft AR invoices can be sent and posted.");

            var activeLines = invoice.LineItems
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.CreatedAt)
                .ThenBy(l => l.Id)
                .ToList();
            var sourceLineDimensions = producer is not null && _sourceDimensions is not null
                ? await _sourceDimensions.GetPostingDimensionsAsync(producer, invoice.Id, cancellationToken)
                : new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>();

            if (activeLines.Count == 0)
                throw new InvalidOperationException("AR invoice has no lines to post.");

            foreach (var line in activeLines)
            {
                if (line.TenantId != tenantId || line.InvoiceId != invoice.Id)
                    throw new InvalidOperationException("AR invoice line belongs to another tenant or document.");
            }

            var customer = await ResolveCustomerForPostingAsync(invoice, cancellationToken);
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, functionalCurrency);
            var exchangeRate = NormalizeExchangeRate(invoice.ExchangeRate);
            var accountCache = new Dictionary<Guid, Account>();

            var arAccountId = customer.DefaultArAccountId
                ?? settings.ControlAccountArId
                ?? throw new InvalidOperationException("AR control account is not configured for this tenant.");
            await ResolvePostingAccountAsync(arAccountId, "AR control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            if (invoice.IsOpeningBalance)
            {
                return await BuildArOpeningBalancePostingRequestAsync(
                    invoice,
                    settings,
                    arAccountId,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    accountCache,
                    sourceLineDimensions,
                    cancellationToken);
            }

            var postingLines = new List<FinancePostingLineDto>();
            var documentDiscountAllocations = AllocateDocumentDiscount(invoice.DiscountAmount, activeLines);
            var lineNumber = 1;
            var permitsDisposalAdjustment = !activeLines.Any(line => line.Quantity * line.UnitPrice < 0m)
                || await IsApprovedFixedAssetDisposalInvoiceAsync(
                    invoice.CustomerId,
                    invoice.Reference,
                    cancellationToken);

            foreach (var line in activeLines)
            {
                if (line.DiscountAmount < 0m || line.TaxAmount < 0m)
                    throw new InvalidOperationException("AR invoice line discount and tax amounts cannot be negative.");

                var grossAmount = RoundMoney(line.Quantity * line.UnitPrice);
                var expectedTradeDiscount = InvoiceTradeDiscountPolicy.CalculateLineDiscount(
                    line.Quantity * line.UnitPrice,
                    line.DiscountPercentage,
                    "AR invoice line");
                if (RoundMoney(line.DiscountAmount) != expectedTradeDiscount)
                    throw new InvalidOperationException("AR invoice line trade discount evidence does not match its percentage and gross amount.");

                if (grossAmount == 0m)
                {
                    continue;
                }

                var revenueAccountId = line.GLAccountId
                    ?? throw new InvalidOperationException($"No revenue account specified for AR line '{line.Description}'.");
                await ResolvePostingAccountAsync(revenueAccountId, "revenue account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                if (grossAmount < 0m)
                {
                    // FIN-LIM-0040: a disposal may record an auctioneer/buyer deduction from the
                    // amount remitted. It is intentionally a debit to the same proceeds-clearing
                    // account and is allowed only for the dedicated non-taxable adjustment type;
                    // ordinary AR users cannot construct arbitrary negative invoice lines.
                    ValidateControlledNegativeInvoiceLine(
                        line.LineItemType,
                        line.TaxTreatment,
                        line.DiscountPercentage,
                        grossAmount,
                        permitsDisposalAdjustment);
                    var revenuePostingLine = BuildPostingLine(
                        revenueAccountId,
                        $"Asset-sale proceeds deduction - {invoice.InvoiceNumber} - {line.Description}",
                        debitTransactionAmount: Math.Abs(grossAmount),
                        creditTransactionAmount: 0m,
                        invoiceCurrency,
                        functionalCurrency,
                        exchangeRate,
                        invoice.InvoiceDate,
                        invoice.InvoiceNumber,
                        lineNumber++,
                        "AR-FixedAssetDisposalAdjustment");
                    ApplySourceDimensions(revenuePostingLine, line, sourceLineDimensions);
                    postingLines.Add(revenuePostingLine);
                }
                else
                {
                    var tradeDiscountAmount = RoundMoney(
                        line.DiscountAmount + documentDiscountAllocations.GetValueOrDefault(line.Id));
                    var netRevenueAmount = RoundMoney(grossAmount - tradeDiscountAmount);
                    if (netRevenueAmount > 0m)
                    {
                        var revenuePostingLine = BuildPostingLine(
                            revenueAccountId,
                            $"Revenue - {invoice.InvoiceNumber} - {line.Description}",
                            debitTransactionAmount: 0m,
                            creditTransactionAmount: netRevenueAmount,
                            invoiceCurrency,
                            functionalCurrency,
                            exchangeRate,
                            invoice.InvoiceDate,
                            invoice.InvoiceNumber,
                            lineNumber++,
                            ResolveLineTag(line));
                        ApplySourceDimensions(revenuePostingLine, line, sourceLineDimensions);
                        postingLines.Add(revenuePostingLine);
                    }
                }

                if (line.LineItemType == LineItemType.Inventory && line.CostTotal.HasValue && line.CostTotal.Value > 0m)
                {
                    var cogsAccountId = settings.ControlAccountCOGSId
                        ?? throw new InvalidOperationException("COGS account is not configured for AR inventory invoice posting.");
                    var inventoryAccountId = settings.ControlAccountInventoryId
                        ?? throw new InvalidOperationException("Inventory control account is not configured for AR inventory invoice posting.");

                    await ResolvePostingAccountAsync(cogsAccountId, "COGS account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);
                    await ResolvePostingAccountAsync(inventoryAccountId, "inventory control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

                    var costAmount = RoundMoney(line.CostTotal.Value);
                    var cogsPostingLine = BuildPostingLine(
                        cogsAccountId,
                        $"COGS - {invoice.InvoiceNumber} - {line.Description}",
                        debitTransactionAmount: costAmount,
                        creditTransactionAmount: 0m,
                        functionalCurrency,
                        functionalCurrency,
                        1m,
                        invoice.InvoiceDate,
                        invoice.InvoiceNumber,
                        lineNumber++,
                        "AR-COGS");
                    ApplySourceDimensions(cogsPostingLine, line, sourceLineDimensions);
                    postingLines.Add(cogsPostingLine);

                    postingLines.Add(BuildPostingLine(
                        inventoryAccountId,
                        $"Inventory issue - {invoice.InvoiceNumber} - {line.Description}",
                        debitTransactionAmount: 0m,
                        creditTransactionAmount: costAmount,
                        functionalCurrency,
                        functionalCurrency,
                        1m,
                        invoice.InvoiceDate,
                        invoice.InvoiceNumber,
                        lineNumber++,
                        "AR-Inventory"));
                }
            }

            var taxSnapshotLines = new List<FinanceTaxCalculationSnapshotDto>();
            if (invoice.TaxAmount > 0m)
            {
                var taxBuild = await BuildArInvoiceTaxPostingLinesAsync(
                    invoice,
                    settings,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    accountCache,
                    lineNumber,
                    documentDiscountAllocations,
                    cancellationToken);

                postingLines.AddRange(taxBuild.Lines);
                taxSnapshotLines.AddRange(taxBuild.Snapshots);
                lineNumber += taxBuild.Lines.Count;
            }

            var debitFunctionalTotal = RoundMoney(postingLines.Sum(l => l.DebitAmount));
            var creditFunctionalTotal = RoundMoney(postingLines.Sum(l => l.CreditAmount));
            var arFunctionalAmount = RoundMoney(creditFunctionalTotal - debitFunctionalTotal);
            var expectedFunctionalTotal = ToFunctionalAmount(invoice.TotalAmount, invoiceCurrency, functionalCurrency, exchangeRate);
            if (arFunctionalAmount <= 0m)
            {
                throw new InvalidOperationException($"Customer invoice {invoice.InvoiceNumber} has no positive AR amount to post.");
            }

            if (arFunctionalAmount != expectedFunctionalTotal)
            {
                throw new InvalidOperationException("AR invoice amount does not match posting line totals.");
            }

            postingLines.Insert(0, BuildPostingLine(
                arAccountId,
                $"AR invoice {invoice.InvoiceNumber}",
                debitTransactionAmount: invoice.TotalAmount,
                creditTransactionAmount: 0m,
                invoiceCurrency,
                functionalCurrency,
                exchangeRate,
                invoice.InvoiceDate,
                invoice.InvoiceNumber,
                1,
                "AR-Control"));

            for (var i = 0; i < postingLines.Count; i++)
            {
                postingLines[i].LineNumber = i + 1;
            }

            return new FinancePostingRequestV2Dto
            {
                SourceModule = "AR",
                SourceDocumentType = "CustomerInvoice",
                SourceDocumentId = invoice.Id,
                SourceDocumentTenantId = invoice.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                Description = $"Customer invoice {invoice.InvoiceNumber} - {invoice.CustomerName}",
                PostingDate = invoice.InvoiceDate,
                JournalType = "AR Invoice",
                AccountingBookCode = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"AR:CustomerInvoice:{invoice.TenantId:N}:{invoice.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines,
                TaxCalculationSnapshots = taxSnapshotLines
            };
        }

        private async Task<FinancePostingRequestV2Dto> BuildArOpeningBalancePostingRequestAsync(
            Invoice invoice,
            FinanceSettings settings,
            Guid arAccountId,
            string invoiceCurrency,
            string functionalCurrency,
            decimal exchangeRate,
            Dictionary<Guid, Account> accountCache,
            IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>> sourceLineDimensions,
            CancellationToken cancellationToken)
        {
            var rateSnapshot = await RevalidateOpeningInvoiceExchangeRateAsync(invoice, cancellationToken);
            exchangeRate = rateSnapshot.Rate;
            var migrationClearingAccountId = settings.MigrationClearingAccountId
                ?? throw new InvalidOperationException("Migration Clearing Account is not configured for AR opening balance posting.");
            await ResolvePostingAccountAsync(
                migrationClearingAccountId,
                "migration clearing account",
                accountCache,
                allowControlAccount: false,
                requireDirectPosting: true,
                cancellationToken);

            var openingAmount = RoundMoney(invoice.TotalAmount);
            if (openingAmount <= 0m)
            {
                throw new InvalidOperationException($"Opening-balance customer invoice {invoice.InvoiceNumber} has no positive AR amount to post.");
            }
            var functionalOpeningAmount = ToFunctionalAmount(
                openingAmount,
                invoiceCurrency,
                functionalCurrency,
                exchangeRate);

            var postingLines = new List<FinancePostingLineDto>
            {
                BuildPostingLine(
                    arAccountId,
                    $"AR opening balance {invoice.InvoiceNumber}",
                    debitTransactionAmount: openingAmount,
                    creditTransactionAmount: 0m,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    1,
                    "AR-Control",
                    rateSnapshot.ExchangeRateId,
                    rateSnapshot.Source)
            };

            var activeLines = invoice.LineItems.Where(line => !line.IsDeleted)
                .OrderBy(line => line.CreatedAt)
                .ThenBy(line => line.Id)
                .ToList();
            var documentDiscounts = AllocateDocumentDiscount(invoice.DiscountAmount, activeLines);
            var sourceAmounts = activeLines
                .Select(line => new
                {
                    Line = line,
                    Amount = RoundMoney(
                        (line.Quantity * line.UnitPrice)
                        - line.DiscountAmount
                        - documentDiscounts.GetValueOrDefault(line.Id)
                        + line.TaxAmount)
                })
                .Where(item => item.Amount != 0m)
                .ToList();
            if (sourceAmounts.Any(item => item.Amount < 0m)
                || RoundMoney(sourceAmounts.Sum(item => item.Amount)) != openingAmount)
                throw new InvalidOperationException("AR opening-balance source lines do not reconcile to the invoice total.");

            var allocatedFunctional = 0m;
            for (var index = 0; index < sourceAmounts.Count; index++)
            {
                var source = sourceAmounts[index];
                var functionalAmount = index == sourceAmounts.Count - 1
                    ? functionalOpeningAmount - allocatedFunctional
                    : ToFunctionalAmount(source.Amount, invoiceCurrency, functionalCurrency, exchangeRate);
                var clearingLine = BuildPostingLine(
                    migrationClearingAccountId,
                    $"Migration clearing - AR opening balance {invoice.InvoiceNumber} - {source.Line.Description}",
                    debitTransactionAmount: 0m,
                    creditTransactionAmount: functionalAmount,
                    functionalCurrency,
                    functionalCurrency,
                    1m,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    index + 2,
                    "AR-MigrationClearing");
                ApplySourceDimensions(clearingLine, source.Line, sourceLineDimensions);
                postingLines.Add(clearingLine);
                allocatedFunctional += functionalAmount;
            }

            return new FinancePostingRequestV2Dto
            {
                SourceModule = "AR",
                SourceDocumentType = "CustomerInvoice",
                SourceDocumentId = invoice.Id,
                SourceDocumentTenantId = invoice.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                Description = $"AR opening balance {invoice.InvoiceNumber} - {invoice.CustomerName}",
                PostingDate = invoice.InvoiceDate,
                JournalType = "AR Opening Balance",
                AccountingBookCode = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"AR:CustomerInvoice:{invoice.TenantId:N}:{invoice.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines,
                TaxCalculationSnapshots = Array.Empty<FinanceTaxCalculationSnapshotDto>()
            };
        }

        private async Task<InvoiceDto> CompleteArInvoicePostingAsync(
            Invoice invoice,
            FinancePostingRequestV2Dto postingRequest,
            bool wasAlreadyLinked,
            Func<Task>? rollbackBeforeFailureAudit,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AR invoice posting.");

            FinancePostingResultDto postingResult;
            try
            {
                postingResult = producer is null
                    ? await _financePostingEngine.PostAsync(postingRequest, cancellationToken)
                    : await _financePostingEngine.PostAsync(postingRequest, producer, cancellationToken);
            }
            catch (Exception ex)
            {
                if (rollbackBeforeFailureAudit != null)
                {
                    await rollbackBeforeFailureAudit();
                }

                if (invoice.TaxAmount > 0m)
                {
                    await RecordArInvoiceAuditAsync(
                        FinanceAuditEvents.TaxPostingFailed,
                        invoice,
                        afterValues: new
                        {
                            invoice.JournalEntryId,
                            invoice.TaxAmount,
                            error = ex.Message
                        },
                        reason: ex.Message,
                        cancellationToken: cancellationToken);
                }

                await RecordArInvoiceAuditAsync(
                    FinanceAuditEvents.ArInvoicePostingFailed,
                    invoice,
                    afterValues: new
                    {
                        invoice.JournalEntryId,
                        error = ex.Message
                    },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);

                _logger.LogError(ex, "Failed to post AR invoice {InvoiceNumber}", invoice.InvoiceNumber);
                throw;
            }

            if (invoice.JournalEntryId.HasValue && invoice.JournalEntryId.Value != postingResult.JournalEntryId)
            {
                throw new InvalidOperationException("Customer invoice is linked to a different journal entry than the posting engine result.");
            }

            if (!invoice.JournalEntryId.HasValue)
            {
                invoice.JournalEntryId = postingResult.JournalEntryId;
                invoice.UpdatedAt = DateTime.UtcNow;
                invoice.UpdatedBy = UserName;
                await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (!postingResult.WasDuplicate)
            {
                await RecordTaxCalculationSnapshotsAsync(postingRequest.TaxCalculationSnapshots, cancellationToken);
                if (postingRequest.TaxCalculationSnapshots.Count > 0)
                {
                    await RecordArInvoiceAuditAsync(
                        FinanceAuditEvents.TaxCalculatedOnArInvoice,
                        invoice,
                        postingEventId: postingResult.PostingEventId,
                        journalEntryId: postingResult.JournalEntryId,
                        afterValues: new
                        {
                            snapshotCount = postingRequest.TaxCalculationSnapshots.Count,
                            totalTax = postingRequest.TaxCalculationSnapshots.Sum(s => s.TaxAmount)
                        },
                        comment: "AR invoice tax calculated from effective-dated tenant tax configuration.",
                        cancellationToken: cancellationToken);

                    await RecordArInvoiceAuditAsync(
                        FinanceAuditEvents.TaxPosted,
                        invoice,
                        postingEventId: postingResult.PostingEventId,
                        journalEntryId: postingResult.JournalEntryId,
                        afterValues: new
                        {
                            postingResult.PostingEventId,
                            postingResult.JournalEntryId,
                            taxLineCount = postingRequest.Lines.Count(l => l.TransactionTag != null && l.TransactionTag.StartsWith("AR-Tax-", StringComparison.OrdinalIgnoreCase))
                        },
                        comment: "AR invoice tax posted through the central finance posting engine.",
                        cancellationToken: cancellationToken);
                }

                await RecordArInvoiceAuditAsync(
                    FinanceAuditEvents.TaxConfigurationUsedInPosting,
                    invoice,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        snapshotCount = postingRequest.TaxCalculationSnapshots.Count,
                        taxIds = postingRequest.TaxCalculationSnapshots.Select(s => s.TaxId).Distinct().ToArray()
                    },
                    comment: "Effective-dated tax configuration used for AR invoice posting where available.",
                    cancellationToken: cancellationToken);
            }

            if (postingResult.WasDuplicate || wasAlreadyLinked)
            {
                await RecordArInvoiceAuditAsync(
                    FinanceAuditEvents.ArInvoiceDuplicatePostingAttempt,
                    invoice,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        postingResult.PostingEventId,
                        postingResult.JournalEntryId,
                        postingResult.PostingAction,
                        postingResult.WasDuplicate
                    },
                    comment: "Duplicate AR invoice posting request returned the existing posting.",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await RecordArInvoiceAuditAsync(
                    FinanceAuditEvents.ArInvoicePosted,
                    invoice,
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
                    comment: "AR invoice posted through the central finance posting engine.",
                    cancellationToken: cancellationToken);
            }

            _logger.LogInformation(
                "Posted AR invoice {InvoiceNumber} through finance posting engine with journal {JournalEntryId}. Duplicate={WasDuplicate}",
                invoice.InvoiceNumber,
                postingResult.JournalEntryId,
                postingResult.WasDuplicate);

            return await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice);
        }

        private async Task<BusinessPartner> ResolveCustomerForPostingAsync(Invoice invoice, CancellationToken cancellationToken)
        {
            var customer = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.Id == invoice.CustomerId &&
                    !p.IsDeleted &&
                    (p.PartnerType == "Customer" || p.PartnerType == "Both"))
                .FirstOrDefaultAsync(cancellationToken);

            if (customer == null)
                throw new InvalidOperationException("AR invoice customer was not found for this tenant.");

            if (!customer.IsActive || customer.IsBlacklisted)
                throw new InvalidOperationException($"Customer '{customer.PartnerName}' is not active for AR posting.");

            return customer;
        }

        private async Task<FinanceSettings> GetFinanceSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted);

            return settings ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        }

        private async Task<Account> ResolvePostingAccountAsync(
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
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == accountId && !a.IsDeleted);

            if (account == null)
                throw new InvalidOperationException($"AR posting {role} was not found for this tenant.");

            if (account.Status != AccountStatus.Active)
                throw new InvalidOperationException($"AR posting {role} account '{account.AccountNumber}' is not active.");

            if (account.IsControlAccount && !allowControlAccount)
                throw new InvalidOperationException($"AR posting {role} account '{account.AccountNumber}' is a control account and cannot be used for this line.");

            if (requireDirectPosting && !account.AllowDirectPosting)
                throw new InvalidOperationException($"AR posting {role} account '{account.AccountNumber}' does not allow direct posting.");

            accountCache[accountId] = account;
            return account;
        }

        private async Task<TaxPostingBuildResult> BuildArInvoiceTaxPostingLinesAsync(
            Invoice invoice,
            FinanceSettings settings,
            string invoiceCurrency,
            string functionalCurrency,
            decimal exchangeRate,
            Dictionary<Guid, Account> accountCache,
            int startingLineNumber,
            IReadOnlyDictionary<Guid, decimal> documentDiscountAllocations,
            CancellationToken cancellationToken)
        {
            var calculatedLines = new List<FinancePostingLineDto>();
            var snapshots = new List<FinanceTaxCalculationSnapshotDto>();

            foreach (var line in invoice.LineItems.Where(l => !l.IsDeleted).OrderBy(l => l.CreatedAt).ThenBy(l => l.Id))
            {
                var lineBase = RoundMoney(
                    (line.Quantity * line.UnitPrice)
                    - line.DiscountAmount
                    - documentDiscountAllocations.GetValueOrDefault(line.Id));
                if (lineBase <= 0m)
                {
                    continue;
                }

                if (IsNoTaxTreatment(line.TaxTreatment))
                {
                    if (RoundMoney(line.TaxAmount) != 0m || RoundMoney(line.TaxRate) != 0m)
                    {
                        throw new InvalidOperationException($"AR invoice line '{line.Description}' is {line.TaxTreatment} but carries a tax amount or rate.");
                    }

                    continue;
                }

                TaxCalculationResultDto taxResult;
                taxResult = await _taxEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
                {
                    BaseAmount = lineBase,
                    TaxGroupId = line.TaxGroupId,
                    TransactionDate = invoice.InvoiceDate,
                    TransactionType = line.LineItemType == LineItemType.GLAccount
                        ? TaxTransactionType.SaleOfServices
                        : TaxTransactionType.SaleOfGoods,
                    CustomerId = invoice.CustomerId
                }, cancellationToken);

                foreach (var breakdown in taxResult.TaxBreakdowns.Where(t => t.TaxAmount > 0m))
                {
                    var accountId = breakdown.TaxPayableAccountId;
                    if (!accountId.HasValue)
                    {
                        throw new InvalidOperationException($"AR invoice tax account is not configured for tax '{breakdown.TaxCode}'.");
                    }

                    await ResolvePostingAccountAsync(
                        accountId.Value,
                        $"output tax account for {breakdown.TaxCode}",
                        accountCache,
                        allowControlAccount: true,
                        requireDirectPosting: false,
                        cancellationToken);

                    var postingLine = BuildPostingLine(
                        accountId.Value,
                        $"{breakdown.TaxName} - {invoice.InvoiceNumber}",
                        debitTransactionAmount: 0m,
                        creditTransactionAmount: breakdown.TaxAmount,
                        invoiceCurrency,
                        functionalCurrency,
                        exchangeRate,
                        invoice.InvoiceDate,
                        invoice.InvoiceNumber,
                        startingLineNumber + calculatedLines.Count,
                        $"AR-Tax-{breakdown.TaxCode}");
                    postingLine.Notes = $"TaxId={breakdown.TaxId};TaxGroupId={taxResult.TaxGroupId};TaxRate={breakdown.TaxRate};TaxableAmount={breakdown.TaxableAmount}";
                    calculatedLines.Add(postingLine);

                    snapshots.Add(ToTaxSnapshot(
                        "CustomerInvoice",
                        invoice.Id,
                        taxResult.TaxGroupId,
                        lineBase,
                        breakdown,
                        invoice.InvoiceDate));
                }
            }

            if (calculatedLines.Count > 0 && RoundMoney(calculatedLines.Sum(l => l.CreditAmount)) == ToFunctionalAmount(invoice.TaxAmount, invoiceCurrency, functionalCurrency, exchangeRate))
            {
                return new TaxPostingBuildResult(calculatedLines, snapshots);
            }

            throw new InvalidOperationException("AR invoice configured tax calculation does not reconcile to the invoice tax total.");
        }

        private static FinanceTaxCalculationSnapshotDto ToTaxSnapshot(
            string documentType,
            Guid documentId,
            Guid? taxGroupId,
            decimal baseAmount,
            TaxBreakdownDto breakdown,
            DateTime calculationDate)
        {
            return new FinanceTaxCalculationSnapshotDto
            {
                DocumentType = documentType,
                DocumentId = documentId,
                TaxId = breakdown.TaxId,
                TaxGroupId = taxGroupId,
                BaseAmount = baseAmount,
                TaxableAmount = breakdown.TaxableAmount,
                TaxRate = breakdown.TaxRate,
                TaxAmount = breakdown.TaxAmount,
                CompoundBasis = breakdown.CompoundBasis,
                CalculationOrder = breakdown.CalculationOrder,
                CalculationDate = calculationDate,
                IsManualOverride = breakdown.IsManualOverride
            };
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
            string? exchangeRateSource = null)
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
                TransactionDebitAmount = debitTransactionAmount,
                TransactionCreditAmount = creditTransactionAmount,
                ForeignCurrencyAmount = isForeign
                    ? debitTransactionAmount > 0m ? debitTransactionAmount : creditTransactionAmount
                    : null,
                ExchangeRate = isForeign ? exchangeRate : null,
                ExchangeRateId = isForeign ? exchangeRateId : null,
                ExchangeRateSource = isForeign
                    ? exchangeRateSource ?? "AR invoice exchange-rate snapshot"
                    : null,
                ExchangeRateDate = isForeign ? exchangeRateDate.Date : null,
                SourceReferenceNumber = reference,
                LineNumber = lineNumber,
                TransactionTag = transactionTag
            };
        }

        private async Task RecordArInvoiceAuditAsync(
            string eventType,
            Invoice invoice,
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
                TenantId = invoice.TenantId,
                SourceModule = "AR",
                SourceDocumentType = "CustomerInvoice",
                SourceDocumentId = invoice.Id,
                JournalEntryId = journalEntryId ?? invoice.JournalEntryId,
                PostingEventId = postingEventId,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Comment = comment,
                Resource = "Finance.ARInvoice",
                ResourceId = invoice.Id.ToString()
            }, cancellationToken);
        }

        private async Task RecordTaxCalculationSnapshotsAsync(
            IReadOnlyList<FinanceTaxCalculationSnapshotDto> snapshots,
            CancellationToken cancellationToken)
        {
            if (snapshots.Count == 0)
            {
                return;
            }

            var repository = _unitOfWork.Repository<TaxCalculation>();
            var newRows = new List<TaxCalculation>();

            foreach (var snapshot in snapshots)
            {
                var exists = await repository.ExistsAsync(c =>
                    c.TenantId == TenantId &&
                    c.DocumentType == snapshot.DocumentType &&
                    c.DocumentId == snapshot.DocumentId &&
                    c.TaxId == snapshot.TaxId &&
                    c.TaxGroupId == snapshot.TaxGroupId &&
                    !c.IsDeleted);

                if (exists)
                {
                    continue;
                }

                newRows.Add(new TaxCalculation
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    DocumentType = snapshot.DocumentType,
                    DocumentId = snapshot.DocumentId,
                    TaxId = snapshot.TaxId,
                    TaxGroupId = snapshot.TaxGroupId,
                    BaseAmount = snapshot.BaseAmount,
                    TaxableAmount = snapshot.TaxableAmount,
                    TaxRate = snapshot.TaxRate,
                    TaxAmount = snapshot.TaxAmount,
                    CompoundBasis = snapshot.CompoundBasis,
                    CalculationOrder = snapshot.CalculationOrder,
                    CalculationDate = snapshot.CalculationDate,
                    IsManualOverride = snapshot.IsManualOverride,
                    OverrideReason = snapshot.OverrideReason,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                });
            }

            if (newRows.Count > 0)
            {
                await repository.AddRangeAsync(newRows);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        private static string ResolveLineTag(InvoiceLineItem line)
            => line.LineItemType == LineItemType.Inventory
                ? "AR-InventoryRevenue"
                : line.LineItemType == LineItemType.FixedAssetDisposal
                    ? "AR-FixedAssetDisposalProceeds"
                    : line.LineItemType == LineItemType.FixedAssetDisposalAdjustment
                        ? "AR-FixedAssetDisposalAdjustment"
                        : "AR-Revenue";

        private static void ValidateControlledNegativeInvoiceLine(
            LineItemType lineItemType,
            TaxTreatment taxTreatment,
            decimal discountPercentage,
            decimal lineNetAmount,
            bool permitsDisposalAdjustment)
        {
            if (lineNetAmount >= 0m)
            {
                return;
            }

            // Negative lines have material credit-note implications. The only supported create-
            // time exception is the disposal proceeds deduction produced by the Finance-owned
            // orchestrator; all other reductions must use the canonical credit-note workflow.
            if (!permitsDisposalAdjustment ||
                lineItemType != LineItemType.FixedAssetDisposalAdjustment ||
                taxTreatment != TaxTreatment.OutOfScope ||
                discountPercentage != 0m)
            {
                throw new InvalidOperationException(
                    "Negative AR invoice lines are restricted to non-taxable fixed-asset disposal adjustments. Use the credit-note workflow for other reductions.");
            }
        }

        private async Task<bool> IsApprovedFixedAssetDisposalInvoiceAsync(
            Guid customerId,
            string? reference,
            CancellationToken cancellationToken)
        {
            const string prefix = "FA-DISPOSAL:";
            if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var disposalReference = reference[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(disposalReference))
            {
                return false;
            }

            // The public AR API can receive a LineItemType string, so the enum alone is not an
            // authorization boundary. A negative adjustment is accepted only while the Finance
            // disposal orchestrator has a completed, same-tenant sale for this exact buyer and
            // immutable disposal reference. This prevents ordinary invoice callers from posing as
            // the trusted internal workflow merely by supplying the dedicated line type.
            return await _unitOfWork.Repository<AssetDisposal>()
                .GetQueryable(disposal =>
                    disposal.TenantId == TenantId &&
                    disposal.BuyerBusinessPartnerId == customerId &&
                    disposal.ReferenceNumber == disposalReference &&
                    disposal.DisposalType == DisposalType.Sale &&
                    disposal.Status == AssetDisposalStatus.Completed &&
                    disposal.CustomerInvoiceId == null &&
                    disposal.DisposalCost > 0m &&
                    !disposal.IsDeleted)
                .AnyAsync(cancellationToken);
        }

        private static bool IsNoTaxTreatment(TaxTreatment treatment)
            => treatment == TaxTreatment.Exempt
                || treatment == TaxTreatment.ZeroRated
                || treatment == TaxTreatment.OutOfScope;

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

        private async Task<OpeningInvoiceExchangeRateSnapshot> ResolveOpeningInvoiceExchangeRateAsync(
            string? currencyCode,
            DateTime invoiceDate,
            Guid? exchangeRateId,
            decimal suppliedRate,
            CancellationToken cancellationToken)
        {
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var transactionCurrency = NormalizeCurrency(currencyCode, functionalCurrency);

            if (string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                if (exchangeRateId.HasValue)
                    throw new InvalidOperationException("Functional-currency AR invoices cannot carry foreign exchange-rate evidence.");
                if (RoundRate(suppliedRate) != 1m)
                    throw new InvalidOperationException("Functional-currency AR invoices must use an exchange rate of 1.");
                return new OpeningInvoiceExchangeRateSnapshot(null, 1m, transactionCurrency, functionalCurrency, "Functional currency");
            }

            if (!exchangeRateId.HasValue)
                throw new InvalidOperationException("Foreign-currency AR invoices require an approved exchange-rate record.");

            var quoteSide = settings.DirectionalExchangeRatePolicyEnabled
                ? settings.ArInvoiceQuoteSide
                : ExchangeRateQuoteSide.Mid;
            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .FirstOrDefaultAsync(item =>
                    item.TenantId == TenantId &&
                    item.Id == exchangeRateId.Value &&
                    !item.IsDeleted);

            if (rate == null ||
                !rate.IsActive ||
                rate.ApprovalStatus is not (RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved) ||
                rate.Rate <= 0m ||
                rate.RateType != ExchangeRateType.Daily ||
                rate.QuoteSide != quoteSide ||
                !string.Equals(rate.BaseCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rate.TargetCurrencyCode, transactionCurrency, StringComparison.OrdinalIgnoreCase) ||
                rate.EffectiveDate.Date > invoiceDate.Date ||
                (rate.EndDate.HasValue && rate.EndDate.Value.Date < invoiceDate.Date))
            {
                throw new InvalidOperationException(
                    "The selected AR invoice exchange rate is not active, approved, effective, or compliant with the tenant invoice-rate policy.");
            }

            if (RoundRate(suppliedRate) != RoundRate(rate.InverseRate))
                throw new InvalidOperationException("The AR invoice exchange-rate value does not match the approved rate record.");

            return new OpeningInvoiceExchangeRateSnapshot(
                rate.Id,
                rate.InverseRate,
                transactionCurrency,
                functionalCurrency,
                rate.RateSource);
        }

        private Task<OpeningInvoiceExchangeRateSnapshot> RevalidateOpeningInvoiceExchangeRateAsync(
            Invoice invoice,
            CancellationToken cancellationToken)
            => ResolveOpeningInvoiceExchangeRateAsync(
                invoice.CurrencyCode,
                invoice.InvoiceDate,
                invoice.ExchangeRateId,
                invoice.ExchangeRate,
                cancellationToken);

        private static decimal RoundRate(decimal amount)
            => decimal.Round(amount, 6, MidpointRounding.AwayFromZero);

        private static decimal RoundMoney(decimal amount)
            => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        private async Task<PaymentTerm?> ResolvePaymentTermAsync(Guid? paymentTermId, CancellationToken cancellationToken)
        {
            if (!paymentTermId.HasValue || paymentTermId.Value == Guid.Empty)
            {
                return await _unitOfWork.Repository<PaymentTerm>()
                    .GetQueryable(pt =>
                        pt.TenantId == TenantId &&
                        pt.IsActive &&
                        pt.IsDefault &&
                        !pt.IsDeleted &&
                        (pt.ApplicableTo == "All" || pt.ApplicableTo == "Customer" || pt.ApplicableTo == "Client"))
                    .OrderBy(pt => pt.ApplicableTo == "Customer" ? 0 : 1)
                    .ThenBy(pt => pt.DisplayOrder)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var paymentTerm = await _unitOfWork.Repository<PaymentTerm>()
                .FirstOrDefaultAsync(pt =>
                    pt.TenantId == TenantId &&
                    pt.Id == paymentTermId.Value &&
                    pt.IsActive &&
                    !pt.IsDeleted);

            if (paymentTerm == null)
            {
                throw new InvalidOperationException($"Active payment term with Id '{paymentTermId.Value}' was not found.");
            }

            if (!IsCustomerPaymentTerm(paymentTerm.ApplicableTo))
            {
                throw new InvalidOperationException($"Payment term '{paymentTerm.Code}' is not applicable to customer invoices.");
            }

            return paymentTerm;
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

        private static int? TryParsePaymentTermsDays(string? paymentTerms)
        {
            if (string.IsNullOrWhiteSpace(paymentTerms))
            {
                return null;
            }

            if (paymentTerms.Equals("COD", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var digits = new string(paymentTerms.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var days) ? days : null;
        }

        private static bool IsCustomerPaymentTerm(string applicableTo)
        {
            return string.Equals(applicableTo, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(applicableTo, "Customer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(applicableTo, "Client", StringComparison.OrdinalIgnoreCase);
        }

        private static decimal CalculateEarlyPaymentDiscountAmount(decimal totalAmount, decimal discountPercentage)
        {
            return discountPercentage > 0
                ? Math.Round(totalAmount * (discountPercentage / 100m), 2, MidpointRounding.AwayFromZero)
                : 0m;
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
                ApprovalRequired = invoice.ApprovalRequired,
                WorkflowInstanceId = invoice.WorkflowInstanceId,
                Notes = invoice.Notes,
                Reference = invoice.Reference,
                IsOpeningBalance = invoice.IsOpeningBalance,
                CurrencyCode = invoice.CurrencyCode,
                ExchangeRate = invoice.ExchangeRate,
                ExchangeRateId = invoice.ExchangeRateId,
                PaymentTermsDays = invoice.PaymentTermsDays,
                PaymentTermId = invoice.PaymentTermId,
                EarlyPaymentDiscountPercentage = invoice.EarlyPaymentDiscountPercentage,
                EarlyPaymentDiscountDueDate = invoice.EarlyPaymentDiscountDueDate,
                EarlyPaymentDiscountAmount = invoice.EarlyPaymentDiscountAmount,
                TaxGroupId = invoice.TaxGroupId,
                JournalEntryId = invoice.JournalEntryId,
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
                    TaxRate = li.TaxRate,
                    TaxAmount = li.TaxAmount,
                    TaxCode = li.TaxCode,
                    TaxGroupId = li.TaxGroupId,
                    TaxTreatment = li.TaxTreatment,
                    Unit = li.Unit,
                    DiscountPercentage = li.DiscountPercentage,
                    DiscountAmount = li.DiscountAmount
                }).ToList(),
                CreatedAt = invoice.CreatedAt
            };
        }

        private sealed record TaxPostingBuildResult(
            List<FinancePostingLineDto> Lines,
            List<FinanceTaxCalculationSnapshotDto> Snapshots);

        private sealed record OpeningInvoiceExchangeRateSnapshot(
            Guid? ExchangeRateId,
            decimal Rate,
            string TransactionCurrency,
            string FunctionalCurrency,
            string Source);
    }
}
