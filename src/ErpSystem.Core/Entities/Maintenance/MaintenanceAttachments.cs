using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR; // For Employee entity
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Maintenance;

public class MaintenanceAttachment : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [Required]
    public AttachmentType AttachmentType { get; set; } = AttachmentType.Photo;

    [Required]
    public AttachmentEntityType EntityType { get; set; } = AttachmentEntityType.WorkOrder;

    [Required]
    public Guid EntityId { get; set; }

    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid UploadedByUserId { get; set; }

    public bool IsMainImage { get; set; } = false;

    // Image-specific properties
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    
    [MaxLength(500)]
    public string? ThumbnailPath { get; set; }

    // GPS coordinates for photos taken on-site
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    [MaxLength(200)]
    public string? LocationDescription { get; set; }

    // Document-specific properties
    [MaxLength(50)]
    public string? DocumentVersion { get; set; }
    
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedDate { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UploadedByUserId))]
    public virtual Employee UploadedByUser { get; set; } = null!;

    // Tags for categorization
    public virtual ICollection<MaintenanceAttachmentTag> Tags { get; set; } = new List<MaintenanceAttachmentTag>();
}

public class MaintenanceAttachmentTag : BaseEntity
{
    [Required]
    public Guid AttachmentId { get; set; }

    [Required]
    [MaxLength(50)]
    public string TagName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TagValue { get; set; }

    // Navigation properties
    [ForeignKey(nameof(AttachmentId))]
    public virtual MaintenanceAttachment Attachment { get; set; } = null!;
}

public class WorkOrderAttachment : BaseEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid AttachmentId { get; set; }

    public WorkOrderAttachmentCategory AttachmentCategory { get; set; } = WorkOrderAttachmentCategory.Reference;

    public int DisplayOrder { get; set; } = 0;

    public bool IsRequired { get; set; } = false;

    // Navigation properties
    [ForeignKey(nameof(WorkOrderId))]
    public virtual WorkOrder WorkOrder { get; set; } = null!;

    [ForeignKey(nameof(AttachmentId))]
    public virtual MaintenanceAttachment Attachment { get; set; } = null!;
}

public class AssetAttachment : BaseEntity
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid AttachmentId { get; set; }

    public AssetAttachmentCategory AttachmentCategory { get; set; } = AssetAttachmentCategory.Photo;

    public int DisplayOrder { get; set; } = 0;

    public bool IsPublic { get; set; } = true;

    // Navigation properties
    [ForeignKey(nameof(AssetId))]
    public virtual MaintenanceAsset Asset { get; set; } = null!;

    [ForeignKey(nameof(AttachmentId))]
    public virtual MaintenanceAttachment Attachment { get; set; } = null!;
}

public class InspectionAttachment : BaseEntity
{
    [Required]
    public Guid InspectionId { get; set; }

    [Required]
    public Guid AttachmentId { get; set; }

    public InspectionAttachmentCategory AttachmentCategory { get; set; } = InspectionAttachmentCategory.Evidence;

    public int DisplayOrder { get; set; } = 0;

    public bool IsEvidence { get; set; } = false;
    public bool ShowsIssue { get; set; } = false;

    // Navigation properties
    [ForeignKey(nameof(InspectionId))]
    public virtual AssetInspection Inspection { get; set; } = null!;

    [ForeignKey(nameof(AttachmentId))]
    public virtual MaintenanceAttachment Attachment { get; set; } = null!;
}

// Entity for tracking attachment downloads/views
public class MaintenanceAttachmentAccess : BaseEntity
{
    [Required]
    public Guid AttachmentId { get; set; }

    [Required]
    public Guid AccessedByUserId { get; set; }

    public DateTime AccessedDate { get; set; } = DateTime.UtcNow;

    public AttachmentAccessType AccessType { get; set; } = AttachmentAccessType.View;

    [MaxLength(100)]
    public string? UserAgent { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    // Navigation properties
    [ForeignKey(nameof(AttachmentId))]
    public virtual MaintenanceAttachment Attachment { get; set; } = null!;

    [ForeignKey(nameof(AccessedByUserId))]
    public virtual Employee AccessedByUser { get; set; } = null!;
}