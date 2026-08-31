using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Immutable audit evidence for a source-document dimension mutation and any resulting budget
/// invalidation.  It records hashes, never mutable display names; exact display evidence remains
/// on the source assignment snapshot.
/// </summary>
public sealed class FinanceSourceDimensionChange : TenantEntity
{
    public FinanceDimensionRouteId RouteId { get; set; }
    [Required, MaxLength(50)] public string ProducerModule { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string SourceRoute { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string SourceDocumentType { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string ContractVersion { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceLineId { get; set; }
    public Guid? PreviousFinanceDimensionSetId { get; set; }
    public Guid? NewFinanceDimensionSetId { get; set; }
    public Guid? PreviousFinanceDimensionSnapshotId { get; set; }
    public Guid? NewFinanceDimensionSnapshotId { get; set; }
    [MaxLength(64)] public string? PreviousCombinationHash { get; set; }
    [MaxLength(64)] public string? NewCombinationHash { get; set; }
    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
    public bool BudgetEvidenceBecameStale { get; set; }
    [MaxLength(2000)] public string? ReleasedBudgetReservationIdsJson { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }

    public FinanceDimensionSnapshot? PreviousFinanceDimensionSnapshot { get; set; }
    public FinanceDimensionSnapshot? NewFinanceDimensionSnapshot { get; set; }
}
