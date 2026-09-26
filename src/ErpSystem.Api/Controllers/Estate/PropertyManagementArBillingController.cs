using ErpSystem.Api.Services.Notifications;
using System.Globalization;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/property-management/ar-billing")]
[Authorize]
public sealed class PropertyManagementArBillingController : ControllerBase
{
    private const string SourceLabel = "Source: Estate / Property Management -> Finance AR";
    private readonly IInvoiceService _invoiceService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ApplicationDbContext _db;

    [HttpPost("recurring/run")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer")]
    public async Task<ActionResult<ErpSystem.Api.Services.Estate.EstateRecurringBillingResult>> RunRecurringBilling(
        [FromServices] ErpSystem.Api.Services.Estate.EstateRecurringBillingService runner,
        CancellationToken cancellationToken)
        => Ok(await runner.RunForTenantAsync(GetTenantId(), cancellationToken));

    public PropertyManagementArBillingController(
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

    [HttpGet("rent/penalty-statuses")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer")]
    public async Task<ActionResult<IReadOnlyList<EstateRentPenaltyStatusResult>>> GetRentPenaltyStatuses(
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var assets = await _db.EstateManagedAssets
            .AsNoTracking()
            .Where(asset => asset.TenantId == tenantId
                && !asset.IsDeleted
                && asset.AssetType != EstateManagedAssetType.Land
                && asset.LastRentInvoiceId.HasValue)
            .Select(asset => new
            {
                asset.Id,
                InvoiceId = asset.LastRentInvoiceId!.Value,
                asset.RentGracePeriodDays,
                asset.RentPenaltyMethod,
                asset.RentPenaltyValue,
                asset.LastRentPenaltySourceInvoiceId
            })
            .ToListAsync(cancellationToken);

        var invoiceIds = assets.Select(asset => asset.InvoiceId).Distinct().ToList();
        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.TenantId == tenantId
                && !invoice.IsDeleted
                && invoiceIds.Contains(invoice.Id))
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.TotalAmount,
                invoice.PaidAmount,
                invoice.CreditedAmount
            })
            .ToDictionaryAsync(invoice => invoice.Id, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var results = assets.Select(asset =>
        {
            if (!invoices.TryGetValue(asset.InvoiceId, out var invoice))
            {
                return new EstateRentPenaltyStatusResult(asset.Id, false, false, null, null, 0m);
            }

            return CalculateRentPenaltyStatus(
                asset.Id,
                asset.InvoiceId,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.TotalAmount,
                invoice.PaidAmount,
                invoice.CreditedAmount,
                asset.RentGracePeriodDays,
                asset.RentPenaltyMethod,
                asset.RentPenaltyValue,
                asset.LastRentPenaltySourceInvoiceId,
                today);
        }).ToList();

        return Ok(results);
    }

    internal static EstateRentPenaltyStatusResult CalculateRentPenaltyStatus(
        Guid assetId,
        Guid invoiceId,
        DateTime invoiceDate,
        DateTime? invoiceDueDate,
        decimal totalAmount,
        decimal paidAmount,
        decimal creditedAmount,
        int gracePeriodDays,
        string penaltyMethod,
        decimal penaltyValue,
        Guid? lastPenaltySourceInvoiceId,
        DateTime today)
    {
        var outstanding = Math.Max(0m, totalAmount - paidAmount - creditedAmount);
        var dueDate = (invoiceDueDate ?? invoiceDate).Date;
        var graceEnds = dueDate.AddDays(gracePeriodDays);
        var isOverdue = outstanding > 0m && today.Date > dueDate;
        var canAssess = isOverdue
            && today.Date > graceEnds
            && penaltyMethod != "None"
            && penaltyValue > 0m
            && lastPenaltySourceInvoiceId != invoiceId;

        return new EstateRentPenaltyStatusResult(
            assetId,
            isOverdue,
            canAssess,
            dueDate,
            graceEnds,
            outstanding);
    }

    [HttpPost("rent/{assetId:guid}/activate")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer")]
    public async Task<ActionResult<EstateRentBillingActivationResult>> ActivateRentBilling(
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var asset = await _db.EstateManagedAssets.FirstOrDefaultAsync(
            item => item.Id == assetId
                && item.TenantId == tenantId
                && !item.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException("The Estate property or unit was not found.");

        if (asset.AssetType == EstateManagedAssetType.Land)
        {
            throw new InvalidOperationException("Use Ground Rent Administration for land leases.");
        }

        if (asset.ExternalListingType?.Contains("Lease", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("Full-term leases use the one-time Estate balance invoice, not recurring rent billing.");
        }

        if (asset.Status is not EstateManagedAssetStatus.Leased
            and not EstateManagedAssetStatus.Occupied)
        {
            throw new InvalidOperationException("Only a leased or occupied property can activate rent billing.");
        }

        if (asset.RentBillingActivatedAt.HasValue)
        {
            return Ok(new EstateRentBillingActivationResult(
                true,
                asset.RentBillingActivatedAt.Value,
                asset.NextRentBillingDate,
                asset.LastRentInvoiceId,
                asset.LastRentInvoiceNumber,
                "Rent billing is already active for this lease."));
        }

        if (!asset.CustomerBusinessPartnerId.HasValue)
        {
            throw new InvalidOperationException("Assign the Finance AR customer before activating rent billing.");
        }

        var monthlyRent = asset.ExternalMonthlyRent ?? asset.ExternalListingPrice;
        if (!monthlyRent.HasValue || monthlyRent.Value <= 0m)
        {
            throw new InvalidOperationException("Record the agreed monthly rent before activating billing.");
        }

        var billingStart = (asset.RightOfEntryDate ?? asset.DateOfTenancy)?.Date
            ?? throw new InvalidOperationException("Record the lease or possession date before activating billing.");
        if (string.IsNullOrWhiteSpace(asset.PropertyFileReference))
        {
            throw new InvalidOperationException("Record the signed agreement reference before activating billing.");
        }

        var incomeAccount = await _db.Accounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.Status == AccountStatus.Active
                && item.AccountType == AccountType.Revenue
                && item.AllowDirectPosting
                && !item.IsControlAccount
                && (item.AccountCode == "4110" || item.AccountCode == "4100"))
            .OrderBy(item => item.AccountCode == "4110" ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Finance must configure an active Rental Income account before rent billing can be activated.");

        InvoiceDto? invoice = null;
        var today = DateTime.UtcNow.Date;
        if (billingStart <= today)
        {
            var periodEnd = billingStart.AddMonths(1).AddDays(-1);
            var reference = BuildRentInvoiceReference(asset.AssetCode, billingStart);
            var existingInvoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted
                && item.BusinessPartnerId == asset.CustomerBusinessPartnerId.Value
                && item.Reference == reference, cancellationToken);
            if (existingInvoice?.Status == InvoiceStatus.Cancelled)
                throw new InvalidOperationException("The first rent invoice was cancelled; Finance must resolve it before billing activation.");
            invoice = existingInvoice is not null
                ? await _invoiceService.GetByIdAsync(existingInvoice.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The existing rent invoice could not be loaded.")
                : await _invoiceService.CreateAsync(
                new InvoiceCreateDto
                {
                    BusinessPartnerId = asset.CustomerBusinessPartnerId.Value,
                    InvoiceDate = billingStart,
                    DueDate = billingStart,
                    Reference = reference,
                    CurrencyCode = string.IsNullOrWhiteSpace(asset.ExternalListingCurrency)
                        ? asset.Currency
                        : asset.ExternalListingCurrency,
                    ExchangeRate = 1m,
                    Notes = EnsureSourceLabel(
                        $"Monthly rent for {asset.AssetCode}; agreement {asset.PropertyFileReference}."),
                    LineItems =
                    [
                        new InvoiceLineItemCreateDto
                        {
                            LineItemType = "GLAccount",
                            GLAccountId = incomeAccount.Id,
                            Description = $"Rent {billingStart:dd MMM yyyy} - {periodEnd:dd MMM yyyy}: {asset.Name}",
                            Quantity = 1m,
                            UnitPrice = monthlyRent.Value,
                            DiscountPercentage = 0m
                        }
                    ]
                },
                cancellationToken);
        }

        var activatedAt = DateTime.UtcNow;
        asset.RentBillingActivatedAt = activatedAt;
        asset.AutoGenerateRentInvoices = true;
        asset.NextRentBillingDate = invoice is null ? billingStart : billingStart.AddMonths(1);
        asset.LastRentInvoiceId = invoice?.Id;
        asset.LastRentInvoiceNumber = invoice?.InvoiceNumber;
        asset.UpdatedAt = activatedAt;
        asset.UpdatedBy = User.Identity?.Name ?? "System";
        asset.LastModifiedById = GetUserId();
        await _db.SaveChangesAsync(cancellationToken);

        if (invoice is not null)
        {
            try
            {
                invoice = await _invoiceService.SendInvoiceAsync(invoice.Id, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Keep Finance approval in control when direct release is not allowed.
            }
        }

        if (invoice is not null)
        {
            await NotifyBillingResultAsync(
                "Rent billing activated",
                $"Finance AR invoice {invoice.InvoiceNumber} was created for {asset.AssetCode}.",
                "estate.property-management.rent-billing-activated",
                "Invoice",
                invoice.Id,
                $"/finance/ar/invoices/{invoice.Id}",
                BillingMetadata(
                    invoice.InvoiceNumber,
                    invoice.BusinessPartnerId,
                    invoice.CustomerName,
                    invoice.TotalAmount,
                    invoice.CurrencyCode,
                    asset.PropertyFileReference,
                    asset.ProjectUnitCode ?? asset.AssetCode),
                cancellationToken);
        }

        return Ok(new EstateRentBillingActivationResult(
            true,
            activatedAt,
            asset.NextRentBillingDate,
            invoice?.Id,
            invoice?.InvoiceNumber,
            invoice is null
                ? $"Rent billing was activated. The first invoice is scheduled for {billingStart:yyyy-MM-dd}."
                : $"Rent billing was activated and Finance AR invoice {invoice.InvoiceNumber} was created."));
    }

    [HttpPut("rent/{assetId:guid}/schedule")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer")]
    public async Task<IActionResult> UpdateRentSchedule(
        Guid assetId,
        [FromBody] UpdateEstateRentScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MonthlyRent <= 0m)
            throw new InvalidOperationException("Monthly rent must be greater than zero.");
        if (request.NextBillingDate.Date < DateTime.UtcNow.Date)
            throw new InvalidOperationException("The next billing date cannot be in the past.");

        var asset = await _db.EstateManagedAssets.FirstOrDefaultAsync(item =>
            item.Id == assetId && item.TenantId == GetTenantId() && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The property or unit was not found.");
        if (asset.AssetType == EstateManagedAssetType.Land
            || asset.ExternalListingType?.Contains("Lease", StringComparison.OrdinalIgnoreCase) == true
            || !asset.RentBillingActivatedAt.HasValue)
            throw new InvalidOperationException("Activate monthly rental billing before adjusting its schedule.");
        var billingStart = (asset.RightOfEntryDate ?? asset.DateOfTenancy)?.Date;
        if (billingStart.HasValue && request.NextBillingDate.Date < billingStart.Value)
            throw new InvalidOperationException("The next billing date cannot precede the tenancy start date.");

        var reference = BuildRentInvoiceReference(asset.AssetCode, request.NextBillingDate.Date);
        if (await _db.Invoices.AsNoTracking().AnyAsync(item =>
            item.TenantId == asset.TenantId && !item.IsDeleted && item.Reference == reference
            && item.BusinessPartnerId == asset.CustomerBusinessPartnerId, cancellationToken))
            throw new InvalidOperationException("An invoice already exists for that rental month. Select a later billing month.");

        asset.ExternalMonthlyRent = request.MonthlyRent;
        asset.NextRentBillingDate = request.NextBillingDate.Date;
        asset.AutoGenerateRentInvoices = request.Enabled;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = User.Identity?.Name ?? "System";
        asset.LastModifiedById = GetUserId();
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Monthly rent schedule updated." });
    }

    [HttpPut("rent/{assetId:guid}/penalty-terms")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Manager,Property Manager")]
    public async Task<ActionResult<EstateRentPenaltyTermsResult>> UpdateRentPenaltyTerms(
        Guid assetId,
        [FromBody] UpdateEstateRentPenaltyTermsRequest request,
        CancellationToken cancellationToken)
    {
        var method = NormalizePenaltyMethod(request.PenaltyMethod);
        if (request.GracePeriodDays is < 0 or > 365)
            throw new InvalidOperationException("The rent grace period must be between 0 and 365 days.");
        if (request.PenaltyValue < 0m || request.PenaltyCapAmount < 0m)
            throw new InvalidOperationException("Penalty values cannot be negative.");
        if (method != "None" && request.PenaltyValue <= 0m)
            throw new InvalidOperationException("Enter a penalty value greater than zero.");
        if (method == "Percentage" && request.PenaltyValue > 100m)
            throw new InvalidOperationException("The percentage penalty cannot exceed 100%. ");

        var asset = await FindRentalAssetAsync(assetId, cancellationToken);
        asset.RentGracePeriodDays = request.GracePeriodDays;
        asset.RentPenaltyMethod = method;
        asset.RentPenaltyValue = method == "None" ? 0m : request.PenaltyValue;
        asset.RentPenaltyCapAmount = method == "None" ? null : request.PenaltyCapAmount;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = User.Identity?.Name ?? "System";
        asset.LastModifiedById = GetUserId();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new EstateRentPenaltyTermsResult(
            asset.Id,
            asset.RentGracePeriodDays,
            asset.RentPenaltyMethod,
            asset.RentPenaltyValue,
            asset.RentPenaltyCapAmount,
            "Rental penalty terms were saved."));
    }

    [HttpPost("rent/{assetId:guid}/penalties/assess")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer")]
    public async Task<ActionResult<EstateRentPenaltyAssessmentResult>> AssessRentPenalty(
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var asset = await FindRentalAssetAsync(assetId, cancellationToken);
        if (asset.RentPenaltyMethod == "None" || asset.RentPenaltyValue <= 0m)
            throw new InvalidOperationException("An Estate manager must configure the rental penalty terms first.");
        if (!asset.LastRentInvoiceId.HasValue)
            throw new InvalidOperationException("No rent invoice is available for penalty assessment.");

        var sourceInvoice = await _db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(invoice => invoice.Id == asset.LastRentInvoiceId.Value
                && invoice.TenantId == GetTenantId()
                && !invoice.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("The latest rent invoice was not found.");
        var outstanding = sourceInvoice.TotalAmount - sourceInvoice.PaidAmount - sourceInvoice.CreditedAmount;
        if (outstanding <= 0m)
            throw new InvalidOperationException("The latest rent invoice has no unpaid balance.");

        var dueDate = (sourceInvoice.DueDate ?? sourceInvoice.InvoiceDate).Date;
        var graceEnds = dueDate.AddDays(asset.RentGracePeriodDays);
        if (DateTime.UtcNow.Date <= graceEnds)
            throw new InvalidOperationException($"The manager-defined grace period ends on {graceEnds:yyyy-MM-dd}.");
        if (asset.LastRentPenaltySourceInvoiceId == sourceInvoice.Id
            && asset.LastRentPenaltyInvoiceId.HasValue)
            throw new InvalidOperationException($"Penalty invoice {asset.LastRentPenaltyInvoiceNumber ?? asset.LastRentPenaltyInvoiceId.ToString()} already covers this rent invoice.");

        var penaltyAmount = asset.RentPenaltyMethod == "Percentage"
            ? decimal.Round(outstanding * asset.RentPenaltyValue / 100m, 2, MidpointRounding.AwayFromZero)
            : asset.RentPenaltyValue;
        if (asset.RentPenaltyCapAmount is > 0m)
            penaltyAmount = Math.Min(penaltyAmount, asset.RentPenaltyCapAmount.Value);
        if (penaltyAmount <= 0m)
            throw new InvalidOperationException("The configured rental penalty produces a zero amount.");

        var revenueAccount = await _db.Accounts
            .AsNoTracking()
            .Where(item => item.TenantId == GetTenantId()
                && !item.IsDeleted
                && item.Status == AccountStatus.Active
                && item.AccountType == AccountType.Revenue
                && item.AllowDirectPosting
                && !item.IsControlAccount
                && (item.AccountCode == "4190" || item.AccountCode == "4110" || item.AccountCode == "4100"))
            .OrderBy(item => item.AccountCode == "4190" ? 0 : item.AccountCode == "4110" ? 1 : 2)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Finance must configure an active rental penalty or rental income account.");

        var assessedOn = DateTime.UtcNow.Date;
        var penaltyInvoice = await _invoiceService.CreateAsync(new InvoiceCreateDto
        {
            BusinessPartnerId = sourceInvoice.BusinessPartnerId,
            InvoiceDate = assessedOn,
            DueDate = assessedOn,
            Reference = BuildRentPenaltyReference(asset.AssetCode, sourceInvoice.InvoiceNumber),
            CurrencyCode = sourceInvoice.CurrencyCode,
            ExchangeRate = sourceInvoice.ExchangeRate,
            Notes = EnsureSourceLabel($"Late-rent penalty for {sourceInvoice.InvoiceNumber}; agreement {asset.PropertyFileReference}."),
            LineItems =
            [
                new InvoiceLineItemCreateDto
                {
                    LineItemType = "GLAccount",
                    GLAccountId = revenueAccount.Id,
                    Description = $"Late-rent penalty: {asset.Name} ({sourceInvoice.InvoiceNumber})",
                    Quantity = 1m,
                    UnitPrice = penaltyAmount,
                    DiscountPercentage = 0m
                }
            ]
        }, cancellationToken);

        asset.LastRentPenaltySourceInvoiceId = sourceInvoice.Id;
        asset.LastRentPenaltyInvoiceId = penaltyInvoice.Id;
        asset.LastRentPenaltyInvoiceNumber = penaltyInvoice.InvoiceNumber;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = User.Identity?.Name ?? "System";
        asset.LastModifiedById = GetUserId();
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyBillingResultAsync(
            "Rental penalty assessed",
            $"Finance AR draft penalty invoice {penaltyInvoice.InvoiceNumber} was created for {asset.AssetCode}.",
            "estate.property-management.rent-penalty-assessed",
            "Invoice",
            penaltyInvoice.Id,
            $"/finance/ar/invoices/{penaltyInvoice.Id}",
            BillingMetadata(penaltyInvoice.InvoiceNumber, penaltyInvoice.BusinessPartnerId, penaltyInvoice.CustomerName, penaltyAmount, penaltyInvoice.CurrencyCode, asset.PropertyFileReference, asset.ProjectUnitCode ?? asset.AssetCode),
            cancellationToken);

        return Ok(new EstateRentPenaltyAssessmentResult(
            penaltyInvoice.Id,
            penaltyInvoice.InvoiceNumber,
            penaltyAmount,
            penaltyInvoice.CurrencyCode,
            sourceInvoice.Id,
            sourceInvoice.InvoiceNumber,
            $"Rental penalty was assessed and Finance AR draft {penaltyInvoice.InvoiceNumber} was created."));
    }

    [HttpPost("sale/{procedureCaseId:guid}/invoice")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer")]
    public async Task<ActionResult<EstateSaleInvoiceResult>> CreateSaleInvoice(
        Guid procedureCaseId,
        CancellationToken cancellationToken)
    {
        var sourceCase = await LoadSaleProcedureCaseAsync(procedureCaseId, cancellationToken, allowLease: true);
        var fields = CaseFields(sourceCase);
        var isLease = (FieldValue(fields, "requestType") ?? sourceCase.Title).Contains("lease", StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(FieldValue(fields, "agreementExecutionStatus"), "Fully executed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The agreement must be fully executed before the balance invoice is created.");

        if (Guid.TryParse(FieldValue(fields, "saleInvoiceId"), out var existingInvoiceId))
        {
            var existingInvoice = await _invoiceService.GetByIdAsync(existingInvoiceId, cancellationToken);
            if (existingInvoice is not null)
            {
                return Ok(new EstateSaleInvoiceResult(
                    existingInvoice.Id,
                    existingInvoice.InvoiceNumber,
                    existingInvoice.TotalAmount,
                    existingInvoice.CurrencyCode,
                    existingInvoice.Status,
                    "The sale invoice already exists."));
            }
        }

        if (!Guid.TryParse(FieldValue(fields, "sourceReference"), out var customerId))
            throw new InvalidOperationException("The sale request is not linked to a Finance AR customer.");
        var salePayable = ResolveSalePayable(fields);
        if (salePayable.EstateBalance <= 0m)
            throw new InvalidOperationException("Sales has already recorded the full agreed amount. No Estate balance remains to invoice.");

        var revenueAccount = await _db.Accounts
            .AsNoTracking()
            .Where(item => item.TenantId == GetTenantId()
                && !item.IsDeleted
                && item.Status == AccountStatus.Active
                && item.AccountType == AccountType.Revenue
                && item.AllowDirectPosting
                && !item.IsControlAccount
                && item.AccountCode == "4100")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Finance must configure the active Sales Revenue account 4100.");

        var propertyUnit = FieldValue(fields, "propertyUnit") ?? FieldValue(fields, "listingReference") ?? sourceCase.Title;
        var agreementReference = FieldValue(fields, "finalSignedAgreementReference") ?? FieldValue(fields, "generatedAgreementReference");
        var invoice = await _invoiceService.CreateAsync(new InvoiceCreateDto
        {
            BusinessPartnerId = customerId,
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date,
            Reference = BuildSaleInvoiceReference(sourceCase.ReferenceNumber ?? sourceCase.Id.ToString(), isLease),
            CurrencyCode = FieldValue(fields, "currency") ?? "GHS",
            ExchangeRate = 1m,
            Notes = EnsureSourceLabel($"Property {(isLease ? "lease" : "purchase")} for {propertyUnit}; agreement {agreementReference}."),
            LineItems =
            [
                new InvoiceLineItemCreateDto
                {
                    LineItemType = "GLAccount",
                    GLAccountId = revenueAccount.Id,
                    Description = $"Property {(isLease ? "lease" : "sale")}: {propertyUnit}",
                    Quantity = 1m,
                    UnitPrice = salePayable.EstateBalance,
                    DiscountPercentage = 0m
                }
            ]
        }, cancellationToken);

        var now = DateTime.UtcNow;
        UpsertCaseField(sourceCase, fields, "saleInvoiceId", "Balance invoice ID", invoice.Id.ToString(), now);
        UpsertCaseField(sourceCase, fields, "saleInvoiceReference", "Balance invoice reference", invoice.InvoiceNumber, now);
        UpsertCaseField(sourceCase, fields, "saleInvoiceStatus", "Balance invoice status", invoice.Status, now);
        UpsertCaseField(sourceCase, fields, "saleInvoiceAmount", "Balance invoice amount", invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "saleInvoicePaidAmount", "Balance invoice paid amount", invoice.PaidAmount.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "saleInvoiceBalance", "Balance invoice remaining", invoice.BalanceAmount.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "estateRemainingAmount", "Balance for Estate processing", salePayable.EstateBalance.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "salePaymentCheckStatus", "Sale payment check", BuildSalePaymentCheckMessage(invoice.CurrencyCode, salePayable, invoice.PaidAmount, invoice.BalanceAmount), now);
        UpsertCaseField(sourceCase, fields, "salePaymentStatus", "Sale payment status", "Pending full payment", now);
        UpsertCaseField(sourceCase, fields, "ownershipTransferStatus", "Ownership transfer status", "Blocked - Estate balance and Legal conveyance required", now);
        sourceCase.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyBillingResultAsync(
            "Property balance invoice created",
            $"Finance AR draft invoice {invoice.InvoiceNumber} was created for {propertyUnit}.",
            "estate.property-management.sale-invoice-created",
            "Invoice",
            invoice.Id,
            $"/finance/ar/invoices/{invoice.Id}",
            BillingMetadata(invoice.InvoiceNumber, invoice.BusinessPartnerId, invoice.CustomerName, invoice.TotalAmount, invoice.CurrencyCode, sourceCase.ReferenceNumber, propertyUnit),
            cancellationToken);

        await NotifyCustomerEstateInvoiceAsync(
            sourceCase,
            fields,
            invoice.Id,
            invoice.InvoiceNumber,
            isLease ? "Lease balance invoice prepared" : "Purchase balance invoice prepared",
            $"Finance is preparing balance invoice {invoice.InvoiceNumber} for {propertyUnit}. Once issued, it will appear with payments and receipts in My Property Requests.",
            "estate.property-management.balance-invoice-customer",
            cancellationToken);

        return Ok(new EstateSaleInvoiceResult(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.TotalAmount,
            invoice.CurrencyCode,
            invoice.Status,
            $"Finance AR draft {invoice.InvoiceNumber} was created for the property {(isLease ? "lease" : "sale")}."));
    }

    [HttpPost("sale/{procedureCaseId:guid}/complete-ownership")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Manager,Property Manager,Head of Estate")]
    public async Task<ActionResult<EstateSaleCompletionResult>> CompleteSaleOwnership(
        Guid procedureCaseId,
        CancellationToken cancellationToken)
    {
        var sourceCase = await LoadSaleProcedureCaseAsync(procedureCaseId, cancellationToken);
        var fields = CaseFields(sourceCase);
        var salePayable = ResolveSalePayable(fields);
        var payableAmount = salePayable.EstateBalance;
        InvoiceDto? invoice = null;
        if (payableAmount > 0m)
        {
            if (!Guid.TryParse(FieldValue(fields, "saleInvoiceId"), out var invoiceId))
                throw new InvalidOperationException("Create and complete the Finance AR sale invoice for the Estate balance first.");
            invoice = await _invoiceService.GetByIdAsync(invoiceId, cancellationToken)
                ?? throw new InvalidOperationException("The linked Finance AR sale invoice was not found.");
            var invoiceAmountMatches = AmountsMatch(invoice.TotalAmount, payableAmount);
            var paidInFull = invoiceAmountMatches
                && invoice.PaidAmount >= payableAmount
                && invoice.BalanceAmount <= 0m
                && string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase);
            if (!paidInFull)
                throw new InvalidOperationException($"Invoice {invoice.InvoiceNumber} must match the Estate balance and be fully paid before ownership transfer.");
        }
        if (!string.Equals(FieldValue(fields, "legalConveyanceStatus"), "Completed by Legal", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Legal must complete conveyance and registration before ownership transfer.");
        var legalCaseIds = await _db.ProcedureCaseFields.AsNoTracking()
            .Where(field => field.TenantId == GetTenantId()
                && !field.IsDeleted
                && field.Key == "sourceProcedureCaseId"
                && field.Value == sourceCase.Id.ToString())
            .Select(field => field.ProcedureCaseId)
            .ToListAsync(cancellationToken);
        if (!await _db.ProcedureCases.AsNoTracking().AnyAsync(item =>
                item.TenantId == GetTenantId()
                && !item.IsDeleted
                && legalCaseIds.Contains(item.Id)
                && item.EntityType == "LegalTransfer"
                && item.Status == "Completed", cancellationToken))
            throw new InvalidOperationException("The linked Legal conveyance case must be completed before ownership transfer.");
        if (!Guid.TryParse(FieldValue(fields, "sourceReference"), out var customerId))
            throw new InvalidOperationException("The purchaser customer reference is missing.");

        var propertyUnit = FieldValue(fields, "propertyUnit");
        var listingReference = FieldValue(fields, "listingReference");
        var asset = await _db.EstateManagedAssets.FirstOrDefaultAsync(item =>
            item.TenantId == GetTenantId()
            && !item.IsDeleted
            && (item.AssetCode == listingReference
                || item.AssetCode == propertyUnit
                || item.ProjectUnitCode == propertyUnit),
            cancellationToken)
            ?? throw new InvalidOperationException("The property asset linked to the sale was not found.");

        if (asset.Status == EstateManagedAssetStatus.Sold)
        {
            if (asset.CustomerBusinessPartnerId != customerId)
                throw new InvalidOperationException("This property is already sold to another customer.");
            return Ok(new EstateSaleCompletionResult(
                asset.Id, asset.AssetCode, customerId, invoice?.Id, invoice?.InvoiceNumber,
                "Ownership transfer was already completed."));
        }

        var now = DateTime.UtcNow;
        var agreementReference = FieldValue(fields, "finalSignedAgreementReference") ?? FieldValue(fields, "generatedAgreementReference");
        var agreementDate = await ResolveSaleAgreementDateAsync(
            sourceCase.TenantId,
            sourceCase.Id,
            agreementReference,
            cancellationToken) ?? now;
        var ownerHistory = string.IsNullOrWhiteSpace(asset.OwnershipHistoryJson)
            ? new List<ExistingLandOwnerDto>()
            : JsonSerializer.Deserialize<List<ExistingLandOwnerDto>>(asset.OwnershipHistoryJson) ?? [];
        foreach (var owner in ownerHistory.Where(item => item.IsCurrentOwner))
        {
            owner.IsCurrentOwner = false;
            owner.OwnershipEndDate = agreementDate.Date;
        }
        ownerHistory.Add(new ExistingLandOwnerDto
        {
            OwnerName = FieldValue(fields, "customerName") ?? sourceCase.ApplicantName ?? string.Empty,
            OwnershipType = "Sale purchaser",
            InterestHeld = "Ownership interest",
            OwnershipStartDate = agreementDate.Date,
            OwnershipPercentage = 100m,
            IsCurrentOwner = true
        });
        asset.OwnershipHistoryJson = JsonSerializer.Serialize(ownerHistory);
        asset.Status = EstateManagedAssetStatus.Sold;
        asset.CustomerBusinessPartnerId = customerId;
        asset.LesseeName = FieldValue(fields, "customerName") ?? sourceCase.ApplicantName;
        asset.PropertyFileReference = agreementReference;
        asset.DateOfTenancy = agreementDate.Date;
        asset.IsAvailableForLease = false;
        asset.IsAvailableForSale = false;
        asset.IsPublishedToExternalPortal = false;
        asset.ExternalListingStatus = "Sold";
        asset.ExternalPublishedAt = null;
        asset.UpdatedAt = now;
        asset.UpdatedBy = User.Identity?.Name ?? "System";
        asset.LastModifiedById = GetUserId();
        UpsertCaseField(sourceCase, fields, "saleInvoiceStatus", "Sale invoice status", invoice?.Status ?? "Not required", now);
        UpsertCaseField(sourceCase, fields, "saleInvoiceAmount", "Sale invoice amount", (invoice?.TotalAmount ?? 0m).ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "saleInvoicePaidAmount", "Sale invoice paid amount", (invoice?.PaidAmount ?? 0m).ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "saleInvoiceBalance", "Sale invoice balance", (invoice?.BalanceAmount ?? 0m).ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "estateRemainingAmount", "Balance for Estate processing", salePayable.EstateBalance.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "salePaymentCheckStatus", "Sale payment check", BuildSalePaymentCheckMessage(FieldValue(fields, "currency") ?? invoice?.CurrencyCode ?? "GHS", salePayable, invoice?.PaidAmount ?? 0m, invoice?.BalanceAmount ?? 0m), now);
        UpsertCaseField(sourceCase, fields, "salePaymentStatus", "Sale payment status", "Paid in full", now);
        UpsertCaseField(sourceCase, fields, "ownershipTransferStatus", "Ownership transfer status", "Completed - purchaser recorded as owner", now);
        UpsertCaseField(sourceCase, fields, "applicationStatus", "Request status", "Sale completed", now);
        UpsertCaseField(sourceCase, fields, "agreementDate", "Agreement date", agreementDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), now);
        sourceCase.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        if (sourceCase.OpenedById != Guid.Empty)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    new CreateNotificationDto
                    {
                        RecipientId = sourceCase.OpenedById,
                        Type = "estate.property.sale-completed",
                        Title = "Property purchase completed",
                        Message = $"Ownership transfer for {asset.AssetCode} is complete. The property is now available in My Properties.",
                        Priority = "High",
                        EntityType = "ProcedureCase",
                        EntityId = sourceCase.Id,
                        ActionUrl = $"/external-portal/my-properties/{asset.Id}",
                        Metadata = new Dictionary<string, object>
                        {
                            ["assetId"] = asset.Id,
                            ["invoiceId"] = invoice?.Id ?? Guid.Empty,
                            ["invoiceNumber"] = invoice?.InvoiceNumber ?? FieldValue(fields, "salesPaymentReference") ?? string.Empty,
                            ["agreementReference"] = asset.PropertyFileReference ?? string.Empty
                        }
                    },
                    GetUserId() ?? sourceCase.OpenedById,
                    GetTenantId());
            }
            catch
            {
                // Notification delivery must not roll back a completed ownership transfer.
            }
        }

        return Ok(new EstateSaleCompletionResult(
            asset.Id,
            asset.AssetCode,
            customerId,
            invoice?.Id,
            invoice?.InvoiceNumber,
            "Ownership transfer completed. The property is sold and removed from portal listings."));
    }

    [HttpPost("premium/{procedureCaseId:guid}/invoice")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer")]
    public async Task<ActionResult<EstatePremiumChargeInvoiceResult>> CreatePremiumChargeInvoice(
        Guid procedureCaseId,
        CancellationToken cancellationToken)
    {
        var sourceCase = await LoadListingProcedureCaseAsync(procedureCaseId, cancellationToken);
        var fields = CaseFields(sourceCase);
        if (!IsPremiumChargeRequired(fields))
            throw new InvalidOperationException("Premium charge is not required for this request.");

        var premiumAmount = ResolvePremiumChargeAmount(fields);
        if (Guid.TryParse(FieldValue(fields, "premiumChargeInvoiceId"), out var existingInvoiceId))
        {
            var existingInvoice = await _invoiceService.GetByIdAsync(existingInvoiceId, cancellationToken);
            if (existingInvoice is not null)
            {
                var existingResult = BuildPremiumChargeInvoiceResult(
                    existingInvoice,
                    premiumAmount,
                    "Premium charge invoice already exists.");
                await PersistPremiumChargePaymentStatusAsync(sourceCase, existingResult, cancellationToken);
                return Ok(existingResult);
            }
        }

        if (!Guid.TryParse(FieldValue(fields, "sourceReference"), out var customerId))
            throw new InvalidOperationException("The property request is not linked to a Finance AR customer.");

        var revenueAccountId = await FindPropertyManagementRevenueAccountIdAsync(cancellationToken);
        var propertyUnit = FieldValue(fields, "propertyUnit") ?? FieldValue(fields, "listingReference") ?? sourceCase.Title;
        var invoice = await _invoiceService.CreateAsync(new InvoiceCreateDto
        {
            BusinessPartnerId = customerId,
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date,
            Reference = BuildPremiumInvoiceReference(sourceCase.ReferenceNumber ?? sourceCase.Id.ToString()),
            CurrencyCode = FieldValue(fields, "currency") ?? "GHS",
            ExchangeRate = 1m,
            Notes = EnsureSourceLabel($"Premium charge for {propertyUnit}."),
            LineItems =
            [
                new InvoiceLineItemCreateDto
                {
                    LineItemType = "GLAccount",
                    GLAccountId = revenueAccountId,
                    Description = $"Premium charge: {propertyUnit}",
                    Quantity = 1m,
                    UnitPrice = premiumAmount,
                    DiscountPercentage = 0m
                }
            ]
        }, cancellationToken);

        var result = BuildPremiumChargeInvoiceResult(
            invoice,
            premiumAmount,
            $"Finance AR draft {invoice.InvoiceNumber} was created for the premium charge.");
        await PersistPremiumChargePaymentStatusAsync(sourceCase, result, cancellationToken);

        await NotifyBillingResultAsync(
            "Premium charge invoice created",
            $"Finance AR draft invoice {invoice.InvoiceNumber} was created for {propertyUnit}.",
            "estate.property-management.premium-invoice-created",
            "Invoice",
            invoice.Id,
            $"/finance/ar/invoices/{invoice.Id}",
            BillingMetadata(invoice.InvoiceNumber, invoice.BusinessPartnerId, invoice.CustomerName, invoice.TotalAmount, invoice.CurrencyCode, sourceCase.ReferenceNumber, propertyUnit),
            cancellationToken);

        await NotifyCustomerPremiumChargeAsync(
            sourceCase,
            fields,
            result,
            "Premium charge invoice prepared",
            $"Premium charge invoice {invoice.InvoiceNumber} is with Finance. It will appear under Bills & Receipts after Finance issues it; Estate will continue the agreement after payment is confirmed.",
            "estate.property-management.premium-invoice-customer",
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("sale/{procedureCaseId:guid}/sync-payment-status")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Head of Estate")]
    public async Task<ActionResult<EstateSalePaymentStatusResult>> SyncSalePaymentStatus(
        Guid procedureCaseId,
        CancellationToken cancellationToken)
    {
        var sourceCase = await LoadSaleProcedureCaseAsync(procedureCaseId, cancellationToken, allowLease: true);
        var fields = CaseFields(sourceCase);
        var result = await SyncSalePaymentStatusAsync(sourceCase, fields, cancellationToken);
        await PersistSalePaymentStatusAsync(sourceCase, result, cancellationToken);
        await NotifySalePaymentReceivedAsync(sourceCase, fields, result, cancellationToken);
        if (string.Equals(result.PaymentStatus, "Paid in full", StringComparison.OrdinalIgnoreCase)
            && result.InvoiceId.HasValue)
            await NotifyCustomerEstateInvoiceAsync(sourceCase, fields, result.InvoiceId, result.InvoiceNumber,
                "Property balance payment confirmed",
                $"Finance confirmed full payment of invoice {result.InvoiceNumber}. Estate can now continue conveyance.",
                "estate.property-management.balance-payment-customer", cancellationToken);

        return Ok(result);
    }

    [HttpPost("premium/{procedureCaseId:guid}/sync-payment-status")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Head of Estate,Finance Officer")]
    public async Task<ActionResult<EstatePremiumChargeInvoiceResult>> SyncPremiumChargePaymentStatus(
        Guid procedureCaseId,
        CancellationToken cancellationToken)
    {
        var sourceCase = await LoadListingProcedureCaseAsync(procedureCaseId, cancellationToken);
        var fields = CaseFields(sourceCase);
        var result = await SyncPremiumChargePaymentStatusAsync(sourceCase, fields, cancellationToken);
        await PersistPremiumChargePaymentStatusAsync(sourceCase, result, cancellationToken);
        await NotifyPremiumChargePaymentReceivedAsync(sourceCase, fields, result, cancellationToken);

        return Ok(result);
    }

    [HttpPost("invoices")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer")]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice(
        [FromBody] EstatePropertyArInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        request.Invoice.Notes = EnsureSourceLabel(request.Invoice.Notes);
        var invoice = await _invoiceService.CreateAsync(request.Invoice, cancellationToken);
        await NotifyBillingResultAsync(
            "Finance AR invoice created",
            $"Finance AR invoice {invoice.InvoiceNumber} was created from Estate / Property Management.",
            "estate.property-management.ar.invoice-created",
            "Invoice",
            invoice.Id,
            $"/finance/ar/invoices/{invoice.Id}",
            BillingMetadata(invoice.InvoiceNumber, invoice.BusinessPartnerId, invoice.CustomerName, invoice.TotalAmount, invoice.CurrencyCode, request.SourceRecordReference, request.PropertyUnit),
            cancellationToken);

        return Ok(invoice);
    }

    [HttpPost("payments")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer")]
    public async Task<ActionResult<CustomerPaymentDto>> CreatePayment(
        [FromBody] EstatePropertyArPaymentRequest request,
        CancellationToken cancellationToken)
    {
        request.Payment.Notes = EnsureSourceLabel(request.Payment.Notes);
        var payment = await _paymentService.CreateAsync(request.Payment, cancellationToken);
        await NotifyBillingResultAsync(
            "Finance AR receipt recorded",
            $"Finance AR receipt {payment.PaymentNumber} was recorded from Estate / Property Management.",
            "estate.property-management.ar.receipt-recorded",
            "CustomerPayment",
            payment.Id,
            $"/finance/ar/payments/{payment.Id}",
            BillingMetadata(payment.PaymentNumber, payment.BusinessPartnerId, payment.CustomerName, payment.TotalAmount, payment.CurrencyCode, request.SourceRecordReference, request.PropertyUnit),
            cancellationToken);

        return Ok(payment);
    }

    [HttpPost("results")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Property Manager,Property Officer,Finance Officer")]
    public async Task<IActionResult> NotifyFinanceArResult(
        [FromBody] EstatePropertyArResultRequest request,
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

        await NotifyBillingResultAsync(
            title,
            $"{reference} result was returned to Estate / Property Management: {title}.",
            $"estate.property-management.ar.{NormalizeNotificationToken(request.ActionType)}",
            "FinanceArResult",
            request.FinanceArEntityId ?? Guid.NewGuid(),
            request.ActionUrl ?? "/estate/property-management/EstatePropertyManagementBillingServiceCharge",
            new Dictionary<string, object>
            {
                ["sourceLabel"] = SourceLabel,
                ["sourceModule"] = "Estate / Property Management",
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
            new[] { "Property Manager", "Property Officer", "Estate Manager", "Estate Officer", "Facilities Manager" },
            title,
            message,
            type,
            entityType,
            entityId,
            actionUrl,
            metadata,
            cancellationToken);
    }

    private static Dictionary<string, object> BillingMetadata(
        string financeReference,
        Guid customerId,
        string customerName,
        decimal amount,
        string currencyCode,
        string? sourceRecordReference,
        string? propertyUnit)
        => new()
        {
            ["sourceLabel"] = SourceLabel,
            ["sourceModule"] = "Estate / Property Management",
            ["financeArReference"] = financeReference,
            ["customerId"] = customerId,
            ["customerName"] = customerName,
            ["amount"] = amount,
            ["currencyCode"] = currencyCode,
            ["sourceRecordReference"] = sourceRecordReference ?? string.Empty,
            ["propertyUnit"] = propertyUnit ?? string.Empty
        };

    private async Task NotifySalePaymentReceivedAsync(
        ProcedureCase sourceCase,
        IDictionary<string, ProcedureCaseField> fields,
        EstateSalePaymentStatusResult result,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(result.PaymentStatus, "Paid in full", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        const string notificationType = "estate.property-management.sale-payment-received";
        var tenantId = GetTenantId();
        var alreadyNotified = await _db.Notifications
            .AsNoTracking()
            .AnyAsync(notification => notification.TenantId == tenantId
                && !notification.IsDeleted
                && notification.NotificationType == notificationType
                && notification.EntityType == "ProcedureCase"
                && notification.EntityId == sourceCase.Id,
                cancellationToken);
        if (alreadyNotified)
        {
            return;
        }

        var propertyUnit = FieldValue(fields, "propertyUnit")
            ?? FieldValue(fields, "listingReference")
            ?? sourceCase.Title
            ?? sourceCase.ReferenceNumber
            ?? sourceCase.Id.ToString();
        var legalCompleted = string.Equals(
            FieldValue(fields, "legalConveyanceStatus"),
            "Completed by Legal",
            StringComparison.OrdinalIgnoreCase);
        var message = legalCompleted
            ? $"Finance AR invoice {result.InvoiceNumber} is paid in full for {propertyUnit}. Refresh the Property Management sale request and complete ownership transfer."
            : $"Finance AR invoice {result.InvoiceNumber} is paid in full for {propertyUnit}. Refresh the Property Management sale request; Legal conveyance and registration is still pending.";

        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                GetUserId(),
                new[] { "Property Manager", "Property Officer", "Estate Manager", "Estate Officer", "Head of Estate" },
                "Sale payment received",
                message,
                notificationType,
                "ProcedureCase",
                sourceCase.Id,
                $"/estate/property-management/EstatePropertyManagementListingApplication?caseId={sourceCase.Id}",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = SourceLabel,
                    ["sourceModule"] = "Estate / Property Management",
                    ["sourceRecordReference"] = sourceCase.ReferenceNumber ?? sourceCase.Id.ToString(),
                    ["propertyUnit"] = propertyUnit,
                    ["invoiceId"] = result.InvoiceId,
                    ["invoiceNumber"] = result.InvoiceNumber,
                    ["invoiceStatus"] = result.InvoiceStatus,
                    ["paymentStatus"] = result.PaymentStatus,
                    ["ownershipTransferStatus"] = result.OwnershipTransferStatus,
                    ["totalAmount"] = result.TotalAmount,
                    ["paidAmount"] = result.PaidAmount,
                    ["balanceAmount"] = result.BalanceAmount
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block Estate from syncing the paid invoice status.
        }
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

    private static string BuildRentInvoiceReference(string assetCode, DateTime billingDate)
    {
        var reference = $"RENT-{assetCode}-{billingDate:yyyyMM}";
        return reference.Length <= 100 ? reference : reference[..100];
    }

    private async Task<ErpSystem.Core.Entities.Estate.EstateManagedAsset> FindRentalAssetAsync(
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var asset = await _db.EstateManagedAssets.FirstOrDefaultAsync(item => item.Id == assetId
            && item.TenantId == GetTenantId()
            && !item.IsDeleted,
            cancellationToken)
            ?? throw new KeyNotFoundException("The Estate property or unit was not found.");
        if (asset.AssetType == EstateManagedAssetType.Land)
            throw new InvalidOperationException("Use Ground Rent Administration for land lease penalties.");
        if (asset.Status is not EstateManagedAssetStatus.Leased and not EstateManagedAssetStatus.Occupied)
            throw new InvalidOperationException("Rental penalty terms apply only to leased or occupied properties.");
        return asset;
    }

    private async Task<ProcedureCase> LoadSaleProcedureCaseAsync(
        Guid procedureCaseId,
        CancellationToken cancellationToken,
        bool allowLease = false)
    {
        var sourceCase = await LoadListingProcedureCaseAsync(procedureCaseId, cancellationToken);
        var fields = CaseFields(sourceCase);
        var requestType = FieldValue(fields, "requestType") ?? sourceCase.Title;
        if (!requestType.Contains("sale", StringComparison.OrdinalIgnoreCase)
            && !requestType.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            && !(allowLease && requestType.Contains("lease", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This operation applies only to property sale or lease requests.");
        return sourceCase;
    }

    private async Task<ProcedureCase> LoadListingProcedureCaseAsync(
        Guid procedureCaseId,
        CancellationToken cancellationToken)
    {
        var sourceCase = await _db.ProcedureCases
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == GetTenantId()
                && item.Id == procedureCaseId
                && !item.IsDeleted
                && item.Module == "PropertyManagement"
                && item.EntityType == "EstatePropertyManagementListingApplication",
                cancellationToken)
            ?? throw new KeyNotFoundException("The property listing request was not found.");
        return sourceCase;
    }

    private async Task<DateTime?> ResolveSaleAgreementDateAsync(
        Guid tenantId,
        Guid sourceCaseId,
        string? agreementReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agreementReference))
        {
            return null;
        }

        var record = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(item => item.Versions.Where(version => !version.IsDeleted))
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.SourceModule == "Estate"
                && (item.DocumentReference == agreementReference
                    || item.SourceRecordId == sourceCaseId))
            .OrderByDescending(item => item.DocumentReference == agreementReference)
            .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : ResolveAgreementDate(record);
    }

    private static DateTime? ResolveAgreementDate(CentralDocumentRecord record)
    {
        var signedVersionDate = record.Versions
            .Where(version => string.Equals(version.Status, "Signed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(version => version.PublishedAt ?? version.UpdatedAt ?? version.CreatedAt)
            .Select(version => (DateTime?)(version.PublishedAt ?? version.UpdatedAt ?? version.CreatedAt))
            .FirstOrDefault();

        return signedVersionDate
            ?? record.PublishedAt
            ?? record.EffectiveDate
            ?? record.UpdatedAt
            ?? record.CreatedAt;
    }

    private static Dictionary<string, ProcedureCaseField> CaseFields(ProcedureCase procedureCase)
        => procedureCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);

    private static string? FieldValue(
        IDictionary<string, ProcedureCaseField> fields,
        string key)
        => fields.TryGetValue(key, out var field) ? field.Value : null;

    private async Task<EstateSalePaymentStatusResult> SyncSalePaymentStatusAsync(
        ProcedureCase sourceCase,
        IDictionary<string, ProcedureCaseField> fields,
        CancellationToken cancellationToken)
    {
        var salePayable = ResolveSalePayable(fields);
        if (salePayable.EstateBalance <= 0m)
        {
            return new EstateSalePaymentStatusResult(
                null,
                FieldValue(fields, "salesPaymentReference"),
                "Not required",
                0m,
                0m,
                0m,
                "Paid in full",
                string.Equals(FieldValue(fields, "legalConveyanceStatus"), "Completed by Legal", StringComparison.OrdinalIgnoreCase)
                    ? "Ready for ownership transfer"
                    : "Blocked - Legal conveyance and registration pending",
                BuildSalePaymentCheckMessage(FieldValue(fields, "currency") ?? "GHS", salePayable, 0m, 0m));
        }

        if (!Guid.TryParse(FieldValue(fields, "saleInvoiceId"), out var invoiceId))
            throw new InvalidOperationException("Create the Finance AR sale invoice for the Estate balance before syncing payment status.");

        var invoice = await _invoiceService.GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("The linked Finance AR sale invoice was not found.");

        var payableAmount = salePayable.EstateBalance;
        var invoiceAmountMatches = AmountsMatch(invoice.TotalAmount, payableAmount);
        var paidInFull = invoiceAmountMatches
            && invoice.PaidAmount >= payableAmount
            && invoice.BalanceAmount <= 0m
            && string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase);
        var paymentStatus = paidInFull ? "Paid in full" : "Pending full payment";
        var paymentCheckStatus = invoiceAmountMatches
            ? BuildSalePaymentCheckMessage(invoice.CurrencyCode, salePayable, invoice.PaidAmount, invoice.BalanceAmount)
            : $"Invoice total {invoice.CurrencyCode} {invoice.TotalAmount:N2} does not match Estate balance {invoice.CurrencyCode} {payableAmount:N2}. Sales already recorded {invoice.CurrencyCode} {salePayable.SalesAmountPaid:N2} against approved amount {invoice.CurrencyCode} {salePayable.ApprovedAmount:N2}.";
        var legalCompleted = string.Equals(
            FieldValue(fields, "legalConveyanceStatus"),
            "Completed by Legal",
            StringComparison.OrdinalIgnoreCase);
        var ownershipStatus = paidInFull
            ? legalCompleted
                ? "Ready for ownership transfer"
                : "Blocked - Legal conveyance and registration pending"
            : "Blocked - full payment and Legal conveyance required";

        return new EstateSalePaymentStatusResult(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            paymentStatus,
            ownershipStatus,
            paymentCheckStatus);
    }

    private async Task<EstatePremiumChargeInvoiceResult> SyncPremiumChargePaymentStatusAsync(
        ProcedureCase sourceCase,
        IDictionary<string, ProcedureCaseField> fields,
        CancellationToken cancellationToken)
    {
        if (!IsPremiumChargeRequired(fields))
        {
            return new EstatePremiumChargeInvoiceResult(
                null,
                null,
                "Not required",
                0m,
                0m,
                0m,
                FieldValue(fields, "currency") ?? "GHS",
                "Not required",
                "Premium charge is not required for this request.");
        }

        var premiumAmount = ResolvePremiumChargeAmount(fields);
        if (!Guid.TryParse(FieldValue(fields, "premiumChargeInvoiceId"), out var invoiceId))
            throw new InvalidOperationException("Create the premium charge invoice before syncing payment status.");

        var invoice = await _invoiceService.GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("The linked premium charge invoice was not found.");

        return BuildPremiumChargeInvoiceResult(
            invoice,
            premiumAmount,
            IsPremiumChargeInvoicePaid(invoice, premiumAmount)
                ? "Premium charge payment has been confirmed by Finance. Estate can continue the agreement."
                : $"Premium charge invoice {invoice.InvoiceNumber} is still awaiting full payment in Finance.");
    }

    private async Task PersistSalePaymentStatusAsync(
        ProcedureCase sourceCase,
        EstateSalePaymentStatusResult result,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var currentUserId = GetUserId();
        var fieldValues = new[]
        {
            new SalePaymentFieldUpdate("saleInvoiceReference", "Balance invoice reference", result.InvoiceNumber),
            new SalePaymentFieldUpdate("saleInvoiceStatus", "Balance invoice status", result.InvoiceStatus),
            new SalePaymentFieldUpdate("saleInvoiceAmount", "Balance invoice amount", result.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)),
            new SalePaymentFieldUpdate("saleInvoicePaidAmount", "Balance invoice paid amount", result.PaidAmount.ToString("0.00", CultureInfo.InvariantCulture)),
            new SalePaymentFieldUpdate("saleInvoiceBalance", "Balance invoice remaining", result.BalanceAmount.ToString("0.00", CultureInfo.InvariantCulture)),
            new SalePaymentFieldUpdate("estateRemainingAmount", "Balance for Estate processing", result.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)),
            new SalePaymentFieldUpdate("salePaymentCheckStatus", "Sale or lease payment check", result.Message),
            new SalePaymentFieldUpdate("salePaymentStatus", "Sale or lease payment status", result.PaymentStatus),
            new SalePaymentFieldUpdate("ownershipTransferStatus", "Ownership transfer status", result.OwnershipTransferStatus)
        };

        foreach (var fieldValue in fieldValues)
        {
            var updated = await _db.ProcedureCaseFields
                .Where(field => field.TenantId == sourceCase.TenantId
                    && field.ProcedureCaseId == sourceCase.Id
                    && field.Key == fieldValue.Key
                    && !field.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(field => field.Label, fieldValue.Label)
                    .SetProperty(field => field.FieldType, "text")
                    .SetProperty(field => field.Value, fieldValue.Value)
                    .SetProperty(field => field.UpdatedAt, now)
                    .SetProperty(field => field.LastModifiedById, currentUserId),
                    cancellationToken);

            if (updated > 0)
            {
                continue;
            }

            _db.ProcedureCaseFields.Add(new ProcedureCaseField
            {
                TenantId = sourceCase.TenantId,
                ProcedureCaseId = sourceCase.Id,
                Key = fieldValue.Key,
                Label = fieldValue.Label,
                FieldType = "text",
                Value = fieldValue.Value,
                CreatedAt = now,
                CreatedById = currentUserId
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        await _db.ProcedureCases
            .Where(item => item.TenantId == sourceCase.TenantId
                && item.Id == sourceCase.Id
                && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LastModifiedById, currentUserId),
                cancellationToken);
    }

    private sealed record SalePaymentFieldUpdate(string Key, string Label, string? Value);

    private async Task<Guid> FindPropertyManagementRevenueAccountIdAsync(CancellationToken cancellationToken)
        => await _db.Accounts
            .AsNoTracking()
            .Where(item => item.TenantId == GetTenantId()
                && !item.IsDeleted
                && item.Status == AccountStatus.Active
                && item.AccountType == AccountType.Revenue
                && item.AllowDirectPosting
                && !item.IsControlAccount
                && item.AccountCode == "4100")
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Finance must configure the active property management revenue account 4100.");

    private async Task PersistPremiumChargePaymentStatusAsync(
        ProcedureCase sourceCase,
        EstatePremiumChargeInvoiceResult result,
        CancellationToken cancellationToken)
    {
        var fields = CaseFields(sourceCase);
        var now = DateTime.UtcNow;
        UpsertCaseField(sourceCase, fields, "premiumChargeInvoiceId", "Premium charge invoice ID", result.InvoiceId?.ToString(), now);
        UpsertCaseField(sourceCase, fields, "premiumChargeInvoiceReference", "Premium charge invoice reference", result.InvoiceNumber, now);
        UpsertCaseField(sourceCase, fields, "premiumChargeInvoiceStatus", "Premium charge invoice status", result.InvoiceStatus, now);
        UpsertCaseField(sourceCase, fields, "premiumChargeAmount", "Premium charge amount", result.Amount.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "premiumChargePaidAmount", "Premium charge paid amount", result.PaidAmount.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "premiumChargeBalance", "Premium charge balance", result.BalanceAmount.ToString("0.00", CultureInfo.InvariantCulture), now);
        UpsertCaseField(sourceCase, fields, "premiumChargePaymentStatus", "Premium charge payment status", result.PaymentStatus, now);
        UpsertCaseField(sourceCase, fields, "applicationStatus", "Request status", result.PaymentStatus == "Paid" ? "Premium charge paid" : result.PaymentStatus, now);
        sourceCase.UpdatedAt = now;
        sourceCase.LastModifiedById = GetUserId();
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyPremiumChargePaymentReceivedAsync(
        ProcedureCase sourceCase,
        IDictionary<string, ProcedureCaseField> fields,
        EstatePremiumChargeInvoiceResult result,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(result.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        const string notificationType = "estate.property-management.premium-payment-received";
        var tenantId = GetTenantId();
        var alreadyNotified = await _db.Notifications
            .AsNoTracking()
            .AnyAsync(notification => notification.TenantId == tenantId
                && !notification.IsDeleted
                && notification.NotificationType == notificationType
                && notification.EntityType == "ProcedureCase"
                && notification.EntityId == sourceCase.Id,
                cancellationToken);
        if (alreadyNotified)
        {
            return;
        }

        var propertyUnit = FieldValue(fields, "propertyUnit")
            ?? FieldValue(fields, "listingReference")
            ?? sourceCase.Title
            ?? sourceCase.ReferenceNumber
            ?? sourceCase.Id.ToString();

        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                GetUserId(),
                new[] { "Property Manager", "Property Officer", "Estate Manager", "Estate Officer", "Head of Estate" },
                "Premium charge payment received",
                $"Finance confirmed premium charge invoice {result.InvoiceNumber} is paid for {propertyUnit}. Estate can now generate the agreement.",
                notificationType,
                "ProcedureCase",
                sourceCase.Id,
                $"/estate/property-management/EstatePropertyManagementListingApplication?caseId={sourceCase.Id}",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = SourceLabel,
                    ["sourceModule"] = "Estate / Property Management",
                    ["sourceRecordReference"] = sourceCase.ReferenceNumber ?? sourceCase.Id.ToString(),
                    ["propertyUnit"] = propertyUnit,
                    ["invoiceId"] = result.InvoiceId,
                    ["invoiceNumber"] = result.InvoiceNumber ?? string.Empty,
                    ["invoiceStatus"] = result.InvoiceStatus,
                    ["paymentStatus"] = result.PaymentStatus,
                    ["amount"] = result.Amount,
                    ["paidAmount"] = result.PaidAmount,
                    ["balanceAmount"] = result.BalanceAmount
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block Estate from syncing the paid invoice status.
        }

        await NotifyCustomerPremiumChargeAsync(
            sourceCase,
            fields,
            result,
            "Premium charge payment confirmed",
            $"Finance has confirmed payment for premium charge invoice {result.InvoiceNumber}. Estate can now continue your agreement.",
            "estate.property-management.premium-payment-customer",
            cancellationToken);
    }

    private async Task NotifyCustomerPremiumChargeAsync(
        ProcedureCase sourceCase,
        IDictionary<string, ProcedureCaseField> fields,
        EstatePremiumChargeInvoiceResult result,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken)
    {
        await NotifyCustomerEstateInvoiceAsync(sourceCase, fields, result.InvoiceId, result.InvoiceNumber,
            title, message, type, cancellationToken);
    }

    private async Task NotifyCustomerEstateInvoiceAsync(
        ProcedureCase sourceCase,
        IDictionary<string, ProcedureCaseField> fields,
        Guid? invoiceId,
        string? invoiceNumber,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipients = new HashSet<Guid>();
            if (sourceCase.OpenedById != Guid.Empty
                && sourceCase.SourceDepartment?.StartsWith("External Portal", StringComparison.OrdinalIgnoreCase) == true)
                recipients.Add(sourceCase.OpenedById);

            if (Guid.TryParse(FieldValue(fields, "sourceReference"), out var customerId))
            {
                var primaryUserId = await _db.BusinessPartners.AsNoTracking()
                    .Where(customer => customer.TenantId == GetTenantId() && customer.Id == customerId && !customer.IsDeleted)
                    .Select(customer => customer.UserId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (primaryUserId.HasValue)
                    recipients.Add(primaryUserId.Value);
                var linkedUserIds = await _db.BusinessPartnerUsers.AsNoTracking()
                    .Where(link => link.TenantId == GetTenantId()
                        && link.BusinessPartnerId == customerId && !link.IsDeleted && link.IsActive)
                    .Select(link => link.UserId)
                    .ToListAsync(cancellationToken);
                recipients.UnionWith(linkedUserIds);
            }

            var notifiedRecipients = await _db.Notifications.AsNoTracking()
                .Where(notification => notification.TenantId == GetTenantId()
                    && !notification.IsDeleted
                    && notification.NotificationType == type
                    && notification.EntityType == "ProcedureCase"
                    && notification.EntityId == sourceCase.Id)
                .Select(notification => notification.RecipientId)
                .ToListAsync(cancellationToken);
            recipients.ExceptWith(notifiedRecipients);

            foreach (var recipientId in recipients)
            await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = recipientId,
                    Type = type,
                    Title = title,
                    Message = message,
                    Priority = "High",
                    EntityType = "ProcedureCase",
                    EntityId = sourceCase.Id,
                    ActionUrl = $"/external-portal/my-property-requests/{sourceCase.Id}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["sourceLabel"] = SourceLabel,
                        ["sourceModule"] = "Estate / Property Management",
                        ["sourceRecordReference"] = sourceCase.ReferenceNumber ?? sourceCase.Id.ToString(),
                        ["propertyUnit"] = FieldValue(fields, "propertyUnit") ?? FieldValue(fields, "listingReference") ?? string.Empty,
                        ["invoiceId"] = invoiceId,
                        ["invoiceNumber"] = invoiceNumber ?? string.Empty
                    }
                },
                GetUserId() ?? recipientId,
                GetTenantId());
        }
        catch
        {
            // Customer notification delivery must not roll back the Finance billing status.
        }
    }

    private static SalePayableSnapshot ResolveSalePayable(IDictionary<string, ProcedureCaseField> fields)
    {
        var amountText = FieldValue(fields, "offerAmount") ?? FieldValue(fields, "listingPrice");
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var purchasePrice)
            || purchasePrice <= 0m)
            throw new InvalidOperationException("Record an approved sale or lease amount before creating the balance invoice.");

        var salesAmountPaid = ParseOptionalMoney(FieldValue(fields, "salesAmountPaid"));
        if (salesAmountPaid < 0m)
            throw new InvalidOperationException("Amount paid in Sales cannot be negative.");
        if (salesAmountPaid > purchasePrice)
            throw new InvalidOperationException("Amount paid in Sales cannot be greater than the approved sale or lease amount.");

        return new SalePayableSnapshot(
            purchasePrice,
            salesAmountPaid,
            decimal.Round(purchasePrice - salesAmountPaid, 2, MidpointRounding.AwayFromZero));
    }

    private static decimal ResolveSalePayableAmount(IDictionary<string, ProcedureCaseField> fields)
        => ResolveSalePayable(fields).EstateBalance;

    private static bool IsPremiumChargeRequired(IDictionary<string, ProcedureCaseField> fields)
    {
        var value = FieldValue(fields, "premiumChargeRequired")?.Trim().ToLowerInvariant();
        return value is "yes" or "true" or "required";
    }

    private static decimal ResolvePremiumChargeAmount(IDictionary<string, ProcedureCaseField> fields)
    {
        var amountText = FieldValue(fields, "premiumChargeAmount");
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var premiumAmount)
            || premiumAmount <= 0m)
            throw new InvalidOperationException("Record a premium charge amount before creating the premium invoice.");

        return decimal.Round(premiumAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ParseOptionalMoney(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? 0m
            : decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new InvalidOperationException("Amount paid in Sales must be a valid number.");

    private static string BuildSalePaymentCheckMessage(
        string currency,
        SalePayableSnapshot salePayable,
        decimal estatePaidAmount,
        decimal estateBalanceAmount)
    {
        var totalPaid = salePayable.SalesAmountPaid + estatePaidAmount;
        if (estateBalanceAmount <= 0m)
        {
            return $"Paid in full; Sales paid {currency} {salePayable.SalesAmountPaid:N2}, Estate paid {currency} {estatePaidAmount:N2}, total paid {currency} {totalPaid:N2} of {currency} {salePayable.ApprovedAmount:N2}.";
        }

        return $"Awaiting Estate balance; Sales paid {currency} {salePayable.SalesAmountPaid:N2}, Estate paid {currency} {estatePaidAmount:N2}, total paid {currency} {totalPaid:N2} of {currency} {salePayable.ApprovedAmount:N2}; Estate balance {currency} {estateBalanceAmount:N2}.";
    }

    private sealed record SalePayableSnapshot(
        decimal ApprovedAmount,
        decimal SalesAmountPaid,
        decimal EstateBalance);

    private static bool AmountsMatch(decimal left, decimal right)
        => Math.Abs(left - right) < 0.01m;

    private static bool IsPremiumChargeInvoicePaid(InvoiceDto invoice, decimal premiumAmount)
        => AmountsMatch(invoice.TotalAmount, premiumAmount)
            && invoice.PaidAmount >= premiumAmount
            && invoice.BalanceAmount <= 0m
            && string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase);

    private static EstatePremiumChargeInvoiceResult BuildPremiumChargeInvoiceResult(
        InvoiceDto invoice,
        decimal premiumAmount,
        string message)
    {
        var paid = IsPremiumChargeInvoicePaid(invoice, premiumAmount);
        return new EstatePremiumChargeInvoiceResult(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.Status,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            invoice.CurrencyCode,
            paid ? "Paid" : "Payment pending",
            message);
    }

    private void UpsertCaseField(
        ProcedureCase procedureCase,
        IDictionary<string, ProcedureCaseField> fields,
        string key,
        string label,
        string? value,
        DateTime now)
    {
        if (fields.TryGetValue(key, out var field))
        {
            field.Value = value;
            field.UpdatedAt = now;
            field.LastModifiedById = GetUserId();
            return;
        }

        field = new ProcedureCaseField
        {
            TenantId = procedureCase.TenantId,
            ProcedureCaseId = procedureCase.Id,
            Key = key,
            Label = label,
            FieldType = "text",
            Value = value,
            CreatedAt = now,
            CreatedById = GetUserId()
        };
        procedureCase.Fields.Add(field);
        fields[key] = field;
    }

    private static string BuildSaleInvoiceReference(string sourceReference, bool isLease = false)
    {
        var reference = $"PROPERTY-{(isLease ? "LEASE" : "SALE")}-{sourceReference}";
        return reference.Length <= 100 ? reference : reference[..100];
    }

    private static string BuildPremiumInvoiceReference(string sourceReference)
    {
        var reference = $"PROPERTY-PREMIUM-{sourceReference}";
        return reference.Length <= 100 ? reference : reference[..100];
    }

    private static string NormalizePenaltyMethod(string? method)
        => method?.Trim().ToLowerInvariant() switch
        {
            "none" or null or "" => "None",
            "fixed" => "Fixed",
            "percentage" => "Percentage",
            _ => throw new InvalidOperationException("Penalty method must be None, Fixed, or Percentage.")
        };

    private static string BuildRentPenaltyReference(string assetCode, string invoiceNumber)
    {
        var reference = $"RENT-PEN-{assetCode}-{invoiceNumber}";
        return reference.Length <= 100 ? reference : reference[..100];
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

public sealed record EstatePropertyArInvoiceRequest(
    InvoiceCreateDto Invoice,
    string? SourceRecordReference,
    string? PropertyUnit);

public sealed record EstateRentBillingActivationResult(
    bool Activated,
    DateTime ActivatedAt,
    DateTime? NextBillingDate,
    Guid? InvoiceId,
    string? InvoiceNumber,
    string Message);

public sealed record EstateSaleInvoiceResult(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string Message);

public sealed record EstateSalePaymentStatusResult(
    Guid? InvoiceId,
    string? InvoiceNumber,
    string InvoiceStatus,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PaymentStatus,
    string OwnershipTransferStatus,
    string Message);

public sealed record EstatePremiumChargeInvoiceResult(
    Guid? InvoiceId,
    string? InvoiceNumber,
    string InvoiceStatus,
    decimal Amount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string CurrencyCode,
    string PaymentStatus,
    string Message);

public sealed record EstateSaleCompletionResult(
    Guid AssetId,
    string AssetCode,
    Guid PurchaserCustomerId,
    Guid? InvoiceId,
    string? InvoiceNumber,
    string Message);

public sealed record UpdateEstateRentPenaltyTermsRequest(
    int GracePeriodDays,
    string PenaltyMethod,
    decimal PenaltyValue,
    decimal? PenaltyCapAmount);

public sealed record UpdateEstateRentScheduleRequest(decimal MonthlyRent, DateTime NextBillingDate, bool Enabled);

public sealed record EstateRentPenaltyTermsResult(
    Guid AssetId,
    int GracePeriodDays,
    string PenaltyMethod,
    decimal PenaltyValue,
    decimal? PenaltyCapAmount,
    string Message);

public sealed record EstateRentPenaltyAssessmentResult(
    Guid PenaltyInvoiceId,
    string PenaltyInvoiceNumber,
    decimal PenaltyAmount,
    string CurrencyCode,
    Guid SourceInvoiceId,
    string SourceInvoiceNumber,
    string Message);

public sealed record EstateRentPenaltyStatusResult(
    Guid AssetId,
    bool IsOverdue,
    bool CanAssessPenalty,
    DateTime? DueDate,
    DateTime? GraceEndsOn,
    decimal OutstandingAmount);

public sealed record EstatePropertyArPaymentRequest(
    PaymentCreateDto Payment,
    string? SourceRecordReference,
    string? PropertyUnit);

public sealed record EstatePropertyArResultRequest(
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
