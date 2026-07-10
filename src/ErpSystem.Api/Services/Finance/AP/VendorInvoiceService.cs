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
    /// Manages vendor invoice lifecycle: creation, matching, approval, and voiding.
    /// </summary>
    public class VendorInvoiceService : IVendorInvoiceService
    {
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
            IFixedAssetService? fixedAssetService = null)
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

        public async Task<VendorInvoiceDto> CreateAsync(VendorInvoiceCreateDto dto, CancellationToken cancellationToken = default)
        {
            var supplier = await ResolveSupplierForInvoiceAsync(dto.SupplierId, cancellationToken);

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
                CurrencyCode = dto.CurrencyCode,
                ExchangeRate = dto.ExchangeRate,
                PaymentTermsDays = paymentTermsDays,
                PaymentTermId = paymentTerm?.Id,
                EarlyPaymentDiscountPercentage = earlyPaymentDiscountPercentage,
                EarlyPaymentDiscountDueDate = earlyPaymentDiscountDueDate,
                WithholdingTaxRate = dto.WithholdingTaxRate,
                WithholdingTaxId = dto.WithholdingTaxId,
                WithholdingTaxAccountId = dto.WithholdingTaxAccountId,
                WithholdingCertificateNumber = dto.WithholdingCertificateNumber,
                WithholdingCertificateDate = dto.WithholdingCertificateDate,
                MatchingType = dto.MatchingType,
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

            // ── Process line items ──────────────────────────────────────
            decimal subtotal = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var lineGross = lineDto.Quantity * lineDto.UnitPrice;
                var lineDiscount = lineGross * (lineDto.DiscountPercentage / 100);
                var lineNet = lineGross - lineDiscount;
                var lineTaxRate = lineDto.TaxTreatment == TaxTreatment.Standard ? lineDto.TaxRate : 0m;
                var lineTax = lineDto.TaxTreatment == TaxTreatment.Standard
                    ? lineNet * (lineTaxRate / 100)
                    : 0m;

                var lineItem = new VendorInvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorInvoiceId = invoice.Id,
                    LineItemType = lineDto.LineItemType,
                    GLAccountId = lineDto.GLAccountId,
                    FixedAssetId = lineDto.FixedAssetId,
                    PurchaseOrderItemId = lineDto.PurchaseOrderItemId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxGroupId = lineDto.TaxGroupId,
                    TaxTreatment = lineDto.TaxTreatment,
                    TaxRate = lineTaxRate,
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

            if (invoice.JournalEntryId.HasValue)
                throw new InvalidOperationException("Posted vendor invoices cannot be updated. Use a reversal, credit note, or adjustment.");

            if (invoice.Status != VendorInvoiceStatus.Draft && invoice.Status != VendorInvoiceStatus.Rejected)
                throw new InvalidOperationException("Only draft or rejected invoices can be updated.");

            var supplier = await _unitOfWork.Repository<Supplier>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == invoice.SupplierId);
            var paymentTerm = await ResolvePaymentTermAsync(dto.PaymentTermId ?? supplier?.PaymentTermId, "Supplier", cancellationToken);
            var paymentTermsDays = paymentTerm?.DueDays ?? dto.PaymentTermsDays;
            var earlyPaymentDiscountPercentage = paymentTerm?.DiscountPercent ?? dto.EarlyPaymentDiscountPercentage;
            var earlyPaymentDiscountDueDate = paymentTerm != null && paymentTerm.DiscountPercent > 0 && paymentTerm.DiscountDays > 0
                ? dto.InvoiceDate.AddDays(paymentTerm.DiscountDays)
                : dto.EarlyPaymentDiscountDueDate;

            var now = DateTime.UtcNow;
            invoice.SupplierInvoiceNumber = dto.SupplierInvoiceNumber;
            invoice.PurchaseOrderId = dto.PurchaseOrderId;
            invoice.InvoiceDate = dto.InvoiceDate;
            invoice.ReceivedDate = dto.ReceivedDate;
            invoice.DueDate = dto.DueDate ?? dto.InvoiceDate.AddDays(paymentTermsDays);
            invoice.CurrencyCode = dto.CurrencyCode;
            invoice.ExchangeRate = dto.ExchangeRate;
            invoice.PaymentTermsDays = paymentTermsDays;
            invoice.PaymentTermId = paymentTerm?.Id;
            invoice.EarlyPaymentDiscountPercentage = earlyPaymentDiscountPercentage;
            invoice.EarlyPaymentDiscountDueDate = earlyPaymentDiscountDueDate;
            invoice.WithholdingTaxRate = dto.WithholdingTaxRate;
            invoice.WithholdingTaxId = dto.WithholdingTaxId;
            invoice.WithholdingTaxAccountId = dto.WithholdingTaxAccountId;
            invoice.WithholdingCertificateNumber = dto.WithholdingCertificateNumber;
            invoice.WithholdingCertificateDate = dto.WithholdingCertificateDate;
            invoice.MatchingType = dto.MatchingType;
            invoice.ExpenseAccountId = dto.ExpenseAccountId;
            invoice.ApAccountId = dto.ApAccountId;
            invoice.Notes = dto.Notes;
            invoice.Reference = dto.Reference;
            invoice.IsOpeningBalance = dto.IsOpeningBalance;
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
                var lineTaxRate = lineDto.TaxTreatment == TaxTreatment.Standard ? lineDto.TaxRate : 0m;
                var lineTax = lineDto.TaxTreatment == TaxTreatment.Standard
                    ? lineNet * (lineTaxRate / 100)
                    : 0m;

                var lineItem = new VendorInvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    VendorInvoiceId = invoice.Id,
                    LineItemType = lineDto.LineItemType,
                    GLAccountId = lineDto.GLAccountId,
                    FixedAssetId = lineDto.FixedAssetId,
                    PurchaseOrderItemId = lineDto.PurchaseOrderItemId,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    TaxGroupId = lineDto.TaxGroupId,
                    TaxTreatment = lineDto.TaxTreatment,
                    TaxRate = lineTaxRate,
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
            invoice.SubmittedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
            invoice.SubmittedDate = now;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = UserName;

            await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("VendorInvoice", id);
            if (!workflowResult.Success)
            {
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

            // Process Inventory Receivals for Inventory lines
            foreach (var line in invoice.LineItems.Where(l => !invoice.IsOpeningBalance && l.LineItemType == "Inventory"))
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

            await PostAsync(invoice.Id, cancellationToken);

            _logger.LogInformation("Approved vendor invoice {InvoiceNumber} and posted to GL through the finance posting engine", invoice.InvoiceNumber);

            return await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice);
        }

        public async Task<VendorInvoiceDto> PostAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Central finance posting engine is not configured for AP invoice posting.");

            var invoice = await LoadInvoiceForPostingAsync(id, cancellationToken);
            var wasAlreadyLinked = invoice.JournalEntryId.HasValue;
            var hasFixedAssetLines = invoice.LineItems.Any(IsFixedAssetLine);
            if (hasFixedAssetLines && _fixedAssetService == null)
            {
                throw new InvalidOperationException("Fixed asset capitalization service is not configured for AP fixed asset lines.");
            }

            try
            {
                var postingRequest = await BuildApInvoicePostingRequestAsync(invoice, cancellationToken);
                var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

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

                if (hasFixedAssetLines)
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

                return await GetByIdAsync(invoice.Id, cancellationToken) ?? MapToDto(invoice);
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
            var invoice = await GetEntityOrThrowAsync(id, cancellationToken);

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

        private async Task<FinancePostingRequestDto> BuildApInvoicePostingRequestAsync(
            VendorInvoice invoice,
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

            if (invoice.IsOpeningBalance)
            {
                throw new InvalidOperationException("Opening-balance AP invoice posting is deferred to the data migration/opening balance posting batch.");
            }

            if (!string.Equals(invoice.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AP invoice workflow approval is not complete.");
            }

            if (invoice.LineItems.Count == 0)
                throw new InvalidOperationException("AP invoice has no lines to post.");

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

            var linkedFinanceReceipt = await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.VendorInvoiceId == invoice.Id && !r.IsDeleted);
            var clearsFinanceGrv = linkedFinanceReceipt != null;
            Guid? grvAccrualAccountId = null;
            if (clearsFinanceGrv)
            {
                grvAccrualAccountId = settings.ControlAccountGRVAccrualId
                    ?? throw new InvalidOperationException("GRV accrual control account is not configured for this tenant.");
                await ResolvePostingAccountAsync(grvAccrualAccountId.Value, "GRV accrual account", accountCache, allowControlAccount: true, requireDirectPosting: false, cancellationToken);
            }

            var postingLines = new List<FinancePostingLineDto>();
            var documentDiscountAmount = 0m;
            var lineNumber = 1;

            foreach (var line in invoice.LineItems.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id))
            {
                if (line.TenantId != tenantId || line.VendorInvoiceId != invoice.Id)
                    throw new InvalidOperationException("AP invoice line belongs to another tenant or document.");

                var grossAmount = RoundMoney(line.Quantity * line.UnitPrice);
                if (grossAmount <= 0m)
                {
                    continue;
                }

                if (line.DiscountAmount < 0m || line.TaxAmount < 0m)
                    throw new InvalidOperationException("AP invoice line discount and tax amounts cannot be negative.");

                if (clearsFinanceGrv)
                {
                    if (IsFixedAssetLine(line))
                    {
                        throw new InvalidOperationException("Fixed asset capitalization from GRV accrual clearing is deferred until procurement receipt capitalization is modeled.");
                    }

                    var lineNetAmount = RoundMoney(grossAmount - line.DiscountAmount);
                    if (lineNetAmount <= 0m)
                    {
                        continue;
                    }

                    postingLines.Add(BuildPostingLine(
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
                        "AP-GRV"));
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
                    postingLines.Add(fixedAssetLine);
                    continue;
                }

                documentDiscountAmount += line.DiscountAmount;

                var debitAccountId = await ResolveDebitAccountForInvoiceLineAsync(
                    invoice,
                    supplier,
                    settings,
                    line,
                    accountCache,
                    cancellationToken);

                postingLines.Add(BuildPostingLine(
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
                    ResolveLineTag(line)));
            }

            if (!clearsFinanceGrv && documentDiscountAmount > 0m)
            {
                var discountAccountId = settings.DiscountReceivedAccountId
                    ?? throw new InvalidOperationException("Purchase discount received account is not configured for this tenant.");
                await ResolvePostingAccountAsync(discountAccountId, "purchase discount received account", accountCache, allowControlAccount: false, requireDirectPosting: true, cancellationToken);

                postingLines.Add(BuildPostingLine(
                    discountAccountId,
                    $"Purchase discount - {invoice.InvoiceNumber}",
                    debitForeignAmount: 0m,
                    creditForeignAmount: documentDiscountAmount,
                    invoiceCurrency,
                    functionalCurrency,
                    exchangeRate,
                    invoice.InvoiceDate,
                    invoice.InvoiceNumber,
                    lineNumber++,
                    "AP-Discount"));
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
                Lines = postingLines,
                TaxCalculationSnapshots = taxSnapshotLines
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

        private static TaxTransactionType ResolveApTaxTransactionType(VendorInvoiceLineItem line)
            => string.Equals(line.LineItemType, "Service", StringComparison.OrdinalIgnoreCase)
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
            string transactionTag)
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
                ForeignCurrencyAmount = isForeign
                    ? debitForeignAmount > 0m ? debitForeignAmount : creditForeignAmount
                    : null,
                ExchangeRate = isForeign ? exchangeRate : null,
                ExchangeRateSource = isForeign ? "AP invoice exchange-rate snapshot" : null,
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

        private static string BuildFixedAssetLineNotes(VendorInvoiceLineItem line)
            => line.FixedAssetId.HasValue
                ? $"VendorInvoiceLineId={line.Id:N};FixedAssetId={line.FixedAssetId.Value:N}"
                : $"VendorInvoiceLineId={line.Id:N}";

        private static bool IsNoTaxTreatment(TaxTreatment treatment)
            => treatment == TaxTreatment.Exempt
                || treatment == TaxTreatment.ZeroRated
                || treatment == TaxTreatment.OutOfScope;

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

        private static decimal RoundMoney(decimal amount)
            => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

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

        private async Task<PaymentTerm?> ResolvePaymentTermAsync(Guid? paymentTermId, string applicableTo, CancellationToken cancellationToken)
        {
            if (!paymentTermId.HasValue || paymentTermId.Value == Guid.Empty)
            {
                return null;
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

        private sealed record TaxPostingBuildResult(
            List<FinancePostingLineDto> Lines,
            List<FinanceTaxCalculationSnapshotDto> Snapshots);
    }
}
