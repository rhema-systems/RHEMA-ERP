using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryNegativeStockPolicyDto
{
    public Guid ConfigurationProfileId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    public ProcurementNegativeStockPolicy DefaultPolicy { get; set; }
    public bool EmergencyOverrideEligible { get; set; }
    public string OverridePermission { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public List<string> EvidenceRequirements { get; set; } = new();
    public int OverrideDurationHours { get; set; }
    public bool AuditRequired { get; set; }
}

public sealed class RegisterInventoryNegativeStockOverrideRequest
{
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }
    [Required, MaxLength(100)] public string ReferenceType { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ReferenceNumber { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.0001", "99999999999999")] public decimal AuthorizedQuantity { get; set; }
    [Required, MinLength(10), MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    public Guid WorkflowInstanceId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class InventoryNegativeStockOverrideDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal AuthorizedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int ConfigurationProfileVersion { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public Guid ApprovedById { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByReferenceId { get; set; }
    public bool IsAvailable { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class InventoryStockDecreaseRequest
{
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public decimal Quantity { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public Guid? NegativeStockOverrideId { get; set; }
    public bool DecreaseCurrentStock { get; set; } = true;
    public bool DecreaseAvailableStock { get; set; } = true;
    public bool CheckInventoryItemBalance { get; set; } = true;
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class InventoryStockDecreaseAuthorization
{
    public bool WouldBeNegative { get; set; }
    public bool EmergencyOverrideApplied { get; set; }
    public Guid? NegativeStockOverrideId { get; set; }
    public long? TransactionId { get; set; }
}
