using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Tenant-specific state for one compiled Finance dimension route definition.</summary>
public sealed class FinanceDimensionRouteCertification : TenantEntity
{
    public FinanceDimensionRouteId RouteId { get; set; }

    [Required, MaxLength(50)]
    public string ProducerModule { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string SourceRoute { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ContractVersion { get; set; } = string.Empty;

    public FinanceDimensionCertificationState State { get; set; } =
        FinanceDimensionCertificationState.LegacyReadOnly;

    public DateTime EffectiveDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<FinanceDimensionCertificationTransition> Transitions { get; set; } =
        new List<FinanceDimensionCertificationTransition>();
}

/// <summary>Append-only state transition history for route certification.</summary>
public sealed class FinanceDimensionCertificationTransition : TenantEntity
{
    [Required]
    public Guid FinanceDimensionRouteCertificationId { get; set; }

    public FinanceDimensionCertificationState PreviousState { get; set; }
    public FinanceDimensionCertificationState NewState { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public Guid ActorUserId { get; set; }
    public DateTime TransitionedAt { get; set; }
    public DateTime EffectiveDate { get; set; }
    public Guid? ReadinessAssessmentId { get; set; }

    [MaxLength(64)]
    public string? ReadinessEvidenceHash { get; set; }

    public bool IsEmergencyRollback { get; set; }

    public FinanceDimensionRouteCertification FinanceDimensionRouteCertification { get; set; } = null!;
    public FinanceDimensionReadinessAssessment? ReadinessAssessment { get; set; }
}

/// <summary>Immutable, expiring readiness evidence used by a single guarded promotion.</summary>
public sealed class FinanceDimensionReadinessAssessment : TenantEntity
{
    public FinanceDimensionRouteId RouteId { get; set; }

    [Required, MaxLength(50)]
    public string ProducerModule { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string SourceRoute { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ContractVersion { get; set; } = string.Empty;

    public FinanceDimensionCertificationState CurrentState { get; set; }
    public FinanceDimensionCertificationState TargetState { get; set; }
    public int BlockerCount { get; set; }

    [Required]
    public string BlockerResultsJson { get; set; } = "[]";

    [Required, MaxLength(200)]
    public string DataVersionWatermark { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string EvidenceHash { get; set; } = string.Empty;

    public DateTime AssessedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Guid AssessedByUserId { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
