using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Interfaces.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/external/service-catalog")]
[Authorize(Policy = "ExternalOnly")]
[EnableRateLimiting("ApiPolicy")]
public sealed class EhcExternalServiceCatalogController : ControllerBase
{
    private static readonly Regex RequestNumberRegex =
        new("^SR-(?<yy>\\d{2})-(?<seq>\\d{6})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICaptchaVerificationService _captchaVerificationService;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly ILogger<EhcExternalServiceCatalogController> _logger;
    private readonly ErpSystem.Core.Interfaces.IFileStorageService _storageService;
    private readonly IAppEventBus _appEventBus;

    public EhcExternalServiceCatalogController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ICaptchaVerificationService captchaVerificationService,
        IWorkflowEngine workflowEngine,
        IWorkflowIntegrationService workflowIntegrationService,
        ErpSystem.Core.Interfaces.IFileStorageService storageService,
        IAppEventBus appEventBus,
        ILogger<EhcExternalServiceCatalogController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _captchaVerificationService = captchaVerificationService;
        _workflowEngine = workflowEngine;
        _workflowIntegrationService = workflowIntegrationService;
        _storageService = storageService;
        _appEventBus = appEventBus;
        _logger = logger;
    }

    [HttpGet("request-types")]
    public async Task<ActionResult> ListRequestTypes(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcServiceRequestTypeDto>() });
        }

        var items = await _db.EhcServiceRequestTypes
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var data = items.Select(t => new EhcServiceRequestTypeDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            Description = t.Description,
            IsActive = t.IsActive,
            WorkflowName = t.WorkflowName,
            FormDefinitionJson = t.FormDefinitionJson
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpGet("requests")]
    public async Task<ActionResult> ListMyRequests(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcServiceRequestSummaryDto>() });
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var items = await _db.EhcServiceRequests
            .AsNoTracking()
            .Include(r => r.RequestType)
            .Where(r => r.TenantId == tenantId && r.RequesterUserId == requesterUserId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var data = items.Select(r => new EhcServiceRequestSummaryDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            RequestTypeName = r.RequestType.Name,
            Status = r.Status,
            Title = r.Title,
            SubmittedAtUtc = r.SubmittedAtUtc
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult> GetMyRequest(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return NotFound(new { success = false, message = "Not found." });
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            return Unauthorized();
        }

        var r = await _db.EhcServiceRequests
            .AsNoTracking()
            .Include(x => x.RequestType)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.RequesterUserId == requesterUserId && !x.IsDeleted, cancellationToken);

        if (r == null)
        {
            return NotFound(new { success = false, message = "Request not found." });
        }

        var dto = new EhcServiceRequestDetailDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            RequestTypeId = r.RequestTypeId,
            RequestTypeName = r.RequestType.Name,
            Status = r.Status,
            Title = r.Title,
            FormDefinitionJson = r.RequestType.FormDefinitionJson,
            FormDataJson = r.FormDataJson,
            WorkflowInstanceId = r.WorkflowInstanceId,
            SubmittedAtUtc = r.SubmittedAtUtc,
            ApprovedAtUtc = r.ApprovedAtUtc,
            RejectedAtUtc = r.RejectedAtUtc,
            RejectionReason = r.RejectionReason,
            Attachments = (r.Attachments ?? new List<EhcServiceRequestAttachment>())
                .Where(a => !a.IsDeleted && !a.IsInternal)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new EhcServiceRequestAttachmentDto
                {
                    Id = a.Id,
                    FilePath = a.FilePath,
                    PublicUrl = null,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize,
                    IsInternal = a.IsInternal,
                    CreatedAtUtc = a.CreatedAt
                })
                .ToList()
        };

        foreach (var a in dto.Attachments)
        {
            try
            {
                a.PublicUrl = await _storageService.GetPublicUrlAsync(a.FilePath);
            }
            catch
            {
                a.PublicUrl = null;
            }
        }

        return Ok(new { success = true, data = dto });
    }

    [HttpPost("requests/{id:guid}/attachments")]
    public async Task<ActionResult> AddAttachment(Guid id, [FromBody] AddEhcServiceRequestAttachmentRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.FileName))
            {
                return BadRequest(new { success = false, message = "FilePath and FileName are required." });
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
            {
                return Unauthorized();
            }

            var r = await _db.EhcServiceRequests
                .Include(x => x.Attachments)
                .Include(x => x.RequestType)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.RequesterUserId == requesterUserId && !x.IsDeleted, cancellationToken);
            if (r == null)
            {
                return NotFound(new { success = false, message = "Request not found." });
            }

            var upload = await _db.FileUploadRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && !u.IsDeleted && u.FilePath == request.FilePath.Trim(), cancellationToken);
            if (upload == null)
            {
                return BadRequest(new { success = false, message = "Invalid file reference. Please upload again." });
            }

            if (!string.Equals(upload.Category, "ehc-service-request", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Invalid upload category." });
            }

            if (upload.UploadedByUserId != requesterUserId)
            {
                return Forbid();
            }

            var now = DateTime.UtcNow;
            var att = new EhcServiceRequestAttachment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = r.Id,
                FilePath = upload.FilePath,
                FileName = string.IsNullOrWhiteSpace(upload.OriginalFileName) ? request.FileName.Trim() : upload.OriginalFileName,
                ContentType = upload.ContentType ?? request.ContentType,
                FileSize = upload.FileSize > 0 ? upload.FileSize : request.FileSize,
                IsInternal = false,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "External",
                CreatedById = requesterUserId
            };

            _db.EhcServiceRequestAttachments.Add(att);
            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = r.Id,
                EventType = "RequesterAttachment",
                Title = "Attachment uploaded",
                Body = $"Requester uploaded {att.FileName}.",
                IsInternal = false,
                ActorUserId = requesterUserId,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "External",
                CreatedById = requesterUserId
            });

            await _db.SaveChangesAsync(cancellationToken);

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Attachment",
                audience: "Internal",
                serviceRequestId: r.Id,
                triggeredByUserId: requesterUserId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = r.Id,
                    ["requestNumber"] = r.RequestNumber,
                    ["requestTypeName"] = r.RequestType?.Name ?? string.Empty,
                    ["requesterUserId"] = requesterUserId,
                    ["fileName"] = att.FileName,
                    ["ActionUrl"] = $"/helpdesk/requests/{r.Id}"
                },
                cancellationToken);

            var publicUrl = await _storageService.GetPublicUrlAsync(att.FilePath);
            return Ok(new
            {
                success = true,
                data = new EhcServiceRequestAttachmentDto
                {
                    Id = att.Id,
                    FilePath = att.FilePath,
                    PublicUrl = publicUrl,
                    FileName = att.FileName,
                    ContentType = att.ContentType,
                    FileSize = att.FileSize,
                    IsInternal = att.IsInternal,
                    CreatedAtUtc = att.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service request attachment {ServiceRequestId}", id);
            return StatusCode(500, new { success = false, message = "Failed to add attachment" });
        }
    }

    [HttpPost("requests")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult> CreateRequest([FromBody] CreateEhcServiceRequestRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
            {
                return Unauthorized();
            }

            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : Request.Host.Host;
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _captchaVerificationService.EnsureCaptchaValidAsync(tenantId, request.CaptchaToken, host, remoteIp, cancellationToken);

            var requestType = await _db.EhcServiceRequestTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.RequestTypeId && t.TenantId == tenantId && t.IsActive && !t.IsDeleted, cancellationToken);
            if (requestType == null)
            {
                return BadRequest(new { success = false, message = "Invalid request type." });
            }

            if (!TryValidateFormData(requestType.FormDefinitionJson, request.FormDataJson, out var normalizedFormDataJson, out var dataError))
            {
                return BadRequest(new { success = false, message = dataError });
            }

            var now = DateTime.UtcNow;
            var requestNumber = await GenerateRequestNumberAsync(tenantId, cancellationToken);
            var entity = new EhcServiceRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RequestTypeId = requestType.Id,
                RequestNumber = requestNumber,
                RequesterUserId = requesterUserId,
                Title = request.Title?.Trim(),
                FormDataJson = normalizedFormDataJson,
                Status = EhcServiceRequestStatus.Submitted,
                SubmittedAtUtc = now,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "External",
                CreatedById = requesterUserId
            };

            _db.EhcServiceRequests.Add(entity);
            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = entity.Id,
                EventType = "Submitted",
                Title = "Request submitted",
                Body = null,
                IsInternal = false,
                ActorUserId = requesterUserId,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "External",
                CreatedById = requesterUserId
            });
            await _db.SaveChangesAsync(cancellationToken);

            await TryStartWorkflowAsync(requestType, entity, requesterUserId);
            await _db.SaveChangesAsync(cancellationToken);

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Submitted",
                audience: "Internal",
                serviceRequestId: entity.Id,
                triggeredByUserId: requesterUserId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = entity.Id,
                    ["requestNumber"] = entity.RequestNumber,
                    ["requestTypeName"] = requestType.Name,
                    ["title"] = entity.Title ?? string.Empty,
                    ["requesterUserId"] = requesterUserId,
                    ["ActionUrl"] = $"/helpdesk/requests/{entity.Id}"
                },
                cancellationToken);

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Submitted",
                audience: "Customer",
                serviceRequestId: entity.Id,
                triggeredByUserId: requesterUserId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = entity.Id,
                    ["requestNumber"] = entity.RequestNumber,
                    ["requestTypeName"] = requestType.Name,
                    ["TargetUserId"] = requesterUserId,
                    ["ActionUrl"] = $"/external-portal/support/requests/{entity.Id}"
                },
                cancellationToken);

            var detail = new EhcServiceRequestDetailDto
            {
                Id = entity.Id,
                RequestNumber = entity.RequestNumber,
                RequestTypeId = requestType.Id,
                RequestTypeName = requestType.Name,
                Status = entity.Status,
                Title = entity.Title,
                FormDefinitionJson = requestType.FormDefinitionJson,
                FormDataJson = entity.FormDataJson,
                WorkflowInstanceId = entity.WorkflowInstanceId,
                SubmittedAtUtc = entity.SubmittedAtUtc,
                ApprovedAtUtc = entity.ApprovedAtUtc,
                RejectedAtUtc = entity.RejectedAtUtc,
                RejectionReason = entity.RejectionReason,
                Attachments = new List<EhcServiceRequestAttachmentDto>()
            };

            return Ok(new { success = true, data = detail });
        }
        catch (CaptchaVerificationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC service request");
            return StatusCode(500, new { success = false, message = "Failed to create service request" });
        }
    }

    private async Task TryStartWorkflowAsync(EhcServiceRequestType requestType, EhcServiceRequest request, Guid initiatedByUserId)
    {
        if (request.WorkflowInstanceId.HasValue)
        {
            return;
        }

        var context = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["entityId"] = request.Id,
            ["entityType"] = "SERVICE_REQUEST",
            ["serviceRequestId"] = request.Id,
            ["serviceRequestNumber"] = request.RequestNumber,
            ["serviceRequestTypeCode"] = requestType.Code,
            ["serviceRequestTypeName"] = requestType.Name,
            ["serviceRequestTitle"] = request.Title ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(request.FormDataJson))
        {
            try
            {
                using var dataDoc = JsonDocument.Parse(request.FormDataJson);
                if (dataDoc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    context["formData"] = dataDoc.RootElement;
                }
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(requestType.WorkflowName))
            {
                var instance = await _workflowEngine.StartWorkflowAsync(requestType.WorkflowName.Trim(), request.Id, initiatedByUserId, context);
                request.WorkflowInstanceId = instance.Id;
                request.Status = instance.Status == Core.Enums.WorkflowInstanceStatus.Completed
                    ? EhcServiceRequestStatus.Approved
                    : EhcServiceRequestStatus.PendingApproval;
                return;
            }

            var workflowResult = await _workflowIntegrationService.SubmitAsync("ServiceRequest", request.Id);
            request.WorkflowInstanceId = workflowResult.ExecutionResult.WorkflowInstanceId;
            request.Status = workflowResult.Outcome switch
            {
                Core.Enums.WorkflowOutcome.Approved => EhcServiceRequestStatus.Approved,
                Core.Enums.WorkflowOutcome.Rejected => EhcServiceRequestStatus.Rejected,
                _ => EhcServiceRequestStatus.PendingApproval
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start workflow for service request {ServiceRequestId}", request.Id);
        }
    }

    private async Task<string> GenerateRequestNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var yy = (year % 100).ToString("D2");
        var prefix = $"SR-{yy}-";

        var last = await _db.EhcServiceRequests
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.RequestNumber.StartsWith(prefix))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.RequestNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSeq = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var m = RequestNumberRegex.Match(last);
            if (m.Success && int.TryParse(m.Groups["seq"].Value, out var lastSeq))
            {
                nextSeq = lastSeq + 1;
            }
        }

        return $"{prefix}{nextSeq:D6}";
    }

    private static bool TryValidateFormData(string formDefinitionJson, string formDataJson, out string normalizedFormDataJson, out string error)
    {
        normalizedFormDataJson = "{}";
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(formDataJson))
        {
            error = "FormDataJson is required.";
            return false;
        }

        try
        {
            using var formDoc = JsonDocument.Parse(formDefinitionJson);
            if (!formDoc.RootElement.TryGetProperty("fields", out var fieldsEl) || fieldsEl.ValueKind != JsonValueKind.Array)
            {
                error = "Invalid form definition.";
                return false;
            }

            using var dataDoc = JsonDocument.Parse(formDataJson);
            if (dataDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "FormDataJson must be a JSON object.";
                return false;
            }

            var requiredKeys = new List<string>();
            foreach (var field in fieldsEl.EnumerateArray())
            {
                if (field.ValueKind != JsonValueKind.Object) continue;
                if (!field.TryGetProperty("key", out var keyEl) || keyEl.ValueKind != JsonValueKind.String) continue;
                var key = keyEl.GetString();
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (field.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True)
                {
                    requiredKeys.Add(key);
                }
            }

            foreach (var key in requiredKeys)
            {
                if (!dataDoc.RootElement.TryGetProperty(key, out var valueEl) || valueEl.ValueKind == JsonValueKind.Null)
                {
                    error = $"Field '{key}' is required.";
                    return false;
                }
            }

            normalizedFormDataJson = JsonSerializer.Serialize(dataDoc.RootElement);
            return true;
        }
        catch (JsonException)
        {
            error = "FormDataJson must be valid JSON.";
            return false;
        }
        catch
        {
            error = "Invalid form data.";
            return false;
        }
    }

    private Task PublishServiceRequestTopicAsync(
        Guid tenantId,
        string activity,
        string audience,
        Guid serviceRequestId,
        Guid? triggeredByUserId,
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return Task.CompletedTask;

        try
        {
            return _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = "EhcServiceRequest",
                EntityId = serviceRequestId,
                Activity = activity,
                Audience = audience,
                TriggeredByUserId = triggeredByUserId,
                Data = data ?? new Dictionary<string, object>()
            }, cancellationToken);
        }
        catch
        {
            return Task.CompletedTask;
        }
    }
}
