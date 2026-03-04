using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/compliance")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminComplianceController : ControllerBase
{
    private const string AuditExportUploadCategory = "ehc-audit-export";

    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _storageService;
    private readonly ILogger<EhcAdminComplianceController> _logger;

    public EhcAdminComplianceController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IFileStorageService storageService,
        ILogger<EhcAdminComplianceController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult> Summary(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = new EhcComplianceSummaryDto() });

        var dto = new EhcComplianceSummaryDto
        {
            ActiveLegalHolds = await _db.EhcLegalHolds.AsNoTracking().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive, cancellationToken),
            ActiveRetentionCategoryExceptions = await _db.EhcRetentionCategoryExceptions.AsNoTracking().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive, cancellationToken),
            AuditExports = await _db.EhcComplianceAuditExports.AsNoTracking().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
        };

        return Ok(new { success = true, data = dto });
    }

    [HttpGet("retention/category-exceptions")]
    public async Task<ActionResult> ListRetentionCategoryExceptions(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = Array.Empty<EhcRetentionCategoryExceptionDto>() });

        var items = await _db.EhcRetentionCategoryExceptions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.Category)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Category.Name)
            .ToListAsync(cancellationToken);

        var data = items.Select(x => new EhcRetentionCategoryExceptionDto
        {
            Id = x.Id,
            CategoryId = x.CategoryId,
            CategoryName = x.Category?.Name,
            IsActive = x.IsActive,
            AuditEventRetentionDays = x.AuditEventRetentionDays,
            Notes = x.Notes
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost("retention/category-exceptions")]
    public async Task<ActionResult> UpsertRetentionCategoryException([FromBody] UpsertEhcRetentionCategoryExceptionRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new UpsertEhcRetentionCategoryExceptionRequestDto();
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

            if (request.CategoryId == Guid.Empty) return BadRequest(new { success = false, message = "CategoryId is required." });
            request.AuditEventRetentionDays = Math.Clamp(request.AuditEventRetentionDays, 1, 3650);

            var categoryExists = await _db.EhcTicketCategories.AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Id == request.CategoryId, cancellationToken);

            if (!categoryExists) return BadRequest(new { success = false, message = "Invalid category." });

            var entity = await _db.EhcRetentionCategoryExceptions
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.CategoryId == request.CategoryId, cancellationToken);

            var now = DateTime.UtcNow;
            if (entity == null)
            {
                entity = new EhcRetentionCategoryException
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CategoryId = request.CategoryId,
                    IsActive = request.IsActive,
                    AuditEventRetentionDays = request.AuditEventRetentionDays,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName ?? "System"
                };
                _db.EhcRetentionCategoryExceptions.Add(entity);
            }
            else
            {
                entity.IsActive = request.IsActive;
                entity.AuditEventRetentionDays = request.AuditEventRetentionDays;
                entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
                entity.UpdatedAt = now;
                entity.UpdatedBy = _currentUserService.UserName ?? "System";
            }

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting EHC retention category exception");
            return StatusCode(500, new { success = false, message = "Failed to save retention settings." });
        }
    }

    [HttpGet("legal-holds")]
    public async Task<ActionResult> ListLegalHolds([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = Array.Empty<EhcLegalHoldDto>() });

        var q = _db.EhcLegalHolds
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (!includeInactive)
        {
            q = q.Where(x => x.IsActive);
        }

        var items = await q
            .Include(x => x.Ticket)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var data = items.Select(x => new EhcLegalHoldDto
        {
            Id = x.Id,
            TicketId = x.TicketId,
            TicketNumber = x.Ticket?.TicketNumber,
            IsActive = x.IsActive,
            Reason = x.Reason,
            ReferenceNumber = x.ReferenceNumber,
            CreatedAtUtc = x.CreatedAt,
            ReleasedAtUtc = x.ReleasedAtUtc
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost("legal-holds")]
    public async Task<ActionResult> CreateLegalHold([FromBody] CreateEhcLegalHoldRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new CreateEhcLegalHoldRequestDto();
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "User context is required." });
            }

            if (request.TicketId == Guid.Empty) return BadRequest(new { success = false, message = "TicketId is required." });

            var ticket = await _db.EhcTickets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Id == request.TicketId, cancellationToken);

            if (ticket == null) return NotFound(new { success = false, message = "Ticket not found." });

            var alreadyActive = await _db.EhcLegalHolds
                .AsNoTracking()
                .AnyAsync(h => h.TenantId == tenantId && !h.IsDeleted && h.TicketId == request.TicketId && h.IsActive, cancellationToken);

            if (alreadyActive) return BadRequest(new { success = false, message = "An active legal hold already exists for this ticket." });

            var now = DateTime.UtcNow;
            var hold = new EhcLegalHold
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TicketId = request.TicketId,
                IsActive = true,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = actorUserId
            };

            _db.EhcLegalHolds.Add(hold);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC legal hold");
            return StatusCode(500, new { success = false, message = "Failed to create legal hold." });
        }
    }

    [HttpPost("legal-holds/{id:guid}/release")]
    public async Task<ActionResult> ReleaseLegalHold(Guid id, [FromBody] ReleaseEhcLegalHoldRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "User context is required." });
            }

            var hold = await _db.EhcLegalHolds
                .FirstOrDefaultAsync(h => h.TenantId == tenantId && !h.IsDeleted && h.Id == id, cancellationToken);

            if (hold == null) return NotFound(new { success = false, message = "Legal hold not found." });
            if (!hold.IsActive) return Ok(new { success = true });

            var now = DateTime.UtcNow;
            hold.IsActive = false;
            hold.ReleasedAtUtc = now;
            hold.ReleasedByUserId = actorUserId;
            hold.UpdatedAt = now;
            hold.UpdatedBy = _currentUserService.UserName ?? "System";

            if (request != null && !string.IsNullOrWhiteSpace(request.Notes))
            {
                var notes = request.Notes.Trim();
                hold.Reason = string.IsNullOrWhiteSpace(hold.Reason) ? notes : $"{hold.Reason}\n\nRelease notes: {notes}";
            }

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing EHC legal hold {HoldId}", id);
            return StatusCode(500, new { success = false, message = "Failed to release legal hold." });
        }
    }

    [HttpGet("audit-exports")]
    public async Task<ActionResult> ListAuditExports(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = Array.Empty<EhcComplianceAuditExportDto>() });

        var items = await _db.EhcComplianceAuditExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.FileUploadRecord)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var data = new List<EhcComplianceAuditExportDto>(items.Count);
        foreach (var x in items)
        {
            var filePath = x.FileUploadRecord?.FilePath ?? string.Empty;
            var publicUrl = string.IsNullOrWhiteSpace(filePath) ? string.Empty : await _storageService.GetPublicUrlAsync(filePath);

            data.Add(new EhcComplianceAuditExportDto
            {
                Id = x.Id,
                FromUtc = x.FromUtc,
                ToUtc = x.ToUtc,
                TicketId = x.TicketId,
                CategoryId = x.CategoryId,
                FilePath = filePath,
                PublicUrl = publicUrl,
                Sha256 = x.Sha256,
                RowCount = x.RowCount,
                CreatedAtUtc = x.CreatedAt
            });
        }

        return Ok(new { success = true, data });
    }

    [HttpGet("retention/runs")]
    public async Task<ActionResult> ListRetentionRuns([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = Array.Empty<DataRetentionJobRunDto>() });

        take = Math.Clamp(take, 1, 200);

        var runs = await _db.DataRetentionJobRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.JobName == "data-retention")
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(take)
            .Select(r => new DataRetentionJobRunDto
            {
                StartedAtUtc = r.StartedAtUtc,
                CompletedAtUtc = r.CompletedAtUtc,
                Success = r.Success,
                CountsJson = r.CountsJson,
                Error = r.Error
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = runs });
    }

    [HttpGet("report")]
    public async Task<ActionResult> Report(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = new EhcComplianceReportDto { GeneratedAtUtc = DateTime.UtcNow } });

        var now = DateTime.UtcNow;

        var summary = new EhcComplianceSummaryDto
        {
            ActiveLegalHolds = await _db.EhcLegalHolds.AsNoTracking().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive, cancellationToken),
            ActiveRetentionCategoryExceptions = await _db.EhcRetentionCategoryExceptions.AsNoTracking().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive, cancellationToken),
            AuditExports = await _db.EhcComplianceAuditExports.AsNoTracking().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken)
        };

        var exceptions = await _db.EhcRetentionCategoryExceptions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.Category)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Category.Name)
            .Select(x => new EhcRetentionCategoryExceptionDto
            {
                Id = x.Id,
                CategoryId = x.CategoryId,
                CategoryName = x.Category.Name,
                IsActive = x.IsActive,
                AuditEventRetentionDays = x.AuditEventRetentionDays,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);

        var holds = await _db.EhcLegalHolds
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.Ticket)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new EhcLegalHoldDto
            {
                Id = x.Id,
                TicketId = x.TicketId,
                TicketNumber = x.Ticket.TicketNumber,
                IsActive = x.IsActive,
                Reason = x.Reason,
                ReferenceNumber = x.ReferenceNumber,
                CreatedAtUtc = x.CreatedAt,
                ReleasedAtUtc = x.ReleasedAtUtc
            })
            .ToListAsync(cancellationToken);

        var exports = await _db.EhcComplianceAuditExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.FileUploadRecord)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var exportDtos = new List<EhcComplianceAuditExportDto>(exports.Count);
        foreach (var x in exports)
        {
            var filePath = x.FileUploadRecord?.FilePath ?? string.Empty;
            var publicUrl = string.IsNullOrWhiteSpace(filePath) ? string.Empty : await _storageService.GetPublicUrlAsync(filePath);
            exportDtos.Add(new EhcComplianceAuditExportDto
            {
                Id = x.Id,
                FromUtc = x.FromUtc,
                ToUtc = x.ToUtc,
                TicketId = x.TicketId,
                CategoryId = x.CategoryId,
                FilePath = filePath,
                PublicUrl = publicUrl,
                Sha256 = x.Sha256,
                RowCount = x.RowCount,
                CreatedAtUtc = x.CreatedAt
            });
        }

        var runs = await _db.DataRetentionJobRuns
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.JobName == "data-retention")
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(20)
            .Select(r => new DataRetentionJobRunDto
            {
                StartedAtUtc = r.StartedAtUtc,
                CompletedAtUtc = r.CompletedAtUtc,
                Success = r.Success,
                CountsJson = r.CountsJson,
                Error = r.Error
            })
            .ToListAsync(cancellationToken);

        var report = new EhcComplianceReportDto
        {
            GeneratedAtUtc = now,
            Summary = summary,
            RetentionCategoryExceptions = exceptions,
            LegalHolds = holds,
            AuditExports = exportDtos,
            RetentionRuns = runs
        };

        return Ok(new { success = true, data = report });
    }

    [HttpPost("audit-exports")]
    public async Task<ActionResult> CreateAuditExport([FromBody] CreateEhcComplianceAuditExportRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new CreateEhcComplianceAuditExportRequestDto();
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "User context is required." });
            }

            var fromUtc = request.FromUtc.Kind == DateTimeKind.Utc ? request.FromUtc : DateTime.SpecifyKind(request.FromUtc, DateTimeKind.Utc);
            var toUtc = request.ToUtc.Kind == DateTimeKind.Utc ? request.ToUtc : DateTime.SpecifyKind(request.ToUtc, DateTimeKind.Utc);
            if (toUtc <= fromUtc) return BadRequest(new { success = false, message = "ToUtc must be after FromUtc." });

            // Guardrail: maximum 31 days per export by default.
            if ((toUtc - fromUtc) > TimeSpan.FromDays(31))
            {
                return BadRequest(new { success = false, message = "Date range too large (max 31 days)." });
            }

            var q = from e in _db.EhcTicketAuditEvents.AsNoTracking()
                    join t in _db.EhcTickets.AsNoTracking() on e.TicketId equals t.Id
                    where e.TenantId == tenantId && !e.IsDeleted
                       && t.TenantId == tenantId && !t.IsDeleted
                       && e.CreatedAt >= fromUtc && e.CreatedAt <= toUtc
                    select new
                    {
                        e.Id,
                        e.TicketId,
                        TicketNumber = t.TicketNumber,
                        t.CategoryId,
                        e.EventType,
                        e.Title,
                        e.Body,
                        e.IsInternal,
                        e.ActorUserId,
                        e.DataJson,
                        e.CreatedAt
                    };

            if (request.TicketId.HasValue && request.TicketId.Value != Guid.Empty)
            {
                q = q.Where(x => x.TicketId == request.TicketId.Value);
            }

            if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
            {
                q = q.Where(x => x.CategoryId == request.CategoryId.Value);
            }

            var rows = await q
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Take(100_000)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var exportPayload = new
            {
                exportedAtUtc = now,
                tenantId,
                fromUtc,
                toUtc,
                ticketId = request.TicketId,
                categoryId = request.CategoryId,
                rowCount = rows.Count,
                rows
            };

            var json = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = false });
            var bytes = Encoding.UTF8.GetBytes(json);

            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var fileName = $"ehc_audit_export_{fromUtc:yyyyMMddHHmmss}_{toUtc:yyyyMMddHHmmss}.json";

            await using var ms = new MemoryStream(bytes, writable: false);
            var upload = await _storageService.UploadFileAsync(new ErpSystem.Core.Models.FileUploadRequest
            {
                FileStream = ms,
                FileName = fileName,
                ContentType = "application/json",
                FileSize = bytes.Length,
                Category = AuditExportUploadCategory,
                TenantId = tenantId.ToString(),
                Metadata = new Dictionary<string, string>
                {
                    ["sha256"] = sha,
                    ["exportedAtUtc"] = now.ToString("O")
                }
            });

            if (!upload.Success)
            {
                return StatusCode(500, new { success = false, message = upload.ErrorMessage ?? "Failed to upload export file." });
            }

            var record = new FileUploadRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Category = AuditExportUploadCategory,
                FilePath = upload.FilePath,
                StoredFileName = upload.FileName,
                OriginalFileName = fileName,
                ContentType = upload.ContentType,
                FileSize = upload.FileSize,
                StorageProvider = upload.StorageProvider,
                UploadedByUserId = actorUserId,
                VirusScanStatus = ErpSystem.Core.Enums.FileVirusScanStatus.Skipped,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = actorUserId
            };

            _db.FileUploadRecords.Add(record);

            var export = new EhcComplianceAuditExport
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TicketId = request.TicketId.HasValue && request.TicketId.Value != Guid.Empty ? request.TicketId.Value : null,
                CategoryId = request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty ? request.CategoryId.Value : null,
                FileUploadRecordId = record.Id,
                Sha256 = sha,
                RowCount = rows.Count,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System",
                CreatedById = actorUserId
            };

            _db.EhcComplianceAuditExports.Add(export);
            await _db.SaveChangesAsync(cancellationToken);

            var publicUrl = await _storageService.GetPublicUrlAsync(record.FilePath);
            var dto = new EhcComplianceAuditExportDto
            {
                Id = export.Id,
                FromUtc = export.FromUtc,
                ToUtc = export.ToUtc,
                TicketId = export.TicketId,
                CategoryId = export.CategoryId,
                FilePath = record.FilePath,
                PublicUrl = publicUrl,
                Sha256 = export.Sha256,
                RowCount = export.RowCount,
                CreatedAtUtc = export.CreatedAt
            };

            return Ok(new { success = true, data = dto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC audit export");
            return StatusCode(500, new { success = false, message = "Failed to create audit export." });
        }
    }
}
