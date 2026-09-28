using ErpSystem.Api.Services.Notifications;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/facilities/ar-billing")]
[Authorize]
public sealed class FacilitiesArBillingController : ControllerBase
{
    private const string SourceLabel = "Source: Estate / Facilities -> Finance AR";
    private static readonly FinancePostingProducerContext DimensionProducer =
        new(FinanceDimensionRouteId.FinanceArCustomerInvoice);
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
        if (string.IsNullOrWhiteSpace(request.PropertyUnit))
            return BadRequest("Select the Estate property or unit to bill.");

        var property = await _db.EstateManagedAssets.AsNoTracking()
            .FirstOrDefaultAsync(asset => asset.TenantId == GetTenantId() && !asset.IsDeleted
                && asset.AssetCode == request.PropertyUnit.Trim(), cancellationToken);
        if (property is null || !property.CustomerBusinessPartnerId.HasValue)
            return BadRequest("The selected Estate property has no customer account.");
        if (property.CustomerBusinessPartnerId.Value != request.Invoice.CustomerId)
            return BadRequest("The invoice customer does not match the selected Estate property.");

        var reference = request.Invoice.Reference?.Trim() ?? string.Empty;
        if (!reference.Contains(property.AssetCode, StringComparison.OrdinalIgnoreCase))
            reference = string.IsNullOrEmpty(reference) ? property.AssetCode : $"{reference} {property.AssetCode}";
        if (!string.IsNullOrWhiteSpace(property.ProjectUnitCode)
            && !reference.Contains(property.ProjectUnitCode, StringComparison.OrdinalIgnoreCase))
            reference = $"{reference} {property.ProjectUnitCode}";
        if (reference.Length > 100)
            return BadRequest("The invoice reference and property code must fit within 100 characters.");
        request.Invoice.Reference = reference;
        request.Invoice.Notes = EnsureSourceLabel(request.Invoice.Notes);
        var invoice = await _invoiceService.CreateAsync(request.Invoice, DimensionProducer, cancellationToken);
        try
        {
            invoice = await _invoiceService.SendInvoiceAsync(invoice.Id, DimensionProducer, cancellationToken);
        }
        catch (InvalidOperationException exception) when (IsPendingFinanceApproval(exception))
        {
            // Finance retains control of invoices subject to its approval workflow.
        }

