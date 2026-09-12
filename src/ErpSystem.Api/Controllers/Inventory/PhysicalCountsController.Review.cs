using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

public partial class PhysicalCountsController
{
    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult> Review(Guid id, [FromBody] PhysicalCountMutationRequest request)
    {
        try { await _countService.ReviewCountAsync(id, GetCurrentUserId(), request); return Ok(new { message = "Review the variance and correct quantities before submitting." }); }
        catch (Exception ex) { return ControlledError(ex, "review count", id); }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult> SubmitReviewed(Guid id, [FromBody] PhysicalCountMutationRequest request)
    {
        try { await _countService.SubmitReviewedCountAsync(id, GetCurrentUserId(), request); return Ok(new { message = "Count submitted for approval. Stock is unchanged." }); }
        catch (Exception ex) { return ControlledError(ex, "submit reviewed count", id); }
    }

    [HttpGet("decision-options")]
    public async Task<ActionResult<PhysicalCountDecisionSetup>> GetDecisionOptions([FromQuery] Guid? countId, CancellationToken ct)
    {
        try {
            var tenantId = RequiredTenantId();
            if (!countId.HasValue || countId.Value == Guid.Empty)
                return BadRequest(new { code = "PHYSICAL_COUNT_REQUIRED", message = "Open a saved physical count to load its decisions." });

            // Derive the authorization scope from the saved tenant-owned count, never caller-supplied scope IDs.
            var count = await _db.PhysicalCounts.AsNoTracking().Where(c =>
                c.Id == countId.Value && c.TenantId == tenantId && !c.IsDeleted)
                .Select(c => new { c.WarehouseId, c.LocationId, c.CountNumber }).SingleOrDefaultAsync(ct);
            if (count is null)
                return NotFound(new { code = "PHYSICAL_COUNT_NOT_FOUND", message = "The physical count was not found in this tenant." });

            var access = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest {
                PermissionCode = "procurement.inventory.read", WarehouseId = count.WarehouseId,
                LocationId = count.LocationId, RequireLocationScope = true,
                SourceType = "PhysicalCount", SourceReference = count.CountNumber
            }, HttpContext.TraceIdentifier, ct);
            if (!access.Allowed) throw new ProcurementAccessAuthorizationException(access.Message);
            return Ok(await LoadDecisionSetupAsync(tenantId, ct));
        } catch (Exception ex) { return ControlledError(ex, "read count decisions", countId ?? Guid.Empty); }
    }

    [HttpGet("decision-setup")]
    public Task<ActionResult<PhysicalCountDecisionSetup>> GetDecisionSetup(CancellationToken ct) => ReadDecisionSetup(ct);

    private async Task RequireDecisionSetupAccess(CancellationToken ct)
    {
        _ = RequiredTenantId();
        var access = await _accessControl.EnforceCapabilityAsync(new ProcurementAccessCapabilityRequest {
            PermissionCode = "procurement.inventory.master-data.manage",
            SourceType = "PhysicalCountDecisionSetup", SourceReference = "Count decisions"
        }, HttpContext.TraceIdentifier, ct);
        if (!access.Allowed) throw new ProcurementAccessAuthorizationException(access.Message);
    }

    private async Task<PhysicalCountDecisionSetup> LoadDecisionSetupAsync(Guid tenantId, CancellationToken ct)
    {
        var setting = await _db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s =>
            s.TenantId == tenantId && s.Key == PhysicalCountDecisionPolicy.SettingKey && !s.IsDeleted, ct);
        return PhysicalCountDecisionPolicy.Read(setting?.Value);
    }

    private async Task<ActionResult<PhysicalCountDecisionSetup>> ReadDecisionSetup(CancellationToken ct)
    {
        try {
            await RequireDecisionSetupAccess(ct);
            return Ok(await LoadDecisionSetupAsync(RequiredTenantId(), ct));
        } catch (Exception ex) { return ControlledError(ex, "read count decision setup", Guid.Empty); }
    }

    [HttpPut("decision-setup")]
    public async Task<ActionResult<PhysicalCountDecisionSetup>> SaveDecisionSetup([FromBody] SavePhysicalCountDecisionSetup request, CancellationToken ct)
    {
        try {
            await RequireDecisionSetupAccess(ct);
            var value = PhysicalCountDecisionPolicy.Serialize(request.Decisions);
            var tenant = RequiredTenantId();
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync<ActionResult<PhysicalCountDecisionSetup>>(async () => {
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var setting = await _db.SystemSettings.SingleOrDefaultAsync(s =>
                    s.TenantId == tenant && s.Key == PhysicalCountDecisionPolicy.SettingKey, ct);
                var before = setting is { IsDeleted: false } ? setting.Value : null;
                if (PhysicalCountDecisionPolicy.Revision(before) != request.Revision)
                    throw new InvalidOperationException("The decision setup changed. Refresh before saving.");
                if (setting == null) {
                    setting = new SystemSettings { TenantId = tenant, Key = PhysicalCountDecisionPolicy.SettingKey };
                    _db.SystemSettings.Add(setting);
                }
                setting.IsDeleted = false;
                setting.Value = value;
                setting.Description = "Physical-count approval labels and enforced consequences.";
                _db.AuditLogs.Add(new AuditLog {
                    TenantId = tenant, UserId = GetCurrentUserId(), Username = _currentUser.UserName ?? "Unknown",
                    Action = "PhysicalCountDecisionSetup.Saved", Resource = "PhysicalCountDecisionSetup", ResourceId = setting.Id.ToString(),
                    OldValues = JsonSerializer.Serialize(PhysicalCountDecisionPolicy.Read(before)),
                    NewValues = JsonSerializer.Serialize(PhysicalCountDecisionPolicy.Read(value)),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Local", Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Ok(PhysicalCountDecisionPolicy.Read(value));
            });
        } catch (Exception ex) { _db.ChangeTracker.Clear(); return ControlledError(ex, "save count decision setup", Guid.Empty); }
    }
}
