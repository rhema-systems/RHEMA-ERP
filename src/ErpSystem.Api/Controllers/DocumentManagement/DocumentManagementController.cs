using System.Text;
using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Models;
using ErpSystem.Data;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Api.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CentralDocumentMetadataTemplateEntity = ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate;

namespace ErpSystem.Api.Controllers.DocumentManagement;

[ApiController]
[Route("api/document-management")]
[Authorize]
public sealed class DocumentManagementController : ControllerBase
{
    private const string WordDocumentContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly ICentralDocumentManagementService _documentManagement;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICentralDocumentRenditionService _renditionService;
    private readonly INotificationService _notificationService;

    public DocumentManagementController(
        ICentralDocumentManagementService documentManagement,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICentralDocumentRenditionService renditionService,
        INotificationService notificationService)
    {
        _documentManagement = documentManagement;
        _db = db;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _renditionService = renditionService;
        _notificationService = notificationService;
    }

    [HttpGet("workspaces")]
    public IActionResult GetWorkspaces()
    {
        return Ok(new
        {
            success = true,
            data = _documentManagement.GetWorkspaces()
        });
    }

    [HttpGet("workspaces/{entityType}")]
    public IActionResult GetWorkspace(string entityType)
    {
        var workspace = _documentManagement.GetWorkspace(entityType);

        if (workspace is null)
        {
            return NotFound(new
            {
                success = false,
                message = $"Central DMS workspace '{entityType}' was not found."
            });
        }

        return Ok(new
        {
            success = true,
            data = workspace
        });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var records = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var recordIds = records.Select(item => item.Id).ToList();
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValuesByRecord = await GetMetadataValuesByRecordAsync(tenantId, recordIds, cancellationToken);
        var accessRules = await GetActiveAccessRulesAsync(tenantId, cancellationToken);
        var retentionPolicies = await GetActiveRetentionPoliciesAsync(tenantId, cancellationToken);
        var annotationReviews = recordIds.Count == 0
            ? new List<CentralDocumentAnnotationReview>()
            : await _db.CentralDocumentAnnotationReviews
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && recordIds.Contains(item.DocumentRecordId)
                    && !item.IsDeleted)
                .ToListAsync(cancellationToken);

        var completenessByRecord = records.ToDictionary(
            record => record.Id,
            record => BuildMetadataCompleteness(
                record,
                ResolveTemplate(record, templatesByCode),
                MetadataValuesFor(record.Id, metadataValuesByRecord)));
        var openAnnotationReviews = annotationReviews.Where(IsOpenAnnotationReview).ToList();
        var metadataCompleteCount = completenessByRecord.Values.Count(item => string.Equals(item.Status, "Complete", StringComparison.OrdinalIgnoreCase));
        var metadataPartialCount = completenessByRecord.Values.Count(item => string.Equals(item.Status, "Partial", StringComparison.OrdinalIgnoreCase));
        var metadataMissingTemplateCount = completenessByRecord.Values.Count(item => string.Equals(item.Status, "Missing template", StringComparison.OrdinalIgnoreCase));
        var repositoryLinkedCount = records.Count(IsRepositoryLinked);
        var versionControlledCount = records.Count(record => !string.IsNullOrWhiteSpace(record.CurrentVersion) && !IsPendingVersionStatus(record.VersionStatus));
        var sourceLabelCount = records.Count(record => !string.IsNullOrWhiteSpace(record.SourceLabel));
        var accessCoveredCount = records.Count(record => HasAccessRule(record, accessRules));
        var accessGapProfiles = records
            .Where(record => !HasAccessRule(record, accessRules))
            .Select(record => string.IsNullOrWhiteSpace(record.AccessProfile) ? "Unclassified" : record.AccessProfile.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var retentionActionCount = records.Count(NeedsRetentionAction);
        var retentionCoveredCount = records.Count(record =>
        {
            var template = ResolveTemplate(record, templatesByCode);
            return FindRetentionPolicy(record, template, retentionPolicies) is not null && !NeedsRetentionAction(record);
        });
        var modules = BuildDashboardModuleQueues(
            records,
            completenessByRecord,
            openAnnotationReviews);

        return Ok(new
        {
            success = true,
            data = new
            {
                metrics = new[]
                {
                    BuildDashboardMetric(
                        "Documents indexed",
                        records.Count.ToString(),
                        "Central DMS records received from source modules",
                        $"{repositoryLinkedCount} repository linked"),
                    BuildDashboardMetric(
                        "Metadata complete",
                        Percent(metadataCompleteCount, records.Count),
                        $"{metadataPartialCount} partial / {metadataMissingTemplateCount} missing template",
                        $"{metadataCompleteCount} complete"),
                    BuildDashboardMetric(
                        "Open annotations",
                        openAnnotationReviews.Count.ToString(),
                        "PDF viewer annotation reviews needing action",
                        $"{annotationReviews.Count} total reviews"),
                    BuildDashboardMetric(
                        "Retention actions",
                        retentionActionCount.ToString(),
                        "Archive, hold, expiry, or review decisions due",
                        $"{retentionCoveredCount} covered"),
                    BuildDashboardMetric(
                        "Access gaps",
                        accessGapProfiles.ToString(),
                        "Access profiles without active DMS rules",
                        $"{accessCoveredCount} records covered"),
                    BuildDashboardMetric(
                        "Repository linked",
                        Percent(repositoryLinkedCount, records.Count),
                        $"{records.Count - repositoryLinkedCount} records not linked",
                        "live")
                },
                readiness = new[]
                {
                    BuildDashboardMetric(
                        "Repository linked",
                        Percent(repositoryLinkedCount, records.Count),
                        "Documents with DMS repository references",
                        $"{repositoryLinkedCount}/{records.Count}"),
                    BuildDashboardMetric(
                        "Version controlled",
                        Percent(versionControlledCount, records.Count),
                        "Documents with current version references",
                        $"{versionControlledCount}/{records.Count}"),
                    BuildDashboardMetric(
                        "Source labels clean",
                        Percent(sourceLabelCount, records.Count),
                        "Records with explicit source module labels",
                        $"{sourceLabelCount}/{records.Count}"),
                    BuildDashboardMetric(
                        "Access classified",
                        Percent(accessCoveredCount, records.Count),
                        "Documents matched to active access rules",
                        $"{accessCoveredCount}/{records.Count}"),
                    BuildDashboardMetric(
                        "Retention controlled",
                        Percent(retentionCoveredCount, records.Count),
                        "Documents matched to retention policy without due action",
                        $"{retentionActionCount} action needed")
                },
                moduleQueues = modules
            }
        });
    }

    [HttpGet("metadata-templates")]
    public async Task<IActionResult> GetMetadataTemplates(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var templates = await _db.CentralDocumentMetadataTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.Module)
            .ThenBy(item => item.DocumentType)
            .ToListAsync(cancellationToken);

        if (templates.Count == 0)
        {
            return Ok(new
            {
                success = true,
                data = _documentManagement.GetMetadataTemplates()
            });
        }

