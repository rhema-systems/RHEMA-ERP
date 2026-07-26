using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Numbering;

public class DocumentSequenceDefinition : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Format { get; set; } = string.Empty;

    public long NextNumber { get; set; } = 1;

    public long StartNumber { get; set; } = 1;

    public int MinimumDigits { get; set; } = 5;

    [Required]
    [MaxLength(20)]
    public string ResetPolicy { get; set; } = DocumentSequenceResetPolicies.Never;

    [MaxLength(20)]
    public string? LastResetPeriodKey { get; set; }

    public bool IsContinuous { get; set; } = true;

    public bool AllowManualEntry { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; } = true;

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public virtual ICollection<DocumentNumberReservation> Reservations { get; set; } = new List<DocumentNumberReservation>();
}

public class DocumentNumberReservation : TenantEntity
{
    [Required]
    public Guid DocumentSequenceDefinitionId { get; set; }

    public virtual DocumentSequenceDefinition DocumentSequenceDefinition { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    public long SequenceNumber { get; set; }

    [MaxLength(20)]
    public string PeriodKey { get; set; } = "ALL";

    [MaxLength(100)]
    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = DocumentNumberReservationStatuses.Used;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; } = DateTime.UtcNow;

    public DateTime? VoidedAt { get; set; }

    [MaxLength(100)]
    public string? ReservedBy { get; set; }
}

public static class DocumentSequenceResetPolicies
{
    public const string Never = "Never";
    public const string Yearly = "Yearly";
    public const string Monthly = "Monthly";
}

public static class DocumentNumberReservationStatuses
{
    public const string Reserved = "Reserved";
    public const string Used = "Used";
    public const string Voided = "Voided";
    public const string Released = "Released";
}
