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
    public Guid FinanceDimensionSetId { get; set; }

    /// <summary>
    /// Exact line-level evidence frozen at submission/approval. Header defaults never receive a
    /// snapshot. Once populated, this assignment cannot be changed or cleared.
    /// </summary>
    public Guid? FinanceDimensionSnapshotId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [ForeignKey(nameof(FinanceDimensionSetId))]
    public FinanceDimensionSet FinanceDimensionSet { get; set; } = null!;

    [ForeignKey(nameof(FinanceDimensionSnapshotId))]
    public FinanceDimensionSnapshot? FinanceDimensionSnapshot { get; set; }
}
