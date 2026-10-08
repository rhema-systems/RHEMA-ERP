using System.Text.Json;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Sales;

public sealed class SalesOrderCustomerDepositService(
    ApplicationDbContext db,
    ICurrentUserProvider currentUser,
    IPaymentService payments) : ISalesOrderCustomerDepositService
{
    private Guid TenantId => currentUser.TenantId != Guid.Empty
        ? currentUser.TenantId
        : throw new UnauthorizedAccessException("A tenant context is required for Sales Order deposits.");

    public async Task<IReadOnlyList<SalesOrderCustomerDepositDto>> GetAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var orderExists = await db.SalesOrders.AsNoTracking().AnyAsync(item =>
            item.Id == salesOrderId && item.TenantId == tenantId && !item.IsDeleted,
            cancellationToken);
        if (!orderExists)
            throw new KeyNotFoundException("Sales Order not found in the current tenant.");

        var deposits = await db.SalesOrderCustomerDeposits.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.SalesOrderId == salesOrderId
                && !item.IsDeleted)
            .Include(item => item.CustomerPayment)
            .OrderByDescending(item => item.CustomerPayment.PaymentDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return deposits.Select(item => new SalesOrderCustomerDepositDto
            {
                Id = item.Id,
                SalesOrderId = item.SalesOrderId,
                CustomerPaymentId = item.CustomerPaymentId,
                PaymentNumber = item.CustomerPayment.PaymentNumber,
                Amount = item.CustomerPayment.TotalAmount,
                Currency = item.CustomerPayment.CurrencyCode,
                TenderType = item.TenderType,
                Reference = FormatReference(item),
                PropertyDescription = item.PropertyDescription,
                Status = item.CustomerPayment.Status,
                PaymentDate = item.CustomerPayment.PaymentDate,
                IsReversed = item.CustomerPayment.ReversedAt.HasValue
            })
            .ToList();
    }

    public async Task<SalesOrderCustomerDepositDto> CreateAsync(
        Guid salesOrderId,
        CreateSalesOrderCustomerDepositDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        if (request.Amount <= 0m)
            throw new InvalidOperationException("Deposit amount must be positive.");
        var idempotencyKey = Clean(request.IdempotencyKey)
            ?? throw new InvalidOperationException("A deposit request key is required.");

        var order = await db.SalesOrders.AsNoTracking()
            .Include(item => item.BusinessPartner)
                .ThenInclude(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == salesOrderId
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Sales Order not found in the current tenant.");

        EnsureApprovedCustomer(order.BusinessPartner);
        if (!order.OpportunityId.HasValue)
            throw new InvalidOperationException("Property deposits require a Sales Order linked to its originating Opportunity.");
        if (order.OrderStatus is SalesOrderStatus.Cancelled or SalesOrderStatus.Closed)
            throw new InvalidOperationException("Deposits cannot be recorded against a cancelled or closed Sales Order.");

        var method = await db.PaymentMethods.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == request.PaymentMethodId
            && item.TenantId == tenantId
            && !item.IsDeleted
            && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Select an active payment method for the current tenant.");

        var ticket = await db.EhcTickets.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId
            && item.CrmOpportunityId == order.OpportunityId.Value
            && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The originating property enquiry could not be resolved.");
        var property = ParseProperty(ticket.PropertyListingContextJson);
        var propertyDescription = BuildPropertyDescription(property, order);

        var tenderType = method.Type switch
        {
            PaymentMethodType.Cash => "Cash",
            PaymentMethodType.Cheque => "Cheque",
            PaymentMethodType.BankTransfer or PaymentMethodType.EFT => "BankDeposit",
            _ => throw new InvalidOperationException("Property deposits support Cash, Cheque, or Bank Deposit payment methods.")
        };

        var identificationReference = Clean(ticket.IdentificationNumber);
        var bankName = Clean(request.BankName);
        var accountNumber = Clean(request.AccountNumber);
        var chequeNumber = Clean(request.ChequeNumber);
        var depositReference = Clean(request.DepositReference);
        var reference = tenderType switch
        {
            "Cash" => identificationReference
                ?? throw new InvalidOperationException("The originating enquiry Identification Number is required for a cash deposit."),
            "Cheque" => JoinRequired(bankName, accountNumber, chequeNumber,
                "Bank Name, Account Number, and Cheque Number are required for a cheque deposit."),
            _ => JoinRequired(bankName, accountNumber, depositReference,
                "Bank Name, Account Number, and Deposit Reference are required for a bank deposit.")
        };

        var payment = await payments.CreateAsync(new PaymentCreateDto
        {
            BusinessPartnerId = order.BusinessPartnerId,
            PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
            TotalAmount = request.Amount,
            PaymentMethodId = method.Id,
            PaymentMethod = method.Name,
            CurrencyCode = order.Currency,
            BankAccountId = request.BankAccountId,
            LiquidityAccountId = request.LiquidityAccountId,
            CheckNumber = tenderType == "Cheque" ? chequeNumber : null,
            ChequeDrawerBank = tenderType == "Cheque" ? bankName : null,
            TransactionReference = reference,
            Notes = propertyDescription,
            Allocations = [],
            SalesOrderDepositLineage = new SalesOrderDepositLineageCreateDto
            {
                SalesOrderId = order.Id,
                IdempotencyKey = idempotencyKey,
                TenderType = tenderType,
                ExternalBankName = bankName,
                ExternalAccountNumber = accountNumber,
                ChequeNumber = chequeNumber,
                DepositReference = depositReference,
                IdentificationReference = identificationReference,
                PropertyDescription = propertyDescription
            }
        }, cancellationToken);

        var result = (await GetAsync(order.Id, cancellationToken))
            .SingleOrDefault(item => item.CustomerPaymentId == payment.Id);
        return result ?? throw new InvalidOperationException("The posted Sales Order deposit lineage could not be loaded.");
    }

    private static void EnsureApprovedCustomer(BusinessPartner partner)
    {
        var now = DateTime.UtcNow;
        var customerRole = partner.Roles.Any(role => !role.IsDeleted
            && role.RoleType == BusinessPartnerRoleType.Customer
            && role.Status == BusinessPartnerRoleStatus.Active
            && role.ActiveFromUtc <= now
            && (!role.InactiveFromUtc.HasValue || role.InactiveFromUtc > now));
        if (!partner.IsActive
            || !string.Equals(partner.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase)
            || !customerRole)
        {
            throw new InvalidOperationException(
                "Complete approval of the active Customer Business Partner before recording a Sales Order deposit.");
        }
    }

    private static EhcPropertyListingContextDto? ParseProperty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<EhcPropertyListingContextDto>(json); }
        catch (JsonException) { return null; }
    }

    private static string BuildPropertyDescription(EhcPropertyListingContextDto? property, SalesOrder order)
    {
        var name = Clean(property?.ListingName) ?? "Property";
        var unit = Clean(property?.ListingReference) ?? Clean(order.PropertyReference);
        return unit is null ? $"Property Deposit - {name}" : $"Property Deposit - {name} - {unit}";
    }

    private static string JoinRequired(string? first, string? second, string? third, string message)
    {
        if (first is null || second is null || third is null)
            throw new InvalidOperationException(message);
        return $"{first} / {second} / {third}";
    }

    private static string FormatReference(SalesOrderCustomerDeposit item) => item.TenderType switch
    {
        "Cash" => item.IdentificationReference ?? item.CustomerPayment.TransactionReference ?? string.Empty,
        "Cheque" => Join(item.ExternalBankName, item.ExternalAccountNumber, item.ChequeNumber),
        "BankDeposit" => Join(item.ExternalBankName, item.ExternalAccountNumber, item.DepositReference),
        _ => item.CustomerPayment.TransactionReference ?? item.CustomerPayment.CheckNumber ?? string.Empty
    };

    private static string Join(params string?[] values) =>
        string.Join(" / ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
