using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Api.Filters;
using ErpSystem.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Text.Json;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Api.Services.Notifications;
using ErpSystem.Api.Services.Otp;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using QColors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/external/documents")]
[Authorize(Roles = Constants.Roles.ExternalUser)]
public sealed class EstateExternalDocumentsController : ControllerBase
{
    private const string PortalRecipientFieldKey = "dispatchedto";
    private static readonly string[] ActiveSalesAllocationStatuses =
        ["Reserved", "PendingApproval", "Approved", "Allocated", "Sold", "Leased"];
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IProcedureCaseService _procedureCaseService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICentralDocumentRenditionService _renditionService;
    private readonly INotificationService _notificationService;
    private readonly IOpportunityService _opportunityService;
    private readonly IEhcTicketService _ticketService;
    private readonly ICaptchaVerificationService _captchaService;
    private readonly IOtpService _otpService;
    private readonly ITenantSmsSender _tenantSmsSender;
    private readonly ITenantEmailSender _tenantEmailSender;
    private readonly ILogger<EstateExternalDocumentsController> _logger;

    public EstateExternalDocumentsController(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IProcedureCaseService procedureCaseService,
        IFileStorageService fileStorageService,
        ICentralDocumentRenditionService renditionService,
        INotificationService notificationService,
        IOpportunityService opportunityService,
        IEhcTicketService ticketService,
        ICaptchaVerificationService captchaService,
        IOtpService otpService,
        ITenantSmsSender tenantSmsSender,
        ITenantEmailSender tenantEmailSender,
        ILogger<EstateExternalDocumentsController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _procedureCaseService = procedureCaseService;
        _fileStorageService = fileStorageService;
        _renditionService = renditionService;
        _notificationService = notificationService;
        _opportunityService = opportunityService;
        _ticketService = ticketService;
        _captchaService = captchaService;
        _otpService = otpService;
        _tenantSmsSender = tenantSmsSender;
        _tenantEmailSender = tenantEmailSender;
        _logger = logger;
    }

