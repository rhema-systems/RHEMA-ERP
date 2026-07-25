using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/facilities/billing-documents")]
[Authorize]
public sealed class FacilitiesBillingDocumentsController : ControllerBase
{
    private const string SourceModule = "Estate / Facilities";
    private const string SourceLabel = "Source: Estate / Facilities -> Finance AR -> Central DMS";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public FacilitiesBillingDocumentsController(
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    [HttpGet("publication-rules")]
    public IActionResult GetPublicationRules()
    {
        return Ok(new
        {
            success = true,
            data = new
            {
                sourceModule = SourceModule,
                sourceLabel = SourceLabel,
                owner = "Estate / Facilities prepares billing instructions; Finance AR owns accounting; Central DMS owns documents.",
                documentTypes = new[]
                {
                    "Invoice",
                    "Receipt",
                    "Statement",
                    "Demand notice",
                    "Adjustment",
                    "Deposit evidence",
                    "Dispute evidence",
                    "Approval note"
                },
                requiredReferences = new[]
                {
                    "Facilities billing reference",
                    "Finance AR invoice / receipt / statement reference",
                    "Property, unit, occupant, or customer account reference"
                }
            }
        });
    }

    [HttpPost("publish-to-dms")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager,Finance Officer")]
    public async Task<IActionResult> PublishToCentralDms([FromBody] PublishFacilitiesBillingDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentTitle))
        {
            return BadRequest(new { success = false, message = "Document title is required." });
        }

        var tenantId = GetTenantId();
        var documentType = TrimOrDefault(request.DocumentType, "Billing document");
        var sourceRecordReference = BuildSourceRecordReference(request);
        var financeReference = TrimToNull(request.FinanceArReference);

        var existingRecord = await _db.CentralDocumentRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.SourceModule == SourceModule
                && item.SourceEntityType == "EstateFacilityBillingDocument"
                && item.SourceRecordReference == sourceRecordReference
                && item.MetadataTemplateCode == BuildTemplateCode(documentType)
                && !item.IsDeleted, cancellationToken);

        if (existingRecord is not null)
        {
            return Ok(new
            {
                success = true,
                data = ToCentralDmsPublicationDto(existingRecord),
                message = "Facilities billing document is already published to Central DMS."
            });
        }

