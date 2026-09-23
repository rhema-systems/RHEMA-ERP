using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

/// <summary>
/// Creates the Estate workflow case only after Sales has completed the linked CRM opportunity.
/// The same service backs the direct Estate endpoint and the EHC property-enquiry handoff.
/// </summary>
public interface IEstateSalesListingApplicationHandoffService
{
    Task<EstateSalesListingApplicationHandoffResult> CreateAsync(
        Guid tenantId,
        EstateSalesListingApplicationHandoffRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EstateSalesListingApplicationHandoffRequest(
    Guid ListingId,
    Guid BusinessPartnerId,
    string? RequestType,
    Guid SalesOpportunityId,
    string SalesReference,
    decimal? AgreedAmount,
    string? RequestedLeaseTerm,
    decimal? SalesAmountPaid,
    string? SalesPaymentReference,
    string? Currency,
    DateTime? SalesCompletedAt,
    string? Notes,
    Guid? EhcTicketId = null,
    string? EhcTicketNumber = null);

public sealed record EstateSalesListingApplicationHandoffResult(
    Guid ProcedureCaseId,
    string? ReferenceNumber,
    string Title,
    string Status,
    string CurrentStageName,
    DateTime CreatedAt,
    bool AlreadyExists);

public sealed class EstateSalesListingApplicationHandoffService(
    ApplicationDbContext db,
    IProcedureCaseService procedureCaseService) : IEstateSalesListingApplicationHandoffService
{
    public async Task<EstateSalesListingApplicationHandoffResult> CreateAsync(
        Guid tenantId,
        EstateSalesListingApplicationHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant context is required.");
        if (request.ListingId == Guid.Empty)
            throw new ArgumentException("Listing id is required.", nameof(request));
        if (request.BusinessPartnerId == Guid.Empty)
            throw new ArgumentException("Customer Business Partner id is required.", nameof(request));
        if (request.SalesOpportunityId == Guid.Empty)
            throw new ArgumentException("A completed Sales opportunity is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SalesReference))
            throw new ArgumentException("Enter the completed Sales reference before handing the enquiry to Estate.", nameof(request));
        if (request.SalesAmountPaid is < 0)
            throw new ArgumentException("Sales amount paid cannot be negative.", nameof(request));

        var opportunity = await db.Opportunities
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.SalesOpportunityId
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("The linked Sales opportunity was not found.");
        if (!string.Equals(opportunity.Stage, "Closed Won", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Close the linked Sales opportunity as Won before handing the enquiry to Estate.");

        var asset = await db.EstateManagedAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ListingId
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);
        var demarcation = asset is null
            ? await db.EstateLandDemarcations
                .AsNoTracking()
                .Include(item => item.EstateManagedAsset)
                .FirstOrDefaultAsync(item => item.Id == request.ListingId
                    && item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.EstateManagedAsset.TenantId == tenantId
                    && !item.EstateManagedAsset.IsDeleted, cancellationToken)
            : null;
        asset ??= demarcation?.EstateManagedAsset;
        if (asset is null)
            throw new KeyNotFoundException("Estate listing was not found.");

        var customer = await db.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.BusinessPartnerId
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Customer Business Partner was not found.");

        var existingCase = await db.ProcedureCases
            .AsNoTracking()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .FirstOrDefaultAsync(procedureCase => procedureCase.TenantId == tenantId
                && !procedureCase.IsDeleted
                && procedureCase.Module == "PropertyManagement"
                && procedureCase.EntityType == "EstatePropertyManagementListingApplication"
                && procedureCase.Fields.Any(field => !field.IsDeleted
                    && field.Key == "salesOpportunityId"
                    && field.Value == request.SalesOpportunityId.ToString()), cancellationToken);
        if (existingCase is not null)
        {
            await ReservePortalListingAsync(tenantId, request.ListingId, demarcation is not null, cancellationToken);
            return ToResult(existingCase, alreadyExists: true);
        }

        var listingReference = demarcation is null
            ? asset.AssetCode
            : EstateLandDemarcationReference.Build(asset.AssetCode, demarcation.DemarcationNumber);
        var listingName = demarcation is null
            ? asset.Name
            : $"{asset.Name} - Parcel {demarcation.DemarcationNumber:000}";
        var requestType = NormalizeRequestType(request.RequestType, demarcation?.ExternalListingType ?? asset.ExternalListingType);
        var requestLabel = requestType switch
        {
            "Purchase" => "Purchase enquiry",
            "Rent" => "Rent enquiry",
            _ => "Lease enquiry"
        };
        if (requestType != "Purchase" && string.IsNullOrWhiteSpace(request.RequestedLeaseTerm))
            throw new InvalidOperationException("Enter the Sales-agreed rent or lease duration before handing the enquiry to Estate.");
        var amount = request.AgreedAmount ?? opportunity.Amount;
        if (amount <= 0)
            throw new InvalidOperationException("Enter a positive agreed amount before handing the enquiry to Estate.");
        var salesAmountPaid = decimal.Round(request.SalesAmountPaid ?? 0m, 2, MidpointRounding.AwayFromZero);
        if (salesAmountPaid > amount)
            throw new InvalidOperationException("Sales amount paid cannot be greater than the agreed amount.");
        var estateRemainingAmount = decimal.Round(amount - salesAmountPaid, 2, MidpointRounding.AwayFromZero);
        var currency = NormalizeCurrency(request.Currency ?? opportunity.Currency ?? demarcation?.ExternalListingCurrency ?? asset.ExternalListingCurrency ?? customer.Currency);
        var completedAt = request.SalesCompletedAt ?? opportunity.ActualCloseDate ?? DateTime.UtcNow;
        var reference = BuildReference("ESTATE");
        var description = Truncate(
            $"{requestLabel} accepted by Sales for {listingReference} - {listingName}. Customer: {customer.PartnerName} ({customer.CustomerAccountNumber}).",
            1000);

        var fieldValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["applicationReference"] = reference,
            ["sourceWorkspace"] = "Sales - Estate Enquiry",
            ["sourceReference"] = customer.Id.ToString(),
            ["ehcTicketId"] = request.EhcTicketId?.ToString(),
            ["ehcTicketNumber"] = TruncateOptional(request.EhcTicketNumber, 30),
            ["customerAccountReference"] = customer.CustomerAccountNumber,
            ["customerName"] = customer.PartnerName,
            ["propertyUnit"] = listingReference,
            ["listingId"] = request.ListingId.ToString(),
            ["listingRecordType"] = demarcation is null ? "EstateManagedAsset" : "EstateLandDemarcation",
            ["listingReference"] = listingReference,
            ["listingName"] = listingName,
            ["listingLocation"] = asset.Location,
            ["listingArea"] = (demarcation is null ? asset.AreaValue : demarcation.AreaSquareFeet).ToString(),
            ["listingAreaUnit"] = demarcation is null ? asset.AreaUnit : "sq ft",
            ["listingType"] = demarcation?.ExternalListingType ?? asset.ExternalListingType,
            ["groundRentRequired"] = (demarcation?.ExternalGroundRentRequired ?? asset.ExternalGroundRentRequired) == true ? "Yes" : "No",
            ["premiumChargeRequired"] = (demarcation?.ExternalPremiumChargeRequired ?? asset.ExternalPremiumChargeRequired) == true ? "Yes" : "No",
            ["requestType"] = requestLabel,
            ["listingPrice"] = amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["offerAmount"] = requestType == "Purchase" ? amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : null,
            ["requestedLeaseTerm"] = requestType == "Purchase" ? null : Truncate(request.RequestedLeaseTerm!.Trim(), 120),
            ["currency"] = currency,
            ["salesAmountPaid"] = salesAmountPaid.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["salesPaymentReference"] = TruncateOptional(request.SalesPaymentReference, 200),
            ["estateRemainingAmount"] = estateRemainingAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["salePaymentCheckStatus"] = BuildSalesPaymentCheckStatus(currency, amount, salesAmountPaid, estateRemainingAmount),
            ["salePaymentStatus"] = requestType is "Purchase" or "Lease"
                ? estateRemainingAmount <= 0m ? "Paid in full" : salesAmountPaid > 0m ? "Part-paid in Sales" : "Pending Estate payment"
                : null,
            ["requestMessage"] = TruncateOptional(request.Notes, 1000),
            ["customerValidationStatus"] = "Validated by Sales",
            ["listingValidationStatus"] = "Pending",
            ["availabilityCheck"] = "Pending",
            ["commercialReviewStatus"] = "Completed by Sales",
            ["decisionStatus"] = "Pending Estate review",
            ["reservationStatus"] = "Sales completed",
            ["customerNotificationStatus"] = "Handled by Sales",
            ["customerAcceptanceStatus"] = "Accepted in Sales",
            ["customerAcceptanceDate"] = completedAt.ToString("yyyy-MM-dd"),
            ["billingStartStatus"] = requestType == "Purchase"
                ? estateRemainingAmount <= 0m ? "No Estate balance from Sales handoff" : "Estate balance pending"
                : "Blocked - agreement pending",
            ["ownershipTransferStatus"] = requestType == "Purchase"
                ? estateRemainingAmount <= 0m
                    ? "Blocked - Legal conveyance and registration pending"
                    : "Blocked - Estate balance and Legal conveyance required"
                : null,
            ["receivedDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["applicationStatus"] = "Submitted from Sales",
            ["salesOpportunityId"] = request.SalesOpportunityId.ToString(),
            ["salesReference"] = Truncate(request.SalesReference.Trim(), 200),
            ["salesCompletedAt"] = completedAt.ToString("O"),
            ["notes"] = string.IsNullOrWhiteSpace(request.Notes) ? description : $"{description} {request.Notes.Trim()}"
        };

        var created = await procedureCaseService.CreateCaseAsync(new CreateProcedureCaseRequest(
            "PropertyManagement",
            "EstatePropertyManagementListingApplication",
            $"{requestLabel} - {listingName}",
            reference,
            customer.PartnerName,
            "Sales - Estate Enquiry",
            DateTime.UtcNow,
            description,
            fieldValues));

        await ReservePortalListingAsync(tenantId, request.ListingId, demarcation is not null, cancellationToken);

        return new EstateSalesListingApplicationHandoffResult(
            created.Id,
            created.ReferenceNumber,
            created.Title,
            created.Status,
            created.CurrentStageName,
            DateTime.UtcNow,
            AlreadyExists: false);
    }

    private async Task ReservePortalListingAsync(
        Guid tenantId,
        Guid listingId,
        bool isDemarcationListing,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (isDemarcationListing)
        {
            await db.EstateLandDemarcations
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId
                    && item.Id == listingId
                    && !item.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.IsPublishedToExternalPortal, false)
                    .SetProperty(item => item.ExternalListingStatus, "Reserved")
                    .SetProperty(item => item.ExternalPublishedAt, (DateTime?)null)
                    .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);
            return;
        }

        await db.EstateManagedAssets
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId
                && item.Id == listingId
                && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsPublishedToExternalPortal, false)
                .SetProperty(item => item.ExternalListingStatus, "Reserved")
                .SetProperty(item => item.ExternalPublishedAt, (DateTime?)null)
                .SetProperty(
                    item => item.Status,
                    item => item.Status == EstateManagedAssetStatus.Available
                        ? EstateManagedAssetStatus.Reserved
                        : item.Status)
                .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);
    }

