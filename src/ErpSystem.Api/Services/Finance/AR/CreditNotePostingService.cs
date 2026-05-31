using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Finance.AR;

/// <summary>
/// Financial orchestration service for credit notes (Option B service split).
/// Handles GL posting, AR balance reduction, tax reversal, unapplied credit tracking,
/// and inventory/COGS reversal for return-linked credit notes.
/// 
/// Separated from ReturnOrderService which handles CRUD/status only.
/// </summary>
public class CreditNotePostingService : ICreditNotePostingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubledgerPostingService _subledgerPostingService;
    private readonly ITaxAuditService _taxAuditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreditNotePostingService> _logger;

    public CreditNotePostingService(
        IUnitOfWork unitOfWork,
        ISubledgerPostingService subledgerPostingService,
        ITaxAuditService taxAuditService,
        ICurrentUserService currentUserService,
        ILogger<CreditNotePostingService> logger)
    {
        _unitOfWork = unitOfWork;
        _subledgerPostingService = subledgerPostingService;
        _taxAuditService = taxAuditService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
    private string UserName => _currentUserService.UserName ?? "system";

    public async Task<CreditNoteDetailDto> PostCreditNoteAsync(Guid creditNoteId, Guid? invoiceId = null, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var creditNote = await _unitOfWork.Repository<CreditNote>()
                .GetQueryable(cn => cn.Id == creditNoteId && cn.TenantId == TenantId)
                .Include(cn => cn.BusinessPartner)
                .Include(cn => cn.Lines)
                .Include(cn => cn.OriginalInvoice)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Credit Note {creditNoteId} not found.");

            if (creditNote.CreditNoteStatus != CreditNoteStatus.Approved)
                throw new InvalidOperationException("Only approved credit notes can be posted/applied.");

            // Resolve the target invoice
            var targetInvoiceId = invoiceId ?? creditNote.OriginalInvoiceId ?? creditNote.AppliedToInvoiceId;
            Invoice? invoice = null;

            if (targetInvoiceId.HasValue)
            {
                invoice = await _unitOfWork.Repository<Invoice>()
                    .GetQueryable(i => i.Id == targetInvoiceId.Value && i.TenantId == TenantId)
                    .Include(i => i.BusinessPartner)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Invoice {targetInvoiceId.Value} not found.");

                if (invoice.Status == InvoiceStatus.Cancelled)
                    throw new InvalidOperationException("Cannot apply credit note to a cancelled invoice.");
            }

            // Determine exchange rate: linked credit notes inherit the target invoice rate
            if (invoice != null)
            {
                creditNote.ExchangeRate = invoice.ExchangeRate;
                creditNote.Currency = invoice.CurrencyCode;
            }
            else if (creditNote.OriginalInvoice != null)
            {
                creditNote.ExchangeRate = creditNote.OriginalInvoice.ExchangeRate;
                creditNote.Currency = creditNote.OriginalInvoice.CurrencyCode;
            }

            var exchangeRate = creditNote.ExchangeRate > 0 ? creditNote.ExchangeRate : 1m;
            var now = DateTime.UtcNow;

            // ── Step 1: Calculate application amounts ──
            decimal appliedForeignAmount;
            decimal appliedBaseAmount;

            if (invoice != null)
            {
                // Apply capped at invoice balance
                appliedForeignAmount = Math.Min(creditNote.TotalAmount, invoice.BalanceAmount);
                appliedBaseAmount = Math.Round(appliedForeignAmount * exchangeRate, 2);

                // Update Invoice.CreditedAmount (not PaidAmount — credit notes are not payments)
                invoice.CreditedAmount += appliedForeignAmount;

                // Transition invoice status
                if (invoice.BalanceAmount <= 0)
                    invoice.Status = InvoiceStatus.Paid;
                else if (invoice.CreditedAmount > 0 || invoice.PaidAmount > 0)
                    invoice.Status = InvoiceStatus.PartiallyPaid;

                await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
            }
            else
            {
                // No target invoice — entire amount is unapplied customer credit
                appliedForeignAmount = 0;
                appliedBaseAmount = 0;
            }

            // ── Step 2: Update customer outstanding balance (base currency) ──
            var customer = creditNote.BusinessPartner;
            var totalBaseCreditAmount = Math.Round(creditNote.TotalAmount * exchangeRate, 2);
            customer.OutstandingBalance -= totalBaseCreditAmount;
            customer.UpdatedAt = now;
            customer.UpdatedBy = UserName;
            await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(customer);

            // ── Step 3: Record CreditNoteApplication ──
            if (invoice != null && appliedForeignAmount > 0)
            {
                var application = new CreditNoteApplication
                {
                    CreditNoteId = creditNote.Id,
                    InvoiceId = invoice.Id,
                    AppliedForeignAmount = appliedForeignAmount,
                    AppliedBaseAmount = appliedBaseAmount,
                    AppliedAt = now,
                    AppliedBy = UserName,
                    IsReversed = false,
                    TenantId = TenantId
                };
                await _unitOfWork.Repository<CreditNoteApplication>().AddAsync(application);
            }

            // ── Step 4: Update credit note tracking ──
            creditNote.CreditNoteStatus = CreditNoteStatus.Applied;
            creditNote.AppliedDate = now;
            creditNote.AppliedToInvoiceId = invoice?.Id;
            creditNote.AppliedAmount = appliedForeignAmount;
            await _unitOfWork.Repository<CreditNote>().UpdateAsync(creditNote);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // ── Step 5: Post GL — Dr Revenue / Dr Output VAT / Cr AR Control ──
            await _subledgerPostingService.PostCreditNoteGLAsync(creditNoteId, cancellationToken);

            // ── Step 6: Record tax audit trail with negative amounts (credit note reduces VAT liability) ──
            var taxResult = new Core.DTOs.Finance.TaxCalculationResultDto
            {
                BaseAmount = -(creditNote.TotalAmount - creditNote.TaxAmount),
                GrandTotal = -creditNote.TotalAmount,
                TotalTaxAmount = -creditNote.TaxAmount
            };
            await _taxAuditService.RecordTaxCalculationsAsync("CreditNote", creditNote.Id, taxResult, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Posted Credit Note {DocNumber}: Applied {AppliedAmount} to Invoice {InvoiceId}, Unapplied: {UnappliedAmount}",
                creditNote.DocumentNumber, appliedForeignAmount, invoice?.Id, creditNote.UnappliedAmount);

            return await GetCreditNoteByIdAsync(creditNote.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to retrieve posted credit note.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CreditNoteDetailDto> VoidCreditNoteAsync(Guid creditNoteId, string reason, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var creditNote = await _unitOfWork.Repository<CreditNote>()
                .GetQueryable(cn => cn.Id == creditNoteId && cn.TenantId == TenantId)
                .Include(cn => cn.BusinessPartner)
                .Include(cn => cn.Applications)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Credit Note {creditNoteId} not found.");

            if (creditNote.CreditNoteStatus == CreditNoteStatus.Voided)
                throw new InvalidOperationException("Credit note is already voided.");

            var now = DateTime.UtcNow;
            var exchangeRate = creditNote.ExchangeRate > 0 ? creditNote.ExchangeRate : 1m;

            // ── Step 1: Reverse all applications ──
            var activeApplications = creditNote.Applications.Where(a => !a.IsReversed).ToList();
            foreach (var app in activeApplications)
            {
                var invoice = await _unitOfWork.Repository<Invoice>()
                    .GetQueryable(i => i.Id == app.InvoiceId && i.TenantId == TenantId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (invoice != null)
                {
                    // Restore Invoice.CreditedAmount
                    invoice.CreditedAmount -= app.AppliedForeignAmount;
                    if (invoice.CreditedAmount < 0) invoice.CreditedAmount = 0;

                    // Recalculate status
                    if (invoice.BalanceAmount > 0 && invoice.Status == InvoiceStatus.Paid)
                    {
                        invoice.Status = (invoice.PaidAmount > 0 || invoice.CreditedAmount > 0)
                            ? InvoiceStatus.PartiallyPaid
                            : InvoiceStatus.Sent;
                    }

                    await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);
                }

                app.IsReversed = true;
                await _unitOfWork.Repository<CreditNoteApplication>().UpdateAsync(app);
            }

            // ── Step 2: Restore customer outstanding balance ──
            var totalBaseCreditAmount = Math.Round(creditNote.TotalAmount * exchangeRate, 2);
            creditNote.BusinessPartner.OutstandingBalance += totalBaseCreditAmount;
            creditNote.BusinessPartner.UpdatedAt = now;
            creditNote.BusinessPartner.UpdatedBy = UserName;
            await _unitOfWork.Repository<BusinessPartner>().UpdateAsync(creditNote.BusinessPartner);

            // ── Step 3: Update credit note status ──
            creditNote.CreditNoteStatus = CreditNoteStatus.Voided;
            creditNote.AppliedAmount = 0;
            await _unitOfWork.Repository<CreditNote>().UpdateAsync(creditNote);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // ── Step 4: Reverse GL journals ──
            await _subledgerPostingService.ReverseCreditNoteGLAsync(creditNoteId, reason, cancellationToken);

            // ── Step 5: Reverse tax audit trail ──
            await _taxAuditService.ReverseTaxCalculationsAsync("CreditNote", creditNote.Id, reason, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Voided Credit Note {DocNumber}. Reason: {Reason}. Restored {BaseAmount} to customer balance.",
                creditNote.DocumentNumber, reason, totalBaseCreditAmount);

            return await GetCreditNoteByIdAsync(creditNote.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to retrieve voided credit note.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CreditNoteDetailDto> ApplyUnappliedCreditAsync(
        Guid creditNoteId,
        Guid targetInvoiceId,
        decimal? amountToApply = null,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var creditNote = await _unitOfWork.Repository<CreditNote>()
                .GetQueryable(cn => cn.Id == creditNoteId && cn.TenantId == TenantId)
                .Include(cn => cn.BusinessPartner)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Credit Note {creditNoteId} not found.");

            // ── Validation 1: Credit Note must be in a posted state with unapplied credit ──
            if (creditNote.CreditNoteStatus != CreditNoteStatus.Applied && creditNote.CreditNoteStatus != CreditNoteStatus.Approved)
                throw new InvalidOperationException($"Credit Note {creditNote.DocumentNumber} cannot be applied from status {creditNote.CreditNoteStatus}.");

            if (creditNote.UnappliedAmount <= 0)
                throw new InvalidOperationException($"Credit Note {creditNote.DocumentNumber} has no unapplied credit remaining.");

            // ── Validation 2: Load target invoice ──
            var invoice = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.Id == targetInvoiceId && i.TenantId == TenantId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Invoice {targetInvoiceId} not found.");

            if (invoice.Status == InvoiceStatus.Cancelled)
                throw new InvalidOperationException("Cannot apply credit to a cancelled invoice.");

            if (invoice.BalanceAmount <= 0)
                throw new InvalidOperationException($"Invoice {invoice.InvoiceNumber} has no outstanding balance.");

            // ── Validation 3: Same customer ──
            if (invoice.BusinessPartnerId != creditNote.BusinessPartnerId)
                throw new InvalidOperationException("Credit Note and target Invoice must belong to the same customer.");

            // ── Validation 4: Same transaction currency ──
            if (!string.Equals(creditNote.Currency, invoice.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Currency mismatch: Credit Note is in {creditNote.Currency} but Invoice is in {invoice.CurrencyCode}. " +
                    "Cross-currency credit application is not yet supported.");

            var now = DateTime.UtcNow;
            var storedExchangeRate = creditNote.ExchangeRate > 0 ? creditNote.ExchangeRate : 1m;

            // ── Step 1: Calculate application amount (transaction currency) ──
            var requestedAmount = amountToApply ?? creditNote.UnappliedAmount;
            var appliedForeignAmount = Math.Min(requestedAmount, Math.Min(creditNote.UnappliedAmount, invoice.BalanceAmount));
            var appliedBaseAmount = Math.Round(appliedForeignAmount * storedExchangeRate, 2);

            // ── Step 2: Create CreditNoteApplication subledger record ──
            var application = new CreditNoteApplication
            {
                CreditNoteId = creditNote.Id,
                InvoiceId = invoice.Id,
                AppliedForeignAmount = appliedForeignAmount,
                AppliedBaseAmount = appliedBaseAmount,
                AppliedAt = now,
                AppliedBy = UserName,
                IsReversed = false,
                TenantId = TenantId
            };
            await _unitOfWork.Repository<CreditNoteApplication>().AddAsync(application);

            // ── Step 3: Update Invoice.CreditedAmount (transaction currency) ──
            invoice.CreditedAmount += appliedForeignAmount;

            if (invoice.BalanceAmount <= 0)
                invoice.Status = InvoiceStatus.Paid;
            else if (invoice.CreditedAmount > 0 || invoice.PaidAmount > 0)
                invoice.Status = InvoiceStatus.PartiallyPaid;

            await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice);

            // ── Step 4: Update CreditNote.AppliedAmount (transaction currency) ──
            creditNote.AppliedAmount += appliedForeignAmount;
            await _unitOfWork.Repository<CreditNote>().UpdateAsync(creditNote);

            // ── IMPORTANT: No BusinessPartner.OutstandingBalance update ──
            // The customer's net AR was already reduced when the Credit Note was originally posted.
            // Reapplication only changes WHICH invoice is settled, not the customer's net balance.

            // ── IMPORTANT: No GL or Tax side effects ──
            // The original PostCreditNoteAsync already posted Dr Revenue/VAT / Cr AR and
            // recorded the tax audit trail. This is purely a subledger allocation.

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Applied {Amount} {Currency} unapplied credit from Credit Note {CnNumber} to Invoice {InvNumber}. " +
                "Remaining unapplied: {UnappliedAmount}",
                appliedForeignAmount, creditNote.Currency,
                creditNote.DocumentNumber, invoice.InvoiceNumber,
                creditNote.UnappliedAmount);

            return await GetCreditNoteByIdAsync(creditNote.Id, cancellationToken)
                ?? throw new InvalidOperationException("Failed to retrieve credit note after reapplication.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<CreditNoteDetailDto?> GetCreditNoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cn = await _unitOfWork.Repository<CreditNote>()
            .GetQueryable(c => c.Id == id)
            .Include(c => c.BusinessPartner)
            .Include(c => c.ReturnOrder!)
            .Include(c => c.Lines)
            .Include(c => c.Applications!)
                .ThenInclude(a => a.Invoice)
            .FirstOrDefaultAsync(cancellationToken);
        return cn == null ? null : MapCreditNoteDetailDto(cn);
    }

    private static CreditNoteDetailDto MapCreditNoteDetailDto(CreditNote c) => new()
    {
        Id = c.Id,
        DocumentNumber = c.DocumentNumber,
        CreditNoteStatus = c.CreditNoteStatus,
        CustomerName = c.BusinessPartner?.PartnerName,
        TotalAmount = c.TotalAmount,
        AppliedAmount = c.AppliedAmount,
        UnappliedAmount = c.UnappliedAmount,
        Currency = c.Currency ?? "USD",
        ExchangeRate = c.ExchangeRate,
        Reason = c.Reason,
        AppliedDate = c.AppliedDate,
        LineCount = c.Lines?.Count ?? 0,
        CreatedAt = c.CreatedAt,
        BusinessPartnerId = c.BusinessPartnerId,
        ReturnOrderId = c.ReturnOrderId,
        ReturnOrderNumber = c.ReturnOrder?.DocumentNumber,
        OriginalInvoiceId = c.OriginalInvoiceId,
        AppliedToInvoiceId = c.AppliedToInvoiceId,
        TaxAmount = c.TaxAmount,
        Lines = c.Lines?.Select(l => new CreditNoteLineDto
        {
            Id = l.Id,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.Quantity * l.UnitPrice,
            TaxAmount = l.TaxAmount,
            TaxCode = l.TaxCode
        }).ToList() ?? new(),
        Applications = c.Applications?.Select(a => new CreditNoteApplicationDto
        {
            Id = a.Id,
            InvoiceId = a.InvoiceId,
            InvoiceNumber = a.Invoice?.InvoiceNumber,
            AppliedForeignAmount = a.AppliedForeignAmount,
            AppliedBaseAmount = a.AppliedBaseAmount,
            AppliedAt = a.AppliedAt,
            AppliedBy = a.AppliedBy,
            IsReversed = a.IsReversed
        }).ToList() ?? new()
    };
}