        var now = DateTime.UtcNow;
        var template = await ResolveMetadataTemplateAsync(tenantId, documentType, cancellationToken);
        var record = new CentralDocumentRecord
        {
            TenantId = tenantId,
            DocumentReference = await NextDocumentReferenceAsync(tenantId, cancellationToken),
            Title = request.DocumentTitle.Trim(),
            SourceModule = SourceModule,
            SourceLabel = SourceLabel,
            SourceEntityType = "EstateFacilityBillingDocument",
            SourceRecordReference = Truncate(sourceRecordReference, 180),
            MetadataTemplateCode = template?.TemplateCode ?? BuildTemplateCode(documentType),
            RepositoryStatus = HasRepositoryReference(request) ? "Linked" : "Not linked",
            RepositoryPath = TrimToNull(request.RepositoryPath),
            ExternalDocumentUrl = TrimToNull(request.ExternalDocumentUrl),
            CurrentVersion = "v1.0",
            VersionStatus = "Published",
            AnnotationStatus = IsPdf(request) ? "PDF preview ready" : "Not required",
            CommentStatus = "Open for comments",
            AccessProfile = string.IsNullOrWhiteSpace(template?.AccessProfile) ? "Finance sensitive" : template.AccessProfile,
            RetentionStatus = "Current",
            LifecycleStatus = "Active",
            PublishedAt = now,
            PublishedById = GetUserId(),
            Notes = BuildNotes(request),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        var version = new CentralDocumentVersion
        {
            TenantId = tenantId,
            DocumentRecordId = record.Id,
            VersionNumber = "v1.0",
            Status = "Published",
            RepositoryPath = record.RepositoryPath,
            FileName = TrimToNull(request.FileName),
            ContentType = TrimToNull(request.ContentType),
            FileSize = request.FileSize,
            PublishedAt = now,
            PublishedById = GetUserId(),
            ChangeSummary = "Initial Estate / Facilities billing document publication into Central DMS.",
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        _db.CentralDocumentRecords.Add(record);
        _db.CentralDocumentVersions.Add(version);
        AddMetadataValue(record, "billingDocumentKind", "Billing document kind", request.BillingDocumentKind ?? documentType, now);
        AddMetadataValue(record, "documentType", "Document type", documentType, now);
        AddMetadataValue(record, "facilitiesBillingReference", "Facilities billing reference", request.FacilitiesBillingReference, now);
        AddMetadataValue(record, "financeArReference", "Finance AR reference", financeReference, now);
        AddMetadataValue(record, "propertyUnit", "Property / unit", request.PropertyUnit, now);
        AddMetadataValue(record, "customerAccountReference", "Customer account reference", request.CustomerAccountReference, now);
        AddMetadataValue(record, "sourceLabel", "Source label", SourceLabel, now);

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = ToCentralDmsPublicationDto(record),
            message = "Facilities billing document published to Central DMS."
        });
    }

    private Guid GetTenantId()
        => _currentUserService.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private async Task<string> NextDocumentReferenceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return $"DMS-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
    }

    private async Task<CentralDocumentMetadataTemplate?> ResolveMetadataTemplateAsync(
        Guid tenantId,
        string documentType,
        CancellationToken cancellationToken)
        => await _db.CentralDocumentMetadataTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.IsActive
                && !item.IsDeleted
                && (item.Module == "Estate / Facilities"
                    || item.Module == "Finance"
                    || item.Module == "Finance AR"))
            .OrderByDescending(item => item.DocumentType == documentType)
            .ThenBy(item => item.TemplateCode)
            .FirstOrDefaultAsync(cancellationToken);

    private void AddMetadataValue(
        CentralDocumentRecord record,
        string fieldKey,
        string fieldLabel,
        string? fieldValue,
        DateTime capturedAt)
    {
        if (string.IsNullOrWhiteSpace(fieldValue))
        {
            return;
        }

        _db.CentralDocumentMetadataValues.Add(new CentralDocumentMetadataValue
        {
            TenantId = record.TenantId,
            DocumentRecordId = record.Id,
            TemplateCode = record.MetadataTemplateCode,
            FieldKey = fieldKey,
            FieldLabel = fieldLabel,
            FieldValue = fieldValue.Trim(),
            ValueType = "text",
            Source = "Estate / Facilities billing publication",
            CapturedAt = capturedAt,
            CapturedById = GetUserId(),
            CreatedAt = capturedAt,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        });
    }

    private static string BuildSourceRecordReference(PublishFacilitiesBillingDocumentRequest request)
    {
        var parts = new[]
        {
            TrimToNull(request.FacilitiesBillingReference),
            TrimToNull(request.FinanceArReference),
            TrimToNull(request.PropertyUnit),
            TrimToNull(request.CustomerAccountReference),
            TrimToNull(request.DocumentTitle)
        };

        return Truncate(string.Join(" / ", parts.Where(item => !string.IsNullOrWhiteSpace(item))), 180);
    }

    private static string BuildNotes(PublishFacilitiesBillingDocumentRequest request)
    {
        var notes = new List<string>
        {
            "Published from Estate / Facilities billing operations.",
            "Finance AR remains the accounting system of record."
        };

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            notes.Add(request.Notes.Trim());
        }

        return Truncate(string.Join(" ", notes), 1000);
    }

    private static bool HasRepositoryReference(PublishFacilitiesBillingDocumentRequest request)
        => !string.IsNullOrWhiteSpace(request.RepositoryPath)
            || !string.IsNullOrWhiteSpace(request.ExternalDocumentUrl)
            || !string.IsNullOrWhiteSpace(request.FileName);

    private static bool IsPdf(PublishFacilitiesBillingDocumentRequest request)
        => string.Equals(request.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            || (request.FileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false)
            || (request.RepositoryPath?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false)
            || (request.ExternalDocumentUrl?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string BuildTemplateCode(string documentType)
    {
        var normalized = new string((string.IsNullOrWhiteSpace(documentType) ? "BILLING-DOCUMENT" : documentType)
            .Trim()
            .ToUpperInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('-');
        return Truncate($"EST-FAC-BILL-{(string.IsNullOrWhiteSpace(normalized) ? "DOCUMENT" : normalized)}", 80);
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TrimOrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static object ToCentralDmsPublicationDto(CentralDocumentRecord record) => new
    {
        record.Id,
        record.DocumentReference,
        record.Title,
        record.SourceModule,
        record.SourceLabel,
        record.SourceEntityType,
        record.SourceRecordReference,
        record.MetadataTemplateCode,
        record.RepositoryStatus,
        record.RepositoryPath,
        record.ExternalDocumentUrl,
        record.CurrentVersion,
        record.VersionStatus,
        record.AnnotationStatus,
        record.CommentStatus,
        record.AccessProfile,
        record.RetentionStatus,
        record.LifecycleStatus,
        PublishedToCentralDmsAt = record.PublishedAt
    };
}

public sealed record PublishFacilitiesBillingDocumentRequest(
    string? DocumentTitle,
    string? DocumentType,
    string? BillingDocumentKind,
    string? FacilitiesBillingReference,
    string? FinanceArReference,
    string? PropertyUnit,
    string? CustomerAccountReference,
    string? RepositoryPath,
    string? ExternalDocumentUrl,
    string? FileName,
    string? ContentType,
    long? FileSize,
    string? Notes);
