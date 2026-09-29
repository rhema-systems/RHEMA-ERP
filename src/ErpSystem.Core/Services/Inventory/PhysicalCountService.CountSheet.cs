using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.DocumentManagement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class PhysicalCountService
{
    public async Task RetainImportedCountSheetAsync(Guid countId, PhysicalCountSheetBinding sheet, Guid userId, string idempotencyKey)
    {
        EnsureActor(userId);
        var count = await _countRepository.GetByIdAsync(countId) ?? throw new ArgumentException("Count not found.");
        await EnsureAccessAsync(count, "procurement.inventory.count");
        EnsureCounterCanEdit(count, userId);
        await EnsureCurrentCounterIdentityAsync(count, userId);
        var linked = await _unitOfWork.Repository<CentralDocumentVersion>().GetQueryable()
            .AnyAsync(v => v.Id == sheet.CentralDocumentVersionId && v.TenantId == count.TenantId && !v.IsDeleted &&
                v.DocumentRecord.SourceRecordId == count.Id && v.DocumentRecord.SourceEntityType == "PhysicalCount" &&
                v.DocumentRecord.TenantId == count.TenantId && !v.DocumentRecord.IsDeleted && v.Status == "Published");
        if (!linked) throw new InvalidOperationException("The imported count sheet must belong to this count.");
        await AddCountActionAsync(count, PhysicalCountActionType.CountRecorded, userId, idempotencyKey,
            "Count sheet imported; previous sheets retained in history.", new { CountSheet = sheet }, "Counter");
        await _unitOfWork.SaveChangesAsync();
    }
}
