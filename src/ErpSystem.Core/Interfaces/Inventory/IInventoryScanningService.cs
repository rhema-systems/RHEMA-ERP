using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryScanningService
{
    Task<IReadOnlyList<InventoryLabelProfileDto>> GetLabelProfilesAsync(CancellationToken cancellationToken = default);
    Task<InventoryLabelProfileDto> SaveLabelProfileAsync(Guid? id, SaveInventoryLabelProfileRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteLabelProfileAsync(Guid id, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryLabelCandidateDto>> SearchLabelCandidatesAsync(string? query, int take = 50, CancellationToken cancellationToken = default);
    Task<InventoryLabelPrintEventDto> RecordLabelPrintAsync(RecordInventoryLabelPrintRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryLabelPrintEventDto>> GetRecentPrintsAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryScanDocumentSummaryDto>> GetDocumentsAsync(InventoryScanOperation operation, int take = 50, CancellationToken cancellationToken = default);
    Task<InventoryScanDocumentContextDto> GetDocumentContextAsync(InventoryScanOperation operation, Guid documentId, CancellationToken cancellationToken = default);
    Task<InventoryScanBatchDto> SynchronizeAsync(SynchronizeInventoryScanBatchRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryScanBatchDto>> GetRecentBatchesAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<InventoryScanBatchDto> GetBatchAsync(Guid id, CancellationToken cancellationToken = default);
}
