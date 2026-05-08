using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
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
    /// Manages vendor invoice lifecycle: creation, matching, approval, and voiding.
    /// </summary>
    public class VendorInvoiceService : IVendorInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<VendorInvoiceService> _logger;
        private readonly ISubledgerPostingService _subledgerPostingService;
        private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService _inventoryValuationService;

        public VendorInvoiceService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ISubledgerPostingService subledgerPostingService,
            ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService inventoryValuationService,
            ILogger<VendorInvoiceService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _subledgerPostingService = subledgerPostingService;
            _inventoryValuationService = inventoryValuationService;
            _logger = logger;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
        private string UserName => _currentUser.UserName ?? "system";

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

        public async Task<VendorInvoiceDto> CreateAsync(VendorInvoiceCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate supplier
            var supplier = await _unitOfWork.Repository<Supplier>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == dto.SupplierId);

            if (supplier == null)
                throw new KeyNotFoundException($"Supplier with Id '{dto.SupplierId}' not found.");

            // Duplicate check
            if (!string.IsNullOrWhiteSpace(dto.SupplierInvoiceNumber))
            {
                var isDup = await IsDuplicateAsync(dto.SupplierId, dto.SupplierInvoiceNumber, dto.InvoiceDate, null, cancellationToken);
                if (isDup)
                    throw new InvalidOperationException($"Duplicate invoice detected with supplier reference '{dto.SupplierInvoiceNumber}'.");
            }

            var invoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var invoice = new VendorInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                InvoiceNumber = invoiceNumber,
                SupplierInvoiceNumber = dto.SupplierInvoiceNumber,
                SupplierId = dto.SupplierId,
                SupplierName = supplier.Name,
                PurchaseOrderId = dto.PurchaseOrderId,
                InvoiceDate = dto.InvoiceDate,
                ReceivedDate = dto.ReceivedDate ?? now,
                DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(dto.PaymentTermsDays),
                CurrencyCode = dto.CurrencyCode,
                ExchangeRate = dto.ExchangeRate,
                PaymentTermsDays = dto.PaymentTermsDays,
                EarlyPaymentDiscountPercentage = dto.EarlyPaymentDiscountPercentage,
                EarlyPaymentDiscountDueDate = dto.EarlyPaymentDiscountDueDate,
                WithholdingTaxRate = dto.WithholdingTaxRate,
                MatchingType = dto.MatchingType,
                MatchingStatus = InvoiceMatchingStatus.Unmatched,
                Status = VendorInvoiceStatus.Draft,
                ApprovalStatus = "Draft",
                ExpenseAccountId = dto.ExpenseAccountId,
                ApAccountId = dto.ApAccountId,
                Notes = dto.Notes,
                Reference = dto.Reference,
                CreatedAt = now,
                CreatedBy = UserName
            };

            // ── Process line items ──────────────────────────────────────
            decimal subtotal = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineGross = lineDto.Quantity * lineDto.UnitPrice;
                var lineDiscount = lineGross * (lineDto.DiscountPercentage / 100);
                var lineNet = lineGross - lineDiscount;
                var lineTax = lineNet * (lineDto.TaxRate / 100);

                var lineItem = new VendorInvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorInvoiceId = invoice.Id,
                    LineItemType = lineDto.LineItemType,
                    GLAccountId = lineDto.GLAccountId,
                    PurchaseOrderItemId = lineDto.PurchaseOrderItemId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxRate = lineDto.TaxRate,
                    TaxAmount = lineTax,
                    TaxCode = lineDto.TaxCode,
                    DiscountPercentage = lineDto.DiscountPercentage,
                    DiscountAmount = lineDiscount,
                    Unit = lineDto.Unit,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                invoice.LineItems.Add(lineItem);
                subtotal += lineNet;
                totalTax += lineTax;
                totalDiscount += lineDiscount;
            }

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.DiscountAmount = totalDiscount;
            invoice.TotalAmount = subtotal + totalTax;
            invoice.PaidAmount = 0;
            invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

            // WHT calculation
            if (invoice.WithholdingTaxRate > 0)
            {
                invoice.WithholdingTaxAmount = invoice.SubTotal * (invoice.WithholdingTaxRate / 100);
            }

            // Early payment discount amount
            if (invoice.EarlyPaymentDiscountPercentage > 0)
            {
                invoice.EarlyPaymentDiscountAmount = invoice.TotalAmount * (invoice.EarlyPaymentDiscountPercentage / 100);
            }

            await _unitOfWork.Repository<VendorInvoice>().AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created vendor invoice {InvoiceNumber} for supplier {SupplierId}", invoiceNumber, supplier.Id);

            return MapToDto(invoice);
        }

        public async Task<VendorInvoiceDto> UpdateAsync(VendorInvoiceUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == dto.Id)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{dto.Id}' not found.");

            if (invoice.Status != VendorInvoiceStatus.Draft && invoice.Status != VendorInvoiceStatus.Rejected)
                throw new InvalidOperationException("Only draft or rejected invoices can be updated.");

            var now = DateTime.UtcNow;
            invoice.SupplierInvoiceNumber = dto.SupplierInvoiceNumber;
            invoice.PurchaseOrderId = dto.PurchaseOrderId;
            invoice.InvoiceDate = dto.InvoiceDate;
            invoice.ReceivedDate = dto.ReceivedDate;
            invoice.DueDate = dto.DueDate;
            invoice.CurrencyCode = dto.CurrencyCode;
            invoice.ExchangeRate = dto.ExchangeRate;
            invoice.PaymentTermsDays = dto.PaymentTermsDays;
            invoice.EarlyPaymentDiscountPercentage = dto.EarlyPaymentDiscountPercentage;
            invoice.EarlyPaymentDiscountDueDate = dto.EarlyPaymentDiscountDueDate;
            invoice.WithholdingTaxRate = dto.WithholdingTaxRate;
            invoice.MatchingType = dto.MatchingType;
            invoice.ExpenseAccountId = dto.ExpenseAccountId;
            invoice.ApAccountId = dto.ApAccountId;
            invoice.Notes = dto.Notes;
            invoice.Reference = dto.Reference;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // If rejected, reset back to draft
            if (invoice.Status == VendorInvoiceStatus.Rejected)
            {
                invoice.Status = VendorInvoiceStatus.Draft;
                invoice.ApprovalStatus = "Draft";
            }

            // Remove existing line items and recreate
            foreach (var existing in invoice.LineItems.ToList())
            {
                await _unitOfWork.Repository<VendorInvoiceLineItem>().DeleteAsync(existing);
            }
            invoice.LineItems.Clear();

            decimal subtotal = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineGross = lineDto.Quantity * lineDto.UnitPrice;
                var lineDiscount = lineGross * (lineDto.DiscountPercentage / 100);
                var lineNet = lineGross - lineDiscount;
                var lineTax = lineNet * (lineDto.TaxRate / 100);

                var lineItem = new VendorInvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorInvoiceId = invoice.Id,
                    LineItemType = lineDto.LineItemType,
                    GLAccountId = lineDto.GLAccountId,
                    PurchaseOrderItemId = lineDto.PurchaseOrderItemId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxRate = lineDto.TaxRate,
                    TaxAmount = lineTax,
                    TaxCode = lineDto.TaxCode,
                    DiscountPercentage = lineDto.DiscountPercentage,
                    DiscountAmount = lineDiscount,
                    Unit = lineDto.Unit,
                    CreatedAt = now,
                    CreatedBy = UserName
                };

                invoice.LineItems.Add(lineItem);
                subtotal += lineNet;
                totalTax += lineTax;
                totalDiscount += lineDiscount;
            }

            invoice.SubTotal = subtotal;
            invoice.TaxAmount = totalTax;
            invoice.DiscountAmount = totalDiscount;
            invoice.TotalAmount = subtotal + totalTax;
            invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

            if (invoice.WithholdingTaxRate > 0)
                invoice.WithholdingTaxAmount = invoice.SubTotal * (invoice.WithholdingTaxRate / 100);

            if (invoice.EarlyPaymentDiscountPercentage > 0)
                invoice.EarlyPaymentDiscountAmount = invoice.TotalAmount * (invoice.EarlyPaymentDiscountPercentage / 100);

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated vendor invoice {InvoiceId}", invoice.Id);

            return MapToDto(invoice);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == id);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            if (invoice.Status != VendorInvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be deleted.");

            await _unitOfWork.Repository<VendorInvoice>().DeleteAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted vendor invoice {InvoiceId}", id);
        }

        // ═════════════════════════════════════════════════════════════════
        //  APPROVAL WORKFLOW
        // ═════════════════════════════════════════════════════════════════

        public async Task<VendorInvoiceDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var invoice = await GetEntityOrThrowAsync(id, cancellationToken);

            if (invoice.Status != VendorInvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be submitted for approval.");

            // Validate matching if configured
            if (invoice.MatchingType != InvoiceMatchingType.None && invoice.MatchingStatus == InvoiceMatchingStatus.Unmatched)
            {
                _logger.LogWarning("Invoice {InvoiceNumber} submitted without matching — matching type is {MatchingType}",
                    invoice.InvoiceNumber, invoice.MatchingType);
            }

            var now = DateTime.UtcNow;
            invoice.Status = VendorInvoiceStatus.PendingApproval;
            invoice.ApprovalStatus = "PendingApproval";
            invoice.SubmittedById = _currentUser.UserId != null ? Guid.Parse(_currentUser.UserId) : null;
            invoice.SubmittedDate = now;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Vendor invoice {InvoiceNumber} submitted for approval", invoice.InvoiceNumber);
            return MapToDto(invoice);
        }

        public async Task<VendorInvoiceDto> ApproveAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            if (invoice.Status != VendorInvoiceStatus.PendingApproval)
                throw new InvalidOperationException("Only pending invoices can be approved.");

            var now = DateTime.UtcNow;
            invoice.Status = VendorInvoiceStatus.Approved;
            invoice.ApprovalStatus = "Approved";
            invoice.ApprovedById = _currentUser.UserId != null ? Guid.Parse(_currentUser.UserId) : null;
            invoice.ApprovedDate = now;
            invoice.ApprovalComments = comments;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            // Process Inventory Receivals for Inventory lines
            foreach (var line in invoice.LineItems.Where(l => l.LineItemType == "Inventory"))
            {
                if (line.InventoryItemId.HasValue && line.WarehouseId.HasValue)
                {
                    await _inventoryValuationService.ProcessReceiptAsync(
                        line.InventoryItemId.Value,
                        line.WarehouseId.Value,
                        line.LocationId,
                        line.Quantity,
                        line.UnitPrice,
                        ErpSystem.Core.Enums.ReferenceType.VendorInvoice,
                        invoice.InvoiceNumber,
                        invoice.Id,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate
                    );
                }
            }

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Post to GL
            await _subledgerPostingService.PostApInvoiceAsync(invoice.Id, cancellationToken);

            _logger.LogInformation("Approved vendor invoice {InvoiceNumber} and posted to GL", invoice.InvoiceNumber);

            return await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice);
        }

        public async Task<VendorInvoiceDto> RejectAsync(Guid id, string comments, CancellationToken cancellationToken = default)
        {
            var invoice = await GetEntityOrThrowAsync(id, cancellationToken);

            if (invoice.Status != VendorInvoiceStatus.PendingApproval)
                throw new InvalidOperationException("Only pending invoices can be rejected.");

            var now = DateTime.UtcNow;
            invoice.Status = VendorInvoiceStatus.Rejected;
            invoice.ApprovalStatus = "Rejected";
            invoice.ApprovalComments = comments;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Rejected vendor invoice {InvoiceNumber}. Reason: {Comments}", invoice.InvoiceNumber, comments);
            return MapToDto(invoice);
        }

        public async Task<VendorInvoiceDto> VoidAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == id)
                .Include(i => i.PaymentAllocations)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null)
                throw new KeyNotFoundException($"Vendor invoice with Id '{id}' not found.");

            if (invoice.Status == VendorInvoiceStatus.Voided)
                throw new InvalidOperationException("Invoice is already voided.");

            if (invoice.PaymentAllocations.Any(a => !a.IsReversal))
                throw new InvalidOperationException("Cannot void an invoice with active payment allocations. Reverse payments first.");

            var now = DateTime.UtcNow;
            invoice.Status = VendorInvoiceStatus.Voided;
            invoice.Notes = $"{invoice.Notes}\n\nVoided on {now:yyyy-MM-dd HH:mm}: {reason}";
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Voided vendor invoice {InvoiceNumber}. Reason: {Reason}", invoice.InvoiceNumber, reason);
            return MapToDto(invoice);
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

            var result = new InvoiceMatchingResultDto
            {
                VendorInvoiceId = invoiceId,
                MatchingType = InvoiceMatchingType.TwoWay,
                InvoiceTotal = invoice.TotalAmount,
                PurchaseOrderTotal = po.TotalAmount,
                TolerancePercentage = 1.0m
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
                    if (priceVariancePct > result.TolerancePercentage)
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
                    if (qtyVariancePct > result.TolerancePercentage)
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
        {
            // Start with 2-way match
            var result = await PerformTwoWayMatchAsync(invoiceId, cancellationToken);
            result.MatchingType = InvoiceMatchingType.ThreeWay;

            var invoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Id == invoiceId)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice?.PurchaseOrderId == null) return result;

            // Fetch goods receipts for this PO
            var receipts = await _unitOfWork.Repository<PurchaseOrderReceipt>()
                .GetQueryable(r => r.TenantId == TenantId && r.PurchaseOrderId == invoice.PurchaseOrderId.Value)
                .ToListAsync(cancellationToken);

            if (!receipts.Any())
            {
                result.IsMatched = false;
                result.MatchingStatus = InvoiceMatchingStatus.MatchException;
                result.Discrepancies.Add(new MatchingDiscrepancyDto
                {
                    ItemDescription = "Goods Receipt",
                    DiscrepancyType = "Missing",
                    InvoiceValue = invoice.TotalAmount
                });
            }
            else
            {
                var totalReceived = receipts.Sum(r => r.Items.Sum(i => i.ReceivedQuantity));
                var totalInvoiced = invoice.LineItems.Sum(l => l.Quantity);

                if (totalReceived > 0)
                {
                    var qtyVariance = Math.Abs(totalInvoiced - totalReceived);
                    var qtyVariancePct = qtyVariance / totalReceived * 100;

                    if (qtyVariancePct > result.TolerancePercentage)
                    {
                        result.IsMatched = false;
                        result.MatchingStatus = InvoiceMatchingStatus.MatchException;
                        result.Discrepancies.Add(new MatchingDiscrepancyDto
                        {
                            ItemDescription = "Received Quantity",
                            DiscrepancyType = "Quantity",
                            InvoiceValue = totalInvoiced,
                            ExpectedValue = totalReceived,
                            Variance = qtyVariance,
                            VariancePercentage = qtyVariancePct
                        });
                    }
                    else if (result.IsMatched)
                    {
                        result.MatchingStatus = InvoiceMatchingStatus.ThreeWayMatched;
                    }
                }

                result.GoodsReceiptTotal = totalReceived;
            }

            invoice.MatchingStatus = result.MatchingStatus;
            invoice.MatchingNotes = result.IsMatched
                ? "3-way match passed"
                : $"3-way match failed — {result.Discrepancies.Count} discrepancies found";
            invoice.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("3-way matching for invoice {InvoiceNumber}: {Status}",
                invoice.InvoiceNumber, result.MatchingStatus);

            return result;
        }

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
                InvoiceTotal = invoice.TotalAmount
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  UTILITIES
        // ═════════════════════════════════════════════════════════════════

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
            var prefix = "VI";
            var currentYear = DateTime.UtcNow.Year;

            var lastInvoice = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.InvoiceNumber.StartsWith($"{prefix}-{currentYear}"))
                .OrderByDescending(i => i.InvoiceNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastInvoice == null)
                return $"{prefix}-{currentYear}-00001";

            var parts = lastInvoice.InvoiceNumber.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var lastSeq))
                return $"{prefix}-{currentYear}-{(lastSeq + 1):00000}";

            return $"{prefix}-{currentYear}-00001";
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
                BaseCurrencyAmount = invoice.BaseCurrencyAmount,
                PaymentTermsDays = invoice.PaymentTermsDays,
                EarlyPaymentDiscountPercentage = invoice.EarlyPaymentDiscountPercentage,
                EarlyPaymentDiscountDueDate = invoice.EarlyPaymentDiscountDueDate,
                EarlyPaymentDiscountAmount = invoice.EarlyPaymentDiscountAmount,
                WithholdingTaxRate = invoice.WithholdingTaxRate,
                WithholdingTaxAmount = invoice.WithholdingTaxAmount,
                MatchingType = invoice.MatchingType,
                MatchingStatus = invoice.MatchingStatus,
                MatchingNotes = invoice.MatchingNotes,
                Status = invoice.Status,
                ApprovalStatus = invoice.ApprovalStatus,
                ExpenseAccountId = invoice.ExpenseAccountId,
                ExpenseAccountName = invoice.ExpenseAccount?.AccountName,
                ApAccountId = invoice.ApAccountId,
                ApAccountName = invoice.ApAccount?.AccountName,
                Notes = invoice.Notes,
                Reference = invoice.Reference,
                LineItems = invoice.LineItems.Select(li => new VendorInvoiceLineItemDto
                {
                    Id = li.Id,
                    VendorInvoiceId = li.VendorInvoiceId,
                    LineItemType = li.LineItemType,
                    GLAccountId = li.GLAccountId,
                    GLAccountName = li.GLAccount?.AccountName,
                    PurchaseOrderItemId = li.PurchaseOrderItemId,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    LineTotal = li.Quantity * li.UnitPrice,
                    TaxRate = li.TaxRate,
                    TaxAmount = li.TaxAmount,
                    TaxCode = li.TaxCode,
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
                    DiscountAmount = a.DiscountAmount,
                    WithholdingTaxAmount = a.WithholdingTaxAmount,
                    AllocationDate = a.AllocationDate,
                    Notes = a.Notes,
                    IsReversal = a.IsReversal
                }).ToList() ?? new List<VendorPaymentAllocationDto>(),
                CreatedAt = invoice.CreatedAt,
                UpdatedAt = invoice.UpdatedAt
            };
        }
    }
}
