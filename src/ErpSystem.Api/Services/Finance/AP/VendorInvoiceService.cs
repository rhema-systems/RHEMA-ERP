using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
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
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Api.Services.Finance.AP
{
    /// <summary>
    /// Manages vendor invoice lifecycle: creation, matching, approval, and voiding.
    /// </summary>
    public class VendorInvoiceService : IVendorInvoiceService
    {
        private const string VendorInvoiceBudgetSource = "VendorInvoice";
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<VendorInvoiceService> _logger;
        private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService _inventoryValuationService;
        private readonly IDocumentNumberingService _documentNumberingService;
        private readonly IWorkflowService _workflowService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly ITaxCalculationEngine? _taxEngine;
        private readonly IFixedAssetService? _fixedAssetService;
        private readonly IProcurementConfigurationService? _procurementConfiguration;
        private readonly IProcurementControlEventService? _procurementControlEvents;
        private readonly IProcurementAcceptedSupplyService? _acceptedSupply;
        private readonly IFinanceBudgetCommitmentService? _budgetCommitments;
        private readonly IFinanceSourceDimensionService? _sourceDimensions;

        public VendorInvoiceService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService inventoryValuationService,
            ILogger<VendorInvoiceService> logger,
            IDocumentNumberingService documentNumberingService,
            IWorkflowService workflowService,
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            ITaxCalculationEngine? taxEngine = null,
            IFixedAssetService? fixedAssetService = null,
            IProcurementConfigurationService? procurementConfiguration = null,
            IProcurementControlEventService? procurementControlEvents = null,
            IProcurementAcceptedSupplyService? acceptedSupply = null,
            IFinanceBudgetCommitmentService? budgetCommitments = null,
            IFinanceSourceDimensionService? sourceDimensions = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _inventoryValuationService = inventoryValuationService;
            _logger = logger;
            _documentNumberingService = documentNumberingService;
            _workflowService = workflowService;
            _financePostingEngine = financePostingEngine;
            _financeAuditService = financeAuditService;
            _taxEngine = taxEngine;
            _fixedAssetService = fixedAssetService;
            _procurementConfiguration = procurementConfiguration;
            _procurementControlEvents = procurementControlEvents;
            _acceptedSupply = acceptedSupply;
            _budgetCommitments = budgetCommitments;
            _sourceDimensions = sourceDimensions;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";
        private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

        // ═════════════════════════════════════════════════════════════════
        //  GET
        // ═════════════════════════════════════════════════════════════════

        public async Task<VendorInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.Supplier)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .Include(i => i.PaymentAllocations)
                .FirstOrDefaultAsync(cancellationToken);

            return invoice == null ? null : MapToDto(invoice);
        }

        public async Task<VendorInvoiceDto?> GetByIdAsync(
            Guid id,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default)
        {
            EnsureVendorInvoiceRoute(producer);
            var invoice = await LoadInvoiceForPostingAsync(id, cancellationToken);
            var result = MapToDto(invoice);
            if (_sourceDimensions is not null)
                result.FinanceDimensions = await _sourceDimensions.GetAsync(
                    producer, invoice.Id, invoice.InvoiceDate,
                    await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                    cancellationToken);
            return result;
        }

        public async Task<VendorInvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.InvoiceNumber == invoiceNumber)
                .Include(i => i.Supplier)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .Include(i => i.PaymentAllocations)
                .FirstOrDefaultAsync(cancellationToken);

            return invoice == null ? null : MapToDto(invoice);
        }

        public async Task<PagedResult<VendorInvoiceDto>> GetAllAsync(VendorInvoiceQueryDto query, CancellationToken cancellationToken = default)
        {
            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                queryable = queryable.Where(i =>
                    i.InvoiceNumber.Contains(query.SearchTerm) ||
                    i.SupplierName.Contains(query.SearchTerm) ||
                    (i.SupplierInvoiceNumber != null && i.SupplierInvoiceNumber.Contains(query.SearchTerm)) ||
                    (i.Reference != null && i.Reference.Contains(query.SearchTerm)));
            }

            if (query.SupplierId.HasValue)
                queryable = queryable.Where(i => i.SupplierId == query.SupplierId.Value);

            if (query.Status.HasValue)
                queryable = queryable.Where(i => i.Status == query.Status.Value);

            if (!string.IsNullOrWhiteSpace(query.ApprovalStatus))
                queryable = queryable.Where(i => i.ApprovalStatus == query.ApprovalStatus);

            if (query.MatchingStatus.HasValue)
                queryable = queryable.Where(i => i.MatchingStatus == query.MatchingStatus.Value);

            if (query.FromDate.HasValue)
                queryable = queryable.Where(i => i.InvoiceDate >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                queryable = queryable.Where(i => i.InvoiceDate <= query.ToDate.Value);

            if (query.DueFromDate.HasValue)
                queryable = queryable.Where(i => i.DueDate >= query.DueFromDate.Value);

            if (query.DueToDate.HasValue)
                queryable = queryable.Where(i => i.DueDate <= query.DueToDate.Value);

            var now = DateTime.UtcNow;
            if (query.OverdueOnly == true)
            {
                queryable = queryable.Where(i =>
                    i.DueDate.HasValue &&
                    i.DueDate.Value < now &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            }

            if (query.IsOpeningBalance.HasValue)
                queryable = queryable.Where(i => i.IsOpeningBalance == query.IsOpeningBalance.Value);

            var totalCount = await queryable.CountAsync(cancellationToken);

            // Sorting
            queryable = query.SortBy?.ToLower() switch
            {
                "invoicenumber" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.InvoiceNumber)
                    : queryable.OrderBy(i => i.InvoiceNumber),
                "supplier" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.SupplierName)
                    : queryable.OrderBy(i => i.SupplierName),
                "amount" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.TotalAmount)
                    : queryable.OrderBy(i => i.TotalAmount),
                "duedate" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.DueDate)
                    : queryable.OrderBy(i => i.DueDate),
                "balance" => query.SortDescending
                    ? queryable.OrderByDescending(i => i.TotalAmount - i.PaidAmount)
                    : queryable.OrderBy(i => i.TotalAmount - i.PaidAmount),
                _ => queryable.OrderByDescending(i => i.InvoiceDate)
            };

            var invoices = await queryable
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Include(i => i.Supplier)
                .Include(i => i.LineItems)
                    .ThenInclude(li => li.GLAccount)
                .ToListAsync(cancellationToken);

            return new PagedResult<VendorInvoiceDto>
            {
                Items = invoices.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = query.Page,
                PageSize = query.PageSize
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  CREATE / UPDATE / DELETE
        // ═════════════════════════════════════════════════════════════════

        public Task<VendorInvoiceDto> CreateAsync(
            VendorInvoiceCreateDto dto,
            CancellationToken cancellationToken = default) =>
            CreateCoreAsync(dto, null, cancellationToken);

        public Task<VendorInvoiceDto> CreateAsync(
            VendorInvoiceCreateDto dto,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            CreateCoreAsync(dto, EnsureVendorInvoiceRoute(producer), cancellationToken);

        private async Task<VendorInvoiceDto> CreateCoreAsync(
            VendorInvoiceCreateDto dto,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var supplier = await ResolveSupplierForInvoiceAsync(dto.SupplierId, cancellationToken);
            var acceptedSupply = await ResolveAcceptedSupplyForCreateAsync(
                dto, supplier, cancellationToken);

            var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId ?? supplier.PaymentTermId, "Supplier", cancellationToken);
            var paymentTermsDays = paymentTerm?.DueDays ?? dto.PaymentTermsDays;
            var earlyPaymentDiscountPercentage = paymentTerm?.DiscountPercent ?? dto.EarlyPaymentDiscountPercentage;
            var earlyPaymentDiscountDueDate = paymentTerm != null && paymentTerm.DiscountPercent > 0 && paymentTerm.DiscountDays > 0
                ? dto.InvoiceDate.AddDays(paymentTerm.DiscountDays)
                : dto.EarlyPaymentDiscountDueDate;

            // Duplicate check
            if (!string.IsNullOrWhiteSpace(dto.SupplierInvoiceNumber))
            {
                var isDup = await IsDuplicateAsync(supplier.Id, dto.SupplierInvoiceNumber, dto.InvoiceDate, null, cancellationToken);
                if (isDup)
                    throw new InvalidOperationException($"Duplicate invoice detected with supplier reference '{dto.SupplierInvoiceNumber}'.");
            }

            var invoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var invoiceWht = await ResolveInvoiceWhtAsync(
                dto.WithholdingTaxId,
                dto.WithholdingTaxRate,
                dto.InvoiceDate,
                dto.IsOpeningBalance,
                cancellationToken);
            var governedExchangeRate = dto.IsOpeningBalance || dto.ExchangeRateId.HasValue
                ? await ResolveOpeningInvoiceExchangeRateAsync(
                    dto.CurrencyCode,
                    dto.InvoiceDate,
                    dto.ExchangeRateId,
                    dto.ExchangeRate,
                    cancellationToken)
                : null;

            var invoice = new VendorInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                InvoiceNumber = invoiceNumber,
                SupplierInvoiceNumber = dto.SupplierInvoiceNumber,
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                PurchaseOrderId = dto.PurchaseOrderId,
                InvoiceDate = dto.InvoiceDate,
                ReceivedDate = dto.ReceivedDate ?? now,
                DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(paymentTermsDays),
                CurrencyCode = governedExchangeRate?.TransactionCurrency ?? dto.CurrencyCode,
                ExchangeRate = governedExchangeRate?.Rate ?? dto.ExchangeRate,
                ExchangeRateId = governedExchangeRate?.ExchangeRateId,
                PaymentTermsDays = paymentTermsDays,
                PaymentTermId = paymentTerm?.Id,
                EarlyPaymentDiscountPercentage = earlyPaymentDiscountPercentage,
                EarlyPaymentDiscountDueDate = earlyPaymentDiscountDueDate,
                // Invoice WHT is an expected payment-time classification. The selected tenant tax
                // owns the rate and account; request values cannot silently override statutory setup.
                WithholdingTaxRate = invoiceWht.Rate,
                WithholdingTaxId = invoiceWht.TaxId,
                WithholdingTaxAccountId = invoiceWht.TaxPayableAccountId,
                WithholdingCertificateNumber = dto.WithholdingCertificateNumber,
                WithholdingCertificateDate = dto.WithholdingCertificateDate,
                MatchingType = ProcurementInvoiceThreeWayMatchRules.IsRequired(dto.PurchaseOrderId, dto.IsOpeningBalance)
                    ? InvoiceMatchingType.ThreeWay
                    : dto.MatchingType,
                MatchingStatus = InvoiceMatchingStatus.Unmatched,
                Status = VendorInvoiceStatus.Draft,
                ApprovalStatus = "Draft",
                ExpenseAccountId = dto.ExpenseAccountId,
                ApAccountId = dto.ApAccountId,
                Notes = dto.Notes,
                Reference = dto.Reference,
                IsOpeningBalance = dto.IsOpeningBalance,
                CreatedAt = now,
                CreatedBy = UserName
            };
            ApplyAcceptedSupply(invoice, acceptedSupply);

            // ── Process line items ──────────────────────────────────────
            decimal subtotal = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineGross = lineDto.Quantity * lineDto.UnitPrice;
                var lineDiscount = lineGross * (lineDto.DiscountPercentage / 100);
                var lineNet = lineGross - lineDiscount;
                var lineTax = await ResolveApLineTaxAsync(lineDto, lineNet, dto.InvoiceDate, supplier.Id, dto.IsOpeningBalance, cancellationToken);

                var lineItem = new VendorInvoiceLineItem
                {
                    Id = lineDto.Id is { } requestedLineId && requestedLineId != Guid.Empty
                        ? requestedLineId
                        : Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorInvoiceId = invoice.Id,
                    LineItemType = lineDto.LineItemType,
                    GLAccountId = lineDto.GLAccountId,
                    BudgetEntryId = lineDto.BudgetEntryId,
                    FixedAssetId = lineDto.FixedAssetId,
                    PurchaseOrderItemId = lineDto.PurchaseOrderItemId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxGroupId = dto.IsOpeningBalance ? null : lineDto.TaxGroupId,
                    TaxTreatment = lineDto.TaxTreatment,
                    TaxRate = lineTax.TaxRate,
                    TaxAmount = lineTax.TaxAmount,
                    TaxCode = lineDto.TaxCode,
                    DiscountPercentage = lineDto.DiscountPercentage,
                    DiscountAmount = lineDiscount,
                    Unit = lineDto.Unit,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                invoice.LineItems.Add(lineItem);
                subtotal += lineNet;
                totalTax += lineTax.TaxAmount;
                totalDiscount += lineDiscount;
            }

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.DiscountAmount = totalDiscount;
            invoice.TotalAmount = subtotal + totalTax;
            invoice.PaidAmount = 0;
            invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

            // This is an invoice estimate only. The actual liability and annual threshold are
            // recalculated from allocations when the supplier payment is created.
            invoice.WithholdingTaxAmount = decimal.Round(
                invoice.SubTotal * (invoice.WithholdingTaxRate / 100m),
                2,
                MidpointRounding.AwayFromZero);

            // Early payment discount amount
            if (invoice.EarlyPaymentDiscountPercentage > 0)
            {
                invoice.EarlyPaymentDiscountAmount = invoice.TotalAmount * (invoice.EarlyPaymentDiscountPercentage / 100);
            }

            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AP invoice route.");
                await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await _unitOfWork.Repository<VendorInvoice>().AddAsync(invoice);
                    await _unitOfWork.SaveChangesAsync(token);
                    await _sourceDimensions.SynchronizeDraftAsync(
                        producer, invoice.Id, invoice.InvoiceDate,
                        await BuildDimensionLineContextsAsync(invoice, token),
                        dto.FinanceDimensions,
                        inheritDefaultForUnassignedLines: true,
                        invoice.LineItems.Any(line => !line.IsDeleted && line.BudgetEntryId.HasValue)
                            ? VendorInvoiceBudgetSource
                            : null,
                        "Vendor invoice created.",
                        token);
                }, cancellationToken);
            }
            else
            {
                await _unitOfWork.Repository<VendorInvoice>().AddAsync(invoice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Created vendor invoice {InvoiceNumber} for supplier {SupplierId}", invoiceNumber, supplier.Id);

            return producer is null
                ? MapToDto(invoice)
                : await GetByIdAsync(invoice.Id, producer, cancellationToken) ?? MapToDto(invoice);
        }

        public Task<VendorInvoiceDto> UpdateAsync(
            VendorInvoiceUpdateDto dto,
            CancellationToken cancellationToken = default) =>
            UpdateCoreAsync(dto, null, cancellationToken);

        public Task<VendorInvoiceDto> UpdateAsync(
            VendorInvoiceUpdateDto dto,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            UpdateCoreAsync(dto, EnsureVendorInvoiceRoute(producer), cancellationToken);

        private async Task<VendorInvoiceDto> UpdateCoreAsync(
            VendorInvoiceUpdateDto dto,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == dto.Id)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{dto.Id}' not found.");

            if (invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted vendor invoices cannot be updated. Use a reversal, credit note, or adjustment.");

            if (invoice.Status != VendorInvoiceStatus.Draft && invoice.Status != VendorInvoiceStatus.Rejected)
                throw new InvalidOperationException("Only draft or rejected invoices can be updated.");

            var previousBudgetKey = BuildVendorInvoiceBudgetMutationKey(invoice);
            var previouslyBudgetRelevant = invoice.LineItems.Any(line =>
                !line.IsDeleted && line.BudgetEntryId.HasValue);

            var supplier = await _unitOfWork.Repository<Supplier>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == invoice.SupplierId);
            var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId ?? supplier?.PaymentTermId, "Supplier", cancellationToken);
            var paymentTermsDays = paymentTerm?.DueDays ?? dto.PaymentTermsDays;
            var earlyPaymentDiscountPercentage = paymentTerm?.DiscountPercent ?? dto.EarlyPaymentDiscountPercentage;
            var earlyPaymentDiscountDueDate = paymentTerm != null && paymentTerm.DiscountPercent > 0 && paymentTerm.DiscountDays > 0
                ? dto.InvoiceDate.AddDays(paymentTerm.DiscountDays)
                : dto.EarlyPaymentDiscountDueDate;
            var invoiceWht = await ResolveInvoiceWhtAsync(
                dto.WithholdingTaxId,
                dto.WithholdingTaxRate,
                dto.InvoiceDate,
                dto.IsOpeningBalance,
                cancellationToken);
            var governedExchangeRate = dto.IsOpeningBalance || dto.ExchangeRateId.HasValue
                ? await ResolveOpeningInvoiceExchangeRateAsync(
                    dto.CurrencyCode,
                    dto.InvoiceDate,
                    dto.ExchangeRateId,
                    dto.ExchangeRate,
                    cancellationToken)
                : null;
            var acceptedSupply = await ResolveAcceptedSupplyForUpdateAsync(
                invoice, dto, supplier, cancellationToken);

            var now = DateTime.UtcNow;
            invoice.SupplierInvoiceNumber = dto.SupplierInvoiceNumber;
            invoice.PurchaseOrderId = dto.PurchaseOrderId;
            invoice.InvoiceDate = dto.InvoiceDate;
            invoice.ReceivedDate = dto.ReceivedDate;
            invoice.DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(paymentTermsDays);
            invoice.CurrencyCode = governedExchangeRate?.TransactionCurrency ?? dto.CurrencyCode;
            invoice.ExchangeRate = governedExchangeRate?.Rate ?? dto.ExchangeRate;
            invoice.ExchangeRateId = governedExchangeRate?.ExchangeRateId;
            invoice.PaymentTermsDays = paymentTermsDays;
            invoice.PaymentTermId = paymentTerm?.Id;
            invoice.EarlyPaymentDiscountPercentage = earlyPaymentDiscountPercentage;
            invoice.EarlyPaymentDiscountDueDate = earlyPaymentDiscountDueDate;
            invoice.WithholdingTaxRate = invoiceWht.Rate;
            invoice.WithholdingTaxId = invoiceWht.TaxId;
            invoice.WithholdingTaxAccountId = invoiceWht.TaxPayableAccountId;
            invoice.WithholdingCertificateNumber = dto.WithholdingCertificateNumber;
            invoice.WithholdingCertificateDate = dto.WithholdingCertificateDate;
            invoice.MatchingType = ProcurementInvoiceThreeWayMatchRules.IsRequired(dto.PurchaseOrderId, dto.IsOpeningBalance)
                ? InvoiceMatchingType.ThreeWay
                : dto.MatchingType;
            invoice.ExpenseAccountId = dto.ExpenseAccountId;
            invoice.ApAccountId = dto.ApAccountId;
            invoice.Notes = dto.Notes;
            invoice.Reference = dto.Reference;
            invoice.IsOpeningBalance = dto.IsOpeningBalance;
            ApplyAcceptedSupply(invoice, acceptedSupply);
            InvalidateMatching(invoice, "Invoice content changed after its previous matching evaluation.");
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // If rejected, reset back to draft
            if (invoice.Status == VendorInvoiceStatus.Rejected)
            {
                invoice.Status = VendorInvoiceStatus.Draft;
                invoice.ApprovalStatus = "Draft";
            }

            var existingLines = invoice.LineItems.Where(line => !line.IsDeleted)
                .ToDictionary(line => line.Id);
            var requestedExistingIds = dto.LineItems.Where(line => line.Id.HasValue)
                .Select(line => line.Id!.Value).ToArray();
            if (requestedExistingIds.Any(id => id == Guid.Empty)
                || requestedExistingIds.Distinct().Count() != requestedExistingIds.Length
                || requestedExistingIds.Any(id => !existingLines.ContainsKey(id)))
                throw new InvalidOperationException("Vendor invoice line identities are invalid or belong to another document.");
            foreach (var existing in existingLines.Values.Where(line => !requestedExistingIds.Contains(line.Id)).ToList())
            {
                await _unitOfWork.Repository<VendorInvoiceLineItem>().DeleteAsync(existing);
                invoice.LineItems.Remove(existing);
            }

            decimal subtotal = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineGross = lineDto.Quantity * lineDto.UnitPrice;
                var lineDiscount = lineGross * (lineDto.DiscountPercentage / 100);
                var lineNet = lineGross - lineDiscount;
                var lineTax = await ResolveApLineTaxAsync(lineDto, lineNet, dto.InvoiceDate, invoice.SupplierId, dto.IsOpeningBalance, cancellationToken);

                var lineItem = lineDto.Id.HasValue
                    ? existingLines[lineDto.Id.Value]
                    : new VendorInvoiceLineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        VendorInvoiceId = invoice.Id,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };
                lineItem.LineItemType = lineDto.LineItemType;
                lineItem.GLAccountId = lineDto.GLAccountId;
                lineItem.BudgetEntryId = lineDto.BudgetEntryId;
                lineItem.FixedAssetId = lineDto.FixedAssetId;
                lineItem.PurchaseOrderItemId = lineDto.PurchaseOrderItemId;
                lineItem.Description = lineDto.Description;
                lineItem.Quantity = lineDto.Quantity;
                lineItem.UnitPrice = lineDto.UnitPrice;
                lineItem.TaxGroupId = dto.IsOpeningBalance ? null : lineDto.TaxGroupId;
                lineItem.TaxTreatment = lineDto.TaxTreatment;
                lineItem.TaxRate = lineTax.TaxRate;
                lineItem.TaxAmount = lineTax.TaxAmount;
                lineItem.TaxCode = lineDto.TaxCode;
                lineItem.DiscountPercentage = lineDto.DiscountPercentage;
                lineItem.DiscountAmount = lineDiscount;
                lineItem.Unit = lineDto.Unit;
                lineItem.UpdatedAt = now;
                lineItem.UpdatedBy = UserName;
                if (!lineDto.Id.HasValue)
                {
                    invoice.LineItems.Add(lineItem);
                }
                subtotal += lineNet;
                totalTax += lineTax.TaxAmount;
                totalDiscount += lineDiscount;
            }

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.DiscountAmount = totalDiscount;
            invoice.TotalAmount = subtotal + totalTax;
            invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

            // Assign zero explicitly when WHT is removed so a draft edit cannot retain a stale
            // deduction from the invoice's previous configured category.
            invoice.WithholdingTaxAmount = decimal.Round(
                invoice.SubTotal * (invoice.WithholdingTaxRate / 100m),
                2,
                MidpointRounding.AwayFromZero);

            if (invoice.EarlyPaymentDiscountPercentage > 0)
                invoice.EarlyPaymentDiscountAmount = invoice.TotalAmount * (invoice.EarlyPaymentDiscountPercentage / 100);

            var currentBudgetKey = BuildVendorInvoiceBudgetMutationKey(invoice);
            var currentlyBudgetRelevant = invoice.LineItems.Any(line =>
                !line.IsDeleted && line.BudgetEntryId.HasValue);

            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AP invoice route.");
                await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                    await _unitOfWork.SaveChangesAsync(token);
                    await _sourceDimensions.SynchronizeDraftAsync(
                        producer, invoice.Id, invoice.InvoiceDate,
                        await BuildDimensionLineContextsAsync(invoice, token),
                        dto.FinanceDimensions,
                        inheritDefaultForUnassignedLines: true,
                        currentlyBudgetRelevant ? VendorInvoiceBudgetSource : null,
                        "Vendor invoice draft changed.",
                        token);
                    if ((previouslyBudgetRelevant || currentlyBudgetRelevant)
                        && !string.Equals(previousBudgetKey, currentBudgetKey, StringComparison.Ordinal))
                    {
                        await _sourceDimensions.InvalidateBudgetEvidenceAsync(
                            producer,
                            invoice.Id,
                            VendorInvoiceBudgetSource,
                            requiresReevaluation: currentlyBudgetRelevant,
                            "Vendor invoice budget-key fields changed; explicit budget refresh is required.",
                            token);
                    }
                }, cancellationToken);
            }
            else
            {
                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Updated vendor invoice {InvoiceId}", invoice.Id);

            return producer is null
                ? MapToDto(invoice)
                : await GetByIdAsync(invoice.Id, producer, cancellationToken) ?? MapToDto(invoice);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == id);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            if (invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted vendor invoices cannot be deleted. Use a reversal, credit note, or adjustment.");

            if (invoice.Status != VendorInvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be deleted.");

            await _unitOfWork.Repository<VendorInvoice>().DeleteAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted vendor invoice {InvoiceId}", id);
        }

        // ═════════════════════════════════════════════════════════════════
        //  APPROVAL WORKFLOW
        // ═════════════════════════════════════════════════════════════════

        public Task<VendorInvoiceDto> SubmitForApprovalAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            SubmitForApprovalRouteAsync(id, null, cancellationToken);

        public Task<VendorInvoiceDto> SubmitForApprovalAsync(
            Guid id,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            SubmitForApprovalRouteAsync(id, EnsureVendorInvoiceRoute(producer), cancellationToken);

        private async Task<VendorInvoiceDto> SubmitForApprovalRouteAsync(
            Guid id,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var scope = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .AsNoTracking()
                .Select(i => new { i.PurchaseOrderId, i.IsOpeningBalance })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            var requiresProcurementMatch = ProcurementInvoiceThreeWayMatchRules.IsRequired(
                scope.PurchaseOrderId,
                scope.IsOpeningBalance);
            var lockResource = requiresProcurementMatch
                ? $"tdc-ap-match:{TenantId:N}:{scope.PurchaseOrderId!.Value:N}"
                : $"finance-ap-budget:{TenantId:N}:{id:N}";
            if (_unitOfWork.HasActiveTransaction)
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    lockResource,
                    cancellationToken);
                return await SubmitForApprovalCoreAsync(id, producer, cancellationToken);
            }

            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        lockResource,
                        cancellationToken);
                    var result = await SubmitForApprovalCoreAsync(
                        id,
                        producer,
                        cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return result;
                }
                catch (VendorInvoiceMatchControlException)
                {
                    // A denied evaluation is itself compliance evidence. The invoice
                    // remains Draft, so commit that immutable decision before returning
                    // the structured hard stop to the caller.
                    await _unitOfWork.CommitAsync(cancellationToken);
                    throw;
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

        private async Task<VendorInvoiceDto> SubmitForApprovalCoreAsync(
            Guid id,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var invoice = await GetEntityOrThrowAsync(id, cancellationToken);

            if (invoice.Status != VendorInvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be submitted for approval.");

            if (invoice.IsOpeningBalance)
                await RevalidateOpeningInvoiceExchangeRateAsync(invoice, cancellationToken);

            if (ProcurementInvoiceThreeWayMatchRules.IsRequired(invoice.PurchaseOrderId, invoice.IsOpeningBalance))
                await EvaluateThreeWayMatchAsync(invoice.Id, "SubmitForApproval", requireApprovalReady: true, cancellationToken);
            else
                await RevalidateWorksAcceptedSupplyAsync(invoice, cancellationToken);

            invoice = await LoadInvoiceForPostingAsync(invoice.Id, cancellationToken);
            var budgetRequest = await BuildVendorInvoiceBudgetRequestAsync(
                invoice, "Submit", useSourceDimensions: producer is not null, cancellationToken);
            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AP invoice route.");
                await _sourceDimensions.ValidateAndFreezeAsync(
                    producer,
                    invoice.Id,
                    invoice.InvoiceDate,
                    await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                    requireCurrentBudgetEvidence: budgetRequest is not null,
                    cancellationToken);
            }
            var budgetReservations = budgetRequest is null
                ? Array.Empty<FinanceBudgetReservationDto>()
                : producer is null
                    ? (await _budgetCommitments!.ReserveAsync(budgetRequest, cancellationToken)).Reservations
                    : await RequireCurrentVendorInvoiceBudgetAsync(budgetRequest, cancellationToken);

            var now = DateTime.UtcNow;
            invoice.Status = VendorInvoiceStatus.PendingApproval;
            invoice.ApprovalStatus = "PendingApproval";
            invoice.SubmittedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
            invoice.SubmittedDate = now;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("VendorInvoice", id);
            if (!workflowResult.Success)
            {
                await ReleaseVendorInvoiceBudgetAsync(
                    invoice.Id,
                    budgetReservations,
                    "Vendor invoice workflow did not start.",
                    "WorkflowStartFailed",
                    cancellationToken);
                if (producer is not null && _sourceDimensions is not null && budgetRequest is not null)
                    await _sourceDimensions.InvalidateBudgetEvidenceAsync(
                        producer,
                        invoice.Id,
                        VendorInvoiceBudgetSource,
                        requiresReevaluation: true,
                        "Vendor invoice workflow did not start; budget evidence must be refreshed before resubmission.",
                        cancellationToken);
                invoice.Status = VendorInvoiceStatus.Draft;
                invoice.ApprovalStatus = "Draft";
                invoice.SubmittedById = null;
                invoice.SubmittedDate = null;
                invoice.UpdatedAt = DateTime.UtcNow;
                invoice.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                throw new InvalidOperationException(workflowResult.Message ?? "Unable to start vendor invoice approval workflow.");
            }

            await RecordApInvoiceAuditAsync(
                FinanceAuditEvents.ApInvoiceSubmitted,
                invoice,
                afterValues: new
                {
                    invoice.Status,
                    invoice.ApprovalStatus,
                    invoice.SubmittedById,
                    invoice.SubmittedDate
                },
                comment: "Submitted for approval.",
                cancellationToken: cancellationToken);

            _logger.LogInformation("Vendor invoice {InvoiceNumber} submitted for approval", invoice.InvoiceNumber);
            return producer is null
                ? MapToDto(invoice)
                : await GetByIdAsync(invoice.Id, producer, cancellationToken) ?? MapToDto(invoice);
        }

        public Task<VendorInvoiceDto> ApproveAsync(
            Guid id,
            string? comments = null,
            CancellationToken cancellationToken = default) =>
            ApproveCoreAsync(id, null, comments, cancellationToken);

        public Task<VendorInvoiceDto> ApproveAsync(
            Guid id,
            FinancePostingProducerContext producer,
            string? comments = null,
            CancellationToken cancellationToken = default) =>
            ApproveCoreAsync(id, EnsureVendorInvoiceRoute(producer), comments, cancellationToken);

        private async Task<VendorInvoiceDto> ApproveCoreAsync(
            Guid id,
            FinancePostingProducerContext? producer,
            string? comments,
            CancellationToken cancellationToken)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            if (invoice.Status != VendorInvoiceStatus.PendingApproval)
                throw new InvalidOperationException("Only pending invoices can be approved.");

            if (invoice.IsOpeningBalance)
                await RevalidateOpeningInvoiceExchangeRateAsync(invoice, cancellationToken);

            if (ProcurementInvoiceThreeWayMatchRules.IsRequired(invoice.PurchaseOrderId, invoice.IsOpeningBalance))
                await EvaluateThreeWayMatchAsync(invoice.Id, "Approve", requireApprovalReady: true, cancellationToken);
            else
                await RevalidateWorksAcceptedSupplyAsync(invoice, cancellationToken);

            invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.LineItems)
                .SingleAsync(cancellationToken);

            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AP invoice route.");
                await _sourceDimensions.ValidateAndFreezeAsync(
                    producer, invoice.Id, invoice.InvoiceDate,
                    await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                    requireCurrentBudgetEvidence: invoice.LineItems.Any(line => line.BudgetEntryId.HasValue),
                    cancellationToken);
            }

            var approverId = CurrentUserId;
            if (approverId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            if (!await _workflowService.CanUserApproveAsync("VendorInvoice", id, approverId))
                throw new InvalidOperationException("This vendor invoice is assigned to another workflow approver.");

            var workflowResult = await _workflowService.ProcessApprovalStepAsync("VendorInvoice", id, approverId, "Approve", comments);
            if (!workflowResult.Success)
                throw new InvalidOperationException(workflowResult.Message ?? "Unable to process vendor invoice approval.");

            if (workflowResult.Status != WorkflowInstanceStatus.Completed)
            {
                _logger.LogInformation(
                    "Recorded intermediate approval for vendor invoice {InvoiceNumber}; workflow status is {WorkflowStatus}",
                    invoice.InvoiceNumber,
                    workflowResult.Status);
                return await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice);
            }

            var now = DateTime.UtcNow;
            invoice.Status = VendorInvoiceStatus.Approved;
            invoice.ApprovalStatus = "Approved";
            invoice.ApprovedById = approverId;
            invoice.ApprovedDate = now;
            invoice.ApprovalComments = comments;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // Inventory is posted only by the governed purchase-receipt/inspection
            // lifecycle. Invoice approval recognizes the AP obligation and must not
            // create a second stock receipt (or a phantom non-PO receipt).

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordApInvoiceAuditAsync(
                FinanceAuditEvents.ApInvoiceApproved,
                invoice,
                afterValues: new
                {
                    invoice.Status,
                    invoice.ApprovalStatus,
                    invoice.ApprovedById,
                    invoice.ApprovedDate,
                    invoice.ApprovalComments
                },
                comment: comments,
                cancellationToken: cancellationToken);

            // Opening-balance AP invoices are posted through the controlled migration flow, not normal AP posting.
            if (!invoice.IsOpeningBalance)
            {
                if (producer is null)
                    await PostAsync(invoice.Id, cancellationToken);
                else
                    await PostAsync(invoice.Id, producer, cancellationToken);
            }

            _logger.LogInformation(
                "Approved vendor invoice {InvoiceNumber}. Normal GL posting executed={PostingExecuted}. OpeningBalance={IsOpeningBalance}",
                invoice.InvoiceNumber,
                !invoice.IsOpeningBalance,
                invoice.IsOpeningBalance);

            return producer is null
                ? await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice)
                : await GetByIdAsync(invoice.Id, producer, cancellationToken) ?? MapToDto(invoice);
        }

        public Task<VendorInvoiceDto> PostAsync(Guid id, CancellationToken cancellationToken = default) =>
            PostCoreAsync(id, null, cancellationToken);

        public Task<VendorInvoiceDto> PostAsync(
            Guid id,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default) =>
            PostCoreAsync(id, EnsureVendorInvoiceRoute(producer), cancellationToken);

        private async Task<VendorInvoiceDto> PostCoreAsync(
            Guid id,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP invoice posting.");

            var invoice = await LoadInvoiceForPostingAsync(id, cancellationToken);
            if (producer is not null)
            {
                if (_sourceDimensions is null)
                    throw new InvalidOperationException("Finance source dimensions are not configured for the manual AP invoice route.");
                await _sourceDimensions.ValidateAndFreezeAsync(
                    producer, invoice.Id, invoice.InvoiceDate,
                    await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                    requireCurrentBudgetEvidence: invoice.LineItems.Any(line => line.BudgetEntryId.HasValue),
                    cancellationToken);
            }
            var wasAlreadyLinked = invoice.JournalEntryId.HasValue;
            // Procured assets are capitalized from the accepted receipt carrying value by
            // FIN-INT-007. Their later supplier invoice clears GRV only, so treating those lines
            // as direct AP capitalization would debit the asset account a second time.
            var hasDirectApFixedAssetLines = !invoice.IsOpeningBalance &&
                !IsProcurementGrvClearingInvoice(invoice) &&
                invoice.LineItems.Any(IsFixedAssetLine);
            if (hasDirectApFixedAssetLines && _fixedAssetService == null)
            {
                throw new InvalidOperationException("Fixed asset capitalization service is not configured for AP fixed asset lines.");
            }

            try
            {
                var budgetReservationIds = await PrepareVendorInvoiceBudgetForPostingAsync(
                    invoice,
                    producer,
                    cancellationToken);
                var postingRequest = await BuildApInvoicePostingRequestAsync(
                    invoice,
                    budgetReservationIds,
                    producer,
                    cancellationToken);
                var postingResult = producer is null
                    ? await _financePostingEngine.PostAsync(postingRequest, cancellationToken)
                    : await _financePostingEngine.PostAsync(postingRequest, producer, cancellationToken);

                if (invoice.JournalEntryId.HasValue && invoice.JournalEntryId.Value != postingResult.JournalEntryId)
                {
                    throw new InvalidOperationException("Vendor invoice is linked to a different journal entry than the posting engine result.");
                }

                if (!invoice.JournalEntryId.HasValue)
                {
                    invoice.JournalEntryId = postingResult.JournalEntryId;
                    invoice.UpdatedAt = DateTime.UtcNow;
                    invoice.UpdatedBy = UserName;
                    await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                if (hasDirectApFixedAssetLines)
                {
                    await _fixedAssetService!.RecordApInvoiceCapitalizationAsync(
                        invoice.Id,
                        postingResult.JournalEntryId,
                        postingResult.PostingEventId,
                        cancellationToken);
                }

                if (!postingResult.WasDuplicate)
                {
                    await RecordTaxCalculationSnapshotsAsync(postingRequest.TaxCalculationSnapshots, cancellationToken);
                    if (postingRequest.TaxCalculationSnapshots.Count > 0)
                    {
                        await RecordApInvoiceAuditAsync(
                            FinanceAuditEvents.TaxCalculatedOnApInvoice,
                            invoice,
                            postingEventId: postingResult.PostingEventId,
                            journalEntryId: postingResult.JournalEntryId,
                            afterValues: new
                            {
                                snapshotCount = postingRequest.TaxCalculationSnapshots.Count,
                                totalTax = postingRequest.TaxCalculationSnapshots.Sum(s => s.TaxAmount)
                            },
                            comment: "AP invoice tax calculated from effective-dated tenant tax configuration.",
                            cancellationToken: cancellationToken);

                        await RecordApInvoiceAuditAsync(
                            FinanceAuditEvents.TaxPosted,
                            invoice,
                            postingEventId: postingResult.PostingEventId,
                            journalEntryId: postingResult.JournalEntryId,
                            afterValues: new
                            {
                                postingResult.PostingEventId,
                                postingResult.JournalEntryId,
                                taxLineCount = postingRequest.Lines.Count(l => l.TransactionTag != null && l.TransactionTag.StartsWith("AP-Tax-", StringComparison.OrdinalIgnoreCase))
                            },
                            comment: "AP invoice tax posted through the central finance posting engine.",
                            cancellationToken: cancellationToken);
                    }

                    await RecordApInvoiceAuditAsync(
                        FinanceAuditEvents.TaxConfigurationUsedInPosting,
                        invoice,
                        postingEventId: postingResult.PostingEventId,
                        journalEntryId: postingResult.JournalEntryId,
                        afterValues: new
                        {
                            snapshotCount = postingRequest.TaxCalculationSnapshots.Count,
                            taxIds = postingRequest.TaxCalculationSnapshots.Select(s => s.TaxId).Distinct().ToArray()
                        },
                        comment: "Effective-dated tax configuration used for AP invoice posting where available.",
                        cancellationToken: cancellationToken);
                }

                if (postingResult.WasDuplicate || wasAlreadyLinked)
                {
                    await RecordApInvoiceAuditAsync(
                        FinanceAuditEvents.ApInvoiceDuplicatePostingAttempt,
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
                        comment: "Duplicate AP invoice posting request returned the existing posting.",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await RecordApInvoiceAuditAsync(
                        FinanceAuditEvents.ApInvoicePosted,
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
                        comment: "AP invoice posted through the central finance posting engine.",
                        cancellationToken: cancellationToken);
                }

                _logger.LogInformation(
                    "Posted AP invoice {InvoiceNumber} through finance posting engine with journal {JournalEntryId}. Duplicate={WasDuplicate}",
                    invoice.InvoiceNumber,
                    postingResult.JournalEntryId,
                    postingResult.WasDuplicate);

                return producer is null
                    ? await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice)
                    : await GetByIdAsync(invoice.Id, producer, cancellationToken) ?? MapToDto(invoice);
            }
            catch (Exception ex)
            {
                if (invoice.TaxAmount > 0m)
                {
                    await RecordApInvoiceAuditAsync(
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

                await RecordApInvoiceAuditAsync(
                    FinanceAuditEvents.ApInvoicePostingFailed,
                    invoice,
                    afterValues: new
                    {
                        invoice.JournalEntryId,
                        error = ex.Message
                    },
                    reason: ex.Message,
                    cancellationToken: cancellationToken);

                _logger.LogError(ex, "Failed to post AP invoice {InvoiceNumber}", invoice.InvoiceNumber);
                throw;
            }
        }

        public async Task<VendorInvoiceDto> RejectAsync(Guid id, string comments, CancellationToken cancellationToken = default)
        {
            if (_unitOfWork.HasActiveTransaction)
                return await RejectCoreAsync(id, comments, cancellationToken);

            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"finance-ap-budget:{TenantId:N}:{id:N}", cancellationToken);
                    var result = await RejectCoreAsync(id, comments, cancellationToken);
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

        private async Task<VendorInvoiceDto> RejectCoreAsync(
            Guid id,
            string comments,
            CancellationToken cancellationToken)
        {
            var invoice = await GetEntityOrThrowAsync(id, cancellationToken);

            // A client may lose the successful response after the workflow has already reached
            // its terminal state. Replaying the command must release any stranded budget
            // reservation instead of rejecting the retry before AP outcome reconciliation.
            if (invoice.Status == VendorInvoiceStatus.Rejected)
                return await ApplyRejectedWorkflowOutcomeAsync(id, comments, cancellationToken);

            if (invoice.Status != VendorInvoiceStatus.PendingApproval)
                throw new InvalidOperationException("Only pending invoices can be rejected.");

            var approverId = CurrentUserId;
            if (approverId == Guid.Empty)
                throw new InvalidOperationException("Unable to resolve the current approver.");

            if (!await _workflowService.CanUserApproveAsync("VendorInvoice", id, approverId))
                throw new InvalidOperationException("This vendor invoice is assigned to another workflow approver.");

            var workflowResult = await _workflowService.ProcessApprovalStepAsync("VendorInvoice", id, approverId, "Reject", comments);
            if (!workflowResult.Success)
                throw new InvalidOperationException(workflowResult.Message ?? "Unable to process vendor invoice rejection.");

            if (workflowResult.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
            {
                _logger.LogInformation(
                    "Recorded vendor invoice rejection workflow action for {InvoiceNumber}; workflow status is {WorkflowStatus}",
                    invoice.InvoiceNumber,
                    workflowResult.Status);
                return MapToDto(invoice);
            }

            return await ApplyRejectedWorkflowOutcomeAsync(id, comments, cancellationToken);
        }

        public async Task<VendorInvoiceDto> ApplyRejectedWorkflowOutcomeAsync(
            Guid id,
            string? comments,
            CancellationToken cancellationToken = default)
        {
            var invoice = await GetEntityOrThrowAsync(id, cancellationToken);
            if (invoice.Status is not (VendorInvoiceStatus.PendingApproval or VendorInvoiceStatus.Rejected))
                throw new InvalidOperationException(
                    "Only a pending or already rejected invoice can receive a rejected workflow outcome.");

            await ReleaseVendorInvoiceBudgetAsync(
                invoice.Id,
                reservations: null,
                "Vendor invoice was rejected.",
                "Rejected",
                cancellationToken);

            if (invoice.Status == VendorInvoiceStatus.Rejected)
                return MapToDto(invoice);

            var now = DateTime.UtcNow;
            invoice.Status = VendorInvoiceStatus.Rejected;
            invoice.ApprovalStatus = "Rejected";
            invoice.ApprovalComments = comments;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await RecordApInvoiceAuditAsync(
                FinanceAuditEvents.ApInvoiceRejected,
                invoice,
                afterValues: new
                {
                    invoice.Status,
                    invoice.ApprovalStatus,
                    invoice.ApprovalComments
                },
                reason: comments,
                comment: comments,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Rejected vendor invoice {InvoiceNumber}. Reason: {Comments}", invoice.InvoiceNumber, comments);
            return MapToDto(invoice);
        }

        public Task<VendorInvoiceDto> VoidAsync(
            Guid id,
            string reason,
            CancellationToken cancellationToken = default) =>
            VoidAsync(id, reason, cancellationToken, executionStrategyScope: false);

        private async Task<VendorInvoiceDto> VoidAsync(
            Guid id,
            string reason,
            CancellationToken cancellationToken,
            bool executionStrategyScope)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A void and reversal reason is required.", nameof(reason));
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP invoice reversal.");

            if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
            {
                return await _unitOfWork.ExecuteInStrategyAsync(
                    () => VoidAsync(id, reason, cancellationToken, executionStrategyScope: true),
                    cancellationToken);
            }

            var ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            VendorInvoice? invoice = null;
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"tdc0508-invoice:{TenantId:N}:{id:N}", cancellationToken);

                invoice = await _unitOfWork.Repository<VendorInvoice>()
                    .GetQueryable(i => i.TenantId == TenantId && i.Id == id && !i.IsDeleted)
                    .Include(i => i.PaymentAllocations.Where(a => !a.IsDeleted))
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

                // A posted supplier debit note is the controlled correction of this exact AP
                // recognition. Do not void its source invoice underneath that evidence; reverse
                // the debit note and any payment applications first.
                var activeDebitNoteExists = await _unitOfWork.Repository<SupplierDebitNote>()
                    .ExistsAsync(note =>
                        note.TenantId == TenantId &&
                        note.OriginalVendorInvoiceId == invoice.Id &&
                        !note.IsDeleted &&
                        note.Status != SupplierDebitNoteStatus.Cancelled &&
                        note.Status != SupplierDebitNoteStatus.Reversed);
                if (activeDebitNoteExists)
                    throw new InvalidOperationException(
                        "Cannot void an invoice with an active linked supplier debit note. Cancel or reverse the debit note and its payment applications first.");

                var supplierCreditApplications = await _unitOfWork.Repository<SupplierDebitNoteApplication>()
                    .GetQueryable(application =>
                        application.TenantId == TenantId &&
                        application.VendorInvoiceId == invoice.Id &&
                        !application.IsDeleted)
                    .Select(application => new
                    {
                        application.Id,
                        application.IsReversal,
                        application.OriginalApplicationId
                    })
                    .ToListAsync(cancellationToken);
                var reversedApplicationIds = supplierCreditApplications
                    .Where(item => item.IsReversal && item.OriginalApplicationId.HasValue)
                    .Select(item => item.OriginalApplicationId!.Value)
                    .ToHashSet();
                if (supplierCreditApplications.Any(item =>
                        !item.IsReversal && !reversedApplicationIds.Contains(item.Id)))
                    throw new InvalidOperationException(
                        "Cannot void an invoice with an effective supplier debit-note application. Reverse the application first.");

                var activeSettlement = RoundMoney(invoice.PaymentAllocations.Sum(
                    allocation => allocation.AllocatedAmount +
                                  allocation.DiscountAmount +
                                  allocation.WithholdingTaxAmount));
                if (Math.Abs(activeSettlement) > 0.01m)
                {
                    throw new InvalidOperationException(
                        "Cannot void an invoice with active payment settlement. Reverse or void the posted payment first.");
                }

                FinancePostingResultDto? reversal = null;
                if (invoice.JournalEntryId.HasValue)
                {
                    // AP-created fixed assets share the invoice journal. Validate the asset
                    // lifecycle before reversing that journal; otherwise a successful AP void
                    // could leave depreciation/valuation based on cost that no longer exists.
                    // The post-step below then updates the register using this same reversal event
                    // rather than creating a second journal that would duplicate AP/tax reversal.
                    if (_fixedAssetService != null)
                        await _fixedAssetService.ValidateApInvoiceCapitalizationReversalAsync(invoice.Id, cancellationToken);

                    var originalEvent = await GetPostedInvoiceEventAsync(invoice, cancellationToken);
                    var originalJournal = await _unitOfWork.Repository<JournalEntry>()
                        .GetQueryable(j =>
                            j.TenantId == TenantId &&
                            j.Id == invoice.JournalEntryId.Value &&
                            !j.IsDeleted)
                        .SingleOrDefaultAsync(cancellationToken)
                        ?? throw new InvalidOperationException(
                            "The AP invoice posting journal was not found for this tenant.");

                    if (!originalJournal.IsReversed)
                    {
                        var plan = await _financePostingEngine.GetReversalPlanAsync(
                            originalEvent.Id,
                            reason.Trim(),
                            DateTime.UtcNow.Date,
                            cancellationToken);
                        reversal = await _financePostingEngine.PostAsync(
                            BuildApReversalRequest(
                                originalEvent,
                                plan,
                                invoice.Id,
                                invoice.InvoiceNumber,
                                "VendorInvoice",
                                "AP Invoice Reversal",
                                $"Reverse AP invoice {invoice.InvoiceNumber}",
                                $"AP:VendorInvoice:{invoice.TenantId:N}:{invoice.Id:N}:Reverse",
                                reason),
                            cancellationToken);
                    }
                    else
                    {
                        reversal = await GetExistingReversalResultAsync(
                            originalJournal,
                            invoice.Id,
                            "VendorInvoice",
                            cancellationToken);
                    }
                }

                if (reversal != null && _fixedAssetService != null)
                {
                    await _fixedAssetService.RecordApInvoiceCapitalizationReversalAsync(
                        invoice.Id,
                        reversal.JournalEntryId,
                        reversal.PostingEventId,
                        reason.Trim(),
                        cancellationToken);
                }

                if (invoice.Status != VendorInvoiceStatus.Voided)
                {
                    var now = DateTime.UtcNow;
                    invoice.Status = VendorInvoiceStatus.Voided;
                    invoice.Notes = AppendLifecycleNote(
                        invoice.Notes,
                        $"Voided on {now:yyyy-MM-dd HH:mm}: {reason.Trim()}");
                    invoice.UpdatedAt = now;
                    invoice.UpdatedBy = UserName;
                    invoice.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
                    await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    await RecordApInvoiceAuditAsync(
                        invoice.JournalEntryId.HasValue
                            ? FinanceAuditEvents.ApInvoiceReversed
                            : FinanceAuditEvents.ApInvoiceVoided,
                        invoice,
                        postingEventId: reversal?.PostingEventId,
                        journalEntryId: reversal?.JournalEntryId,
                        beforeValues: new { status = "PostedOrApproved", invoice.JournalEntryId },
                        afterValues: new
                        {
                            invoice.Status,
                            OriginalJournalEntryId = invoice.JournalEntryId,
                            ReversalPostingEventId = reversal?.PostingEventId,
                            ReversalJournalEntryId = reversal?.JournalEntryId,
                            ActiveSettlementAmount = activeSettlement
                        },
                        reason: reason.Trim(),
                        comment: "AP invoice state and its balanced Finance reversal were committed atomically.",
                        cancellationToken: cancellationToken);
                }

                if (ownsTransaction)
                    await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogWarning(
                    "Voided AP invoice {InvoiceNumber}. OriginalJournal={OriginalJournalId}; ReversalJournal={ReversalJournalId}; Reason={Reason}",
                    invoice.InvoiceNumber,
                    invoice.JournalEntryId,
                    reversal?.JournalEntryId,
                    reason.Trim());

                return await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice);
            }
            catch (Exception ex)
            {
                if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();

                if (invoice != null)
                {
                    await RecordApInvoiceAuditAsync(
                        FinanceAuditEvents.ApInvoiceReversalFailed,
                        invoice,
                        afterValues: new { invoice.JournalEntryId, error = ex.Message },
                        reason: ex.Message,
                        comment: "AP invoice void was rejected; no document or ledger mutation was committed.",
                        cancellationToken: cancellationToken);
                }

                throw;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  MATCHING
        // ═════════════════════════════════════════════════════════════════

        public async Task<InvoiceMatchingResultDto> PerformTwoWayMatchAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == invoiceId)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{invoiceId}' not found.");

            if (!invoice.PurchaseOrderId.HasValue)
                throw new InvalidOperationException("No purchase order linked to this invoice for matching.");

            var po = await _unitOfWork.Repository<PurchaseOrder>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == invoice.PurchaseOrderId.Value)
                .Include(p => p.Items)
                .FirstOrDefaultAsync(cancellationToken);

            if (po == null)
                throw new KeyNotFoundException($"Purchase order with Id '{invoice.PurchaseOrderId}' not found.");

            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var priceTolerance = ProcurementInvoiceThreeWayMatchRules.NormalizeTolerance(
                settings.ApInvoicePriceTolerancePercent);
            var quantityTolerance = ProcurementInvoiceThreeWayMatchRules.NormalizeTolerance(
                settings.ApInvoiceQuantityTolerancePercent);

            var result = new InvoiceMatchingResultDto
            {
                VendorInvoiceId = invoiceId,
                MatchingType = InvoiceMatchingType.TwoWay,
                InvoiceTotal = invoice.TotalAmount,
                PurchaseOrderTotal = po.TotalAmount,
                TolerancePercentage = priceTolerance,
                PriceTolerancePercentage = priceTolerance,
                QuantityTolerancePercentage = quantityTolerance,
                IsRequired = ProcurementInvoiceThreeWayMatchRules.IsRequired(
                    invoice.PurchaseOrderId, invoice.IsOpeningBalance)
            };

            // Compare totals
            var variance = Math.Abs(invoice.TotalAmount - po.TotalAmount);
            var variancePercentage = po.TotalAmount > 0 ? (variance / po.TotalAmount * 100) : 100;

            if (variancePercentage <= result.TolerancePercentage)
            {
                result.IsMatched = true;
                result.MatchingStatus = InvoiceMatchingStatus.TwoWayMatched;
            }
            else
            {
                result.IsMatched = false;
                result.MatchingStatus = InvoiceMatchingStatus.MatchException;
                result.Discrepancies.Add(new MatchingDiscrepancyDto
                {
                    ItemDescription = "Total Amount",
                    DiscrepancyType = "Price",
                    InvoiceValue = invoice.TotalAmount,
                    ExpectedValue = po.TotalAmount,
                    Variance = variance,
                    VariancePercentage = variancePercentage
                });
            }

            // Line-by-line comparison for PO items
            foreach (var invoiceLine in invoice.LineItems.Where(l => l.PurchaseOrderItemId.HasValue))
            {
                var poItem = po.Items.FirstOrDefault(p => p.Id == invoiceLine.PurchaseOrderItemId);
                if (poItem == null)
                {
                    result.Discrepancies.Add(new MatchingDiscrepancyDto
                    {
                        ItemDescription = invoiceLine.Description,
                        DiscrepancyType = "Missing",
                        InvoiceValue = invoiceLine.Quantity * invoiceLine.UnitPrice
                    });
                    result.IsMatched = false;
                    result.MatchingStatus = InvoiceMatchingStatus.MatchException;
                    continue;
                }

                // Price variance check
                if (poItem.UnitPrice > 0)
                {
                    var priceVariance = Math.Abs(invoiceLine.UnitPrice - poItem.UnitPrice);
                    var priceVariancePct = priceVariance / poItem.UnitPrice * 100;
                    if (priceVariancePct > result.PriceTolerancePercentage)
                    {
                        result.Discrepancies.Add(new MatchingDiscrepancyDto
                        {
                            ItemDescription = invoiceLine.Description,
                            DiscrepancyType = "Price",
                            InvoiceValue = invoiceLine.UnitPrice,
                            ExpectedValue = poItem.UnitPrice,
                            Variance = priceVariance,
                            VariancePercentage = priceVariancePct
                        });
                        result.IsMatched = false;
                        result.MatchingStatus = InvoiceMatchingStatus.MatchException;
                    }
                }

                // Quantity variance check
                if (poItem.OrderedQuantity > 0)
                {
                    var qtyVariance = Math.Abs(invoiceLine.Quantity - poItem.OrderedQuantity);
                    var qtyVariancePct = qtyVariance / poItem.OrderedQuantity * 100;
                    if (qtyVariancePct > result.QuantityTolerancePercentage)
                    {
                        result.Discrepancies.Add(new MatchingDiscrepancyDto
                        {
                            ItemDescription = invoiceLine.Description,
                            DiscrepancyType = "Quantity",
                            InvoiceValue = invoiceLine.Quantity,
                            ExpectedValue = poItem.OrderedQuantity,
                            Variance = qtyVariance,
                            VariancePercentage = qtyVariancePct
                        });
                        result.IsMatched = false;
                        result.MatchingStatus = InvoiceMatchingStatus.MatchException;
                    }
                }
            }

            // Persist matching result
            InvalidateMatching(invoice, "Two-way matching cannot satisfy the mandatory PO/receipt/invoice approval gate.");
            invoice.MatchingStatus = result.MatchingStatus;
            invoice.MatchingNotes = result.IsMatched
                ? "2-way match passed"
                : $"2-way match failed — {result.Discrepancies.Count} discrepancies found";
            invoice.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("2-way matching for invoice {InvoiceNumber}: {Status}",
                invoice.InvoiceNumber, result.MatchingStatus);

            return result;
        }

        public async Task<InvoiceMatchingResultDto> PerformThreeWayMatchAsync(Guid invoiceId, CancellationToken cancellationToken = default)
            => await EvaluateThreeWayMatchAsync(
                invoiceId, "ManualEvaluation", requireApprovalReady: false, cancellationToken);

        public async Task<InvoiceMatchingResultDto> GetThreeWayMatchReadinessAsync(
            Guid invoiceId,
            CancellationToken cancellationToken = default) =>
            await EvaluateThreeWayMatchAsync(
                invoiceId, "Readiness", requireApprovalReady: false, cancellationToken, persist: false);

        public async Task<InvoiceMatchingResultDto> GetMatchingResultAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == invoiceId)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{invoiceId}' not found.");

            return new InvoiceMatchingResultDto
            {
                VendorInvoiceId = invoiceId,
                MatchingType = invoice.MatchingType,
                MatchingStatus = invoice.MatchingStatus,
                IsMatched = invoice.MatchingStatus == InvoiceMatchingStatus.TwoWayMatched
                         || invoice.MatchingStatus == InvoiceMatchingStatus.ThreeWayMatched,
                InvoiceTotal = invoice.TotalAmount,
                IsRequired = ProcurementInvoiceThreeWayMatchRules.IsRequired(
                    invoice.PurchaseOrderId, invoice.IsOpeningBalance),
                ApprovalReady = invoice.MatchingControlEventId.HasValue &&
                    (invoice.MatchingStatus == InvoiceMatchingStatus.ThreeWayMatched ||
                     invoice.MatchExceptionControlEventId.HasValue),
                MatchingControlEventId = invoice.MatchingControlEventId,
                MatchExceptionControlEventId = invoice.MatchExceptionControlEventId,
                SnapshotHash = invoice.MatchingSnapshotHash,
                EvaluatedAtUtc = invoice.MatchingEvaluatedAtUtc,
                PriceTolerancePercentage = invoice.MatchingPriceTolerancePercent,
                QuantityTolerancePercentage = invoice.MatchingQuantityTolerancePercent,
                TolerancePercentage = invoice.MatchingPriceTolerancePercent,
                Message = invoice.MatchingNotes ?? string.Empty,
                DecisionKeys = ProcurementInvoiceThreeWayMatchRules.DecisionKeys.ToList()
            };
        }

        private async Task<InvoiceMatchingResultDto> EvaluateThreeWayMatchAsync(
            Guid invoiceId,
            string action,
            bool requireApprovalReady,
            CancellationToken cancellationToken,
            bool persist = true)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == invoiceId && !item.IsDeleted)
                .Include(item => item.LineItems)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Vendor invoice with Id '{invoiceId}' not found.");

            var result = new InvoiceMatchingResultDto
            {
                VendorInvoiceId = invoice.Id,
                MatchingType = InvoiceMatchingType.ThreeWay,
                MatchingStatus = InvoiceMatchingStatus.Unmatched,
                InvoiceTotal = invoice.TotalAmount,
                IsRequired = ProcurementInvoiceThreeWayMatchRules.IsRequired(
                    invoice.PurchaseOrderId, invoice.IsOpeningBalance),
                DecisionKeys = ProcurementInvoiceThreeWayMatchRules.DecisionKeys.ToList()
            };

            if (!result.IsRequired)
            {
                result.ApprovalReady = true;
                result.IsMatched = true;
                result.Message = invoice.IsOpeningBalance
                    ? "Opening-balance invoices use the controlled migration approval path and do not require PO three-way matching."
                    : "This invoice is not linked to a purchase order; TDC-0504 three-way matching is not applicable.";
                result.Checks.Add(Check("AP-MATCH-SCOPE", "Three-way matching applicability", true, false, result.Message));
                return result;
            }

            var hardStops = new List<(string Code, string Message)>();
            void AddHardStop(string code, string label, string message)
            {
                hardStops.Add((code, message));
                result.Checks.Add(Check(code, label, false, false, message));
            }
            void AddVariance(string code, string label, string description, string kind,
                decimal actual, decimal expected, decimal variance, decimal variancePercent)
            {
                result.Discrepancies.Add(new MatchingDiscrepancyDto
                {
                    ItemDescription = description,
                    DiscrepancyType = kind,
                    InvoiceValue = actual,
                    ExpectedValue = expected,
                    Variance = variance,
                    VariancePercentage = variancePercent,
                    ExceptionEligible = true
                });
                result.Checks.Add(Check(code, label, false, true,
                    $"{description}: {kind.ToLowerInvariant()} variance {variancePercent:0.####}% exceeds the configured tolerance."));
            }

            var tolerancesResolved = false;
            try
            {
                var tolerances = await GetInvoiceMatchTolerancesAsync(cancellationToken);
                result.PriceTolerancePercentage = ProcurementInvoiceThreeWayMatchRules.NormalizeTolerance(
                    tolerances.PriceTolerancePercent);
                result.QuantityTolerancePercentage = ProcurementInvoiceThreeWayMatchRules.NormalizeTolerance(
                    tolerances.QuantityTolerancePercent);
                result.TolerancePercentage = result.PriceTolerancePercentage;
                tolerancesResolved = true;
                result.Checks.Add(Check("AP-MATCH-TOLERANCE", "Configured matching tolerances", true, false,
                    $"Price tolerance {result.PriceTolerancePercentage:0.##}% and quantity tolerance {result.QuantityTolerancePercentage:0.##}% are active."));
            }
            catch (InvalidOperationException exception)
            {
                AddHardStop("AP_MATCH_SETTINGS_MISSING", "Configured matching tolerances", exception.Message);
            }

            ProcurementConfigurationProfileDto? profile = null;
            if (_procurementConfiguration == null)
            {
                AddHardStop("AP_MATCH_CONFIGURATION_UNAVAILABLE", "Procurement configuration", "The procurement configuration control is not registered.");
            }
            else
            {
                profile = await ResolveProcurementProfileForMatchingAsync(
                    cancellationToken);
                if (profile == null)
                    AddHardStop("AP_MATCH_CONFIGURATION_MISSING", "Procurement configuration", "No TDC procurement configuration profile is available for invoice-match audit lineage.");
                else
                {
                    result.ConfigurationProfileCode = profile.ProfileCode;
                    result.ConfigurationProfileVersion = profile.Version;
                    result.Checks.Add(Check("AP-MATCH-CONFIGURATION", "Procurement configuration", true, false,
                        $"{profile.ProfileCode} v{profile.Version} is retained as invoice-match audit lineage."));
                }
            }

            var purchaseOrder = await _unitOfWork.Repository<PurchaseOrder>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id == invoice.PurchaseOrderId && !item.IsDeleted)
                .Include(item => item.BusinessPartner)
                .Include(item => item.Items)
                .SingleOrDefaultAsync(cancellationToken);
            if (purchaseOrder == null)
            {
                AddHardStop("AP_MATCH_PO_NOT_FOUND", "Purchase order", "The linked purchase order was not found in the current tenant.");
                return await FinalizeThreeWayEvaluationAsync(invoice, null, profile, action, result, hardStops,
                    Array.Empty<object>(), requireApprovalReady, persist, cancellationToken);
            }

            result.PurchaseOrderTotal = purchaseOrder.TotalAmount;
            var supplier = await _unitOfWork.Repository<Supplier>()
                .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == invoice.SupplierId && !item.IsDeleted);
            var supplierMatches = supplier != null &&
                (supplier.Id == purchaseOrder.BusinessPartnerId ||
                 string.Equals(supplier.SupplierCode, purchaseOrder.BusinessPartner.PartnerCode, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(supplier.Name, purchaseOrder.BusinessPartner.PartnerName, StringComparison.OrdinalIgnoreCase));
            if (!supplierMatches)
                AddHardStop("AP_MATCH_SUPPLIER_MISMATCH", "Supplier identity", "The invoice supplier does not match the linked purchase-order supplier.");
            else
                result.Checks.Add(Check("AP-MATCH-SUPPLIER", "Supplier identity", true, false, "Invoice and purchase-order supplier lineage match."));

            if (!string.Equals(invoice.CurrencyCode, purchaseOrder.Currency, StringComparison.OrdinalIgnoreCase))
                AddHardStop("AP_MATCH_CURRENCY_MISMATCH", "Currency", "Invoice and purchase-order currencies do not match.");
            else
                result.Checks.Add(Check("AP-MATCH-CURRENCY", "Currency", true, false, $"Currency {invoice.CurrencyCode} matches."));

            var activeLines = invoice.LineItems.Where(item => !item.IsDeleted).ToList();
            if (activeLines.Count == 0)
                AddHardStop("AP_MATCH_LINES_MISSING", "Invoice lines", "The invoice has no active lines to match.");

            ProcurementAcceptedSupplyResolutionDto? acceptedSupply = null;
            var acceptedByPoLine = new Dictionary<Guid, decimal>();
            if (!purchaseOrder.ProcurementCategory.HasValue)
            {
                AddHardStop("AP_MATCH_CATEGORY_MISSING", "Procurement category",
                    "The purchase order has no governed category snapshot from its approved requisition.");
            }
            else if (purchaseOrder.ProcurementCategory == ProcurementCategoryClass.Works)
            {
                AddHardStop("AP_MATCH_WORKS_QS_HANDOFF_REQUIRED", "Works certification",
                    "Works invoices must originate from the approved QS payment-certificate handoff.");
            }
            else if (_acceptedSupply == null)
            {
                AddHardStop("AP_MATCH_ACCEPTED_SUPPLY_UNAVAILABLE", "Accepted supply",
                    "The shared category-aware accepted-supply control is not registered.");
            }
            else
            {
                try
                {
                    var kind = purchaseOrder.ProcurementCategory == ProcurementCategoryClass.Goods
                        ? ProcurementAcceptedSupplyKind.GoodsReceiptInspection
                        : ProcurementAcceptedSupplyKind.ServiceCompletion;
                    var sourceId = kind == ProcurementAcceptedSupplyKind.GoodsReceiptInspection
                        ? purchaseOrder.Id
                        : invoice.AcceptedSupplyKind == kind && invoice.AcceptedSupplySourceId.HasValue
                            ? invoice.AcceptedSupplySourceId.Value
                            : Guid.Empty;
                    acceptedSupply = await _acceptedSupply.ResolveAsync(
                        kind, sourceId, purchaseOrder.Id, invoice.Id, cancellationToken);
                    acceptedByPoLine = acceptedSupply.Lines
                        .ToDictionary(value => value.PurchaseOrderItemId,
                            value => value.AcceptedQuantity);
                    result.Checks.Add(Check(
                        kind == ProcurementAcceptedSupplyKind.GoodsReceiptInspection
                            ? "AP-MATCH-GOODS-ACCEPTANCE"
                            : "AP-MATCH-SERVICE-COMPLETION",
                        kind == ProcurementAcceptedSupplyKind.GoodsReceiptInspection
                            ? "Accepted GRN inspection"
                            : "Approved service completion",
                        true,
                        false,
                        $"Authoritative {acceptedSupply.SourceReference} acceptance lineage is current."));
                    if (persist)
                        ApplyAcceptedSupply(invoice, acceptedSupply);
                }
                catch (ProcurementAcceptedSupplyValidationException exception)
                {
                    AddHardStop(exception.Code, "Accepted supply", exception.Message);
                }
            }
            result.GoodsReceiptTotal = acceptedByPoLine.Values.Sum();

            var committedStatuses = new[]
            {
                VendorInvoiceStatus.PendingApproval, VendorInvoiceStatus.Approved,
                VendorInvoiceStatus.PartiallyPaid, VendorInvoiceStatus.Paid,
                VendorInvoiceStatus.Overdue, VendorInvoiceStatus.OnHold
            };
            var priorInvoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(item => item.TenantId == TenantId && item.Id != invoice.Id &&
                                      item.PurchaseOrderId == purchaseOrder.Id && !item.IsDeleted &&
                                      committedStatuses.Contains(item.Status))
                .Include(item => item.LineItems)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var priorByPoLine = priorInvoices.SelectMany(item => item.LineItems.Where(line => !line.IsDeleted))
                .Where(item => item.PurchaseOrderItemId.HasValue)
                .GroupBy(item => item.PurchaseOrderItemId!.Value)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

            foreach (var group in activeLines.GroupBy(item => item.PurchaseOrderItemId))
            {
                if (!group.Key.HasValue)
                {
                    AddHardStop("AP_MATCH_PO_LINE_MISSING", "PO line mapping", "Every line on a PO-linked invoice must reference an exact purchase-order line.");
                    continue;
                }

                var poLine = purchaseOrder.Items.SingleOrDefault(item => item.Id == group.Key.Value && !item.IsDeleted);
                if (poLine == null)
                {
                    AddHardStop("AP_MATCH_PO_LINE_INVALID", "PO line mapping", "An invoice line references a foreign or missing purchase-order line.");
                    continue;
                }

                foreach (var invoiceLine in group)
                {
                    var priceVariance = Math.Abs(invoiceLine.UnitPrice - poLine.UnitPrice);
                    var priceVariancePercent = ProcurementInvoiceThreeWayMatchRules.PercentageVariance(
                        invoiceLine.UnitPrice, poLine.UnitPrice);
                    if (tolerancesResolved && !ProcurementInvoiceThreeWayMatchRules.IsPriceWithinTolerance(
                            invoiceLine.UnitPrice, poLine.UnitPrice, result.PriceTolerancePercentage))
                        AddVariance("AP-MATCH-PRICE", "PO unit price", invoiceLine.Description, "Price",
                            invoiceLine.UnitPrice, poLine.UnitPrice, priceVariance, priceVariancePercent);
                }

                var currentQuantity = group.Sum(item => item.Quantity);
                priorByPoLine.TryGetValue(poLine.Id, out var priorQuantity);
                acceptedByPoLine.TryGetValue(poLine.Id, out var acceptedQuantity);
                var cumulativeQuantity = priorQuantity + currentQuantity;
                if (acceptedQuantity <= 0m)
                {
                    AddHardStop("AP_MATCH_ACCEPTED_QUANTITY_MISSING", "Accepted quantity",
                        $"No independently accepted quantity exists for PO line '{poLine.ItemDescription ?? poLine.Id.ToString()}'.");
                    continue;
                }

                if (tolerancesResolved && !ProcurementInvoiceThreeWayMatchRules.IsCumulativeQuantityWithinTolerance(
                        cumulativeQuantity, acceptedQuantity, result.QuantityTolerancePercentage))
                {
                    var variance = cumulativeQuantity - acceptedQuantity;
                    var variancePercent = ProcurementInvoiceThreeWayMatchRules.PercentageVariance(
                        cumulativeQuantity, acceptedQuantity);
                    AddVariance("AP-MATCH-QUANTITY", "Accepted cumulative quantity",
                        poLine.ItemDescription ?? poLine.Id.ToString(), "Quantity",
                        cumulativeQuantity, acceptedQuantity, variance, variancePercent);
                }
            }

            if (hardStops.Count == 0 && result.Discrepancies.Count == 0)
            {
                result.Checks.Add(Check("AP-MATCH-THREE-WAY", "Three-way result", true, false,
                    "Invoice price and cumulative quantity are within configured PO and accepted-receipt tolerances."));
            }

            var snapshot = new
            {
                schemaVersion = "tdc.ap-three-way-match.v1",
                invoice = new
                {
                    invoice.Id, invoice.PurchaseOrderId, invoice.SupplierId, invoice.CurrencyCode,
                    invoice.SubTotal, invoice.TaxAmount, invoice.DiscountAmount, invoice.TotalAmount,
                    Lines = activeLines.OrderBy(item => item.Id).Select(item => new
                    {
                        item.Id, item.PurchaseOrderItemId, item.Quantity, item.UnitPrice,
                        item.DiscountAmount, item.TaxAmount
                    })
                },
                purchaseOrder = new
                {
                    purchaseOrder.Id, purchaseOrder.BusinessPartnerId, purchaseOrder.Currency,
                    purchaseOrder.TotalAmount, purchaseOrder.SourceIntegrityHash,
                    Lines = purchaseOrder.Items.Where(item => !item.IsDeleted).OrderBy(item => item.Id)
                        .Select(item => new { item.Id, item.OrderedQuantity, item.UnitPrice })
                },
                acceptedByPoLine = acceptedByPoLine.OrderBy(item => item.Key),
                acceptedSupply = acceptedSupply == null ? null : new
                {
                    acceptedSupply.Kind, acceptedSupply.SourceId,
                    acceptedSupply.SourceReference, acceptedSupply.Category,
                    acceptedSupply.PurchaseOrderId, acceptedSupply.BusinessPartnerId,
                    acceptedSupply.CurrencyCode, acceptedSupply.AcceptedAmount,
                    acceptedSupply.AcceptedAtUtc, acceptedSupply.SourceIntegrityHash
                },
                priorByPoLine = priorByPoLine.OrderBy(item => item.Key),
                tolerances = new { result.PriceTolerancePercentage, result.QuantityTolerancePercentage },
                profile = profile == null ? null : new
                {
                    profile.Id, profile.ProfileCode, profile.Version,
                    Decisions = profile.Decisions.OrderBy(item => item.DecisionKey)
                        .Select(item => new { item.DecisionKey, ValueHash = ProcurementInvoiceThreeWayMatchRules.HashSnapshot(item.Value.GetRawText()) })
                }
            };

            return await FinalizeThreeWayEvaluationAsync(invoice, purchaseOrder, profile, action, result,
                hardStops, new object[] { snapshot }, requireApprovalReady, persist, cancellationToken);
        }

        private async Task<InvoiceMatchingResultDto> FinalizeThreeWayEvaluationAsync(
            VendorInvoice invoice,
            PurchaseOrder? purchaseOrder,
            ProcurementConfigurationProfileDto? profile,
            string action,
            InvoiceMatchingResultDto result,
            IReadOnlyCollection<(string Code, string Message)> hardStops,
            IReadOnlyCollection<object> snapshotParts,
            bool requireApprovalReady,
            bool persist,
            CancellationToken cancellationToken)
        {
            var snapshotHash = ProcurementInvoiceThreeWayMatchRules.HashSnapshot(new
            {
                invoice.Id,
                invoice.PurchaseOrderId,
                snapshotParts,
                HardStops = hardStops,
                result.Discrepancies
            });
            result.SnapshotHash = snapshotHash;
            result.EvaluatedAtUtc = DateTime.UtcNow;
            result.IsMatched = hardStops.Count == 0 && result.Discrepancies.Count == 0;
            result.MatchingStatus = result.IsMatched
                ? InvoiceMatchingStatus.ThreeWayMatched
                : InvoiceMatchingStatus.MatchException;

            ProcurementControlEvent? approvedException = null;
            if (!result.IsMatched && hardStops.Count == 0 && purchaseOrder != null)
                approvedException = await FindApprovedMatchExceptionAsync(
                    invoice, purchaseOrder, snapshotHash, cancellationToken);
            result.ApprovedExceptionApplied = approvedException != null;
            result.MatchExceptionControlEventId = approvedException?.Id;
            result.ApprovalReady = result.IsMatched || result.ApprovedExceptionApplied;

            if (_procurementControlEvents == null)
            {
                result.ApprovalReady = false;
                result.Checks.Add(Check("AP_MATCH_AUDIT_UNAVAILABLE", "Immutable decision audit", false, false,
                    "The procurement control-event service is not registered."));
            }

            result.Message = result.ApprovalReady
                ? result.IsMatched
                    ? "Mandatory three-way matching passed."
                    : "Three-way variances are covered by a current independently approved exception."
                : hardStops.FirstOrDefault().Message ??
                  $"Mandatory three-way matching failed with {result.Discrepancies.Count} tolerance variance(s).";

            if (persist && _procurementControlEvents != null)
            {
                var correlation = $"tdc0504-{invoice.Id:N}-{Guid.NewGuid():N}";
                var controlEvent = await _procurementControlEvents.RecordAsync(
                    new ProcurementControlEventWriteRequest
                    {
                        EventKey = ProcurementControlEventKey.Create(
                            "ap-three-way-match", TenantId, invoice.Id, action, correlation),
                        EventType = "InvoiceThreeWayMatching",
                        Action = ProcurementInvoiceThreeWayMatchRules.EvaluationAction,
                        Result = result.ApprovalReady
                            ? ProcurementControlEventResult.Allowed
                            : ProcurementControlEventResult.Denied,
                        RuleCode = ProcurementInvoiceThreeWayMatchRules.RuleCode,
                        RuleVersion = ProcurementInvoiceThreeWayMatchRules.RuleVersion,
                        DecisionKeys = ProcurementInvoiceThreeWayMatchRules.DecisionKeys.ToList(),
                        SourceType = "VendorInvoice",
                        SourceId = invoice.Id,
                        SourceReference = invoice.InvoiceNumber,
                        Reason = result.Message,
                        InputValues = new
                        {
                            action,
                            invoiceId = invoice.Id,
                            purchaseOrderId = invoice.PurchaseOrderId,
                            profile = profile == null ? null : new { profile.Id, profile.ProfileCode, profile.Version }
                        },
                        ResultValues = new
                        {
                            approvalReady = result.ApprovalReady,
                            matched = result.IsMatched,
                            approvedExceptionApplied = result.ApprovedExceptionApplied,
                            snapshotHash,
                            priceTolerancePercent = result.PriceTolerancePercentage,
                            quantityTolerancePercent = result.QuantityTolerancePercentage,
                            hardStops,
                            result.Discrepancies
                        },
                        CorrelationId = correlation,
                        OccurredAtUtc = result.EvaluatedAtUtc.Value,
                        Evidence = approvedException == null
                            ? new List<ProcurementControlEventEvidenceReference>()
                            : new List<ProcurementControlEventEvidenceReference>
                            {
                                new()
                                {
                                    ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                                    Reference = approvedException.Id.ToString(),
                                    Label = "Approved invoice-match exception control event",
                                    RequirementKey = ProcurementInvoiceThreeWayMatchRules.ExceptionRuleCode
                                }
                            }
                    }, cancellationToken);
                result.MatchingControlEventId = controlEvent.Id;

                invoice.MatchingType = InvoiceMatchingType.ThreeWay;
                invoice.MatchingStatus = result.MatchingStatus;
                invoice.MatchingNotes = result.Message;
                invoice.MatchingControlEventId = controlEvent.Id;
                invoice.MatchExceptionControlEventId = approvedException?.Id;
                invoice.MatchingSnapshotHash = snapshotHash;
                invoice.MatchingEvaluatedAtUtc = result.EvaluatedAtUtc;
                invoice.MatchingPriceTolerancePercent = result.PriceTolerancePercentage;
                invoice.MatchingQuantityTolerancePercent = result.QuantityTolerancePercentage;
                invoice.UpdatedAt = DateTime.UtcNow;
                invoice.UpdatedBy = UserName;
                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (requireApprovalReady && !result.ApprovalReady)
            {
                var failure = hardStops.FirstOrDefault();
                throw new VendorInvoiceMatchControlException(
                    string.IsNullOrWhiteSpace(failure.Code) ? "AP_THREE_WAY_MATCH_BLOCKED" : failure.Code,
                    result.Message);
            }
            return result;
        }

        private async Task<ProcurementControlEvent?> FindApprovedMatchExceptionAsync(
            VendorInvoice invoice,
            PurchaseOrder purchaseOrder,
            string snapshotHash,
            CancellationToken cancellationToken)
        {
            var candidates = await _unitOfWork.Repository<ProcurementControlEvent>()
                .GetQueryable(item => item.TenantId == TenantId && item.SourceId == invoice.Id &&
                                      item.SourceType == "VendorInvoice" &&
                                      item.Action == ProcurementInvoiceThreeWayMatchRules.ExceptionApprovalAction &&
                                      item.RuleCode == ProcurementInvoiceThreeWayMatchRules.ExceptionRuleCode &&
                                      item.RuleVersion == ProcurementInvoiceThreeWayMatchRules.ExceptionRuleVersion &&
                                      (item.Result == ProcurementControlEventResult.Allowed ||
                                       item.Result == ProcurementControlEventResult.Succeeded) && !item.IsDeleted)
                .Include(item => item.EvidenceLinks)
                .OrderByDescending(item => item.OccurredAtUtc)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                if (candidate.EvidenceLinks.Count == 0 ||
                    invoice.SubmittedById.HasValue && candidate.ActorUserId == invoice.SubmittedById.Value ||
                    string.IsNullOrWhiteSpace(candidate.ResultValuesJson))
                    continue;
                try
                {
                    using var json = JsonDocument.Parse(candidate.ResultValuesJson);
                    var root = json.RootElement;
                    if (!TryGuid(root, "purchaseOrderId", out var purchaseOrderId) ||
                        purchaseOrderId != purchaseOrder.Id ||
                        !TryGuid(root, "workflowInstanceId", out var workflowInstanceId) ||
                        !TryGuid(root, "exceptionId", out var exceptionId) ||
                        !root.TryGetProperty("invoiceSnapshotHash", out var hashElement) ||
                        !string.Equals(hashElement.GetString(), snapshotHash, StringComparison.OrdinalIgnoreCase) ||
                        !root.TryGetProperty("expiresAtUtc", out var expiryElement) ||
                        !expiryElement.TryGetDateTime(out var expiresAtUtc) || expiresAtUtc <= DateTime.UtcNow)
                        continue;

                    var workflow = await _unitOfWork.Repository<WorkflowInstance>()
                        .FirstOrDefaultAsync(item => item.TenantId == TenantId &&
                            item.Id == workflowInstanceId && item.Status == WorkflowInstanceStatus.Completed &&
                            !item.IsDeleted);
                    if (workflow == null || workflow.InitiatedById == candidate.ActorUserId)
                        continue;

                    var approvedException = await _unitOfWork
                        .Repository<VendorInvoiceMatchException>()
                        .GetQueryable(item =>
                            item.TenantId == TenantId &&
                            item.Id == exceptionId &&
                            item.VendorInvoiceId == invoice.Id &&
                            item.PurchaseOrderId == purchaseOrder.Id &&
                            item.Status == VendorInvoiceMatchExceptionStatus.Approved &&
                            item.ApprovalControlEventId == candidate.Id &&
                            !item.IsDeleted)
                        .Include(item => item.Evidence)
                        .AsNoTracking()
                        .SingleOrDefaultAsync(cancellationToken);
                    if (approvedException == null || approvedException.Evidence.Count == 0)
                        continue;
                    try
                    {
                        await VendorInvoiceMatchExceptionEvidenceValidator.RevalidateAsync(
                            _unitOfWork, TenantId, invoice,
                            approvedException.Evidence, cancellationToken);
                    }
                    catch (VendorInvoiceMatchExceptionControlException exception)
                        when (exception.Code == "AP_MATCH_EXCEPTION_EVIDENCE_STALE")
                    {
                        // Evidence can be revoked after approval. A stale exception is
                        // ignored so the current match remains visibly blocked.
                        continue;
                    }
                    return candidate;
                }
                catch (JsonException)
                {
                    // An unparseable exception event is untrusted and deliberately ignored.
                }
            }
            return null;
        }

        private static bool TryGuid(JsonElement root, string property, out Guid value)
        {
            value = Guid.Empty;
            return root.TryGetProperty(property, out var element) &&
                   Guid.TryParse(element.GetString(), out value);
        }

        private static InvoiceMatchingCheckDto Check(
            string key, string label, bool passed, bool exceptionEligible, string message) => new()
        {
            CheckKey = key,
            Label = label,
            Passed = passed,
            ExceptionEligible = exceptionEligible,
            Message = message
        };

        private static void InvalidateMatching(VendorInvoice invoice, string reason)
        {
            invoice.MatchingStatus = InvoiceMatchingStatus.Unmatched;
            invoice.MatchingNotes = reason;
            invoice.MatchingControlEventId = null;
            invoice.MatchExceptionControlEventId = null;
            invoice.MatchingSnapshotHash = null;
            invoice.MatchingEvaluatedAtUtc = null;
            invoice.MatchingPriceTolerancePercent = 0m;
            invoice.MatchingQuantityTolerancePercent = 0m;
        }

        // ═════════════════════════════════════════════════════════════════
        //  UTILITIES
        // ═════════════════════════════════════════════════════════════════

        private async Task<IReadOnlyList<FinanceBudgetReservationDto>> ReserveVendorInvoiceBudgetAsync(
            Guid invoiceId,
            string operation,
            CancellationToken cancellationToken)
        {
            var invoice = await LoadInvoiceForPostingAsync(invoiceId, cancellationToken);
            var request = await BuildVendorInvoiceBudgetRequestAsync(
                invoice, operation, useSourceDimensions: false, cancellationToken);
            if (request is null)
                return Array.Empty<FinanceBudgetReservationDto>();
            return (await _budgetCommitments!.ReserveAsync(request, cancellationToken)).Reservations;
        }

        public async Task<FinanceSourceDocumentDimensionDto> RefreshBudgetAsync(
            Guid id,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken = default)
        {
            EnsureVendorInvoiceRoute(producer);
            if (_sourceDimensions is null)
                throw new InvalidOperationException("Finance source dimensions are not configured for the manual AP invoice route.");
            if (_budgetCommitments is null)
                throw new InvalidOperationException("Finance budget commitments are not configured for this AP invoice.");

            return await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"finance-ap-budget:{TenantId:N}:{id:N}", cancellationToken);
                    var invoice = await GetEntityOrThrowAsync(id, cancellationToken);
                    if (invoice.Status != VendorInvoiceStatus.Draft)
                        throw new InvalidOperationException("Budget evidence can be refreshed only while the vendor invoice is Draft.");
                    invoice = await LoadInvoiceForPostingAsync(invoice.Id, cancellationToken);
                    var request = await BuildVendorInvoiceBudgetRequestAsync(
                        invoice, "Refresh", useSourceDimensions: true, cancellationToken);
                    if (request is null)
                    {
                        var current = await _sourceDimensions.GetAsync(
                            producer, invoice.Id, invoice.InvoiceDate,
                            await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                            cancellationToken);
                        await _unitOfWork.CommitAsync(cancellationToken);
                        return current;
                    }

                    var result = await _budgetCommitments.ReserveAsync(request, cancellationToken);
                    await _sourceDimensions.MarkBudgetEvidenceCurrentAsync(
                        producer, invoice.Id, result.EvaluationHash, cancellationToken);
                    var dimensions = await _sourceDimensions.GetAsync(
                        producer, invoice.Id, invoice.InvoiceDate,
                        await BuildDimensionLineContextsAsync(invoice, cancellationToken),
                        cancellationToken);
                    await _unitOfWork.CommitAsync(cancellationToken);
                    return dimensions;
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

        private async Task<IReadOnlyList<FinanceBudgetReservationDto>> RequireCurrentVendorInvoiceBudgetAsync(
            FinanceBudgetCommitmentRequestDto request,
            CancellationToken cancellationToken)
        {
            var rows = await _unitOfWork.Repository<FinanceBudgetReservation>()
                .GetQueryable(row => row.TenantId == TenantId && !row.IsDeleted
                    && row.SourceDocumentType == VendorInvoiceBudgetSource
                    && row.SourceDocumentId == request.SourceDocumentId
                    && row.Status == "Reserved")
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            if (rows.Count == 0)
                throw new InvalidOperationException(
                    "Finance budget evidence has not been refreshed for this vendor invoice. Run Budget refresh before submission.");
            if (rows.Any(row => row.SourceVersion != request.SourceVersion)
                || rows.Select(row => row.EvaluationHash).Distinct().Count() != 1)
                throw new InvalidOperationException(
                    "Finance budget evidence is stale for the current vendor invoice lines and dimensions. Run Budget refresh again.");
            var expectedCells = request.Lines.Select(line => line.BudgetEntryId).Distinct().OrderBy(id => id).ToArray();
            var reservedCells = rows.Select(line => line.BudgetEntryId).Distinct().OrderBy(id => id).ToArray();
            if (!expectedCells.SequenceEqual(reservedCells))
                throw new InvalidOperationException(
                    "Finance budget reservations do not match the current vendor invoice budget cells.");
            return rows.Select(row => new FinanceBudgetReservationDto
            {
                Id = row.Id,
                Version = row.ReservationVersion,
                Status = row.Status
            }).ToArray();
        }

        private async Task<IReadOnlyList<Guid>> PrepareVendorInvoiceBudgetForPostingAsync(
            VendorInvoice invoice,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var request = await BuildVendorInvoiceBudgetRequestAsync(
                invoice, "Post", useSourceDimensions: producer is not null, cancellationToken);
            if (request is null)
                return Array.Empty<Guid>();

            // The posting event is the durable accounting truth. A journal may already be
            // committed even when the repairable invoice back-reference failed to save. Check
            // that evidence before reserving so a retry cannot leak a second commitment and then
            // short-circuit as a duplicate in the posting engine.
            var durablePosting = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(posting => posting.TenantId == TenantId && !posting.IsDeleted
                    && posting.SourceModule == "AP"
                    && posting.SourceDocumentType == VendorInvoiceBudgetSource
                    && posting.SourceDocumentId == invoice.Id
                    && posting.PostingAction == "Post"
                    && posting.PostingStatus == "Posted")
                .AsNoTracking()
                .Select(posting => new { posting.Id, posting.JournalEntryId })
                .SingleOrDefaultAsync(cancellationToken);
            if (durablePosting != null)
            {
                var expectedBudgetEntryIds = request.Lines
                    .Select(line => line.BudgetEntryId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray();
                var consumed = await _unitOfWork.Repository<FinanceBudgetReservation>()
                    .GetQueryable(row => row.TenantId == TenantId && !row.IsDeleted
                        && row.SourceDocumentType == VendorInvoiceBudgetSource
                        && row.SourceDocumentId == invoice.Id
                        && row.Status == "Consumed"
                        && row.JournalEntryId == durablePosting.JournalEntryId
                        && row.PostingEventId == durablePosting.Id)
                    .AsNoTracking()
                    .Select(row => row.BudgetEntryId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArrayAsync(cancellationToken);
                if (!consumed.SequenceEqual(expectedBudgetEntryIds))
                    throw new InvalidOperationException(
                        "The posted AP invoice is missing its consumed Finance budget evidence.");
                return Array.Empty<Guid>();
            }

            if (invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException(
                    "The AP invoice journal back-reference has no matching posted Finance event.");

            if (producer is not null)
            {
                var current = await RequireCurrentVendorInvoiceBudgetAsync(request, cancellationToken);
                return current.Select(row => row.Id).ToArray();
            }

            var result = await _budgetCommitments!.ReserveAsync(request, cancellationToken);
            return result.Reservations.Select(row => row.Id).ToArray();
        }

        private async Task<FinanceBudgetCommitmentRequestDto?> BuildVendorInvoiceBudgetRequestAsync(
            VendorInvoice invoice,
            string operation,
            bool useSourceDimensions,
            CancellationToken cancellationToken)
        {
            var activeLines = invoice.LineItems.Where(line => !line.IsDeleted).ToList();
            var hasBudgetSelections = activeLines.Any(line => line.BudgetEntryId.HasValue);
            var procurementOwned = invoice.PurchaseOrderId.HasValue
                || await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                    .GetQueryable(receipt => receipt.TenantId == TenantId && !receipt.IsDeleted
                        && receipt.VendorInvoiceId == invoice.Id)
                    .AsNoTracking()
                    .AnyAsync(cancellationToken);
            if (invoice.IsOpeningBalance || procurementOwned)
            {
                if (hasBudgetSelections)
                    throw new InvalidOperationException(
                        "Opening and PO/GRV-backed invoices cannot create a second AP budget commitment.");
                return null;
            }

            var supplier = invoice.Supplier ?? await ResolveInvoiceSupplierForPostingAsync(invoice, cancellationToken);
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var accountCache = new Dictionary<Guid, Account>();
            var candidateLines = new List<(VendorInvoiceLineItem Line, Account Account)>();
            foreach (var line in activeLines)
            {
                var directExpense = !IsFixedAssetLine(line)
                    && !string.Equals(line.LineItemType, "Inventory", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(line.LineItemType, "Product", StringComparison.OrdinalIgnoreCase);
                if (!directExpense)
                {
                    if (line.BudgetEntryId.HasValue)
                        throw new InvalidOperationException(
                            $"AP line '{line.Description}' is not a direct expense and cannot select a Finance budget cell.");
                    continue;
                }

                var accountId = await ResolveDebitAccountForInvoiceLineAsync(
                    invoice, supplier, settings, line, accountCache, cancellationToken);
                var account = accountCache[accountId];
                if (account.AccountType == AccountType.Expense && account.BudgetTrackingEnabled)
                {
                    if (!line.BudgetEntryId.HasValue)
                        throw new InvalidOperationException(
                            $"AP expense line '{line.Description}' requires an adopted Finance budget cell before submission.");
                    candidateLines.Add((line, account));
                }
                else if (line.BudgetEntryId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"AP line '{line.Description}' selected a budget cell for an account that is not budget-controlled.");
                }
            }

            if (candidateLines.Count == 0)
                return null;
            if (_budgetCommitments == null)
                throw new InvalidOperationException(
                    "Finance budget commitments are not configured for this budget-controlled AP invoice.");

            var entryIds = candidateLines.Select(item => item.Line.BudgetEntryId!.Value).Distinct().ToArray();
            var entries = await _unitOfWork.Repository<BudgetEntry>()
                .GetQueryable(entry => entry.TenantId == TenantId && entryIds.Contains(entry.Id) && !entry.IsDeleted)
                .Include(entry => entry.BudgetReturn)
                .Include(entry => entry.FinanceDimensionSet)
                    .ThenInclude(set => set!.Items)
                .ToDictionaryAsync(entry => entry.Id, cancellationToken);
            if (entries.Count != entryIds.Length)
                throw new InvalidOperationException("One or more selected Finance budget cells were not found in this tenant.");

            var sourceAssignments = useSourceDimensions
                ? await _unitOfWork.Repository<FinanceSourceDimensionAssignment>()
                    .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted
                        && item.RouteId == FinanceDimensionRouteId.FinanceApVendorInvoice
                        && item.SourceDocumentId == invoice.Id && item.SourceLineId.HasValue)
                    .AsNoTracking()
                    .ToDictionaryAsync(item => item.SourceLineId!.Value, cancellationToken)
                : new Dictionary<Guid, FinanceSourceDimensionAssignment>();
            var sourceSetIds = sourceAssignments.Values.Where(item => item.FinanceDimensionSetId.HasValue)
                .Select(item => item.FinanceDimensionSetId!.Value).Distinct().ToArray();
            var sourceSets = sourceSetIds.Length == 0
                ? new Dictionary<Guid, FinanceDimensionSet>()
                : await _unitOfWork.Repository<FinanceDimensionSet>()
                    .GetQueryable(set => set.TenantId == TenantId && sourceSetIds.Contains(set.Id) && !set.IsDeleted)
                    .Include(set => set.Items)
                    .ToDictionaryAsync(set => set.Id, cancellationToken);
            var sourceHashes = sourceAssignments.ToDictionary(
                item => item.Key,
                item => item.Value.FinanceDimensionSetId.HasValue
                    && sourceSets.TryGetValue(item.Value.FinanceDimensionSetId.Value, out var set)
                        ? set.CombinationHash
                        : "NONE");
            var sourceVersion = BuildVendorInvoiceBudgetSourceVersion(invoice, activeLines, sourceHashes);
            return new FinanceBudgetCommitmentRequestDto
            {
                SourceDocumentType = VendorInvoiceBudgetSource,
                SourceDocumentId = invoice.Id,
                SourceDocumentReference = invoice.InvoiceNumber,
                SourceVersion = sourceVersion,
                BudgetDate = invoice.InvoiceDate.Date,
                IdempotencyKey = $"AP:{invoice.Id:N}:Budget:{operation}:{sourceVersion[..12]}",
                CorrelationId = $"AP:{invoice.Id:N}:Budget",
                Lines = candidateLines.Select(item =>
                {
                    var entry = entries[item.Line.BudgetEntryId!.Value];
                    return new FinanceBudgetCommitmentLineDto
                    {
                        SourceLineId = item.Line.Id.ToString("N"),
                        BudgetEntryId = entry.Id,
                        AccountId = item.Account.Id,
                        FiscalPeriodId = entry.FiscalPeriodId,
                        SegmentValueId = entry.BudgetReturn?.SegmentValueId,
                        DimensionAssignments = useSourceDimensions
                            ? sourceAssignments.TryGetValue(item.Line.Id, out var sourceAssignment)
                                && sourceAssignment.FinanceDimensionSetId.HasValue
                                && sourceSets.TryGetValue(sourceAssignment.FinanceDimensionSetId.Value, out var sourceSet)
                                ? sourceSet.Items
                                    .Select(dimension => new BudgetDimensionAssignmentInputDto
                                    {
                                        FinanceDimensionDefinitionId = dimension.FinanceDimensionDefinitionId,
                                        FinanceDimensionValueId = dimension.FinanceDimensionValueId
                                    }).ToArray()
                                : Array.Empty<BudgetDimensionAssignmentInputDto>()
                            : entry.FinanceDimensionSet?.Items
                                .Select(dimension => new BudgetDimensionAssignmentInputDto
                                {
                                    FinanceDimensionDefinitionId = dimension.FinanceDimensionDefinitionId,
                                    FinanceDimensionValueId = dimension.FinanceDimensionValueId
                                }).ToArray() ?? Array.Empty<BudgetDimensionAssignmentInputDto>(),
                        TransactionAmount = RoundMoney(item.Line.Quantity * item.Line.UnitPrice),
                        TransactionCurrencyCode = invoice.CurrencyCode,
                        ExchangeRateId = invoice.ExchangeRateId
                    };
                }).ToArray()
            };
        }

        private async Task ReleaseVendorInvoiceBudgetAsync(
            Guid invoiceId,
            IReadOnlyList<FinanceBudgetReservationDto>? reservations,
            string reason,
            string operation,
            CancellationToken cancellationToken)
        {
            if (_budgetCommitments == null)
                return;
            var active = reservations ?? await _unitOfWork.Repository<FinanceBudgetReservation>()
                .GetQueryable(row => row.TenantId == TenantId && !row.IsDeleted
                    && row.SourceDocumentType == VendorInvoiceBudgetSource
                    && row.SourceDocumentId == invoiceId && row.Status == "Reserved")
                .AsNoTracking()
                .Select(row => new FinanceBudgetReservationDto
                {
                    Id = row.Id,
                    Version = row.ReservationVersion
                })
                .ToListAsync(cancellationToken);
            foreach (var reservation in active)
            {
                await _budgetCommitments.ReleaseAsync(
                    reservation.Id,
                    new ReleaseFinanceBudgetReservationDto
                    {
                        Reason = reason,
                        ExpectedVersion = reservation.Version,
                        IdempotencyKey = $"AP:{invoiceId:N}:BudgetRelease:{operation}:{reservation.Id:N}",
                        CorrelationId = $"AP:{invoiceId:N}:Budget"
                    },
                    cancellationToken);
            }
        }

        private static string BuildVendorInvoiceBudgetSourceVersion(
            VendorInvoice invoice,
            IReadOnlyCollection<VendorInvoiceLineItem> lines,
            IReadOnlyDictionary<Guid, string> sourceDimensionHashes)
        {
            var payload = string.Join('|', new[]
            {
                invoice.Id.ToString("N"),
                invoice.InvoiceDate.Date.ToString("yyyy-MM-dd"),
                invoice.CurrencyCode.Trim().ToUpperInvariant(),
                invoice.ExchangeRateId?.ToString("N") ?? "NONE",
                string.Join(';', lines.OrderBy(line => line.Id).Select(line =>
                    $"{line.Id:N},{line.LineItemType},{line.GLAccountId?.ToString("N") ?? "NONE"},{line.BudgetEntryId?.ToString("N") ?? "NONE"},{line.Quantity:0.####},{line.UnitPrice:0.####},{line.DiscountAmount:0.00},{(sourceDimensionHashes.TryGetValue(line.Id, out var hash) ? hash : "NONE")}"))
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }

        private static string BuildVendorInvoiceBudgetMutationKey(VendorInvoice invoice)
        {
            var payload = string.Join('|', new[]
            {
                invoice.Id.ToString("N"),
                invoice.InvoiceDate.Date.ToString("yyyy-MM-dd"),
                invoice.CurrencyCode.Trim().ToUpperInvariant(),
                invoice.ExchangeRateId?.ToString("N") ?? "NONE",
                invoice.ExchangeRate.ToString("0.########"),
                string.Join(';', invoice.LineItems.Where(line => !line.IsDeleted)
                    .OrderBy(line => line.Id)
                    .Select(line =>
                        $"{line.Id:N},{line.LineItemType},{line.GLAccountId?.ToString("N") ?? "NONE"},{line.BudgetEntryId?.ToString("N") ?? "NONE"},{line.Quantity:0.####},{line.UnitPrice:0.####},{line.DiscountAmount:0.00}"))
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }

        private static void ApplySourceDimensions(
            FinancePostingLineDto postingLine,
            VendorInvoiceLineItem sourceLine,
            IReadOnlyDictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>> sourceDimensions)
        {
            postingLine.SourceDocumentLineId = sourceLine.Id;
            postingLine.Dimensions = sourceDimensions.TryGetValue(sourceLine.Id, out var values)
                ? values
                : Array.Empty<FinancePostingDimensionValueDto>();
        }

        private async Task<VendorInvoice> LoadInvoiceForPostingAsync(Guid id, CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == tenantId && i.Id == id && !i.IsDeleted)
                .Include(i => i.LineItems)
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            if (invoice.TenantId != tenantId)
                throw new InvalidOperationException("Vendor invoice belongs to another tenant.");

            return invoice;
        }

        private async Task<IReadOnlyList<FinanceSourceDocumentLineContext>> BuildDimensionLineContextsAsync(
            VendorInvoice invoice,
            CancellationToken cancellationToken)
        {
            var lines = invoice.LineItems.Where(line => !line.IsDeleted)
                .OrderBy(line => line.CreatedAt).ThenBy(line => line.Id).ToList();
            if (lines.Count == 0) return Array.Empty<FinanceSourceDocumentLineContext>();
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var accountCache = new Dictionary<Guid, Account>();
            Guid? commonAccountId = null;
            if (invoice.IsOpeningBalance)
                commonAccountId = settings.MigrationClearingAccountId
                    ?? throw new InvalidOperationException("Migration Clearing Account is not configured for AP opening balance dimensions.");
            else
            {
                var clearsFinanceGrv = await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                    .GetQueryable(receipt => receipt.TenantId == TenantId && !receipt.IsDeleted
                        && receipt.VendorInvoiceId == invoice.Id)
                    .AsNoTracking().AnyAsync(cancellationToken);
                if (clearsFinanceGrv || IsProcurementGrvClearingInvoice(invoice))
                    commonAccountId = settings.ControlAccountGRVAccrualId
                        ?? throw new InvalidOperationException("GRV accrual control account is not configured for AP invoice dimensions.");
            }

            if (commonAccountId.HasValue)
                return lines.Select(line => new FinanceSourceDocumentLineContext(line.Id, commonAccountId.Value)).ToArray();

            var supplier = invoice.Supplier ?? await ResolveInvoiceSupplierForPostingAsync(invoice, cancellationToken);
            var output = new List<FinanceSourceDocumentLineContext>(lines.Count);
            foreach (var line in lines)
                output.Add(new FinanceSourceDocumentLineContext(
                    line.Id,
                    await ResolveDebitAccountForInvoiceLineAsync(
                        invoice, supplier, settings, line, accountCache, cancellationToken)));
            return output;
        }

        private static FinancePostingProducerContext EnsureVendorInvoiceRoute(
            FinancePostingProducerContext producer)
        {
            ArgumentNullException.ThrowIfNull(producer);
            if (producer.RouteId != FinanceDimensionRouteId.FinanceApVendorInvoice)
                throw new InvalidOperationException("The trusted producer context is not the Finance AP vendor-invoice route.");
            return producer;
        }

        private async Task<FinancePostingRequestDto> BuildApInvoicePostingRequestAsync(
            VendorInvoice invoice,
            IReadOnlyList<Guid> budgetReservationIds,
            FinancePostingProducerContext? producer,
            CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            if (invoice.TenantId != tenantId)
                throw new InvalidOperationException("Vendor invoice belongs to another tenant.");

            if (invoice.Status != VendorInvoiceStatus.Approved &&
                invoice.Status != VendorInvoiceStatus.PartiallyPaid &&
                invoice.Status != VendorInvoiceStatus.Paid)
            {
                throw new InvalidOperationException("Only approved AP invoices can be posted.");
            }

            if (!string.Equals(invoice.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AP invoice workflow approval is not complete.");
            }

            var activeLines = invoice.LineItems
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.CreatedAt)
                .ThenBy(l => l.Id)
                .ToList();
            var sourceLineDimensions = producer is not null && _sourceDimensions is not null
                ? await _sourceDimensions.GetPostingDimensionsAsync(producer, invoice.Id, cancellationToken)
                : new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>();

            if (activeLines.Count == 0)
                throw new InvalidOperationException("AP invoice has no lines to post.");

            foreach (var line in activeLines)
            {
                if (line.TenantId != tenantId || line.VendorInvoiceId != invoice.Id)
                    throw new InvalidOperationException("AP invoice line belongs to another tenant or document.");
            }

            var supplier = await ResolveInvoiceSupplierForPostingAsync(invoice, cancellationToken);
            var settings = await GetFinanceSettingsAsync(cancellationToken);
            var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
            var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, functionalCurrency);
            var exchangeRate = NormalizeExchangeRate(invoice.ExchangeRate);
            var accountCache = new Dictionary<Guid, Account>();

            var apAccountId = invoice.ApAccountId
                ?? supplier.DefaultApAccountId
                ?? settings.ControlAccountApId
                ?? throw new InvalidOperationException("AP control account is not configured for this tenant.");
            await ResolvePostingAccountAsync(apAccountId, "AP control account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);

            if (invoice.IsOpeningBalance)
            {
                return await BuildApOpeningBalancePostingRequestAsync(
                    invoice,
                    settings,
                    apAccountId,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    accountCache,
                    sourceLineDimensions,
                    cancellationToken);
            }

            var linkedFinanceReceipt = await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.VendorInvoiceId == invoice.Id && !r.IsDeleted);
            var clearsFinanceGrv = linkedFinanceReceipt != null;
            var clearsProcurementGrv = IsProcurementGrvClearingInvoice(invoice);
            var clearsGrv = clearsFinanceGrv || clearsProcurementGrv;
            Guid? grvAccrualAccountId = null;
            if (clearsGrv)
            {
                grvAccrualAccountId = settings.ControlAccountGRVAccrualId
                    ?? throw new InvalidOperationException("GRV accrual control account is not configured for this tenant.");
                await ResolvePostingAccountAsync(grvAccrualAccountId.Value, "GRV accrual account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);
            }

            var postingLines = new List<FinancePostingLineDto>();
            Guid? discountAccountId = null;
            var lineNumber = 1;

            foreach (var line in activeLines)
            {
                var grossAmount = RoundMoney(line.Quantity * line.UnitPrice);
                if (grossAmount <= 0m)
                {
                    continue;
                }

                if (line.DiscountAmount < 0m || line.TaxAmount < 0m)
                    throw new InvalidOperationException("AP invoice line discount and tax amounts cannot be negative.");

                if (clearsGrv)
                {
                    if (clearsFinanceGrv && IsFixedAssetLine(line))
                    {
                        throw new InvalidOperationException("Fixed asset capitalization from GRV accrual clearing is deferred until procurement receipt capitalization is modeled.");
                    }

                    var lineNetAmount = RoundMoney(grossAmount - line.DiscountAmount);
                    if (lineNetAmount <= 0m)
                    {
                        continue;
                    }

                    var grvLine = BuildPostingLine(
                        grvAccrualAccountId!.Value,
                        $"Clear GRV accrual - {invoice.InvoiceNumber} - {line.Description}",
                        debitForeignAmount: lineNetAmount,
                        creditForeignAmount: 0m,
                        invoiceCurrency,
                        functionalCurrency,
                        exchangeRate,
                        invoice.InvoiceDate,
                        invoice.InvoiceNumber,
                        lineNumber++,
                        "AP-GRV");
                    ApplySourceDimensions(grvLine, line, sourceLineDimensions);
                    postingLines.Add(grvLine);
                    continue;
                }

                if (IsFixedAssetLine(line))
                {
                    var lineNetAmount = RoundMoney(grossAmount - line.DiscountAmount);
                    if (lineNetAmount <= 0m)
                    {
                        continue;
                    }

                    var fixedAssetAccountId = await ResolveDebitAccountForInvoiceLineAsync(
                        invoice,
                        supplier,
                        settings,
                        line,
                        accountCache,
                        cancellationToken);

                    var fixedAssetLine = BuildPostingLine(
                        fixedAssetAccountId,
                        $"AP fixed asset capitalization {invoice.InvoiceNumber} - {line.Description}",
                        debitForeignAmount: lineNetAmount,
                        creditForeignAmount: 0m,
                        invoiceCurrency,
                        functionalCurrency,
                        exchangeRate,
                        invoice.InvoiceDate,
                        invoice.InvoiceNumber,
                        lineNumber++,
                        "AP-FixedAsset");
                    fixedAssetLine.Notes = BuildFixedAssetLineNotes(line);
                    ApplySourceDimensions(fixedAssetLine, line, sourceLineDimensions);
                    postingLines.Add(fixedAssetLine);
                    continue;
                }

                var debitAccountId = await ResolveDebitAccountForInvoiceLineAsync(
                    invoice,
                    supplier,
                    settings,
                    line,
                    accountCache,
                    cancellationToken);

                var expenseLine = BuildPostingLine(
                    debitAccountId,
                    $"AP invoice {invoice.InvoiceNumber} - {line.Description}",
                    debitForeignAmount: grossAmount,
                    creditForeignAmount: 0m,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    lineNumber++,
                    ResolveLineTag(line));
                expenseLine.SourceDocumentLineId = line.Id;
                ApplySourceDimensions(expenseLine, line, sourceLineDimensions);
                postingLines.Add(expenseLine);

                if (line.DiscountAmount > 0m)
                {
                    discountAccountId ??= settings.DiscountReceivedAccountId
                        ?? throw new InvalidOperationException("Purchase discount received account is not configured for this tenant.");
                    await ResolvePostingAccountAsync(discountAccountId.Value, "purchase discount received account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                    var discountLine = BuildPostingLine(
                        discountAccountId.Value,
                        $"Purchase discount - {invoice.InvoiceNumber} - {line.Description}",
                        debitForeignAmount: 0m,
                        creditForeignAmount: line.DiscountAmount,
                        invoiceCurrency,
                        functionalCurrency,
                        exchangeRate,
                        invoice.InvoiceDate,
                        invoice.InvoiceNumber,
                        lineNumber++,
                        "AP-Discount");
                    ApplySourceDimensions(discountLine, line, sourceLineDimensions);
                    postingLines.Add(discountLine);
                }
            }

            var taxSnapshotLines = new List<FinanceTaxCalculationSnapshotDto>();
            if (invoice.TaxAmount > 0m)
            {
                var taxBuild = await BuildApInvoiceTaxPostingLinesAsync(
                    invoice,
                    settings,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    accountCache,
                    lineNumber,
                    cancellationToken);

                postingLines.AddRange(taxBuild.Lines);
                taxSnapshotLines.AddRange(taxBuild.Snapshots);
                lineNumber += taxBuild.Lines.Count;
            }

            var debitFunctionalTotal = RoundMoney(postingLines.Sum(l => l.DebitAmount));
            var creditFunctionalTotal = RoundMoney(postingLines.Sum(l => l.CreditAmount));
            var apFunctionalAmount = RoundMoney(debitFunctionalTotal - creditFunctionalTotal);
            var expectedFunctionalTotal = ToFunctionalAmount(invoice.TotalAmount, invoiceCurrency, functionalCurrency, exchangeRate);
            if (apFunctionalAmount <= 0m)
            {
                throw new InvalidOperationException($"Vendor invoice {invoice.InvoiceNumber} has no positive AP amount to post.");
            }

            if (apFunctionalAmount != expectedFunctionalTotal)
            {
                throw new InvalidOperationException("AP invoice amount does not match posting line totals.");
            }

            postingLines.Insert(0, BuildPostingLine(
                apAccountId,
                $"AP invoice {invoice.InvoiceNumber}",
                debitForeignAmount: 0m,
                creditForeignAmount: invoice.TotalAmount,
                invoiceCurrency,
                functionalCurrency,
                exchangeRate,
                invoice.InvoiceDate,
                invoice.InvoiceNumber,
                1,
                "AP-Control"));

            for (var i = 0; i < postingLines.Count; i++)
            {
                postingLines[i].LineNumber = i + 1;
            }

            return new FinancePostingRequestDto
            {
                SourceModule = "AP",
                SourceDocumentType = "VendorInvoice",
                SourceDocumentId = invoice.Id,
                SourceDocumentTenantId = invoice.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                Description = $"Vendor invoice {invoice.InvoiceNumber} - {invoice.SupplierName}",
                PostingDate = invoice.InvoiceDate,
                JournalType = "AP Invoice",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"AP:VendorInvoice:{invoice.TenantId:N}:{invoice.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                BudgetReservationIds = budgetReservationIds,
                BudgetReservationSourceDocumentType = budgetReservationIds.Count > 0
                    ? VendorInvoiceBudgetSource
                    : null,
                Lines = postingLines,
                TaxCalculationSnapshots = taxSnapshotLines
            };
        }

        private async Task<FinancePostingRequestDto> BuildApOpeningBalancePostingRequestAsync(
            VendorInvoice invoice,
            FinanceSettings settings,
            Guid apAccountId,
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
                ?? throw new InvalidOperationException("Migration Clearing Account is not configured for AP opening balance posting.");
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
                throw new InvalidOperationException($"Opening-balance vendor invoice {invoice.InvoiceNumber} has no positive AP amount to post.");
            }
            var functionalOpeningAmount = ToFunctionalAmount(
                openingAmount,
                invoiceCurrency,
                functionalCurrency,
                exchangeRate);

            var postingLines = new List<FinancePostingLineDto>
            {
                BuildPostingLine(
                    apAccountId,
                    $"AP opening balance {invoice.InvoiceNumber}",
                    debitForeignAmount: 0m,
                    creditForeignAmount: openingAmount,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    2,
                    "AP-Control",
                    rateSnapshot.ExchangeRateId,
                    rateSnapshot.Source)
            };

            var activeLines = invoice.LineItems.Where(line => !line.IsDeleted)
                .OrderBy(line => line.CreatedAt)
                .ThenBy(line => line.Id)
                .ToList();
            var sourceAmounts = activeLines
                .Select(line => new
                {
                    Line = line,
                    Amount = RoundMoney((line.Quantity * line.UnitPrice) - line.DiscountAmount + line.TaxAmount)
                })
                .Where(item => item.Amount != 0m)
                .ToList();
            if (sourceAmounts.Any(item => item.Amount < 0m)
                || RoundMoney(sourceAmounts.Sum(item => item.Amount)) != openingAmount)
                throw new InvalidOperationException("AP opening-balance source lines do not reconcile to the invoice total.");

            var allocatedFunctional = 0m;
            for (var index = 0; index < sourceAmounts.Count; index++)
            {
                var source = sourceAmounts[index];
                var functionalAmount = index == sourceAmounts.Count - 1
                    ? functionalOpeningAmount - allocatedFunctional
                    : ToFunctionalAmount(source.Amount, invoiceCurrency, functionalCurrency, exchangeRate);
                var clearingLine = BuildPostingLine(
                    migrationClearingAccountId,
                    $"Migration clearing - AP opening balance {invoice.InvoiceNumber} - {source.Line.Description}",
                    debitForeignAmount: functionalAmount,
                    creditForeignAmount: 0m,
                    functionalCurrency,
                    functionalCurrency,
                    1m,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    index + 1,
                    "AP-MigrationClearing");
                ApplySourceDimensions(clearingLine, source.Line, sourceLineDimensions);
                postingLines.Insert(index, clearingLine);
                allocatedFunctional += functionalAmount;
            }

            for (var index = 0; index < postingLines.Count; index++)
                postingLines[index].LineNumber = index + 1;

            return new FinancePostingRequestDto
            {
                SourceModule = "AP",
                SourceDocumentType = "VendorInvoice",
                SourceDocumentId = invoice.Id,
                SourceDocumentTenantId = invoice.TenantId,
                PostingAction = "Post",
                SourceDocumentReference = invoice.InvoiceNumber,
                Description = $"AP opening balance {invoice.InvoiceNumber} - {invoice.SupplierName}",
                PostingDate = invoice.InvoiceDate,
                JournalType = "AP Opening Balance",
                BookClassification = "IFRS",
                FunctionalCurrencyCode = functionalCurrency,
                IdempotencyKey = $"AP:VendorInvoice:{invoice.TenantId:N}:{invoice.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = postingLines,
                TaxCalculationSnapshots = Array.Empty<FinanceTaxCalculationSnapshotDto>()
            };
        }

        private async Task<Supplier> ResolveInvoiceSupplierForPostingAsync(VendorInvoice invoice, CancellationToken cancellationToken)
        {
            var supplier = await _unitOfWork.Repository<Supplier>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == invoice.SupplierId && !s.IsDeleted);

            if (supplier == null)
                throw new InvalidOperationException("AP invoice supplier was not found for this tenant.");

            if (!supplier.IsActive || supplier.IsBlacklisted || string.Equals(supplier.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Supplier '{supplier.Name}' is not active for AP posting.");

            return supplier;
        }

        private async Task<ProcurementConfigurationProfileDto?>
            ResolveProcurementProfileForMatchingAsync(
                CancellationToken cancellationToken)
        {
            if (_procurementConfiguration == null) return null;

            var effective = await _procurementConfiguration
                .GetEffectiveProfileAsync(
                    "TDC-PROCUREMENT", DateTime.UtcNow, cancellationToken);
            if (effective is not null) return effective;

            var profileId = await _unitOfWork
                .Repository<ProcurementConfigurationProfile>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.ProfileCode == "TDC-PROCUREMENT" &&
                    !item.IsDeleted)
                .AsNoTracking()
                .OrderByDescending(item => item.Version)
                .ThenByDescending(item => item.UpdatedAt)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return profileId.HasValue
                ? await _procurementConfiguration.GetProfileAsync(
                    profileId.Value, cancellationToken)
                : null;
        }

        private async Task<(decimal PriceTolerancePercent, decimal QuantityTolerancePercent)>
            GetInvoiceMatchTolerancesAsync(CancellationToken cancellationToken)
        {
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .GetQueryable(item => item.TenantId == TenantId && !item.IsDeleted)
                .Select(item => new
                {
                    item.ApInvoicePriceTolerancePercent,
                    item.ApInvoiceQuantityTolerancePercent
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
                throw new InvalidOperationException("Finance settings are not configured for the current tenant.");

            return (settings.ApInvoicePriceTolerancePercent, settings.ApInvoiceQuantityTolerancePercent);
        }

        private async Task<FinanceSettings> GetFinanceSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = await _unitOfWork.Repository<FinanceSettings>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted);

            return settings ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        }

        private async Task<Guid> ResolveDebitAccountForInvoiceLineAsync(
            VendorInvoice invoice,
            Supplier supplier,
            FinanceSettings settings,
            VendorInvoiceLineItem line,
            Dictionary<Guid, Account> accountCache,
            CancellationToken cancellationToken)
        {
            if (IsFixedAssetLine(line))
            {
                if (!line.FixedAssetId.HasValue)
                {
                    throw new InvalidOperationException($"AP fixed asset line '{line.Description}' must reference a fixed asset.");
                }

                var asset = await _unitOfWork.Repository<ErpSystem.Core.Entities.Finance.FixedAssets.FixedAsset>()
                    .GetQueryable(a => a.TenantId == TenantId && a.Id == line.FixedAssetId.Value && !a.IsDeleted)
                    .Include(a => a.Category)
                    .FirstOrDefaultAsync(cancellationToken);

                if (asset == null)
                {
                    throw new InvalidOperationException($"AP fixed asset line '{line.Description}' references an asset that was not found for this tenant.");
                }

                if (asset.Category == null || asset.Category.TenantId != TenantId)
                {
                    throw new InvalidOperationException($"Fixed asset '{asset.AssetCode}' category was not found for this tenant.");
                }

                await ResolvePostingAccountAsync(
                    asset.Category.AssetAccountId,
                    "fixed asset cost account",
                    accountCache,
                    allowControlAccount: false,
                    requireDirectPosting: true,
                    cancellationToken);

                if (asset.PostingEventId.HasValue || asset.JournalEntryId.HasValue)
                {
                    var sameApInvoiceLine =
                        string.Equals(asset.SourceDocumentType, "VendorInvoice", StringComparison.OrdinalIgnoreCase) &&
                        asset.SourceDocumentId == invoice.Id &&
                        asset.SourceDocumentLineId == line.Id;
                    if (!sameApInvoiceLine)
                    {
                        throw new InvalidOperationException($"Fixed asset '{asset.AssetCode}' is already capitalized.");
                    }
                }

                return asset.Category.AssetAccountId;
            }

            var isInventoryLine =
                string.Equals(line.LineItemType, "Inventory", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(line.LineItemType, "Product", StringComparison.OrdinalIgnoreCase);

            var accountId = isInventoryLine
                ? line.GLAccountId ?? settings.ControlAccountInventoryId
                    ?? throw new InvalidOperationException("Inventory or clearing account is not configured for AP invoice line posting.")
                : line.GLAccountId ?? invoice.ExpenseAccountId ?? supplier.DefaultExpenseAccountId
                    ?? throw new InvalidOperationException($"No expense account specified for AP line '{line.Description}'.");

            await ResolvePostingAccountAsync(
                accountId,
                isInventoryLine ? "inventory or clearing account" : "expense account",
                accountCache,
                allowControlAccount: isInventoryLine,
                requireDirectPosting: !isInventoryLine,
                cancellationToken);

            return accountId;
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
                throw new InvalidOperationException($"AP posting {role} was not found for this tenant.");

            if (account.Status != AccountStatus.Active)
                throw new InvalidOperationException($"AP posting {role} account '{account.AccountNumber}' is not active.");

            if (account.IsControlAccount && !allowControlAccount)
                throw new InvalidOperationException($"AP posting {role} account '{account.AccountNumber}' is a control account and cannot be used for this line.");

            if (requireDirectPosting && !account.AllowDirectPosting)
                throw new InvalidOperationException($"AP posting {role} account '{account.AccountNumber}' does not allow direct posting.");

            accountCache[accountId] = account;
            return account;
        }

        private async Task<TaxPostingBuildResult> BuildApInvoiceTaxPostingLinesAsync(
            VendorInvoice invoice,
            FinanceSettings settings,
            string invoiceCurrency,
            string functionalCurrency,
            decimal exchangeRate,
            Dictionary<Guid, Account> accountCache,
            int startingLineNumber,
            CancellationToken cancellationToken)
        {
            var calculatedLines = new List<FinancePostingLineDto>();
            var snapshots = new List<FinanceTaxCalculationSnapshotDto>();

            if (_taxEngine == null)
            {
                throw new InvalidOperationException("Effective-dated tax calculation is not configured for AP invoice posting.");
            }

            if (invoice.TaxAmount <= 0m)
            {
                return new TaxPostingBuildResult(calculatedLines, snapshots);
            }

            if (_taxEngine != null)
            {
                foreach (var line in invoice.LineItems.Where(l => !l.IsDeleted).OrderBy(l => l.CreatedAt).ThenBy(l => l.Id))
                {
                    var lineBase = RoundMoney((line.Quantity * line.UnitPrice) - line.DiscountAmount);
                    if (lineBase <= 0m)
                    {
                        continue;
                    }

                    if (IsNoTaxTreatment(line.TaxTreatment))
                    {
                        if (RoundMoney(line.TaxAmount) != 0m || RoundMoney(line.TaxRate) != 0m)
                        {
                            throw new InvalidOperationException($"AP invoice line '{line.Description}' is {line.TaxTreatment} but carries a tax amount or rate.");
                        }

                        continue;
                    }

                    var taxResult = await _taxEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
                    {
                        BaseAmount = lineBase,
                        TaxGroupId = line.TaxGroupId,
                        TransactionDate = invoice.InvoiceDate,
                        TransactionType = ResolveApTaxTransactionType(line),
                        SupplierId = invoice.SupplierId
                    }, cancellationToken);

                    foreach (var breakdown in taxResult.TaxBreakdowns.Where(t => t.TaxAmount > 0m))
                    {
                        Guid? accountId;
                        if (breakdown.IsInputTaxDeductible)
                        {
                            accountId = breakdown.TaxReceivableAccountId;
                        }
                        else if (IsFixedAssetLine(line))
                        {
                            accountId = await ResolveDebitAccountForInvoiceLineAsync(
                                invoice,
                                await ResolveInvoiceSupplierForPostingAsync(invoice, cancellationToken),
                                settings,
                                line,
                                accountCache,
                                cancellationToken);
                        }
                        else
                        {
                            accountId = line.GLAccountId ?? invoice.ExpenseAccountId;
                        }

                        if (!accountId.HasValue)
                        {
                            throw new InvalidOperationException($"AP invoice tax account is not configured for tax '{breakdown.TaxCode}'.");
                        }

                        await ResolvePostingAccountAsync(
                            accountId.Value,
                            $"input tax account for {breakdown.TaxCode}",
                            accountCache,
                            allowControlAccount: true,
                            requireDirectPosting: false,
                            cancellationToken);

                        var postingLine = BuildPostingLine(
                            accountId.Value,
                            $"{breakdown.TaxName} - {invoice.InvoiceNumber}",
                            debitForeignAmount: breakdown.TaxAmount,
                            creditForeignAmount: 0m,
                            invoiceCurrency,
                            functionalCurrency,
                            exchangeRate,
                            invoice.InvoiceDate,
                            invoice.InvoiceNumber,
                            startingLineNumber + calculatedLines.Count,
                            $"AP-Tax-{breakdown.TaxCode}");
                        postingLine.Notes = IsFixedAssetLine(line)
                            ? $"{BuildFixedAssetLineNotes(line)};TaxId={breakdown.TaxId};TaxGroupId={taxResult.TaxGroupId};TaxRate={breakdown.TaxRate};TaxableAmount={breakdown.TaxableAmount};Recoverable={breakdown.IsInputTaxDeductible}"
                            : $"TaxId={breakdown.TaxId};TaxGroupId={taxResult.TaxGroupId};TaxRate={breakdown.TaxRate};TaxableAmount={breakdown.TaxableAmount}";
                        calculatedLines.Add(postingLine);

                        snapshots.Add(ToTaxSnapshot(
                            "VendorInvoice",
                            invoice.Id,
                            taxResult.TaxGroupId,
                            lineBase,
                            breakdown,
                            invoice.InvoiceDate));
                    }
                }

                if (calculatedLines.Count > 0 && RoundMoney(calculatedLines.Sum(l => l.DebitAmount)) == ToFunctionalAmount(invoice.TaxAmount, invoiceCurrency, functionalCurrency, exchangeRate))
                {
                    return new TaxPostingBuildResult(calculatedLines, snapshots);
                }
            }

            throw new InvalidOperationException("AP invoice configured tax calculation does not reconcile to the invoice tax total.");
        }

        private async Task<(decimal TaxRate, decimal TaxAmount)> ResolveApLineTaxAsync(
            VendorInvoiceLineItemCreateDto lineDto,
            decimal lineNet,
            DateTime invoiceDate,
            Guid supplierId,
            bool isOpeningBalance,
            CancellationToken cancellationToken)
        {
            if (isOpeningBalance || lineDto.TaxTreatment != TaxTreatment.Standard || lineNet <= 0m)
            {
                return (0m, 0m);
            }

            if (_taxEngine != null && lineDto.TaxGroupId.HasValue)
            {
                var taxResult = await _taxEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
                {
                    BaseAmount = lineNet,
                    TaxGroupId = lineDto.TaxGroupId,
                    TransactionDate = invoiceDate,
                    TransactionType = ResolveApTaxTransactionType(lineDto.LineItemType),
                    SupplierId = supplierId
                }, cancellationToken);

                return (
                    taxResult.TotalTaxAmount > 0m ? taxResult.TotalTaxAmount / lineNet * 100m : 0m,
                    taxResult.TotalTaxAmount);
            }

            var lineTaxRate = lineDto.TaxRate;
            return (lineTaxRate, lineNet * (lineTaxRate / 100m));
        }

        private static TaxTransactionType ResolveApTaxTransactionType(VendorInvoiceLineItem line)
            => ResolveApTaxTransactionType(line.LineItemType);

        private static TaxTransactionType ResolveApTaxTransactionType(string? lineItemType)
            => string.Equals(lineItemType, "Service", StringComparison.OrdinalIgnoreCase)
                ? TaxTransactionType.PurchaseOfServices
                : TaxTransactionType.PurchaseOfGoods;

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
            decimal debitForeignAmount,
            decimal creditForeignAmount,
            string invoiceCurrency,
            string functionalCurrency,
            decimal exchangeRate,
            DateTime exchangeRateDate,
            string reference,
            int lineNumber,
            string transactionTag,
            Guid? exchangeRateId = null,
            string? exchangeRateSource = null)
        {
            var isForeign = !string.Equals(invoiceCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
            var debitAmount = ToFunctionalAmount(debitForeignAmount, invoiceCurrency, functionalCurrency, exchangeRate);
            var creditAmount = ToFunctionalAmount(creditForeignAmount, invoiceCurrency, functionalCurrency, exchangeRate);

            return new FinancePostingLineDto
            {
                AccountId = accountId,
                Description = description,
                DebitAmount = debitAmount,
                CreditAmount = creditAmount,
                TransactionCurrency = invoiceCurrency,
                TransactionDebitAmount = debitForeignAmount,
                TransactionCreditAmount = creditForeignAmount,
                ForeignCurrencyAmount = isForeign
                    ? debitForeignAmount > 0m ? debitForeignAmount : creditForeignAmount
                    : null,
                ExchangeRate = isForeign ? exchangeRate : null,
                ExchangeRateId = isForeign ? exchangeRateId : null,
                ExchangeRateSource = isForeign
                    ? exchangeRateSource ?? "AP invoice exchange-rate snapshot"
                    : null,
                ExchangeRateDate = isForeign ? exchangeRateDate.Date : null,
                SourceReferenceNumber = reference,
                LineNumber = lineNumber,
                TransactionTag = transactionTag
            };
        }

        private static string ResolveLineTag(VendorInvoiceLineItem line)
            => IsFixedAssetLine(line)
                ? "AP-FixedAsset"
                : string.Equals(line.LineItemType, "Inventory", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(line.LineItemType, "Product", StringComparison.OrdinalIgnoreCase)
                ? "AP-Inventory"
                : "AP-Expense";

        private static bool IsFixedAssetLine(VendorInvoiceLineItem line)
            => string.Equals(line.LineItemType, "FixedAsset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(line.LineItemType, "Fixed Asset", StringComparison.OrdinalIgnoreCase)
                || line.FixedAssetId.HasValue;

        private static bool IsProcurementGrvClearingInvoice(VendorInvoice invoice)
            => invoice.PurchaseOrderId.HasValue &&
                invoice.AcceptedSupplyKind == ProcurementAcceptedSupplyKind.GoodsReceiptInspection &&
                invoice.AcceptedSupplySourceId == invoice.PurchaseOrderId;

        private static string BuildFixedAssetLineNotes(VendorInvoiceLineItem line)
            => line.FixedAssetId.HasValue
                ? $"VendorInvoiceLineId={line.Id:N};FixedAssetId={line.FixedAssetId.Value:N}"
                : $"VendorInvoiceLineId={line.Id:N}";

        private static bool IsNoTaxTreatment(TaxTreatment treatment)
            => treatment == TaxTreatment.Exempt
                || treatment == TaxTreatment.ZeroRated
                || treatment == TaxTreatment.OutOfScope;

        private async Task<FinancePostingEvent> GetPostedInvoiceEventAsync(
            VendorInvoice invoice,
            CancellationToken cancellationToken)
        {
            if (!invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("The AP invoice is not linked to a posted journal.");

            return await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.SourceModule == "AP" &&
                    item.SourceDocumentType == "VendorInvoice" &&
                    item.SourceDocumentId == invoice.Id &&
                    item.PostingAction == "Post" &&
                    item.PostingStatus == "Posted" &&
                    item.JournalEntryId == invoice.JournalEntryId.Value &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The authoritative AP invoice posting event was not found for this tenant.");
        }

        private async Task<FinancePostingResultDto> GetExistingReversalResultAsync(
            JournalEntry originalJournal,
            Guid sourceDocumentId,
            string sourceDocumentType,
            CancellationToken cancellationToken)
        {
            if (!originalJournal.ReversalJournalEntryId.HasValue)
                throw new InvalidOperationException(
                    "The original journal is marked reversed but has no reversal-journal lineage.");

            var existing = await _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(item =>
                    item.TenantId == TenantId &&
                    item.SourceModule == "AP" &&
                    item.SourceDocumentType == sourceDocumentType &&
                    item.SourceDocumentId == sourceDocumentId &&
                    item.PostingAction != "Post" &&
                    item.PostingStatus == "Posted" &&
                    item.JournalEntryId == originalJournal.ReversalJournalEntryId.Value &&
                    !item.IsDeleted)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The existing AP reversal posting event was not found for this tenant.");

            return new FinancePostingResultDto
            {
                PostingEventId = existing.Id,
                JournalEntryId = existing.JournalEntryId!.Value,
                PostingStatus = existing.PostingStatus,
                WasDuplicate = true,
                TotalDebitAmount = existing.TotalDebitAmount,
                TotalCreditAmount = existing.TotalCreditAmount,
                FunctionalCurrencyCode = existing.FunctionalCurrencyCode,
                PostingDate = existing.PostingDate,
                SourceModule = existing.SourceModule,
                OriginModuleCode = existing.OriginModuleCode ?? existing.SourceModule,
                SourceDocumentType = existing.SourceDocumentType,
                SourceDocumentId = existing.SourceDocumentId,
                PostingAction = existing.PostingAction
            };
        }

        private static FinancePostingRequestDto BuildApReversalRequest(
            FinancePostingEvent originalEvent,
            FinanceReversalPlanDto plan,
            Guid sourceDocumentId,
            string sourceReference,
            string sourceDocumentType,
            string journalType,
            string description,
            string idempotencyKey,
            string reason) => new()
        {
            SourceModule = "AP",
            OriginModuleCode = originalEvent.OriginModuleCode,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentTenantId = originalEvent.TenantId,
            PostingAction = "Reverse",
            SourceDocumentReference = sourceReference,
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
        };

        private static string AppendLifecycleNote(string? notes, string entry) =>
            string.IsNullOrWhiteSpace(notes) ? entry : $"{notes.TrimEnd()}\n\n{entry}";

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

        private async Task RecordApInvoiceAuditAsync(
            string eventType,
            VendorInvoice invoice,
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
                SourceModule = "AP",
                SourceDocumentType = "VendorInvoice",
                SourceDocumentId = invoice.Id,
                JournalEntryId = journalEntryId ?? invoice.JournalEntryId,
                PostingEventId = postingEventId,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Comment = comment,
                Resource = "Finance.APInvoice",
                ResourceId = invoice.Id.ToString()
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
                    throw new InvalidOperationException("Functional-currency AP opening invoices cannot carry foreign exchange-rate evidence.");
                if (RoundRate(suppliedRate) != 1m)
                    throw new InvalidOperationException("Functional-currency AP opening invoices must use an exchange rate of 1.");
                return new OpeningInvoiceExchangeRateSnapshot(null, 1m, transactionCurrency, functionalCurrency, "Functional currency");
            }

            if (!exchangeRateId.HasValue)
                throw new InvalidOperationException("Foreign-currency AP opening invoices require an approved exchange-rate record.");

            var quoteSide = settings.DirectionalExchangeRatePolicyEnabled
                ? settings.ApInvoiceQuoteSide
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
                    "The selected AP opening-invoice exchange rate is not active, approved, effective, or compliant with the tenant invoice-rate policy.");
            }

            if (RoundRate(suppliedRate) != RoundRate(rate.Rate))
                throw new InvalidOperationException("The AP opening-invoice exchange-rate value does not match the approved rate record.");

            return new OpeningInvoiceExchangeRateSnapshot(
                rate.Id,
                rate.Rate,
                transactionCurrency,
                functionalCurrency,
                rate.RateSource);
        }

        private Task<OpeningInvoiceExchangeRateSnapshot> RevalidateOpeningInvoiceExchangeRateAsync(
            VendorInvoice invoice,
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

        private async Task<ProcurementAcceptedSupplyResolutionDto?>
            ResolveAcceptedSupplyForCreateAsync(
                VendorInvoiceCreateDto dto,
                Supplier supplier,
                CancellationToken cancellationToken)
        {
            if (dto.IsOpeningBalance)
            {
                if (dto.AcceptedSupplyKind.HasValue || dto.AcceptedSupplySourceId.HasValue)
                    throw new InvalidOperationException(
                        "Opening-balance invoices cannot consume a procurement acceptance record.");
                return null;
            }

            if (!dto.PurchaseOrderId.HasValue)
            {
                if (!dto.AcceptedSupplyKind.HasValue && !dto.AcceptedSupplySourceId.HasValue)
                    return null;
                if (!dto.IsTrustedAcceptedSupplyHandoff ||
                    dto.AcceptedSupplyKind != ProcurementAcceptedSupplyKind.WorksPaymentCertificate ||
                    !dto.AcceptedSupplySourceId.HasValue)
                    throw new InvalidOperationException(
                        "Works invoices must originate from the approved QS payment-certificate handoff.");
                var works = await RequireAcceptedSupplyServiceAsync()
                    .ResolveAsync(dto.AcceptedSupplyKind.Value,
                        dto.AcceptedSupplySourceId.Value, null, null, cancellationToken);
                await EnsureAcceptedSupplySupplierAsync(works, supplier, cancellationToken);
                return works;
            }

            var order = await _unitOfWork.Repository<PurchaseOrder>()
                .GetQueryable(value => value.TenantId == TenantId &&
                    value.Id == dto.PurchaseOrderId.Value && !value.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "The selected purchase order was not found in the current tenant.");
            if (!order.ProcurementCategory.HasValue)
                throw new InvalidOperationException(
                    "The selected purchase order has no governed category snapshot.");

            if (order.ProcurementCategory == ProcurementCategoryClass.Works)
                throw new InvalidOperationException(
                    "Works invoices must originate from the approved QS payment-certificate handoff.");
            if (order.ProcurementCategory == ProcurementCategoryClass.Goods)
            {
                if (dto.AcceptedSupplyKind.HasValue || dto.AcceptedSupplySourceId.HasValue)
                    throw new InvalidOperationException(
                        "Goods acceptance is derived automatically from governed GRN inspections.");
                return null;
            }

            if (dto.AcceptedSupplyKind != ProcurementAcceptedSupplyKind.ServiceCompletion ||
                !dto.AcceptedSupplySourceId.HasValue)
                throw new InvalidOperationException(
                    "Select an approved service completion for this Services purchase order.");
            var service = await RequireAcceptedSupplyServiceAsync()
                .ResolveAsync(dto.AcceptedSupplyKind.Value,
                    dto.AcceptedSupplySourceId.Value, order.Id, null, cancellationToken);
            await EnsureAcceptedSupplySupplierAsync(service, supplier, cancellationToken);
            return service;
        }

        private async Task<ProcurementAcceptedSupplyResolutionDto?>
            ResolveAcceptedSupplyForUpdateAsync(
                VendorInvoice invoice,
                VendorInvoiceUpdateDto dto,
                Supplier? supplier,
                CancellationToken cancellationToken)
        {
            if (invoice.AcceptedSupplyKind == ProcurementAcceptedSupplyKind.WorksPaymentCertificate)
            {
                if (dto.PurchaseOrderId != invoice.PurchaseOrderId ||
                    dto.AcceptedSupplyKind != invoice.AcceptedSupplyKind ||
                    dto.AcceptedSupplySourceId != invoice.AcceptedSupplySourceId)
                    throw new InvalidOperationException(
                        "QS Works acceptance lineage cannot be changed through the AP invoice editor.");
                return await RequireAcceptedSupplyServiceAsync().ResolveAsync(
                    invoice.AcceptedSupplyKind.Value,
                    invoice.AcceptedSupplySourceId!.Value,
                    invoice.PurchaseOrderId,
                    invoice.Id,
                    cancellationToken);
            }

            var request = new VendorInvoiceCreateDto
            {
                PurchaseOrderId = dto.PurchaseOrderId,
                AcceptedSupplyKind = dto.AcceptedSupplyKind,
                AcceptedSupplySourceId = dto.AcceptedSupplySourceId,
                IsOpeningBalance = dto.IsOpeningBalance
            };
            return await ResolveAcceptedSupplyForCreateAsync(
                request,
                supplier ?? throw new InvalidOperationException(
                    "The invoice supplier is unavailable."),
                cancellationToken);
        }

        private IProcurementAcceptedSupplyService RequireAcceptedSupplyServiceAsync() =>
            _acceptedSupply ?? throw new InvalidOperationException(
                "The shared procurement accepted-supply control is not registered.");

        private async Task EnsureAcceptedSupplySupplierAsync(
            ProcurementAcceptedSupplyResolutionDto accepted,
            Supplier supplier,
            CancellationToken cancellationToken)
        {
            if (supplier.Id == accepted.BusinessPartnerId)
                return;
            var partner = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(value => value.TenantId == TenantId &&
                    value.Id == accepted.BusinessPartnerId && !value.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (partner == null ||
                !(string.Equals(partner.PartnerCode, supplier.SupplierCode,
                      StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(partner.PartnerName, supplier.Name,
                      StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "The accepted-supply supplier does not match the invoice supplier.");
        }

        private static void ApplyAcceptedSupply(
            VendorInvoice invoice,
            ProcurementAcceptedSupplyResolutionDto? accepted)
        {
            invoice.AcceptedSupplyKind = accepted?.Kind;
            invoice.AcceptedSupplySourceId = accepted?.SourceId;
            invoice.AcceptedSupplySourceReference = accepted?.SourceReference;
            invoice.AcceptedSupplySnapshotHash = accepted?.SourceIntegrityHash;
            invoice.AcceptedSupplyValidatedAtUtc = accepted?.AcceptedAtUtc;
        }

        private async Task RevalidateWorksAcceptedSupplyAsync(
            VendorInvoice invoice,
            CancellationToken cancellationToken)
        {
            if (invoice.IsOpeningBalance)
                return;
            var appearsToBeQsHandoff =
                invoice.Reference?.StartsWith("QS-", StringComparison.OrdinalIgnoreCase) == true;
            if (invoice.AcceptedSupplyKind != ProcurementAcceptedSupplyKind.WorksPaymentCertificate)
            {
                if (appearsToBeQsHandoff)
                    throw new VendorInvoiceMatchControlException(
                        "AP_WORKS_CERTIFICATE_LINEAGE_MISSING",
                        "The QS invoice has no typed Works certificate lineage.");
                return;
            }
            if (!invoice.AcceptedSupplySourceId.HasValue)
                throw new VendorInvoiceMatchControlException(
                    "AP_WORKS_CERTIFICATE_SOURCE_MISSING",
                    "The Works invoice has no authoritative certificate identifier.");

            ProcurementAcceptedSupplyResolutionDto accepted;
            try
            {
                accepted = await RequireAcceptedSupplyServiceAsync().ResolveAsync(
                    invoice.AcceptedSupplyKind.Value,
                    invoice.AcceptedSupplySourceId.Value,
                    invoice.PurchaseOrderId,
                    invoice.Id,
                    cancellationToken);
            }
            catch (ProcurementAcceptedSupplyValidationException exception)
            {
                throw new VendorInvoiceMatchControlException(exception.Code, exception.Message);
            }
            if (!accepted.AcceptedAmount.HasValue ||
                RoundMoney(accepted.AcceptedAmount.Value) != RoundMoney(invoice.TotalAmount))
                throw new VendorInvoiceMatchControlException(
                    "AP_WORKS_CERTIFICATE_AMOUNT_MISMATCH",
                    "The AP invoice total no longer matches the approved Works certificate.");
            ApplyAcceptedSupply(invoice, accepted);
        }

        private async Task<Supplier> ResolveSupplierForInvoiceAsync(Guid supplierOrBusinessPartnerId, CancellationToken cancellationToken)
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
                throw new InvalidOperationException("Customer business partners cannot be used for AP supplier invoices.");
            }

            if (partner.IsBlacklisted)
            {
                throw new InvalidOperationException($"Business partner '{partner.PartnerName}' is blacklisted and cannot be used for AP supplier invoices.");
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
                Notes = $"Auto-created from business partner {partner.PartnerCode} for AP supplier invoice entry.",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await supplierRepository.AddAsync(supplier);
            return supplier;
        }

        public async Task<bool> IsDuplicateAsync(Guid supplierId, string? supplierInvoiceNumber, DateTime invoiceDate, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(supplierInvoiceNumber)) return false;

            var dateTolerance = invoiceDate.AddDays(-30);
            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.SupplierId == supplierId &&
                    i.SupplierInvoiceNumber == supplierInvoiceNumber &&
                    i.InvoiceDate >= dateTolerance &&
                    i.Status != VendorInvoiceStatus.Voided);

            if (excludeId.HasValue)
                queryable = queryable.Where(i => i.Id != excludeId.Value);

            return await queryable.AnyAsync(cancellationToken);
        }

        private async Task<VendorInvoice> GetEntityOrThrowAsync(Guid id, CancellationToken cancellationToken)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == id);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            return invoice;
        }

        private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
        {
            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.APInvoice,
                TenantId,
                DateTime.UtcNow,
                nameof(VendorInvoice),
                cancellationToken: cancellationToken);
        }

        private VendorInvoiceDto MapToDto(VendorInvoice invoice)
        {
            return new VendorInvoiceDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                SupplierInvoiceNumber = invoice.SupplierInvoiceNumber,
                SupplierId = invoice.SupplierId,
                SupplierName = invoice.SupplierName,
                PurchaseOrderId = invoice.PurchaseOrderId,
                PurchaseOrderNumber = invoice.PurchaseOrder?.OrderNumber,
                InvoiceDate = invoice.InvoiceDate,
                ReceivedDate = invoice.ReceivedDate,
                DueDate = invoice.DueDate,
                SubTotal = invoice.SubTotal,
                TaxAmount = invoice.TaxAmount,
                DiscountAmount = invoice.DiscountAmount,
                TotalAmount = invoice.TotalAmount,
                PaidAmount = invoice.PaidAmount,
                BalanceAmount = invoice.BalanceAmount,
                CurrencyCode = invoice.CurrencyCode,
                ExchangeRate = invoice.ExchangeRate,
                ExchangeRateId = invoice.ExchangeRateId,
                BaseCurrencyAmount = invoice.BaseCurrencyAmount,
                PaymentTermsDays = invoice.PaymentTermsDays,
                PaymentTermId = invoice.PaymentTermId,
                EarlyPaymentDiscountPercentage = invoice.EarlyPaymentDiscountPercentage,
                EarlyPaymentDiscountDueDate = invoice.EarlyPaymentDiscountDueDate,
                EarlyPaymentDiscountAmount = invoice.EarlyPaymentDiscountAmount,
                WithholdingTaxRate = invoice.WithholdingTaxRate,
                WithholdingTaxAmount = invoice.WithholdingTaxAmount,
                WithholdingTaxId = invoice.WithholdingTaxId,
                WithholdingTaxAccountId = invoice.WithholdingTaxAccountId,
                WithholdingCertificateNumber = invoice.WithholdingCertificateNumber,
                WithholdingCertificateDate = invoice.WithholdingCertificateDate,
                MatchingType = invoice.MatchingType,
                MatchingStatus = invoice.MatchingStatus,
                MatchingNotes = invoice.MatchingNotes,
                MatchingControlEventId = invoice.MatchingControlEventId,
                MatchingSnapshotHash = invoice.MatchingSnapshotHash,
                MatchingEvaluatedAtUtc = invoice.MatchingEvaluatedAtUtc,
                MatchingPriceTolerancePercent = invoice.MatchingPriceTolerancePercent,
                MatchingQuantityTolerancePercent = invoice.MatchingQuantityTolerancePercent,
                MatchExceptionControlEventId = invoice.MatchExceptionControlEventId,
                AcceptedSupplyKind = invoice.AcceptedSupplyKind,
                AcceptedSupplySourceId = invoice.AcceptedSupplySourceId,
                AcceptedSupplySourceReference = invoice.AcceptedSupplySourceReference,
                AcceptedSupplySnapshotHash = invoice.AcceptedSupplySnapshotHash,
                AcceptedSupplyValidatedAtUtc = invoice.AcceptedSupplyValidatedAtUtc,
                Status = invoice.Status,
                ApprovalStatus = invoice.ApprovalStatus,
                ExpenseAccountId = invoice.ExpenseAccountId,
                ExpenseAccountName = invoice.ExpenseAccount?.AccountName,
                ApAccountId = invoice.ApAccountId,
                ApAccountName = invoice.ApAccount?.AccountName,
                JournalEntryId = invoice.JournalEntryId,
                Notes = invoice.Notes,
                Reference = invoice.Reference,
                IsOpeningBalance = invoice.IsOpeningBalance,
                LineItems = invoice.LineItems.Select(li => new VendorInvoiceLineItemDto
                {
                    Id = li.Id,
                    VendorInvoiceId = li.VendorInvoiceId,
                    LineItemType = li.LineItemType,
                    GLAccountId = li.GLAccountId,
                    GLAccountName = li.GLAccount?.AccountName,
                    BudgetEntryId = li.BudgetEntryId,
                    FixedAssetId = li.FixedAssetId,
                    CapitalizationJournalEntryId = li.CapitalizationJournalEntryId,
                    CapitalizationPostingEventId = li.CapitalizationPostingEventId,
                    CapitalizedAt = li.CapitalizedAt,
                    PurchaseOrderItemId = li.PurchaseOrderItemId,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    LineTotal = li.Quantity * li.UnitPrice,
                    TaxRate = li.TaxRate,
                    TaxAmount = li.TaxAmount,
                    TaxCode = li.TaxCode,
                    TaxGroupId = li.TaxGroupId,
                    TaxTreatment = li.TaxTreatment,
                    DiscountPercentage = li.DiscountPercentage,
                    DiscountAmount = li.DiscountAmount,
                    Unit = li.Unit
                }).ToList(),
                PaymentAllocations = invoice.PaymentAllocations?.Select(a => new VendorPaymentAllocationDto
                {
                    Id = a.Id,
                    VendorPaymentId = a.VendorPaymentId,
                    VendorInvoiceId = a.VendorInvoiceId,
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
                    IsReversal = a.IsReversal
                }).ToList() ?? new List<VendorPaymentAllocationDto>(),
                CreatedAt = invoice.CreatedAt,
                UpdatedAt = invoice.UpdatedAt
            };
        }

        private async Task<PaymentTerm?> ResolvePaymentTermAsync(Guid? paymentTermId, string applicableTo, CancellationToken cancellationToken)
        {
            if (!paymentTermId.HasValue || paymentTermId.Value == Guid.Empty)
            {
                return await _unitOfWork.Repository<PaymentTerm>()
                    .GetQueryable(t =>
                        t.TenantId == TenantId &&
                        !t.IsDeleted &&
                        t.IsActive &&
                        t.IsDefault &&
                        (t.ApplicableTo == "All" || t.ApplicableTo == applicableTo || t.ApplicableTo == "Vendor"))
                    .OrderBy(t => t.ApplicableTo == applicableTo ? 0 : 1)
                    .ThenBy(t => t.DisplayOrder)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var paymentTerm = await _unitOfWork.Repository<PaymentTerm>()
                .FirstOrDefaultAsync(t =>
                    t.TenantId == TenantId &&
                    t.Id == paymentTermId.Value &&
                    !t.IsDeleted &&
                    t.IsActive);

            if (paymentTerm == null)
            {
                throw new InvalidOperationException($"Active payment term with Id '{paymentTermId.Value}' was not found.");
            }

            if (!string.Equals(paymentTerm.ApplicableTo, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(paymentTerm.ApplicableTo, applicableTo, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Payment term '{paymentTerm.Code}' is not applicable to {applicableTo} transactions.");
            }

            return paymentTerm;
        }

        /// <summary>
        /// Resolves the AP invoice's expected payment-time WHT classification from tenant setup.
        /// Threshold application is intentionally deferred to VendorPaymentService because the
        /// Ghana annual supplier aggregate is triggered by payment, not invoice capture.
        /// </summary>
        private async Task<InvoiceWhtResolution> ResolveInvoiceWhtAsync(
            Guid? taxId,
            decimal requestedRate,
            DateTime invoiceDate,
            bool isOpeningBalance,
            CancellationToken cancellationToken)
        {
            if (isOpeningBalance || !taxId.HasValue || taxId.Value == Guid.Empty)
            {
                if (!isOpeningBalance && requestedRate > 0m)
                {
                    throw new InvalidOperationException("Select a configured purchase WHT tax instead of supplying a standalone invoice WHT rate.");
                }
                return new InvoiceWhtResolution(null, 0m, null);
            }

            var tax = await _unitOfWork.Repository<Tax>().FirstOrDefaultAsync(item =>
                item.TenantId == TenantId && !item.IsDeleted && item.IsActive && item.Id == taxId.Value
                && item.Category == TaxCategory.Withholding
                && (item.Applicability == TaxApplicability.Purchases || item.Applicability == TaxApplicability.Both));
            if (tax == null)
            {
                throw new InvalidOperationException("The selected invoice tax is not an active purchase WHT configuration for this tenant.");
            }

            var effectiveRate = tax.EffectiveFrom.Date <= invoiceDate.Date
                ? tax.Rate
                : await _unitOfWork.Repository<TaxRateHistory>()
                    .GetQueryable(history =>
                        history.TenantId == TenantId && !history.IsDeleted && history.TaxId == tax.Id
                        && history.EffectiveFrom <= invoiceDate
                        && (!history.EffectiveTo.HasValue || history.EffectiveTo.Value >= invoiceDate))
                    .OrderByDescending(history => history.EffectiveFrom)
                    .Select(history => (decimal?)history.Rate)
                    .FirstOrDefaultAsync(cancellationToken);
            if (!effectiveRate.HasValue)
            {
                throw new InvalidOperationException($"WHT tax {tax.Code} is not effective on {invoiceDate:yyyy-MM-dd}.");
            }
            if (!tax.TaxPayableAccountId.HasValue)
            {
                throw new InvalidOperationException($"WHT tax {tax.Code} has no payable account configured.");
            }

            return new InvoiceWhtResolution(tax.Id, effectiveRate.Value, tax.TaxPayableAccountId);
        }

        private sealed record OpeningInvoiceExchangeRateSnapshot(
            Guid? ExchangeRateId,
            decimal Rate,
            string TransactionCurrency,
            string FunctionalCurrency,
            string Source);

        private sealed record InvoiceWhtResolution(Guid? TaxId, decimal Rate, Guid? TaxPayableAccountId);

        private sealed record TaxPostingBuildResult(
            List<FinancePostingLineDto> Lines,
            List<FinanceTaxCalculationSnapshotDto> Snapshots);
    }
}
