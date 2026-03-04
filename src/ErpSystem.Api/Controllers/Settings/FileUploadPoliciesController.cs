using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Settings;

[ApiController]
[Route("api/settings/file-uploads")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
public sealed class FileUploadPoliciesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public FileUploadPoliciesController(ErpSystem.Data.ApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;

    public class FileUploadPolicyDto
    {
        public string Category { get; set; } = "*";
        public bool IsEnabled { get; set; } = true;
        public long? MaxFileSizeBytes { get; set; }
        public long? MaxTenantTotalBytes { get; set; }
        public long? MaxCategoryTotalBytes { get; set; }
        public string? AllowedExtensionsCsv { get; set; }
        public string? AllowedMimeTypesCsv { get; set; }
        public bool RequireVirusScan { get; set; }
    }

    public sealed class UpsertFileUploadPolicyRequestDto : FileUploadPolicyDto { }

    public sealed class FileUploadUsageDto
    {
        public long TotalBytes { get; set; }
        public int TotalFiles { get; set; }
        public List<FileUploadUsageByCategoryDto> ByCategory { get; set; } = new();
    }

    public sealed class FileUploadUsageByCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public int Files { get; set; }
    }

    private static string NormalizeCategory(string category)
    {
        var c = (category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(c)) return string.Empty;
        if (c == "*") return "*";
        c = c.Replace('\\', '/');
        c = c.Replace("..", string.Empty);
        c = c.Trim('/');
        return c.ToLowerInvariant();
    }

    [HttpGet("policies")]
    public async Task<ActionResult> ListPolicies(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = Array.Empty<FileUploadPolicyDto>() });

        var items = await _db.FileUploadPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .OrderBy(p => p.Category == "*" ? 0 : 1)
            .ThenBy(p => p.Category)
            .Select(p => new FileUploadPolicyDto
            {
                Category = p.Category,
                IsEnabled = p.IsEnabled,
                MaxFileSizeBytes = p.MaxFileSizeBytes,
                MaxTenantTotalBytes = p.MaxTenantTotalBytes,
                MaxCategoryTotalBytes = p.MaxCategoryTotalBytes,
                AllowedExtensionsCsv = p.AllowedExtensionsCsv,
                AllowedMimeTypesCsv = p.AllowedMimeTypesCsv,
                RequireVirusScan = p.RequireVirusScan
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpPut("policies/{category}")]
    public async Task<ActionResult> UpsertPolicy(string category, [FromBody] UpsertFileUploadPolicyRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new UpsertFileUploadPolicyRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

        category = NormalizeCategory(category);
        if (string.IsNullOrWhiteSpace(category)) return BadRequest(new { success = false, message = "Category is required." });

        var entity = await _db.FileUploadPolicies.FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Category == category, cancellationToken);
        if (entity == null)
        {
            entity = new FileUploadPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Category = category,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName,
                CreatedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null
            };
            _db.FileUploadPolicies.Add(entity);
        }

        entity.IsEnabled = request.IsEnabled;
        entity.MaxFileSizeBytes = request.MaxFileSizeBytes;
        entity.MaxTenantTotalBytes = request.MaxTenantTotalBytes;
        entity.MaxCategoryTotalBytes = request.MaxCategoryTotalBytes;
        entity.AllowedExtensionsCsv = string.IsNullOrWhiteSpace(request.AllowedExtensionsCsv) ? null : request.AllowedExtensionsCsv.Trim();
        entity.AllowedMimeTypesCsv = string.IsNullOrWhiteSpace(request.AllowedMimeTypesCsv) ? null : request.AllowedMimeTypesCsv.Trim();
        entity.RequireVirusScan = request.RequireVirusScan;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var luid) ? luid : null;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("policies/{category}")]
    public async Task<ActionResult> DeletePolicy(string category, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

        category = NormalizeCategory(category);
        if (string.IsNullOrWhiteSpace(category) || category == "*")
            return BadRequest(new { success = false, message = "Invalid category." });

        var entity = await _db.FileUploadPolicies.FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Category == category, cancellationToken);
        if (entity == null) return Ok(new { success = true });

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _currentUserService.UserName;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    [HttpGet("usage")]
    public async Task<ActionResult> GetUsage(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty) return Ok(new { success = true, data = new FileUploadUsageDto() });

        var byCategory = await _db.FileUploadRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .GroupBy(r => r.Category)
            .Select(g => new FileUploadUsageByCategoryDto
            {
                Category = g.Key,
                Bytes = g.Sum(x => x.FileSize),
                Files = g.Count()
            })
            .OrderByDescending(x => x.Bytes)
            .ToListAsync(cancellationToken);

        var totalBytes = byCategory.Sum(x => x.Bytes);
        var totalFiles = byCategory.Sum(x => x.Files);

        return Ok(new
        {
            success = true,
            data = new FileUploadUsageDto
            {
                TotalBytes = totalBytes,
                TotalFiles = totalFiles,
                ByCategory = byCategory
            }
        });
    }
}
