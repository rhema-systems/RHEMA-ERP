namespace ErpSystem.Core.DTOs.Inventory;

public sealed record PhysicalCountSheetBinding(
    Guid CentralDocumentVersionId, string RequestHash, int SavedItems, int BlankItems);