    [HttpGet("/api/estate/external/customer-profiles")]
    public async Task<IActionResult> GetCustomerProfiles(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var customers = await PortalCustomers(tenantId, userId.Value)
            .OrderBy(item => item.PartnerName)
            .ToListAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            data = customers.Select(customer => new
            {
                customer.Id,
                customer.PartnerName,
                customer.PrimaryEmail,
                customer.PrimaryPhone,
                customer.PhysicalAddress,
                customer.CustomerAccountNumber,
                customer.Currency
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyEstateDocuments(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var identities = BuildIdentityTerms();
        if (tenantId == Guid.Empty || identities.Count == 0)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var candidateRecordIds = await _db.CentralDocumentMetadataValues
            .AsNoTracking()
            .Where(value => value.TenantId == tenantId
                && !value.IsDeleted
                && value.FieldValue != null
                && value.FieldKey == PortalRecipientFieldKey)
            .Where(value => identities.Contains(value.FieldValue!.Trim().ToLower()))
            .Select(value => value.DocumentRecordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (candidateRecordIds.Count == 0)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var records = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(record => record.Versions.Where(version => !version.IsDeleted))
            .Include(record => record.MetadataValues.Where(value => !value.IsDeleted))
            .Where(record => record.TenantId == tenantId
                && !record.IsDeleted
                && candidateRecordIds.Contains(record.Id)
                && record.SourceModule == "Estate"
                && record.LifecycleStatus == "Dispatched"
                && record.RepositoryStatus == "Linked")
            .OrderByDescending(record => record.PublishedAt ?? record.UpdatedAt ?? record.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var data = records
            .Select(record =>
            {
                var version = record.Versions
                    .OrderByDescending(item => item.PublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.RenditionPath)
                        || !string.IsNullOrWhiteSpace(item.RepositoryPath));
                var contentUrl = $"/api/estate/external/documents/{record.Id}/content";
                var hasFile = !string.IsNullOrWhiteSpace(version?.RenditionPath)
                    || !string.IsNullOrWhiteSpace(version?.RepositoryPath)
                    || !string.IsNullOrWhiteSpace(record.RepositoryPath);

                return new
                {
                    record.Id,
                    documentRecordId = record.Id,
                    versionId = version?.Id,
                    record.DocumentReference,
                    record.Title,
                    record.SourceLabel,
                    record.SourceEntityType,
                    record.SourceRecordReference,
                    record.LifecycleStatus,
                    record.VersionStatus,
                    record.PublishedAt,
                    record.CreatedAt,
                    fileName = version?.FileName,
                    repositoryPath = hasFile ? contentUrl : null,
                    renditionPath = hasFile && IsPdfDocument(version?.ContentType, version?.FileName, version?.RenditionPath ?? version?.RepositoryPath ?? record.RepositoryPath) ? contentUrl : null,
                    contentType = ResolveExternalContentType(version, record),
                    version = version?.VersionNumber,
                    dispatchChannel = MetadataValue(record, "dispatchchannel"),
                    dispatchReference = MetadataValue(record, "dispatchreference"),
                    dispatchedAt = MetadataValue(record, "dispatchedat")
                };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.renditionPath)
                || !string.IsNullOrWhiteSpace(item.repositoryPath))
            .ToList();

        return Ok(new { success = true, data });
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetMyEstateDocumentContent(Guid id, CancellationToken cancellationToken)
    {
        var record = await LoadAuthorizedExternalEstateDocumentAsync(id, cancellationToken);
        if (record is null)
        {
            return NotFound(new { success = false, message = "Estate document was not found." });
        }

        var version = SelectExternalDocumentVersion(record);
        var filePath = SelectExternalDocumentPath(record, version);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NotFound(new { success = false, message = "Estate document file was not found." });
        }

        try
        {
            var stream = await _fileStorageService.DownloadFileAsync(filePath, version?.FileUploadRecordId ?? record.Id);
            var fileName = SafeDownloadFileName(version?.FileName, record.DocumentReference);
            var contentType = ResolveExternalContentType(version, record);
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { success = false, message = "Estate document file was not found." });
        }
    }

    [HttpGet("/api/estate/external/request-types")]
    public IActionResult GetRequestTypes()
    {
        return Ok(new
        {
            success = true,
            data = ExternalRequestDefinitions.Select(definition => new
            {
                definition.Code,
                definition.Title,
                definition.Module,
                definition.EntityType,
                definition.Category
            })
        });
    }

    [HttpGet("/api/estate/external/requests")]
    public async Task<IActionResult> GetMyRequests(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? source,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var portalCustomerIds = await PortalCustomers(tenantId, userId.Value)
            .Select(customer => customer.Id)
            .ToListAsync(cancellationToken);
        var portalCustomerReferences = portalCustomerIds
            .Select(customerId => customerId.ToString())
            .ToList();

        var query = _db.ProcedureCases
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && ((item.OpenedById == userId.Value
                        && (item.SourceDepartment == "External Portal"
                            || item.SourceDepartment == "External Portal - Estate Services"
                            || item.SourceDepartment == "External Portal - Estate Listings"))
                    || (item.SourceDepartment == "Sales - Estate Enquiry"
                        && item.EntityType == "EstatePropertyManagementListingApplication"
                        && item.Fields.Any(field => !field.IsDeleted
                            && field.Key == "sourceReference"
                            && field.Value != null
                            && portalCustomerReferences.Contains(field.Value)))));

        if (string.Equals(source, "estateServices", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item =>
                item.SourceDepartment == "External Portal"
                || item.SourceDepartment == "External Portal - Estate Services");
        }

        var usePaging = page.HasValue || pageSize.HasValue;
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedPageSize = Math.Clamp(pageSize ?? 10, 1, 25);
        var totalCount = usePaging
            ? await query.CountAsync(cancellationToken)
            : 0;

        IQueryable<ProcedureCase> orderedQuery = query
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt);
        if (usePaging)
        {
            orderedQuery = orderedQuery
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize);
        }
        else
        {
            orderedQuery = orderedQuery.Take(100);
        }

        var cases = await orderedQuery
            .AsSplitQuery()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .ToListAsync(cancellationToken);

        var propertyReferences = cases
            .SelectMany(item => item.Fields)
            .Where(field => field.Key == "listingReference" || field.Key == "propertyUnit")
            .Select(field => field.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var managedAssets = propertyReferences.Count == 0
            ? []
            : await _db.EstateManagedAssets
                .AsNoTracking()
                .Include(asset => asset.Demarcations.Where(parcel => !parcel.IsDeleted))
                .Where(asset => asset.TenantId == tenantId
                    && !asset.IsDeleted
                    && (propertyReferences.Contains(asset.AssetCode)
                        || (asset.ProjectUnitCode != null
                            && propertyReferences.Contains(asset.ProjectUnitCode))
                        || asset.Demarcations.Any(parcel => !parcel.IsDeleted
                            && parcel.ChildFixedAssetReference != null
                            && propertyReferences.Contains(parcel.ChildFixedAssetReference))))
                .ToListAsync(cancellationToken);

        var requests = cases
            .Select(item =>
            {
                var fieldValues = item.Fields.ToDictionary(
                    field => field.Key,
                    field => field.Value,
                    StringComparer.OrdinalIgnoreCase);
                var listingReference = fieldValues.GetValueOrDefault("listingReference");
                var propertyUnit = fieldValues.GetValueOrDefault("propertyUnit");
                var asset = managedAssets.FirstOrDefault(candidate =>
                    string.Equals(candidate.AssetCode, listingReference, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.AssetCode, propertyUnit, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.ProjectUnitCode, propertyUnit, StringComparison.OrdinalIgnoreCase)
                    || candidate.Demarcations.Any(parcel => !parcel.IsDeleted
                        && (string.Equals(parcel.ChildFixedAssetReference, listingReference, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(parcel.ChildFixedAssetReference, propertyUnit, StringComparison.OrdinalIgnoreCase))));
                if (asset?.RightOfEntryDate is { } actualPossessionDate)
                {
                    fieldValues["actualPossessionDate"] = actualPossessionDate.ToString("yyyy-MM-dd");
                }

                return new
                {
                    item.Id,
                    item.Module,
                    item.EntityType,
                    item.Title,
                    item.ReferenceNumber,
                    item.ApplicantName,
                    item.SourceDepartment,
                    item.Status,
                    item.CurrentStageIndex,
                    item.CurrentStageName,
                    item.CurrentAssignedRole,
                    CustomerIntakeUploadClosed = HasFirstInternalStageBeenRoutedForward(item),
                    FieldValues = fieldValues,
                    Documents = item.Documents
                        .Where(document => string.Equals(document.ProvidedBy, "Customer", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(document => document.CreatedAt)
                        .Select(ToExternalDocumentDto)
                        .ToList(),
                    item.CreatedAt,
                    item.UpdatedAt
                };
            })
            .ToList();

        if (!usePaging)
        {
            return Ok(new { success = true, data = requests });
        }

        return Ok(new
        {
            success = true,
            data = requests,
            pagination = new
            {
                page = normalizedPage,
                pageSize = normalizedPageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)normalizedPageSize),
                hasPreviousPage = normalizedPage > 1,
                hasNextPage = normalizedPage * normalizedPageSize < totalCount
            }
        });
    }

    [HttpGet("/api/estate/external/requests/{requestId:guid}")]
    public async Task<IActionResult> GetMyRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var procedureCase = await LoadOwnedExternalEstateRequestAsync(
            tenantId,
            userId.Value,
            requestId,
            cancellationToken);

        return procedureCase is null
            ? NotFound(new { success = false, message = "Estate service request was not found." })
            : Ok(new { success = true, data = ToExternalRequestDto(procedureCase) });
    }

    [HttpPost("/api/estate/external/requests/{requestId:guid}/customer-intake-documents/{documentId:guid}/upload")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadCustomerIntakeDocument(
        Guid requestId,
        Guid documentId,
        [FromForm] IFormFile? file,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Select a document to upload." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var uploaded = await _procedureCaseService.UploadCustomerIntakeDocumentAsync(
                requestId,
                documentId,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                notes);

            if (uploaded is null)
            {
                return NotFound(new { success = false, message = "Property request document was not found." });
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var userId = GetUserId();
            if (tenantId == Guid.Empty || userId is null)
            {
                return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
            }

            var procedureCase = await LoadOwnedExternalListingCaseAsync(tenantId, userId.Value, requestId, cancellationToken);
            return procedureCase is null
                ? NotFound(new { success = false, message = "Property request was not found." })
                : Ok(new { success = true, data = ToExternalRequestDto(procedureCase) });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("/api/estate/external/my-properties")]
    public async Task<IActionResult> GetMyProperties(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var customers = await PortalCustomers(tenantId, userId.Value)
            .Select(customer => new
            {
                customer.Id,
                customer.PartnerName,
                customer.CustomerAccountNumber
            })
            .ToListAsync(cancellationToken);

        var customerIds = customers.Select(customer => customer.Id).ToList();

        var properties = await _db.EstateManagedAssets
            .AsNoTracking()
            .Where(asset => asset.TenantId == tenantId
                && !asset.IsDeleted
                && asset.CustomerBusinessPartnerId.HasValue
                && customerIds.Contains(asset.CustomerBusinessPartnerId.Value)
                && (asset.Status == EstateManagedAssetStatus.Reserved
                    || asset.Status == EstateManagedAssetStatus.Leased
                    || asset.Status == EstateManagedAssetStatus.Occupied
                    || asset.Status == EstateManagedAssetStatus.Sold))
            .OrderBy(asset => asset.Name)
            .Select(asset => new
            {
                asset.Id,
                asset.AssetCode,
                asset.ProjectUnitCode,
                asset.Name,
                asset.Status,
                TransactionType = asset.Status == EstateManagedAssetStatus.Sold
                    ? "Purchased"
                    : asset.ExternalListingType != null && asset.ExternalListingType.Contains("Lease")
                        ? "Lease"
                        : "Rental",
                asset.Location,
                asset.Town,
                asset.District,
                AgreementReference = asset.PropertyFileReference,
                AgreementDate = asset.DateOfTenancy,
                ActualPossessionDate = asset.RightOfEntryDate,
                asset.LeaseTermYears,
                MonthlyRent = asset.ExternalListingType != null && asset.ExternalListingType.Contains("Lease")
                    ? null
                    : asset.ExternalMonthlyRent ?? asset.ExternalListingPrice,
                FullTermLeaseAmount = asset.ExternalListingType != null && asset.ExternalListingType.Contains("Lease")
                    ? asset.ExternalListingPrice
                    : null,
                CurrencyCode = string.IsNullOrWhiteSpace(asset.ExternalListingCurrency)
                    ? asset.Currency
                    : asset.ExternalListingCurrency,
                asset.NextRentBillingDate,
                asset.RentGracePeriodDays,
                asset.RentPenaltyMethod,
                asset.RentPenaltyValue,
                asset.RentPenaltyCapAmount,
                asset.CustomerBusinessPartnerId
            })
            .ToListAsync(cancellationToken);

        var saleAgreementReferencesNeedingDates = properties
            .Where(asset => asset.TransactionType == "Purchased"
                && asset.AgreementDate is null
                && !string.IsNullOrWhiteSpace(asset.AgreementReference))
            .Select(asset => asset.AgreementReference!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var saleAgreementDates = saleAgreementReferencesNeedingDates.Count == 0
            ? new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase)
            : (await _db.CentralDocumentRecords
                .AsNoTracking()
                .AsSplitQuery()
                .Include(record => record.Versions.Where(version => !version.IsDeleted))
                .Where(record => record.TenantId == tenantId
                    && !record.IsDeleted
                    && record.SourceModule == "Estate"
                    && saleAgreementReferencesNeedingDates.Contains(record.DocumentReference))
                .ToListAsync(cancellationToken))
                .GroupBy(record => record.DocumentReference, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(record => record.UpdatedAt ?? record.CreatedAt)
                        .Select(ResolveAgreementDate)
                        .FirstOrDefault(),
                    StringComparer.OrdinalIgnoreCase);

        var invoices = await _db.Invoices
            .AsNoTracking()
            .ForCustomerProperties(tenantId, customerIds)
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.BusinessPartnerId,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.TotalAmount,
                invoice.PaidAmount,
                BalanceAmount = invoice.TotalAmount - invoice.PaidAmount - invoice.CreditedAmount,
                invoice.CurrencyCode,
                invoice.Status,
                invoice.Reference
            })
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(invoice => invoice.Id).ToList();
        var invoiceDescriptions = invoiceIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _db.Set<InvoiceLineItem>()
                .AsNoTracking()
                .Where(line => !line.IsDeleted && invoiceIds.Contains(line.InvoiceId))
                .GroupBy(line => line.InvoiceId)
                .Select(group => new
                {
                    InvoiceId = group.Key,
                    Description = group
                        .OrderBy(line => line.CreatedAt)
                        .Select(line => line.Description)
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(item => item.InvoiceId, item => item.Description, cancellationToken);
        var invoiceReceipts = invoiceIds.Count == 0
            ? new Dictionary<Guid, List<ExternalInvoiceReceiptDto>>()
            : (await _db.Set<PaymentAllocation>()
                .AsNoTracking()
                .Where(allocation => !allocation.IsDeleted
                    && !allocation.IsReversal
                    && invoiceIds.Contains(allocation.InvoiceId)
                    && !allocation.CustomerPayment.IsDeleted
                    && allocation.CustomerPayment.Status != "Cancelled")
                .OrderByDescending(allocation => allocation.AllocationDate)
                .Select(allocation => new
                {
                    allocation.InvoiceId,
                    Receipt = new ExternalInvoiceReceiptDto(
                        allocation.CustomerPaymentId,
                        allocation.CustomerPayment.PaymentNumber,
                        allocation.CustomerPayment.PaymentDate,
                        allocation.PaymentCurrencyAmount > 0
                            ? allocation.PaymentCurrencyAmount
                            : allocation.AllocatedAmount,
                        allocation.PaymentCurrencyCode,
                        allocation.CustomerPayment.PaymentMethod,
                        allocation.CustomerPayment.TransactionReference,
                        allocation.CustomerPayment.Status)
                })
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.InvoiceId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Receipt).ToList());

        var ownedListingCaseIds = await _db.ProcedureCases
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.OpenedById == userId.Value
                && item.SourceDepartment == "External Portal - Estate Listings"
                && item.EntityType == "EstatePropertyManagementListingApplication")
            .Select(item => item.Id.ToString())
            .ToListAsync(cancellationToken);

        List<ProcedureCase> legalTransfers = [];
        if (ownedListingCaseIds.Count > 0)
        {
            legalTransfers = await _db.ProcedureCases
                .AsNoTracking()
                .AsSplitQuery()
                .Include(item => item.Fields.Where(field => !field.IsDeleted))
                .Include(item => item.Documents.Where(document => !document.IsDeleted))
                .Where(item => item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.Module == "Legal"
                    && item.EntityType == "LegalTransfer"
                    && item.Fields.Any(field =>
                        !field.IsDeleted
                        && field.Key == "sourceProcedureCaseId"
                        && field.Value != null
                        && ownedListingCaseIds.Contains(field.Value)))
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                Customers = customers,
                Properties = properties.Select(asset => new
                {
                    asset.Id,
                    asset.AssetCode,
                    asset.ProjectUnitCode,
                    asset.Name,
                    asset.Status,
                    asset.TransactionType,
                    asset.Location,
                    asset.Town,
                    asset.District,
                    asset.AgreementReference,
                    AgreementDate = asset.AgreementDate
                        ?? (!string.IsNullOrWhiteSpace(asset.AgreementReference)
                            && saleAgreementDates.TryGetValue(asset.AgreementReference, out var agreementDate)
                                ? agreementDate
                                : null),
                    asset.ActualPossessionDate,
                    asset.LeaseTermYears,
                    asset.MonthlyRent,
                    asset.FullTermLeaseAmount,
                    asset.CurrencyCode,
                    asset.NextRentBillingDate,
                    asset.RentGracePeriodDays,
                    asset.RentPenaltyMethod,
                    asset.RentPenaltyValue,
                    asset.RentPenaltyCapAmount,
                    asset.CustomerBusinessPartnerId
                }),
                Invoices = invoices.Select(invoice =>
                {
                    invoiceDescriptions.TryGetValue(invoice.Id, out var description);
                    invoiceReceipts.TryGetValue(invoice.Id, out var receipts);
                    return new
                    {
                        invoice.Id,
                        invoice.InvoiceNumber,
                        invoice.BusinessPartnerId,
                        invoice.InvoiceDate,
                        invoice.DueDate,
                        invoice.TotalAmount,
                        invoice.PaidAmount,
                        invoice.BalanceAmount,
                        invoice.CurrencyCode,
                        invoice.Status,
                        invoice.Reference,
                        Description = description,
                        Receipts = receipts ?? new List<ExternalInvoiceReceiptDto>()
                    };
                }),
                LegalTransfers = legalTransfers.Select(ToExternalLegalTransferDto).ToList()
            }
        });
    }

    [HttpGet("/api/estate/external/legal-transfers/{legalCaseId:guid}/draft")]
    public async Task<IActionResult> DownloadLegalTransferDraft(
        Guid legalCaseId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var legalCase = await LoadOwnedExternalLegalTransferCaseAsync(
            tenantId,
            userId.Value,
            legalCaseId,
            asTracking: false,
            cancellationToken);
        if (legalCase is null)
        {
            return NotFound(new { success = false, message = "Legal transfer was not found." });
        }

        var fields = legalCase.Fields.ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);
        var draftReference = FieldValue(fields, "draftDocumentReference");
        if (string.IsNullOrWhiteSpace(draftReference))
        {
            return NotFound(new { success = false, message = "The transfer draft has not been generated yet." });
        }

        var record = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(item => item.Versions.Where(version => !version.IsDeleted))
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && (item.DocumentReference == draftReference || item.SourceRecordId == legalCase.Id))
            .OrderByDescending(item => item.DocumentReference == draftReference)
            .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            return NotFound(new { success = false, message = "The generated transfer draft was not found." });
        }

        try
        {
            return await DownloadCentralDocumentRecordAsPdfAsync(record, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { success = false, message = "The generated transfer draft file was not found in storage." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("/api/estate/external/legal-transfers/{legalCaseId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> DownloadLegalTransferDocument(
        Guid legalCaseId,
        Guid documentId,
        [FromQuery] bool download = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var legalCase = await LoadOwnedExternalLegalTransferCaseAsync(
            tenantId,
            userId.Value,
            legalCaseId,
            asTracking: false,
            cancellationToken);
        if (legalCase is null)
        {
            return NotFound(new { success = false, message = "Legal transfer was not found." });
        }

        var fields = legalCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(field => field.Key, field => field.Value, StringComparer.OrdinalIgnoreCase);
        var document = legalCase.Documents.FirstOrDefault(item =>
            item.Id == documentId
            && !item.IsDeleted
            && IsCustomerReleasedLegalTransferDocument(legalCase, fields, item));

        if (document is null || string.IsNullOrWhiteSpace(document.FileUrl))
        {
            return NotFound(new { success = false, message = "Signed transfer document was not found." });
        }

        try
        {
            var stream = await _fileStorageService.DownloadFileAsync(document.FileUrl, document.Id);
            var fileName = SafeDownloadFileName(document.FileName, document.Name);
            var contentType = IsPdfDocument(null, document.FileName, document.FileUrl)
                ? "application/pdf"
                : "application/octet-stream";
            if (!download)
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            }

            return File(stream, contentType, download ? fileName : null, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { success = false, message = "Signed transfer document file was not found." });
        }
    }

    [HttpPost("/api/estate/external/legal-transfers/{legalCaseId:guid}/executed-transfer-form")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UploadExecutedTransferForm(
        Guid legalCaseId,
        [FromForm] IFormFile? file,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Select the signed transfer form to upload." });
        }

        var legalCase = await LoadOwnedExternalLegalTransferCaseAsync(
            tenantId,
            userId.Value,
            legalCaseId,
            asTracking: true,
            cancellationToken);
        if (legalCase is null)
        {
            return NotFound(new { success = false, message = "Legal transfer was not found." });
        }

        if (!string.Equals(legalCase.CurrentStageName, "Client Execution", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "The transfer form can be uploaded only when Legal requests client execution." });
        }

        await using var stream = file.OpenReadStream();
        var upload = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            Category = "procedure-case-documents",
            TenantId = tenantId.ToString(),
            OverwriteExisting = false
        });
        if (!upload.Success)
        {
            return BadRequest(new { success = false, message = upload.ErrorMessage ?? "Signed transfer form upload failed." });
        }

        var now = DateTime.UtcNow;
        var document = legalCase.Documents.FirstOrDefault(item =>
            !item.IsDeleted
            && string.Equals(item.Name, "Executed transfer form", StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            document = new ProcedureCaseDocument
            {
                TenantId = tenantId,
                ProcedureCaseId = legalCase.Id,
                Name = "Executed transfer form",
                RequiredFrom = "Client Execution",
                ProvidedBy = "Client / Legal",
                IsMandatory = true,
                CreatedAt = now,
                CreatedById = userId.Value
            };
            _db.ProcedureCaseDocuments.Add(document);
        }

        document.RequiredFrom = "Client Execution";
        document.ProvidedBy = "Client / Legal";
        document.FileName = upload.OriginalFileName;
        document.FileUrl = upload.FilePath;
        document.Notes = string.IsNullOrWhiteSpace(notes)
            ? "Uploaded by client from External Portal."
            : notes.Trim();
        document.UploadedById = userId.Value;
        document.UploadedAt = now;
        document.UpdatedAt = now;
        document.LastModifiedById = userId.Value;

        var fields = legalCase.Fields.ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);
        UpsertField(legalCase, fields, "signatureStatus", "Signature status", "select", "Client signed");
        UpsertField(legalCase, fields, "transferDeclarationReference", "Transfer declaration reference", "text", upload.OriginalFileName);
        legalCase.LastActionById = userId.Value;
        legalCase.LastModifiedById = userId.Value;
        legalCase.UpdatedAt = now;
        AddExternalCaseActivity(legalCase, userId.Value, "Client executed transfer form uploaded", upload.OriginalFileName);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(upload.FilePath))
            {
                await _fileStorageService.DeleteFileAsync(upload.FilePath);
            }
            throw;
        }

        await NotifyLegalTransferClientExecutionReceivedAsync(legalCase, cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Signed transfer form submitted to Legal.",
            data = ToExternalLegalTransferDto(legalCase)
        });
    }

    [HttpGet("/api/estate/external/invoices/{invoiceId:guid}/pdf")]
    public async Task<IActionResult> GetPropertyInvoicePdf(
        Guid invoiceId,
        [FromQuery] bool download = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var customerIds = await PortalCustomers(tenantId, userId.Value)
            .Select(customer => customer.Id).ToListAsync(cancellationToken);
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(item => item.Tenant)
            .Include(item => item.LineItems.Where(line => !line.IsDeleted))
            .ForCustomerProperties(tenantId, customerIds)
            .FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return NotFound(new { success = false, message = "Property invoice was not found." });
        }

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var currency = string.IsNullOrWhiteSpace(invoice.CurrencyCode) ? "GHS" : invoice.CurrencyCode;
        string Money(decimal amount) => $"{currency} {amount:N2}";
        var balance = invoice.TotalAmount - invoice.PaidAmount - invoice.CreditedAmount;
        var pdf = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor(QColors.Grey.Darken3));
                page.Header().Column(header =>
                {
                    header.Item().Text(invoice.Tenant?.Name ?? "Invoice").FontSize(18).SemiBold().FontColor(QColors.Blue.Darken3);
                    header.Item().PaddingTop(4).Text("CUSTOMER INVOICE").FontSize(11).SemiBold().FontColor(QColors.Grey.Darken1);
                });
                page.Content().PaddingVertical(24).Column(column =>
                {
                    column.Spacing(16);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Bill to").SemiBold();
                            left.Item().Text(invoice.CustomerName);
                            if (!string.IsNullOrWhiteSpace(invoice.CustomerAddress)) left.Item().Text(invoice.CustomerAddress);
                        });
                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().Text(invoice.InvoiceNumber).FontSize(14).SemiBold();
                            right.Item().Text($"Invoice date: {invoice.InvoiceDate:dd MMM yyyy}");
                            right.Item().Text($"Due date: {(invoice.DueDate.HasValue ? invoice.DueDate.Value.ToString("dd MMM yyyy") : "Not recorded")}");
                            right.Item().Text($"Status: {invoice.Status}");
                        });
                    });
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Background(QColors.Grey.Lighten3).Padding(7).Text("Description").SemiBold();
                            header.Cell().Background(QColors.Grey.Lighten3).Padding(7).AlignRight().Text("Qty").SemiBold();
                            header.Cell().Background(QColors.Grey.Lighten3).Padding(7).AlignRight().Text("Price").SemiBold();
                            header.Cell().Background(QColors.Grey.Lighten3).Padding(7).AlignRight().Text("Amount").SemiBold();
                        });
                        foreach (var line in invoice.LineItems.OrderBy(item => item.CreatedAt))
                        {
                            table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).Padding(7).Text(line.Description);
                            table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).Padding(7).AlignRight().Text(line.Quantity.ToString("N2"));
                            table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).Padding(7).AlignRight().Text(Money(line.UnitPrice));
                            table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).Padding(7).AlignRight().Text(Money(line.LineTotal));
                        }
                    });
                    column.Item().AlignRight().Width(240).Column(totals =>
                    {
                        totals.Item().Row(row => { row.RelativeItem().Text("Total"); row.ConstantItem(120).AlignRight().Text(Money(invoice.TotalAmount)).SemiBold(); });
                        totals.Item().Row(row => { row.RelativeItem().Text("Amount paid"); row.ConstantItem(120).AlignRight().Text(Money(invoice.PaidAmount)); });
                        totals.Item().PaddingTop(6).BorderTop(1).Row(row => { row.RelativeItem().Text("Balance due").SemiBold(); row.ConstantItem(120).AlignRight().Text(Money(balance)).SemiBold(); });
                    });
                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        column.Item().PaddingTop(8).Column(notes => { notes.Item().Text("Notes / Terms").SemiBold(); notes.Item().Text(invoice.Notes); });
                    }
                });
                page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
            });
        }).GeneratePdf();

        return new FileContentResult(pdf, "application/pdf")
        {
            FileDownloadName = download ? $"{invoice.InvoiceNumber}.pdf" : null,
            EnableRangeProcessing = true
        };
    }

    [HttpPost("/api/estate/external/requests/{requestId:guid}/customer-decision")]
    public async Task<IActionResult> SubmitCustomerDecision(
        Guid requestId,
        [FromBody] ExternalPropertyRequestDecision request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var procedureCase = await LoadOwnedExternalListingCaseAsync(tenantId, userId.Value, requestId, cancellationToken);
        if (procedureCase is null)
        {
            return NotFound(new { success = false, message = "Property request was not found." });
        }

        var fields = procedureCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);
        var decisionStatus = FieldValue(fields, "decisionStatus");
        if (!IsApprovedDecisionStatus(decisionStatus))
        {
            return BadRequest(new { success = false, message = "Customer acceptance is available only after Property Management approves the request." });
        }

        var accepted = string.Equals(request.Decision, "Accept", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Decision, "Accepted", StringComparison.OrdinalIgnoreCase);
        var rejected = string.Equals(request.Decision, "Reject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Decision, "Rejected", StringComparison.OrdinalIgnoreCase);
        if (!accepted && !rejected)
        {
            return BadRequest(new { success = false, message = "Choose Accept or Reject." });
        }

        var isRentalRequest = IsRentalPropertyRequest(fields, procedureCase.Title);
        if (accepted && string.IsNullOrWhiteSpace(FieldValue(fields, "generatedAgreementReference")))
        {
            return BadRequest(new { success = false, message = "The approved agreement must be generated before you can accept this request." });
        }
        if (accepted && !IsLegalAgreementReleaseApproved(fields))
        {
            return BadRequest(new { success = false, message = "Legal must approve the draft agreement before it can be accepted." });
        }
        if (accepted && isRentalRequest && !IsValidIsoDate(FieldValue(fields, "moveInDate")))
        {
            return BadRequest(new { success = false, message = "Property Management must set the approved move-in date before you can accept this rental." });
        }

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        UpsertField(procedureCase, fields, "customerAcceptanceStatus", "Customer acceptance status", "select", accepted ? "Accepted" : "Rejected");
        UpsertField(procedureCase, fields, "customerNotificationStatus", "Customer notification status", "select", accepted ? "Accepted by customer" : "Rejected by customer");
        UpsertField(procedureCase, fields, "customerAcceptanceDate", "Customer acceptance date", "date", today);
        UpsertField(procedureCase, fields, "applicationStatus", "Request status", "select", accepted ? "Customer accepted" : "Customer rejected");
        UpsertField(
            procedureCase,
            fields,
            "billingStartStatus",
            "Billing start status",
            "select",
            accepted && isRentalRequest ? "Blocked - signature pending" : "Not applicable");
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            UpsertField(procedureCase, fields, "notes", "Property request notes", "textarea", request.Notes.Trim());
        }

        procedureCase.LastActionById = userId.Value;
        procedureCase.UpdatedAt = DateTime.UtcNow;
        procedureCase.LastModifiedById = userId.Value;
        AddExternalCaseActivity(
            procedureCase,
            userId.Value,
            accepted ? "Customer accepted" : "Customer rejected",
            request.Notes);
        if (accepted)
        {
            await MarkCompetingExternalListingRequestsUnavailableAsync(
                procedureCase,
                fields,
                tenantId,
                userId.Value,
                cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
        await NotifyListingCustomerActionAsync(
            procedureCase,
            accepted ? "Customer accepted property request" : "Customer rejected property request",
            $"{procedureCase.ReferenceNumber ?? procedureCase.Title} was {(accepted ? "accepted" : "rejected")} by the customer.",
            "estate.property.customer-decision",
            cancellationToken);

        return Ok(new
        {
            success = true,
            message = accepted
                ? "Your acceptance has been recorded. Upload the signed agreement when it is ready."
                : "Your rejection has been recorded.",
            data = ToExternalRequestDto(procedureCase)
        });
    }

    [HttpPost("/api/estate/external/requests/{requestId:guid}/withdraw")]
    public async Task<IActionResult> WithdrawPropertyRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var procedureCase = await LoadOwnedExternalListingCaseAsync(
            tenantId,
            userId.Value,
            requestId,
            cancellationToken);
        if (procedureCase is null)
        {
            return NotFound(new { success = false, message = "Property request was not found." });
        }

        var terminalStatuses = new[] { "Completed", "Archived", "Cancelled", "Canceled", "Rejected", "Closed" };
        if (terminalStatuses.Contains(procedureCase.Status, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "This property request is already closed." });
        }

        var fields = procedureCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);
        if (string.Equals(FieldValue(fields, "customerAcceptanceStatus"), "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "An accepted property request cannot be withdrawn. Contact Property Management." });
        }

        procedureCase.Status = "Cancelled";
        procedureCase.CurrentAssignedRole = null;
        procedureCase.CurrentStageOwner = null;
        procedureCase.LastActionById = userId.Value;
        procedureCase.LastModifiedById = userId.Value;
        procedureCase.UpdatedAt = DateTime.UtcNow;
        UpsertField(procedureCase, fields, "applicationStatus", "Request status", "select", "Withdrawn");
        AddExternalCaseActivity(procedureCase, userId.Value, "Customer withdrew request", "Withdrawn from the customer portal.");
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = "The property request was withdrawn.",
            data = ToExternalRequestDto(procedureCase)
        });
    }

    [HttpPost("/api/estate/external/requests/{requestId:guid}/clarification-response")]
    public async Task<IActionResult> SubmitClarificationResponse(
        Guid requestId,
        [FromBody] ExternalPropertyClarificationResponse request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var response = request.Response?.Trim();
        if (string.IsNullOrWhiteSpace(response))
        {
            return BadRequest(new { success = false, message = "Enter your clarification response." });
        }

        var procedureCase = await LoadOwnedExternalListingCaseAsync(
            tenantId,
            userId.Value,
            requestId,
            cancellationToken);
        if (procedureCase is null)
        {
            return NotFound(new { success = false, message = "Property request was not found." });
        }
        if (!string.Equals(procedureCase.Status, "Clarification required", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "This request is not awaiting clarification." });
        }

        var now = DateTime.UtcNow;
        var fields = procedureCase.Fields.ToDictionary(
            field => field.Key,
            field => field,
            StringComparer.OrdinalIgnoreCase);
        UpsertField(procedureCase, fields, "clarificationResponse", "Customer clarification response", "textarea", response);
        UpsertField(procedureCase, fields, "applicationStatus", "Request status", "select", "Clarification submitted");
        UpsertField(procedureCase, fields, "customerNotificationStatus", "Customer notification status", "select", "Clarification submitted");
        procedureCase.Status = "Open";
        procedureCase.LastActionById = userId.Value;
        procedureCase.LastModifiedById = userId.Value;
        procedureCase.UpdatedAt = now;
        AddExternalCaseActivity(procedureCase, userId.Value, "Submitted clarification", response);

        if (procedureCase.WorkflowInstanceId.HasValue)
        {
            var workflow = await _db.WorkflowInstances
                .FirstOrDefaultAsync(item => item.Id == procedureCase.WorkflowInstanceId.Value, cancellationToken);
            if (workflow is not null)
            {
                workflow.Status = WorkflowInstanceStatus.InProgress;
                workflow.UpdatedAt = now;
                workflow.LastModifiedById = userId.Value;
            }

            var activeStep = await _db.WorkflowStepInstances
                .Where(item => item.WorkflowInstanceId == procedureCase.WorkflowInstanceId.Value
                    && item.WorkflowStepId == procedureCase.WorkflowStepId
                    && (item.Status == WorkflowStepInstanceStatus.Pending
                        || item.Status == WorkflowStepInstanceStatus.InProgress))
                .OrderByDescending(item => item.CreatedDate)
                .FirstOrDefaultAsync(cancellationToken);
            if (activeStep is not null)
            {
                var approvals = await _db.WorkflowApprovals
                    .Where(item => item.StepInstanceId == activeStep.Id
                        && item.Status == WorkflowApprovalStatus.MoreInfoRequested)
                    .ToListAsync(cancellationToken);
                foreach (var approval in approvals)
                {
                    approval.Status = WorkflowApprovalStatus.Pending;
                    approval.ProcessedById = null;
                    approval.ProcessedDate = null;
                    approval.Comments = null;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await NotifyListingCustomerActionAsync(
            procedureCase,
            "Customer clarification submitted",
            $"{procedureCase.ReferenceNumber ?? procedureCase.Title} has received the customer's clarification response.",
            "estate.property.application-clarification-response",
            cancellationToken);

        return Ok(new { success = true, data = ToExternalRequestDto(procedureCase) });
    }

    [HttpGet("/api/estate/external/requests/{requestId:guid}/agreement")]
    public async Task<IActionResult> DownloadGeneratedAgreement(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        var procedureCase = await LoadOwnedExternalListingCaseAsync(tenantId, userId.Value, requestId, cancellationToken);
        if (procedureCase is null)
        {
            return NotFound(new { success = false, message = "Property request was not found." });
        }

        var fields = procedureCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);
        if (!IsApprovedDecisionStatus(FieldValue(fields, "decisionStatus")))
        {
            return BadRequest(new { success = false, message = "The agreement is available only after Property Management approves the request." });
        }
        if (!IsLegalAgreementReleaseApproved(fields))
        {
            return BadRequest(new { success = false, message = "The agreement is still under Legal review." });
        }

        var signedLegalAgreement = await _db.ProcedureCases
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.EntityType == "LegalPropertyAgreementReview"
                && item.Fields.Any(field => !field.IsDeleted
                    && field.Key == "sourceProcedureCaseId"
                    && field.Value == requestId.ToString()))
            .SelectMany(item => item.Documents.Where(document => !document.IsDeleted
                && document.Name == "Head of Legal signed agreement"
                && document.FileUrl != null))
            .OrderByDescending(document => document.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (signedLegalAgreement is null || string.IsNullOrWhiteSpace(signedLegalAgreement.FileUrl))
        {
            var generatedReference = FieldValue(fields, "generatedAgreementReference");
            if (string.IsNullOrWhiteSpace(generatedReference))
            {
                return Conflict(new { success = false, message = "The generated agreement is not yet available for the customer." });
            }

            var generatedRecord = await _db.CentralDocumentRecords
                .AsNoTracking()
                .Include(item => item.Versions.Where(version => !version.IsDeleted))
                .Where(item => item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.DocumentReference == generatedReference)
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var generatedVersion = generatedRecord is null
                ? null
                : ResolveCurrentDocumentVersion(generatedRecord);
            var generatedPath = generatedVersion is null
                ? null
                : FirstNonBlank(generatedVersion.RenditionPath, generatedVersion.RepositoryPath);
            if (generatedRecord is null || generatedVersion is null || string.IsNullOrWhiteSpace(generatedPath))
            {
                return Conflict(new { success = false, message = "The generated agreement file is not yet available for the customer." });
            }

            var generatedStream = await _fileStorageService.DownloadFileAsync(
                generatedPath,
                !string.IsNullOrWhiteSpace(generatedVersion.RenditionPath)
                    ? generatedRecord.Id
                    : generatedVersion.FileUploadRecordId ?? generatedRecord.Id);
            return File(generatedStream, "application/pdf", SafeDownloadFileName(generatedVersion.FileName, "property-agreement.pdf"));
        }
        var signedStream = await _fileStorageService.DownloadFileAsync(
            signedLegalAgreement.FileUrl,
            signedLegalAgreement.Id);
        return File(signedStream, "application/pdf", SafeDownloadFileName(signedLegalAgreement.FileName, "signed-agreement.pdf"));
    }

    [HttpPost("/api/estate/external/requests/{requestId:guid}/signed-agreement")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UploadSignedAgreement(
        Guid requestId,
        [FromForm] IFormFile? file,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Select the signed agreement document to upload." });
        }

        var procedureCase = await LoadOwnedExternalListingCaseAsync(tenantId, userId.Value, requestId, cancellationToken);
        if (procedureCase is null)
        {
            return NotFound(new { success = false, message = "Property request was not found." });
        }

        var fields = procedureCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(FieldValue(fields, "customerAcceptanceStatus"), "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Accept the approved request before uploading a signed agreement." });
        }

        var isRentalRequest = IsRentalPropertyRequest(fields, procedureCase.Title);
        var approvedMoveInDate = FieldValue(fields, "moveInDate");
        if (isRentalRequest && !IsValidIsoDate(approvedMoveInDate))
        {
            return BadRequest(new { success = false, message = "Property Management must set the approved move-in date before the signed rental agreement can be uploaded." });
        }

        await using var stream = file.OpenReadStream();
        var upload = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            Category = "procedure-case-documents",
            TenantId = tenantId.ToString(),
            OverwriteExisting = false
        });
        if (!upload.Success)
        {
            return BadRequest(new { success = false, message = upload.ErrorMessage ?? "Signed agreement upload failed." });
        }

        var now = DateTime.UtcNow;
        var document = new ProcedureCaseDocument
        {
            TenantId = tenantId,
            ProcedureCaseId = procedureCase.Id,
            Name = "Signed property agreement",
            RequiredFrom = "Agreement signing and close",
            IsMandatory = true,
            FileName = upload.OriginalFileName,
            FileUrl = upload.FilePath,
            Notes = string.IsNullOrWhiteSpace(notes) ? "Uploaded by customer from External Portal." : notes.Trim(),
            UploadedById = userId.Value,
            UploadedAt = now,
            CreatedAt = now,
            CreatedById = userId.Value
        };
        _db.ProcedureCaseDocuments.Add(document);

        var agreementReference = FirstNonBlank(
            upload.OriginalFileName,
            upload.FileName,
            upload.FilePath) ?? $"SIGNED-{procedureCase.ReferenceNumber ?? procedureCase.Id.ToString()}";
        UpsertField(procedureCase, fields, "signedAgreementReference", "Signed agreement upload reference", "text", agreementReference);
        UpsertField(
            procedureCase,
            fields,
            "billingStartStatus",
            "Billing start status",
            "select",
            isRentalRequest ? "Blocked - internal approval and signature pending" : "Not applicable");
        UpsertField(procedureCase, fields, "moveInEffectiveStatus", "Move-in effective status", "select", isRentalRequest
            ? "Blocked - final agreement pending"
            : "Not applicable");
        UpsertField(procedureCase, fields, "agreementExecutionStatus", "Agreement execution status", "select", "Customer signed - internal approval pending");
        UpsertField(procedureCase, fields, "internalApprovalStatus", "Internal agreement approval status", "select", "Not submitted");
        UpsertField(procedureCase, fields, "internalSignatureStatus", "Internal digital signature status", "select", "Blocked - approval pending");
        UpsertField(procedureCase, fields, "applicationStatus", "Request status", "select", "Signed agreement submitted");
        AddExternalCaseActivity(procedureCase, userId.Value, "Signed agreement uploaded", upload.OriginalFileName);
        procedureCase.LastActionById = userId.Value;
        procedureCase.UpdatedAt = now;
        procedureCase.LastModifiedById = userId.Value;

        var linkedLegalCase = await _db.ProcedureCases
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.EntityType == "LegalPropertyAgreementReview"
                && item.Fields.Any(field => !field.IsDeleted
                    && field.Key == "sourceProcedureCaseId"
                    && field.Value == procedureCase.Id.ToString()),
                cancellationToken);
        if (linkedLegalCase is not null)
        {
            var legalDocument = linkedLegalCase.Documents.FirstOrDefault(document =>
                string.Equals(document.Name, "Customer signed agreement", StringComparison.OrdinalIgnoreCase));
            if (legalDocument is null)
            {
                legalDocument = new ProcedureCaseDocument
                {
                    TenantId = tenantId,
                    ProcedureCaseId = linkedLegalCase.Id,
                    Name = "Customer signed agreement",
                    RequiredFrom = "Head of Legal Signature",
                    ProvidedBy = "Customer",
                    IsMandatory = true,
                    CreatedAt = now,
                    CreatedById = userId.Value
                };
                linkedLegalCase.Documents.Add(legalDocument);
            }

            legalDocument.FileName = upload.OriginalFileName;
            legalDocument.FileUrl = upload.FilePath;
            legalDocument.Notes = string.IsNullOrWhiteSpace(notes)
                ? $"Uploaded by customer for {procedureCase.ReferenceNumber ?? procedureCase.Title}."
                : notes.Trim();
            legalDocument.UploadedById = userId.Value;
            legalDocument.UploadedAt = now;
            legalDocument.UpdatedAt = now;
            legalDocument.LastModifiedById = userId.Value;
            linkedLegalCase.LastActionById = userId.Value;
            linkedLegalCase.UpdatedAt = now;
            linkedLegalCase.LastModifiedById = userId.Value;
        }
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(upload.FilePath))
            {
                await _fileStorageService.DeleteFileAsync(upload.FilePath);
            }
            throw;
        }
        await NotifyListingCustomerActionAsync(
            procedureCase,
            "Customer signed agreement received",
            $"The customer uploaded a signed agreement for {procedureCase.ReferenceNumber ?? procedureCase.Title}. Submit it for internal approval.",
            "estate.property.signed-agreement-uploaded",
            cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Signed agreement submitted. Internal signature and approval can now continue if a workflow is assigned.",
            data = ToExternalRequestDto(procedureCase)
        });
    }

    [HttpPost("/api/estate/external/requests")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateExternalEstateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var definition = ExternalRequestDefinitions.FirstOrDefault(item =>
            string.Equals(item.Code, request.RequestType, StringComparison.OrdinalIgnoreCase));

        if (definition is null)
        {
            return BadRequest(new { success = false, message = "Unsupported Estate service request type." });
        }

        EstateManagedAsset? selectedProperty = null;
        if (!string.IsNullOrWhiteSpace(request.PropertyReference))
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var userId = GetUserId();
            if (tenantId == Guid.Empty || userId is null)
            {
                return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
            }

            var customerIds = await PortalCustomers(tenantId, userId.Value)
                .Select(customer => customer.Id)
                .ToListAsync(cancellationToken);
            var propertyReference = request.PropertyReference.Trim();
            selectedProperty = await _db.EstateManagedAssets.AsNoTracking().FirstOrDefaultAsync(asset =>
                asset.TenantId == tenantId
                && !asset.IsDeleted
                && asset.CustomerBusinessPartnerId.HasValue
                && customerIds.Contains(asset.CustomerBusinessPartnerId.Value)
                && (asset.Status == EstateManagedAssetStatus.Reserved
                    || asset.Status == EstateManagedAssetStatus.Leased
                    || asset.Status == EstateManagedAssetStatus.Occupied
                    || asset.Status == EstateManagedAssetStatus.Sold)
                && (asset.ProjectUnitCode == propertyReference || asset.AssetCode == propertyReference),
                cancellationToken);
            if (selectedProperty is null)
            {
                return BadRequest(new { success = false, message = "Select a property linked to your account." });
            }
        }

        var reference = BuildExternalReference("PORTAL");
        var applicantName = string.IsNullOrWhiteSpace(request.ApplicantName)
            ? _currentUserService.UserName
            : request.ApplicantName.Trim();
        var contact = string.IsNullOrWhiteSpace(request.Contact)
            ? _currentUserService.Email ?? _currentUserService.UserName
            : request.Contact.Trim();
        var fieldValues = BuildFieldValues(definition, request, reference, contact);
        if (definition.EntityType == "EstateFacilityMaintenance" && selectedProperty is not null)
            fieldValues["estateManagedAssetId"] = selectedProperty.Id.ToString();

        try
        {
            var created = await _procedureCaseService.CreateCaseAsync(new CreateProcedureCaseRequest(
                definition.Module,
                definition.EntityType,
                $"{definition.Title} - {applicantName}",
                reference,
                applicantName,
                "External Portal - Estate Services",
                DateTime.UtcNow,
                request.Description,
                fieldValues));

            await NotifyExternalServiceRequestAsync(definition, created.Id, created.ReferenceNumber ?? reference, applicantName, cancellationToken);

            return Ok(new
            {
                success = true,
                data = new
                {
                    created.Id,
                    created.Module,
                    created.EntityType,
                    created.Title,
                    created.ReferenceNumber,
                    created.Status,
                    created.CurrentStageName,
                    created.CurrentAssignedRole,
                    created.Documents,
                    CreatedAt = DateTime.UtcNow
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("/api/estate/external/listings")]
    public async Task<IActionResult> GetListings(
        [FromQuery] string? location = null,
        [FromQuery] string? listingType = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? businessPartnerId = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default, [FromQuery] Guid? listingId = null)
    {
        if (minPrice < 0 || maxPrice < 0 || (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice))
        {
            return BadRequest(new { success = false, message = "Enter a valid price range." });
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var normalizedLocation = Normalize(location);
        var normalizedSearch = Normalize(search);
        var normalizedListingType = NormalizeListingType(listingType);
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedPageSize = Math.Clamp(pageSize ?? take ?? 10, 1, 10);
        var activeAllocatedListingIds = await GetActiveSalesAllocationListingIdsAsync(tenantId, cancellationToken);

        var query = WhereExternallyAvailableListings(_db.EstateManagedAssets
            .AsNoTracking())
            .Where(asset => asset.TenantId == tenantId
                && (asset.AssetType == EstateManagedAssetType.Land
                    || asset.AssetType == EstateManagedAssetType.Property
                    || asset.AssetType == EstateManagedAssetType.Facility));
        if (activeAllocatedListingIds.Length > 0)
            query = query.Where(asset => !activeAllocatedListingIds.Contains(asset.Id));

        var portalUserId = GetUserId();
        if (portalUserId.HasValue && businessPartnerId.HasValue)
        {
            var selectedCustomer = await FindPortalCustomerAsync(
                tenantId,
                portalUserId.Value,
                businessPartnerId.Value,
                cancellationToken);
            if (selectedCustomer is null)
            {
                return BadRequest(new { success = false, message = "Select a customer account linked to your portal login." });
            }

            var selectedCustomerId = selectedCustomer.Id.ToString();
            query = query.Where(asset => !_db.ProcedureCases.Any(procedureCase =>
                procedureCase.TenantId == tenantId
                && !procedureCase.IsDeleted
                && procedureCase.OpenedById == portalUserId.Value
                && procedureCase.SourceDepartment == "External Portal - Estate Listings"
                && procedureCase.Status != "Completed"
                && procedureCase.Status != "Archived"
                && procedureCase.Status != "Cancelled"
                && procedureCase.Status != "Canceled"
                && procedureCase.Status != "Rejected"
                && procedureCase.Status != "Closed"
                && !procedureCase.Fields.Any(field => !field.IsDeleted
                    && field.Key == "applicationStatus"
                    && (field.Value == "Rejected"
                        || field.Value == "Withdrawn"
                        || field.Value == "Cancelled"
                        || field.Value == "Canceled"
                        || field.Value == "Archived"
                        || field.Value == "Sale completed"
                        || field.Value == "Agreement fully executed"))
                && procedureCase.Fields.Any(field => !field.IsDeleted
                    && field.Key == "sourceReference"
                    && field.Value == selectedCustomerId)
                && procedureCase.Fields.Any(field => !field.IsDeleted
                    && field.Key == "propertyUnit"
                    && (field.Value == asset.AssetCode
                        || (asset.ProjectUnitCode != null && field.Value == asset.ProjectUnitCode)))));
        }

        if (normalizedListingType != null)
        {
            query = query.Where(asset => asset.ExternalListingType == normalizedListingType
                || (normalizedListingType == "Sale" && (asset.ExternalListingType == "SaleAndRent" || asset.ExternalListingType == "SaleAndLease"))
                || (normalizedListingType == "Rent" && asset.ExternalListingType == "SaleAndRent")
                || (normalizedListingType == "Lease" && asset.ExternalListingType == "SaleAndLease"));
        }

        if (normalizedLocation != null)
        {
            query = query.Where(asset =>
                (asset.Location != null && asset.Location.ToLower().Contains(normalizedLocation))
                || (asset.Town != null && asset.Town.ToLower().Contains(normalizedLocation))
                || (asset.District != null && asset.District.ToLower().Contains(normalizedLocation))
                || (asset.Region != null && asset.Region.ToLower().Contains(normalizedLocation)));
        }

        if (normalizedSearch != null)
        {
            query = query.Where(asset =>
                (asset.AssetCode != null && asset.AssetCode.ToLower().Contains(normalizedSearch))
                || (asset.Name != null && asset.Name.ToLower().Contains(normalizedSearch))
                || (asset.Description != null && asset.Description.ToLower().Contains(normalizedSearch))
                || (asset.Location != null && asset.Location.ToLower().Contains(normalizedSearch))
                || (asset.Town != null && asset.Town.ToLower().Contains(normalizedSearch))
                || (asset.District != null && asset.District.ToLower().Contains(normalizedSearch))
                || (asset.ProjectTitle != null && asset.ProjectTitle.ToLower().Contains(normalizedSearch))
                || (asset.UnitType != null && asset.UnitType.ToLower().Contains(normalizedSearch)));
        }

        query = ApplyPublicPriceFilter(query, normalizedListingType, minPrice, maxPrice);

        var demarcationQuery = _db.EstateLandDemarcations
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Published"
                && item.BoundaryVerified
                && item.EstateManagedAsset.TenantId == tenantId
                && !item.EstateManagedAsset.IsDeleted
                && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                && !item.EstateManagedAsset.ProjectId.HasValue
                && !item.EstateManagedAsset.IsPublishedToExternalPortal);
        if (activeAllocatedListingIds.Length > 0)
            demarcationQuery = demarcationQuery.Where(item => !activeAllocatedListingIds.Contains(item.Id));

        if (normalizedListingType != null)
        {
            demarcationQuery = demarcationQuery.Where(item => item.ExternalListingType == normalizedListingType
                || (normalizedListingType == "Sale" && (item.ExternalListingType == "SaleAndRent" || item.ExternalListingType == "SaleAndLease"))
                || (normalizedListingType == "Rent" && item.ExternalListingType == "SaleAndRent")
                || (normalizedListingType == "Lease" && item.ExternalListingType == "SaleAndLease"));
        }

        if (normalizedLocation != null)
        {
            demarcationQuery = demarcationQuery.Where(item =>
                (item.EstateManagedAsset.Location != null && item.EstateManagedAsset.Location.ToLower().Contains(normalizedLocation))
                || (item.EstateManagedAsset.Town != null && item.EstateManagedAsset.Town.ToLower().Contains(normalizedLocation))
                || (item.EstateManagedAsset.District != null && item.EstateManagedAsset.District.ToLower().Contains(normalizedLocation))
                || (item.EstateManagedAsset.Region != null && item.EstateManagedAsset.Region.ToLower().Contains(normalizedLocation)));
        }

        if (normalizedSearch != null)
        {
            demarcationQuery = demarcationQuery.Where(item =>
                item.Description.ToLower().Contains(normalizedSearch)
                || (item.ChildFixedAssetReference != null && item.ChildFixedAssetReference.ToLower().Contains(normalizedSearch))
                || item.EstateManagedAsset.AssetCode.ToLower().Contains(normalizedSearch)
                || item.EstateManagedAsset.Name.ToLower().Contains(normalizedSearch)
                || (item.EstateManagedAsset.Description != null && item.EstateManagedAsset.Description.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.Location != null && item.EstateManagedAsset.Location.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.Town != null && item.EstateManagedAsset.Town.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.District != null && item.EstateManagedAsset.District.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.ProjectTitle != null && item.EstateManagedAsset.ProjectTitle.ToLower().Contains(normalizedSearch)));
        }

        demarcationQuery = ApplyPublicPriceFilter(demarcationQuery, normalizedListingType, minPrice, maxPrice);

        if (listingId.HasValue)
        {
            query = query.Where(asset => asset.Id == listingId.Value);
            demarcationQuery = demarcationQuery.Where(item => item.Id == listingId.Value);
        }

        var listingsPage = await BuildExternalListingsPageAsync(
            query,
            demarcationQuery,
            normalizedPage,
            normalizedPageSize,
            usePublicImageRoute: false,
            cancellationToken);

        return Ok(new
        {
            success = true,
            data = listingsPage.Items,
            pagination = new
            {
                page = normalizedPage,
                pageSize = normalizedPageSize,
                totalCount = listingsPage.TotalCount,
                totalPages = Math.Max(1, (int)Math.Ceiling(listingsPage.TotalCount / (double)normalizedPageSize)),
                hasPreviousPage = normalizedPage > 1,
                hasNextPage = (long)normalizedPage * normalizedPageSize < listingsPage.TotalCount
            }
        });
    }

    [AllowAnonymous]
    [HttpGet("/api/estate/public/listings")]
    public async Task<IActionResult> GetPublicListings(
        [FromQuery] string? location = null,
        [FromQuery] string? listingType = null,
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default)
    {
        if (minPrice < 0 || maxPrice < 0 || (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice))
        {
            return BadRequest(new { success = false, message = "Enter a valid price range." });
        }

        var tenantId = await ResolvePublicTenantIdAsync(cancellationToken);
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        var normalizedLocation = Normalize(location);
        var normalizedSearch = Normalize(search);
        var normalizedListingType = NormalizeListingType(listingType);
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedPageSize = Math.Clamp(pageSize ?? take ?? 10, 1, 10);
        var activeAllocatedListingIds = await GetActiveSalesAllocationListingIdsAsync(tenantId, cancellationToken);

        var query = WhereExternallyAvailableListings(_db.EstateManagedAssets
            .AsNoTracking())
            .Where(asset => asset.TenantId == tenantId
                && (asset.AssetType == EstateManagedAssetType.Land
                    || asset.AssetType == EstateManagedAssetType.Property
                    || asset.AssetType == EstateManagedAssetType.Facility));
        if (activeAllocatedListingIds.Length > 0)
            query = query.Where(asset => !activeAllocatedListingIds.Contains(asset.Id));

        if (normalizedListingType != null)
        {
            query = query.Where(asset => asset.ExternalListingType == normalizedListingType
                || (normalizedListingType == "Sale" && (asset.ExternalListingType == "SaleAndRent" || asset.ExternalListingType == "SaleAndLease"))
                || (normalizedListingType == "Rent" && asset.ExternalListingType == "SaleAndRent")
                || (normalizedListingType == "Lease" && asset.ExternalListingType == "SaleAndLease"));
        }

        if (normalizedLocation != null)
        {
            query = query.Where(asset =>
                (asset.Location != null && asset.Location.ToLower().Contains(normalizedLocation))
                || (asset.Town != null && asset.Town.ToLower().Contains(normalizedLocation))
                || (asset.District != null && asset.District.ToLower().Contains(normalizedLocation))
                || (asset.Region != null && asset.Region.ToLower().Contains(normalizedLocation)));
        }

        if (normalizedSearch != null)
        {
            query = query.Where(asset =>
                (asset.AssetCode != null && asset.AssetCode.ToLower().Contains(normalizedSearch))
                || (asset.Name != null && asset.Name.ToLower().Contains(normalizedSearch))
                || (asset.Description != null && asset.Description.ToLower().Contains(normalizedSearch))
                || (asset.Location != null && asset.Location.ToLower().Contains(normalizedSearch))
                || (asset.Town != null && asset.Town.ToLower().Contains(normalizedSearch))
                || (asset.District != null && asset.District.ToLower().Contains(normalizedSearch))
                || (asset.ProjectTitle != null && asset.ProjectTitle.ToLower().Contains(normalizedSearch))
                || (asset.UnitType != null && asset.UnitType.ToLower().Contains(normalizedSearch)));
        }

        query = ApplyPublicPriceFilter(query, normalizedListingType, minPrice, maxPrice);

        var demarcationQuery = _db.EstateLandDemarcations
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Published"
                && item.BoundaryVerified
                && item.EstateManagedAsset.TenantId == tenantId
                && !item.EstateManagedAsset.IsDeleted
                && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                && !item.EstateManagedAsset.ProjectId.HasValue
                && !item.EstateManagedAsset.IsPublishedToExternalPortal);
        if (activeAllocatedListingIds.Length > 0)
            demarcationQuery = demarcationQuery.Where(item => !activeAllocatedListingIds.Contains(item.Id));

        if (normalizedListingType != null)
        {
            demarcationQuery = demarcationQuery.Where(item => item.ExternalListingType == normalizedListingType
                || (normalizedListingType == "Sale" && (item.ExternalListingType == "SaleAndRent" || item.ExternalListingType == "SaleAndLease"))
                || (normalizedListingType == "Rent" && item.ExternalListingType == "SaleAndRent")
                || (normalizedListingType == "Lease" && item.ExternalListingType == "SaleAndLease"));
        }

        if (normalizedLocation != null)
        {
            demarcationQuery = demarcationQuery.Where(item =>
                (item.EstateManagedAsset.Location != null && item.EstateManagedAsset.Location.ToLower().Contains(normalizedLocation))
                || (item.EstateManagedAsset.Town != null && item.EstateManagedAsset.Town.ToLower().Contains(normalizedLocation))
                || (item.EstateManagedAsset.District != null && item.EstateManagedAsset.District.ToLower().Contains(normalizedLocation))
                || (item.EstateManagedAsset.Region != null && item.EstateManagedAsset.Region.ToLower().Contains(normalizedLocation)));
        }

        if (normalizedSearch != null)
        {
            demarcationQuery = demarcationQuery.Where(item =>
                item.Description.ToLower().Contains(normalizedSearch)
                || (item.ChildFixedAssetReference != null && item.ChildFixedAssetReference.ToLower().Contains(normalizedSearch))
                || item.EstateManagedAsset.AssetCode.ToLower().Contains(normalizedSearch)
                || item.EstateManagedAsset.Name.ToLower().Contains(normalizedSearch)
                || (item.EstateManagedAsset.Description != null && item.EstateManagedAsset.Description.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.Location != null && item.EstateManagedAsset.Location.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.Town != null && item.EstateManagedAsset.Town.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.District != null && item.EstateManagedAsset.District.ToLower().Contains(normalizedSearch))
                || (item.EstateManagedAsset.ProjectTitle != null && item.EstateManagedAsset.ProjectTitle.ToLower().Contains(normalizedSearch)));
        }

        demarcationQuery = ApplyPublicPriceFilter(demarcationQuery, normalizedListingType, minPrice, maxPrice);

        var listingsPage = await BuildExternalListingsPageAsync(
            query,
            demarcationQuery,
            normalizedPage,
            normalizedPageSize,
            usePublicImageRoute: true,
            cancellationToken);

        return Ok(new
        {
            success = true,
            data = listingsPage.Items,
            pagination = new
            {
                page = normalizedPage,
                pageSize = normalizedPageSize,
                totalCount = listingsPage.TotalCount,
                totalPages = Math.Max(1, (int)Math.Ceiling(listingsPage.TotalCount / (double)normalizedPageSize)),
                hasPreviousPage = normalizedPage > 1,
                hasNextPage = (long)normalizedPage * normalizedPageSize < listingsPage.TotalCount
            }
        });
    }

    private async Task<ListingPageResult> BuildExternalListingsPageAsync(
        IQueryable<EstateManagedAsset> assetQuery,
        IQueryable<EstateLandDemarcation> demarcationQuery,
        int normalizedPage,
        int normalizedPageSize,
        bool usePublicImageRoute,
        CancellationToken cancellationToken)
    {
        var sourceTake = CalculateListingsToRead(normalizedPage, normalizedPageSize);
        var pageOffset = sourceTake - normalizedPageSize;

        var assetCount = await assetQuery.CountAsync(cancellationToken);
        var demarcationCount = await demarcationQuery.CountAsync(cancellationToken);

        var filteredAssets = await assetQuery
            .OrderByDescending(item => item.ExternalPublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .ThenBy(item => item.AssetCode)
            .ThenBy(item => item.Id)
            .Take(sourceTake)
            .Include(asset => asset.Documents.Where(document => !document.IsDeleted && document.IsListingImage))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var filteredDemarcations = await demarcationQuery
            .OrderByDescending(item => item.ExternalPublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .ThenBy(item => item.EstateManagedAsset.AssetCode)
            .ThenBy(item => item.DemarcationNumber)
            .ThenBy(item => item.Id)
            .Take(sourceTake)
            .Include(item => item.EstateManagedAsset)
                .ThenInclude(asset => asset.Documents.Where(document => !document.IsDeleted && document.IsListingImage))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var listings = filteredAssets
            .Select(item => (
                Data: (object)ToExternalListingDto(item, usePublicImageRoute),
                PublishedAt: item.ExternalPublishedAt ?? item.UpdatedAt ?? item.CreatedAt))
            .Concat(filteredDemarcations.Select(item => (
                Data: (object)ToExternalListingDto(item, usePublicImageRoute),
                PublishedAt: item.ExternalPublishedAt ?? item.UpdatedAt ?? item.CreatedAt)))
            .OrderByDescending(item => item.PublishedAt)
            .Skip(pageOffset)
            .Take(normalizedPageSize)
            .Select(item => item.Data)
            .ToList();

        return new ListingPageResult(listings, assetCount + demarcationCount);
    }

    private static int CalculateListingsToRead(int normalizedPage, int normalizedPageSize)
        => normalizedPage > int.MaxValue / normalizedPageSize
            ? int.MaxValue
            : normalizedPage * normalizedPageSize;

    private sealed record ListingPageResult(IReadOnlyList<object> Items, int TotalCount);

    [HttpGet("/api/estate/external/listings/{listingId:guid}/images/{documentId:guid}")]
    public async Task<IActionResult> GetListingImage(Guid listingId, Guid documentId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var externallyAvailableAssetIds = WhereExternallyAvailableListings(
                _db.EstateManagedAssets.AsNoTracking())
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.Id);
        var demarcationListingAssetIds = _db.EstateLandDemarcations
            .AsNoTracking()
            .Where(item => item.Id == listingId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Published"
                && item.BoundaryVerified
                && item.EstateManagedAsset.TenantId == tenantId
                && !item.EstateManagedAsset.IsDeleted
                && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                && !item.EstateManagedAsset.ProjectId.HasValue
                && !item.EstateManagedAsset.IsPublishedToExternalPortal)
            .Select(item => item.EstateManagedAssetId);
        var document = await _db.EstateManagedAssetDocuments
            .AsNoTracking()
            .Include(item => item.EstateManagedAsset)
            .FirstOrDefaultAsync(item => item.Id == documentId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsListingImage
                && (externallyAvailableAssetIds.Contains(item.EstateManagedAssetId)
                    || demarcationListingAssetIds.Contains(item.EstateManagedAssetId)), cancellationToken);

        if (document == null)
        {
            return NotFound(new { success = false, message = "Listing image was not found." });
        }

        var stream = await _fileStorageService.DownloadFileAsync(document.FilePath, document.Id);
        return File(stream, document.ContentType ?? "application/octet-stream", document.FileName);
    }

    [AllowAnonymous]
    [HttpGet("/api/estate/public/listings/{listingId:guid}/images/{documentId:guid}")]
    public async Task<IActionResult> GetPublicListingImage(Guid listingId, Guid documentId, CancellationToken cancellationToken)
    {
        var tenantId = await ResolvePublicTenantIdAsync(cancellationToken);
        if (tenantId == Guid.Empty)
        {
            return NotFound(new { success = false, message = "Listing image was not found." });
        }

        var externallyAvailableAssetIds = WhereExternallyAvailableListings(
                _db.EstateManagedAssets.AsNoTracking())
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.Id);
        var demarcationListingAssetIds = _db.EstateLandDemarcations
            .AsNoTracking()
            .Where(item => item.Id == listingId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Published"
                && item.BoundaryVerified
                && item.EstateManagedAsset.TenantId == tenantId
                && !item.EstateManagedAsset.IsDeleted
                && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                && !item.EstateManagedAsset.ProjectId.HasValue
                && !item.EstateManagedAsset.IsPublishedToExternalPortal)
            .Select(item => item.EstateManagedAssetId);

        var document = await _db.EstateManagedAssetDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == documentId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsListingImage
                && (externallyAvailableAssetIds.Contains(item.EstateManagedAssetId)
                    || demarcationListingAssetIds.Contains(item.EstateManagedAssetId)), cancellationToken);

        if (document == null)
        {
            return NotFound(new { success = false, message = "Listing image was not found." });
        }

        var stream = await _fileStorageService.DownloadFileAsync(document.FilePath, document.Id);
        return File(stream, document.ContentType ?? "application/octet-stream", document.FileName);
    }

    [HttpPost("/api/estate/external/listings/{listingId:guid}/requests")]
    public async Task<IActionResult> CreateListingRequest(
        Guid listingId,
        [FromBody] CreateExternalListingRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var asset = await WhereExternallyAvailableListings(_db.EstateManagedAssets.AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == listingId
                && item.TenantId == tenantId
                && (item.AssetType == EstateManagedAssetType.Land
                    || item.AssetType == EstateManagedAssetType.Property
                    || item.AssetType == EstateManagedAssetType.Facility), cancellationToken);
        var demarcationListing = asset is null
            ? await _db.EstateLandDemarcations
                .AsNoTracking()
                .Include(item => item.EstateManagedAsset)
                .FirstOrDefaultAsync(item => item.Id == listingId
                    && item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.IsPublishedToExternalPortal
                    && item.ExternalListingStatus == "Published"
                    && item.BoundaryVerified
                    && item.EstateManagedAsset.TenantId == tenantId
                    && !item.EstateManagedAsset.IsDeleted
                    && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                    && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                    && !item.EstateManagedAsset.ProjectId.HasValue
                    && !item.EstateManagedAsset.IsPublishedToExternalPortal,
                    cancellationToken)
            : null;
        asset ??= demarcationListing?.EstateManagedAsset;

        if (asset == null)
        {
            return NotFound(new { success = false, message = "Listing was not found or is not available." });
        }

        var listingType = demarcationListing?.ExternalListingType ?? asset.ExternalListingType;
        var listingReference = demarcationListing is null
            ? asset.AssetCode
            : EstateLandDemarcationReference.DisplayReference(
                demarcationListing.ChildFixedAssetReference, asset.AssetCode, demarcationListing.DemarcationNumber);
        var listingName = demarcationListing is null
            ? asset.Name
            : EstateLandDemarcationReference.DisplayReference(
                demarcationListing.ChildFixedAssetReference, asset.AssetCode, demarcationListing.DemarcationNumber);
        var listingCurrency = demarcationListing?.ExternalListingCurrency ?? asset.ExternalListingCurrency;
        var listingSalePrice = demarcationListing?.ExternalSalePrice ?? asset.ExternalSalePrice;
        var listingPrice = demarcationListing is null
            ? asset.ExternalListingPrice
            : ResolveDemarcationLeaseAmount(demarcationListing);
        var listingMonthlyRent = demarcationListing?.ExternalMonthlyRent ?? asset.ExternalMonthlyRent;
        var listingLeaseTermMonths = demarcationListing?.ExternalLeaseTermMonths ?? asset.ExternalLeaseTermMonths;

        var requestType = NormalizeListingRequestType(request.RequestType, listingType);
        if (requestType == "Purchase" && request.OfferAmount is not > 0)
        {
            return BadRequest(new { success = false, message = "Enter a positive bid amount for this land sale." });
        }

        var portalUserId = GetUserId();
        if (portalUserId is null)
        {
            return Unauthorized(new { success = false, message = "A signed-in portal account is required." });
        }

        if (!request.BusinessPartnerId.HasValue)
        {
            return BadRequest(new { success = false, message = "Select the customer account placing this request." });
        }

        var customer = await FindPortalCustomerAsync(
            tenantId,
            portalUserId.Value,
            request.BusinessPartnerId.Value,
            cancellationToken);
        if (customer is null)
        {
            return BadRequest(new
            {
                success = false,
                message = "The selected customer account is not linked to your portal login."
            });
        }

        var alreadySubmitted = await _db.ProcedureCases
            .AsNoTracking()
            .AnyAsync(procedureCase => procedureCase.TenantId == tenantId
                && !procedureCase.IsDeleted
                && procedureCase.OpenedById == portalUserId.Value
                && procedureCase.SourceDepartment == "External Portal - Estate Listings"
                && procedureCase.Status != "Completed"
                && procedureCase.Status != "Archived"
                && procedureCase.Status != "Cancelled"
                && procedureCase.Status != "Canceled"
                && procedureCase.Status != "Rejected"
                && procedureCase.Status != "Closed"
                && !procedureCase.Fields.Any(field => !field.IsDeleted
                    && field.Key == "applicationStatus"
                    && (field.Value == "Rejected"
                        || field.Value == "Withdrawn"
                        || field.Value == "Cancelled"
                        || field.Value == "Canceled"
                        || field.Value == "Archived"
                        || field.Value == "Sale completed"
                        || field.Value == "Agreement fully executed"))
                && procedureCase.Fields.Any(field => !field.IsDeleted
                    && field.Key == "sourceReference"
                    && field.Value == customer.Id.ToString())
                && procedureCase.Fields.Any(field => !field.IsDeleted
                    && field.Key == "propertyUnit"
                    && (field.Value == listingReference
                        || field.Value == asset.AssetCode
                        || (asset.ProjectUnitCode != null && field.Value == asset.ProjectUnitCode))),
                cancellationToken);
        if (alreadySubmitted)
        {
            return Conflict(new
            {
                success = false,
                message = "You have already submitted a request for this property. Track it under My Property Requests."
            });
        }

        var applicantName = customer.PartnerName;
        var reference = BuildExternalReference("LISTING");
        var requestLabel = requestType == "Purchase"
            ? "Purchase bid"
            : requestType == "Lease"
                ? "Lease request"
                : "Rental request";
        var transactionSummary = requestType == "Purchase"
            ? $"{requestLabel} for {listingReference} - {listingName} by {customer.PartnerName} ({customer.CustomerAccountNumber}) at {listingCurrency} {request.OfferAmount:0.##}."
            : $"{requestLabel} for {listingReference} - {listingName} by {customer.PartnerName} ({customer.CustomerAccountNumber}).";
        var description = string.IsNullOrWhiteSpace(request.Message)
            ? transactionSummary
            : $"{transactionSummary} {request.Message.Trim()}";
        var publishedListingType = listingType == "SaleAndRent"
            ? "Sale or Rent"
            : listingType == "SaleAndLease"
                ? "Sale or Lease"
                : listingType;
        var publishedAmount = requestType == "Purchase"
            ? listingSalePrice ?? listingPrice
            : requestType == "Lease" ? listingPrice ?? listingMonthlyRent : listingMonthlyRent ?? listingPrice;

        var fieldValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["applicationReference"] = reference,
            ["sourceWorkspace"] = "External Portal - Property Listings",
            ["sourceReference"] = customer.Id.ToString(),
            ["customerAccountReference"] = customer.CustomerAccountNumber,
            ["customerName"] = customer.PartnerName,
            ["propertyUnit"] = listingReference,
            ["listingReference"] = listingReference,
            ["listingId"] = listingId.ToString(),
            ["listingRecordType"] = demarcationListing is null ? "EstateManagedAsset" : "EstateLandDemarcation",
            ["listingName"] = listingName,
            ["listingLocation"] = asset.Location,
            ["listingArea"] = demarcationListing is null
                ? asset.AreaValue?.ToString(CultureInfo.InvariantCulture)
                : demarcationListing.AreaSquareFeet.ToString(CultureInfo.InvariantCulture),
            ["listingAreaUnit"] = demarcationListing is null ? asset.AreaUnit : "sq ft",
            ["listingType"] = publishedListingType,
            ["requestType"] = requestLabel,
            ["listingPrice"] = publishedAmount?.ToString("0.##"),
            ["offerAmount"] = requestType == "Purchase" ? request.OfferAmount?.ToString("0.##") : null,
            ["groundRentRequired"] = (demarcationListing?.ExternalGroundRentRequired ?? asset.ExternalGroundRentRequired) == true ? "Yes" : "No",
            ["premiumChargeRequired"] = (demarcationListing?.ExternalPremiumChargeRequired ?? asset.ExternalPremiumChargeRequired) == true ? "Yes" : "No",
            ["premiumChargeAmount"] = (demarcationListing?.ExternalPremiumChargeAmount ?? asset.ExternalPremiumChargeAmount)?.ToString("0.00", CultureInfo.InvariantCulture),
            ["salesAmountPaid"] = "0.00",
            ["estateRemainingAmount"] = (requestType == "Purchase" ? request.OfferAmount : publishedAmount)?.ToString("0.00", CultureInfo.InvariantCulture),
            ["salePaymentStatus"] = requestType is "Purchase" or "Lease" ? "Pending Estate payment" : null,
            ["salePaymentCheckStatus"] = requestType is "Purchase" or "Lease" ? "No Sales payment recorded; Finance balance invoice required after agreement execution." : null,
            ["currency"] = listingCurrency,
            ["requestedLeaseTerm"] = requestType == "Purchase" || !listingLeaseTermMonths.HasValue
                ? null
                : FormatRecurringTerm(listingLeaseTermMonths.Value),
            ["requestMessage"] = request.Message?.Trim(),
            ["customerValidationStatus"] = "Pending",
            ["listingValidationStatus"] = "Pending",
            ["availabilityCheck"] = "Pending",
            ["commercialReviewStatus"] = "Pending",
            ["decisionStatus"] = "Pending review",
            ["reservationStatus"] = "Not reserved",
            ["customerNotificationStatus"] = "Not notified",
            ["customerAcceptanceStatus"] = "Pending",
            ["customerAcceptanceDate"] = null,
            ["agreementTemplateReference"] = null,
            ["generatedAgreementReference"] = null,
            ["signedAgreementReference"] = null,
            ["moveInDate"] = null,
            ["billingStartDate"] = null,
            ["billingStartStatus"] = requestType is "Purchase" or "Lease"
                ? "Not applicable"
                : "Blocked - agreement pending",
            ["receivedDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["applicationStatus"] = "Submitted",
            ["notes"] = description
        };

        try
        {
            var created = await _procedureCaseService.CreateCaseAsync(new CreateProcedureCaseRequest(
                "PropertyManagement",
                "EstatePropertyManagementListingApplication",
                $"{requestLabel} - {listingName}",
                reference,
                applicantName,
                "External Portal - Estate Listings",
                DateTime.UtcNow,
                description,
                fieldValues));

            await NotifyListingRequestAsync(asset, created.Id, created.ReferenceNumber ?? reference, requestType, applicantName, cancellationToken);

            return Ok(new
            {
                success = true,
                data = new
                {
                    created.Id,
                    created.Module,
                    created.EntityType,
                    created.Title,
                    created.ReferenceNumber,
                    created.Status,
                    created.CurrentStageName,
                    created.CurrentAssignedRole,
                    Documents = created.Documents
                        .Where(document => string.Equals(document.ProvidedBy, "Customer", StringComparison.OrdinalIgnoreCase))
                        .Select(document => new
                        {
                            document.Id,
                            document.Name,
                            document.RequiredFrom,
                            document.ProvidedBy,
                            document.IsMandatory,
                            document.FileName
                        })
                        .ToList(),
                    CreatedAt = DateTime.UtcNow
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("/api/estate/external/enquiry-profiles")]
    public async Task<IActionResult> GetEnquiryProfiles(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var userId = GetUserId();
        if (tenantId == Guid.Empty || userId is null) return Unauthorized();
        var profiles = await PortalEnquiryPartners(tenantId, userId.Value).OrderBy(p => p.PartnerName)
            .Select(p => new { p.Id, p.PartnerName, p.PrimaryEmail, p.PrimaryPhone, p.PartnerType }).ToListAsync(cancellationToken);
        return Ok(new { success = true, data = profiles });
    }

    [HttpPost("/api/estate/external/listings/{listingId:guid}/enquiries")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<IActionResult> CreateListingEnquiry(Guid listingId,
        [FromBody] CreatePropertyListingEnquiryRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.SubmissionId == Guid.Empty || string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 4000)
            return BadRequest(new { success = false, message = "Enter an enquiry message of up to 4,000 characters and a submission identifier." });
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (await HasActiveSalesAllocationForListingAsync(tenantId, listingId, cancellationToken))
            {
                return Conflict(new
                {
                    success = false,
                    message = "This property has already been reserved and is no longer accepting enquiries."
                });
            }
            var asset = await WhereExternallyAvailableListings(_db.EstateManagedAssets.AsNoTracking())
                .FirstOrDefaultAsync(item => item.Id == listingId
                    && item.TenantId == tenantId
                    && (item.AssetType == EstateManagedAssetType.Land
                        || item.AssetType == EstateManagedAssetType.Property
                        || item.AssetType == EstateManagedAssetType.Facility), cancellationToken);
            var demarcationListing = asset is null
                ? await _db.EstateLandDemarcations
                    .AsNoTracking()
                    .Include(item => item.EstateManagedAsset)
                    .FirstOrDefaultAsync(item => item.Id == listingId
                        && item.TenantId == tenantId
                        && !item.IsDeleted
                        && item.IsPublishedToExternalPortal
                        && item.ExternalListingStatus == "Published"
                        && item.BoundaryVerified
                        && item.EstateManagedAsset.TenantId == tenantId
                        && !item.EstateManagedAsset.IsDeleted
                        && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                        && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                        && !item.EstateManagedAsset.ProjectId.HasValue
                        && !item.EstateManagedAsset.IsPublishedToExternalPortal,
                        cancellationToken)
                : null;
            asset ??= demarcationListing?.EstateManagedAsset;

            if (asset == null)
            {
                return NotFound(new { success = false, message = "Listing was not found or is not available." });
            }

            var userId = GetUserId();
            if (userId is null || tenantId == Guid.Empty) return Unauthorized();
            var partners = PortalEnquiryPartners(tenantId, userId.Value);
            var partner = request.BusinessPartnerId.HasValue
                ? await partners.FirstOrDefaultAsync(p => p.Id == request.BusinessPartnerId, cancellationToken)
                : await partners.OrderBy(p => p.PartnerName).FirstOrDefaultAsync(cancellationToken);
            if (request.BusinessPartnerId.HasValue && partner is null)
                return BadRequest(new { success = false, message = "The selected business partner is not linked to your portal account." });
            var duplicate = await FindDuplicatePropertyEnquiryAsync(
                tenantId,
                listingId,
                request.SubmissionId,
                partner?.Id,
                _currentUserService.Email ?? partner?.PrimaryEmail,
                partner?.PrimaryPhone,
                cancellationToken);
            if (duplicate is not null)
            {
                return Conflict(new
                {
                    success = false,
                    message = $"You already have an active enquiry for this property ({duplicate.TicketNumber}). Sales will continue from that request."
                });
            }
            var reference = demarcationListing is null ? asset.AssetCode
                : EstateLandDemarcationReference.DisplayReference(
                    demarcationListing.ChildFixedAssetReference, asset.AssetCode, demarcationListing.DemarcationNumber);
            var name = demarcationListing is null ? asset.Name : EstateLandDemarcationReference.DisplayReference(
                demarcationListing.ChildFixedAssetReference, asset.AssetCode, demarcationListing.DemarcationNumber);
            var type = demarcationListing?.ExternalListingType ?? asset.ExternalListingType;
            var currency = demarcationListing?.ExternalListingCurrency ?? asset.ExternalListingCurrency;
            var price = type == "Rent"
                ? demarcationListing?.ExternalMonthlyRent ?? asset.ExternalMonthlyRent
                : type == "Lease"
                    ? (demarcationListing is null ? asset.ExternalListingPrice : ResolveDemarcationLeaseAmount(demarcationListing))
                    : demarcationListing?.ExternalSalePrice ?? asset.ExternalSalePrice ?? demarcationListing?.ExternalListingPrice ?? asset.ExternalListingPrice;
            var category = await _db.EhcTicketCategories.AsNoTracking().FirstOrDefaultAsync(c =>
                c.TenantId == tenantId && !c.IsDeleted && c.Code == "PROPERTY-LISTING", cancellationToken);
            if (category is null)
            {
                _logger.LogError(
                    "Property enquiry routing category PROPERTY-LISTING is missing for tenant {TenantId}; listing {ListingId}; trace {TraceId}",
                    tenantId,
                    listingId,
                    HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "The enquiry could not be sent right now. Please try again later." });
            }

            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            await _captchaService.EnsureCaptchaValidAsync(tenantId, request.CaptchaToken,
                string.IsNullOrWhiteSpace(forwardedHost) ? Request.Host.Host : forwardedHost,
                HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            var property = new EhcPropertyListingContextDto("estate-public-listing", listingId, reference, name, type,
                string.IsNullOrWhiteSpace(currency) ? "GHS" : currency, asset.Location, price, asset.Id, demarcationListing?.Id,
                partner?.Id, partner?.PartnerName ?? _currentUserService.FullName,
                _currentUserService.FullName, _currentUserService.Email ?? partner?.PrimaryEmail, partner?.PrimaryPhone);
            var ticket = await _ticketService.CreateExternalPropertyEnquiryAsync(new CreateEhcTicketRequestDto
            {
                TicketType = EhcTicketType.Enquiry, Source = EhcTicketSource.Web, CategoryId = category.Id,
                Subject = Truncate($"Property enquiry: {name}", 200), Description = request.Message.Trim(),
                RelatedEntityType = "EstateListing", RelatedEntityReference = reference
            }, property, request.SubmissionId, cancellationToken);
            return Ok(new { success = true, data = ticket });
        }
        catch (CaptchaVerificationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Property enquiry validation/setup failed for listing {ListingId}; submission {SubmissionId}; trace {TraceId}",
                listingId,
                request.SubmissionId,
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "The enquiry could not be sent right now. Please try again later."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Property enquiry could not be completed for listing {ListingId}; submission {SubmissionId}; trace {TraceId}",
                listingId,
                request.SubmissionId,
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "The enquiry could not be sent right now. Please try again later."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create external property enquiry for listing {ListingId}; submission {SubmissionId}; trace {TraceId}",
                listingId,
                request.SubmissionId,
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "The enquiry could not be sent right now. Please try again later."
            });
        }
    }

    [AllowAnonymous]
    [HttpPost("/api/estate/public/property-enquiry-contacts/challenges")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<IActionResult> RequestPublicPropertyEnquiryContactChallenge(
        [FromBody] PublicPropertyEnquiryContactChallengeRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "Select Email or Phone and enter a valid contact." });
        if (request.ListingId == Guid.Empty)
            return BadRequest(new { success = false, message = "A property listing is required." });
        if (!TryNormalizePublicContact(request.Channel, request.Contact, out var channel, out var normalizedContact, out var validationMessage))
        {
            return BadRequest(new { success = false, message = validationMessage ?? "Select Email or Phone and enter a valid contact." });
        }

        try
        {
            var tenantId = await ResolvePublicTenantIdAsync(cancellationToken);
            if (tenantId == Guid.Empty)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "Verification is unavailable right now. Please try again later." });

            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            await _captchaService.EnsureCaptchaValidAsync(
                tenantId,
                request.CaptchaToken,
                string.IsNullOrWhiteSpace(forwardedHost) ? Request.Host.Host : forwardedHost,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            var (asset, _) = await LoadExternalListingForEnquiryAsync(tenantId, request.ListingId, cancellationToken);
            if (asset is null)
                return NotFound(new { success = false, message = "Listing was not found or is not available." });

            var now = DateTime.UtcNow;
            var verification = new EhcPublicPropertyEnquiryVerification
            {
                TenantId = tenantId,
                ListingId = request.ListingId,
                Channel = channel,
                ContactHash = HashPublicContact(normalizedContact),
                RequestedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(10),
                CreatedAt = now,
                CreatedBy = "public-property-enquiry"
            };
            _db.EhcPublicPropertyEnquiryVerifications.Add(verification);
            await _db.SaveChangesAsync(cancellationToken);

            var otpChannel = channel == "Email" ? OtpChannel.Email : OtpChannel.Sms;
            var code = await _otpService.CreateOtpAsync(
                tenantId,
                OtpPurpose.PublicPropertyEnquiry,
                otpChannel,
                normalizedContact,
                TimeSpan.FromMinutes(10),
                maxAttempts: 5,
                cancellationToken);

            try
            {
                if (otpChannel == OtpChannel.Email)
                {
                    await _tenantEmailSender.SendAsync(
                        tenantId,
                        normalizedContact,
                        "Property enquiry verification code",
                        $"<p>Your property enquiry verification code is <strong>{code}</strong>. It expires in 10 minutes.</p>",
                        isHtml: true,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await _tenantSmsSender.SendOtpAsync(
                        tenantId,
                        normalizedContact,
                        $"Your property enquiry verification code is {code}. It expires in 10 minutes.",
                        cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                verification.IsDeleted = true;
                verification.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                HttpContext.Items[SystemExceptionResultLoggingFilter.HandledExceptionItemKey] = ex;
                _logger.LogError(ex,
                    "Public property enquiry {Channel} verification delivery failed for tenant {TenantId}; listing {ListingId}; trace {TraceId}",
                    channel,
                    tenantId,
                    request.ListingId,
                    HttpContext.TraceIdentifier);

                var deliveryLabel = otpChannel == OtpChannel.Email ? "email" : "SMS";
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Verification code could not be sent",
                    Detail = $"The {deliveryLabel} verification code could not be sent. Ask an administrator to check the tenant {deliveryLabel} settings, then try again.",
                    Instance = Request.Path
                };
                problem.Extensions["code"] = otpChannel == OtpChannel.Email
                    ? "PUBLIC_ENQUIRY_EMAIL_DELIVERY_FAILED"
                    : "PUBLIC_ENQUIRY_SMS_DELIVERY_FAILED";
                problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
                return StatusCode(StatusCodes.Status503ServiceUnavailable, problem);
            }

            return Accepted(new
            {
                success = true,
                data = new
                {
                    channel,
                    maskedContact = MaskPublicContact(channel, normalizedContact),
                    expiresInSeconds = 600
                },
                message = "If the contact can receive messages, a verification code has been sent."
            });
        }
        catch (CaptchaVerificationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            HttpContext.Items[SystemExceptionResultLoggingFilter.HandledExceptionItemKey] = ex;
            _logger.LogError(ex,
                "Public property enquiry verification challenge failed for listing {ListingId}; trace {TraceId}",
                request.ListingId,
                HttpContext.TraceIdentifier);
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Verification is temporarily unavailable",
                Detail = "Verification is unavailable right now. Please try again later. If the problem continues, contact your administrator.",
                Instance = Request.Path
            };
            problem.Extensions["code"] = "PUBLIC_ENQUIRY_VERIFICATION_UNAVAILABLE";
            problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
            return StatusCode(StatusCodes.Status503ServiceUnavailable, problem);
        }
    }

    [AllowAnonymous]
    [HttpPost("/api/estate/public/property-enquiry-contacts/verifications")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<IActionResult> VerifyPublicPropertyEnquiryContact(
        [FromBody] PublicPropertyEnquiryContactVerificationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "The verification request is invalid." });
        if (request.ListingId == Guid.Empty)
            return BadRequest(new { success = false, message = "A property listing is required." });
        if (!TryNormalizePublicContact(request.Channel, request.Contact, out var channel, out var normalizedContact, out var validationMessage))
        {
            return BadRequest(new { success = false, message = validationMessage ?? "The verification request is invalid." });
        }

        try
        {
            var tenantId = await ResolvePublicTenantIdAsync(cancellationToken);
            if (tenantId == Guid.Empty)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "Verification is unavailable right now. Please try again later." });

            var now = DateTime.UtcNow;
            var contactHash = HashPublicContact(normalizedContact);
            var challenge = await _db.EhcPublicPropertyEnquiryVerifications
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId &&
                               !item.IsDeleted &&
                               item.ListingId == request.ListingId &&
                               item.Channel == channel &&
                               item.ContactHash == contactHash &&
                               item.VerifiedAtUtc == null &&
                               item.ExpiresAtUtc > now)
                .OrderByDescending(item => item.RequestedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (challenge is null)
                return BadRequest(new { success = false, message = "The verification code is invalid or has expired." });

            var otpResult = await _otpService.VerifyOtpAsync(
                tenantId,
                OtpPurpose.PublicPropertyEnquiry,
                channel == "Email" ? OtpChannel.Email : OtpChannel.Sms,
                normalizedContact,
                request.OtpCode,
                consumeOnSuccess: true,
                cancellationToken);
            await _db.EhcPublicPropertyEnquiryVerifications
                .Where(item => item.Id == challenge.Id && item.VerifiedAtUtc == null && !item.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.VerificationAttemptCount, item => item.VerificationAttemptCount + 1)
                    .SetProperty(item => item.LastAttemptAtUtc, now)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            if (!otpResult.Success)
                return BadRequest(new { success = false, message = "The verification code is invalid or has expired." });

            var token = CreatePublicContactVerificationToken();
            var tokenHash = HashPublicContact(token);
            var expiresAt = now.AddMinutes(10);

            var claimed = await _db.EhcPublicPropertyEnquiryVerifications
                .Where(item => item.Id == challenge.Id && item.VerifiedAtUtc == null && !item.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.VerifiedAtUtc, now)
                    .SetProperty(item => item.VerificationTokenHash, tokenHash)
                    .SetProperty(item => item.ExpiresAtUtc, expiresAt)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);
            if (claimed != 1)
                return BadRequest(new { success = false, message = "The verification code is invalid or has expired." });

            var contact = await _db.EhcPublicPropertyEnquiryContacts.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.Channel == channel &&
                item.NormalizedContact == normalizedContact, cancellationToken);
            var linkedCustomer = await FindApprovedCustomerBusinessPartnerIdAsync(tenantId, channel, normalizedContact, cancellationToken);
            if (contact is not null)
            {
                contact.LastVerifiedAtUtc = now;
                contact.BusinessPartnerId = linkedCustomer;
                contact.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                await _db.EhcPublicPropertyEnquiryVerifications
                    .Where(item => item.Id == challenge.Id && item.ContactId == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ContactId, contact.Id), cancellationToken);
                await LinkHistoricalPublicEnquiriesAsync(contact, cancellationToken);
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    verificationToken = token,
                    expiresAtUtc = expiresAt,
                    profile = new
                    {
                        contactName = string.IsNullOrWhiteSpace(contact?.ContactName) ? null : contact.ContactName,
                        contactEmail = channel == "Email" ? normalizedContact : null,
                        contactPhone = channel == "Phone" ? normalizedContact : null,
                        linkedCustomer = linkedCustomer.HasValue,
                        requiresPortalLogin = linkedCustomer.HasValue,
                        externalPortalPath = linkedCustomer.HasValue
                            ? "/login?redirect=%2Fexternal-portal%2Fproperty-listings"
                            : null
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Public property enquiry contact verification failed for listing {ListingId}; trace {TraceId}",
                request.ListingId,
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { success = false, message = "Verification is unavailable right now. Please try again later." });
        }
    }

    [AllowAnonymous]
    [HttpPost("/api/estate/public/listings/{listingId:guid}/enquiries")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<IActionResult> CreatePublicListingEnquiry(Guid listingId,
        [FromBody] PublicPropertyListingEnquiryRequestDto request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { success = false, message = "Enter the contact details and enquiry message." });
        }

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true))
        {
            return BadRequest(new
            {
                success = false,
                message = validationResults.FirstOrDefault()?.ErrorMessage ?? "The enquiry details are invalid."
            });
        }

        try
        {
            var tenantId = await ResolvePublicTenantIdAsync(cancellationToken);
            if (tenantId == Guid.Empty)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "The enquiry could not be sent right now. Please try again later." });
            }

            var (asset, demarcationListing) = await LoadExternalListingForEnquiryAsync(tenantId, listingId, cancellationToken);
            if (asset == null)
            {
                return NotFound(new { success = false, message = "Listing was not found or is not available." });
            }

            var selectedContact = string.Equals(request.PreferredContactMethod, "Email", StringComparison.OrdinalIgnoreCase)
                ? request.ContactEmail
                : request.ContactPhone;
            if (!TryNormalizePublicContact(request.PreferredContactMethod, selectedContact, out var channel,
                    out var normalizedContact, out var contactValidationMessage))
            {
                return BadRequest(new { success = false, message = contactValidationMessage ?? "The selected contact is invalid." });
            }

            var now = DateTime.UtcNow;
            var tokenHash = HashPublicContact(request.ContactVerificationToken);
            var contactHash = HashPublicContact(normalizedContact);
            var grant = await _db.EhcPublicPropertyEnquiryVerifications.AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                item.ListingId == listingId &&
                item.Channel == channel &&
                item.ContactHash == contactHash &&
                item.VerificationTokenHash == tokenHash &&
                item.VerifiedAtUtc != null &&
                item.ExpiresAtUtc > now,
                cancellationToken);
            if (grant is null)
                return BadRequest(new { success = false, message = "Verify your selected contact method before sending the enquiry." });

            if (grant.ConsumedAtUtc.HasValue && grant.ConsumedSubmissionId != request.SubmissionId)
                return BadRequest(new { success = false, message = "This contact verification has already been used. Request a new code." });

            var publicContact = grant.ContactId.HasValue
                ? await _db.EhcPublicPropertyEnquiryContacts.FirstOrDefaultAsync(item =>
                    item.Id == grant.ContactId.Value && item.TenantId == tenantId && !item.IsDeleted &&
                    item.Channel == channel && item.NormalizedContact == normalizedContact,
                    cancellationToken)
                : await _db.EhcPublicPropertyEnquiryContacts.FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && !item.IsDeleted && item.Channel == channel &&
                    item.NormalizedContact == normalizedContact,
                    cancellationToken);
            if (grant.ContactId.HasValue && publicContact is null)
                return BadRequest(new { success = false, message = "Verify your selected contact method before sending the enquiry." });

            var linkedCustomer = await FindApprovedCustomerBusinessPartnerIdAsync(tenantId, channel, normalizedContact, cancellationToken);
            if (linkedCustomer.HasValue)
            {
                return Conflict(new
                {
                    success = false,
                    code = "CUSTOMER_PORTAL_REQUIRED",
                    message = "This verified contact belongs to an existing customer. Sign in to the external portal to continue.",
                    externalPortalPath = "/login?redirect=%2Fexternal-portal%2Fproperty-listings"
                });
            }

            var duplicate = await FindDuplicatePropertyEnquiryAsync(
                tenantId,
                listingId,
                request.SubmissionId,
                businessPartnerId: null,
                contactEmail: channel == "Email" ? normalizedContact : null,
                contactPhone: channel == "Phone" ? normalizedContact : null,
                cancellationToken);
            if (duplicate is not null)
            {
                return Conflict(new
                {
                    success = false,
                    message = "You already have an active enquiry for this property. Sales will continue from that request."
                });
            }

            var category = await _db.EhcTicketCategories.AsNoTracking().FirstOrDefaultAsync(c =>
                c.TenantId == tenantId && !c.IsDeleted && c.Code == "PROPERTY-LISTING", cancellationToken);
            if (category is null)
            {
                _logger.LogError(
                    "Property enquiry routing category PROPERTY-LISTING is missing for tenant {TenantId}; listing {ListingId}; trace {TraceId}",
                    tenantId,
                    listingId,
                    HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "The enquiry could not be sent right now. Please try again later." });
            }

            var workflowActor = await ResolvePublicPropertyEnquiryWorkflowActorAsync(tenantId, cancellationToken);
            if (workflowActor is null)
            {
                _logger.LogError(
                    "No active public property enquiry workflow actor exists for tenant {TenantId}; listing {ListingId}; trace {TraceId}",
                    tenantId,
                    listingId,
                    HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "The enquiry could not be sent right now. Please try again later." });
            }

            if (!grant.ConsumedAtUtc.HasValue)
            {
                var consumed = await _db.EhcPublicPropertyEnquiryVerifications
                    .Where(item => item.Id == grant.Id && item.ConsumedAtUtc == null && !item.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.ConsumedAtUtc, now)
                        .SetProperty(item => item.ConsumedSubmissionId, request.SubmissionId)
                        .SetProperty(item => item.UpdatedAt, now), cancellationToken);
                if (consumed != 1)
                {
                    var concurrentlyConsumed = await _db.EhcPublicPropertyEnquiryVerifications.AsNoTracking()
                        .FirstOrDefaultAsync(item => item.Id == grant.Id, cancellationToken);
                    if (concurrentlyConsumed?.ConsumedSubmissionId != request.SubmissionId)
                        return BadRequest(new { success = false, message = "This contact verification has already been used. Request a new code." });
                }
            }

            if (publicContact is null)
            {
                publicContact = new EhcPublicPropertyEnquiryContact
                {
                    TenantId = tenantId,
                    Channel = channel,
                    NormalizedContact = normalizedContact,
                    ContactName = request.ContactName.Trim(),
                    LastVerifiedAtUtc = grant.VerifiedAtUtc ?? now,
                    CreatedAt = now,
                    CreatedBy = "public-property-enquiry"
                };
                _db.EhcPublicPropertyEnquiryContacts.Add(publicContact);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException) when (_db.Entry(publicContact).State == EntityState.Added)
                {
                    // A different verified grant for this same tenant/contact may submit at the
                    // same time. The filtered unique index owns identity: reuse its winning row.
                    _db.Entry(publicContact).State = EntityState.Detached;
                    publicContact = await _db.EhcPublicPropertyEnquiryContacts.FirstAsync(item =>
                        item.TenantId == tenantId && !item.IsDeleted && item.Channel == channel &&
                        item.NormalizedContact == normalizedContact, cancellationToken);
                }
            }
            else if (string.IsNullOrWhiteSpace(publicContact.ContactName))
            {
                publicContact.ContactName = request.ContactName.Trim();
                publicContact.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (!grant.ContactId.HasValue)
            {
                await _db.EhcPublicPropertyEnquiryVerifications
                    .Where(item => item.Id == grant.Id && item.ContactId == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ContactId, publicContact.Id), cancellationToken);
            }
            await LinkHistoricalPublicEnquiriesAsync(publicContact, cancellationToken);

            var reference = demarcationListing is null ? asset.AssetCode
                : EstateLandDemarcationReference.DisplayReference(
                    demarcationListing.ChildFixedAssetReference, asset.AssetCode, demarcationListing.DemarcationNumber);
            var name = demarcationListing is null ? asset.Name : EstateLandDemarcationReference.DisplayReference(
                demarcationListing.ChildFixedAssetReference, asset.AssetCode, demarcationListing.DemarcationNumber);
            var type = demarcationListing?.ExternalListingType ?? asset.ExternalListingType;
            var currency = demarcationListing?.ExternalListingCurrency ?? asset.ExternalListingCurrency;
            var price = type == "Rent"
                ? demarcationListing?.ExternalMonthlyRent ?? asset.ExternalMonthlyRent
                : type == "Lease"
                    ? (demarcationListing is null ? asset.ExternalListingPrice : ResolveDemarcationLeaseAmount(demarcationListing))
                    : demarcationListing?.ExternalSalePrice ?? asset.ExternalSalePrice ?? demarcationListing?.ExternalListingPrice ?? asset.ExternalListingPrice;
            var contactName = publicContact.ContactName;
            var contactEmail = channel == "Email" ? normalizedContact : null;
            var contactPhone = channel == "Phone" ? normalizedContact : null;
            var contactReference = string.IsNullOrWhiteSpace(request.ContactReference) ? null : request.ContactReference.Trim();
            var alternativePhoneNumber = string.IsNullOrWhiteSpace(request.AlternativePhoneNumber)
                ? null
                : request.AlternativePhoneNumber.Trim();
            var preferredContactMethod = request.PreferredContactMethod.Trim();
            var property = new EhcPropertyListingContextDto("estate-public-listing", listingId, reference, name, type,
                string.IsNullOrWhiteSpace(currency) ? "GHS" : currency, asset.Location, price, asset.Id, demarcationListing?.Id,
                null, contactName, contactName, contactEmail, contactPhone, contactReference,
                alternativePhoneNumber, preferredContactMethod, publicContact.Id);
            var ticket = await _ticketService.CreatePublicPropertyEnquiryAsync(new CreateEhcTicketRequestDto
            {
                TicketType = EhcTicketType.Enquiry,
                Source = EhcTicketSource.Web,
                CategoryId = category.Id,
                Subject = Truncate($"Property enquiry: {name}", 200),
                Description = request.Message.Trim(),
                RelatedEntityType = "EstateListing",
                RelatedEntityReference = reference
            }, property, request.SubmissionId, tenantId, workflowActor.Id, workflowActor.UserName, cancellationToken);
            publicContact.LastEnquiryAtUtc = now;
            publicContact.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, data = ticket });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Public property enquiry validation/setup failed for listing {ListingId}; submission {SubmissionId}; trace {TraceId}",
                listingId,
                request.SubmissionId,
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "The enquiry could not be sent right now. Please try again later."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Public property enquiry could not be completed for listing {ListingId}; submission {SubmissionId}; trace {TraceId}",
                listingId,
                request.SubmissionId,
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "The enquiry could not be sent right now. Please try again later."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create public property enquiry for listing {ListingId}; submission {SubmissionId}; trace {TraceId}",
                listingId,
                request.SubmissionId,
                HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "The enquiry could not be sent right now. Please try again later."
            });
        }
    }

    private static bool TryNormalizePublicContact(
        string? requestedChannel,
        string? value,
        out string channel,
        out string normalizedContact,
        out string? validationMessage)
    {
        channel = string.Equals(requestedChannel?.Trim(), "Email", StringComparison.OrdinalIgnoreCase)
            ? "Email"
            : string.Equals(requestedChannel?.Trim(), "Phone", StringComparison.OrdinalIgnoreCase)
                ? "Phone"
                : string.Empty;
        normalizedContact = string.Empty;
        validationMessage = null;
        if (channel.Length == 0)
        {
            validationMessage = "Select Email or Phone as the preferred contact method.";
            return false;
        }

        if (channel == "Email")
        {
            var candidate = value?.Trim().ToLowerInvariant() ?? string.Empty;
            try
            {
                var parsed = new MailAddress(candidate);
                if (!string.Equals(parsed.Address, candidate, StringComparison.OrdinalIgnoreCase) || candidate.Length > 320)
                    throw new FormatException();
                normalizedContact = candidate;
                return true;
            }
            catch (FormatException)
            {
                validationMessage = "Enter a valid email address.";
                return false;
            }
        }

        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.StartsWith("00", StringComparison.Ordinal))
            trimmed = $"+{trimmed[2..]}";
        if (!trimmed.StartsWith('+') || trimmed.Length is < 9 or > 16 ||
            trimmed[1] == '0' || trimmed.Skip(1).Any(character => !char.IsDigit(character)))
        {
            validationMessage = "Enter a valid international phone number including its country code.";
            return false;
        }

        normalizedContact = trimmed;
        return true;
    }

    private static string HashPublicContact(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string CreatePublicContactVerificationToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string MaskPublicContact(string channel, string normalizedContact)
    {
        if (channel == "Email")
        {
            var separator = normalizedContact.IndexOf('@');
            if (separator <= 0) return "***";
            var local = normalizedContact[..separator];
            var visible = local[..Math.Min(2, local.Length)];
            return $"{visible}***{normalizedContact[separator..]}";
        }

        return normalizedContact.Length <= 4
            ? "****"
            : $"***{normalizedContact[^4..]}";
    }

    private async Task<Guid?> FindApprovedCustomerBusinessPartnerIdAsync(
        Guid tenantId,
        string channel,
        string normalizedContact,
        CancellationToken cancellationToken)
    {
        var query = _db.BusinessPartners.AsNoTracking().Where(partner =>
            partner.TenantId == tenantId && !partner.IsDeleted && partner.IsActive &&
            partner.ApprovalStatus == "Approved" &&
            _db.BusinessPartnerRoles.Any(role => role.TenantId == tenantId && !role.IsDeleted &&
                role.BusinessPartnerId == partner.Id && role.RoleType == BusinessPartnerRoleType.Customer &&
                role.Status == BusinessPartnerRoleStatus.Active));

        if (channel == "Email")
        {
            return await query
                .Where(partner =>
                    (partner.PrimaryEmail != null && partner.PrimaryEmail.Trim().ToLower() == normalizedContact) ||
                    (partner.UserId != null && _db.Users.Any(user => user.Id == partner.UserId &&
                        user.TenantId == tenantId && user.IsActive && user.Email != null &&
                        user.Email.Trim().ToLower() == normalizedContact)) ||
                    _db.BusinessPartnerUsers.Any(link => link.TenantId == tenantId && !link.IsDeleted &&
                        link.IsActive && link.BusinessPartnerId == partner.Id &&
                        link.User.IsActive && link.User.Email != null && link.User.Email.Trim().ToLower() == normalizedContact))
                .Select(partner => (Guid?)partner.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var candidates = await query
            .Select(partner => new { partner.Id, partner.PrimaryPhone, partner.UserId })
            .ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            if (NormalizeStoredPhone(candidate.PrimaryPhone) == normalizedContact)
                return candidate.Id;
        }

        var candidateIds = candidates.Select(item => item.Id).ToList();
        var directUserIds = candidates.Where(item => item.UserId.HasValue)
            .Select(item => item.UserId!.Value)
            .ToList();
        var directUserPhones = await _db.Users.AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.IsActive && directUserIds.Contains(user.Id) &&
                           user.PhoneNumber != null)
            .Select(user => new { user.Id, user.PhoneNumber })
            .ToListAsync(cancellationToken);
        foreach (var directUser in directUserPhones)
        {
            if (NormalizeStoredPhone(directUser.PhoneNumber) != normalizedContact) continue;
            var partner = candidates.First(item => item.UserId == directUser.Id);
            return partner.Id;
        }

        var linkedUserPhones = await _db.BusinessPartnerUsers.AsNoTracking()
            .Where(link => link.TenantId == tenantId && !link.IsDeleted && link.IsActive &&
                           candidateIds.Contains(link.BusinessPartnerId) &&
                           link.User.IsActive && link.User.PhoneNumber != null)
            .Select(link => new { link.BusinessPartnerId, link.User.PhoneNumber })
            .ToListAsync(cancellationToken);
        foreach (var linkedUser in linkedUserPhones)
        {
            if (NormalizeStoredPhone(linkedUser.PhoneNumber) == normalizedContact)
                return linkedUser.BusinessPartnerId;
        }
        return null;
    }

    private static string? NormalizeStoredPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var trimmed = phone.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
            return $"+{digits[2..]}";
        if (trimmed.StartsWith('+'))
            return $"+{digits}";
        // The ERP's local operating country is Ghana. Only the unambiguous Ghana local form is
        // expanded; other international contacts must already include their country code.
        if (digits.Length == 10 && digits[0] == '0')
            return $"+233{digits[1..]}";
        return null;
    }

    private async Task LinkHistoricalPublicEnquiriesAsync(
        EhcPublicPropertyEnquiryContact contact,
        CancellationToken cancellationToken)
    {
        var candidates = await _db.EhcTickets
            .Where(ticket => ticket.TenantId == contact.TenantId && !ticket.IsDeleted &&
                             ticket.RequesterUserId == null &&
                             ticket.PublicPropertyEnquiryContactId == null &&
                             ticket.PropertyListingContextJson != null)
            .ToListAsync(cancellationToken);
        var changed = false;
        foreach (var ticket in candidates)
        {
            EhcPropertyListingContextDto? context;
            try
            {
                context = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(ticket.PropertyListingContextJson!);
            }
            catch (JsonException)
            {
                continue;
            }

            var rawContact = contact.Channel == "Email" ? context?.ContactEmail : context?.ContactPhone;
            var normalized = contact.Channel == "Email"
                ? rawContact?.Trim().ToLowerInvariant()
                : NormalizeStoredPhone(rawContact);
            if (!string.Equals(normalized, contact.NormalizedContact, StringComparison.Ordinal)) continue;
            ticket.PublicPropertyEnquiryContactId = contact.Id;
            ticket.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<BusinessPartner> PortalEnquiryPartners(Guid tenantId, Guid userId)
        => _db.BusinessPartners.AsNoTracking().Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActive
            && p.ApprovalStatus == "Approved" && (BusinessPartnerRoles.SupplierTypes.Contains(p.PartnerType) || BusinessPartnerRoles.CustomerTypes.Contains(p.PartnerType))
            && (p.UserId == userId || _db.BusinessPartnerUsers.Any(link => link.TenantId == tenantId && !link.IsDeleted
                && link.IsActive && link.UserId == userId && link.BusinessPartnerId == p.Id)));

    private async Task<(EstateManagedAsset? Asset, EstateLandDemarcation? Demarcation)> LoadExternalListingForEnquiryAsync(
        Guid tenantId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        if (await HasActiveSalesAllocationForListingAsync(tenantId, listingId, cancellationToken))
            return (null, null);

        var asset = await WhereExternallyAvailableListings(_db.EstateManagedAssets.AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == listingId
                && item.TenantId == tenantId
                && (item.AssetType == EstateManagedAssetType.Land
                    || item.AssetType == EstateManagedAssetType.Property
                    || item.AssetType == EstateManagedAssetType.Facility), cancellationToken);
        var demarcationListing = asset is null
            ? await _db.EstateLandDemarcations
                .AsNoTracking()
                .Include(item => item.EstateManagedAsset)
                .FirstOrDefaultAsync(item => item.Id == listingId
                    && item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.IsPublishedToExternalPortal
                    && item.ExternalListingStatus == "Published"
                    && item.BoundaryVerified
                    && item.EstateManagedAsset.TenantId == tenantId
                    && !item.EstateManagedAsset.IsDeleted
                    && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                    && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                    && !item.EstateManagedAsset.ProjectId.HasValue
                    && !item.EstateManagedAsset.IsPublishedToExternalPortal,
                    cancellationToken)
            : null;

        return (asset ?? demarcationListing?.EstateManagedAsset, demarcationListing);
    }

    private async Task<Guid[]> GetActiveSalesAllocationListingIdsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return [];

        var sourceItemIds = await _db.SalesAllocations.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && (item.AdapterKey == "land-management" || item.AdapterKey == "property-register")
                && ActiveSalesAllocationStatuses.Contains(item.Status))
            .Select(item => item.SourceItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return sourceItemIds
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
    }

    private Task<bool> HasActiveSalesAllocationForListingAsync(
        Guid tenantId,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || listingId == Guid.Empty)
            return Task.FromResult(false);

        var sourceItemId = listingId.ToString("D");
        return _db.SalesAllocations.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId
            && !item.IsDeleted
            && (item.AdapterKey == "land-management" || item.AdapterKey == "property-register")
            && item.SourceItemId == sourceItemId
            && ActiveSalesAllocationStatuses.Contains(item.Status), cancellationToken);
    }

    private async Task<DuplicatePropertyEnquiry?> FindDuplicatePropertyEnquiryAsync(
        Guid tenantId,
        Guid listingId,
        Guid submissionId,
        Guid? businessPartnerId,
        string? contactEmail,
        string? contactPhone,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim().ToLowerInvariant();
        var normalizedPhone = NormalizeContactPhone(contactPhone);
        if (!businessPartnerId.HasValue && string.IsNullOrWhiteSpace(normalizedEmail) && string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return null;
        }

        var candidates = await _db.EhcTickets
            .AsNoTracking()
            .Where(ticket => ticket.TenantId == tenantId
                && !ticket.IsDeleted
                && ticket.TicketType == EhcTicketType.Enquiry
                && ticket.Status != EhcTicketStatus.Closed
                && ticket.ExternalSubmissionId != submissionId
                && ticket.PropertyListingContextJson != null)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.TicketNumber,
                ticket.Status,
                ticket.PropertyListingContextJson
            })
            .Take(5000)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            EhcPropertyListingContextDto? property;
            try
            {
                property = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(candidate.PropertyListingContextJson!);
            }
            catch (JsonException)
            {
                continue;
            }

            if (property is null ||
                property.ListingId != listingId ||
                !(string.Equals(property.Source, "estate-public-listing", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(property.Source, "state-public-listing", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (businessPartnerId.HasValue && property.BusinessPartnerId == businessPartnerId)
            {
                return new DuplicatePropertyEnquiry(candidate.Id, candidate.TicketNumber, candidate.Status);
            }

            if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                string.Equals(property.ContactEmail?.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicatePropertyEnquiry(candidate.Id, candidate.TicketNumber, candidate.Status);
            }

            if (!string.IsNullOrWhiteSpace(normalizedPhone) &&
                string.Equals(NormalizeContactPhone(property.ContactPhone), normalizedPhone, StringComparison.Ordinal))
            {
                return new DuplicatePropertyEnquiry(candidate.Id, candidate.TicketNumber, candidate.Status);
            }
        }

        return null;
    }

    private async Task<PublicPropertyEnquiryWorkflowActor?> ResolvePublicPropertyEnquiryWorkflowActorAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.IsActive
                && (user.UserName == "external" || user.Email == "external@default.com"))
            .OrderBy(user => user.UserName)
            .Select(user => new PublicPropertyEnquiryWorkflowActor(user.Id, user.UserName ?? "external"))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? NormalizeContactPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? value.Trim() : digits;
    }

    private sealed record DuplicatePropertyEnquiry(Guid Id, string TicketNumber, EhcTicketStatus Status);
    private sealed record PublicPropertyEnquiryWorkflowActor(Guid Id, string UserName);

    private HashSet<string> BuildIdentityTerms()
    {
        var terms = new[]
            {
                _currentUserService.Email,
                _currentUserService.UserName
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant());

        return terms.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? MetadataValue(CentralDocumentRecord record, string fieldKey) =>
        record.MetadataValues
            .FirstOrDefault(value => string.Equals(value.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
            ?.FieldValue;

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private async Task<CentralDocumentRecord?> LoadAuthorizedExternalEstateDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var identities = BuildIdentityTerms();
        if (tenantId == Guid.Empty || identities.Count == 0)
        {
            return null;
        }

        var isRecipient = await _db.CentralDocumentMetadataValues
            .AsNoTracking()
            .AnyAsync(value => value.TenantId == tenantId
                && !value.IsDeleted
                && value.DocumentRecordId == id
                && value.FieldValue != null
                && value.FieldKey == PortalRecipientFieldKey
                && identities.Contains(value.FieldValue!.Trim().ToLower()), cancellationToken);

        if (!isRecipient)
        {
            return null;
        }

        return await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(record => record.Versions.Where(version => !version.IsDeleted))
            .FirstOrDefaultAsync(record => record.TenantId == tenantId
                && !record.IsDeleted
                && record.Id == id
                && record.SourceModule == "Estate"
                && record.LifecycleStatus == "Dispatched"
                && record.RepositoryStatus == "Linked", cancellationToken);
    }

    private static CentralDocumentVersion? SelectExternalDocumentVersion(CentralDocumentRecord record)
    {
        var externallyReleasedVersions = record.Versions
            .Where(version => IsExternalDocumentVersionStatus(version.Status) && HasExternalDocumentVersionPath(version))
            .ToList();

        if (!string.IsNullOrWhiteSpace(record.CurrentVersion))
        {
            var current = externallyReleasedVersions.FirstOrDefault(version =>
                string.Equals(version.VersionNumber, record.CurrentVersion, StringComparison.OrdinalIgnoreCase));
            if (current is not null)
            {
                return current;
            }
        }

        // Estate external portal: dispatched recipients can only receive the version that was current/published externally.
        return externallyReleasedVersions
            .OrderByDescending(item => item.PublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefault();
    }

    private static bool IsExternalDocumentVersionStatus(string? status)
        => string.Equals(status, "Current", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase);

    private static bool HasExternalDocumentVersionPath(CentralDocumentVersion version)
        => !string.IsNullOrWhiteSpace(version.RenditionPath)
            || !string.IsNullOrWhiteSpace(version.RepositoryPath);

    private static string? SelectExternalDocumentPath(CentralDocumentRecord record, CentralDocumentVersion? version)
    {
        if (!string.IsNullOrWhiteSpace(version?.RenditionPath))
        {
            return version.RenditionPath;
        }

        if (!string.IsNullOrWhiteSpace(version?.RepositoryPath))
        {
            return version.RepositoryPath;
        }

        return string.Equals(record.VersionStatus, "Current", StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.VersionStatus, "Published", StringComparison.OrdinalIgnoreCase)
            ? record.RepositoryPath
            : null;
    }

    private static string ResolveExternalContentType(CentralDocumentVersion? version, CentralDocumentRecord record)
    {
        if (!string.IsNullOrWhiteSpace(version?.RenditionPath))
        {
            return "application/pdf";
        }

        if (!string.IsNullOrWhiteSpace(version?.ContentType))
        {
            return version.ContentType;
        }

        var path = version?.RepositoryPath ?? record.RepositoryPath ?? version?.FileName;
        return IsPdfDocument(null, version?.FileName, path) ? "application/pdf" : "application/octet-stream";
    }

    private static bool IsPdfDocument(string? contentType, string? fileName, string? path)
        => contentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true
            || fileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true
            || path?.Contains(".pdf", StringComparison.OrdinalIgnoreCase) == true;

    private static string SafeDownloadFileName(string? fileName, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(fileName) ? $"{fallback}.pdf" : fileName.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar, '_');
        }

        return candidate;
    }

    private async Task NotifyExternalServiceRequestAsync(
        ExternalEstateRequestDefinition definition,
        Guid caseId,
        string referenceNumber,
        string? applicantName,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var sourceLabel = definition.Module == "Facilities"
            ? "Source: External Portal -> Estate / Facilities"
            : "Source: External Portal -> Estate";
        var roles = definition.Module == "Facilities"
            ? new[] { "Facilities Officer", "Facilities Supervisor", "Facilities Manager", "Estate Manager", "Estate Officer" }
            : new[] { "Estate Manager", "Estate Officer", "Land Registry Officer", "Records Officer" };

        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                GetUserId(),
                roles,
                "External Estate request submitted",
                $"{definition.Title} request {referenceNumber} was submitted by {applicantName ?? "an external customer"}.",
                "estate.external.request",
                "ProcedureCase",
                caseId,
                definition.Module == "Facilities"
                    ? $"/estate/facilities/{definition.EntityType}"
                    : $"/estate/{definition.EntityType}",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = sourceLabel,
                    ["sourceModule"] = definition.Module,
                    ["requestType"] = definition.Code,
                    ["entityType"] = definition.EntityType,
                    ["referenceNumber"] = referenceNumber
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block customer request submission.
        }
    }

    private async Task NotifyListingRequestAsync(
        EstateManagedAsset asset,
        Guid caseId,
        string referenceNumber,
        string requestType,
        string? applicantName,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                tenantId,
                GetUserId(),
                new[] { "Property Management Officer", "Property Manager", "Estate Manager", "Estate Officer" },
                "External property listing request submitted",
                $"{requestType} request {referenceNumber} was submitted for {asset.AssetCode} - {asset.Name} by {applicantName ?? "an external customer"}.",
                "estate.property.listing-request",
                "ProcedureCase",
                caseId,
                "/estate/property-management/EstatePropertyManagementListingApplication",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = "Source: External Portal -> Estate / Property Management",
                    ["sourceModule"] = "PropertyManagement",
                    ["assetId"] = asset.Id,
                    ["assetCode"] = asset.AssetCode,
                    ["listingType"] = asset.ExternalListingType,
                    ["requestType"] = requestType,
                    ["referenceNumber"] = referenceNumber
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block customer request submission.
        }
    }

    private static object ToExternalListingDto(EstateManagedAsset asset, bool usePublicImageRoute = false)
    {
        var image = asset.Documents
            .Where(document => document.IsListingImage)
            .OrderByDescending(document => document.IsPrimaryListingImage)
            .ThenByDescending(document => document.CreatedAt)
            .FirstOrDefault();

        return new
        {
            asset.Id,
            asset.AssetCode,
            asset.Name,
            asset.AssetType,
            asset.Status,
            asset.Description,
            asset.Location,
            asset.Region,
            asset.District,
            asset.Town,
            asset.BlockName,
            asset.FloorLabel,
            asset.UnitType,
            asset.AreaSquareMeters,
            asset.AreaValue,
            asset.AreaUnit,
            asset.ExternalListingType,
            asset.ExternalListingPrice,
            asset.ExternalSalePrice,
            asset.ExternalMonthlyRent,
            asset.ExternalGroundRentRequired,
            asset.ExternalPremiumChargeRequired,
            asset.ExternalPremiumChargeAmount,
            asset.ExternalLeaseTermMonths,
            asset.GroundRentPayable,
            asset.GroundRentRatePerAcre,
            asset.GroundRentComputed,
            asset.ExternalListingCurrency,
            asset.ExternalListingNotes,
            asset.ExternalPublishedAt,
            PrimaryImageDocumentId = image?.Id,
            PrimaryImageUrl = image is null
                ? null
                : usePublicImageRoute
                    ? $"/estate/public/listings/{asset.Id}/images/{image.Id}"
                    : $"/estate/external/listings/{asset.Id}/images/{image.Id}",
            SourceLabel = "Source: Estate / Property Management -> External Portal"
        };
    }

    private static decimal? ResolveDemarcationLeaseAmount(EstateLandDemarcation demarcation)
        => string.Equals(demarcation.ExternalListingType, "Lease", StringComparison.OrdinalIgnoreCase)
            && demarcation.ExternalMonthlyRent.HasValue
            && demarcation.ExternalListingPrice == demarcation.ExternalMonthlyRent
            && demarcation.TargetSalePrice is > 0m
                ? demarcation.TargetSalePrice
                : demarcation.ExternalListingPrice ?? (string.Equals(demarcation.ExternalListingType, "Lease", StringComparison.OrdinalIgnoreCase)
                    ? demarcation.TargetSalePrice
                    : null);

    private static object ToExternalListingDto(EstateLandDemarcation demarcation, bool usePublicImageRoute = false)
    {
        var asset = demarcation.EstateManagedAsset;
        var image = asset.Documents
            .Where(document => document.IsListingImage)
            .OrderByDescending(document => document.IsPrimaryListingImage)
            .ThenByDescending(document => document.CreatedAt)
            .FirstOrDefault();
        return new
        {
            Id = demarcation.Id,
            AssetCode = EstateLandDemarcationReference.DisplayReference(
                demarcation.ChildFixedAssetReference, asset.AssetCode, demarcation.DemarcationNumber),
            Name = EstateLandDemarcationReference.DisplayReference(
                demarcation.ChildFixedAssetReference, asset.AssetCode, demarcation.DemarcationNumber),
            asset.AssetType,
            asset.Status,
            Description = FirstNonBlank(demarcation.ExternalListingNotes, demarcation.Description, asset.Description),
            asset.Location,
            asset.Region,
            asset.District,
            asset.Town,
            asset.BlockName,
            asset.FloorLabel,
            UnitType = "Land portion",
            AreaSquareMeters = demarcation.AreaSquareFeet / 10.7639104167m,
            AreaValue = demarcation.AreaSquareFeet,
            AreaUnit = "square feet",
            demarcation.ExternalListingType,
            ExternalListingPrice = ResolveDemarcationLeaseAmount(demarcation),
            demarcation.ExternalSalePrice,
            demarcation.ExternalMonthlyRent,
            demarcation.ExternalGroundRentRequired,
            demarcation.ExternalPremiumChargeRequired,
            demarcation.ExternalPremiumChargeAmount,
            demarcation.ExternalLeaseTermMonths,
            demarcation.GroundRentPayable,
            demarcation.GroundRentRatePerAcre,
            demarcation.GroundRentComputed,
            demarcation.ExternalListingCurrency,
            demarcation.ExternalListingNotes,
            demarcation.ExternalPublishedAt,
            PrimaryImageDocumentId = image?.Id,
            PrimaryImageUrl = image is null
                ? null
                : usePublicImageRoute
                    ? $"/estate/public/listings/{demarcation.Id}/images/{image.Id}"
                    : $"/estate/external/listings/{demarcation.Id}/images/{image.Id}",
            SourceLabel = "Source: Estate Land Bank Demarcation -> External Portal"
        };
    }

    private async Task<ProcedureCase?> LoadOwnedExternalListingCaseAsync(
        Guid tenantId,
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var portalCustomerIds = await PortalCustomers(tenantId, userId)
            .Select(customer => customer.Id)
            .ToListAsync(cancellationToken);
        var portalCustomerReferences = portalCustomerIds
            .Select(customerId => customerId.ToString())
            .ToList();

        var procedureCase = await _db.ProcedureCases
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstOrDefaultAsync(item =>
                item.Id == requestId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.EntityType == "EstatePropertyManagementListingApplication",
                cancellationToken);

        return procedureCase is not null
            && IsOwnedExternalListingCase(procedureCase, userId, portalCustomerReferences)
                ? procedureCase
                : null;
    }

    private async Task<ProcedureCase?> LoadOwnedExternalEstateRequestAsync(
        Guid tenantId,
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var portalCustomerIds = await PortalCustomers(tenantId, userId)
            .Select(customer => customer.Id)
            .ToListAsync(cancellationToken);
        var portalCustomerReferences = portalCustomerIds
            .Select(customerId => customerId.ToString())
            .ToList();

        var procedureCase = await _db.ProcedureCases
            .AsNoTracking()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstOrDefaultAsync(item =>
                item.Id == requestId
                && item.TenantId == tenantId
                && !item.IsDeleted,
                cancellationToken);

        if (procedureCase is null)
        {
            return null;
        }

        var isDirectPortalRequest = procedureCase.OpenedById == userId
            && (procedureCase.SourceDepartment == "External Portal"
                || procedureCase.SourceDepartment == "External Portal - Estate Services"
                || procedureCase.SourceDepartment == "External Portal - Estate Listings");
        return isDirectPortalRequest
            || IsOwnedExternalListingCase(procedureCase, userId, portalCustomerReferences)
                ? procedureCase
                : null;
    }

    private static bool IsOwnedExternalListingCase(
        ProcedureCase procedureCase,
        Guid userId,
        IReadOnlyCollection<string> portalCustomerReferences)
    {
        if (procedureCase.EntityType != "EstatePropertyManagementListingApplication")
        {
            return false;
        }

        if (procedureCase.OpenedById == userId
            && procedureCase.SourceDepartment == "External Portal - Estate Listings")
        {
            return true;
        }

        return procedureCase.SourceDepartment == "Sales - Estate Enquiry"
            && procedureCase.Fields.Any(field => !field.IsDeleted
                && field.Key == "sourceReference"
                && field.Value is not null
                && portalCustomerReferences.Contains(field.Value));
    }

    private static object ToExternalRequestDto(ProcedureCase procedureCase)
        => new
        {
            procedureCase.Id,
            procedureCase.Module,
            procedureCase.EntityType,
            procedureCase.Title,
            procedureCase.ReferenceNumber,
            procedureCase.ApplicantName,
            procedureCase.SourceDepartment,
            procedureCase.Status,
            procedureCase.CurrentStageIndex,
            procedureCase.CurrentStageName,
            procedureCase.CurrentAssignedRole,
            CustomerIntakeUploadClosed = HasFirstInternalStageBeenRoutedForward(procedureCase),
            Documents = procedureCase.Documents
                .Where(document => !document.IsDeleted
                    && string.Equals(document.ProvidedBy, "Customer", StringComparison.OrdinalIgnoreCase))
                .OrderBy(document => document.CreatedAt)
                .Select(ToExternalDocumentDto)
                .ToList(),
            FieldValues = procedureCase.Fields
                .Where(field => !field.IsDeleted)
                .ToDictionary(
                    field => field.Key,
                    field => field.Value,
                    StringComparer.OrdinalIgnoreCase),
            procedureCase.CreatedAt,
            procedureCase.UpdatedAt
        };

    private static bool HasFirstInternalStageBeenRoutedForward(ProcedureCase procedureCase)
        => string.Equals(procedureCase.SourceDepartment, "External Portal - Estate Listings", StringComparison.OrdinalIgnoreCase)
            && string.Equals(procedureCase.EntityType, "EstatePropertyManagementListingApplication", StringComparison.OrdinalIgnoreCase)
            && procedureCase.CurrentStageIndex > 0;

    private static object ToExternalDocumentDto(ProcedureCaseDocument document)
        => new
        {
            document.Id,
            document.Name,
            document.RequiredFrom,
            document.ProvidedBy,
            document.IsMandatory,
            document.FileName
        };

    private sealed record ExternalInvoiceReceiptDto(
        Guid CustomerPaymentId,
        string PaymentNumber,
        DateTime PaymentDate,
        decimal Amount,
        string PaymentCurrencyCode,
        string PaymentMethod,
        string? TransactionReference,
        string Status);

    private static object ToExternalLegalTransferDto(ProcedureCase legalCase)
    {
        var fields = legalCase.Fields
            .Where(field => !field.IsDeleted)
            .ToDictionary(
                field => field.Key,
                field => field.Value,
                StringComparer.OrdinalIgnoreCase);
        var executedForm = legalCase.Documents.FirstOrDefault(document =>
            !document.IsDeleted
            && string.Equals(document.Name, "Executed transfer form", StringComparison.OrdinalIgnoreCase)
            && (!string.IsNullOrWhiteSpace(document.FileName) || !string.IsNullOrWhiteSpace(document.FileUrl)));
        var draftReference = fields.GetValueOrDefault("draftDocumentReference");
        var signedDocuments = legalCase.Documents
            .Where(document => IsCustomerReleasedLegalTransferDocument(legalCase, fields, document))
            .OrderBy(document => document.Name)
            .Select(document => new
            {
                document.Id,
                document.Name,
                document.FileName,
                document.UploadedAt
            })
            .ToList();

        return new
        {
            legalCase.Id,
            legalCase.Module,
            legalCase.EntityType,
            legalCase.Title,
            legalCase.ReferenceNumber,
            legalCase.Status,
            legalCase.CurrentStageName,
            legalCase.CurrentAssignedRole,
            FieldValues = fields,
            SourceProcedureCaseId = fields.GetValueOrDefault("sourceProcedureCaseId"),
            SourceRecordReference = fields.GetValueOrDefault("sourceRecordReference"),
            PropertyNumber = fields.GetValueOrDefault("propertyNumber"),
            TransferFeePayable = fields.GetValueOrDefault("transferFeePayable"),
            PaymentStatus = fields.GetValueOrDefault("paymentStatus"),
            DraftDocumentReference = draftReference,
            ExecutedTransferFormFileName = executedForm?.FileName,
            SignedDocuments = signedDocuments,
            CanDownloadDraft = !string.IsNullOrWhiteSpace(draftReference),
            CanUploadExecutedTransferForm = string.Equals(legalCase.CurrentStageName, "Client Execution", StringComparison.OrdinalIgnoreCase)
                && executedForm is null,
            legalCase.CreatedAt,
            legalCase.UpdatedAt
        };
    }

    private static bool IsCustomerReleasedLegalTransferDocument(
        ProcedureCase legalCase,
        IReadOnlyDictionary<string, string?> fields,
        ProcedureCaseDocument document)
    {
        if (!string.Equals(legalCase.EntityType, "LegalTransfer", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(document.Name, "Executed transfer form", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(document.FileUrl))
        {
            return false;
        }

        var signatureStatus = fields.GetValueOrDefault("signatureStatus") ?? string.Empty;
        return string.Equals(legalCase.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            || signatureStatus.Contains("Head of Legal signed", StringComparison.OrdinalIgnoreCase)
            || signatureStatus.Contains("Fully signed", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ProcedureCase?> LoadOwnedExternalLegalTransferCaseAsync(
        Guid tenantId,
        Guid userId,
        Guid legalCaseId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = _db.ProcedureCases
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .Where(item => item.Id == legalCaseId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.Module == "Legal"
                && item.EntityType == "LegalTransfer");

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        var legalCase = await query.FirstOrDefaultAsync(cancellationToken);
        if (legalCase is null)
        {
            return null;
        }

        var sourceProcedureCaseId = legalCase.Fields.FirstOrDefault(field =>
            string.Equals(field.Key, "sourceProcedureCaseId", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!Guid.TryParse(sourceProcedureCaseId, out var sourceCaseId))
        {
            return null;
        }

        var ownsSourceCase = await _db.ProcedureCases
            .AsNoTracking()
            .AnyAsync(item => item.Id == sourceCaseId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.OpenedById == userId
                && item.SourceDepartment == "External Portal - Estate Listings"
                && item.EntityType == "EstatePropertyManagementListingApplication", cancellationToken);

        return ownsSourceCase ? legalCase : null;
    }

    private async Task<FileResult> DownloadCentralDocumentRecordAsPdfAsync(
        CentralDocumentRecord record,
        CancellationToken cancellationToken)
    {
        var version = record.Versions
            .OrderByDescending(item => item.PublishedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.RepositoryPath)
                || !string.IsNullOrWhiteSpace(item.RenditionPath));
        var pdfFileName = $"{Path.GetFileNameWithoutExtension(
            SafeDownloadFileName(version?.FileName, record.DocumentReference))}.pdf";

        if (!string.IsNullOrWhiteSpace(version?.RenditionPath))
        {
            try
            {
                var renditionStream = await _fileStorageService.DownloadFileAsync(
                    version.RenditionPath,
                    version.FileUploadRecordId ?? record.Id);
                return File(renditionStream, "application/pdf", pdfFileName);
            }
            catch (FileNotFoundException)
            {
                // Fall back to the source document and rebuild the PDF below.
            }
        }

        var filePath = FirstNonBlank(version?.RepositoryPath, record.RepositoryPath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new FileNotFoundException("The document file is not available for download.");
        }

        var stream = await _fileStorageService.DownloadFileAsync(filePath, version?.FileUploadRecordId ?? record.Id);
        if (IsPdfDocument(version?.ContentType, version?.FileName, filePath))
        {
            return File(stream, "application/pdf", pdfFileName);
        }

        await using (stream)
        {
            var preview = await _renditionService.CreatePdfPreviewAsync(
                new CentralDocumentPdfPreviewRequest(
                    stream,
                    version?.FileName ?? $"{record.DocumentReference}.docx",
                    version?.ContentType ?? "application/octet-stream"),
                cancellationToken);

            if (!preview.Success || preview.PdfStream is null)
            {
                throw new InvalidOperationException(preview.ErrorMessage ?? "The document could not be converted to PDF.");
            }

            return File(preview.PdfStream, "application/pdf", pdfFileName);
        }
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

    private static string? FieldValue(
        IReadOnlyDictionary<string, ProcedureCaseField> fields,
        string key)
        => fields.TryGetValue(key, out var field)
            ? field.Value
            : null;

    private static bool IsApprovedDecisionStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Approved ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegalAgreementReleaseApproved(
        IReadOnlyDictionary<string, ProcedureCaseField> fields)
    {
        var reviewStatus = FieldValue(fields, "legalAgreementReviewStatus");
        if (!string.IsNullOrWhiteSpace(reviewStatus)
            && ((reviewStatus.Contains("head of legal", StringComparison.OrdinalIgnoreCase)
                    && reviewStatus.Contains("signed", StringComparison.OrdinalIgnoreCase))
                || (reviewStatus.Contains("approved by legal", StringComparison.OrdinalIgnoreCase)
                    && reviewStatus.Contains("customer signature", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        // Agreements completed before the Legal handoff was introduced remain accessible.
        return string.Equals(
            FieldValue(fields, "agreementExecutionStatus"),
            "Fully executed",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidIsoDate(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    private static bool IsRentalPropertyRequest(
        IReadOnlyDictionary<string, ProcedureCaseField> fields,
        string? title)
    {
        var requestType = FieldValue(fields, "requestType") ?? string.Empty;
        var requestTitle = title ?? string.Empty;
        return !requestType.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            && !requestType.Contains("sale", StringComparison.OrdinalIgnoreCase)
            && !requestTitle.StartsWith("Purchase bid", StringComparison.OrdinalIgnoreCase)
            && !requestTitle.StartsWith("Sale request", StringComparison.OrdinalIgnoreCase);
    }

    private async Task MarkCompetingExternalListingRequestsUnavailableAsync(
        ProcedureCase acceptedCase,
        IReadOnlyDictionary<string, ProcedureCaseField> acceptedFields,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var listingReference = Normalize(FieldValue(acceptedFields, "listingReference"));
        var propertyUnit = Normalize(FieldValue(acceptedFields, "propertyUnit"));
        if (listingReference is null && propertyUnit is null)
        {
            return;
        }

        var acceptedRequestType = FirstNonBlank(
            FieldValue(acceptedFields, "requestType"),
            FieldValue(acceptedFields, "listingType"),
            acceptedCase.Title);
        var isPurchase = acceptedRequestType?.Contains("purchase", StringComparison.OrdinalIgnoreCase) == true
            || acceptedRequestType?.Contains("sale", StringComparison.OrdinalIgnoreCase) == true;
        var outcomeLabel = isPurchase ? "Sold out" : "Rented out";
        var outcomeStatus = $"{outcomeLabel} - another customer accepted";
        var outcomeMessage = isPurchase
            ? "This listing has been sold because another customer accepted the approved purchase request."
            : "This listing has been rented out because another customer accepted the approved rental request.";

        var competitors = await _db.ProcedureCases
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Where(item => item.TenantId == tenantId
                && item.Id != acceptedCase.Id
                && !item.IsDeleted
                && item.SourceDepartment == "External Portal - Estate Listings"
                && item.EntityType == "EstatePropertyManagementListingApplication"
                && item.Status != "Completed"
                && item.Status != "Cancelled"
                && item.Status != "Canceled"
                && ((listingReference != null && item.Fields.Any(field =>
                        !field.IsDeleted
                        && field.Key == "listingReference"
                        && field.Value != null
                        && field.Value.Trim().ToLower() == listingReference))
                    || (propertyUnit != null && item.Fields.Any(field =>
                        !field.IsDeleted
                        && field.Key == "propertyUnit"
                        && field.Value != null
                        && field.Value.Trim().ToLower() == propertyUnit))))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var competitor in competitors)
        {
            var competitorFields = competitor.Fields
                .Where(field => !field.IsDeleted)
                .ToDictionary(field => field.Key, field => field, StringComparer.OrdinalIgnoreCase);

            competitor.Status = "Archived";
            competitor.CurrentStageName = "Listing unavailable";
            competitor.CurrentStageOwner = null;
            competitor.CurrentAssignedRole = null;
            competitor.LastActionById = userId;
            competitor.UpdatedAt = now;
            competitor.LastModifiedById = userId;

            UpsertField(competitor, competitorFields, "applicationStatus", "Request status", "select", outcomeStatus);
            UpsertField(competitor, competitorFields, "customerNotificationStatus", "Customer notification status", "select", outcomeStatus);
            UpsertField(competitor, competitorFields, "listingOutcomeMessage", "Listing outcome", "textarea", outcomeMessage);
            AddExternalCaseActivity(competitor, userId, outcomeLabel, outcomeMessage);
        }
    }

    private async Task NotifyListingCustomerActionAsync(
        ProcedureCase procedureCase,
        string title,
        string message,
        string notificationType,
        CancellationToken cancellationToken)
    {
        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                procedureCase.TenantId,
                GetUserId(),
                new[] { "Property Management Officer", "Property Manager", "Estate Manager", "Estate Officer" },
                title,
                message,
                notificationType,
                "ProcedureCase",
                procedureCase.Id,
                $"/estate/property-management/EstatePropertyManagementListingApplication?caseId={procedureCase.Id}",
                new Dictionary<string, object>
                {
                    ["sourceModule"] = "PropertyManagement",
                    ["entityType"] = procedureCase.EntityType,
                    ["referenceNumber"] = procedureCase.ReferenceNumber ?? string.Empty
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block a customer portal action.
        }
    }

    private async Task NotifyLegalTransferClientExecutionReceivedAsync(
        ProcedureCase legalCase,
        CancellationToken cancellationToken)
    {
        try
        {
            var legalReference = legalCase.ReferenceNumber ?? legalCase.Title;
            var assignedRole = FirstNonBlank(legalCase.CurrentAssignedRole, "Legal Admin Assistant");
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                legalCase.TenantId,
                GetUserId(),
                assignedRole is null ? ["Legal Admin Assistant"] : [assignedRole],
                "Client signed transfer form received",
                $"{legalReference}: the client uploaded the signed transfer form. Review it and route the Legal transfer forward.",
                "legal.transfer.client-executed-form-uploaded",
                "ProcedureCase",
                legalCase.Id,
                $"/legal/LegalTransfer?caseId={legalCase.Id}",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = "Source: External Portal -> Legal",
                    ["sourceModule"] = "Legal",
                    ["sourceEntityType"] = legalCase.EntityType,
                    ["referenceNumber"] = legalReference ?? string.Empty,
                    ["currentStage"] = legalCase.CurrentStageName ?? string.Empty,
                    ["assignedRole"] = assignedRole ?? string.Empty
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block the customer upload.
        }
    }

    private void UpsertField(
        ProcedureCase procedureCase,
        IDictionary<string, ProcedureCaseField> fields,
        string key,
        string label,
        string fieldType,
        string? value)
    {
        if (fields.TryGetValue(key, out var field))
        {
            field.Value = value;
            field.UpdatedAt = DateTime.UtcNow;
            field.LastModifiedById = procedureCase.LastActionById;
            return;
        }

        var created = new ProcedureCaseField
        {
            TenantId = procedureCase.TenantId,
            ProcedureCaseId = procedureCase.Id,
            Key = key,
            Label = label,
            FieldType = fieldType,
            Value = value,
            CreatedAt = DateTime.UtcNow,
            CreatedById = procedureCase.LastActionById
        };
        _db.ProcedureCaseFields.Add(created);
        fields[key] = created;
    }

    private void AddExternalCaseActivity(
        ProcedureCase procedureCase,
        Guid userId,
        string action,
        string? details)
    {
        var now = DateTime.UtcNow;
        _db.ProcedureCaseActivities.Add(new ProcedureCaseActivity
        {
            TenantId = procedureCase.TenantId,
            ProcedureCaseId = procedureCase.Id,
            Action = action,
            StageName = procedureCase.CurrentStageName,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            PerformedById = userId,
            PerformedAt = now,
            CreatedAt = now,
            CreatedById = userId
        });
    }

    private IQueryable<BusinessPartner> PortalCustomers(Guid tenantId, Guid userId)
        => _db.BusinessPartners
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsActive
                && item.ApprovalStatus == "Approved"
                && item.CustomerAccountNumber != null
                && BusinessPartnerRoles.CustomerTypes.Contains(item.PartnerType)
                && (item.UserId == userId
                    || _db.BusinessPartnerUsers.Any(link => link.TenantId == tenantId
                        && !link.IsDeleted
                        && link.IsActive
                        && link.UserId == userId
                        && link.BusinessPartnerId == item.Id)));

    private Task<BusinessPartner?> FindPortalCustomerAsync(
        Guid tenantId,
        Guid userId,
        Guid businessPartnerId,
        CancellationToken cancellationToken)
        => PortalCustomers(tenantId, userId)
            .Where(item => item.Id == businessPartnerId)
            .FirstOrDefaultAsync(cancellationToken);

    private static IQueryable<EstateManagedAsset> ApplyPublicPriceFilter(
        IQueryable<EstateManagedAsset> query,
        string? listingType,
        decimal? minPrice,
        decimal? maxPrice)
    {
        if (!minPrice.HasValue && !maxPrice.HasValue)
        {
            return query;
        }

        if (listingType is "Rent" or "Lease")
        {
            return query.Where(asset =>
                (asset.ExternalMonthlyRent ?? asset.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (asset.ExternalMonthlyRent ?? asset.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (asset.ExternalMonthlyRent ?? asset.ExternalListingPrice) <= maxPrice.Value));
        }

        if (listingType == "Sale")
        {
            return query.Where(asset =>
                (asset.ExternalSalePrice ?? asset.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (asset.ExternalSalePrice ?? asset.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (asset.ExternalSalePrice ?? asset.ExternalListingPrice) <= maxPrice.Value));
        }

        return query.Where(asset =>
            ((asset.ExternalSalePrice ?? asset.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (asset.ExternalSalePrice ?? asset.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (asset.ExternalSalePrice ?? asset.ExternalListingPrice) <= maxPrice.Value))
            || ((asset.ExternalMonthlyRent ?? asset.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (asset.ExternalMonthlyRent ?? asset.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (asset.ExternalMonthlyRent ?? asset.ExternalListingPrice) <= maxPrice.Value)));
    }

    private static IQueryable<EstateLandDemarcation> ApplyPublicPriceFilter(
        IQueryable<EstateLandDemarcation> query,
        string? listingType,
        decimal? minPrice,
        decimal? maxPrice)
    {
        if (!minPrice.HasValue && !maxPrice.HasValue)
        {
            return query;
        }

        if (listingType is "Rent" or "Lease")
        {
            return query.Where(item =>
                (item.ExternalMonthlyRent ?? item.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (item.ExternalMonthlyRent ?? item.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (item.ExternalMonthlyRent ?? item.ExternalListingPrice) <= maxPrice.Value));
        }

        if (listingType == "Sale")
        {
            return query.Where(item =>
                (item.ExternalSalePrice ?? item.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (item.ExternalSalePrice ?? item.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (item.ExternalSalePrice ?? item.ExternalListingPrice) <= maxPrice.Value));
        }

        return query.Where(item =>
            ((item.ExternalSalePrice ?? item.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (item.ExternalSalePrice ?? item.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (item.ExternalSalePrice ?? item.ExternalListingPrice) <= maxPrice.Value))
            || ((item.ExternalMonthlyRent ?? item.ExternalListingPrice).HasValue
                && (!minPrice.HasValue ||
                    (item.ExternalMonthlyRent ?? item.ExternalListingPrice) >= minPrice.Value)
                && (!maxPrice.HasValue ||
                    (item.ExternalMonthlyRent ?? item.ExternalListingPrice) <= maxPrice.Value)));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeListingType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "sale" => "Sale",
            "rent" => "Rent",
            "lease" => "Lease",
            "saleandrent" or "sale and rent" or "sale/rent" or "both" => "SaleAndRent",
            "saleandlease" or "sale and lease" or "sale/lease" => "SaleAndLease",
            _ => null
        };
    }

    private static string NormalizeListingRequestType(string? requestedType, string listingType)
    {
        var normalized = NormalizeListingType(requestedType);
        if (normalized is null)
        {
            normalized = listingType switch
            {
                "SaleAndRent" => "Rent",
                "SaleAndLease" => "Lease",
                _ => listingType
            };
        }

        if (listingType != "SaleAndRent" && listingType != "SaleAndLease" && normalized != listingType)
        {
            normalized = listingType;
        }

        if (listingType == "SaleAndRent" && normalized is not ("Sale" or "Rent"))
        {
            normalized = "Rent";
        }
        if (listingType == "SaleAndLease" && normalized is not ("Sale" or "Lease"))
        {
            normalized = "Lease";
        }

        return normalized == "Sale"
            ? "Purchase"
            : normalized == "Rent"
                ? "Rent"
                : "Lease";
    }

    private static string FormatRecurringTerm(int termMonths)
        => termMonths > 0 && termMonths % 12 == 0
            ? $"{termMonths / 12} years"
            : $"{termMonths} months";

    private static IQueryable<EstateManagedAsset> WhereExternallyAvailableListings(IQueryable<EstateManagedAsset> query)
        // Estate external portal: keep GET listings and POST requests on the same availability gate so stale listing IDs cannot start procedures.
        => query.Where(asset => !asset.IsDeleted
            && asset.IsPublishedToExternalPortal
            && asset.ExternalListingStatus == "Published"
            && (asset.Status == EstateManagedAssetStatus.Available || asset.Status == EstateManagedAssetStatus.LandBank)
            && !asset.CustomerBusinessPartnerId.HasValue
            && string.IsNullOrEmpty(asset.LesseeName)
            && (asset.ExternalListingType == "Sale"
                || ((asset.ExternalListingType == "Rent"
                        || asset.ExternalListingType == "Lease"
                        || asset.ExternalListingType == "SaleAndRent"
                        || asset.ExternalListingType == "SaleAndLease")
                    && (asset.ExternalListingType == "Lease" || asset.ExternalListingType == "SaleAndLease"
                        ? (asset.ExternalListingPrice ?? asset.ExternalMonthlyRent) > 0
                        : asset.ExternalMonthlyRent > 0)
                    && (asset.AssetType != EstateManagedAssetType.Land
                        || asset.ExternalGroundRentRequired != true
                        || asset.GroundRentPayable > 0)))
            && ((asset.ExternalListingType == "Sale" && asset.IsAvailableForSale)
                || (asset.ExternalListingType == "Rent" && asset.IsAvailableForLease)
                || (asset.ExternalListingType == "Lease" && asset.IsAvailableForLease)
                || (asset.ExternalListingType == "SaleAndRent"
                    && (asset.IsAvailableForSale || asset.IsAvailableForLease))
                || (asset.ExternalListingType == "SaleAndLease"
                    && (asset.IsAvailableForSale || asset.IsAvailableForLease)))
            && ((asset.AssetType == EstateManagedAssetType.Land
                    && asset.Status == EstateManagedAssetStatus.LandBank
                    && !asset.ProjectId.HasValue
                    && !asset.IsReadyForProjectManagement
                    && asset.BoundaryVerified
                    && asset.Demarcations.Any(item => !item.IsDeleted)
                    && !asset.Demarcations.Any(item => !item.IsDeleted && !item.BoundaryVerified))
                || ((asset.AssetType == EstateManagedAssetType.Property
                        || asset.AssetType == EstateManagedAssetType.Facility)
                    && (asset.SourceType == EstateManagedAssetSourceType.Imported
                        || (asset.SourceType == EstateManagedAssetSourceType.ProjectUnit
                            && asset.IsPublishedFromProject))
                    && asset.Status == EstateManagedAssetStatus.Available)));

    private async Task<Guid> ResolvePublicTenantIdAsync(CancellationToken cancellationToken)
    {
        if (_currentUserService.TenantId is { } currentTenantId && currentTenantId != Guid.Empty)
        {
            return currentTenantId;
        }

        var host = HttpContext.Request.Host.Host;
        if (!string.IsNullOrWhiteSpace(host))
        {
            var normalizedHost = host.Trim().ToLowerInvariant();
            var hostTenantId = await _db.Tenants
                .AsNoTracking()
                .Where(tenant => !tenant.IsDeleted && tenant.Status == TenantStatus.Active)
                .Where(tenant => tenant.Domain != null
                    && tenant.Domain.Trim().ToLower() == normalizedHost)
                .Select(tenant => tenant.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (hostTenantId != Guid.Empty)
            {
                return hostTenantId;
            }
        }

        var defaultPublicTenantId = await _db.Tenants
            .AsNoTracking()
            .Where(tenant => !tenant.IsDeleted
                && tenant.Status == TenantStatus.Active
                && tenant.IsDefaultForPublicUsers)
            .OrderBy(tenant => tenant.DefaultPriority)
            .Select(tenant => tenant.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (defaultPublicTenantId != Guid.Empty)
        {
            return defaultPublicTenantId;
        }

        return Guid.Empty;
    }

    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static CentralDocumentVersion? ResolveCurrentDocumentVersion(CentralDocumentRecord record)
        => record.Versions
            .Where(version => !version.IsDeleted)
            .OrderByDescending(version =>
                string.Equals(version.VersionNumber, record.CurrentVersion, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(version =>
                string.Equals(version.Status, "Current", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(version => version.PublishedAt ?? version.CreatedAt)
            .FirstOrDefault();

    private static string BuildExternalReference(string prefix)
        => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}".ToUpperInvariant();

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static IDictionary<string, string?> BuildFieldValues(
        ExternalEstateRequestDefinition definition,
        CreateExternalEstateServiceRequest request,
        string reference,
        string? contact)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["referenceNumber"] = reference,
            ["applicantName"] = request.ApplicantName,
            ["propertyNumber"] = request.PropertyReference,
            ["housePlotShopNumber"] = request.PropertyReference,
            ["propertyUnit"] = request.PropertyReference,
            ["location"] = request.Location,
            ["locationDetail"] = request.Location,
            ["sourceLabel"] = definition.Module == "Facilities" ? "Source: External Portal -> Estate / Facilities" : "Source: External Portal -> Estate",
            ["sourceSystem"] = "External Portal",
            ["sourceWorkspace"] = "Estate Services",
            ["contactReference"] = contact,
            ["notes"] = request.Description,
            ["issueDescription"] = request.Description,
            ["complaintDescription"] = request.Description,
            ["serviceImpact"] = request.ServiceImpact,
            ["reportedPriority"] = definition.EntityType == "EstateFacilityMaintenance" ? null : request.Priority,
            ["customerReportedUrgency"] = definition.EntityType == "EstateFacilityMaintenance" ? null : request.Priority,
            ["requester"] = request.ApplicantName,
            ["requesterType"] = "Tenant / occupant",
            ["complainantName"] = request.ApplicantName,
            ["complainantType"] = "Client",
            ["issueType"] = request.Category,
            ["complaintCategory"] = request.Category,
            ["schedule"] = ResolveEstateSchedule(definition),
            ["procedureType"] = definition.Title
        };

        if (string.Equals(definition.Code, "hosConversion", StringComparison.OrdinalIgnoreCase))
        {
            values["housingRequestType"] = "Conversion to HOS";
        }

        if (string.Equals(definition.Code, "tenancyRecognition", StringComparison.OrdinalIgnoreCase))
        {
            values["housingRequestType"] = "Recognition of tenancy";
        }

        foreach (var item in request.AdditionalValues ?? new Dictionary<string, string?>())
        {
            if (!string.IsNullOrWhiteSpace(item.Key)
                && !(definition.EntityType == "EstateFacilityMaintenance"
                    && new[] {
                        "priority", "reportedPriority", "customerReportedUrgency", "maintenanceTypeId", "handoffDescription",
                        "estimatedHours", "estimatedCost", "serviceProviderBusinessPartnerId", "serviceProviderContractId",
                        "propertyUnit", "propertyNumber", "estateManagedAssetId", "issueDescription"
                    }
                        .Contains(item.Key.Trim(), StringComparer.OrdinalIgnoreCase)))
            {
                values[item.Key.Trim()] = item.Value;
            }
        }

        return values;
    }

    private static string ResolveEstateSchedule(ExternalEstateRequestDefinition definition)
        => definition.EntityType switch
        {
            "EstateFacilityMaintenance" or "EstateFacilityComplaint" => "Facilities",
            "EstateSearchApplication" or "EstateCertifiedTrueCopy" or "EstateRecordAmendment" => "Records",
            "EstateServicedPlotAllocation" => "Serviced Plots",
            "EstateLandsPartiallyServiced" or "EstateAdditionalLand" or "EstateChangeOfUse" or "EstateTransfer" or "EstateAssignment" or "EstateMortgageConsent" or "EstateLeasePreparation" or "EstateLeaseRenewal" => "Lands / Partially Serviced",
            "EstateHousingHomeOwnership" => "Housing",
            "EstateTraditionalLands" => "Traditional Lands",
            "EstateTenancyRegularisation" => "Regularisation",
            _ => definition.Category
        };

    private static readonly ExternalEstateRequestDefinition[] ExternalRequestDefinitions =
    [
        new("maintenance", "Maintenance Request", "Facilities", "EstateFacilityMaintenance", "Maintenance"),
        new("complaint", "Facilities Complaint", "Facilities", "EstateFacilityComplaint", "Complaint"),
        new("searchApplication", "Search Application", "Estate", "EstateSearchApplication", "Estate records"),
        new("changeAddress", "Change of Address", "Estate", "EstateRecordAmendment", "Estate records"),
        new("certifiedTrueCopy", "Certified True Copy", "Estate", "EstateCertifiedTrueCopy", "Estate records"),
        new("jointOwnership", "Joint Ownership / Addition of Name", "Estate", "EstateJointOwnership", "Ownership"),
        new("transfer", "Transfer / Portion Transfer", "Estate", "EstateTransfer", "Transfer"),
        new("assignment", "Assignment Consent", "Estate", "EstateAssignment", "Assignment"),
        new("mortgageConsent", "Consent to Mortgage / Mortgage in Principle", "Estate", "EstateMortgageConsent", "Mortgage"),
        new("leaseDocument", "Lease Document Preparation", "Estate", "EstateLeasePreparation", "Lease"),
        new("additionalLand", "Additional Land Application", "Estate", "EstateAdditionalLand", "Allocation"),
        new("changeOfUse", "Change of Land Use", "Estate", "EstateChangeOfUse", "Land use"),
        new("leaseRenewal", "Lease Renewal", "Estate", "EstateLeaseRenewal", "Lease"),
        new("landApplication", "Land / Partially Serviced Plot Application", "Estate", "EstateLandsPartiallyServiced", "Allocation"),
        new("traditionalLand", "Traditional Land Documentation", "Estate", "EstateTraditionalLands", "Traditional Lands"),
        new("tenancyRecognition", "Recognition of Tenancy", "Estate", "EstateHousingHomeOwnership", "Housing"),
        new("hosConversion", "Conversion of Rental Unit to HOS", "Estate", "EstateHousingHomeOwnership", "Housing"),
        new("regularisation", "Tenancy Regularisation", "Estate", "EstateTenancyRegularisation", "Regularisation"),
        new("rightOfEntry", "Right of Entry / Allocation Follow-up", "Estate", "EstateServicedPlotAllocation", "Allocation")
    ];
}

public sealed record CreateExternalEstateServiceRequest(
    string RequestType,
    string? ApplicantName,
    string? Contact,
    string? PropertyReference,
    string? Location,
    string? Category,
    string? Priority,
    string? ServiceImpact,
    DateTime? TargetDate,
    string? Description,
    IDictionary<string, string?>? AdditionalValues);

public sealed record CreateExternalListingRequest(
    string? RequestType,
    Guid? BusinessPartnerId,
    string? ApplicantName,
    string? Contact,
    decimal? OfferAmount,
    string? Message);

public sealed record ExternalPropertyRequestDecision(
    string? Decision,
    string? Notes);

public sealed record ExternalPropertyClarificationResponse(string? Response);

public sealed record ExternalEstateRequestDefinition(
    string Code,
    string Title,
    string Module,
    string EntityType,
    string Category);

public sealed record CreatePropertyListingEnquiryRequest(
    Guid SubmissionId,
    string Message,
    Guid? BusinessPartnerId = null,
    string? CaptchaToken = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? ContactReference = null);