    private static EstateSalesListingApplicationHandoffResult ToResult(ProcedureCase procedureCase, bool alreadyExists)
        => new(procedureCase.Id, procedureCase.ReferenceNumber, procedureCase.Title, procedureCase.Status,
            procedureCase.CurrentStageName, procedureCase.CreatedAt, alreadyExists);

    private static string NormalizeRequestType(string? requestedType, string listingType)
    {
        var normalizedListingType = NormalizeListingType(listingType);
        if (string.IsNullOrWhiteSpace(requestedType))
        {
            return normalizedListingType;
        }

        var normalized = requestedType.Trim();
        if (normalized.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("buy", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("sale", StringComparison.OrdinalIgnoreCase))
        {
            return "Purchase";
        }

        if (normalized.Contains("rent", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("rental", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("tenancy", StringComparison.OrdinalIgnoreCase))
        {
            return "Rent";
        }

        if (normalized.Contains("lease", StringComparison.OrdinalIgnoreCase))
        {
            return "Lease";
        }

        return normalizedListingType;
    }

    private static string NormalizeListingType(string? listingType)
    {
        var normalized = listingType?.Trim();
        if (string.Equals(normalized, "Sale", StringComparison.OrdinalIgnoreCase))
        {
            return "Purchase";
        }

        if (string.Equals(normalized, "Rent", StringComparison.OrdinalIgnoreCase))
        {
            return "Rent";
        }

        return "Lease";
    }

    private static string NormalizeCurrency(string? value)
    {
        var currency = string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            throw new ArgumentException("Currency must be a three-letter code.", nameof(value));
        return currency;
    }

    private static string BuildReference(string prefix)
        => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".ToUpperInvariant();

    private static string BuildSalesPaymentCheckStatus(
        string currency,
        decimal agreedAmount,
        decimal salesAmountPaid,
        decimal estateRemainingAmount)
        => estateRemainingAmount <= 0m
            ? $"Sales recorded {currency} {salesAmountPaid:N2} paid against agreed amount {currency} {agreedAmount:N2}; no Estate balance remains."
            : salesAmountPaid > 0m
                ? $"Sales recorded {currency} {salesAmountPaid:N2} paid against agreed amount {currency} {agreedAmount:N2}; Estate balance is {currency} {estateRemainingAmount:N2}."
                : $"No Sales payment recorded against agreed amount {currency} {agreedAmount:N2}; Estate balance is {currency} {estateRemainingAmount:N2}.";

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateOptional(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), maxLength);
}
