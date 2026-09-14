using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Sales;
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
            return ToResult(existingCase, alreadyExists: true);

        var listingReference = demarcation is null
            ? asset.AssetCode
            : EstateLandDemarcationReference.Build(asset.AssetCode, demarcation.DemarcationNumber);
        var listingName = demarcation is null
            ? asset.Name
            : $"{asset.Name} - Parcel {demarcation.DemarcationNumber:000}";
        var requestType = NormalizeRequestType(request.RequestType, demarcation?.ExternalListingType ?? asset.ExternalListingType);
        var requestLabel = requestType == "Purchase" ? "Purchase enquiry" : "Lease enquiry";
        var amount = request.AgreedAmount ?? opportunity.Amount;
        if (amount <= 0)
            throw new InvalidOperationException("Enter a positive agreed amount before handing the enquiry to Estate.");
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
            ["listingReference"] = listingReference,
            ["listingType"] = demarcation?.ExternalListingType ?? asset.ExternalListingType,
            ["requestType"] = requestLabel,
            ["listingPrice"] = amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["offerAmount"] = requestType == "Purchase" ? amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : null,
            ["currency"] = currency,
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
            ["billingStartStatus"] = requestType == "Purchase" ? "Sales payment handled" : "Blocked - agreement pending",
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

        return new EstateSalesListingApplicationHandoffResult(
            created.Id,
            created.ReferenceNumber,
            created.Title,
            created.Status,
            created.CurrentStageName,
            DateTime.UtcNow,
            AlreadyExists: false);
    }

    private static EstateSalesListingApplicationHandoffResult ToResult(ProcedureCase procedureCase, bool alreadyExists)
        => new(procedureCase.Id, procedureCase.ReferenceNumber, procedureCase.Title, procedureCase.Status,
            procedureCase.CurrentStageName, procedureCase.CreatedAt, alreadyExists);

    private static string NormalizeRequestType(string? requestedType, string listingType)
    {
        var normalized = string.IsNullOrWhiteSpace(requestedType) ? listingType : requestedType.Trim();
        normalized = normalized.Equals("Purchase", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Buy", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Sale", StringComparison.OrdinalIgnoreCase)
            ? "Purchase"
            : normalized.Equals("Lease", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Rent", StringComparison.OrdinalIgnoreCase)
                ? "Lease"
                : string.Equals(listingType, "Sale", StringComparison.OrdinalIgnoreCase) ? "Purchase" : "Lease";
        return string.Equals(listingType, "Sale", StringComparison.OrdinalIgnoreCase) ? "Purchase"
            : string.Equals(listingType, "Rent", StringComparison.OrdinalIgnoreCase) ? "Lease"
            : normalized;
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

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? TruncateOptional(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), maxLength);
}
