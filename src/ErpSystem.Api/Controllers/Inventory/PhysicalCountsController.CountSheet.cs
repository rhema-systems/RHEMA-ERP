using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Services.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Inventory;

public partial class PhysicalCountsController
{
    public sealed record CountSheetLineVersion(Guid Id, string RowVersion);

    [HttpPost("{id:guid}/count-sheet")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<PhysicalCountSheetBinding>> ImportCountSheet(Guid id, [FromForm] IFormFile file,
        [FromForm] string rowVersion, [FromForm] string lineVersions, [FromForm] Guid idempotencyKey, CancellationToken cancellationToken)
    {
        ControlledFileUploadResult? upload = null;
        var actor = GetCurrentUserId();
        var tenant = RequiredTenantId();
        var key = $"sheet:{idempotencyKey:N}";
        try
        {
            if (actor == Guid.Empty) return Unauthorized();
            if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose an .xlsx count sheet smaller than 10 MB.");
            if (idempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(rowVersion))
                throw new InvalidOperationException("Reload the count before uploading its sheet.");
            var count = await _countService.GetByIdAsync(id) ?? throw new InvalidOperationException("Count not found.");
            var access = await _accessControl.EnforceCapabilityAsync(new ErpSystem.Core.DTOs.Procurement.ProcurementAccessCapabilityRequest {
                PermissionCode = "procurement.inventory.count", WarehouseId = count.WarehouseId, LocationId = count.LocationId,
                RequireLocationScope = true, SourceType = "PhysicalCount", SourceReference = count.CountNumber
            }, HttpContext.TraceIdentifier, cancellationToken);
            if (!access.Allowed) return Forbid();
            var versions = JsonSerializer.Deserialize<List<CountSheetLineVersion>>(lineVersions,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Count-line versions are required.");
            if (versions.Count != count.Items.Count || versions.Select(v => v.Id).Distinct().Count() != versions.Count ||
                versions.Any(v => string.IsNullOrWhiteSpace(v.RowVersion) || !count.Items.Any(i => i.Id == v.Id)))
                throw new InvalidOperationException("Reload the count; its item list has changed.");
            await using var content = new MemoryStream();
            await file.CopyToAsync(content, cancellationToken);
            var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                Convert.ToHexString(SHA256.HashData(content.ToArray())) + "|" + rowVersion + "|" +
                JsonSerializer.Serialize(versions.OrderBy(v => v.Id)))));

            async Task<PhysicalCountSheetBinding?> Replay()
            {
                var action = await _db.Set<PhysicalCountAction>().AsNoTracking().SingleOrDefaultAsync(a =>
                    a.TenantId == tenant && a.PhysicalCountId == id && a.ActionType == PhysicalCountActionType.CountRecorded &&
                    a.IdempotencyKey == key && !a.IsDeleted, cancellationToken);
                if (action is null) return null;
                var binding = PhysicalCountSheetLineage.Read(action);
                if (action.ActorUserId != actor || binding?.RequestHash != requestHash)
                    throw new InvalidOperationException("This upload retry belongs to a different file or user.");
                return binding;
            }
            if (await Replay() is { } replay) return Ok(replay);
            if (count.Status is not ("InProgress" or "UnderReview")) throw new InvalidOperationException("Count sheets can only be saved during counting or review.");

            // Scan before interpreting the workbook or changing any count quantities.
            upload = await _controlledFiles.UploadAsync(new ControlledFileUploadRequest {
                TenantId = tenant, ActorUserId = actor, ActorName = _currentUser.UserName,
                Category = ControlledFileUploadCategories.InventoryStockTakingEvidence,
                FileName = Path.GetFileName(file.FileName), ContentType = file.ContentType, FileSize = file.Length,
                OpenReadStream = () => new MemoryStream(content.ToArray(), writable: false)
            }, cancellationToken);

            var saved = await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                _db.ChangeTracker.Clear();
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                // Serialize imports for this count, including the action-chain sequence.
                var locked = await _db.Set<PhysicalCount>().FromSqlInterpolated(
                    $"SELECT * FROM dbo.PhysicalCounts WITH (UPDLOCK,HOLDLOCK) WHERE Id={id} AND TenantId={tenant} AND IsDeleted=0")
                    .SingleAsync(cancellationToken);
                if (await Replay() is { } retried) { await tx.CommitAsync(cancellationToken); return retried; }
                if (locked.Status is not ("InProgress" or "UnderReview") || Convert.ToBase64String(locked.RowVersion) != rowVersion)
                    throw new InvalidOperationException("The count changed. Reload before replacing its count sheet.");
                var fresh = await _countService.GetByIdAsync(id) ?? throw new InvalidOperationException("Count not found.");
                foreach (var item in fresh.Items)
                    if (versions.SingleOrDefault(v => v.Id == item.Id)?.RowVersion != item.RowVersion)
                        throw new InvalidOperationException("A count line changed. Reload before replacing the sheet.");
                content.Position = 0;
                var rows = PhysicalCountSheetReader.Read(content, fresh.Items);
                foreach (var row in rows)
                {
                    row.RowVersion = versions.Single(v => v.Id == row.PhysicalCountItemId).RowVersion;
                    row.IdempotencyKey = $"{idempotencyKey:N}:{row.PhysicalCountItemId:N}";
                    await _countService.RecordCountItemAsync(row, actor);
                }
                var link = await _centralDocuments.RegisterAsync(new CentralDocumentRepositoryRegistration {
                    TenantId = tenant, ActorUserId = actor, ActorName = _currentUser.UserName,
                    FileUploadRecordId = upload.Record.Id, SourceModule = "Inventory", SourceLabel = "Imported count sheet",
                    SourceEntityType = "PhysicalCount", SourceRecordId = id, SourceRecordReference = fresh.CountNumber,
                    Title = Path.GetFileName(file.FileName), DocumentType = "PhysicalCountSheet", AccessProfile = "Module restricted",
                    VersionStatus = "Published", ChangeSummary = "Quantities saved from this count sheet. Earlier sheets remain in history."
                }, cancellationToken);
                var binding = new PhysicalCountSheetBinding(link.DocumentVersionId, requestHash, rows.Count, fresh.Items.Count - rows.Count);
                await _countService.RetainImportedCountSheetAsync(id, binding, actor, key);
                await tx.CommitAsync(cancellationToken);
                return binding;
            });
            // A concurrent retry may already have committed the same request with its own upload.
            if (!await _db.CentralDocumentVersions.AnyAsync(v => v.TenantId == tenant && v.FileUploadRecordId == upload.Record.Id, cancellationToken))
            {
                try { await _controlledFiles.DeleteAsync(tenant, upload.Record.Id, actor, cancellationToken); }
                catch (Exception cleanup) { _logger.LogWarning(cleanup, "Could not clean up duplicate count-sheet upload {UploadId}", upload.Record.Id); }
            }
            return Ok(saved);
        }
        catch (Exception ex)
        {
            if (upload != null)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    // Never delete a committed file after an uncertain commit response.
                    if (!await _db.CentralDocumentVersions.AnyAsync(v => v.TenantId == tenant && v.FileUploadRecordId == upload.Record.Id, cancellationToken))
                        await _controlledFiles.DeleteAsync(tenant, upload.Record.Id, actor, cancellationToken);
                }
                catch (Exception cleanup) { _logger.LogWarning(cleanup, "Could not clean up unbound count-sheet upload {UploadId}", upload.Record.Id); }
            }
            if (ex is ControlledFileUploadException scan) return StatusCode(scan.StatusCode, new { code = scan.Code, message = scan.Message });
            if (ex is JsonException or InvalidDataException or FormatException)
                return BadRequest(new { code = "COUNT_SHEET_INVALID", message = "The count sheet or its item versions could not be read. Reload the count and use the downloaded template." });
            return ControlledError(ex, "save count sheet", id);
        }
    }
}
