using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Entities.Inventory;

[Table("InventoryNegativeStockOverrides")]
public sealed class InventoryNegativeStockOverride : TenantEntity
{
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }
    [Required, MaxLength(100)] public string ReferenceType { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ReferenceNumber { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal AuthorizedQuantity { get; set; }

    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    [Required, MaxLength(64)] public string DecisionSnapshotHash { get; set; } = string.Empty;

    public Guid WorkflowInstanceId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public Guid ApprovedById { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public Guid? ConsumedByReferenceId { get; set; }
    public long? ConsumptionTransactionId { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public InventoryItem InventoryItem { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
    public ProcurementConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public ProcurementConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
}
