using ErpSystem.Api.Services.Notifications;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/facilities/ar-billing")]
[Authorize]
public sealed class FacilitiesArBillingController : ControllerBase
{
    private const string SourceLabel = "Source: Estate / Facilities -> Finance AR";
    private readonly IInvoiceService _invoiceService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ApplicationDbContext _db;

    public FacilitiesArBillingController(
        IInvoiceService invoiceService,
        IPaymentService paymentService,
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        ApplicationDbContext db)
    {
        _invoiceService = invoiceService;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _currentUserService = currentUserService;
        _db = db;
    }

    [HttpPost("invoices")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager,Finance Officer")]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice(
        [FromBody] EstateFacilitiesArInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        request.Invoice.Notes = EnsureSourceLabel(request.Invoice.Notes);
        var invoice = await _invoiceService.CreateAsync(request.Invoice, cancellationToken);
        await NotifyBillingResultAsync(
            "Finance AR invoice created",
            $"Finance AR invoice {invoice.InvoiceNumber} was created from Estate / Facilities.",
            "estate.facilities.ar.invoice-created",
            "Invoice",
            invoice.Id,
            $"/finance/ar/invoices/{invoice.Id}",
            new Dictionary<string, object>
            {
                ["sourceLabel"] = SourceLabel,
                ["sourceModule"] = "Estate / Facilities",
                ["financeArReference"] = invoice.InvoiceNumber,
                ["businessPartnerId"] = invoice.BusinessPartnerId,
                ["customerName"] = invoice.CustomerName,
                ["amount"] = invoice.TotalAmount,
                ["currencyCode"] = invoice.CurrencyCode,
                ["sourceRecordReference"] = request.SourceRecordReference ?? string.Empty,
                ["propertyUnit"] = request.PropertyUnit ?? string.Empty
            },
            cancellationToken);

        return Ok(invoice);
    }

    [HttpPost("payments")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager,Finance Officer")]
    public async Task<ActionResult<CustomerPaymentDto>> CreatePayment(
        [FromBody] EstateFacilitiesArPaymentRequest request,
        CancellationToken cancellationToken)
    {
        request.Payment.Notes = EnsureSourceLabel(request.Payment.Notes);
        var payment = await _paymentService.CreateAsync(request.Payment, cancellationToken);
        await NotifyBillingResultAsync(
            "Finance AR receipt recorded",
            $"Finance AR receipt {payment.PaymentNumber} was recorded from Estate / Facilities.",
            "estate.facilities.ar.receipt-recorded",
            "CustomerPayment",
            payment.Id,
            $"/finance/ar/payments/{payment.Id}",
            new Dictionary<string, object>
            {
                ["sourceLabel"] = SourceLabel,
                ["sourceModule"] = "Estate / Facilities",
                ["financeArReference"] = payment.PaymentNumber,
                ["businessPartnerId"] = payment.BusinessPartnerId,
                ["customerName"] = payment.CustomerName,
                ["amount"] = payment.TotalAmount,
                ["currencyCode"] = payment.CurrencyCode,
                ["sourceRecordReference"] = request.SourceRecordReference ?? string.Empty,
                ["propertyUnit"] = request.PropertyUnit ?? string.Empty
            },
            cancellationToken);

        return Ok(payment);
    }

    [HttpPost("results")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager,Finance Officer")]
    public async Task<IActionResult> NotifyFinanceArResult(
        [FromBody] EstateFacilitiesArResultRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ActionType))
        {
            return BadRequest(new { success = false, message = "Finance AR action type is required." });
        }

        var title = FinanceArResultTitle(request.ActionType);
        var reference = string.IsNullOrWhiteSpace(request.FinanceArReference)
            ? "Finance AR"
            : $"Finance AR {request.FinanceArReference.Trim()}";
        var message = $"{reference} result was returned to Estate / Facilities: {title}.";

        await NotifyBillingResultAsync(
            title,
            message,
            $"estate.facilities.ar.{NormalizeNotificationToken(request.ActionType)}",
            "FinanceArResult",
            request.FinanceArEntityId ?? Guid.NewGuid(),
            request.ActionUrl ?? "/estate/facilities/EstateFacilityBillingServiceCharge",
            new Dictionary<string, object>
            {
                ["sourceLabel"] = SourceLabel,
                ["sourceModule"] = "Estate / Facilities",
                ["actionType"] = request.ActionType,
                ["financeArReference"] = request.FinanceArReference ?? string.Empty,
                ["customerId"] = request.CustomerId?.ToString() ?? string.Empty,
                ["customerName"] = request.CustomerName ?? string.Empty,
                ["amount"] = request.Amount ?? 0,
                ["currencyCode"] = request.CurrencyCode ?? string.Empty,
                ["sourceRecordReference"] = request.SourceRecordReference ?? string.Empty,
                ["propertyUnit"] = request.PropertyUnit ?? string.Empty,
                ["notes"] = request.Notes ?? string.Empty
            },
            cancellationToken);

        return Ok(new { success = true, message = "Finance AR result notification created." });
    }

    private async Task NotifyBillingResultAsync(
        string title,
        string message,
        string type,
        string entityType,
        Guid entityId,
        string actionUrl,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        await RoleNotificationDispatcher.NotifyRolesAsync(
            _db,
            _notificationService,
            GetTenantId(),
            GetUserId(),
            new[] { "Facilities Manager", "Facilities Officer", "Estate Manager", "Estate Officer", "Property Manager" },
            title,
            message,
            type,
            entityType,
            entityId,
            actionUrl,
            metadata,
            cancellationToken);
    }

    private Guid GetTenantId()
        => _currentUserService.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant context is required.");

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private static string EnsureSourceLabel(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return SourceLabel;
        }

        return notes.Contains(SourceLabel, StringComparison.OrdinalIgnoreCase)
            ? notes.Trim()
            : $"{notes.Trim()} {SourceLabel}";
    }

    private static string FinanceArResultTitle(string actionType)
    {
        var normalized = NormalizeNotificationToken(actionType);
        return normalized switch
        {
            "invoicecreated" => "Finance AR invoice created",
            "receiptrecorded" => "Finance AR receipt recorded",
            "allocationcompleted" => "Finance AR allocation completed",
            "statementissued" => "Finance AR statement issued",
            "arrearsupdated" => "Finance AR arrears updated",
            "deposithandled" => "Finance AR deposit handled",
            "glposted" => "Finance AR GL posting completed",
            "returnedforcorrection" => "Finance AR returned item for correction",
            _ => $"Finance AR {actionType.Trim()} result"
        };
    }

    private static string NormalizeNotificationToken(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
}

public sealed record EstateFacilitiesArInvoiceRequest(
    InvoiceCreateDto Invoice,
    string? SourceRecordReference,
    string? PropertyUnit);

public sealed record EstateFacilitiesArPaymentRequest(
    PaymentCreateDto Payment,
    string? SourceRecordReference,
    string? PropertyUnit);

public sealed record EstateFacilitiesArResultRequest(
    string ActionType,
    Guid? FinanceArEntityId,
    string? FinanceArReference,
    Guid? CustomerId,
    string? CustomerName,
    decimal? Amount,
    string? CurrencyCode,
    string? SourceRecordReference,
    string? PropertyUnit,
    string? Notes,
    string? ActionUrl);
