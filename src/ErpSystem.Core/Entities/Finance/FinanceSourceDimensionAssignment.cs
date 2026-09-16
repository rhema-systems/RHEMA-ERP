using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Module-neutral persistence for a Finance source document's canonical dimension assignment.
/// A null <see cref="SourceLineId"/> is the editable document default; a non-null value is the
/// authoritative economic-line assignment. Producer identity is copied from the compiled route,
/// never from a browser payload.
/// </summary>
public sealed class FinanceSourceDimensionAssignment : TenantEntity
{
    public FinanceDimensionRouteId RouteId { get; set; }

    [Required, MaxLength(50)]
    public string ProducerModule { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string SourceRoute { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string SourceDocumentType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ContractVersion { get; set; } = string.Empty;

    public Guid SourceDocumentId { get; set; }
    public Guid? SourceLineId { get; set; }

    /// <summary>
    /// Trusted economic account resolved by the producer service. Populated only for line rows;
    /// clients cannot select or overwrite it through a dimension payload.
    /// </summary>
    public Guid? ResolvedAccountId { get; set; }

    /// <summary>
    /// Effective accounting date captured on the header row for rule-drift/readiness evaluation.
    /// </summary>
    public DateTime? SourceDocumentDate { get; set; }

    /// <summary>
    /// Header-only evidence of the complete trusted economic-line context at the last draft save.
    /// </summary>
    public int? ExpectedSourceLineCount { get; set; }

    [MaxLength(64)]
    public string? SourceLineManifestHash { get; set; }
    /// <summary>
    /// Canonical assignment. It remains nullable for a clearable header default and for an
    /// explicitly captured CaptureOptional line with no supplied values. The assignment row,
    /// rather than a null set ID, distinguishes adapted evidence from a legacy/unadapted line.
    /// </summary>
    public Guid? FinanceDimensionSetId { get; set; }

    [Required, MaxLength(20)]
    public string BudgetEvidenceStatus { get; set; } = "NotApplicable";

    [MaxLength(64)]
    public string? BudgetEvaluationHash { get; set; }

    public DateTime? BudgetEvidenceUpdatedAt { get; set; }

    /// <summary>
    /// Exact line-level evidence frozen at submission/approval. Header defaults never receive a
    /// snapshot. Once populated, this assignment cannot be changed or cleared.
    /// </summary>
    public Guid? FinanceDimensionSnapshotId { get; set; }

    /// <summary>
    /// Freezes both populated and explicitly empty line evidence. A snapshot is present when a
    /// canonical set exists; EvidenceFrozenAt also protects the valid empty-set case.
    /// </summary>
    public DateTime? EvidenceFrozenAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [ForeignKey(nameof(FinanceDimensionSetId))]
    public FinanceDimensionSet? FinanceDimensionSet { get; set; }

    [ForeignKey(nameof(FinanceDimensionSnapshotId))]
    public FinanceDimensionSnapshot? FinanceDimensionSnapshot { get; set; }
}
