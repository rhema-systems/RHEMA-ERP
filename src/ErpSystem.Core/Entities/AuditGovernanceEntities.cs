using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

public enum AuditLifecycleActionType
{
    LegalHoldPlaced = 0,
    LegalHoldReleased = 1,
    Archived = 2,
    Restored = 3
}

public enum AuditOperationKind
{
    Other = 0,
    Create = 1,
    Update = 2,
    Approve = 3,
    Reject = 4,
    Override = 5,
    Post = 6,
    Reverse = 7,
    Dispatch = 8,
    Receive = 9
}

/// <summary>
/// Append-only governance overlay for any registered audit store. The audited record remains
/// untouched; legal-hold, archive, and restore state is derived from this immutable history.
/// </summary>
[Table("AuditRecordLifecycleEvents")]
public sealed class AuditRecordLifecycleEvent : TenantEntity
{
    [Required, StringLength(100)]
    public string StoreKey { get; set; } = string.Empty;

    public Guid RecordId { get; set; }
    public int SequenceNumber { get; set; }
    public AuditLifecycleActionType Action { get; set; }

    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RequestKey { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ArchiveReference { get; set; }

    public DateTime SourceOccurredAtUtc { get; set; }
    public DateTime RetainUntilUtc { get; set; }
    public Guid ActorUserId { get; set; }

    [Required, StringLength(300)]
    public string ActorName { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string ActorRolesJson { get; set; } = "[]";

    [Required, Column(TypeName = "nvarchar(max)")]
    public string RecordSnapshotJson { get; set; } = "{}";

    [StringLength(64)]
    public string? PreviousIntegrityHash { get; set; }

    [Required, StringLength(64)]
    public string IntegrityHash { get; set; } = string.Empty;

    public ApplicationUser ActorUser { get; set; } = null!;
}