        var released = string.Equals(invoice.Status, "Sent", StringComparison.OrdinalIgnoreCase);
        await NotifyBillingResultAsync(
            released ? "Finance AR invoice sent" : "Finance AR invoice awaiting release",
            released
                ? $"Finance AR invoice {invoice.InvoiceNumber} was sent from Estate / Facilities."
                : $"Finance AR invoice {invoice.InvoiceNumber} was created from Estate / Facilities and awaits Finance release.",
            released ? "estate.facilities.ar.invoice-sent" : "estate.facilities.ar.invoice-awaiting-release",
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

    private static bool IsPendingFinanceApproval(InvalidOperationException exception)
        => exception.Message.Contains("approval", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("posting decision", StringComparison.OrdinalIgnoreCase);

    [HttpGet("invoices")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager,Finance Officer,Finance Manager,Accounts Officer,Senior Accountant,Financial Controller")]
    public async Task<IActionResult> GetPropertyInvoices([FromQuery] string propertyUnit, CancellationToken cancellationToken)
    {
        var property = await FindBillingPropertyAsync(propertyUnit, cancellationToken);
        if (property is null) return NotFound("The Estate property or customer account was not found.");

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(item => item.TenantId == GetTenantId() && !item.IsDeleted
                && item.BusinessPartnerId == property.CustomerBusinessPartnerId
                && item.Reference != null && item.Reference.Contains(property.AssetCode)
                && item.Notes != null && item.Notes.Contains(SourceLabel))
            .OrderByDescending(item => item.InvoiceDate).ThenByDescending(item => item.CreatedAt)
            .Take(50)
            .Select(item => new
            {
                item.Id, item.InvoiceNumber, item.Reference, item.InvoiceDate, item.DueDate,
                item.Status, item.TotalAmount, item.PaidAmount, item.CurrencyCode,
                item.WorkflowInstanceId
            })
            .ToListAsync(cancellationToken);
        var workflowIds = invoices.Where(item => item.WorkflowInstanceId.HasValue)
            .Select(item => item.WorkflowInstanceId!.Value).Distinct().ToList();
        var completedWorkflowIds = (await _db.WorkflowInstances.AsNoTracking()
            .Where(item => item.TenantId == GetTenantId() && workflowIds.Contains(item.Id)
                && item.Status == ErpSystem.Core.Enums.WorkflowInstanceStatus.Completed)
            .Select(item => item.Id).ToListAsync(cancellationToken)).ToHashSet();
        return Ok(invoices.Select(item => new
        {
            item.Id, item.InvoiceNumber, item.Reference, item.InvoiceDate, item.DueDate,
            Status = item.Status.ToString(), item.TotalAmount, item.PaidAmount, item.CurrencyCode,
            CanRelease = item.WorkflowInstanceId.HasValue
                && completedWorkflowIds.Contains(item.WorkflowInstanceId.Value)
                && item.Status.ToString() is "PendingApproval" or "Approved"
        }));
    }

    [HttpPost("invoices/{id:guid}/release")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Finance Officer,Finance Manager,Accounts Officer,Senior Accountant,Financial Controller")]
    public async Task<ActionResult<InvoiceDto>> ReleaseInvoice(
        Guid id, [FromBody] EstateFacilitiesReleaseInvoiceRequest request, CancellationToken cancellationToken)
    {
        var property = await FindBillingPropertyAsync(request.PropertyUnit, cancellationToken);
        if (property is null) return NotFound("The Estate property or customer account was not found.");
        var invoice = await _invoiceService.GetByIdAsync(id, cancellationToken);
        if (invoice is null) return NotFound();
        if (invoice.CustomerId != property.CustomerBusinessPartnerId
            || invoice.Reference?.Contains(property.AssetCode, StringComparison.OrdinalIgnoreCase) != true
            || invoice.Notes?.Contains(SourceLabel, StringComparison.OrdinalIgnoreCase) != true)
            return BadRequest("The invoice is not a Facilities charge for this property.");

        try
        {
            invoice = await _invoiceService.SendInvoiceAsync(id, DimensionProducer, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }

        await NotifyBillingResultAsync(
            "Finance AR invoice sent",
            $"Finance AR invoice {invoice.InvoiceNumber} was released from Estate / Facilities.",
            "estate.facilities.ar.invoice-sent",
            "Invoice", invoice.Id, $"/finance/ar/invoices/{invoice.Id}",
            new Dictionary<string, object>
            {
                ["sourceLabel"] = SourceLabel,
                ["sourceModule"] = "Estate / Facilities",
                ["financeArReference"] = invoice.InvoiceNumber,
                ["customerId"] = invoice.CustomerId,
                ["amount"] = invoice.TotalAmount,
                ["currencyCode"] = invoice.CurrencyCode,
                ["propertyUnit"] = property.AssetCode
            }, cancellationToken);
        return Ok(invoice);
    }

    private async Task<ErpSystem.Core.Entities.Estate.EstateManagedAsset?> FindBillingPropertyAsync(
        string? propertyUnit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(propertyUnit)) return null;
        return await _db.EstateManagedAssets.AsNoTracking().FirstOrDefaultAsync(asset =>
            asset.TenantId == GetTenantId() && !asset.IsDeleted
            && asset.AssetCode == propertyUnit.Trim() && asset.CustomerBusinessPartnerId.HasValue,
            cancellationToken);
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

public sealed record EstateFacilitiesReleaseInvoiceRequest(string PropertyUnit);

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