        return Ok(new
        {
            success = true,
            data = templates.Select(ToTemplateDto).ToList()
        });
    }

    [HttpGet("document-templates")]
    public async Task<IActionResult> GetDocumentTemplates([FromQuery] string? module = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        await EnsureDefaultGenerationTemplatesAsync(tenantId, cancellationToken);

        var query = _db.CentralDocumentGenerationTemplates
            .AsNoTracking()
            .Where(template => template.TenantId == tenantId && !template.IsDeleted);

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(template => template.Module == module.Trim());
        }

        var templates = await query
            .OrderBy(template => template.Module)
            .ThenBy(template => template.Title)
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = templates.Select(ToGeneratedTemplateDto).ToList() });
    }

    [HttpPut("document-templates/{templateCode}")]
    public async Task<IActionResult> UpdateDocumentTemplate(
        string templateCode,
        [FromBody] UpsertGeneratedDocumentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDmsAccessAdministrator())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(templateCode))
        {
            return BadRequest(new { success = false, message = "Template code is required." });
        }

        var tenantId = GetTenantId();
        await EnsureDefaultGenerationTemplatesAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var code = templateCode.Trim();
        var template = await _db.CentralDocumentGenerationTemplates
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.TemplateCode == code
                && !item.IsDeleted, cancellationToken);

        if (template is null)
        {
            template = new CentralDocumentGenerationTemplate
            {
                TenantId = tenantId,
                TemplateCode = code,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = GetUserId()
            };
            _db.CentralDocumentGenerationTemplates.Add(template);
        }

        template.Title = Truncate(TrimOrDefault(request.Title, template.Title), 180);
        template.TitleTemplate = Truncate(TrimOrDefault(request.TitleTemplate, template.TitleTemplate), 250);
        template.Module = Truncate(TrimOrDefault(request.Module, template.Module), 120);
        template.SourceLabel = Truncate(TrimOrDefault(request.SourceLabel, template.SourceLabel), 500);
        template.DocumentType = Truncate(TrimOrDefault(request.DocumentType, template.DocumentType), 150);
        template.MetadataTemplateCode = Truncate(TrimOrDefault(request.MetadataTemplateCode, template.MetadataTemplateCode), 80);
        template.AccessProfile = Truncate(TrimOrDefault(request.AccessProfile, template.AccessProfile), 150);
        template.MergeFieldsJson = JsonSerializer.Serialize((request.MergeFields ?? DeserializeStringArray(template.MergeFieldsJson)).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        template.Body = request.Body ?? template.Body;
        template.IsActive = request.IsActive ?? template.IsActive;
        template.RequiresApproval = request.RequiresApproval ?? template.RequiresApproval;
        template.ApprovalRole = TrimToNull(request.ApprovalRole);
        template.SignatureRole = TrimToNull(request.SignatureRole);
        template.DefaultDispatchChannel = TrimToNull(request.DefaultDispatchChannel);
        template.UpdatedAt = now;
        template.UpdatedBy = _currentUserService.UserName ?? "System";
        template.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = ToGeneratedTemplateDto(template) });
    }

    [HttpPost("document-templates")]
    public async Task<IActionResult> CreateDocumentTemplate(
        [FromBody] UpsertGeneratedDocumentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            return BadRequest(new { success = false, message = "Template code is required." });
        }

        return await UpdateDocumentTemplate(request.TemplateCode, request, cancellationToken);
    }

    [HttpPost("generated-documents/{recordId:guid}/workflow")]
    public async Task<IActionResult> UpdateGeneratedDocumentWorkflow(
        Guid recordId,
        [FromBody] GeneratedDocumentWorkflowActionRequest request,
        CancellationToken cancellationToken)
    {
        var action = TrimOrDefault(request.Action, string.Empty);
        if (string.IsNullOrWhiteSpace(action))
        {
            return BadRequest(new { success = false, message = "Workflow action is required." });
        }

        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == recordId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "Generated DMS document was not found." });
        }

        var normalizedAction = NormalizeMetadataField(action);
        // PR review: generated document workflow transitions must honor DMS permissions and configured approval/signature roles.
        if (!await CanRunGeneratedDocumentWorkflowActionAsync(tenantId, record, normalizedAction, cancellationToken))
        {
            return Forbid();
        }

        var version = await _db.CentralDocumentVersions
            .Where(item => item.TenantId == tenantId
                && item.DocumentRecordId == record.Id
                && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var metadata = new List<UpsertDocumentMetadataValueRequest>();
        var actor = _currentUserService.UserName ?? "System";

        switch (normalizedAction)
        {
            case "submitapproval":
            case "submitforapproval":
                record.LifecycleStatus = "Pending approval";
                record.VersionStatus = "Submitted";
                metadata.AddRange(WorkflowMetadata(
                    ("Approval status", "Pending approval"),
                    ("Submitted for approval by", actor),
                    ("Submitted for approval at", now.ToString("O")),
                    ("Approval notes", request.Notes)));
                break;
            case "approve":
                record.LifecycleStatus = "Approved";
                record.VersionStatus = "Approved";
                metadata.AddRange(WorkflowMetadata(
                    ("Approval status", "Approved"),
                    ("Approved by", actor),
                    ("Approved at", now.ToString("O")),
                    ("Approval notes", request.Notes)));
                break;
            case "sign":
                record.LifecycleStatus = "Signed";
                record.VersionStatus = "Signed";
                metadata.AddRange(WorkflowMetadata(
                    ("Signature status", "Signed"),
                    ("Signed by", actor),
                    ("Signed at", now.ToString("O")),
                    ("Signature role", request.SignatureRole),
                    ("Signature notes", request.Notes)));
                break;
            case "dispatch":
                record.LifecycleStatus = "Dispatched";
                record.VersionStatus = "Current";
                record.PublishedAt ??= now;
                record.PublishedById ??= GetUserId();
                metadata.AddRange(WorkflowMetadata(
                    ("Dispatch status", "Dispatched"),
                    ("Dispatch channel", request.DispatchChannel),
                    ("Dispatched to", request.DispatchedTo),
                    ("Dispatch reference", request.DispatchReference),
                    ("Dispatched at", now.ToString("O")),
                    ("Dispatch notes", request.Notes)));
                break;
            case "return":
            case "returnforaction":
                record.LifecycleStatus = "Returned for action";
                record.VersionStatus = "Returned";
                metadata.AddRange(WorkflowMetadata(
                    ("Approval status", "Returned for action"),
                    ("Returned by", actor),
                    ("Returned at", now.ToString("O")),
                    ("Return notes", request.Notes)));
                break;
            default:
                return BadRequest(new { success = false, message = "Unsupported generated document workflow action." });
        }

        if (version is not null)
        {
            version.Status = record.VersionStatus;
            version.ChangeSummary = string.IsNullOrWhiteSpace(request.Notes) ? $"{action} applied." : request.Notes.Trim();
            version.UpdatedAt = now;
            version.UpdatedBy = actor;
            version.LastModifiedById = GetUserId();
            if (string.Equals(record.VersionStatus, "Current", StringComparison.OrdinalIgnoreCase))
            {
                version.PublishedAt ??= now;
                version.PublishedById ??= GetUserId();
            }
        }

        record.UpdatedAt = now;
        record.UpdatedBy = actor;
        record.LastModifiedById = GetUserId();
        await UpsertMetadataValuesAsync(tenantId, record, metadata, "Generated document workflow", now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await NotifyGeneratedDocumentWorkflowAsync(record, normalizedAction, request, cancellationToken);

        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var values = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new
            {
                record = ToRecordDto(record, templatesByCode, values),
                version = version is null ? null : ToVersionDto(version)
            }
        });
    }

    private async Task NotifyGeneratedDocumentWorkflowAsync(
        CentralDocumentRecord record,
        string normalizedAction,
        GeneratedDocumentWorkflowActionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await RoleNotificationDispatcher.NotifyRolesAsync(
                _db,
                _notificationService,
                record.TenantId,
                GetUserId(),
                GeneratedDocumentWorkflowRoles(record, normalizedAction, request),
                GeneratedDocumentWorkflowTitle(normalizedAction),
                GeneratedDocumentWorkflowMessage(record, normalizedAction),
                "dms.generated-document.workflow",
                "CentralDocumentRecord",
                record.Id,
                "/document-management",
                new Dictionary<string, object>
                {
                    ["sourceLabel"] = record.SourceLabel,
                    ["sourceModule"] = record.SourceModule,
                    ["sourceEntityType"] = record.SourceEntityType ?? string.Empty,
                    ["documentReference"] = record.DocumentReference,
                    ["workflowAction"] = normalizedAction,
                    ["lifecycleStatus"] = record.LifecycleStatus,
                    ["versionStatus"] = record.VersionStatus,
                    ["dispatchChannel"] = request.DispatchChannel ?? string.Empty,
                    ["dispatchReference"] = request.DispatchReference ?? string.Empty
                },
                cancellationToken);
        }
        catch
        {
            // Notification delivery must not block the DMS workflow action.
        }
    }

    private static IEnumerable<string?> GeneratedDocumentWorkflowRoles(
        CentralDocumentRecord record,
        string normalizedAction,
        GeneratedDocumentWorkflowActionRequest request)
    {
        var sourceRoles = DmsSourceRoles(record).ToList();

        return normalizedAction switch
        {
            "submitapproval" or "submitforapproval" => sourceRoles.Concat(new[] { "Head of Estate", "Estate Manager" }),
            "approve" => sourceRoles.Concat(new[] { request.SignatureRole, "Authorised Signatory", "Records Officer" }),
            "sign" => sourceRoles.Concat(new[] { "Records Officer", "Estate Officer" }),
            "dispatch" => sourceRoles.Concat(new[] { "Records Officer", "Estate Officer" }),
            "return" or "returnforaction" => sourceRoles.Concat(new[] { "Records Officer", "Estate Officer" }),
            _ => sourceRoles
        };
    }

    private static IEnumerable<string> DmsSourceRoles(CentralDocumentRecord record)
    {
        var source = $"{record.SourceModule} {record.SourceLabel} {record.SourceEntityType}";

        if (source.Contains("Facilities", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Facility", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Facilities Manager";
        }

        if (source.Contains("PropertyManagement", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Property Management", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Property Manager";
        }

        yield return "Estate Manager";
        yield return "Estate Officer";
    }

    private static string GeneratedDocumentWorkflowTitle(string normalizedAction) =>
        normalizedAction switch
        {
            "submitapproval" or "submitforapproval" => "Generated document submitted for approval",
            "approve" => "Generated document approved",
            "sign" => "Generated document signed",
            "dispatch" => "Generated document dispatched",
            "return" or "returnforaction" => "Generated document returned for action",
            _ => "Generated document workflow updated"
        };

    private static string GeneratedDocumentWorkflowMessage(CentralDocumentRecord record, string normalizedAction)
    {
        var status = normalizedAction switch
        {
            "submitapproval" or "submitforapproval" => "submitted for approval",
            "approve" => "approved",
            "sign" => "signed",
            "dispatch" => "dispatched",
            "return" or "returnforaction" => "returned for action",
            _ => "updated"
        };

        return $"{record.DocumentReference} - {record.Title} was {status}.";
    }

    private async Task<GeneratedDocumentTemplateDefinition?> ResolveGeneratedTemplateAsync(
        Guid tenantId,
        string templateCode,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultGenerationTemplatesAsync(tenantId, cancellationToken);
        var template = await _db.CentralDocumentGenerationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.TemplateCode == templateCode
                && item.IsActive
                && !item.IsDeleted, cancellationToken);

        return template is null ? null : ToGeneratedTemplateDefinition(template);
    }

    private async Task EnsureDefaultGenerationTemplatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existingCodes = await _db.CentralDocumentGenerationTemplates
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.TemplateCode)
            .ToListAsync(cancellationToken);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var definition in GeneratedDocumentTemplates.Where(definition => !existing.Contains(definition.TemplateCode)))
        {
            _db.CentralDocumentGenerationTemplates.Add(new CentralDocumentGenerationTemplate
            {
                TenantId = tenantId,
                TemplateCode = definition.TemplateCode,
                Title = definition.Title,
                TitleTemplate = definition.TitleTemplate,
                Module = definition.Module,
                SourceLabel = definition.SourceLabel,
                DocumentType = definition.DocumentType,
                MetadataTemplateCode = definition.MetadataTemplateCode,
                AccessProfile = definition.AccessProfile,
                MergeFieldsJson = JsonSerializer.Serialize(definition.MergeFields),
                Body = definition.Body,
                IsActive = true,
                RequiresApproval = true,
                ApprovalRole = "Head of Estate",
                SignatureRole = "Authorised Signatory",
                DefaultDispatchChannel = "Email / Print",
                CreatedAt = now,
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<UpsertDocumentMetadataValueRequest> WorkflowMetadata(
        params (string Label, string? Value)[] values) =>
        values
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => new UpsertDocumentMetadataValueRequest(
                NormalizeMetadataField(item.Label),
                item.Label,
                item.Value,
                "text"))
            .ToList();

    private static GeneratedDocumentTemplateDefinition ToGeneratedTemplateDefinition(CentralDocumentGenerationTemplate template) =>
        new(
            template.TemplateCode,
            template.Title,
            template.TitleTemplate,
            template.Module,
            template.SourceLabel,
            template.DocumentType,
            template.MetadataTemplateCode,
            template.AccessProfile,
            DeserializeStringArray(template.MergeFieldsJson),
            template.Body,
            template.RequiresApproval,
            template.ApprovalRole,
            template.SignatureRole,
            template.DefaultDispatchChannel);

    [HttpPost("document-templates/generate")]
    public async Task<IActionResult> GenerateDocumentFromTemplate(
        [FromBody] GenerateDocumentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            return BadRequest(new { success = false, message = "Template code is required." });
        }

        var tenantId = GetTenantId();
        var template = await ResolveGeneratedTemplateAsync(tenantId, request.TemplateCode.Trim(), cancellationToken);

        if (template is null)
        {
            return NotFound(new { success = false, message = "Document generation template was not found." });
        }

        var now = DateTime.UtcNow;
        var mergeValues = BuildMergeValues(template, request, now);
        var content = MergeTemplate(template.Body, mergeValues);
        var title = Truncate(MergeTemplate(template.TitleTemplate, mergeValues), 250);
        var sourceRecord = Truncate(TrimToNull(request.SourceRecordReference)
            ?? TrimToNull(request.CaseReference)
            ?? request.SourceRecordId?.ToString()
            ?? "Unreferenced case", 140);
        var generatedSourceReference = $"{sourceRecord} / {template.TemplateCode}";
        var sourceModule = TrimOrDefault(request.SourceModule, template.Module);
        if (!CanUseSourceModuleForDms(sourceModule))
        {
            return Forbid();
        }

        var sourceLabel = string.IsNullOrWhiteSpace(request.SourceLabel)
            ? template.SourceLabel
            : request.SourceLabel.Trim();

        var pdfBytes = BuildSimplePdf(title, content);
        await using var pdfStream = new MemoryStream(pdfBytes);
        var safeFileName = $"{SafeFileName(template.TemplateCode)}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var storageResult = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = pdfStream,
            FileName = safeFileName,
            ContentType = "application/pdf",
            FileSize = pdfBytes.Length,
            Category = "central-dms-generated",
            TenantId = tenantId.ToString(),
            Metadata =
            {
                ["TemplateCode"] = template.TemplateCode,
                ["SourceModule"] = sourceModule,
                ["SourceRecord"] = generatedSourceReference
            }
        });

        if (!storageResult.Success)
        {
            return BadRequest(new { success = false, message = storageResult.ErrorMessage ?? "Unable to generate the PDF rendition." });
        }

        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.SourceModule == sourceModule
                && item.MetadataTemplateCode == template.MetadataTemplateCode
                && item.SourceRecordReference == generatedSourceReference,
                cancellationToken);

        if (record is null)
        {
            record = new CentralDocumentRecord
            {
                TenantId = tenantId,
                DocumentReference = await NextDocumentReferenceAsync(tenantId, cancellationToken),
                Title = title,
                SourceModule = sourceModule,
                SourceLabel = sourceLabel,
                SourceEntityType = template.DocumentType,
                SourceRecordReference = generatedSourceReference,
                SourceRecordId = request.SourceRecordId,
                MetadataTemplateCode = template.MetadataTemplateCode,
                RepositoryStatus = "Linked",
                RepositoryPath = storageResult.FilePath,
                CurrentVersion = "v1.0",
                VersionStatus = "Draft",
                AnnotationStatus = "PDF preview ready",
                CommentStatus = "No comments",
                AccessProfile = template.AccessProfile,
                RetentionStatus = "Current",
                LifecycleStatus = "Draft",
                Notes = $"Generated from {template.TemplateCode}.",
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = GetUserId()
            };
            _db.CentralDocumentRecords.Add(record);
        }
        else
        {
            if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
            {
                return Forbid();
            }

            record.Title = title;
            record.SourceLabel = sourceLabel;
            record.RepositoryStatus = "Linked";
            record.RepositoryPath = storageResult.FilePath;
            record.CurrentVersion = NextVersionNumber(record.CurrentVersion);
            record.VersionStatus = "Draft";
            record.AnnotationStatus = "PDF preview ready";
            record.CommentStatus = "No comments";
            record.AccessProfile = template.AccessProfile;
            record.LifecycleStatus = "Draft";
            record.Notes = $"Regenerated from {template.TemplateCode}.";
            record.UpdatedAt = now;
            record.UpdatedBy = _currentUserService.UserName ?? "System";
            record.LastModifiedById = GetUserId();
        }

        var uploadRecord = new FileUploadRecord
        {
            TenantId = tenantId,
            Category = "central-dms-generated",
            FilePath = storageResult.FilePath,
            StoredFileName = storageResult.FileName,
            OriginalFileName = storageResult.OriginalFileName,
            ContentType = storageResult.ContentType,
            FileSize = storageResult.FileSize,
            StorageProvider = storageResult.StorageProvider,
            UploadedByUserId = GetUserId() ?? Guid.Empty,
            VirusScanStatus = FileVirusScanStatus.Skipped,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };
        _db.FileUploadRecords.Add(uploadRecord);

        var version = new CentralDocumentVersion
        {
            TenantId = tenantId,
            DocumentRecordId = record.Id,
            VersionNumber = record.CurrentVersion ?? "v1.0",
            Status = "Draft",
            RepositoryPath = storageResult.FilePath,
            RenditionPath = storageResult.FilePath,
            FileName = storageResult.OriginalFileName,
            ContentType = "application/pdf",
            FileSize = storageResult.FileSize,
            FileUploadRecordId = uploadRecord.Id,
            ChangeSummary = $"Generated from {template.TemplateCode}.",
            CreatedByUserId = GetUserId(),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };
        _db.CentralDocumentVersions.Add(version);

        var metadataValues = BuildGeneratedDocumentMetadataValues(template, mergeValues, generatedSourceReference);
        await UpsertMetadataValuesAsync(tenantId, record, metadataValues, "Generated document", now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var values = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                template = ToGeneratedTemplateDto(template),
                record = ToRecordDto(record, templatesByCode, values),
                version = ToVersionDto(version),
                content,
                pdfUrl = VersionContentUrl(record.Id, version.Id),
                dmsReference = record.DocumentReference,
                sourceLabel = record.SourceLabel
            }
        });
    }

    [HttpGet("register")]
    public async Task<IActionResult> GetRegister(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var records = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        records = await FilterViewableRecordsAsync(tenantId, records, cancellationToken);
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValuesByRecord = await GetMetadataValuesByRecordAsync(tenantId, records.Select(item => item.Id), cancellationToken);

        if (records.Count == 0)
        {
            return Ok(new
            {
                success = true,
                data = _documentManagement.GetRegisterItems()
            });
        }

        return Ok(new
        {
            success = true,
            data = records.Select(record => ToRegisterDto(record, templatesByCode, MetadataValuesFor(record.Id, metadataValuesByRecord))).ToList()
        });
    }

    [HttpGet("records")]
    public async Task<IActionResult> GetRecords([FromQuery] string? module = null, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        take = Math.Clamp(take, 1, 500);
        var query = _db.CentralDocumentRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(item => item.SourceModule == module);
        }

        var records = await query
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        records = await FilterViewableRecordsAsync(tenantId, records, cancellationToken);
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValuesByRecord = await GetMetadataValuesByRecordAsync(tenantId, records.Select(item => item.Id), cancellationToken);

        return Ok(new { success = true, data = records.Select(record => ToRecordDto(record, templatesByCode, MetadataValuesFor(record.Id, metadataValuesByRecord))).ToList() });
    }

    [HttpGet("queues/integration")]
    public async Task<IActionResult> GetIntegrationQueue([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        take = Math.Clamp(take, 1, 500);
        var records = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        records = await FilterViewableRecordsAsync(tenantId, records, cancellationToken);
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValuesByRecord = await GetMetadataValuesByRecordAsync(tenantId, records.Select(item => item.Id), cancellationToken);

        var queueItems = records
            .Select(record =>
            {
                var completeness = BuildMetadataCompleteness(record, ResolveTemplate(record, templatesByCode), MetadataValuesFor(record.Id, metadataValuesByRecord));
                var issue = IntegrationIssue(record, completeness);
                var status = issue == "Ready for DMS control" ? "Accepted" : "Pending Review";
                return new
                {
                    id = record.Id,
                    queueReference = record.DocumentReference,
                    sourceModule = record.SourceModule,
                    sourceLabel = record.SourceLabel,
                    sourceRecord = record.SourceRecordReference ?? record.SourceRecordId?.ToString() ?? "Not recorded",
                    documentType = record.SourceEntityType ?? ResolveTemplate(record, templatesByCode)?.DocumentType ?? "Document package",
                    templateCode = record.MetadataTemplateCode ?? string.Empty,
                    issue,
                    receivedAt = record.CreatedAt,
                    status,
                    document = ToRecordDto(record, templatesByCode, MetadataValuesFor(record.Id, metadataValuesByRecord))
                };
            })
            .Where(item => item.status == "Pending Review")
            .ToList();

        return Ok(new { success = true, data = queueItems });
    }

    [HttpGet("queues/versions")]
    public async Task<IActionResult> GetVersionQueue([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        take = Math.Clamp(take, 1, 500);
        var versions = await _db.CentralDocumentVersions
            .AsNoTracking()
            .Include(item => item.DocumentRecord)
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && !item.DocumentRecord.IsDeleted
                && (item.Status == "Draft"
                    || item.Status == "Submitted"
                    || item.Status == "Published"
                    || item.Status == "Pending Publication"))
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        versions = await FilterViewableVersionsAsync(tenantId, versions, cancellationToken);

        var queueItems = versions.Select(version => new
        {
            id = version.Id,
            documentRecordId = version.DocumentRecordId,
            documentReference = version.DocumentRecord.DocumentReference,
            title = version.DocumentRecord.Title,
            sourceModule = version.DocumentRecord.SourceModule,
            sourceLabel = version.DocumentRecord.SourceLabel,
            currentVersion = version.DocumentRecord.CurrentVersion ?? "None",
            requestedVersion = version.VersionNumber,
            reason = version.ChangeSummary ?? "Version publication pending",
            status = version.Status == "Published" ? "Pending Publication" : version.Status,
            document = ToRecordDto(version.DocumentRecord),
            version = ToVersionDto(version)
        }).ToList();

        return Ok(new { success = true, data = queueItems });
    }

    [HttpGet("records/{id:guid}")]
    public async Task<IActionResult> GetRecord(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .AsNoTracking()
            .Include(item => item.Versions.Where(version => !version.IsDeleted))
            .Include(item => item.AnnotationReviews.Where(review => !review.IsDeleted))
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }
        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanView, cancellationToken))
        {
            return Forbid();
        }
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValues = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        var accessRules = await GetActiveAccessRulesAsync(tenantId, cancellationToken);
        var retentionPolicies = await GetActiveRetentionPoliciesAsync(tenantId, cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                record = ToRecordDto(record, templatesByCode, metadataValues, accessRules, retentionPolicies),
                versions = record.Versions.OrderByDescending(item => item.CreatedAt).Select(ToVersionDto),
                annotationReviews = record.AnnotationReviews.OrderByDescending(item => item.CreatedAt).Select(ToAnnotationReviewDto)
            }
        });
    }

    [HttpPost("records")]
    public async Task<IActionResult> CreateRecord([FromBody] UpsertDocumentRecordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.SourceModule))
        {
            return BadRequest(new { success = false, message = "Title and source module are required." });
        }

        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;
        var requestedAccessProfile = string.IsNullOrWhiteSpace(request.AccessProfile)
            ? "Module restricted"
            : request.AccessProfile.Trim();
        if (!await CanAssignAccessProfileAsync(tenantId, requestedAccessProfile, cancellationToken))
        {
            return Forbid();
        }

        var record = new CentralDocumentRecord
        {
            TenantId = tenantId,
            DocumentReference = string.IsNullOrWhiteSpace(request.DocumentReference)
                ? await NextDocumentReferenceAsync(tenantId, cancellationToken)
                : request.DocumentReference.Trim(),
            Title = request.Title.Trim(),
            SourceModule = request.SourceModule.Trim(),
            SourceLabel = string.IsNullOrWhiteSpace(request.SourceLabel)
                ? $"Source: {request.SourceModule.Trim()} -> Central DMS"
                : request.SourceLabel.Trim(),
            SourceEntityType = request.SourceEntityType,
            SourceRecordReference = request.SourceRecordReference,
            SourceRecordId = request.SourceRecordId,
            MetadataTemplateCode = request.MetadataTemplateCode,
            RepositoryStatus = request.RepositoryStatus ?? "Not linked",
            RepositoryPath = request.RepositoryPath,
            ExternalDocumentUrl = request.ExternalDocumentUrl,
            CurrentVersion = request.CurrentVersion,
            VersionStatus = request.VersionStatus ?? "Draft",
            AnnotationStatus = request.AnnotationStatus ?? "Not required",
            CommentStatus = request.CommentStatus ?? "No comments",
            AccessProfile = requestedAccessProfile,
            RetentionStatus = request.RetentionStatus ?? "Current",
            LifecycleStatus = request.LifecycleStatus ?? "Active",
            EffectiveDate = request.EffectiveDate,
            ExpiryDate = request.ExpiryDate,
            ReviewDate = request.ReviewDate,
            Notes = request.Notes,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        _db.CentralDocumentRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetRecord), new { id = record.Id }, new { success = true, data = ToRecordDto(record, templatesByCode) });
    }

    [HttpPut("records/{id:guid}")]
    public async Task<IActionResult> UpdateRecord(Guid id, [FromBody] UpsertDocumentRecordRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
        {
            return Forbid();
        }

        var requestedAccessProfile = request.AccessProfile?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedAccessProfile)
            && !string.Equals(requestedAccessProfile, record.AccessProfile, StringComparison.OrdinalIgnoreCase)
            && !await CanAssignAccessProfileAsync(tenantId, requestedAccessProfile, cancellationToken))
        {
            return Forbid();
        }

        record.Title = request.Title?.Trim() ?? record.Title;
        record.SourceModule = request.SourceModule?.Trim() ?? record.SourceModule;
        record.SourceLabel = request.SourceLabel?.Trim() ?? record.SourceLabel;
        record.SourceEntityType = request.SourceEntityType ?? record.SourceEntityType;
        record.SourceRecordReference = request.SourceRecordReference ?? record.SourceRecordReference;
        record.SourceRecordId = request.SourceRecordId ?? record.SourceRecordId;
        record.MetadataTemplateCode = request.MetadataTemplateCode ?? record.MetadataTemplateCode;
        record.RepositoryStatus = request.RepositoryStatus ?? record.RepositoryStatus;
        record.RepositoryPath = request.RepositoryPath ?? record.RepositoryPath;
        record.ExternalDocumentUrl = request.ExternalDocumentUrl ?? record.ExternalDocumentUrl;
        record.CurrentVersion = request.CurrentVersion ?? record.CurrentVersion;
        record.VersionStatus = request.VersionStatus ?? record.VersionStatus;
        record.AnnotationStatus = request.AnnotationStatus ?? record.AnnotationStatus;
        record.CommentStatus = request.CommentStatus ?? record.CommentStatus;
        record.AccessProfile = requestedAccessProfile ?? record.AccessProfile;
        record.RetentionStatus = request.RetentionStatus ?? record.RetentionStatus;
        record.LifecycleStatus = request.LifecycleStatus ?? record.LifecycleStatus;
        record.EffectiveDate = request.EffectiveDate ?? record.EffectiveDate;
        record.ExpiryDate = request.ExpiryDate ?? record.ExpiryDate;
        record.ReviewDate = request.ReviewDate ?? record.ReviewDate;
        record.Notes = request.Notes ?? record.Notes;
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValues = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        var accessRules = await GetActiveAccessRulesAsync(tenantId, cancellationToken);
        var retentionPolicies = await GetActiveRetentionPoliciesAsync(tenantId, cancellationToken);
        return Ok(new { success = true, data = ToRecordDto(record, templatesByCode, metadataValues, accessRules, retentionPolicies) });
    }

    [HttpPut("records/{id:guid}/lifecycle-controls")]
    public async Task<IActionResult> UpdateLifecycleControls(Guid id, [FromBody] UpdateDocumentLifecycleControlsRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
        {
            return Forbid();
        }

        var requestedAccessProfile = TrimOrDefault(request.AccessProfile, "Module restricted");
        if (!string.Equals(requestedAccessProfile, record.AccessProfile, StringComparison.OrdinalIgnoreCase)
            && !await CanAssignAccessProfileAsync(tenantId, requestedAccessProfile, cancellationToken))
        {
            return Forbid();
        }

        record.MetadataTemplateCode = TrimToNull(request.MetadataTemplateCode);
        record.RepositoryStatus = TrimOrDefault(request.RepositoryStatus, "Not linked");
        record.RepositoryPath = TrimToNull(request.RepositoryPath);
        record.ExternalDocumentUrl = TrimToNull(request.ExternalDocumentUrl);
        record.CurrentVersion = TrimToNull(request.CurrentVersion);
        record.VersionStatus = TrimOrDefault(request.VersionStatus, "Draft");
        record.AnnotationStatus = TrimOrDefault(request.AnnotationStatus, "Not required");
        record.CommentStatus = TrimOrDefault(request.CommentStatus, "No comments");
        record.AccessProfile = requestedAccessProfile;
        record.RetentionStatus = TrimOrDefault(request.RetentionStatus, "Current");
        record.LifecycleStatus = TrimOrDefault(request.LifecycleStatus, "Active");
        record.EffectiveDate = request.EffectiveDate;
        record.ExpiryDate = request.ExpiryDate;
        record.ReviewDate = request.ReviewDate;
        record.Notes = TrimToNull(request.Notes);
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);
        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValues = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        var accessRules = await GetActiveAccessRulesAsync(tenantId, cancellationToken);
        var retentionPolicies = await GetActiveRetentionPoliciesAsync(tenantId, cancellationToken);
        return Ok(new { success = true, data = ToRecordDto(record, templatesByCode, metadataValues, accessRules, retentionPolicies) });
    }

    [HttpPut("records/{id:guid}/metadata-values")]
    public async Task<IActionResult> UpdateMetadataValues(Guid id, [FromBody] UpdateDocumentMetadataValuesRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var values = (request.Values ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.FieldLabel))
            .Select(item => new
            {
                FieldLabel = item.FieldLabel!.Trim(),
                FieldKey = string.IsNullOrWhiteSpace(item.FieldKey)
                    ? NormalizeMetadataField(item.FieldLabel!)
                    : NormalizeMetadataField(item.FieldKey!),
                FieldValue = TrimToNull(item.FieldValue),
                ValueType = string.IsNullOrWhiteSpace(item.ValueType) ? "text" : item.ValueType!.Trim()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.FieldKey))
            .GroupBy(item => item.FieldKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        var fieldKeys = values.Select(item => item.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingValues = await _db.CentralDocumentMetadataValues
            .Where(item => item.TenantId == tenantId
                && item.DocumentRecordId == record.Id
                && fieldKeys.Contains(item.FieldKey))
            .ToListAsync(cancellationToken);

        foreach (var value in values)
        {
            var existing = existingValues.FirstOrDefault(item =>
                string.Equals(item.FieldKey, value.FieldKey, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(value.FieldValue))
            {
                if (existing is not null && !existing.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.DeletedAt = now;
                    existing.DeletedBy = _currentUserService.UserName ?? "System";
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = _currentUserService.UserName ?? "System";
                    existing.LastModifiedById = GetUserId();
                }

                continue;
            }

            if (existing is null)
            {
                _db.CentralDocumentMetadataValues.Add(new CentralDocumentMetadataValue
                {
                    TenantId = tenantId,
                    DocumentRecordId = record.Id,
                    TemplateCode = record.MetadataTemplateCode,
                    FieldKey = value.FieldKey,
                    FieldLabel = value.FieldLabel,
                    FieldValue = value.FieldValue,
                    ValueType = value.ValueType,
                    Source = "Manual",
                    CapturedAt = now,
                    CapturedById = GetUserId(),
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName ?? "System",
                    CreatedById = GetUserId()
                });
                continue;
            }

            existing.TemplateCode = record.MetadataTemplateCode;
            existing.FieldLabel = value.FieldLabel;
            existing.FieldValue = value.FieldValue;
            existing.ValueType = value.ValueType;
            existing.Source = "Manual";
            existing.CapturedAt = now;
            existing.CapturedById = GetUserId();
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.UpdatedAt = now;
            existing.UpdatedBy = _currentUserService.UserName ?? "System";
            existing.LastModifiedById = GetUserId();
        }

        record.UpdatedAt = now;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);

        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValues = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        var accessRules = await GetActiveAccessRulesAsync(tenantId, cancellationToken);
        var retentionPolicies = await GetActiveRetentionPoliciesAsync(tenantId, cancellationToken);
        return Ok(new { success = true, data = ToRecordDto(record, templatesByCode, metadataValues, accessRules, retentionPolicies) });
    }

    [HttpDelete("records/{id:guid}")]
    public async Task<IActionResult> DeleteRecord(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanArchive, cancellationToken))
        {
            return Forbid();
        }

        record.IsDeleted = true;
        record.DeletedAt = DateTime.UtcNow;
        record.DeletedBy = _currentUserService.UserName ?? "System";
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("records/{id:guid}/versions")]
    public async Task<IActionResult> AddVersion(Guid id, [FromBody] UpsertDocumentVersionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
        {
            return Forbid();
        }

        var version = new CentralDocumentVersion
        {
            TenantId = tenantId,
            DocumentRecordId = record.Id,
            VersionNumber = string.IsNullOrWhiteSpace(request.VersionNumber) ? "v1.0" : request.VersionNumber.Trim(),
            Status = request.Status ?? "Draft",
            RepositoryPath = request.RepositoryPath,
            RenditionPath = request.RenditionPath,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            FileUploadRecordId = request.FileUploadRecordId,
            ChangeSummary = request.ChangeSummary,
            CreatedByUserId = GetUserId(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System"
        };

        record.CurrentVersion = version.VersionNumber;
        record.VersionStatus = version.Status;
        record.RepositoryPath = version.RepositoryPath ?? record.RepositoryPath;
        record.RepositoryStatus = string.IsNullOrWhiteSpace(version.RepositoryPath) ? record.RepositoryStatus : "Linked";
        record.UpdatedAt = DateTime.UtcNow;

        _db.CentralDocumentVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = ToVersionDto(version) });
    }

    [HttpPost("records/{id:guid}/versions/upload")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadVersionFile(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string? versionNumber,
        [FromForm] string? status,
        [FromForm] string? changeSummary,
        [FromForm] string? renditionPath,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "A document file is required." });
        }

        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
        {
            return Forbid();
        }

        await using var stream = file.OpenReadStream();
        var storageResult = await _fileStorageService.UploadFileAsync(new FileUploadRequest
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            Category = "central-dms",
            TenantId = tenantId.ToString(),
            Metadata =
            {
                ["DocumentRecordId"] = record.Id.ToString(),
                ["DocumentReference"] = record.DocumentReference,
                ["SourceModule"] = record.SourceModule
            }
        });

        if (!storageResult.Success)
        {
            return BadRequest(new { success = false, message = storageResult.ErrorMessage ?? "Unable to upload the DMS document file." });
        }

        var now = DateTime.UtcNow;
        var uploadRecord = new FileUploadRecord
        {
            TenantId = tenantId,
            Category = "central-dms",
            FilePath = storageResult.FilePath,
            StoredFileName = storageResult.FileName,
            OriginalFileName = storageResult.OriginalFileName,
            ContentType = storageResult.ContentType,
            FileSize = storageResult.FileSize,
            StorageProvider = storageResult.StorageProvider,
            UploadedByUserId = GetUserId() ?? Guid.Empty,
            VirusScanStatus = FileVirusScanStatus.Skipped,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };
        _db.FileUploadRecords.Add(uploadRecord);

        var isPdf = IsPdfFile(storageResult.ContentType, storageResult.OriginalFileName);
        var pdfRenditionPath = isPdf ? storageResult.FilePath : TrimToNull(renditionPath);
        CentralDocumentRenditionResult? renditionResult = null;

        if (!isPdf && string.IsNullOrWhiteSpace(pdfRenditionPath))
        {
            await using var renditionSourceStream = file.OpenReadStream();
            renditionResult = await _renditionService.CreatePdfRenditionAsync(new CentralDocumentRenditionRequest(
                renditionSourceStream,
                storageResult.OriginalFileName,
                storageResult.ContentType,
                tenantId,
                record.Id,
                record.DocumentReference,
                record.SourceModule), cancellationToken);

            if (renditionResult.Success)
            {
                pdfRenditionPath = renditionResult.RenditionPath;
            }
        }

        var version = new CentralDocumentVersion
        {
            TenantId = tenantId,
            DocumentRecordId = record.Id,
            VersionNumber = string.IsNullOrWhiteSpace(versionNumber) ? NextVersionNumber(record.CurrentVersion) : versionNumber.Trim(),
            Status = string.IsNullOrWhiteSpace(status) ? "Submitted" : status.Trim(),
            RepositoryPath = storageResult.FilePath,
            RenditionPath = pdfRenditionPath,
            FileName = storageResult.OriginalFileName,
            ContentType = storageResult.ContentType,
            FileSize = storageResult.FileSize,
            FileUploadRecordId = uploadRecord.Id,
            ChangeSummary = BuildVersionChangeSummary(changeSummary, renditionResult),
            CreatedByUserId = GetUserId(),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        record.CurrentVersion = version.VersionNumber;
        record.VersionStatus = version.Status;
        record.RepositoryPath = version.RepositoryPath;
        record.RepositoryStatus = "Linked";
        record.AnnotationStatus = !string.IsNullOrWhiteSpace(version.RenditionPath)
            ? "PDF preview ready"
            : renditionResult is { IsSupported: true }
                ? "PDF rendition failed"
            : "PDF rendition required";
        record.UpdatedAt = now;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        _db.CentralDocumentVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, data = ToVersionDto(version) });
    }

    [HttpPut("records/{id:guid}/versions/{versionId:guid}/rendition")]
    public async Task<IActionResult> UpdateVersionRendition(
        Guid id,
        Guid versionId,
        [FromBody] UpdateDocumentRenditionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        var version = await _db.CentralDocumentVersions
            .FirstOrDefaultAsync(item => item.Id == versionId
                && item.DocumentRecordId == record.Id
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (version is null)
        {
            return NotFound(new { success = false, message = "DMS version record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
        {
            return Forbid();
        }

        var renditionPathValue = TrimToNull(request.RenditionPath);
        if (renditionPathValue is null)
        {
            return BadRequest(new { success = false, message = "A PDF rendition path is required." });
        }

        version.RenditionPath = renditionPathValue;
        version.ChangeSummary = string.IsNullOrWhiteSpace(request.ChangeSummary)
            ? version.ChangeSummary
            : request.ChangeSummary.Trim();
        version.UpdatedAt = DateTime.UtcNow;
        version.UpdatedBy = _currentUserService.UserName ?? "System";
        version.LastModifiedById = GetUserId();

        record.AnnotationStatus = string.IsNullOrWhiteSpace(request.AnnotationStatus)
            ? "PDF preview ready"
            : request.AnnotationStatus.Trim();
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = ToVersionDto(version) });
    }

    [HttpPost("records/{id:guid}/versions/{versionId:guid}/rendition/generate")]
    public async Task<IActionResult> GenerateVersionRendition(
        Guid id,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        var version = await _db.CentralDocumentVersions
            .FirstOrDefaultAsync(item => item.Id == versionId
                && item.DocumentRecordId == record.Id
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (version is null)
        {
            return NotFound(new { success = false, message = "DMS version record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(version.RenditionPath))
        {
            return Ok(new { success = true, data = ToVersionDto(version) });
        }

        if (IsPdfFile(version.ContentType, version.FileName) && !string.IsNullOrWhiteSpace(version.RepositoryPath))
        {
            version.RenditionPath = version.RepositoryPath;
            record.AnnotationStatus = "PDF preview ready";
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, data = ToVersionDto(version) });
        }

        if (!version.FileUploadRecordId.HasValue)
        {
            return BadRequest(new
            {
                success = false,
                message = "This DMS version does not have a stored source file that can be converted."
            });
        }

        var uploadRecord = await _db.FileUploadRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == version.FileUploadRecordId.Value
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (uploadRecord is null)
        {
            return NotFound(new { success = false, message = "The stored source file was not found." });
        }

        await using var sourceStream = await _fileStorageService.DownloadFileAsync(uploadRecord.FilePath, uploadRecord.Id);
        var renditionResult = await _renditionService.CreatePdfRenditionAsync(new CentralDocumentRenditionRequest(
            sourceStream,
            uploadRecord.OriginalFileName,
            uploadRecord.ContentType ?? version.ContentType ?? "application/octet-stream",
            tenantId,
            record.Id,
            record.DocumentReference,
            record.SourceModule), cancellationToken);

        if (!renditionResult.Success || string.IsNullOrWhiteSpace(renditionResult.RenditionPath))
        {
            record.AnnotationStatus = renditionResult.IsSupported ? "PDF rendition failed" : "PDF rendition required";
            record.UpdatedAt = DateTime.UtcNow;
            record.UpdatedBy = _currentUserService.UserName ?? "System";
            record.LastModifiedById = GetUserId();
            await _db.SaveChangesAsync(cancellationToken);

            return BadRequest(new
            {
                success = false,
                message = renditionResult.ErrorMessage ?? "Unable to generate the PDF rendition."
            });
        }

        version.RenditionPath = renditionResult.RenditionPath;
        version.ChangeSummary = BuildVersionChangeSummary(version.ChangeSummary, renditionResult);
        version.UpdatedAt = DateTime.UtcNow;
        version.UpdatedBy = _currentUserService.UserName ?? "System";
        version.LastModifiedById = GetUserId();

        record.AnnotationStatus = "PDF preview ready";
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = ToVersionDto(version) });
    }

    [HttpGet("records/{id:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> DownloadVersionFile(
        Guid id,
        Guid versionId,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        var version = await _db.CentralDocumentVersions
            .FirstOrDefaultAsync(item => item.Id == versionId
                && item.DocumentRecordId == record.Id
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (version is null)
        {
            return NotFound(new { success = false, message = "DMS version record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanView, cancellationToken))
        {
            return Forbid();
        }

        var normalizedFormat = string.IsNullOrWhiteSpace(format) ? "pdf" : format.Trim().ToLowerInvariant();
        if (normalizedFormat is not ("pdf" or "word" or "docx"))
        {
            return BadRequest(new { success = false, message = "Download format must be pdf or word." });
        }

        if (normalizedFormat == "pdf")
        {
            var pdfFile = await OpenPdfVersionFileAsync(record, version, tenantId, cancellationToken);
            if (!pdfFile.Success || pdfFile.Stream is null)
            {
                return BadRequest(new { success = false, message = pdfFile.ErrorMessage ?? "The PDF download could not be prepared." });
            }

            return File(
                pdfFile.Stream,
                "application/pdf",
                SafeDownloadFileName(pdfFile.FileName, record.DocumentReference, ".pdf"));
        }

        var sourceFile = await OpenVersionSourceFileAsync(version, tenantId, cancellationToken);
        if (sourceFile.Success && sourceFile.Stream is not null && IsWordFile(sourceFile.ContentType, sourceFile.FileName))
        {
            return File(
                sourceFile.Stream,
                WordDocumentContentType,
                SafeDownloadFileName(sourceFile.FileName, record.DocumentReference, ".docx"));
        }

        sourceFile.Stream?.Dispose();

        var pdfSource = await OpenPdfVersionFileAsync(record, version, tenantId, cancellationToken);
        if (!pdfSource.Success || pdfSource.Stream is null)
        {
            return BadRequest(new { success = false, message = pdfSource.ErrorMessage ?? "The Word download could not be prepared." });
        }

        await using (pdfSource.Stream)
        {
            var wordResult = await _renditionService.CreateEditableWordCopyFromPdfAsync(
                new CentralDocumentWordCopyRequest(
                    pdfSource.Stream,
                    pdfSource.FileName ?? version.FileName ?? record.DocumentReference,
                    record.Title,
                    record.DocumentReference),
                cancellationToken);

            if (!wordResult.Success || wordResult.WordStream is null)
            {
                return BadRequest(new { success = false, message = wordResult.ErrorMessage ?? "Unable to create an editable Word copy." });
            }

            return File(
                wordResult.WordStream,
                WordDocumentContentType,
                SafeDownloadFileName(wordResult.FileName, record.DocumentReference, ".docx"));
        }
    }

    [HttpGet("records/{id:guid}/content")]
    public async Task<IActionResult> GetRecordContent(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanView, cancellationToken))
        {
            return Forbid();
        }

        var version = await ResolveCurrentVersionAsync(record, tenantId, cancellationToken);
        if (version is null)
        {
            return BadRequest(new { success = false, message = "This DMS record does not have a viewable current version." });
        }

        var pdfFile = await OpenPdfVersionFileAsync(record, version, tenantId, cancellationToken);
        if (!pdfFile.Success || pdfFile.Stream is null)
        {
            return BadRequest(new { success = false, message = pdfFile.ErrorMessage ?? "The PDF preview could not be prepared." });
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{SafeDownloadFileName(pdfFile.FileName, record.DocumentReference, ".pdf")}\"";
        return File(pdfFile.Stream, "application/pdf");
    }

    [HttpGet("records/{id:guid}/versions/{versionId:guid}/content")]
    public async Task<IActionResult> GetVersionContent(Guid id, Guid versionId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        var version = await _db.CentralDocumentVersions
            .FirstOrDefaultAsync(item => item.Id == versionId
                && item.DocumentRecordId == record.Id
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (version is null)
        {
            return NotFound(new { success = false, message = "DMS version record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanView, cancellationToken))
        {
            return Forbid();
        }

        var pdfFile = await OpenPdfVersionFileAsync(record, version, tenantId, cancellationToken);
        if (!pdfFile.Success || pdfFile.Stream is null)
        {
            return BadRequest(new { success = false, message = pdfFile.ErrorMessage ?? "The PDF preview could not be prepared." });
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{SafeDownloadFileName(pdfFile.FileName, record.DocumentReference, ".pdf")}\"";
        return File(pdfFile.Stream, "application/pdf");
    }

    [HttpPost("source-handoffs/register")]
    public async Task<IActionResult> RegisterSourceHandoff([FromBody] SourceDocumentHandoffRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.SourceModule))
        {
            return BadRequest(new { success = false, message = "Title and source module are required." });
        }

        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;
        var sourceReference = TrimToNull(request.SourceRecordReference);
        var requestedAccessProfile = TrimToNull(request.AccessProfile);
        if (!CanUseSourceModuleForDms(request.SourceModule))
        {
            return Forbid();
        }

        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.SourceModule == request.SourceModule.Trim()
                && ((request.SourceRecordId.HasValue && item.SourceRecordId == request.SourceRecordId)
                    || (!string.IsNullOrWhiteSpace(sourceReference) && item.SourceRecordReference == sourceReference)), cancellationToken);

        if (record is null)
        {
            var newAccessProfile = requestedAccessProfile ?? "Module restricted";
            if (!string.Equals(newAccessProfile, "Module restricted", StringComparison.OrdinalIgnoreCase)
                && !await CanAssignAccessProfileAsync(tenantId, newAccessProfile, cancellationToken))
            {
                return Forbid();
            }

            record = new CentralDocumentRecord
            {
                TenantId = tenantId,
                DocumentReference = await NextDocumentReferenceAsync(tenantId, cancellationToken),
                Title = request.Title.Trim(),
                SourceModule = request.SourceModule.Trim(),
                SourceLabel = string.IsNullOrWhiteSpace(request.SourceLabel)
                    ? $"Source: {request.SourceModule.Trim()} -> Central DMS"
                    : request.SourceLabel.Trim(),
                SourceEntityType = TrimToNull(request.SourceEntityType),
                SourceRecordReference = sourceReference,
                SourceRecordId = request.SourceRecordId,
                MetadataTemplateCode = TrimToNull(request.MetadataTemplateCode),
                RepositoryStatus = "Not linked",
                VersionStatus = "Draft",
                AnnotationStatus = "Not required",
                CommentStatus = "No comments",
                AccessProfile = newAccessProfile,
                RetentionStatus = "Current",
                LifecycleStatus = "Draft",
                Notes = TrimToNull(request.Notes),
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = GetUserId()
            };
            _db.CentralDocumentRecords.Add(record);
        }
        else
        {
            if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken))
            {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(requestedAccessProfile)
                && !string.Equals(requestedAccessProfile, record.AccessProfile, StringComparison.OrdinalIgnoreCase)
                && !await CanAssignAccessProfileAsync(tenantId, requestedAccessProfile, cancellationToken))
            {
                return Forbid();
            }

            record.Title = request.Title.Trim();
            record.SourceLabel = string.IsNullOrWhiteSpace(request.SourceLabel) ? record.SourceLabel : request.SourceLabel.Trim();
            record.SourceEntityType = TrimToNull(request.SourceEntityType) ?? record.SourceEntityType;
            record.MetadataTemplateCode = TrimToNull(request.MetadataTemplateCode) ?? record.MetadataTemplateCode;
            record.AccessProfile = requestedAccessProfile ?? record.AccessProfile;
            record.Notes = TrimToNull(request.Notes) ?? record.Notes;
            record.UpdatedAt = now;
            record.UpdatedBy = _currentUserService.UserName ?? "System";
            record.LastModifiedById = GetUserId();
        }

        if (request.MetadataValues?.Count > 0)
        {
            await UpsertMetadataValuesAsync(tenantId, record, request.MetadataValues, "Source module", now, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValues = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new
            {
                recordId = record.Id,
                dmsReference = record.DocumentReference,
                status = record.LifecycleStatus,
                metadataCompleteness = BuildMetadataCompleteness(record, ResolveTemplate(record, templatesByCode), metadataValues),
                sourceLabel = record.SourceLabel
            }
        });
    }

    [HttpPut("records/{id:guid}/versions/{versionId:guid}/status")]
    public async Task<IActionResult> UpdateVersionStatus(Guid id, Guid versionId, [FromBody] UpdateDocumentVersionStatusRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        var version = await _db.CentralDocumentVersions
            .FirstOrDefaultAsync(item => item.Id == versionId
                && item.DocumentRecordId == record.Id
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (version is null)
        {
            return NotFound(new { success = false, message = "DMS version record was not found." });
        }

        if (!await CanUseRecordActionAsync(
                tenantId,
                record,
                rule => string.Equals(request.Status, "Archived", StringComparison.OrdinalIgnoreCase)
                    ? rule.CanArchive
                    : rule.CanApprove,
                cancellationToken))
        {
            return Forbid();
        }

        var status = TrimOrDefault(request.Status, "Draft");
        var now = DateTime.UtcNow;

        if (string.Equals(status, "Current", StringComparison.OrdinalIgnoreCase))
        {
            var currentVersions = await _db.CentralDocumentVersions
                .Where(item => item.DocumentRecordId == record.Id
                    && item.TenantId == tenantId
                    && item.Id != version.Id
                    && !item.IsDeleted
                    && item.Status == "Current")
                .ToListAsync(cancellationToken);

            foreach (var currentVersion in currentVersions)
            {
                currentVersion.Status = "Published";
                currentVersion.UpdatedAt = now;
                currentVersion.UpdatedBy = _currentUserService.UserName ?? "System";
                currentVersion.LastModifiedById = GetUserId();
            }

            version.PublishedAt ??= now;
            version.PublishedById ??= GetUserId();
            record.CurrentVersion = version.VersionNumber;
            record.VersionStatus = "Current";
            record.RepositoryPath = version.RepositoryPath ?? record.RepositoryPath;
            record.RepositoryStatus = string.IsNullOrWhiteSpace(record.RepositoryPath) ? record.RepositoryStatus : "Linked";
        }
        else if (string.Equals(status, "Superseded", StringComparison.OrdinalIgnoreCase)
            && string.Equals(record.CurrentVersion, version.VersionNumber, StringComparison.OrdinalIgnoreCase))
        {
            record.VersionStatus = "Superseded";
        }
        else if (string.Equals(record.CurrentVersion, version.VersionNumber, StringComparison.OrdinalIgnoreCase))
        {
            record.VersionStatus = status;
        }

        version.Status = status;
        version.ChangeSummary = string.IsNullOrWhiteSpace(request.ChangeSummary)
            ? version.ChangeSummary
            : request.ChangeSummary.Trim();
        version.UpdatedAt = now;
        version.UpdatedBy = _currentUserService.UserName ?? "System";
        version.LastModifiedById = GetUserId();
        record.UpdatedAt = now;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);

        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValues = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        var accessRules = await GetActiveAccessRulesAsync(tenantId, cancellationToken);
        var retentionPolicies = await GetActiveRetentionPoliciesAsync(tenantId, cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                record = ToRecordDto(record, templatesByCode, metadataValues, accessRules, retentionPolicies),
                version = ToVersionDto(version)
            }
        });
    }

    [HttpPost("records/{id:guid}/annotation-reviews")]
    public async Task<IActionResult> AddAnnotationReview(Guid id, [FromBody] UpsertAnnotationReviewRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanAnnotate, cancellationToken))
        {
            return Forbid();
        }

        var review = new CentralDocumentAnnotationReview
        {
            TenantId = tenantId,
            DocumentRecordId = record.Id,
            DocumentVersionId = request.DocumentVersionId,
            ReviewTitle = string.IsNullOrWhiteSpace(request.ReviewTitle) ? "PDF annotation review" : request.ReviewTitle.Trim(),
            Status = request.Status ?? "Open",
            SyncfusionAnnotationStatus = request.SyncfusionAnnotationStatus ?? "Not started",
            AssignedReviewerId = request.AssignedReviewerId,
            DueDate = request.DueDate,
            ReviewNotes = request.ReviewNotes,
            AnnotationStateJson = request.AnnotationStateJson,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        record.AnnotationStatus = review.SyncfusionAnnotationStatus;
        record.CommentStatus = review.Status == "Closed" ? "Resolved" : "Open comments";
        record.UpdatedAt = DateTime.UtcNow;

        _db.CentralDocumentAnnotationReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = ToAnnotationReviewDto(review) });
    }

    [HttpPut("records/{id:guid}/annotation-reviews/{reviewId:guid}")]
    public async Task<IActionResult> UpdateAnnotationReview(Guid id, Guid reviewId, [FromBody] UpdateAnnotationReviewRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var record = await _db.CentralDocumentRecords
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

        if (record is null)
        {
            return NotFound(new { success = false, message = "DMS document record was not found." });
        }

        var review = await _db.CentralDocumentAnnotationReviews
            .FirstOrDefaultAsync(item => item.Id == reviewId
                && item.DocumentRecordId == record.Id
                && item.TenantId == tenantId
                && !item.IsDeleted, cancellationToken);

        if (review is null)
        {
            return NotFound(new { success = false, message = "DMS annotation review was not found." });
        }

        if (!await CanUseRecordActionAsync(tenantId, record, rule => rule.CanAnnotate, cancellationToken))
        {
            return Forbid();
        }

        var status = TrimOrDefault(request.Status, review.Status);
        var annotationStatus = TrimOrDefault(request.SyncfusionAnnotationStatus, review.SyncfusionAnnotationStatus);
        var now = DateTime.UtcNow;

        review.Status = status;
        review.SyncfusionAnnotationStatus = annotationStatus;
        review.ReviewNotes = request.ReviewNotes is null ? review.ReviewNotes : TrimToNull(request.ReviewNotes);
        review.AnnotationStateJson = request.AnnotationStateJson is null ? review.AnnotationStateJson : TrimToNull(request.AnnotationStateJson);
        review.UpdatedAt = now;
        review.UpdatedBy = _currentUserService.UserName ?? "System";
        review.LastModifiedById = GetUserId();

        if (string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            review.ClosedAt ??= now;
            review.ClosedById ??= GetUserId();
            record.CommentStatus = "Resolved";
        }
        else
        {
            review.ClosedAt = null;
            review.ClosedById = null;
            record.CommentStatus = string.Equals(status, "Returned", StringComparison.OrdinalIgnoreCase)
                ? "Returned for action"
                : "Open comments";
        }

        record.AnnotationStatus = review.SyncfusionAnnotationStatus;
        record.UpdatedAt = now;
        record.UpdatedBy = _currentUserService.UserName ?? "System";
        record.LastModifiedById = GetUserId();

        await _db.SaveChangesAsync(cancellationToken);

        var templatesByCode = await GetTemplatesByCodeAsync(tenantId, cancellationToken);
        var metadataValues = await GetMetadataValuesAsync(tenantId, record.Id, cancellationToken);
        var accessRules = await GetActiveAccessRulesAsync(tenantId, cancellationToken);
        var retentionPolicies = await GetActiveRetentionPoliciesAsync(tenantId, cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                record = ToRecordDto(record, templatesByCode, metadataValues, accessRules, retentionPolicies),
                review = ToAnnotationReviewDto(review)
            }
        });
    }

    [HttpPost("metadata-templates")]
    public async Task<IActionResult> CreateMetadataTemplate([FromBody] UpsertMetadataTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!IsDmsAccessAdministrator())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.DocumentType) || string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            return BadRequest(new { success = false, message = "Module, document type, and template code are required." });
        }

        var tenantId = GetTenantId();
        var template = new CentralDocumentMetadataTemplateEntity
        {
            TenantId = tenantId,
            Module = request.Module.Trim(),
            DocumentType = request.DocumentType.Trim(),
            TemplateCode = request.TemplateCode.Trim(),
            SourceLabel = string.IsNullOrWhiteSpace(request.SourceLabel)
                ? $"Source: {request.Module.Trim()} -> Central DMS"
                : request.SourceLabel.Trim(),
            RequiredFieldsJson = JsonSerializer.Serialize(request.RequiredFields ?? []),
            RelationshipsJson = JsonSerializer.Serialize(request.Relationships ?? []),
            RetentionRule = request.RetentionRule ?? string.Empty,
            AccessProfile = request.AccessProfile ?? string.Empty,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        _db.CentralDocumentMetadataTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = ToTemplateDto(template) });
    }

    [HttpPost("access-rules")]
    public async Task<IActionResult> CreateAccessRule([FromBody] UpsertAccessRuleRequest request, CancellationToken cancellationToken)
    {
        if (!IsDmsAccessAdministrator())
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var rule = new CentralDocumentAccessRule
        {
            TenantId = tenantId,
            AccessProfile = string.IsNullOrWhiteSpace(request.AccessProfile) ? "Module restricted" : request.AccessProfile.Trim(),
            Module = request.Module,
            RoleName = request.RoleName,
            PermissionKey = request.PermissionKey,
            CanView = request.CanView ?? true,
            CanUpload = request.CanUpload ?? false,
            CanAnnotate = request.CanAnnotate ?? false,
            CanApprove = request.CanApprove ?? false,
            CanArchive = request.CanArchive ?? false,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        _db.CentralDocumentAccessRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = rule });
    }

    [HttpGet("access-rules")]
    public async Task<IActionResult> GetAccessRules(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var rules = await _db.CentralDocumentAccessRules
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.AccessProfile)
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = rules });
    }

    [HttpPost("retention-policies")]
    public async Task<IActionResult> CreateRetentionPolicy([FromBody] UpsertRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        if (!IsDmsAccessAdministrator())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.PolicyCode) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "Policy code and name are required." });
        }

        var tenantId = GetTenantId();
        var policy = new CentralDocumentRetentionPolicy
        {
            TenantId = tenantId,
            PolicyCode = request.PolicyCode.Trim(),
            Name = request.Name.Trim(),
            Module = request.Module,
            DocumentType = request.DocumentType,
            RetentionDays = Math.Clamp(request.RetentionDays ?? 2555, 1, 36500),
            RequiresLegalHoldReview = request.RequiresLegalHoldReview ?? false,
            AllowArchive = request.AllowArchive ?? true,
            AllowDestruction = request.AllowDestruction ?? false,
            IsActive = request.IsActive ?? true,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = GetUserId()
        };

        _db.CentralDocumentRetentionPolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = policy });
    }

    [HttpGet("retention-policies")]
    public async Task<IActionResult> GetRetentionPolicies(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var policies = await _db.CentralDocumentRetentionPolicies
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .OrderBy(item => item.PolicyCode)
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = policies });
    }

    private static readonly GeneratedDocumentTemplateDefinition[] GeneratedDocumentTemplates =
    [
        Template(
            "EST-OFFER-LETTER",
            "Offer Letter",
            "Offer Letter - {{ApplicantName}}",
            "EST-ALLOC-ROE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "LandUse", "Premium", "GroundRent", "OfferExpiryDate"],
            """
            {{Today}}

            {{ApplicantName}}

            Dear {{ApplicantName}},

            OFFER OF ALLOCATION

            We refer to your application under reference {{CaseReference}}. The Estate Section has approved an offer for {{PropertyNumber}} for {{LandUse}} use.

            The offer is subject to payment of the premium stated as {{Premium}} and ground rent stated as {{GroundRent}}. This offer remains valid until {{OfferExpiryDate}} and is subject to all Estate and records controls.

            Please contact the Estate Section for completion of acceptance and records processing.

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-RIGHT-ENTRY",
            "Right of Entry",
            "Right of Entry - {{ApplicantName}}",
            "EST-ALLOC-ROE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "SiteLocation", "EntryDate", "Purpose"],
            """
            {{Today}}

            RIGHT OF ENTRY

            Permission is hereby granted to {{ApplicantName}} to enter {{PropertyNumber}} at {{SiteLocation}} for {{Purpose}}.

            This Right of Entry is issued under Estate control reference {{CaseReference}} and does not replace final title, lease, or allocation documentation.

            Effective entry date: {{EntryDate}}

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-PROPOSAL-LETTER",
            "Proposal Letter",
            "Proposal Letter - {{ApplicantName}}",
            "EST-REGULAR",
            ["ApplicantName", "CaseReference", "PropertyNumber", "ProposalDecision", "RequiredAction", "ResponseDueDate"],
            """
            {{Today}}

            {{ApplicantName}}

            PROPOSAL LETTER

            Following review of Estate case {{CaseReference}} for {{PropertyNumber}}, the proposal decision is recorded as {{ProposalDecision}}.

            Required action: {{RequiredAction}}

            Kindly respond on or before {{ResponseDueDate}} so the Estate Section may continue processing.

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-REMINDER",
            "Reminder Letter",
            "Reminder - {{ApplicantName}}",
            "EST-REG-FILE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "OutstandingItem", "ResponseDueDate"],
            """
            {{Today}}

            {{ApplicantName}}

            REMINDER NOTICE

            Our records show that {{OutstandingItem}} remains outstanding for Estate case {{CaseReference}} concerning {{PropertyNumber}}.

            Please regularise this item on or before {{ResponseDueDate}} to avoid processing delays.

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-RATE-REVISION",
            "Rate Revision Notice",
            "Rate Revision - {{ApplicantName}}",
            "EST-REG-FILE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "OriginalAmount", "RevisedAmount", "EffectiveDate", "PaymentDeadline"],
            """
            {{Today}}

            {{ApplicantName}}

            RATE REVISION NOTICE

            Please be informed that the rate payable for {{PropertyNumber}} under Estate reference {{CaseReference}} has been revised.

            Original amount: {{OriginalAmount}}
            Revised amount: {{RevisedAmount}}
            Effective date: {{EffectiveDate}}
            Payment deadline: {{PaymentDeadline}}

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-GR-DEMAND",
            "Ground Rent Arrears Demand Letter",
            "Ground Rent Demand - {{ApplicantName}}",
            "EST-REG-FILE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "OutstandingAmount", "GroundRent", "PaymentDeadline"],
            """
            {{Today}}

            {{ApplicantName}}

            GROUND RENT ARREARS DEMAND

            Our records show outstanding ground rent for {{PropertyNumber}} under Estate reference {{CaseReference}}.

            Ground rent payable: {{GroundRent}}
            Outstanding amount: {{OutstandingAmount}}
            Payment deadline: {{PaymentDeadline}}

            Please settle the arrears or contact the Estate Section for reconciliation.

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-CTC",
            "Certified True Copy",
            "Certified True Copy - {{PropertyNumber}}",
            "EST-REC-AMD",
            ["ApplicantName", "CaseReference", "PropertyNumber", "OriginalDocument", "CertificationPurpose"],
            """
            CERTIFIED TRUE COPY

            This is to certify that the attached or referenced copy of {{OriginalDocument}} for {{PropertyNumber}} has been checked against Estate records under reference {{CaseReference}}.

            Purpose of certification: {{CertificationPurpose}}

            Certified for: {{ApplicantName}}

            Date: {{Today}}
            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-SEARCH-REPORT",
            "Search Report",
            "Search Report - {{PropertyNumber}}",
            "EST-REG-FILE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "SearchPurpose", "SearchOutcome"],
            """
            ESTATE RECORDS SEARCH REPORT

            Search reference: {{CaseReference}}
            Property / plot number: {{PropertyNumber}}
            Requested by: {{ApplicantName}}
            Search purpose: {{SearchPurpose}}

            Search outcome:
            {{SearchOutcome}}

            Date: {{Today}}
            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-AGENCY-NOTICE",
            "Agency Notification Letter",
            "Agency Notification - {{PropertyNumber}}",
            "EST-REC-AMD",
            ["AgencyName", "CaseReference", "PropertyNumber", "NotificationSubject", "EffectiveDate"],
            """
            {{Today}}

            {{AgencyName}}

            AGENCY NOTIFICATION

            Please be informed that Estate records for {{PropertyNumber}} have been updated under reference {{CaseReference}}.

            Subject: {{NotificationSubject}}
            Effective date: {{EffectiveDate}}

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-INVITATION",
            "Invitation Letter",
            "Invitation - {{ApplicantName}}",
            "EST-REGULAR",
            ["ApplicantName", "CaseReference", "MeetingDate", "MeetingVenue", "MeetingPurpose"],
            """
            {{Today}}

            {{ApplicantName}}

            INVITATION

            You are invited to attend a meeting with the Estate Section concerning reference {{CaseReference}}.

            Date: {{MeetingDate}}
            Venue: {{MeetingVenue}}
            Purpose: {{MeetingPurpose}}

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-LEASE-REQUEST",
            "Lease Request Letter",
            "Lease Request - {{ApplicantName}}",
            "EST-LEASE-XFER",
            ["ApplicantName", "CaseReference", "PropertyNumber", "LeasePurpose", "RequiredDocuments"],
            """
            {{Today}}

            {{ApplicantName}}

            LEASE REQUEST

            We acknowledge your request concerning {{PropertyNumber}} under Estate reference {{CaseReference}}.

            Lease purpose: {{LeasePurpose}}
            Required documents: {{RequiredDocuments}}

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-RENT-CARD",
            "Rent Card",
            "Rent Card - {{ApplicantName}}",
            "EST-ALLOC-ROE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "HouseType", "RentCardNumber", "DateOfTenancy", "GroundRent"],
            """
            RENT CARD

            Tenant / purchaser: {{ApplicantName}}
            Property / unit: {{PropertyNumber}}
            House type: {{HouseType}}
            Rent card number: {{RentCardNumber}}
            Date of tenancy: {{DateOfTenancy}}
            Ground rent: {{GroundRent}}
            Estate reference: {{CaseReference}}

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """),
        Template(
            "EST-COMPLETION",
            "Completion Letter",
            "Completion Letter - {{ApplicantName}}",
            "EST-ALLOC-ROE",
            ["ApplicantName", "CaseReference", "PropertyNumber", "CompletionBasis", "EffectiveDate", "RequiredNextAction"],
            """
            {{Today}}

            {{ApplicantName}}

            COMPLETION LETTER

            The Estate Section confirms completion of {{CompletionBasis}} for {{PropertyNumber}} under reference {{CaseReference}}.

            Effective date: {{EffectiveDate}}
            Required next action: {{RequiredNextAction}}

            Prepared by: {{PreparedBy}}
            Source: Estate / Facility -> Central DMS
            """)
    ];

    private static GeneratedDocumentTemplateDefinition Template(
        string templateCode,
        string title,
        string titleTemplate,
        string metadataTemplateCode,
        IReadOnlyList<string> mergeFields,
        string body) =>
        new(
            templateCode,
            title,
            titleTemplate,
            "Estate",
            "Source: Estate / Facility -> Central DMS",
            title,
            metadataTemplateCode,
            "Estate + Records",
            mergeFields,
            body.Trim(),
            true,
            "Head of Estate",
            "Authorised Signatory",
            "Email / Print");

    private static object ToGeneratedTemplateDto(GeneratedDocumentTemplateDefinition template) => new
    {
        template.TemplateCode,
        template.Title,
        template.TitleTemplate,
        template.Module,
        template.SourceLabel,
        template.DocumentType,
        template.MetadataTemplateCode,
        template.AccessProfile,
        template.MergeFields,
        template.Body,
        template.RequiresApproval,
        template.ApprovalRole,
        template.SignatureRole,
        template.DefaultDispatchChannel,
        template.IsActive
    };

    private static object ToGeneratedTemplateDto(CentralDocumentGenerationTemplate template) => new
    {
        template.Id,
        template.TemplateCode,
        template.Title,
        template.TitleTemplate,
        template.Module,
        template.SourceLabel,
        template.DocumentType,
        template.MetadataTemplateCode,
        template.AccessProfile,
        mergeFields = DeserializeStringArray(template.MergeFieldsJson),
        template.Body,
        template.RequiresApproval,
        template.ApprovalRole,
        template.SignatureRole,
        template.DefaultDispatchChannel,
        template.IsActive,
        template.UpdatedAt,
        template.CreatedAt
    };

    private static Dictionary<string, string> BuildMergeValues(
        GeneratedDocumentTemplateDefinition template,
        GenerateDocumentTemplateRequest request,
        DateTime now)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Today"] = now.ToString("dd MMMM yyyy"),
            ["PreparedBy"] = string.IsNullOrWhiteSpace(request.PreparedBy) ? "Estate Section" : request.PreparedBy.Trim(),
            ["ApplicantName"] = TrimOrDefault(request.ApplicantName, "Applicant"),
            ["CaseReference"] = TrimOrDefault(request.CaseReference, request.SourceRecordReference ?? "Pending reference"),
            ["SourceRecordReference"] = TrimOrDefault(request.SourceRecordReference, request.CaseReference ?? "Pending reference"),
            ["DocumentType"] = template.DocumentType,
            ["TemplateCode"] = template.TemplateCode,
            ["Purpose"] = TrimOrDefault(request.Purpose, template.DocumentType)
        };

        foreach (var item in request.MergeValues ?? new Dictionary<string, string?>())
        {
            if (!string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            {
                values[item.Key.Trim()] = item.Value!.Trim();
            }
        }

        foreach (var field in template.MergeFields)
        {
            values.TryAdd(field, $"[{field}]");
        }

        return values;
    }

    private static string MergeTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        var merged = template;
        foreach (var item in values)
        {
            merged = merged.Replace($"{{{{{item.Key}}}}}", item.Value, StringComparison.OrdinalIgnoreCase);
        }

        return merged;
    }

    private static IReadOnlyList<UpsertDocumentMetadataValueRequest> BuildGeneratedDocumentMetadataValues(
        GeneratedDocumentTemplateDefinition template,
        IReadOnlyDictionary<string, string> values,
        string sourceRecordReference)
    {
        var metadata = new List<UpsertDocumentMetadataValueRequest>
        {
            new("documenttype", "Document type", template.DocumentType, "text"),
            new("templatecode", "Template code", template.TemplateCode, "text"),
            new("sourcerecordreference", "Source record reference", sourceRecordReference, "text"),
            new("sourcelabel", "Source label", template.SourceLabel, "text")
        };

        foreach (var field in template.MergeFields)
        {
            metadata.Add(new UpsertDocumentMetadataValueRequest(
                NormalizeMetadataField(field),
                field,
                values.TryGetValue(field, out var value) ? value : null,
                "text"));
        }

        return metadata;
    }

    private static byte[] BuildSimplePdf(string title, string content)
    {
        var lines = WrapPdfLines($"{title}\n\n{content}", 88).Take(58).ToList();
        var contentStream = new StringBuilder();
        contentStream.AppendLine("BT");
        contentStream.AppendLine("/F1 10 Tf");
        contentStream.AppendLine("50 790 Td");

        foreach (var line in lines)
        {
            contentStream.Append('(').Append(EscapePdfText(line)).AppendLine(") Tj");
            contentStream.AppendLine("0 -14 Td");
        }

        contentStream.AppendLine("ET");
        var streamBytes = Encoding.ASCII.GetBytes(contentStream.ToString());
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {streamBytes.Length} >>\nstream\n{contentStream}endstream"
        };

        var pdf = new StringBuilder();
        var offsets = new List<int> { 0 };
        pdf.AppendLine("%PDF-1.4");

        foreach (var obj in objects.Select((value, index) => new { value, number = index + 1 }))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(obj.number).AppendLine(" 0 obj");
            pdf.AppendLine(obj.value);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.Append(offset.ToString("0000000000")).AppendLine(" 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static IEnumerable<string> WrapPdfLines(string value, int maxLength)
    {
        foreach (var rawLine in value.Replace("\r", string.Empty).Split('\n'))
        {
            var words = rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            var line = new StringBuilder();
            foreach (var word in words)
            {
                if (line.Length + word.Length + 1 > maxLength)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(word);
            }

            yield return line.ToString();
        }
    }

    private static string EscapePdfText(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "generated-document" : cleaned;
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

    private async Task<IReadOnlyDictionary<string, CentralDocumentMetadataTemplateEntity>> GetTemplatesByCodeAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var templates = await _db.CentralDocumentMetadataTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .ToListAsync(cancellationToken);

        return templates
            .Where(item => !string.IsNullOrWhiteSpace(item.TemplateCode))
            .GroupBy(item => item.TemplateCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<CentralDocumentAccessRule>> GetActiveAccessRulesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await _db.CentralDocumentAccessRules
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.AccessProfile)
            .ThenBy(item => item.Module)
            .ThenBy(item => item.RoleName)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<CentralDocumentRetentionPolicy>> GetActiveRetentionPoliciesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => await _db.CentralDocumentRetentionPolicies
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.PolicyCode)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<CentralDocumentMetadataValue>> GetMetadataValuesAsync(
        Guid tenantId,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        return await _db.CentralDocumentMetadataValues
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.DocumentRecordId == recordId
                && !item.IsDeleted)
            .OrderBy(item => item.FieldLabel)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CentralDocumentMetadataValue>>> GetMetadataValuesByRecordAsync(
        Guid tenantId,
        IEnumerable<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        var ids = recordIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CentralDocumentMetadataValue>>();
        }

        var values = await _db.CentralDocumentMetadataValues
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && ids.Contains(item.DocumentRecordId)
                && !item.IsDeleted)
            .OrderBy(item => item.FieldLabel)
            .ToListAsync(cancellationToken);

        return values
            .GroupBy(item => item.DocumentRecordId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CentralDocumentMetadataValue>)group.ToList());
    }

    private async Task<List<CentralDocumentRecord>> FilterViewableRecordsAsync(
        Guid tenantId,
        IEnumerable<CentralDocumentRecord> records,
        CancellationToken cancellationToken)
    {
        var viewableRecords = new List<CentralDocumentRecord>();
        foreach (var record in records)
        {
            if (await CanUseRecordActionAsync(tenantId, record, rule => rule.CanView, cancellationToken))
            {
                viewableRecords.Add(record);
            }
        }

        return viewableRecords;
    }

    private async Task<List<CentralDocumentVersion>> FilterViewableVersionsAsync(
        Guid tenantId,
        IEnumerable<CentralDocumentVersion> versions,
        CancellationToken cancellationToken)
    {
        var viewableVersions = new List<CentralDocumentVersion>();
        foreach (var version in versions)
        {
            if (await CanUseRecordActionAsync(tenantId, version.DocumentRecord, rule => rule.CanView, cancellationToken))
            {
                viewableVersions.Add(version);
            }
        }

        return viewableVersions;
    }

    private static IReadOnlyList<CentralDocumentMetadataValue> MetadataValuesFor(
        Guid recordId,
        IReadOnlyDictionary<Guid, IReadOnlyList<CentralDocumentMetadataValue>> valuesByRecord)
        => valuesByRecord.TryGetValue(recordId, out var values) ? values : [];

    private static object ToRecordDto(
        CentralDocumentRecord record,
        IReadOnlyDictionary<string, CentralDocumentMetadataTemplateEntity>? templatesByCode = null,
        IReadOnlyList<CentralDocumentMetadataValue>? metadataValues = null,
        IReadOnlyList<CentralDocumentAccessRule>? accessRules = null,
        IReadOnlyList<CentralDocumentRetentionPolicy>? retentionPolicies = null) => new
    {
        record.Id,
        record.DocumentReference,
        record.Title,
        record.SourceModule,
        record.SourceLabel,
        record.SourceEntityType,
        record.SourceRecordReference,
        record.SourceRecordId,
        record.MetadataTemplateCode,
        record.RepositoryStatus,
        RepositoryPath = IsRepositoryLinked(record) ? RecordContentUrl(record.Id) : null,
        record.ExternalDocumentUrl,
        record.CurrentVersion,
        record.VersionStatus,
        record.AnnotationStatus,
        record.CommentStatus,
        record.AccessProfile,
        record.RetentionStatus,
        record.LifecycleStatus,
        record.EffectiveDate,
        record.ExpiryDate,
        record.ReviewDate,
        record.PublishedAt,
        record.Notes,
        record.CreatedAt,
        record.UpdatedAt,
        metadataValues = (metadataValues ?? []).Select(ToMetadataValueDto),
        metadataCompleteness = BuildMetadataCompleteness(record, ResolveTemplate(record, templatesByCode), metadataValues ?? []),
        accessRetentionCompliance = BuildAccessRetentionCompliance(record, ResolveTemplate(record, templatesByCode), accessRules ?? [], retentionPolicies ?? [])
    };

    private static object ToRegisterDto(
        CentralDocumentRecord record,
        IReadOnlyDictionary<string, CentralDocumentMetadataTemplateEntity>? templatesByCode = null,
        IReadOnlyList<CentralDocumentMetadataValue>? metadataValues = null) => new
    {
        id = record.Id,
        documentReference = record.DocumentReference,
        title = record.Title,
        module = record.SourceModule,
        sourceRecord = record.SourceRecordReference ?? record.SourceRecordId?.ToString() ?? string.Empty,
        templateCode = record.MetadataTemplateCode ?? string.Empty,
        version = record.CurrentVersion ?? string.Empty,
        repositoryStatus = record.RepositoryStatus,
        annotationStatus = record.AnnotationStatus,
        commentStatus = record.CommentStatus,
        retentionStatus = record.RetentionStatus,
        sourceLabel = record.SourceLabel,
        metadataCompleteness = BuildMetadataCompleteness(record, ResolveTemplate(record, templatesByCode), metadataValues ?? [])
    };

    private static CentralDocumentMetadataTemplateEntity? ResolveTemplate(
        CentralDocumentRecord record,
        IReadOnlyDictionary<string, CentralDocumentMetadataTemplateEntity>? templatesByCode)
    {
        if (string.IsNullOrWhiteSpace(record.MetadataTemplateCode) || templatesByCode is null)
        {
            return null;
        }

        return templatesByCode.TryGetValue(record.MetadataTemplateCode.Trim(), out var template) ? template : null;
    }

    private static CentralDocumentMetadataCompletenessDto BuildMetadataCompleteness(
        CentralDocumentRecord record,
        CentralDocumentMetadataTemplateEntity? template,
        IReadOnlyList<CentralDocumentMetadataValue> metadataValues)
    {
        if (string.IsNullOrWhiteSpace(record.MetadataTemplateCode) || template is null)
        {
            return new CentralDocumentMetadataCompletenessDto(
                "Missing template",
                0,
                0,
                0,
                record.MetadataTemplateCode,
                null,
                null,
                [],
                []);
        }

        var requiredFields = DeserializeStringArray(template.RequiredFieldsJson)
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = requiredFields
            .Select(field =>
            {
                var value = ResolveMetadataValue(record, field, metadataValues);
                return new CentralDocumentMetadataCompletenessItemDto(
                    field,
                    !string.IsNullOrWhiteSpace(value),
                    value);
            })
            .ToList();

        var requiredCount = items.Count;
        var capturedCount = items.Count(item => item.Captured);
        var missingFields = items.Where(item => !item.Captured).Select(item => item.Field).ToList();
        var percentage = requiredCount == 0 ? 100 : (int)Math.Round(capturedCount * 100m / requiredCount);
        var status = requiredCount == 0 || capturedCount == requiredCount ? "Complete" : "Partial";

        return new CentralDocumentMetadataCompletenessDto(
            status,
            percentage,
            capturedCount,
            requiredCount,
            template.TemplateCode,
            template.Module,
            template.DocumentType,
            items,
            missingFields);
    }

    private async Task UpsertMetadataValuesAsync(
        Guid tenantId,
        CentralDocumentRecord record,
        IReadOnlyList<UpsertDocumentMetadataValueRequest> values,
        string source,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingValues = await _db.CentralDocumentMetadataValues
            .Where(item => item.TenantId == tenantId
                && item.DocumentRecordId == record.Id)
            .ToListAsync(cancellationToken);

        foreach (var value in values.Where(item => !string.IsNullOrWhiteSpace(item.FieldLabel)))
        {
            var fieldLabel = value.FieldLabel!.Trim();
            var fieldKey = string.IsNullOrWhiteSpace(value.FieldKey)
                ? NormalizeMetadataField(fieldLabel)
                : NormalizeMetadataField(value.FieldKey);
            var existing = existingValues.FirstOrDefault(item =>
                string.Equals(item.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                _db.CentralDocumentMetadataValues.Add(new CentralDocumentMetadataValue
                {
                    TenantId = tenantId,
                    DocumentRecordId = record.Id,
                    TemplateCode = record.MetadataTemplateCode,
                    FieldKey = fieldKey,
                    FieldLabel = fieldLabel,
                    FieldValue = TrimToNull(value.FieldValue),
                    ValueType = string.IsNullOrWhiteSpace(value.ValueType) ? "text" : value.ValueType.Trim(),
                    Source = source,
                    CapturedAt = now,
                    CapturedById = GetUserId(),
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName ?? "System",
                    CreatedById = GetUserId()
                });
                continue;
            }

            existing.TemplateCode = record.MetadataTemplateCode;
            existing.FieldLabel = fieldLabel;
            existing.FieldValue = TrimToNull(value.FieldValue);
            existing.ValueType = string.IsNullOrWhiteSpace(value.ValueType) ? existing.ValueType : value.ValueType.Trim();
            existing.Source = source;
            existing.CapturedAt = now;
            existing.CapturedById = GetUserId();
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.UpdatedAt = now;
            existing.UpdatedBy = _currentUserService.UserName ?? "System";
            existing.LastModifiedById = GetUserId();
        }
    }

    private static CentralDocumentAccessRetentionComplianceDto BuildAccessRetentionCompliance(
        CentralDocumentRecord record,
        CentralDocumentMetadataTemplateEntity? template,
        IReadOnlyList<CentralDocumentAccessRule> accessRules,
        IReadOnlyList<CentralDocumentRetentionPolicy> retentionPolicies)
    {
        var matchedAccessRules = accessRules
            .Where(rule => string.Equals(rule.AccessProfile, record.AccessProfile, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(rule.Module)
                    || string.Equals(rule.Module, record.SourceModule, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var documentType = template?.DocumentType ?? record.SourceEntityType;
        var matchedPolicy = retentionPolicies
            .Where(policy => string.IsNullOrWhiteSpace(policy.Module)
                || string.Equals(policy.Module, record.SourceModule, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(policy => !string.IsNullOrWhiteSpace(policy.DocumentType)
                && string.Equals(policy.DocumentType, documentType, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(policy => !string.IsNullOrWhiteSpace(policy.Module)
                && string.Equals(policy.Module, record.SourceModule, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(policy => string.IsNullOrWhiteSpace(policy.DocumentType)
                || string.Equals(policy.DocumentType, documentType, StringComparison.OrdinalIgnoreCase));

        var retentionNeedsAction = NeedsRetentionAction(record);
        var accessStatus = matchedAccessRules.Count == 0 ? "No access rule" : "Covered";
        var retentionStatus = matchedPolicy is null
            ? "No retention policy"
            : retentionNeedsAction
                ? "Action required"
                : "Covered";

        return new CentralDocumentAccessRetentionComplianceDto(
            accessStatus,
            matchedAccessRules.Count,
            matchedAccessRules.Select(ToAccessRuleSummaryDto).ToList(),
            retentionStatus,
            retentionNeedsAction,
            matchedPolicy is null ? null : ToRetentionPolicySummaryDto(matchedPolicy),
            record.RetentionStatus,
            record.ReviewDate,
            record.ExpiryDate);
    }

    private static IReadOnlyList<object> BuildDashboardModuleQueues(
        IReadOnlyList<CentralDocumentRecord> records,
        IReadOnlyDictionary<Guid, CentralDocumentMetadataCompletenessDto> completenessByRecord,
        IReadOnlyList<CentralDocumentAnnotationReview> openAnnotationReviews)
    {
        var definitions = new (string Module, string[] Aliases)[]
        {
            ("Estate / Facilities", ["Estate / Facilities", "Estate / Facility", "Estate/Facilities", "Estate/Facility", "Estate / Facilites", "Facilities Management"]),
            ("Estate / Property Management", ["Estate / Property Management", "Estate/Property Management", "Property Management"]),
            ("Project Management", ["Project Management", "Projects"]),
            ("Maintenance", ["Maintenance", "Maintenance Management"]),
            ("Finance", ["Finance", "Finance AR", "Finance AP"])
        };

        return definitions
            .Select(definition =>
            {
                var moduleRecords = records
                    .Where(record => MatchesAnyModule(record.SourceModule, definition.Aliases))
                    .ToList();
                var moduleRecordIds = moduleRecords.Select(record => record.Id).ToHashSet();

                return (object)new
                {
                    module = definition.Module,
                    sourceLabel = $"Source: {definition.Module} -> Central DMS",
                    pendingMetadata = moduleRecords.Count(record =>
                        !completenessByRecord.TryGetValue(record.Id, out var completeness)
                        || !string.Equals(completeness.Status, "Complete", StringComparison.OrdinalIgnoreCase)),
                    pendingVersion = moduleRecords.Count(record =>
                        string.IsNullOrWhiteSpace(record.CurrentVersion)
                        || IsPendingVersionStatus(record.VersionStatus)),
                    openAnnotations = openAnnotationReviews.Count(review => moduleRecordIds.Contains(review.DocumentRecordId)),
                    retentionReviews = moduleRecords.Count(NeedsRetentionAction)
                };
            })
            .ToList();
    }

    private static string IntegrationIssue(
        CentralDocumentRecord record,
        CentralDocumentMetadataCompletenessDto completeness)
    {
        if (string.Equals(completeness.Status, "Missing template", StringComparison.OrdinalIgnoreCase))
        {
            return "Metadata template required";
        }

        if (!string.Equals(completeness.Status, "Complete", StringComparison.OrdinalIgnoreCase))
        {
            return "Metadata completion required";
        }

        if (!IsRepositoryLinked(record))
        {
            return "Repository file link required";
        }

        if (string.IsNullOrWhiteSpace(record.CurrentVersion) || IsPendingVersionStatus(record.VersionStatus))
        {
            return "Version publication required";
        }

        if (NeedsRetentionAction(record))
        {
            return "Retention review required";
        }

        return "Ready for DMS control";
    }

    private static object BuildDashboardMetric(string label, string value, string detail, string trend) => new
    {
        label,
        value,
        detail,
        trend
    };

    private static bool HasAccessRule(
        CentralDocumentRecord record,
        IReadOnlyList<CentralDocumentAccessRule> accessRules)
    {
        if (string.IsNullOrWhiteSpace(record.AccessProfile))
        {
            return false;
        }

        return accessRules.Any(rule => string.Equals(rule.AccessProfile, record.AccessProfile, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(rule.Module)
                || string.Equals(rule.Module, record.SourceModule, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<CentralDocumentVersion?> ResolveCurrentVersionAsync(
        CentralDocumentRecord record,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var query = _db.CentralDocumentVersions
            .Where(item => item.DocumentRecordId == record.Id
                && item.TenantId == tenantId
                && !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(record.CurrentVersion))
        {
            var current = await query
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(item => item.VersionNumber == record.CurrentVersion, cancellationToken);

            if (current is not null)
            {
                return current;
            }
        }

        return await query
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<VersionDownloadFile> OpenPdfVersionFileAsync(
        CentralDocumentRecord record,
        CentralDocumentVersion version,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(version.RenditionPath))
        {
            var renditionFile = await OpenStoredPathAsync(
                version.RenditionPath,
                $"{Path.GetFileNameWithoutExtension(version.FileName ?? record.DocumentReference)}.pdf",
                "application/pdf",
                cancellationToken);

            if (renditionFile.Success)
            {
                return renditionFile;
            }
        }

        var sourceFile = await OpenVersionSourceFileAsync(version, tenantId, cancellationToken);
        if (sourceFile.Success && sourceFile.Stream is not null && IsPdfFile(sourceFile.ContentType, sourceFile.FileName))
        {
            return sourceFile with
            {
                FileName = SafeDownloadFileName(sourceFile.FileName, record.DocumentReference, ".pdf"),
                ContentType = "application/pdf"
            };
        }

        if (!sourceFile.Success || sourceFile.Stream is null)
        {
            return sourceFile.Success
                ? new VersionDownloadFile(false, null, null, null, "This DMS version does not have a stored source file that can be converted.")
                : sourceFile;
        }

        await using (sourceFile.Stream)
        {
            var renditionResult = await _renditionService.CreatePdfRenditionAsync(new CentralDocumentRenditionRequest(
                sourceFile.Stream,
                sourceFile.FileName ?? version.FileName ?? record.DocumentReference,
                sourceFile.ContentType ?? version.ContentType ?? "application/octet-stream",
                tenantId,
                record.Id,
                record.DocumentReference,
                record.SourceModule), cancellationToken);

            if (!renditionResult.Success || string.IsNullOrWhiteSpace(renditionResult.RenditionPath))
            {
                record.AnnotationStatus = renditionResult.IsSupported ? "PDF rendition failed" : "PDF rendition required";
                record.UpdatedAt = DateTime.UtcNow;
                record.UpdatedBy = _currentUserService.UserName ?? "System";
                record.LastModifiedById = GetUserId();
                await _db.SaveChangesAsync(cancellationToken);

                return new VersionDownloadFile(
                    false,
                    null,
                    null,
                    null,
                    renditionResult.ErrorMessage ?? "Unable to generate the PDF rendition.");
            }

            version.RenditionPath = renditionResult.RenditionPath;
            version.ChangeSummary = BuildVersionChangeSummary(version.ChangeSummary, renditionResult);
            version.UpdatedAt = DateTime.UtcNow;
            version.UpdatedBy = _currentUserService.UserName ?? "System";
            version.LastModifiedById = GetUserId();

            record.AnnotationStatus = "PDF preview ready";
            record.UpdatedAt = DateTime.UtcNow;
            record.UpdatedBy = _currentUserService.UserName ?? "System";
            record.LastModifiedById = GetUserId();

            await _db.SaveChangesAsync(cancellationToken);

            return await OpenStoredPathAsync(
                renditionResult.RenditionPath,
                $"{Path.GetFileNameWithoutExtension(sourceFile.FileName ?? record.DocumentReference)}.pdf",
                "application/pdf",
                cancellationToken);
        }
    }

    private async Task<VersionDownloadFile> OpenVersionSourceFileAsync(
        CentralDocumentVersion version,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (version.FileUploadRecordId.HasValue)
        {
            var uploadRecord = await _db.FileUploadRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == version.FileUploadRecordId.Value
                    && item.TenantId == tenantId
                    && !item.IsDeleted, cancellationToken);

            if (uploadRecord is not null)
            {
                try
                {
                    var stream = await _fileStorageService.DownloadFileAsync(uploadRecord.FilePath, uploadRecord.Id);
                    return new VersionDownloadFile(
                        true,
                        stream,
                        uploadRecord.OriginalFileName ?? uploadRecord.StoredFileName ?? version.FileName,
                        uploadRecord.ContentType ?? version.ContentType,
                        null);
                }
                catch (FileNotFoundException ex)
                {
                    return new VersionDownloadFile(false, null, null, null, ex.Message);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(version.RepositoryPath))
        {
            return await OpenStoredPathAsync(
                version.RepositoryPath,
                version.FileName,
                version.ContentType,
                cancellationToken);
        }

        return new VersionDownloadFile(false, null, null, null, "This DMS version does not have a downloadable source file.");
    }

    private async Task<VersionDownloadFile> OpenStoredPathAsync(
        string? path,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storagePath = ResolveStoragePath(path);
        if (storagePath is null)
        {
            return new VersionDownloadFile(false, null, null, null, "Only files stored in the DMS upload store can be downloaded securely.");
        }

        try
        {
            var stream = await _fileStorageService.DownloadFileAsync(storagePath, Guid.Empty);
            return new VersionDownloadFile(
                true,
                stream,
                FileNameFromPath(path) ?? fileName,
                contentType,
                null);
        }
        catch (FileNotFoundException ex)
        {
            return new VersionDownloadFile(false, null, null, null, ex.Message);
        }
    }

    private static string? ResolveStoragePath(string? path)
    {
        var value = TrimToNull(path);
        if (value is null)
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            value = uri.AbsolutePath;
        }

        value = value.Replace('\\', '/').TrimStart('/');
        if (value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            value = value["uploads/".Length..];
        }

        return string.IsNullOrWhiteSpace(value) || value.Contains("..", StringComparison.Ordinal)
            ? null
            : value;
    }

    private static string? FileNameFromPath(string? path)
    {
        var value = TrimToNull(path);
        if (value is null)
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            value = uri.AbsolutePath;
        }

        value = value.Split('?', '#')[0].Replace('\\', '/');
        return TrimToNull(Path.GetFileName(value));
    }

    private static string SafeDownloadFileName(string? fileName, string fallback, string extension)
    {
        var name = FileNameFromPath(fileName) ?? fileName ?? fallback;
        if (string.IsNullOrWhiteSpace(Path.GetExtension(name)))
        {
            name = $"{name}{extension}";
        }

        return SafeFileName(name);
    }

    private static bool IsWordFile(string? contentType, string? fileName)
        => (!string.IsNullOrWhiteSpace(contentType)
                && (contentType.Contains("word", StringComparison.OrdinalIgnoreCase)
                    || contentType.Contains("rtf", StringComparison.OrdinalIgnoreCase)))
            || (!string.IsNullOrWhiteSpace(fileName)
                && (fileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase)));

    private async Task<bool> CanUseRecordActionAsync(
        Guid tenantId,
        CentralDocumentRecord record,
        Func<CentralDocumentAccessRule, bool> actionPredicate,
        CancellationToken cancellationToken)
    {
        if (IsDmsAccessAdministrator())
        {
            return true;
        }

        var rules = await _db.CentralDocumentAccessRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId
                && rule.IsActive
                && !rule.IsDeleted
                && rule.AccessProfile == record.AccessProfile
                && (rule.Module == null || rule.Module == record.SourceModule))
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            // Absence of an active access rule is not an implicit grant; only DMS access administrators bypass rules.
            return false;
        }

        return rules.Any(rule => actionPredicate(rule)
            && (string.IsNullOrWhiteSpace(rule.RoleName)
                || _currentUserService.IsInRole(rule.RoleName)));
    }

    private async Task<bool> CanAssignAccessProfileAsync(
        Guid tenantId,
        string accessProfile,
        CancellationToken cancellationToken)
    {
        if (IsDmsAccessAdministrator())
        {
            return true;
        }

        var rules = await _db.CentralDocumentAccessRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId
                && rule.IsActive
                && !rule.IsDeleted
                && rule.AccessProfile == accessProfile)
            .ToListAsync(cancellationToken);

        // Do not let non-admin users reclassify a protected document into an access profile with no active rules.
        return rules.Count > 0
            && rules.Any(rule => rule.CanUpload
                && (string.IsNullOrWhiteSpace(rule.RoleName)
                    || _currentUserService.IsInRole(rule.RoleName)));
    }

    private async Task<bool> CanRunGeneratedDocumentWorkflowActionAsync(
        Guid tenantId,
        CentralDocumentRecord record,
        string normalizedAction,
        CancellationToken cancellationToken)
    {
        if (IsDmsAccessAdministrator())
        {
            return true;
        }

        var hasDocumentPermission = normalizedAction switch
        {
            "submitapproval" or "submitforapproval" => await CanUseRecordActionAsync(tenantId, record, rule => rule.CanUpload, cancellationToken),
            "approve" or "return" or "returnforaction" => await CanUseRecordActionAsync(tenantId, record, rule => rule.CanApprove, cancellationToken),
            "sign" => await CanUseRecordActionAsync(tenantId, record, rule => rule.CanApprove, cancellationToken),
            "dispatch" => await CanUseRecordActionAsync(tenantId, record, rule => rule.CanArchive, cancellationToken),
            _ => false
        };
        if (!hasDocumentPermission)
        {
            return false;
        }

        var template = await ResolveGeneratedTemplateForRecordAsync(tenantId, record, cancellationToken);
        return normalizedAction switch
        {
            "approve" or "return" or "returnforaction" => HasConfiguredOrSourceRole(template?.ApprovalRole, record, "Authorised Signatory", "Records Officer"),
            "sign" => HasConfiguredOrSourceRole(template?.SignatureRole, record, "Authorised Signatory", "Records Officer"),
            "dispatch" => HasAnyRole("Records Officer", "Estate Officer"),
            _ => true
        };
    }

    private async Task<GeneratedDocumentTemplateDefinition?> ResolveGeneratedTemplateForRecordAsync(
        Guid tenantId,
        CentralDocumentRecord record,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultGenerationTemplatesAsync(tenantId, cancellationToken);
        var templateCode = await _db.CentralDocumentMetadataValues
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.DocumentRecordId == record.Id
                && !item.IsDeleted
                && item.FieldKey == "templatecode")
            .Select(item => item.FieldValue)
            .FirstOrDefaultAsync(cancellationToken);

        templateCode = TrimToNull(templateCode) ?? TemplateCodeFromSourceReference(record.SourceRecordReference);
        var query = _db.CentralDocumentGenerationTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive && !item.IsDeleted);

        CentralDocumentGenerationTemplate? template = null;
        if (!string.IsNullOrWhiteSpace(templateCode))
        {
            template = await query.FirstOrDefaultAsync(item => item.TemplateCode == templateCode, cancellationToken);
        }

        template ??= await query.FirstOrDefaultAsync(item =>
            item.Module == record.SourceModule
            && item.MetadataTemplateCode == record.MetadataTemplateCode
            && item.DocumentType == record.SourceEntityType,
            cancellationToken);

        return template is null ? null : ToGeneratedTemplateDefinition(template);
    }

    private bool HasConfiguredOrSourceRole(string? configuredRole, CentralDocumentRecord record, params string[] fallbackRoles)
    {
        if (!string.IsNullOrWhiteSpace(configuredRole))
        {
            return _currentUserService.IsInRole(configuredRole);
        }

        return HasAnyRole(DmsSourceRoles(record).Concat(fallbackRoles).ToArray());
    }

    private bool HasAnyRole(params string[] roles)
        => roles.Any(role => !string.IsNullOrWhiteSpace(role) && _currentUserService.IsInRole(role));

    private bool CanUseSourceModuleForDms(string? sourceModule)
    {
        if (IsDmsAccessAdministrator())
        {
            return true;
        }

        var source = sourceModule ?? string.Empty;
        if (source.Contains("Estate", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Facilities", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Facility", StringComparison.OrdinalIgnoreCase)
            || source.Contains("Property", StringComparison.OrdinalIgnoreCase))
        {
            return HasAnyRole(
                "Estate Officer",
                "Estate Manager",
                "Land Registry Officer",
                "Survey Officer",
                "Facilities Officer",
                "Facilities Manager",
                "Property Manager");
        }

        if (source.Contains("Finance", StringComparison.OrdinalIgnoreCase))
        {
            return HasAnyRole("Finance Officer", "Finance Manager", "Accounts Payable", "Accounts Receivable");
        }

        if (source.Contains("Legal", StringComparison.OrdinalIgnoreCase))
        {
            return HasAnyRole("Legal Officer", "Legal Manager", "Head of Legal");
        }

        if (source.Contains("Planning", StringComparison.OrdinalIgnoreCase))
        {
            return HasAnyRole("Planning Officer", "Planning Manager");
        }

        if (source.Contains("Project", StringComparison.OrdinalIgnoreCase))
        {
            return HasAnyRole("Project Manager", "Project Officer", "PMO");
        }

        return false;
    }

    private bool IsDmsAccessAdministrator()
        => _currentUserService.IsInRole("SuperAdmin")
            || _currentUserService.IsInRole("TenantAdmin")
            || _currentUserService.IsInRole("Document Control Officer")
            || _currentUserService.IsInRole("Records Officer");

    private static string? TemplateCodeFromSourceReference(string? sourceRecordReference)
    {
        if (string.IsNullOrWhiteSpace(sourceRecordReference))
        {
            return null;
        }

        var markerIndex = sourceRecordReference.LastIndexOf(" / ", StringComparison.Ordinal);
        return markerIndex < 0 ? null : TrimToNull(sourceRecordReference[(markerIndex + 3)..]);
    }

    private sealed record VersionDownloadFile(
        bool Success,
        Stream? Stream,
        string? FileName,
        string? ContentType,
        string? ErrorMessage);

    private static CentralDocumentRetentionPolicy? FindRetentionPolicy(
        CentralDocumentRecord record,
        CentralDocumentMetadataTemplateEntity? template,
        IReadOnlyList<CentralDocumentRetentionPolicy> retentionPolicies)
    {
        var documentType = template?.DocumentType ?? record.SourceEntityType;
        return retentionPolicies
            .Where(policy => string.IsNullOrWhiteSpace(policy.Module)
                || string.Equals(policy.Module, record.SourceModule, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(policy => !string.IsNullOrWhiteSpace(policy.DocumentType)
                && string.Equals(policy.DocumentType, documentType, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(policy => !string.IsNullOrWhiteSpace(policy.Module)
                && string.Equals(policy.Module, record.SourceModule, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(policy => string.IsNullOrWhiteSpace(policy.DocumentType)
                || string.Equals(policy.DocumentType, documentType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOpenAnnotationReview(CentralDocumentAnnotationReview review)
        => !string.Equals(review.Status, "Closed", StringComparison.OrdinalIgnoreCase)
            && review.ClosedAt is null;

    private static bool IsPendingVersionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        var completedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Current",
            "Published",
            "Approved",
            "Active"
        };

        return !completedStatuses.Contains(status.Trim());
    }

    private static bool IsRepositoryLinked(CentralDocumentRecord record)
        => string.Equals(record.RepositoryStatus, "Linked", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(record.RepositoryPath)
            || !string.IsNullOrWhiteSpace(record.ExternalDocumentUrl);

    private static bool IsPdfFile(string? contentType, string? fileName)
        => (!string.IsNullOrWhiteSpace(contentType)
                && contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(fileName)
                && fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

    private static string NextVersionNumber(string? currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return "v1.0";
        }

        var normalized = currentVersion.Trim().TrimStart('v', 'V');
        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var major))
        {
            return "v1.0";
        }

        var minor = parts.Length > 1 && int.TryParse(parts[1], out var parsedMinor)
            ? parsedMinor + 1
            : 1;
        return $"v{major}.{minor}";
    }

    private static bool MatchesAnyModule(string sourceModule, IReadOnlyCollection<string> aliases)
    {
        if (string.IsNullOrWhiteSpace(sourceModule))
        {
            return false;
        }

        return aliases.Any(alias => sourceModule.Contains(alias, StringComparison.OrdinalIgnoreCase));
    }

    private static string Percent(int count, int total)
        => total == 0 ? "0%" : $"{(int)Math.Round(count * 100m / total)}%";

    private static bool NeedsRetentionAction(CentralDocumentRecord record)
    {
        var actionStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Review due",
            "Archive due",
            "Legal hold",
            "Destruction approval required"
        };

        var today = DateTime.UtcNow.Date;
        return actionStatuses.Contains(record.RetentionStatus)
            || (record.ReviewDate.HasValue && record.ReviewDate.Value.Date <= today)
            || (record.ExpiryDate.HasValue && record.ExpiryDate.Value.Date <= today);
    }

    private static CentralDocumentAccessRuleSummaryDto ToAccessRuleSummaryDto(CentralDocumentAccessRule rule) =>
        new(
            rule.AccessProfile,
            rule.Module,
            string.IsNullOrWhiteSpace(rule.RoleName) ? "Any configured role" : rule.RoleName,
            rule.PermissionKey,
            rule.CanView,
            rule.CanUpload,
            rule.CanAnnotate,
            rule.CanApprove,
            rule.CanArchive);

    private static CentralDocumentRetentionPolicySummaryDto ToRetentionPolicySummaryDto(CentralDocumentRetentionPolicy policy) =>
        new(
            policy.PolicyCode,
            policy.Name,
            policy.Module,
            policy.DocumentType,
            policy.RetentionDays,
            policy.RequiresLegalHoldReview,
            policy.AllowArchive,
            policy.AllowDestruction,
            policy.Notes);

    private static string? ResolveMetadataValue(
        CentralDocumentRecord record,
        string field,
        IReadOnlyList<CentralDocumentMetadataValue> metadataValues)
    {
        var fieldKey = NormalizeMetadataField(field);
        var savedValue = metadataValues.FirstOrDefault(item =>
            string.Equals(item.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));

        return Value(savedValue?.FieldValue) ?? ResolveRequiredFieldValue(record, field);
    }

    private static string? ResolveRequiredFieldValue(CentralDocumentRecord record, string field)
    {
        var key = NormalizeMetadataField(field);

        if (key.Contains("documentreference", StringComparison.Ordinal) || key == "dmsreference")
        {
            return Value(record.DocumentReference);
        }

        if (key.Contains("documenttitle", StringComparison.Ordinal)
            || key == "title"
            || key.Contains("documentname", StringComparison.Ordinal))
        {
            return Value(record.Title);
        }

        if (key.Contains("documentdate", StringComparison.Ordinal) || key.Contains("effectivedate", StringComparison.Ordinal))
        {
            return Value(record.EffectiveDate?.ToString("yyyy-MM-dd") ?? record.PublishedAt?.ToString("yyyy-MM-dd"));
        }

        if (key.Contains("expiry", StringComparison.Ordinal) || key.Contains("expiration", StringComparison.Ordinal))
        {
            return Value(record.ExpiryDate?.ToString("yyyy-MM-dd"));
        }

        if (key.Contains("reviewdate", StringComparison.Ordinal))
        {
            return Value(record.ReviewDate?.ToString("yyyy-MM-dd"));
        }

        if (key.Contains("sourcemodule", StringComparison.Ordinal)
            || key.Contains("sourceworkspace", StringComparison.Ordinal)
            || key.Contains("sourcesystem", StringComparison.Ordinal)
            || key == "module")
        {
            return Value(record.SourceModule);
        }

        if (key.Contains("sourcelabel", StringComparison.Ordinal))
        {
            return Value(record.SourceLabel);
        }

        if (key.Contains("sourceentity", StringComparison.Ordinal))
        {
            return Value(record.SourceEntityType);
        }

        if (key.Contains("sourcerecord", StringComparison.Ordinal)
            || key.Contains("propertyreference", StringComparison.Ordinal)
            || key.Contains("unitreference", StringComparison.Ordinal)
            || key.Contains("asset", StringComparison.Ordinal)
            || key.Contains("case", StringComparison.Ordinal)
            || key.Contains("customeraccount", StringComparison.Ordinal)
            || key.Contains("invoice", StringComparison.Ordinal)
            || key.Contains("receipt", StringComparison.Ordinal)
            || key.Contains("workorder", StringComparison.Ordinal)
            || key.Contains("matterreference", StringComparison.Ordinal)
            || key.Contains("projectreference", StringComparison.Ordinal))
        {
            return Value(record.SourceRecordReference ?? record.SourceRecordId?.ToString());
        }

        if (key.Contains("documentcategory", StringComparison.Ordinal)
            || key.Contains("documenttype", StringComparison.Ordinal)
            || key.Contains("documentpurpose", StringComparison.Ordinal))
        {
            return Value(record.SourceEntityType ?? record.MetadataTemplateCode);
        }

        if (key.Contains("metadatatemplate", StringComparison.Ordinal) || key.Contains("template", StringComparison.Ordinal))
        {
            return Value(record.MetadataTemplateCode);
        }

        if (key.Contains("repository", StringComparison.Ordinal) || key.Contains("file", StringComparison.Ordinal))
        {
            return Value(record.RepositoryPath ?? record.ExternalDocumentUrl ?? LinkedStatusValue(record.RepositoryStatus));
        }

        if (key.Contains("version", StringComparison.Ordinal) || key.Contains("revision", StringComparison.Ordinal))
        {
            return Value(record.CurrentVersion ?? record.VersionStatus);
        }

        if (key.Contains("annotation", StringComparison.Ordinal))
        {
            return Value(record.AnnotationStatus);
        }

        if (key.Contains("comment", StringComparison.Ordinal))
        {
            return Value(record.CommentStatus);
        }

        if (key.Contains("access", StringComparison.Ordinal) || key.Contains("confidentiality", StringComparison.Ordinal))
        {
            return Value(record.AccessProfile);
        }

        if (key.Contains("retention", StringComparison.Ordinal) || key.Contains("legalhold", StringComparison.Ordinal))
        {
            return Value(record.RetentionStatus);
        }

        if (key.Contains("approval", StringComparison.Ordinal) || key.Contains("lifecycle", StringComparison.Ordinal) || key == "status")
        {
            return Value(record.LifecycleStatus);
        }

        if (key.Contains("preparedby", StringComparison.Ordinal) || key.Contains("createdby", StringComparison.Ordinal))
        {
            return Value(record.CreatedBy);
        }

        if (key.Contains("notes", StringComparison.Ordinal))
        {
            return Value(record.Notes);
        }

        return null;
    }

    private static string NormalizeMetadataField(string value)
        => new(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static string? LinkedStatusValue(string? status)
        => string.Equals(status, "Linked", StringComparison.OrdinalIgnoreCase) ? status : null;

    private static string? Value(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object ToVersionDto(CentralDocumentVersion version) => new
    {
        version.Id,
        version.DocumentRecordId,
        version.VersionNumber,
        version.Status,
        RepositoryPath = string.IsNullOrWhiteSpace(version.RepositoryPath)
            ? null
            : VersionContentUrl(version.DocumentRecordId, version.Id),
        RenditionPath = string.IsNullOrWhiteSpace(version.RenditionPath)
            ? null
            : VersionContentUrl(version.DocumentRecordId, version.Id),
        version.FileName,
        version.ContentType,
        version.FileSize,
        version.FileUploadRecordId,
        version.PublishedAt,
        version.ChangeSummary,
        version.CreatedAt
    };

    private static string RecordContentUrl(Guid recordId)
        => $"/api/document-management/records/{recordId}/content";

    private static string VersionContentUrl(Guid recordId, Guid versionId)
        => $"/api/document-management/records/{recordId}/versions/{versionId}/content";

    private static object ToAnnotationReviewDto(CentralDocumentAnnotationReview review) => new
    {
        review.Id,
        review.DocumentRecordId,
        review.DocumentVersionId,
        review.ReviewTitle,
        review.Status,
        review.SyncfusionAnnotationStatus,
        review.AssignedReviewerId,
        review.DueDate,
        review.ClosedAt,
        review.ReviewNotes,
        review.AnnotationStateJson,
        review.CreatedAt
    };

    private static object ToMetadataValueDto(CentralDocumentMetadataValue value) => new
    {
        value.Id,
        value.DocumentRecordId,
        value.TemplateCode,
        value.FieldKey,
        value.FieldLabel,
        value.FieldValue,
        value.ValueType,
        value.Source,
        value.CapturedAt,
        value.CapturedById,
        value.CreatedAt,
        value.UpdatedAt
    };

    private static object ToTemplateDto(CentralDocumentMetadataTemplateEntity template) => new
    {
        template.Id,
        template.Module,
        template.DocumentType,
        template.TemplateCode,
        requiredFields = DeserializeStringArray(template.RequiredFieldsJson),
        relationships = DeserializeStringArray(template.RelationshipsJson),
        template.RetentionRule,
        template.AccessProfile,
        template.SourceLabel,
        template.IsActive,
        template.PublishedAt
    };

    private static IReadOnlyList<string> DeserializeStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? BuildVersionChangeSummary(
        string? changeSummary,
        CentralDocumentRenditionResult? renditionResult)
    {
        var summary = TrimToNull(changeSummary);
        if (renditionResult is null)
        {
            return summary;
        }

        var renditionNote = renditionResult.Success
            ? "PDF rendition generated by Syncfusion."
            : renditionResult.IsSupported
                ? $"Syncfusion PDF rendition failed: {renditionResult.ErrorMessage}"
                : "PDF rendition required for this file type.";

        var combined = string.IsNullOrWhiteSpace(summary)
            ? renditionNote
            : $"{summary} {renditionNote}";

        return combined.Length <= 1000 ? combined : combined[..1000];
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TrimOrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record UpsertDocumentRecordRequest(
    string? DocumentReference,
    string? Title,
    string? SourceModule,
    string? SourceLabel,
    string? SourceEntityType,
    string? SourceRecordReference,
    Guid? SourceRecordId,
    string? MetadataTemplateCode,
    string? RepositoryStatus,
    string? RepositoryPath,
    string? ExternalDocumentUrl,
    string? CurrentVersion,
    string? VersionStatus,
    string? AnnotationStatus,
    string? CommentStatus,
    string? AccessProfile,
    string? RetentionStatus,
    string? LifecycleStatus,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate,
    DateTime? ReviewDate,
    string? Notes);

public sealed record CentralDocumentMetadataCompletenessDto(
    string Status,
    int Percentage,
    int CapturedCount,
    int RequiredCount,
    string? TemplateCode,
    string? Module,
    string? DocumentType,
    IReadOnlyList<CentralDocumentMetadataCompletenessItemDto> Items,
    IReadOnlyList<string> MissingFields);

public sealed record CentralDocumentMetadataCompletenessItemDto(
    string Field,
    bool Captured,
    string? Value);

public sealed record CentralDocumentAccessRetentionComplianceDto(
    string AccessStatus,
    int AccessRuleCount,
    IReadOnlyList<CentralDocumentAccessRuleSummaryDto> AccessRules,
    string RetentionPolicyStatus,
    bool RetentionNeedsAction,
    CentralDocumentRetentionPolicySummaryDto? RetentionPolicy,
    string CurrentRetentionStatus,
    DateTime? ReviewDate,
    DateTime? ExpiryDate);

public sealed record CentralDocumentAccessRuleSummaryDto(
    string AccessProfile,
    string? Module,
    string RoleName,
    string? PermissionKey,
    bool CanView,
    bool CanUpload,
    bool CanAnnotate,
    bool CanApprove,
    bool CanArchive);

public sealed record CentralDocumentRetentionPolicySummaryDto(
    string PolicyCode,
    string Name,
    string? Module,
    string? DocumentType,
    int RetentionDays,
    bool RequiresLegalHoldReview,
    bool AllowArchive,
    bool AllowDestruction,
    string? Notes);

public sealed record UpdateDocumentMetadataValuesRequest(
    IReadOnlyList<UpsertDocumentMetadataValueRequest>? Values);

public sealed record UpsertDocumentMetadataValueRequest(
    string? FieldKey,
    string? FieldLabel,
    string? FieldValue,
    string? ValueType);

public sealed record UpdateDocumentLifecycleControlsRequest(
    string? MetadataTemplateCode,
    string? RepositoryStatus,
    string? RepositoryPath,
    string? ExternalDocumentUrl,
    string? CurrentVersion,
    string? VersionStatus,
    string? AnnotationStatus,
    string? CommentStatus,
    string? AccessProfile,
    string? RetentionStatus,
    string? LifecycleStatus,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate,
    DateTime? ReviewDate,
    string? Notes);

public sealed record UpsertDocumentVersionRequest(
    string? VersionNumber,
    string? Status,
    string? RepositoryPath,
    string? RenditionPath,
    string? FileName,
    string? ContentType,
    long? FileSize,
    Guid? FileUploadRecordId,
    string? ChangeSummary);

public sealed record UpdateDocumentVersionStatusRequest(
    string? Status,
    string? ChangeSummary);

public sealed record UpdateDocumentRenditionRequest(
    string? RenditionPath,
    string? AnnotationStatus,
    string? ChangeSummary);

public sealed record SourceDocumentHandoffRequest(
    string? Title,
    string? SourceModule,
    string? SourceLabel,
    string? SourceEntityType,
    string? SourceRecordReference,
    Guid? SourceRecordId,
    string? MetadataTemplateCode,
    string? AccessProfile,
    string? Notes,
    IReadOnlyList<UpsertDocumentMetadataValueRequest>? MetadataValues);

public sealed record GeneratedDocumentTemplateDefinition(
    string TemplateCode,
    string Title,
    string TitleTemplate,
    string Module,
    string SourceLabel,
    string DocumentType,
    string MetadataTemplateCode,
    string AccessProfile,
    IReadOnlyList<string> MergeFields,
    string Body,
    bool RequiresApproval,
    string? ApprovalRole,
    string? SignatureRole,
    string? DefaultDispatchChannel,
    bool IsActive = true);

public sealed record UpsertGeneratedDocumentTemplateRequest(
    string? TemplateCode,
    string? Title,
    string? TitleTemplate,
    string? Module,
    string? SourceLabel,
    string? DocumentType,
    string? MetadataTemplateCode,
    string? AccessProfile,
    IReadOnlyList<string>? MergeFields,
    string? Body,
    bool? IsActive,
    bool? RequiresApproval,
    string? ApprovalRole,
    string? SignatureRole,
    string? DefaultDispatchChannel);

public sealed record GeneratedDocumentWorkflowActionRequest(
    string? Action,
    string? Notes,
    string? SignatureRole,
    string? DispatchChannel,
    string? DispatchedTo,
    string? DispatchReference);

public sealed record GenerateDocumentTemplateRequest(
    string? TemplateCode,
    string? SourceModule,
    string? SourceLabel,
    string? SourceEntityType,
    string? SourceRecordReference,
    Guid? SourceRecordId,
    string? CaseTitle,
    string? CaseReference,
    string? ApplicantName,
    string? PreparedBy,
    string? Purpose,
    Dictionary<string, string?>? MergeValues);

public sealed record UpsertAnnotationReviewRequest(
    Guid? DocumentVersionId,
    string? ReviewTitle,
    string? Status,
    string? SyncfusionAnnotationStatus,
    Guid? AssignedReviewerId,
    DateTime? DueDate,
    string? ReviewNotes,
    string? AnnotationStateJson);

public sealed record UpdateAnnotationReviewRequest(
    string? Status,
    string? SyncfusionAnnotationStatus,
    string? ReviewNotes,
    string? AnnotationStateJson);

public sealed record UpsertMetadataTemplateRequest(
    string? Module,
    string? DocumentType,
    string? TemplateCode,
    string? SourceLabel,
    IReadOnlyList<string>? RequiredFields,
    IReadOnlyList<string>? Relationships,
    string? RetentionRule,
    string? AccessProfile,
    bool? IsActive);

public sealed record UpsertAccessRuleRequest(
    string? AccessProfile,
    string? Module,
    string? RoleName,
    string? PermissionKey,
    bool? CanView,
    bool? CanUpload,
    bool? CanAnnotate,
    bool? CanApprove,
    bool? CanArchive,
    bool? IsActive);

public sealed record UpsertRetentionPolicyRequest(
    string? PolicyCode,
    string? Name,
    string? Module,
    string? DocumentType,
    int? RetentionDays,
    bool? RequiresLegalHoldReview,
    bool? AllowArchive,
    bool? AllowDestruction,
    bool? IsActive,
    string? Notes);
