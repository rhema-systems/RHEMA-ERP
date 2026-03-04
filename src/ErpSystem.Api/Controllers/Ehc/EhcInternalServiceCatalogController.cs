using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/service-catalog")]
[Authorize(Policy = "InternalOnly")]
public sealed class EhcInternalServiceCatalogController : ControllerBase
{
    private static readonly Regex RequestNumberRegex =
        new("^SR-(?<yy>\\d{2})-(?<seq>\\d{6})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IWorkflowService _workflowService;
    private readonly ErpSystem.Core.Interfaces.IFileStorageService _storageService;
    private readonly IAppEventBus _appEventBus;
    private readonly ILogger<EhcInternalServiceCatalogController> _logger;

    public EhcInternalServiceCatalogController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IWorkflowEngine workflowEngine,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IWorkflowService workflowService,
        ErpSystem.Core.Interfaces.IFileStorageService storageService,
        IAppEventBus appEventBus,
        ILogger<EhcInternalServiceCatalogController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _workflowEngine = workflowEngine;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _workflowService = workflowService;
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
    public async Task<ActionResult> ListRequests([FromQuery] int take = 200, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcServiceRequestSummaryDto>() });
        }

        if (take < 1) take = 50;
        if (take > 1000) take = 1000;

        var items = await _db.EhcServiceRequests
            .AsNoTracking()
            .Include(r => r.RequestType)
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
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

    [HttpPost("requests")]
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
                CreatedBy = _currentUserService.UserName ?? "System",
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
                CreatedBy = _currentUserService.UserName ?? "System",
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

            var requesterActionUrl = await GetRequesterActionUrlAsync(tenantId, requesterUserId, entity.Id, cancellationToken);
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
                    ["ActionUrl"] = requesterActionUrl
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating internal service request");
            return StatusCode(500, new { success = false, message = "Failed to create service request" });
        }
    }

    [HttpGet("approvals/pending")]
    public async Task<ActionResult> ListPendingApprovals(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<WorkflowApprovalItem>() });
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var items = await _workflowService.GetPendingApprovalsAsync(userId);
        var data = (items ?? new List<WorkflowApprovalItem>())
            .Where(i =>
                string.Equals(i.EntityType, "SERVICE_REQUEST", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.EntityType, "ServiceRequest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.EntityType, "Service Request", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.SubmittedAt)
            .ToList();

        return Ok(new { success = true, data });
    }

    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult> GetRequest(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return NotFound(new { success = false, message = "Not found." });
        }

        var r = await _db.EhcServiceRequests
            .AsNoTracking()
            .Include(x => x.RequestType)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

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
                .Where(a => !a.IsDeleted)
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

            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            {
                return Unauthorized();
            }

            var r = await _db.EhcServiceRequests
                .Include(x => x.RequestType)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
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
                IsInternal = request.IsInternal,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = actorUserId
            };

            _db.EhcServiceRequestAttachments.Add(att);
            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = r.Id,
                EventType = request.IsInternal ? "InternalAttachment" : "Attachment",
                Title = "Attachment uploaded",
                Body = $"{(request.IsInternal ? "Internal" : "User")} attachment uploaded: {att.FileName}.",
                IsInternal = request.IsInternal,
                ActorUserId = actorUserId,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = actorUserId
            });

            await _db.SaveChangesAsync(cancellationToken);

            var customerAudience = !att.IsInternal;
            if (customerAudience)
            {
                var requesterActionUrl = await GetRequesterActionUrlAsync(tenantId, r.RequesterUserId, r.Id, cancellationToken);
                await PublishServiceRequestTopicAsync(
                    tenantId,
                    activity: "Attachment",
                    audience: "Customer",
                    serviceRequestId: r.Id,
                    triggeredByUserId: actorUserId,
                    data: new Dictionary<string, object>
                    {
                        ["serviceRequestId"] = r.Id,
                        ["requestNumber"] = r.RequestNumber,
                        ["fileName"] = att.FileName,
                        ["TargetUserId"] = r.RequesterUserId,
                        ["ActionUrl"] = requesterActionUrl
                    },
                    cancellationToken);
            }

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Attachment",
                audience: "Internal",
                serviceRequestId: r.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = r.Id,
                    ["requestNumber"] = r.RequestNumber,
                    ["requestTypeName"] = r.RequestType?.Name ?? string.Empty,
                    ["requesterUserId"] = r.RequesterUserId,
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

    public sealed class UpdateServiceRequestStatusDto
    {
        public string? Notes { get; set; }
    }

    [HttpPost("requests/{id:guid}/fulfill")]
    public async Task<ActionResult> Fulfill(Guid id, [FromBody] UpdateServiceRequestStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });
            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty) return Unauthorized();

            var r = await _db.EhcServiceRequests.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
            if (r == null) return NotFound(new { success = false, message = "Request not found." });

            if (r.Status is not (EhcServiceRequestStatus.Approved or EhcServiceRequestStatus.PendingApproval))
            {
                return BadRequest(new { success = false, message = $"Cannot fulfill in status '{r.Status}'." });
            }

            r.Status = EhcServiceRequestStatus.Fulfilled;
            r.UpdatedAt = DateTime.UtcNow;
            r.UpdatedBy = _currentUserService.UserName ?? "System";

            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = r.Id,
                EventType = "Fulfilled",
                Title = "Request fulfilled",
                Body = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                IsInternal = false,
                ActorUserId = actorUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = actorUserId
            });

            await _db.SaveChangesAsync(cancellationToken);

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Fulfilled",
                audience: "Customer",
                serviceRequestId: r.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = r.Id,
                    ["requestNumber"] = r.RequestNumber,
                    ["TargetUserId"] = r.RequesterUserId,
                    ["ActionUrl"] = await GetRequesterActionUrlAsync(tenantId, r.RequesterUserId, r.Id, cancellationToken)
                },
                cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fulfilling service request {ServiceRequestId}", id);
            return StatusCode(500, new { success = false, message = "Failed to fulfill request" });
        }
    }

    [HttpPost("requests/{id:guid}/close")]
    public async Task<ActionResult> Close(Guid id, [FromBody] UpdateServiceRequestStatusDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });
            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty) return Unauthorized();

            var r = await _db.EhcServiceRequests.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
            if (r == null) return NotFound(new { success = false, message = "Request not found." });

            if (r.Status is EhcServiceRequestStatus.Cancelled or EhcServiceRequestStatus.Closed)
            {
                return Ok(new { success = true });
            }

            r.Status = EhcServiceRequestStatus.Closed;
            r.UpdatedAt = DateTime.UtcNow;
            r.UpdatedBy = _currentUserService.UserName ?? "System";

            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = r.Id,
                EventType = "Closed",
                Title = "Request closed",
                Body = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                IsInternal = false,
                ActorUserId = actorUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = actorUserId
            });

            await _db.SaveChangesAsync(cancellationToken);

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Closed",
                audience: "Customer",
                serviceRequestId: r.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = r.Id,
                    ["requestNumber"] = r.RequestNumber,
                    ["TargetUserId"] = r.RequesterUserId,
                    ["ActionUrl"] = await GetRequesterActionUrlAsync(tenantId, r.RequesterUserId, r.Id, cancellationToken)
                },
                cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing service request {ServiceRequestId}", id);
            return StatusCode(500, new { success = false, message = "Failed to close request" });
        }
    }

    public sealed class ProcessServiceRequestApprovalDto
    {
        public string? Comments { get; set; }
        public string? Reason { get; set; }
    }

    [HttpPost("requests/{id:guid}/approve")]
    public async Task<ActionResult> Approve(Guid id, [FromBody] ProcessServiceRequestApprovalDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var request = await _db.EhcServiceRequests
                .Include(r => r.RequestType)
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, cancellationToken);
            if (request == null)
            {
                return NotFound(new { success = false, message = "Request not found." });
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync("ServiceRequest", id, userId);
            if (!canApprove)
            {
                return Forbid();
            }

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                "ServiceRequest",
                id,
                userId,
                "Approve",
                dto.Comments);

            var adapter = _workflowStatusAdapterRegistry.GetAdapter("ServiceRequest");
            adapter.ApplyApprovalOutcome(request, workflowResult.Outcome, userId);

            request.WorkflowInstanceId ??= workflowResult.ExecutionResult.WorkflowInstanceId;
            request.UpdatedAt = DateTime.UtcNow;
            request.UpdatedBy = _currentUserService.UserName ?? "System";

            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = request.Id,
                EventType = "Approved",
                Title = "Request approved",
                Body = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim(),
                IsInternal = false,
                ActorUserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = userId
            });

            await _db.SaveChangesAsync(cancellationToken);

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Approved",
                audience: "Customer",
                serviceRequestId: request.Id,
                triggeredByUserId: userId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = request.Id,
                    ["requestNumber"] = request.RequestNumber,
                    ["TargetUserId"] = request.RequesterUserId,
                    ["ActionUrl"] = await GetRequesterActionUrlAsync(tenantId, request.RequesterUserId, request.Id, cancellationToken)
                },
                cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving service request {ServiceRequestId}", id);
            return StatusCode(500, new { success = false, message = "Failed to approve service request" });
        }
    }

    [HttpPost("requests/{id:guid}/reject")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] ProcessServiceRequestApprovalDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                return BadRequest(new { success = false, message = "Rejection reason is required." });
            }

            var request = await _db.EhcServiceRequests
                .Include(r => r.RequestType)
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted, cancellationToken);
            if (request == null)
            {
                return NotFound(new { success = false, message = "Request not found." });
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync("ServiceRequest", id, userId);
            if (!canApprove)
            {
                return Forbid();
            }

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                "ServiceRequest",
                id,
                userId,
                "Reject",
                dto.Reason);

            var adapter = _workflowStatusAdapterRegistry.GetAdapter("ServiceRequest");
            adapter.ApplyApprovalOutcome(request, workflowResult.Outcome, userId, dto.Reason);

            request.WorkflowInstanceId ??= workflowResult.ExecutionResult.WorkflowInstanceId;
            request.UpdatedAt = DateTime.UtcNow;
            request.UpdatedBy = _currentUserService.UserName ?? "System";

            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = request.Id,
                EventType = "Rejected",
                Title = "Request rejected",
                Body = dto.Reason.Trim(),
                IsInternal = false,
                ActorUserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = userId
            });

            await _db.SaveChangesAsync(cancellationToken);

            await PublishServiceRequestTopicAsync(
                tenantId,
                activity: "Rejected",
                audience: "Customer",
                serviceRequestId: request.Id,
                triggeredByUserId: userId,
                data: new Dictionary<string, object>
                {
                    ["serviceRequestId"] = request.Id,
                    ["requestNumber"] = request.RequestNumber,
                    ["TargetUserId"] = request.RequesterUserId,
                    ["reason"] = dto.Reason.Trim(),
                    ["ActionUrl"] = await GetRequesterActionUrlAsync(tenantId, request.RequesterUserId, request.Id, cancellationToken)
                },
                cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting service request {ServiceRequestId}", id);
            return StatusCode(500, new { success = false, message = "Failed to reject service request" });
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

    private async Task<string> GetRequesterActionUrlAsync(Guid tenantId, Guid requesterUserId, Guid serviceRequestId, CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _db.Users
                .AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.Id == requesterUserId && u.IsActive)
                .Select(u => u.AuthenticationProvider)
                .FirstOrDefaultAsync(cancellationToken);

            return provider == AuthenticationProvider.Local
                ? $"/external-portal/support/requests/{serviceRequestId}"
                : $"/helpdesk/requests/{serviceRequestId}";
        }
        catch
        {
            return $"/helpdesk/requests/{serviceRequestId}";
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
}
